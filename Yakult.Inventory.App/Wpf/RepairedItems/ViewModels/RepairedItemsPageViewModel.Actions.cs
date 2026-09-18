using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RepairedItems.ViewModels
{
    /// <summary>Data loading and the Mark/Dispose/Sell/Export/History actions, ported verbatim
    /// from LoadDataAsync/MarkSelectedAsync/ExecuteLifecycleActionAsync/ExportToCsvAsync/
    /// OpenSelectedRepairHistory. Dialog interactions are bridged to the View via RequestXxx
    /// events/delegates (mirrors the pattern established by ItemsPageViewModel/AssetPageViewModel).</summary>
    public sealed partial class RepairedItemsPageViewModel
    {
        private RepairedItemRow _selectedRow;
        public RepairedItemRow SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewHistoryCommand { get; }
        public RelayCommand MarkRepairedCommand { get; }
        public RelayCommand MarkUnrepairedCommand { get; }
        public RelayCommand MarkSpareCommand { get; }
        public RelayCommand DisposeCommand { get; }
        public RelayCommand SellCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
        public RelayCommand CopySerialCommand { get; }
        public RelayCommand TotalCardClickCommand { get; }
        public RelayCommand RepairedCardClickCommand { get; }
        public RelayCommand UnrepairedCardClickCommand { get; }
        public RelayCommand SpareCardClickCommand { get; }
        public RelayCommand DamagedCardClickCommand { get; }

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<List<RepairedItemRow>, string> RequestMarkAction;
        public event Action<List<RepairedItemRow>, string> RequestSellDispose;
        public event Action<string> RequestViewRepairHistory;
        public event Func<string, string> RequestSaveFilePath;
        public Func<string, string, bool> ConfirmYesNo { get; set; }
        public Action<string> CopyToClipboard { get; set; }

        public async Task LoadDataAsync()
        {
            if (_isLoadingData) return;

            _isLoadingData = true;
            try
            {
                SetBusyState(true, "Loading repair items...");
                var list = new List<RepairedItemRow>();

                // Set-linked filters (see property declarations in the main partial for why these
                // are server-side only). Matched via a single EXISTS against one qualifying Set row
                // (all provided fields must match the same Set, not just any Set the item is in) —
                // same pattern as ItemsPageViewModel.Queries.cs's LoadItemsAsync. This is a SEPARATE
                // join from the `aset` derived table below: `aset` resolves only the item's CURRENT/
                // latest Set (for the "Set" grid column), while this EXISTS matches ANY qualifying
                // Set the item has ever been part of — different semantics, so both are kept.
                string setCodeFilter = SetCodeFilter;
                string documentNumberFilter = DocumentNumberFilter;
                string companyFilter = CompanyFilter;
                string departmentFilter = DepartmentFilter;
                string branchFilter = BranchFilter;
                string employeeFilter = EmployeeFilter;
                string referenceCodeFilter = ReferenceCodeFilter;
                string parentTagFilter = ParentTagFilter;
                string reqIdFilter = ReqIdFilter;

                var whereClauses = new List<string>
                {
                    "arch.EntityId IS NULL",
                    "(i.ItemType IS NULL OR LTRIM(RTRIM(i.ItemType)) = '' OR LOWER(LTRIM(RTRIM(i.ItemType))) = 'hardware')"
                };

                var setConditions = new List<string>();
                if (!string.IsNullOrWhiteSpace(setCodeFilter)) setConditions.Add("s.SetCode LIKE @SetCode");
                if (!string.IsNullOrWhiteSpace(documentNumberFilter)) setConditions.Add("s.DocumentNumber LIKE @DocumentNumber");
                if (!string.IsNullOrWhiteSpace(companyFilter)) setConditions.Add("co.Name LIKE @Company");
                if (!string.IsNullOrWhiteSpace(departmentFilter)) setConditions.Add("dept.Name LIKE @Department");
                if (!string.IsNullOrWhiteSpace(branchFilter)) setConditions.Add("br.Name LIKE @Branch");
                if (!string.IsNullOrWhiteSpace(employeeFilter)) setConditions.Add("emp.Name LIKE @Employee");
                if (!string.IsNullOrWhiteSpace(referenceCodeFilter)) setConditions.Add("si.ReferenceCode LIKE @ReferenceCode");
                if (!string.IsNullOrWhiteSpace(parentTagFilter)) setConditions.Add("ptg.Label LIKE @ParentTag");
                if (!string.IsNullOrWhiteSpace(reqIdFilter)) setConditions.Add("CAST(s.ReqId AS NVARCHAR(20)) LIKE @ReqId");

                if (setConditions.Count > 0)
                {
                    whereClauses.Add($@"EXISTS (
    SELECT 1 FROM dbo.SetItem si
    INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
    LEFT JOIN dbo.Company co ON co.ComId = s.ComId
    LEFT JOIN dbo.Branch br ON br.BranchId = s.CurrentBranchId
    LEFT JOIN dbo.Department dept ON dept.DeptId = s.CurrentDepartmentId
    LEFT JOIN dbo.Employee emp ON emp.EmpId = s.ReceivedById
    LEFT JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si.ParentTagGroupId
    WHERE si.ItemId = i.ItemId AND {string.Join(" AND ", setConditions)}
)");
                }

                string sql = $@"
SELECT
    i.ItemId,
    i.Name,
    i.Category,
    i.ModelNumber,
    i.SerialNumber,
    i.StockOnHand,
    i.Active,
    i.ConditionId,
    c.ConditionName,
    ISNULL(rh.RepairCount, 0) AS RepairCount,
    lr.RepairAction AS LastRepairAction,
    lr.CreatedAt AS LastRepairAt,
     i.DurationStartDate,
    aset.SetCode AS CurrentSetCode,
    lr.Remark AS LastRepairRemark,
    lr.ProcessedByName AS LastRepairProcessedByName,
    su.Source AS LastRepairSourceRaw
FROM dbo.Item i
LEFT JOIN dbo.Condition c ON c.ConditionId = i.ConditionId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (
    SELECT
        SerialNumber,
        COUNT(*) AS RepairCount
    FROM dbo.ItemRepairHistory
    WHERE SerialNumber IS NOT NULL
    GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN (
    SELECT
         SerialNumber,
         UpdateId,
         RepairAction,
         CreatedAt,
         Remark,
         ProcessedByName,
         ROW_NUMBER() OVER (
             PARTITION BY SerialNumber
             ORDER BY CreatedAt DESC, ISNULL(UpdateId, 0) DESC, ISNULL(ItemId, 0) DESC
         ) AS rn
     FROM dbo.ItemRepairHistory
     WHERE SerialNumber IS NOT NULL
 ) lr ON lr.SerialNumber = i.SerialNumber AND lr.rn = 1
 LEFT JOIN dbo.SetItemUpdate su ON su.UpdateId = lr.UpdateId
 LEFT JOIN (
     SELECT
         x.ItemId,
         x.SetCode,
        ROW_NUMBER() OVER (
            PARTITION BY x.ItemId
            ORDER BY x.SetCreatedAt DESC, x.SetId DESC
        ) AS rn
    FROM (
        SELECT
            si.ItemId,
            s.SetId,
            s.SetCode,
            s.CreatedAt AS SetCreatedAt
        FROM dbo.SetItem si
        INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE s.Active = 1
          AND archS.EntityId IS NULL

        UNION ALL

        SELECT
            r.ItemId,
            s.SetId,
            s.SetCode,
            s.CreatedAt AS SetCreatedAt
        FROM dbo.Request r
        INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE r.Active = 1
          AND r.SetId IS NOT NULL
          AND s.Active = 1
          AND archS.EntityId IS NULL
    ) x
) aset ON aset.ItemId = i.ItemId AND aset.rn = 1
WHERE {string.Join(" AND ", whereClauses)}
ORDER BY
    CASE WHEN lr.CreatedAt IS NULL THEN 1 ELSE 0 END,
    lr.CreatedAt DESC,
    i.ItemId DESC;";

                using (var con = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(sql, con))
                {
                    if (!string.IsNullOrWhiteSpace(setCodeFilter))
                        cmd.Parameters.AddWithValue("@SetCode", $"%{setCodeFilter}%");
                    if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                        cmd.Parameters.AddWithValue("@DocumentNumber", $"%{documentNumberFilter}%");
                    if (!string.IsNullOrWhiteSpace(companyFilter))
                        cmd.Parameters.AddWithValue("@Company", $"%{companyFilter}%");
                    if (!string.IsNullOrWhiteSpace(departmentFilter))
                        cmd.Parameters.AddWithValue("@Department", $"%{departmentFilter}%");
                    if (!string.IsNullOrWhiteSpace(branchFilter))
                        cmd.Parameters.AddWithValue("@Branch", $"%{branchFilter}%");
                    if (!string.IsNullOrWhiteSpace(employeeFilter))
                        cmd.Parameters.AddWithValue("@Employee", $"%{employeeFilter}%");
                    if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                        cmd.Parameters.AddWithValue("@ReferenceCode", $"%{referenceCodeFilter}%");
                    if (!string.IsNullOrWhiteSpace(parentTagFilter))
                        cmd.Parameters.AddWithValue("@ParentTag", $"%{parentTagFilter}%");
                    if (!string.IsNullOrWhiteSpace(reqIdFilter))
                        cmd.Parameters.AddWithValue("@ReqId", $"%{reqIdFilter}%");

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new RepairedItemRow
                            {
                                ItemId = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ModelNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                                SerialNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                StockOnHand = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                Active = !reader.IsDBNull(6) && reader.GetBoolean(6),
                                ConditionId = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                                ConditionName = reader.IsDBNull(8) ? null : reader.GetString(8),
                                RepairCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                LastRepairAction = reader.IsDBNull(10) ? null : reader.GetString(10),
                                LastRepairAt = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                                DurationStartDate = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12),
                                CurrentSetCode = reader.IsDBNull(13) ? null : reader.GetString(13),
                                LastRepairRemark = reader.IsDBNull(14) ? null : reader.GetString(14),
                                LastRepairProcessedByName = reader.IsDBNull(15) ? null : reader.GetString(15),
                                LastRepairSourceRaw = reader.IsDBNull(16) ? null : reader.GetString(16)
                            });
                        }
                    }
                }

                _allRows = list;
                LoadCategories();
                LoadConditions();
                ApplyDeferredStateSelections();
                UpdateSummaryCounts();
                ApplyFilters(resetToFirstPage: true);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error", "Failed to load repaired items: " + ex.Message);
                _allRows = new List<RepairedItemRow>();
                LoadCategories();
                LoadConditions();
                UpdateSummaryCounts();
                ApplyFilters(resetToFirstPage: true);
            }
            finally
            {
                _isLoadingData = false;
                SetBusyState(false);
            }
        }

    /// <summary>Rows targeted by the bulk actions (Mark/Dispose/Sell): checked rows on the
    /// current page if any are checked, otherwise falls back to the single grid-selected row
    /// so the buttons keep working for users who click a row instead of checking it.</summary>
    private List<RepairedItemRow> GetTargetRows()
    {
        var allPaged = PagedRows.ToList();
        var checkedRows = allPaged.Where(r => r != null && r.Selected).ToList();
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogBatch.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetTargetRows totalPaged={allPaged.Count} checked={checkedRows.Count} selectedFlags={string.Join(",", allPaged.Select(r => r?.Selected.ToString() ?? "null"))}\r\n"); } catch { }
        if (checkedRows.Count > 0) return checkedRows;
        return SelectedRow != null ? new List<RepairedItemRow> { SelectedRow } : new List<RepairedItemRow>();
    }

    private void CopySelectedSerial()
    {
        var row = SelectedRow;
        if (row == null || string.IsNullOrWhiteSpace(row.SerialNumber)) return;
        CopyToClipboard?.Invoke(row.SerialNumber);
    }

        private void OpenSelectedRepairHistory()
        {
            var row = SelectedRow;
            if (row == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select an item first.");
                return;
            }
            if (string.IsNullOrWhiteSpace(row.SerialNumber))
            {
                RequestInfo?.Invoke("Not Available", "Repair history is tracked by Serial Number. The selected row has no serial.");
                return;
            }

            RequestViewRepairHistory?.Invoke(row.SerialNumber);
        }

        private async Task MarkSelectedAsync(string repairAction)
        {
            var rows = GetTargetRows();
            if (rows.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please check one or more items first.");
                return;
            }

            var withSerial = rows.Where(r => !string.IsNullOrWhiteSpace(r.SerialNumber)).ToList();
            var skippedNoSerial = rows.Count - withSerial.Count;
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogBatch.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MarkSelected checked={rows.Count} withSerial={withSerial.Count} skippedNoSerial={skippedNoSerial}\r\n"); } catch { }
            if (withSerial.Count == 0)
            {
                RequestInfo?.Invoke("Not Available", "Repair history is tracked by Serial Number. The selected item(s) have no serial.");
                return;
            }
            if (skippedNoSerial > 0)
            {
                RequestWarning?.Invoke("Some Items Skipped",
                    $"{skippedNoSerial} checked item(s) have no serial number and will be skipped. Repair history is tracked by Serial Number.\n\nContinuing with {withSerial.Count} item(s).");
            }

            var actionLabel = string.IsNullOrWhiteSpace(repairAction) ? "Repaired" : repairAction.Trim();

            if (withSerial.Count == 1
                && string.Equals((withSerial[0].LastRepairAction ?? string.Empty).Trim(), actionLabel, StringComparison.OrdinalIgnoreCase))
            {
                bool dup = ConfirmYesNo?.Invoke("Already Marked",
                    "This item is already marked with the same latest action.\n\nDo you still want to add another history entry?") ?? false;
                if (!dup) return;
            }

            RequestMarkAction?.Invoke(withSerial, actionLabel);
        }

        /// <summary>Called by the View after MarkActionDialog returns OK. The dialog is shown once
        /// (seeded from the first checked item) and its remark/call-ticket choices are applied to
        /// every checked item's own repair history entry.</summary>
        public async Task ConfirmAndSaveMarkActionAsync(List<RepairedItemRow> rows, string actionLabel, string remark,
            bool resolveCallTicket, int? callTicketId, string resolveStatus, string reasonText, string extraTicketNote)
        {
            if (rows == null || rows.Count == 0) return;

            var confirmRemark = string.IsNullOrWhiteSpace(remark) ? "(no remark)" : remark.Trim();
            var itemSummary = rows.Count == 1
                ? $"Item: {rows[0].Name}\nSerial: {(string.IsNullOrWhiteSpace(rows[0].SerialNumber) ? "-" : rows[0].SerialNumber)}\nCondition: {rows[0].ConditionName}"
                : $"{rows.Count} items selected";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Repair Action",
                $"Add this repair history entry?\n\nAction: {actionLabel}\n{itemSummary}\nRemark: {confirmRemark}") ?? false;
            if (!confirm) return;

            try
            {
                SetBusyState(true, "Saving repair history...");

                var userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                var userName = string.IsNullOrWhiteSpace(AppSession.CurrentUserName) ? "System" : AppSession.CurrentUserName;

                foreach (var row in rows)
                {
                    await _repairHistoryRepository.LogManualRepairActionAsync(
                        itemId: row.ItemId,
                        serialNumber: row.SerialNumber,
                        setId: null,
                        setCode: null,
                        previousStatus: row.LastRepairAction,
                        newStatus: actionLabel,
                        conditionId: row.ConditionId > 0 ? (int?)row.ConditionId : null,
                        conditionName: row.ConditionName,
                        processedByUserId: userId,
                        processedByName: userName,
                        repairAction: actionLabel,
                        remark: remark);

                    if (resolveCallTicket && callTicketId.HasValue && callTicketId.Value > 0)
                    {
                        try
                        {
                            var callRepo = new CallMonitoringRepository();
                            if (await callRepo.CallSchemaExistsAsync())
                            {
                                var ticketNote =
                                    $"Replacement item action from Repair Items page\n" +
                                    $"Action: {actionLabel}\n" +
                                    $"ItemId: {row.ItemId}\n" +
                                    $"Serial: {row.SerialNumber}\n" +
                                    $"Reason: {reasonText}" +
                                    (!string.IsNullOrWhiteSpace(extraTicketNote) ? $"\nNote: {extraTicketNote}" : string.Empty);

                                await callRepo.AddTicketNoteAsync(callTicketId.Value, "ReplacementItem", ticketNote, userId);
                                await callRepo.SetTicketStatusAsync(callTicketId.Value, resolveStatus, userId, "Resolved via replacement item (Repair Items page).");
                            }
                        }
                        catch (Exception ex)
                        {
                            RequestWarning?.Invoke("Call Monitoring Update Failed",
                                "Repair history was saved, but updating the Call Monitoring ticket failed:\n\n" + ex.Message);
                        }
                    }
                }

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error", $"Failed to mark item(s): {ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async Task ExecuteLifecycleActionAsync(string action)
        {
            var rows = GetTargetRows();
            if (rows.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please check one or more items first.");
                return;
            }

            RequestSellDispose?.Invoke(rows, action);
        }

        /// <summary>Called by the View after SellDisposeDialog returns OK. The dialog is shown once
        /// (seeded from the first checked item) and its quantity/recipient/amount/remarks are
        /// applied identically to every checked item.</summary>
        public async Task ConfirmAndExecuteLifecycleActionAsync(List<RepairedItemRow> rows, string action, int quantity, string recipientName, decimal? saleAmount, string remarks)
        {
            if (rows == null || rows.Count == 0) return;

            var itemSummary = rows.Count == 1
                ? $"Item: {rows[0].Name}\nSerial: {(string.IsNullOrWhiteSpace(rows[0].SerialNumber) ? "—" : rows[0].SerialNumber)}"
                : $"{rows.Count} items selected";

            bool confirm = ConfirmYesNo?.Invoke("Confirm",
                $"This will mark the item(s) as {action} and archive them.\n\n{itemSummary}\nQty: {quantity}\n\nContinue?") ?? false;
            if (!confirm) return;

            try
            {
                SetBusyState(true, $"Applying {action} action...");

                var userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                var userName = string.IsNullOrWhiteSpace(AppSession.CurrentUserName) ? "System" : AppSession.CurrentUserName;

                foreach (var row in rows)
                {
                    await _lifecycleDecisionRepository.ExecuteSellOrDisposeAsync(
                        itemId: row.ItemId,
                        action: action,
                        decidedByUserId: userId,
                        decidedByDisplayName: userName,
                        quantity: quantity,
                        recipientName: recipientName,
                        saleAmount: saleAmount,
                        remarks: remarks);
                }

                RequestInfo?.Invoke("Done", $"{rows.Count} item(s) marked as {action} and archived.");

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error", $"Failed to execute action: {ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private static string Csv(string value)
        {
            var v = value ?? string.Empty;
            v = v.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        private async Task ExportToCsvAsync()
        {
            if (_isExportingCsv) return;

            try
            {
                var rows = _filteredRows ?? new List<RepairedItemRow>();
                if (rows.Count == 0)
                {
                    RequestInfo?.Invoke("Export CSV", "No data to export.");
                    return;
                }

                var defaultFileName = $"RepairItems_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var path = RequestSaveFilePath?.Invoke(defaultFileName);
                if (string.IsNullOrEmpty(path)) return;

                _isExportingCsv = true;
                SetBusyState(true, "Exporting current repair items view to CSV...");

                var csv = await Task.Run(() =>
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("ItemId,Name,Category,ModelNumber,SerialNumber,Condition,Repair,Repairs,LastAction,LastRepairAt,Origin,ProcessedBy,Remark,Spare,Location,Set,Stock,Active");

                    foreach (var r in rows.Where(x => x != null))
                    {
                        builder.AppendLine(
                            $"{r.ItemId}," +
                            $"{Csv(r.Name)}," +
                            $"{Csv(r.Category)}," +
                            $"{Csv(r.ModelNumber)}," +
                            $"{Csv(r.SerialNumber)}," +
                            $"{Csv(r.ConditionName)}," +
                            $"{Csv(r.RepairStatus)}," +
                            $"{r.RepairCount}," +
                            $"{Csv(r.LastRepairAction)}," +
                            $"{Csv(r.LastRepairAt.HasValue ? r.LastRepairAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty)}," +
                            $"{Csv(r.LastRepairOrigin)}," +
                            $"{Csv(r.LastRepairProcessedByName)}," +
                            $"{Csv(r.LastRepairRemarkOneLine)}," +
                            $"{Csv(r.SpareLabel)}," +
                            $"{Csv(r.LocationLabel)}," +
                            $"{Csv(r.CurrentSetCode)}," +
                            $"{r.StockOnHand}," +
                            $"{Csv(r.Active ? "Yes" : "No")}");
                    }

                    return builder.ToString();
                });

                await Task.Run(() => File.WriteAllText(path, csv, Encoding.UTF8));
                RequestInfo?.Invoke("Export Complete", $"Exported {rows.Count} item(s) to CSV successfully.");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Export Error", $"Error exporting to CSV: {ex.Message}");
            }
            finally
            {
                _isExportingCsv = false;
                SetBusyState(false);
            }
        }
    }
}
