using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.Set.ViewModels
{
    public sealed partial class SetPageViewModel
    {
        private readonly string _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetField(ref _isBusy, value);
        }

        // ── Loading ──────────────────────────────────────────────────────────
        public async void LoadSets() => await LoadSetsAsync();

        private bool _showArchived;
        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetField(ref _showArchived, value)) LoadSets(); }
        }

        public async Task LoadSetsAsync()
        {
            IsBusy = true;
            try
            {
                // Sets that have had an invoice recorded (IsInvoice = 1) used to be hidden here
                // unless "Show Invoices" was checked — recording an invoice is a status change on
                // the Set, not a reason to remove it from the main list, so this always passes
                // true now. GetAllSetsAsync's includeInvoices parameter is left in place since
                // other callers (AddRequestsToSetDialog, SetsExportViewModel, DashboardService)
                // still rely on it to exclude already-invoiced sets from their own flows.
                var sets = await _repository.GetAllSetsAsync(ShowArchived, includeInvoices: true,
                    setCodeFilter: SetCodeFilter, documentNumberFilter: DocumentNumberFilter,
                    companyFilter: CompanyFilter, departmentFilter: DepartmentFilter,
                    branchFilter: BranchFilter, employeeFilter: EmployeeFilter,
                    referenceCodeFilter: ReferenceCodeFilter, parentTagFilter: ParentTagFilter,
                    reqIdFilter: ReqIdFilter);
                var setIds = sets.Select(s => s.SetId).ToList();
                _itemNamesBySetId = await Task.Run(() => _repository.GetItemNamesBySetIds(setIds));
                _serialNumbersBySetId = await Task.Run(() => _repository.GetSerialNumbersBySetIds(setIds));
                _categoriesBySetId = await Task.Run(() => _repository.GetCategoriesBySetIds(setIds));
                _purchaseDateRangeBySetId = await Task.Run(() => _repository.GetPurchaseDateRangeBySetIds(setIds));

                _allRows = sets.Select(dto =>
                {
                    _purchaseDateRangeBySetId.TryGetValue(dto.SetId, out var range);
                    return new SetRow(dto,
                        _categoriesBySetId.TryGetValue(dto.SetId, out var cats) ? cats : null,
                        range.Min, range.Max);
                }).ToList();
                LoadCategoryFilterOptions();
                ApplyFilters();

                if (_pendingHighlightSetId.HasValue)
                {
                    int pending = _pendingHighlightSetId.Value;
                    _pendingHighlightSetId = null;
                    ApplyHighlight(pending);
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load sets: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Highlight-on-load (e.g. redirected here right after a Set was auto-created) ──
        /// <summary>Fired once the target row has been located, paged-to, and selected — the View
        /// subscribes to this to call DataGrid.ScrollIntoView, which the ViewModel cannot do.</summary>
        public event Action<SetRow> HighlightApplied;

        private int? _pendingHighlightSetId;

        /// <summary>Selects and scrolls to the given Set's row. Safe to call before the initial
        /// load finishes — the request is deferred and applied once LoadSetsAsync completes.</summary>
        public void RequestHighlight(int setId)
        {
            if (_allRows != null && _allRows.Count > 0)
                ApplyHighlight(setId);
            else
                _pendingHighlightSetId = setId;
        }

        private void ApplyHighlight(int setId)
        {
            var index = _filteredRows?.FindIndex(r => r.SetId == setId) ?? -1;
            if (index < 0) return;

            CurrentPage = (index / PageSize) + 1;
            BindPage();

            var row = Rows.FirstOrDefault(r => r.SetId == setId);
            if (row == null) return;

            SelectedRow  = row;
            row.Selected = true;
            HighlightApplied?.Invoke(row);
        }

        // ── Pagination ───────────────────────────────────────────────────────
        public int PageSize { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetField(ref _currentPage, value);
        }

        private string _pageInfoText = "Page 0 of 0 (0 sets)";
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
            var src = _filteredRows ?? _allRows ?? new List<SetRow>();
            var totalPages = GetTotalPages();

            foreach (var row in Rows) row.PropertyChanged -= Row_PropertyChanged;

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 sets)";
                RecomputeSelectedCount();
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

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} sets)";
            RecomputeSelectedCount();
        }

        private void Row_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SetRow.Selected))
                RecomputeSelectedCount();
        }

        // Called directly by the View's row-checkbox Checked/Unchecked handlers, in addition to
        // the Row_PropertyChanged subscription above — belt-and-suspenders against WPF DataGrid
        // container recycling occasionally not propagating the bound PropertyChanged reliably
        // for template-column checkboxes, which was undercounting SelectedCount in practice.
        public void RefreshSelectionState() => RecomputeSelectedCount();

        private void RecomputeSelectedCount()
        {
            SelectedCount = Rows.Count(r => r.Selected);
            OnPropertyChanged(nameof(HasSelection));
        }

        // ── Archive (dialog invoked by the View; this commits the result) ────
        public void CommitArchive(List<SetRow> rows, string reason, bool archiveItems, bool deactivateItems)
        {
            int succeeded = 0;
            var errors = new List<string>();

            foreach (var row in rows)
            {
                try
                {
                    using (var con = new SqlConnection(_connectionString))
                    {
                        con.Open();
                        using (var tx = con.BeginTransaction())
                        {
                            try
                            {
                                ArchiveSetInTransaction(row.SetId, reason, archiveItems, deactivateItems, con, tx);
                                tx.Commit();
                                succeeded++;
                            }
                            catch
                            {
                                tx.Rollback();
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{row.SetCode}: {ex.Message}");
                }
            }

            if (errors.Count == 0)
            {
                string msg = rows.Count == 1
                    ? "Set archived successfully!\n\nYou can view archived sets in the Archive page."
                    : $"Successfully archived {succeeded} set(s).\n\nYou can view archived sets in the Archive page.";
                RequestInfo?.Invoke(rows.Count == 1 ? "Success" : "Bulk Archive Complete", msg);
            }
            else
            {
                string detail = string.Join("\n", errors.Take(5));
                if (errors.Count > 5) detail += $"\n... and {errors.Count - 5} more";
                RequestWarning?.Invoke("Bulk Archive Partial", $"Archived {succeeded} of {rows.Count} set(s).\n\n{errors.Count} failed:\n{detail}");
            }

            LoadSets();
        }

        private void ArchiveSetInTransaction(int setId, string reason, bool archiveItems, bool deactivateItems,
            SqlConnection con, SqlTransaction transaction)
        {
            const string insertArchiveSql = @"
                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                VALUES ('Set', @SetId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                cmd.ExecuteNonQuery();
            }

            if (!archiveItems) return;

            const string archiveItemsSql = @"
                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                SELECT 'Item', r.ItemId, 1, GETDATE(), @ArchivedBy,
                       'Archived with Set ' + CAST(@SetId AS NVARCHAR(20))
                FROM dbo.Request r
                WHERE r.SetId = @SetId
                  AND NOT EXISTS (
                      SELECT 1 FROM ArchiveStatus
                      WHERE EntityType = 'Item' AND EntityId = r.ItemId
                  )";

            using (var cmd = new SqlCommand(archiveItemsSql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                cmd.ExecuteNonQuery();
            }

            if (!deactivateItems) return;

            const string deactivateItemsSql = @"
                UPDATE i
                SET i.Active = 0
                FROM dbo.Item i
                INNER JOIN dbo.Request r ON i.ItemId = r.ItemId
                WHERE r.SetId = @SetId";

            using (var cmd = new SqlCommand(deactivateItemsSql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.ExecuteNonQuery();
            }
        }

        // ── Delete (permanent, double-confirmed; handles both single-row and bulk) ──
        private async void DeleteRowsAsync(List<SetRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please check at least one set to delete.");
                return;
            }

            bool single = rows.Count == 1;
            string listText = single
                ? $"Set Code: {rows[0].SetCode}\nDocument #: {rows[0].Dto.DocumentNumber ?? "N/A"}\nStatus: {rows[0].Dto.Status}\nItem Count: {rows[0].ItemCount}"
                : string.Join("\n", rows.Take(10).Select(x => $"• {x.SetCode} ({x.Dto.Status}, {x.ItemCount} item(s))")) +
                  (rows.Count > 10 ? $"\n… and {rows.Count - 10} more" : "");

            bool confirmed = ConfirmYesNo?.Invoke(
                "Confirm Permanent Deletion",
                "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                (single
                    ? $"This will PERMANENTLY delete this set and restore stock:\n\n{listText}\n\n"
                    : $"This will PERMANENTLY delete {rows.Count} set(s) and restore their stock:\n\n{listText}\n\n") +
                "All items in these sets will have their stock restored.\n" +
                "Related inventory entries will also be deleted.\n\n" +
                "This action CANNOT be undone!\n\n" +
                "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                "⚠️ Use 'Archive' instead for normal records.\n\n" +
                "Are you absolutely sure?") ?? false;
            if (!confirmed) return;

            bool doubleConfirmed = ConfirmYesNo?.Invoke(
                "Final Confirmation",
                single
                    ? "FINAL CONFIRMATION\n\nThis set will be permanently deleted and cannot be recovered.\nStock will be restored for all items in this set.\n\nAre you absolutely certain?"
                    : $"FINAL CONFIRMATION\n\n{rows.Count} set(s) will be permanently deleted and cannot be recovered.\nStock will be restored for all items in these sets.\n\nAre you absolutely certain?") ?? false;
            if (!doubleConfirmed) return;

            int successCount = 0;
            var failedSets = new List<string>();

            foreach (var row in rows)
            {
                try
                {
                    bool success = await _repository.DeleteSetAndRestoreStock(row.SetId);
                    if (success) successCount++;
                    else failedSets.Add(row.SetCode);
                }
                catch (Exception ex)
                {
                    failedSets.Add($"{row.SetCode}: {ex.Message}");
                }
            }

            if (failedSets.Count == 0)
            {
                RequestInfo?.Invoke("Deleted Successfully",
                    $"✔ {successCount} set(s) permanently deleted.\n✔ Stock restored for all affected items.\n✔ Related inventory entries deleted.");
            }
            else
            {
                RequestWarning?.Invoke("Partial Deletion",
                    $"Completed with errors:\n\n✔ Deleted: {successCount}\n✖ Failed: {failedSets.Count}\n\nFailed sets:\n" +
                    string.Join("\n", failedSets.Select(x => $"• {x}")));
            }

            LoadSets();
        }
    }
}
