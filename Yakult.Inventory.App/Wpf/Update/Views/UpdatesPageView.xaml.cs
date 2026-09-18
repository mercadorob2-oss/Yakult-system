using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.ItemAudit;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using Yakult.Inventory.App.WPF.Update.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Update.Views
{
    /// <summary>
    /// Code-behind for the Mobile Updates page. The row actions below (View Details, Mark
    /// Processed, Delete, Audit Timeline, Configure API) are copied verbatim from
    /// Pages\Update\ViewUpdatesPage.cs — only the WinForms-control references
    /// (dgvUpdates.SelectedRows, FindForm(), progressBar) were mechanically swapped for their
    /// WPF equivalents. The underlying SQL/business logic (item auto-creation, condition/repair
    /// handling, audit trail entries) is unchanged.
    /// </summary>
    public partial class UpdatesPageView : UserControl
    {
        private readonly UpdatesPageViewModel _vm;
        private readonly SetItemUpdateRepository _repository = new SetItemUpdateRepository();
        private readonly AuditRepository _auditRepository = new AuditRepository();
        private readonly ItemRepairHistoryRepository _repairHistoryRepository = new ItemRepairHistoryRepository();

        public UpdatesPageView()
        {
            InitializeComponent();

            _vm = new UpdatesPageViewModel();
            DataContext = _vm;
            _vm.ErrorOccurred += msg => WinForms.MessageBox.Show(GetOwner(), $"Failed to load updates: {msg}", "Error",
                WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);

            RebuildSortByOptions();
            _ = _vm.LoadDataAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private string GetConnectionString() => DatabaseConfig.ConnectionString;

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(UpdatesGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(UpdatesGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void UpdatesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key, e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending);

            foreach (var col in UpdatesGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;
        }

        private void UpdatesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedUpdateDetails();

        private void BtnDatePreset_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out var days))
                _vm.ApplyDatePreset(days);
        }

        private void BtnViewDetails_Click(object sender, System.Windows.RoutedEventArgs e) => OpenSelectedUpdateDetails();

        private async void BtnMarkProcessed_Click(object sender, System.Windows.RoutedEventArgs e) => await MarkSelectedAsProcessedAsync();

        private async void BtnDelete_Click(object sender, System.Windows.RoutedEventArgs e) => await DeleteSelectedUpdatesAsync();

        private async void BtnAuditTimeline_Click(object sender, System.Windows.RoutedEventArgs e) => await OpenAuditTimelineForSelectedAsync();

        private void BtnConfigureApi_Click(object sender, System.Windows.RoutedEventArgs e) => ShowConfigureApiDialog();

        private List<SetItemUpdateDto> GetSelectedUpdateDtos() => UpdatesGrid.SelectedItems.Cast<SetItemUpdateDto>().ToList();

        private SetItemUpdateDto GetSelectedUpdateDto() => _vm.SelectedRow ?? GetSelectedUpdateDtos().FirstOrDefault();

        // ── Row actions (verbatim from Pages\Update\ViewUpdatesPage.cs) ─────────

        private void OpenSelectedUpdateDetails()
        {
            var dto = GetSelectedUpdateDto();
            if (dto == null)
            {
                WinForms.MessageBox.Show("Please select an update first.", "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SetItemUpdateDetailsDialog(dto))
            {
                dialog.ShowDialog(GetOwner());
            }
        }

        private void ShowConfigureApiDialog()
        {
            var currentUrl = AppConfig.ApiBaseUrl;

            string currentIp = "192.168.27.124";
            string currentPort = "7000";
            if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var existing))
            {
                currentIp = existing.Host;
                currentPort = existing.Port.ToString();
            }

            using (var dlg = new WinForms.Form())
            {
                dlg.Text = "Configure API Connection";
                dlg.Size = new Size(340, 220);
                dlg.StartPosition = WinForms.FormStartPosition.CenterParent;
                dlg.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                int labelX = 20, fieldX = 120, rowH = 36, startY = 20;

                var lblIp = new WinForms.Label
                {
                    Text = "IP Address",
                    Location = new Point(labelX, startY + 4),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5F)
                };

                var txtIp = new WinForms.TextBox
                {
                    Text = currentIp,
                    Location = new Point(fieldX, startY),
                    Width = 180,
                    Font = new Font("Segoe UI", 10F)
                };

                var lblPort = new WinForms.Label
                {
                    Text = "Port",
                    Location = new Point(labelX, startY + rowH + 4),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5F)
                };

                var txtPort = new WinForms.TextBox
                {
                    Text = currentPort,
                    Location = new Point(fieldX, startY + rowH),
                    Width = 80,
                    Font = new Font("Segoe UI", 10F)
                };

                var btnSave = new WinForms.Button
                {
                    Text = "Save",
                    DialogResult = WinForms.DialogResult.OK,
                    Location = new Point(fieldX, startY + rowH * 3),
                    Size = new Size(85, 32),
                    FlatStyle = WinForms.FlatStyle.Flat,
                    BackColor = Color.FromArgb(41, 128, 185),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
                };
                btnSave.FlatAppearance.BorderSize = 0;

                var btnCancel = new WinForms.Button
                {
                    Text = "Cancel",
                    DialogResult = WinForms.DialogResult.Cancel,
                    Location = new Point(fieldX + 95, startY + rowH * 3),
                    Size = new Size(85, 32),
                    FlatStyle = WinForms.FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9F)
                };

                dlg.Controls.AddRange(new WinForms.Control[] { lblIp, txtIp, lblPort, txtPort, btnSave, btnCancel });
                dlg.AcceptButton = btnSave;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(GetOwner()) != WinForms.DialogResult.OK)
                    return;

                var ip = txtIp.Text.Trim();
                var portStr = txtPort.Text.Trim();

                if (string.IsNullOrWhiteSpace(ip))
                {
                    WinForms.MessageBox.Show("IP Address cannot be empty.", "Validation", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }

                if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
                {
                    WinForms.MessageBox.Show("Please enter a valid port number (1-65535).", "Validation", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }

                var newUrl = $"http://{ip}:{port}";
                try
                {
                    AppConfig.SetApiBaseUrl(newUrl);
                    WinForms.MessageBox.Show($"API connection updated to:\n{newUrl}\n\nThe change takes effect immediately.", "Saved", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    _ = _vm.LoadDataAsync();
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Failed to save API connection:\n{ex.Message}", "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                }
            }
        }

        private async Task OpenAuditTimelineForSelectedAsync()
        {
            var dto = GetSelectedUpdateDto();
            if (dto == null)
            {
                WinForms.MessageBox.Show("Please select an update first.", "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            var serial = (dto.SerialNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(serial))
            {
                WinForms.MessageBox.Show("Selected update has no Serial Number. Audit timeline requires a serial number.", "Missing Serial", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            using (var form = new WinForms.Form())
            {
                form.Text = $"Item Movement Audit — {serial}";
                form.StartPosition = WinForms.FormStartPosition.CenterParent;
                form.Size = new Size(1200, 720);
                form.MinimizeBox = false;
                form.MaximizeBox = true;

                var page = new Yakult.Inventory.App.Pages.ItemAudit.ViewItemMovementAuditPage(autoLoad: false)
                {
                    Dock = WinForms.DockStyle.Fill
                };
                form.Controls.Add(page);

                form.Shown += async (_, __) =>
                {
                    try
                    {
                        await page.LoadTimelineForSerialAsync(serial);
                    }
                    catch (Exception ex)
                    {
                        WinForms.MessageBox.Show(ex.Message, "Audit Timeline", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    }
                };

                var owner = GetOwner();
                if (owner != null)
                    form.ShowDialog(owner);
                else
                    form.ShowDialog();
            }
        }

        private async Task MarkSelectedAsProcessedAsync()
        {
            var selectedDtos = GetSelectedUpdateDtos();
            if (selectedDtos.Count == 0)
            {
                WinForms.MessageBox.Show("Please select at least one update to mark as processed.", "No Selection",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new MarkAsProcessedDialog(selectedDtos))
            {
                var owner = GetOwner();
                var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                if (result != WinForms.DialogResult.OK)
                {
                    return;
                }

                var selectedConditionId = dialog.SelectedConditionId;
                var selectedConditionName = dialog.SelectedConditionName;
                var selectedRepairAction = dialog.SelectedRepairAction;
                var updatesToProcess = dialog.UpdatesToProcess;

                if (!updatesToProcess.Any())
                {
                    WinForms.MessageBox.Show("No valid updates to process.", "No Updates", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    var processedById = AppSession.CurrentUserId;
                    var processedByName = AppSession.CurrentUserName;
                    var auditEntries = new List<AuditEntry>();
                    var selectedUpdates = new List<int>();
                    var selectedDtosForProcessing = new List<SetItemUpdateDto>();
                    var processedCount = 0;
                    var errorCount = 0;
                    var originalSelectionCount = updatesToProcess.Count;
                    string repairAction = null;

                    if (!string.IsNullOrWhiteSpace(selectedRepairAction) &&
                        !selectedRepairAction.StartsWith("None", StringComparison.OrdinalIgnoreCase))
                    {
                        repairAction = selectedRepairAction.Trim();
                    }

                    foreach (var dto in updatesToProcess)
                    {
                        selectedUpdates.Add(dto.UpdateId);
                        selectedDtosForProcessing.Add(dto);

                        var auditEntry = new AuditEntry
                        {
                            Action = "MarkAsProcessed",
                            EntityId = dto.UpdateId,
                            EntityType = "SetItemUpdate",
                            UserId = processedById,
                            UserName = processedByName,
                            Timestamp = DateTime.Now,
                            Notes = "Marked as processed from desktop application",
                            OldValues = JsonConvert.SerializeObject(new
                            {
                                dto.SetCode,
                                dto.SerialNumber,
                                dto.ModelNumber,
                                dto.PreviousStatus,
                                dto.NewStatus,
                                dto.Remark,
                                dto.Processed,
                                dto.ProcessedBy,
                                dto.ProcessedAt
                            })
                        };
                        auditEntries.Add(auditEntry);
                    }

                    // Apply the selected condition to items and capture which updates were actually applied
                    var appliedUpdateIds = await ApplyConditionToItemsAsync(selectedDtosForProcessing, selectedConditionId, processedById, processedByName, repairAction);

                    if (appliedUpdateIds == null || appliedUpdateIds.Count == 0)
                    {
                        WinForms.MessageBox.Show(
                            "None of the selected updates could be matched to items in the master list.\n" +
                            "Make sure the items exist and their serial numbers match before processing.",
                            "No items updated",
                            WinForms.MessageBoxButtons.OK,
                            WinForms.MessageBoxIcon.Information);

                        await _vm.LoadDataAsync();
                        return;
                    }

                    // Only mark as processed the updates that were successfully applied to items
                    selectedUpdates = selectedUpdates
                        .Where(id => appliedUpdateIds.Contains(id))
                        .ToList();

                    auditEntries = auditEntries
                        .Where(a => a.EntityId.HasValue && appliedUpdateIds.Contains(a.EntityId.Value))
                        .ToList();

                    var skippedCount = originalSelectionCount - selectedUpdates.Count;

                    // If a repair action was selected, log repair history entries
                    if (!string.IsNullOrWhiteSpace(repairAction))
                    {
                        var appliedDtos = selectedDtosForProcessing
                            .Where(d => appliedUpdateIds.Contains(d.UpdateId))
                            .ToList();

                        if (appliedDtos.Count > 0)
                        {
                            await _repairHistoryRepository.LogRepairsAsync(
                                appliedDtos,
                                selectedConditionId,
                                selectedConditionName,
                                processedById,
                                processedByName,
                                repairAction);
                        }
                    }

                    const int batchSize = 10;
                    for (int i = 0; i < selectedUpdates.Count; i += batchSize)
                    {
                        var batch = selectedUpdates.Skip(i).Take(batchSize).ToList();
                        var batchAuditEntries = auditEntries.Skip(i).Take(batchSize).ToList();

                        try
                        {
                            var batchResult = await _repository.MarkAsProcessedBatchAsync(batch, processedById, processedByName);
                            processedCount += batchResult;

                            foreach (var auditEntry in batchAuditEntries)
                            {
                                auditEntry.NewValues = JsonConvert.SerializeObject(new
                                {
                                    Processed = true,
                                    ProcessedBy = processedByName,
                                    ProcessedAt = DateTime.Now,
                                    ConditionId = selectedConditionId,
                                    ConditionName = selectedConditionName
                                });
                                await _auditRepository.LogAsync(auditEntry);
                            }

                            await LogConditionChangeAuditAsync(
                                selectedDtosForProcessing,
                                batch,
                                appliedUpdateIds,
                                selectedConditionName,
                                repairAction,
                                processedByName);
                        }
                        catch (Exception ex)
                        {
                            errorCount += batch.Count;
                            foreach (var auditEntry in batchAuditEntries)
                            {
                                auditEntry.Notes = "Error marking as processed: " + ex.Message;
                                auditEntry.NewValues = JsonConvert.SerializeObject(new { Error = ex.Message });
                                await _auditRepository.LogAsync(auditEntry);
                            }
                        }
                    }

                    string message = $"Successfully processed {processedCount} update(s).";
                    if (skippedCount > 0)
                    {
                        message += $"\n{skippedCount} update(s) were skipped because no matching item was found. They remain unprocessed.";
                    }
                    if (errorCount > 0)
                    {
                        message += $"\nFailed to process {errorCount} update(s). Check audit log for details.";
                    }

                    WinForms.MessageBox.Show(message, "Processing Complete",
                        WinForms.MessageBoxButtons.OK,
                        errorCount == 0 ? WinForms.MessageBoxIcon.Information : WinForms.MessageBoxIcon.Warning);

                    await _vm.LoadDataAsync();
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show("An error occurred while processing updates: " + ex.Message, "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private async Task DeleteSelectedUpdatesAsync()
        {
            var selectedDtos = GetSelectedUpdateDtos();
            if (selectedDtos.Count == 0)
            {
                WinForms.MessageBox.Show("Please select at least one update to delete.", "No Selection",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            var confirm = WinForms.MessageBox.Show(
                $"Delete {selectedDtos.Count} selected update(s)?\n\n" +
                "This only removes them from the Mobile Updates list. Items and inventory are not changed.",
                "Confirm Delete",
                WinForms.MessageBoxButtons.YesNo,
                WinForms.MessageBoxIcon.Warning);

            if (confirm != WinForms.DialogResult.Yes)
                return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var selectedIds = selectedDtos.Select(dto => dto.UpdateId).ToList();

                if (selectedIds.Count == 0)
                {
                    WinForms.MessageBox.Show("No valid updates were found in the selection.", "Nothing to Delete",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    return;
                }

                var idList = string.Join(",", selectedIds);
                var sql = @"DELETE FROM dbo.SetItemUpdate
WHERE UpdateId IN (SELECT value FROM STRING_SPLIT(@UpdateIds, ','));";

                int affected;
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UpdateIds", idList);

                    await con.OpenAsync();
                    affected = await cmd.ExecuteNonQueryAsync();
                }

                WinForms.MessageBox.Show($"Deleted {affected} update(s).", "Delete Complete",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

                await _vm.LoadDataAsync();
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show("Failed to delete updates: " + ex.Message, "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<HashSet<int>> ApplyConditionToItemsAsync(List<SetItemUpdateDto> updates, int? conditionId, int processedByUserId, string processedByUserName, string repairAction)
        {
            var appliedUpdateIds = new HashSet<int>();

            if (updates == null || updates.Count == 0)
            {
                return appliedUpdateIds;
            }

            var isSpareToInventory = !string.IsNullOrWhiteSpace(repairAction)
                && repairAction.Equals("Repaired - Spare inventory", StringComparison.OrdinalIgnoreCase);

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // Load a default category for auto-created items (first active category)
                        int? defaultCategoryId = null;
                        string defaultCategoryName = null;

                        using (var cmdCategory = new SqlCommand(@"SELECT TOP (1) CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY CategoryId", con, tx))
                        using (var rdr = await cmdCategory.ExecuteReaderAsync())
                        {
                            if (await rdr.ReadAsync())
                            {
                                defaultCategoryId = rdr.GetInt32(0);
                                defaultCategoryName = rdr.GetString(1);
                            }
                        }

                        // Determine a safe, existing ConditionId to use for both inserts and updates
                        int? effectiveConditionId = null;

                        // 1) Try the condition selected in the dialog, if any
                        if (conditionId.HasValue && conditionId.Value > 0)
                        {
                            using (var cmdCheck = new SqlCommand(@"SELECT ConditionID FROM dbo.[Condition] WHERE ConditionID = @ConditionId", con, tx))
                            {
                                cmdCheck.Parameters.AddWithValue("@ConditionId", conditionId.Value);
                                var result = await cmdCheck.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                {
                                    effectiveConditionId = Convert.ToInt32(result);
                                }
                            }
                        }

                        // 2) If the selected condition is invalid or none was selected, fall back to any existing condition
                        if (!effectiveConditionId.HasValue)
                        {
                            using (var cmdDefaultCond = new SqlCommand(@"SELECT TOP (1) ConditionID FROM dbo.[Condition] ORDER BY ConditionID", con, tx))
                            {
                                var result = await cmdDefaultCond.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                {
                                    effectiveConditionId = Convert.ToInt32(result);
                                }
                            }
                        }

                        const string insertItemSql = @"INSERT INTO dbo.Item
(Name, Description, ModelNumber, Active, CategoryId, Category,
 SerialNumber, UnitOfMeasure, StockOnHand, DateCreated, CreatedBy, DateModified, ModifiedBy, ItemType, StartDate, EndDate, ConditionId)
VALUES
 (@Name, @Description, @ModelNumber, @Active, @CategoryId, @Category,
  @SerialNumber, @UnitOfMeasure, @StockOnHand, @DateCreated, @CreatedBy, @DateModified, @ModifiedBy, @ItemType, @StartDate, @EndDate, @ConditionId);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        foreach (var dto in updates)
                        {
                            int? itemId = dto.ItemId;

                            // Try to match an existing item by serial number
                            if (!itemId.HasValue && !string.IsNullOrWhiteSpace(dto.SerialNumber))
                            {
                                using (var cmdFind = new SqlCommand(@"SELECT TOP (1) ItemId FROM dbo.Item WHERE SerialNumber = @SerialNumber ORDER BY ItemId", con, tx))
                                {
                                    cmdFind.Parameters.AddWithValue("@SerialNumber", dto.SerialNumber);
                                    var result = await cmdFind.ExecuteScalarAsync();
                                    if (result != null && result != DBNull.Value)
                                    {
                                        itemId = Convert.ToInt32(result);
                                    }
                                }
                            }

                            // If still not found, auto-create an Item so it appears in View Items
                            if (!itemId.HasValue && isSpareToInventory)
                            {
                                if (!defaultCategoryId.HasValue || !effectiveConditionId.HasValue)
                                {
                                    // No category or no valid condition available — cannot safely create an item
                                    continue;
                                }

                                using (var cmdInsertItem = new SqlCommand(insertItemSql, con, tx))
                                {
                                    var now = DateTime.Now;

                                    var name = !string.IsNullOrWhiteSpace(dto.ItemType)
                                        ? dto.ItemType.Trim()
                                        : (!string.IsNullOrWhiteSpace(dto.ModelNumber)
                                            ? dto.ModelNumber.Trim()
                                            : (!string.IsNullOrWhiteSpace(dto.SerialNumber)
                                                ? $"Item {dto.SerialNumber.Trim()}"
                                                : "Mobile item"));

                                    var description = string.IsNullOrWhiteSpace(dto.Remark)
                                        ? $"Auto-created from mobile update for set {dto.SetCode}"
                                        : dto.Remark.Trim();

                                    // Normalize ItemType to satisfy CK_Item_ItemType (Hardware, Software/License, Services)
                                    var rawItemType = dto.ItemType?.Trim();
                                    string normalizedItemType;
                                    if (string.IsNullOrWhiteSpace(rawItemType))
                                    {
                                        normalizedItemType = "Hardware";
                                    }
                                    else if (rawItemType.Equals("Hardware", StringComparison.OrdinalIgnoreCase))
                                    {
                                        normalizedItemType = "Hardware";
                                    }
                                    else if (rawItemType.Equals("Software/License", StringComparison.OrdinalIgnoreCase)
                                             || rawItemType.Equals("Software", StringComparison.OrdinalIgnoreCase)
                                             || rawItemType.Equals("Software License", StringComparison.OrdinalIgnoreCase))
                                    {
                                        normalizedItemType = "Software/License";
                                    }
                                    else if (rawItemType.Equals("Services", StringComparison.OrdinalIgnoreCase)
                                             || rawItemType.Equals("Service", StringComparison.OrdinalIgnoreCase))
                                    {
                                        normalizedItemType = "Service";
                                    }
                                    else
                                    {
                                        // Fallback for any unexpected value
                                        normalizedItemType = "Hardware";
                                    }

                                    // Choose a valid ConditionId for the new item from the validated effectiveConditionId
                                    var insertConditionId = effectiveConditionId.Value;

                                    cmdInsertItem.Parameters.AddWithValue("@Name", name);
                                    cmdInsertItem.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                                    cmdInsertItem.Parameters.AddWithValue("@ModelNumber", (object)(dto.ModelNumber ?? (object)DBNull.Value) ?? DBNull.Value);
                                    cmdInsertItem.Parameters.AddWithValue("@Active", true);
                                    cmdInsertItem.Parameters.AddWithValue("@CategoryId", defaultCategoryId.Value);
                                    cmdInsertItem.Parameters.AddWithValue("@Category", (object)defaultCategoryName ?? DBNull.Value);
                                    var serialNumber = string.IsNullOrWhiteSpace(dto.SerialNumber) ? null : dto.SerialNumber.Trim();
                                    cmdInsertItem.Parameters.AddWithValue("@SerialNumber", (object)serialNumber ?? DBNull.Value);
                                    cmdInsertItem.Parameters.AddWithValue("@UnitOfMeasure", "Unit");
                                    // Start with 0 — stock will be incremented below together with an Inventory entry
                                    cmdInsertItem.Parameters.AddWithValue("@StockOnHand", 0);
                                    cmdInsertItem.Parameters.AddWithValue("@DateCreated", now);
                                    cmdInsertItem.Parameters.AddWithValue("@CreatedBy", processedByUserId);
                                    cmdInsertItem.Parameters.AddWithValue("@DateModified", now);
                                    cmdInsertItem.Parameters.AddWithValue("@ModifiedBy", processedByUserId);
                                    cmdInsertItem.Parameters.AddWithValue("@ItemType", normalizedItemType);
                                    cmdInsertItem.Parameters.AddWithValue("@StartDate", DBNull.Value);
                                    cmdInsertItem.Parameters.AddWithValue("@EndDate", DBNull.Value);
                                    cmdInsertItem.Parameters.AddWithValue("@ConditionId", insertConditionId);

                                    var newIdObj = await cmdInsertItem.ExecuteScalarAsync();
                                    if (newIdObj != null && newIdObj != DBNull.Value)
                                    {
                                        itemId = Convert.ToInt32(newIdObj);
                                    }
                                }
                            }

                            if (!itemId.HasValue)
                            {
                                // Still no item — leave this update unprocessed
                                continue;
                            }

                            // CHECK: Does the item still exist in the active items table?
                            // This prevents FK_Inventory_Item conflicts if the item was hard-deleted or archived.
                            bool itemExists = false;
                            using (var cmdCheckExists = new SqlCommand(@"SELECT COUNT(*) FROM dbo.Item WHERE ItemId = @ItemId", con, tx))
                            {
                                cmdCheckExists.Parameters.AddWithValue("@ItemId", itemId.Value);
                                var existsResult = await cmdCheckExists.ExecuteScalarAsync();
                                itemExists = Convert.ToInt32(existsResult) > 0;
                            }

                            if (!itemExists)
                            {
                                // Item missing (likely deleted or archived) - skip and alert handled in caller if needed
                                // but for now we just skip to avoid the FK error
                                continue;
                            }

                            // Only update ConditionId if we resolved a valid existing condition
                            if (effectiveConditionId.HasValue)
                            {
                                using (var cmdUpdate = new SqlCommand(@"UPDATE dbo.Item SET ConditionId = @ConditionId WHERE ItemId = @ItemId", con, tx))
                                {
                                    cmdUpdate.Parameters.AddWithValue("@ConditionId", effectiveConditionId.Value);
                                    cmdUpdate.Parameters.AddWithValue("@ItemId", itemId.Value);
                                    await cmdUpdate.ExecuteNonQueryAsync();
                                }
                            }

                            if (isSpareToInventory)
                            {
                                using (var cmdUpdateStock = new SqlCommand(@"UPDATE dbo.Item SET StockOnHand = ISNULL(StockOnHand, 0) + 1 WHERE ItemId = @ItemId", con, tx))
                                {
                                    cmdUpdateStock.Parameters.AddWithValue("@ItemId", itemId.Value);
                                    await cmdUpdateStock.ExecuteNonQueryAsync();
                                }

                                using (var cmdInsertInventory = new SqlCommand(@"INSERT INTO dbo.Inventory
(Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId)
VALUES (@Description, @EntryType, @Quantity, SYSDATETIME(), @PostedBy, @ReqId, @ItemId);", con, tx))
                                {
                                    cmdInsertInventory.Parameters.AddWithValue("@Description", (object)($"Mobile update processed for set {dto.SetCode}, serial {dto.SerialNumber}") ?? DBNull.Value);
                                    cmdInsertInventory.Parameters.AddWithValue("@EntryType", "Positive");
                                    cmdInsertInventory.Parameters.AddWithValue("@Quantity", 1);
                                    cmdInsertInventory.Parameters.AddWithValue("@PostedBy", processedByUserId);
                                    cmdInsertInventory.Parameters.AddWithValue("@ReqId", DBNull.Value);
                                    cmdInsertInventory.Parameters.AddWithValue("@ItemId", itemId.Value);
                                    await cmdInsertInventory.ExecuteNonQueryAsync();
                                }

                                // Detach from set: remove the request association so it no longer appears in View Set
                                if (dto.SetId.HasValue)
                                {
                                    int? reqIdToDetach = null;

                                    if (!string.IsNullOrWhiteSpace(dto.SerialNumber))
                                    {
                                        using (var cmdFindReq = new SqlCommand(@"SELECT TOP (1) r.ReqId
FROM dbo.Request r
INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
WHERE r.SetId = @SetId AND i.SerialNumber = @SerialNumber
ORDER BY r.ReqId;", con, tx))
                                        {
                                            cmdFindReq.Parameters.AddWithValue("@SetId", dto.SetId.Value);
                                            cmdFindReq.Parameters.AddWithValue("@SerialNumber", dto.SerialNumber);
                                            var reqObj = await cmdFindReq.ExecuteScalarAsync();
                                            if (reqObj != null && reqObj != DBNull.Value)
                                            {
                                                reqIdToDetach = Convert.ToInt32(reqObj);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        using (var cmdFindReq = new SqlCommand(@"SELECT TOP (1) ReqId
FROM dbo.Request
WHERE SetId = @SetId AND ItemId = @ItemId
ORDER BY ReqId;", con, tx))
                                        {
                                            cmdFindReq.Parameters.AddWithValue("@SetId", dto.SetId.Value);
                                            cmdFindReq.Parameters.AddWithValue("@ItemId", itemId.Value);
                                            var reqObj = await cmdFindReq.ExecuteScalarAsync();
                                            if (reqObj != null && reqObj != DBNull.Value)
                                            {
                                                reqIdToDetach = Convert.ToInt32(reqObj);
                                            }
                                        }
                                    }

                                    if (reqIdToDetach.HasValue)
                                    {
                                        using (var cmdDetach = new SqlCommand(@"UPDATE dbo.Request
SET SetId = NULL
WHERE ReqId = @ReqId;", con, tx))
                                        {
                                            cmdDetach.Parameters.AddWithValue("@ReqId", reqIdToDetach.Value);
                                            await cmdDetach.ExecuteNonQueryAsync();
                                        }
                                    }
                                }
                            }

                            appliedUpdateIds.Add(dto.UpdateId);
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }

            return appliedUpdateIds;
        }

        private async Task LogConditionChangeAuditAsync(
            List<SetItemUpdateDto> selectedDtosForProcessing,
            List<int> batchUpdateIds,
            HashSet<int> appliedUpdateIds,
            string selectedConditionName,
            string repairAction,
            string processedByName)
        {
            try
            {
                if (selectedDtosForProcessing == null || selectedDtosForProcessing.Count == 0)
                    return;

                if (batchUpdateIds == null || batchUpdateIds.Count == 0)
                    return;

                var appliedDtosInBatch = selectedDtosForProcessing
                    .Where(d => batchUpdateIds.Contains(d.UpdateId))
                    .Where(d => appliedUpdateIds.Contains(d.UpdateId))
                    .ToList();

                if (appliedDtosInBatch.Count == 0)
                    return;

                var itemAuditRepo = new ItemAuditTrailRepository();
                var now = DateTime.Now;

                foreach (var upd in appliedDtosInBatch)
                {
                    int? itemId = upd.ItemId;
                    if (!itemId.HasValue && !string.IsNullOrWhiteSpace(upd.SerialNumber))
                    {
                        itemId = await GetItemIdBySerialAsync(upd.SerialNumber);
                    }

                    if (!itemId.HasValue)
                        continue;

                    var notes = $"Condition changed to '{selectedConditionName}'";
                    if (!string.IsNullOrWhiteSpace(repairAction))
                        notes += $" | Action: {repairAction}";

                    await itemAuditRepo.LogActionAsync(new ItemAuditTrailDto
                    {
                        ItemId = itemId.Value,
                        SerialNumber = upd.SerialNumber,
                        Action = "Condition Changed",
                        ActionTime = now,
                        Status = "Completed",
                        BranchName = upd.BranchName,
                        DepartmentName = upd.DepartmentName,
                        ReferenceType = "Update",
                        ReferenceId = upd.UpdateId,
                        SetCode = upd.SetCode,
                        Notes = notes,
                        CreatedBy = processedByName ?? "System"
                    }).ConfigureAwait(false);
                }
            }
            catch
            {
                // Intentionally swallow so processing isn't blocked by audit failures.
            }
        }

        private async Task<int?> GetItemIdBySerialAsync(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return null;

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(@"SELECT TOP (1) ItemId FROM dbo.Item WHERE SerialNumber = @SerialNumber ORDER BY ItemId", con))
            {
                cmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return null;
                return Convert.ToInt32(result);
            }
        }
    }
}
