using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.Request.ViewModels
{
    public sealed partial class RequestPageViewModel
    {
        private readonly string _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

        // ── Loading ──────────────────────────────────────────────────────────
        public void LoadRequests()
        {
            try
            {
                _allRows = _repository.GetAllRequests(
                    setCodeFilter: _setCodeFilter,
                    documentNumberFilter: _documentNumberFilter,
                    companyFilter: _companyFilter,
                    departmentFilter: _departmentFilter,
                    branchFilter: _branchFilter,
                    employeeFilter: _employeeFilter,
                    referenceCodeFilter: _referenceCodeFilter,
                    parentTagFilter: _parentTagFilter,
                    reqIdFilter: _reqIdFilter).Select(dto => new RequestRow(dto)).ToList();
                if (AllowedCategories != null)
                    _allRows = _allRows.Where(r => r.Category != null && AllowedCategories.Contains(r.Category)).ToList();
                if (RestrictToRequestSetManagementWorkflow)
                    _allRows = _allRows.Where(r => r.Dto.WorkflowType == "RequestSetManagement").ToList();
                LoadCategoryFilterOptions();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load requests: {ex.Message}");
            }
        }

        // ── Pagination ───────────────────────────────────────────────────────
        public int PageSize { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetField(ref _currentPage, value);
        }

        private string _pageInfoText = "Page 0 of 0 (0 requests)";
        public string PageInfoText
        {
            get => _pageInfoText;
            private set => SetField(ref _pageInfoText, value);
        }

        private int GetTotalPages()
        {
            var total = _filteredRows?.Count ?? 0;
            return total <= 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
        }

        private void BindPage()
        {
            foreach (var row in Rows) row.PropertyChanged -= Row_PropertyChanged;

            var src = _filteredRows ?? _allRows ?? new List<RequestRow>();
            var totalPages = GetTotalPages();

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 requests)";
                RecomputeHasAnySelected();
                return;
            }

            CurrentPage = totalPages > 0 ? Math.Max(1, Math.Min(CurrentPage, totalPages)) : 1;

            var pageItems = src.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Rows.Clear();
            foreach (var item in pageItems)
            {
                Rows.Add(item);
                item.PropertyChanged += Row_PropertyChanged;
            }

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} requests)";
            RecomputeHasAnySelected();
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RequestRow.Selected))
                RecomputeHasAnySelected();
        }

        // Called directly by the View's row-checkbox Checked/Unchecked handlers, in addition to
        // the Row_PropertyChanged subscription above — belt-and-suspenders against WPF DataGrid
        // container recycling occasionally not propagating the bound PropertyChanged reliably
        // for template-column checkboxes, which was undercounting SelectedCount in practice.
        public void RefreshSelectionState() => RecomputeHasAnySelected();

        private void RecomputeHasAnySelected()
        {
            int count = Rows.Count(r => r.Selected);
            HasAnySelectedOnPage = count > 0;
            SelectedCount = count;
            OnPropertyChanged(nameof(HasSelection));
        }

        // ── Mark Submitted ───────────────────────────────────────────────────
        private void MarkSelectedAsSubmitted()
        {
            var row = SelectedRow;
            if (row == null)
            {
                RequestWarning?.Invoke("No Selection", "Please select a request to mark as submitted.");
                return;
            }

            if (string.Equals(row.Category, "Cartridge", StringComparison.OrdinalIgnoreCase))
            {
                RequestWarning?.Invoke("Not Allowed",
                    "Cartridge requests cannot be submitted here.\n\nPlease use the Cartridge Management module to manage cartridge requests.");
                return;
            }

            if (row.Status == "Submitted")
            {
                RequestInfo?.Invoke("Already Submitted", "This request is already marked as Submitted.");
                return;
            }

            bool confirmed = ConfirmYesNo?.Invoke(
                "Confirm Submit",
                $"Mark this request as SUBMITTED?\n\n" +
                $"Employee: {row.EmployeeName}\n" +
                $"Item: {row.ItemName}\n" +
                $"Quantity: {row.Quantity}\n\n" +
                "This will:\n" +
                "✔ Change status to 'Submitted'\n" +
                "✔ Create an Inventory entry (ledger)\n" +
                "✔ Deduct from stock on hand\n\n" +
                "Continue?") ?? false;
            if (!confirmed) return;

            try
            {
                bool success = _repository.SubmitRequest(row.ReqId, AppSession.CurrentUserId);
                if (success)
                {
                    RequestInfo?.Invoke("Success",
                        "Request submitted successfully!\n\n✔ Status changed to Submitted\n✔ Inventory entry created\n✔ Stock updated");
                    LoadRequests();
                }
                else
                {
                    RequestError?.Invoke("Error", "Failed to submit request. Request not found.");
                }
            }
            catch (InvalidOperationException ex)
            {
                RequestWarning?.Invoke("Insufficient Stock", $"Cannot submit request:\n\n{ex.Message}\n\nPlease check stock availability.");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to submit request: {ex.Message}");
            }
        }

        // ── Edit (dialog invoked by the View; this commits the result) ───────
        public void CommitEditedRequest(RequestDto request, string originalStatus)
        {
            try
            {
                request.ModifiedByUserId = AppSession.CurrentUserId;

                if (!string.Equals(originalStatus, "Submitted", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(request.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
                {
                    bool success = _repository.SubmitRequest(request.ReqId, AppSession.CurrentUserId);
                    if (success)
                    {
                        RequestInfo?.Invoke("Success",
                            "Request submitted successfully!\n\n✔ Status changed to Submitted\n✔ Inventory entry created\n✔ Stock updated");
                    }
                }
                else
                {
                    _repository.UpdateRequest(request);
                    RequestInfo?.Invoke("Success", "Request updated successfully.");
                }

                LoadRequests();
            }
            catch (InvalidOperationException ex)
            {
                RequestWarning?.Invoke("Insufficient Stock", $"Cannot submit request:\n\n{ex.Message}\n\nPlease check stock availability.");
                LoadRequests();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to update request: {ex.Message}");
            }
        }

        // ── Archive (dialog invoked by the View; this commits the result) ────
        public void CommitArchive(List<RequestRow> rows, string reason, bool archiveItem)
        {
            int succeeded = 0;
            var errors = new List<string>();

            foreach (var row in rows)
            {
                try
                {
                    ArchiveRequestSql(row.ReqId, row.Dto.ItemId, reason, archiveItem);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Req #{row.ReqId}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                RequestWarning?.Invoke("Partial Success",
                    $"Archived {succeeded} of {rows.Count} request(s).\n\nErrors:\n" + string.Join("\n", errors));
            }
            else
            {
                string successMsg = rows.Count == 1
                    ? (archiveItem
                        ? "Request and associated item archived successfully!\n\nYou can view archived requests in the Archive page."
                        : "Request archived successfully!\n\nYou can view archived requests in the Archive page.")
                    : $"{succeeded} request(s) archived successfully!\n\nYou can view archived requests in the Archive page.";
                RequestInfo?.Invoke("Success", successMsg);
            }

            LoadRequests();
        }

        private void ArchiveRequestSql(int reqId, int itemId, string reason, bool archiveItem)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string insertArchiveSql = @"
                            INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                            VALUES ('Request', @ReqId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                        using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                            cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                            cmd.ExecuteNonQuery();
                        }

                        if (archiveItem)
                        {
                            const string archiveItemSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Item', @ItemId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(archiveItemSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", $"Archived with Request {reqId}: {reason}");
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // ── Delete (permanent, double-confirmed) ─────────────────────────────
        private async Task DeleteCheckedAsync()
        {
            var checkedRows = Rows.Where(r => r.Selected).ToList();
            if (checkedRows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one request to delete.");
                return;
            }

            try
            {
                var detailsList = new List<string>();
                foreach (var row in checkedRows)
                {
                    var (itemName, quantity, employeeName) = await _repository.GetRequestDetailsForDelete(row.ReqId);
                    detailsList.Add($"• {employeeName} - {itemName} (Qty: {quantity})");
                }

                string countText = checkedRows.Count == 1 ? "this request" : $"these {checkedRows.Count} requests";

                bool confirmed = ConfirmYesNo?.Invoke(
                    "Confirm Permanent Deletion",
                    "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                    $"This will PERMANENTLY delete {countText} and restore stock:\n\n" +
                    $"{string.Join("\n", detailsList)}\n\n" +
                    "Stock will be restored for all deleted requests.\n\n" +
                    "This action CANNOT be undone!\n\n" +
                    "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                    "⚠️ Use 'Archive' button instead for normal records.\n\n" +
                    "Are you absolutely sure you want to permanently delete?") ?? false;
                if (!confirmed) return;

                bool doubleConfirmed = ConfirmYesNo?.Invoke(
                    "Final Confirmation",
                    "FINAL CONFIRMATION\n\n" +
                    $"{checkedRows.Count} request(s) will be permanently deleted and cannot be recovered.\n\n" +
                    "Are you absolutely certain?") ?? false;
                if (!doubleConfirmed) return;

                try
                {
                    int successCount = 0;
                    foreach (var row in checkedRows)
                    {
                        bool success = await _repository.DeleteRequestAndRestoreStock(row.ReqId);
                        if (success) successCount++;
                    }

                    if (successCount > 0)
                    {
                        RequestInfo?.Invoke("Deleted Successfully",
                            $"{successCount} request(s) permanently deleted!\n\n✔ Requests removed from database\n✔ Stock restored for deleted requests");
                        LoadRequests();
                    }
                }
                catch (Exception ex)
                {
                    RequestError?.Invoke("Error", $"Error deleting request:\n\n{ex.Message}");
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error retrieving request details:\n\n{ex.Message}");
            }
        }

        // ── PDF export (checkbox-selected rows on the current page) ─────────
        private void ExportSelectedPdf()
        {
            var checkedRows = Rows.Where(r => r.Selected).ToList();
            if (checkedRows.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one request.");
                return;
            }

            var defaultFileName = $"SelectedRequests_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var path = RequestSaveFilePath?.Invoke(defaultFileName);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                RequestPdfGenerator.GenerateSelectedRequestsPdf(checkedRows.Select(r => r.Dto).ToList(), path, "Selected Requests");

                bool open = ConfirmYesNo?.Invoke("Export Complete", "PDF generated successfully.\n\nOpen it now?") ?? false;
                if (open) RequestPdfGenerator.TryOpen(path);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to generate PDF: {ex.Message}");
            }
        }

        // ── Bulk Add to Set (mirrors ItemsPageView's Bulk Add to Invoice) ───────
        private void BulkAddToSet()
        {
            var checkedRows = Rows.Where(r => r.Selected).ToList();
            if (checkedRows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one request to add to a Set.");
                return;
            }

            RequestBulkAddToSet?.Invoke(checkedRows.Select(r => r.ReqId).ToList());
        }
    }
}
