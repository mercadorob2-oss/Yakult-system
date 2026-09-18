using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.WPF.Renewal.ViewRenewals.ViewModels
{
    public sealed partial class RenewalPageViewModel
    {
        // ── Loading ──────────────────────────────────────────────────────────
        public async void LoadRenewals() => await LoadRenewalsAsync();

        public async Task LoadRenewalsAsync()
        {
            try
            {
                var renewals = await Task.Run(() => _repository.GetAllRenewals(
                    setCodeFilter: SetCodeFilter,
                    documentNumberFilter: DocumentNumberFilter,
                    companyFilter: CompanyFilter,
                    departmentFilter: DepartmentFilter,
                    branchFilter: BranchFilter,
                    employeeFilter: EmployeeFilter,
                    referenceCodeFilter: ReferenceCodeFilter,
                    parentTagFilter: ParentTagFilter,
                    reqIdFilter: ReqIdFilter));

                var setIds = renewals.Select(r => r.SetId).Distinct().ToList();
                _itemNamesBySetId = await Task.Run(() => _repository.GetItemNamesBySetIds(setIds));
                var categoriesBySetId = await Task.Run(() => _setRepository.GetCategoriesBySetIds(setIds));
                var itemTypesBySetId = await Task.Run(() => _setRepository.GetItemTypesBySetIds(setIds));
                var subTypesBySetId = await Task.Run(() => _setRepository.GetSubTypesBySetIds(setIds));

                _allRows = renewals.Select(dto => new RenewalRow(dto)).ToList();
                foreach (var row in _allRows)
                {
                    row.Categories = categoriesBySetId.TryGetValue(row.SetId, out var cats) ? cats : Array.Empty<string>();
                    row.ItemTypes = itemTypesBySetId.TryGetValue(row.SetId, out var types) ? types : Array.Empty<string>();

                    if (subTypesBySetId.TryGetValue(row.SetId, out var subTypeEntry))
                    {
                        row.SubTypes = subTypeEntry.SubTypes;
                        row.HasNonSubTypeItems = subTypeEntry.HasNonSubType;
                        row.SubTypeReferenceCodes = subTypeEntry.ReferenceCodes;
                    }
                    else
                    {
                        row.SubTypes = Array.Empty<string>();
                        row.HasNonSubTypeItems = true;
                        row.SubTypeReferenceCodes = Array.Empty<string>();
                    }

                    // Subscribed at load time (not per-page) because selection here is
                    // deliberately cross-page — see the class-level Selection scope note.
                    row.PropertyChanged += Row_PropertyChanged;
                }

                LoadCategoryFilterOptions();
                LoadItemTypeFilterOptions();
                LoadDocumentYearOptions();
                ApplyFilters(); // also recomputes SelectedCount
                UpdateSummary();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load renewals: {ex.Message}");
            }
        }

        private void Row_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RenewalRow.Selected))
                RecomputeSelectedCount();
        }

        // Called directly by the View's row-checkbox Click handler, in addition to the
        // Row_PropertyChanged subscription above — belt-and-suspenders against WPF DataGrid
        // container recycling occasionally not propagating the bound PropertyChanged reliably
        // for template-column checkboxes, which was undercounting SelectedCount in practice.
        public void RefreshSelectionState() => RecomputeSelectedCount();

        private void RecomputeSelectedCount()
        {
            SelectedCount = (_filteredRows ?? _allRows).Count(r => r.Selected);
            OnPropertyChanged(nameof(HasSelection));
        }

        // ── Summary cards — computed from ALL non-archived renewals, independent of the
        // current search/status/type/date filters (matches the original's UpdateSummary()). ──
        public int TotalCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public int ExpiringCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ActiveCount { get; private set; }

        private void UpdateSummary()
        {
            var nonArchived = (_allRows ?? new List<RenewalRow>()).Where(r => r.Active).ToList();

            TotalCount = nonArchived.Count;
            ExpiredCount = nonArchived.Count(r => r.ExpiryStatus == "Expired");
            ExpiringCount = nonArchived.Count(r => r.ExpiryStatus == "Expiring Soon");
            WarningCount = nonArchived.Count(r => r.ExpiryStatus == "Warning");
            ActiveCount = nonArchived.Count(r => r.ExpiryStatus == "Active");

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ExpiredCount));
            OnPropertyChanged(nameof(ExpiringCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(ActiveCount));
        }

        // ── Pagination ───────────────────────────────────────────────────────
        public int PageSize { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetField(ref _currentPage, value);
        }

        private string _pageInfoText = "Page 0 of 0 (0 renewals)";
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
            var src = _filteredRows ?? _allRows ?? new List<RenewalRow>();
            var totalPages = GetTotalPages();

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 renewals)";
                return;
            }

            CurrentPage = totalPages > 0 ? Math.Max(1, Math.Min(CurrentPage, totalPages)) : 1;

            var pageItems = src.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Rows.Clear();
            foreach (var item in pageItems) Rows.Add(item);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} renewals)";
        }

        // ── Export (CSV) — always dumps the full unfiltered dataset, matching the original ──
        private void ExportCsv()
        {
            if (_allRows == null || _allRows.Count == 0)
            {
                RequestInfo?.Invoke("Export", "No data to export.");
                return;
            }

            var defaultFileName = $"Renewals_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var path = RequestSaveFilePath?.Invoke(defaultFileName);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                using (var writer = new StreamWriter(path))
                {
                    writer.WriteLine("Set Code,Type,Document Number,Company,Start Date,End Date,Days Until Expiry,Status,Items,Amount");
                    foreach (var row in _allRows)
                    {
                        var d = row.Dto;
                        writer.WriteLine($"\"{d.SetCode}\"," +
                            $"\"{d.SetType}\"," +
                            $"\"{d.DocumentNumber}\"," +
                            $"\"{d.CompanyName}\"," +
                            $"\"{d.StartDate:MM/dd/yyyy}\"," +
                            $"\"{d.EndDate:MM/dd/yyyy}\"," +
                            $"{d.DaysUntilExpiry}," +
                            $"\"{d.ExpiryStatus}\"," +
                            $"{d.ItemCount}," +
                            $"{d.TotalAmountDue:F2}");
                    }
                }

                RequestInfo?.Invoke("Export Complete", $"Data exported successfully to:\n{path}");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Export Error", $"Failed to export data: {ex.Message}");
            }
        }

        // ── PDF export (checkbox-selected rows across ALL filtered pages) ────
        private void ExportPdf()
        {
            var selected = (_filteredRows ?? _allRows)?.Where(r => r.Selected).ToList() ?? new List<RenewalRow>();
            if (selected.Count == 0)
            {
                RequestInfo?.Invoke("PDF Export", "Please select at least one renewal.");
                return;
            }

            var defaultFileName = $"SelectedRenewals_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var path = RequestSaveFilePath?.Invoke(defaultFileName);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                RenewalReportPdfGenerator.GenerateSelectedRenewalsPdf(selected.Select(r => r.Dto).ToList(), path, "Selected Renewals");

                bool open = ConfirmYesNo?.Invoke("Export Complete", "PDF exported successfully.\n\nOpen it now?") ?? false;
                if (open) RenewalReportPdfGenerator.TryOpen(path);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("PDF Export Error", $"Failed to export PDF: {ex.Message}");
            }
        }
    }
}
