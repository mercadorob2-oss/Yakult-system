using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Asset.ViewModels
{
    /// <summary>
    /// Business logic for the Assets page, ported verbatim from
    /// Pages\Asset\ViewAssetPage.cs (checkbox single-selection workflow, in-memory
    /// reflection-based search/sort/paging, Add/Edit/Deactivate via AddEditAssetDialog +
    /// AssetRepository). No permission gating exists on this page today (none added here).
    /// </summary>
    public sealed class AssetPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly AssetRepository _repo = new AssetRepository();
        private List<AssetDto> _allAssets = new List<AssetDto>();
        private List<AssetDto> _filteredAssets = new List<AssetDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public AssetPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedAssets = new ObservableCollection<AssetDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            DeactivateCommand = new RelayCommand(Deactivate);
            RefreshCommand = new RelayCommand(LoadAssets);
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(AssetSummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetSummaryMode(AssetSummaryMode.ActiveOnly));
            InactiveCardCommand = new RelayCommand(() => SetSummaryMode(AssetSummaryMode.InactiveOnly));

            _selection = new SelectionTracker<AssetDto>(a => a.Selected, (a, s) => a.Selected = s, a => a.AssetId);
            _selection.Changed += () =>
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            };
            ResetFiltersCommand = new RelayCommand(ResetFilters);
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
            _summaryMode = AssetSummaryMode.ActiveOnly;
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
                _summaryMode = value ? AssetSummaryMode.All : AssetSummaryMode.ActiveOnly;
                NotifySummaryCardsChanged();
                ApplySearchFilter();
            }
        }

        // ── Clickable summary cards (Total/Active/Inactive) ──────────────────
        private enum AssetSummaryMode { All, ActiveOnly, InactiveOnly }
        private AssetSummaryMode _summaryMode = AssetSummaryMode.ActiveOnly;

        public bool IsTotalCardActive => _summaryMode == AssetSummaryMode.All;
        public bool IsActiveCardActive => _summaryMode == AssetSummaryMode.ActiveOnly;
        public bool IsInactiveCardActive => _summaryMode == AssetSummaryMode.InactiveOnly;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand ActiveCardCommand { get; private set; }
        public RelayCommand InactiveCardCommand { get; private set; }

        private void SetSummaryMode(AssetSummaryMode mode)
        {
            _summaryMode = mode;
            NotifySummaryCardsChanged();

            bool needsShowInactive = mode != AssetSummaryMode.ActiveOnly;
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
                    // "Oldest Added" force a sort by CreatedAt; "Default" leaves order untouched.
                    if (SelectedFilterBy == "Most Recently Added")
                        SortDataSource("CreatedAt", ListSortDirection.Descending);
                    else if (SelectedFilterBy == "Oldest Added")
                        SortDataSource("CreatedAt", ListSortDirection.Ascending);

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
                // — picking a Sort-By option resets Filter-By back to Default.
                if (_selectedFilterBy != "Default")
                {
                    _selectedFilterBy = "Default";
                    OnPropertyChanged(nameof(SelectedFilterBy));
                }

                SortDataSource(value.ColumnKey, value.Direction);
                UpdatePagination();
            }
        }

        public ListSortDirection? CurrentSortDirection => _sortDirection;

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
        /// NOT reset Filter-By, unlike the Sort-By dropdown (matches DgvAssets_ColumnHeaderMouseClick,
        /// which has no such reset).</summary>
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
            if (_filteredAssets == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var sorted = direction == ListSortDirection.Ascending
                    ? _filteredAssets.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                    : _filteredAssets.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));

                _filteredAssets = sorted.ToList();
                _sortColumnKey = propertyName;
                _sortDirection = direction;
            }
            catch
            {
                // Ignore sorting errors (matches original SortDataSource's silent catch).
            }
        }

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<AssetDto> PagedAssets { get; }

        // ── Selection (tracked against _allAssets, not just the current page) ───────────
        private readonly SelectionTracker<AssetDto> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public List<AssetDto> GetSelectedAssets() => _selection.GetSelected(_allAssets);
        public void SetAssetSelected(int assetId, bool selected) => _selection.SetSelected(_allAssets, assetId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedAssets, _allAssets, selected);
        public void ClearSelection() => _selection.ClearSelection(_allAssets);

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredAssets?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredAssets.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 assets)";
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
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private void UpdatePagination()
        {
            var data = _filteredAssets ?? new List<AssetDto>();
            if (data.Count == 0)
            {
                PagedAssets.Clear();
                PageInfoText = "Page 0 of 0 (0 assets)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedAssets.Clear();
            foreach (var a in paged) PagedAssets.Add(a);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} assets)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Summary cards (computed from the FILTERED set — matches the original's
        //    UpdateSummaryCards(), which counts _filteredAssets, not _allAssets) ──

        private int _totalCount, _activeCount, _inactiveCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ActiveCount { get => _activeCount; private set => SetField(ref _activeCount, value); }
        public int InactiveCount { get => _inactiveCount; private set => SetField(ref _inactiveCount, value); }

        private void UpdateSummaryCards(List<AssetDto> data)
        {
            data = data ?? new List<AssetDto>();
            TotalCount = data.Count;
            ActiveCount = data.Count(a => a.IsActive);
            InactiveCount = TotalCount - ActiveCount;
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddAsset;
        public event Action<AssetDto> RequestEditAsset;

        /// <summary>Set by the View to a WinForms Yes/No confirmation prompt.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        public void LoadAssets()
        {
            try
            {
                _allAssets = _repo.GetAllAssets() ?? new List<AssetDto>();
                ApplySearchFilter();
                _selection.Sync(_allAssets);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error loading assets: {ex.Message}");
            }
        }

        private void ApplySearchFilter()
        {
            if (_allAssets == null) _allAssets = new List<AssetDto>();

            string q = SearchText?.Trim() ?? string.Empty;
            IEnumerable<AssetDto> filtered = _allAssets;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var lower = q.ToLowerInvariant();
                filtered = filtered.Where(a =>
                    (!string.IsNullOrEmpty(a.ModelNumber) && a.ModelNumber.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(a.SerialNumber) && a.SerialNumber.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(a.Description) && a.Description.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(a.VendorName) && a.VendorName.ToLowerInvariant().Contains(lower)));
            }

            var forSummary = filtered.ToList();
            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            switch (_summaryMode)
            {
                case AssetSummaryMode.ActiveOnly:
                    filtered = filtered.Where(a => a.IsActive);
                    break;
                case AssetSummaryMode.InactiveOnly:
                    filtered = filtered.Where(a => !a.IsActive);
                    break;
                // All: no filter
            }

            _filteredAssets = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddAsset?.Invoke();

        private void Edit()
        {
            var selected = GetSingleCheckedAsset("edit");
            if (selected == null) return;
            RequestEditAsset?.Invoke(selected);
        }

        private void Deactivate()
        {
            var selected = GetSingleCheckedAsset("deactivate");
            if (selected == null) return;

            if (!selected.IsActive)
            {
                RequestInfo?.Invoke("Already Inactive", "This asset is already inactive.");
                return;
            }

            bool confirm = ConfirmYesNo?.Invoke("Confirm Deactivate",
                $"Deactivate asset \"{selected.DisplayName}\"?\n\nIt will no longer appear in active lists.") ?? false;
            if (!confirm) return;

            try
            {
                _repo.DeactivateAsset(selected.AssetId);
                RequestInfo?.Invoke("Done", "Asset deactivated successfully.");
                LoadAssets();
            }
            catch (Exception ex)
            {
                RequestWarning?.Invoke("Cannot Deactivate", ex.Message);
            }
        }

        /// <summary>Returns the single checked asset across all pages (not just the currently-paged
        /// rows — upgraded alongside the "N Selected" badge, since a badge showing a cross-page
        /// count while Edit/Deactivate silently only look at the current page would be confusing),
        /// or null
        /// after raising the appropriate info message (matches GetSingleCheckedAsset, which
        /// only scanned the DataGridView's currently-bound (i.e. current page's) rows).</summary>
        private AssetDto GetSingleCheckedAsset(string action)
        {
            var checkedAssets = _selection.GetSelected(_allAssets);

            if (checkedAssets.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", $"Please select an asset to {action}.");
                return null;
            }
            if (checkedAssets.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", $"Please select only one asset to {action}.");
                return null;
            }
            return checkedAssets[0];
        }
    }
}
