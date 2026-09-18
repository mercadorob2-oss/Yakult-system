using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Vendor.ViewModels
{
    /// <summary>
    /// Business logic for the Vendors page, ported verbatim from
    /// Pages\Vendor\ViewVendorsPage.cs (in-memory reflection-based search/sort/paging,
    /// Add/Edit via AddVendorDialog/EditVendorDialog, Archive/PermanentDelete via
    /// VendorRepository). No permission gating exists on this page today (none added here).
    /// </summary>
    public sealed class VendorPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly VendorRepository _repo = new VendorRepository();
        private List<VendorDto> _allVendors = new List<VendorDto>();
        private List<VendorDto> _filteredVendors = new List<VendorDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public VendorPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedVendors = new ObservableCollection<VendorDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            ArchiveCommand = new RelayCommand(async () => await ArchiveAsync());
            DeleteCommand = new RelayCommand(async () => await PermanentDeleteAsync());
            RefreshCommand = new RelayCommand(async () => await LoadVendorsAsync());
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(VendorSummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetSummaryMode(VendorSummaryMode.ActiveOnly));
            InactiveCardCommand = new RelayCommand(() => SetSummaryMode(VendorSummaryMode.InactiveOnly));
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _selection = new SelectionTracker<VendorDto>(v => v.Selected, (v, s) => v.Selected = s, v => v.VendorId);
            _selection.Changed += () =>
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            };
        }

        public RelayCommand ResetFiltersCommand { get; private set; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _showInactive = false;
            OnPropertyChanged(nameof(ShowInactive));
            _summaryMode = VendorSummaryMode.ActiveOnly;
            NotifySummaryCardsChanged();

            ApplySearchFilter();
        }

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplySearchFilter(); }
        }

        private bool _showInactive;
        public bool ShowInactive
        {
            get => _showInactive;
            set
            {
                if (!SetField(ref _showInactive, value)) return;
                _summaryMode = value ? VendorSummaryMode.All : VendorSummaryMode.ActiveOnly;
                NotifySummaryCardsChanged();
                ApplySearchFilter();
            }
        }

        // ── Clickable summary cards (Total/Active/Inactive) ──────────────────
        private enum VendorSummaryMode { All, ActiveOnly, InactiveOnly }
        private VendorSummaryMode _summaryMode = VendorSummaryMode.ActiveOnly;

        public bool IsTotalCardActive => _summaryMode == VendorSummaryMode.All;
        public bool IsActiveCardActive => _summaryMode == VendorSummaryMode.ActiveOnly;
        public bool IsInactiveCardActive => _summaryMode == VendorSummaryMode.InactiveOnly;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand ActiveCardCommand { get; private set; }
        public RelayCommand InactiveCardCommand { get; private set; }

        private void SetSummaryMode(VendorSummaryMode mode)
        {
            _summaryMode = mode;
            NotifySummaryCardsChanged();

            bool needsShowInactive = mode != VendorSummaryMode.ActiveOnly;
            if (_showInactive != needsShowInactive)
            {
                _showInactive = needsShowInactive;
                OnPropertyChanged(nameof(ShowInactive));
            }
            ApplySearchFilter();
        }

        private void NotifySummaryCardsChanged()
        {
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsInactiveCardActive));
        }

        public ObservableCollection<string> FilterByOptions { get; }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    // Mirrors CmbFilterBy_SelectedIndexChanged: only "Most Recently Added"/
                    // "Oldest Added" force a sort by CreatedDate; "Default" leaves order untouched.
                    if (SelectedFilterBy == "Most Recently Added")
                        SortDataSource("CreatedDate", ListSortDirection.Descending);
                    else if (SelectedFilterBy == "Oldest Added")
                        SortDataSource("CreatedDate", ListSortDirection.Ascending);

                    UpdatePagination();
                }
            }
        }

        // ── Sort By dropdown ──────────────────────────────────────────────────

        public ObservableCollection<ListSortOption> SortByOptions { get; }

        private ListSortOption _selectedSortBy;
        public ListSortOption SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                _selectedSortBy = value;
                OnPropertyChanged();

                if (_suppressSortByChange || value == null) return;

                // Mirrors: if (cmbFilterBy.SelectedIndex != 0) cmbFilterBy.SelectedIndex = 0;
                if (_selectedFilterBy != "Default")
                {
                    _selectedFilterBy = "Default";
                    OnPropertyChanged(nameof(SelectedFilterBy));
                }

                SortDataSource(value.ColumnKey, value.Direction);
                UpdatePagination();
            }
        }

        public void SetSortByOptions(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                SortByOptions.Clear();
                foreach (var o in options) SortByOptions.Add(o);

                ListSortOption selected = null;
                if (_sortColumnKey != null)
                {
                    selected = options.FirstOrDefault(o =>
                        o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending));
                }
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                {
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);
                }

                SelectedSortBy = selected ?? options.FirstOrDefault();
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's DataGrid.Sorting handler (column header click) — does
        /// NOT reset Filter-By, matching DgvVendors_ColumnHeaderMouseClick.</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            SortDataSource(columnKey, direction);
            UpdatePagination();

            _suppressSortByChange = true;
            try
            {
                var match = SortByOptions.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == _sortDirection);
                if (match != null) SelectedSortBy = match;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        private void SortDataSource(string propertyName, ListSortDirection direction)
        {
            if (_filteredVendors == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var sorted = direction == ListSortDirection.Ascending
                    ? _filteredVendors.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                    : _filteredVendors.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));

                _filteredVendors = sorted.ToList();
                _sortColumnKey = propertyName;
                _sortDirection = direction;
            }
            catch
            {
                // Ignore sorting errors (matches original SortDataSource's silent catch).
            }
        }

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<VendorDto> PagedVendors { get; }

        // ── Selection (tracked against _allVendors, not just the current page, so a
        //    selection made on one page survives paging/filtering) ──────────────────
        private readonly SelectionTracker<VendorDto> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public List<VendorDto> GetSelectedVendors() => _selection.GetSelected(_allVendors);
        public void SetVendorSelected(int vendorId, bool selected) => _selection.SetSelected(_allVendors, vendorId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedVendors, _allVendors, selected);
        public void ClearSelection() => _selection.ClearSelection(_allVendors);

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredVendors?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredVendors.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 vendors)";
        public string PageInfoText
        {
            get => _pageInfoText;
            private set => SetField(ref _pageInfoText, value);
        }

        private bool _canFirstPage, _canPrevPage, _canNextPage, _canLastPage;
        public bool CanFirstPage { get => _canFirstPage; private set => SetField(ref _canFirstPage, value); }
        public bool CanPrevPage { get => _canPrevPage; private set => SetField(ref _canPrevPage, value); }
        public bool CanNextPage { get => _canNextPage; private set => SetField(ref _canNextPage, value); }
        public bool CanLastPage { get => _canLastPage; private set => SetField(ref _canLastPage, value); }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ArchiveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private void UpdatePagination()
        {
            var data = _filteredVendors ?? new List<VendorDto>();
            if (data.Count == 0)
            {
                PagedVendors.Clear();
                PageInfoText = "Page 0 of 0 (0 vendors)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedVendors.Clear();
            foreach (var v in paged) PagedVendors.Add(v);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} vendors)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Summary cards (computed from the FILTERED set — matches the original's
        //    UpdateSummaryCards(), which counts _filteredVendors, not _allVendors) ──

        private int _totalCount, _activeCount, _inactiveCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ActiveCount { get => _activeCount; private set => SetField(ref _activeCount, value); }
        public int InactiveCount { get => _inactiveCount; private set => SetField(ref _inactiveCount, value); }

        private void UpdateSummaryCards(List<VendorDto> data)
        {
            data = data ?? new List<VendorDto>();
            TotalCount = data.Count;
            ActiveCount = data.Count(v => v != null && v.IsActive);
            InactiveCount = TotalCount - ActiveCount;
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddVendor;
        public event Action<VendorDto> RequestEditVendor;

        /// <summary>Set by the View to a WinForms Yes/No confirmation prompt.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        public async Task LoadVendorsAsync()
        {
            try
            {
                _allVendors = (await _repo.GetAllVendorsAsync())?.ToList() ?? new List<VendorDto>();
                ApplySearchFilter();
                _selection.Sync(_allVendors);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error loading vendors: {ex.Message}");
            }
        }

        private void ApplySearchFilter()
        {
            if (_allVendors == null) _allVendors = new List<VendorDto>();

            string q = SearchText?.Trim() ?? string.Empty;
            IEnumerable<VendorDto> filtered = _allVendors;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var lower = q.ToLowerInvariant();
                filtered = filtered.Where(v =>
                    (!string.IsNullOrEmpty(v.VendorName) && v.VendorName.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(v.Address) && v.Address.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(v.TIN) && v.TIN.ToLowerInvariant().Contains(lower)));
            }

            var forSummary = filtered.ToList();
            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            switch (_summaryMode)
            {
                case VendorSummaryMode.ActiveOnly:
                    filtered = filtered.Where(v => v.IsActive && !v.IsArchived);
                    break;
                case VendorSummaryMode.InactiveOnly:
                    filtered = filtered.Where(v => !v.IsActive || v.IsArchived);
                    break;
                // All: no filter
            }

            _filteredVendors = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddVendor?.Invoke();

        private void Edit()
        {
            var checkedItems = _allVendors.Where(v => v.Selected).ToList();

            if (checkedItems.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one vendor to edit.");
                return;
            }
            if (checkedItems.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one vendor to edit.");
                return;
            }

            RequestEditVendor?.Invoke(checkedItems[0]);
        }

        private async Task ArchiveAsync()
        {
            var checkedItems = _allVendors.Where(v => v.Selected).ToList();

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one vendor to archive.");
                return;
            }

            string message = checkedItems.Count == 1
                ? "Are you sure you want to archive this vendor?\n\n" +
                  $"Vendor: {checkedItems[0].VendorName}\n" +
                  $"TIN: {checkedItems[0].TIN ?? "N/A"}\n\n" +
                  "The vendor will be moved to the archive."
                : $"Are you sure you want to archive {checkedItems.Count} vendors?\n\n" +
                  "All selected vendors will be moved to the archive.";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Archive", message) ?? false;
            if (!confirm) return;

            try
            {
                int successCount = 0;
                int failCount = 0;

                foreach (var vendor in checkedItems)
                {
                    bool success = await _repo.ArchiveVendorAsync(
                        vendor.VendorId,
                        Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System",
                        "Archived from Vendor page");

                    if (success) successCount++;
                    else failCount++;
                }

                if (failCount == 0)
                {
                    string successMsg = checkedItems.Count == 1
                        ? "Vendor archived successfully!"
                        : $"{checkedItems.Count} vendors archived successfully!";
                    RequestInfo?.Invoke("Success", successMsg);
                }
                else
                {
                    RequestWarning?.Invoke("Partial Success", $"Archive completed with errors.\n\nSuccess: {successCount}\nFailed: {failCount}");
                }

                await LoadVendorsAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error archiving vendor(s):\n\n{ex.Message}");
            }
        }

        private async Task PermanentDeleteAsync()
        {
            var checkedItems = _allVendors.Where(v => v.Selected).ToList();

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one vendor to delete.");
                return;
            }

            string itemList = string.Join("\n", checkedItems.Select(v => $"• {v.VendorName} (TIN: {v.TIN ?? "N/A"})"));
            string countText = checkedItems.Count == 1 ? "this vendor" : $"these {checkedItems.Count} vendors";

            bool result = ConfirmYesNo?.Invoke("Confirm Permanent Deletion",
                $"⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                $"This will PERMANENTLY delete {countText}:\n\n" +
                $"{itemList}\n\n" +
                $"This action CANNOT be undone!\n\n" +
                $"⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                $"⚠️ Use 'Archive' button instead for normal records.\n\n" +
                $"Are you absolutely sure you want to permanently delete?") ?? false;

            if (!result) return;

            bool doubleCheck = ConfirmYesNo?.Invoke("Final Confirmation",
                "FINAL CONFIRMATION\n\n" +
                $"{checkedItems.Count} vendor(s) will be permanently deleted and cannot be recovered.\n\n" +
                "Are you absolutely certain?") ?? false;

            if (!doubleCheck) return;

            try
            {
                int successCount = 0;
                int failureCount = 0;
                var failedItems = new List<string>();

                foreach (var vendor in checkedItems)
                {
                    try
                    {
                        var (success, msg) = await _repo.PermanentDeleteVendorAsync(vendor.VendorId);
                        if (success)
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            failedItems.Add($"• {vendor.VendorName} - {msg}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        string reason = ex.Message.Contains("REFERENCE constraint") || ex.Message.Contains("conflicted with")
                            ? "Has related records (items, receipts, etc.)"
                            : ex.Message;
                        failedItems.Add($"• {vendor.VendorName} - {reason}");
                    }
                }

                if (successCount > 0 && failureCount == 0)
                {
                    RequestInfo?.Invoke("Deleted Successfully",
                        $"✓ All {successCount} vendor(s) permanently deleted!\n\n✓ Removed from database");
                }
                else if (successCount > 0 && failureCount > 0)
                {
                    RequestWarning?.Invoke("Partial Success",
                        $"⚠️ PARTIAL SUCCESS\n\n" +
                        $"✓ Successfully deleted: {successCount} vendor(s)\n" +
                        $"✗ Failed to delete: {failureCount} vendor(s)\n\n" +
                        $"Failed items:\n{string.Join("\n", failedItems)}\n\n" +
                        $"Tip: Use 'Archive' instead for items with related records.");
                }
                else if (failureCount > 0)
                {
                    RequestError?.Invoke("Deletion Failed",
                        $"✗ Failed to delete all {failureCount} vendor(s)\n\n" +
                        $"Failed items:\n{string.Join("\n", failedItems)}\n\n" +
                        $"Tip: Use 'Archive' instead for items with related records.");
                }

                if (successCount > 0)
                    await LoadVendorsAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error deleting vendors:\n\n{ex.Message}");
            }
        }
    }
}
