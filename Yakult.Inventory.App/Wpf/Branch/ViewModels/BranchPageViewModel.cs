using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Branch.ViewModels
{
    /// <summary>Grid-display DTO, moved here verbatim from the bottom of the original
    /// Pages\Branch\ViewBranchPage.cs (it was a page-local class, not in the shared Dtos.cs).
    /// <c>Selected</c> is new — added for the WPF checkbox-column binding, mirroring the
    /// existing Selected convention on VendorDto/ItemCategoryDto/AssetDto.</summary>
    public class BranchViewDto
    {
        public bool Selected { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public bool IsFactory { get; set; }
        public bool IsDepot { get; set; }
        public bool IsCenter { get; set; }
        public string CenterRegion { get; set; }
        public bool IsDistributor { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedByName { get; set; }
    }

    /// <summary>
    /// Business logic for the Branches page, ported verbatim from Pages\Branch\ViewBranchPage.cs.
    /// Notable quirks preserved exactly (not "fixed"):
    /// - No Show-Inactive concept (the SQL already excludes archived branches).
    /// - ApplyFilters() does NOT reapply any active column sort — typing in the search box
    ///   silently drops back to natural DB order (DateCreated DESC) among the matches. Vendor/
    ///   Category do reapply their sort on search; Branch's original never did.
    /// - Selecting "Default" in Filter-By triggers a full synchronous DB reload (LoadBranches()),
    ///   not just an in-memory reorder — same quirk carried into SelectedFilterBy.
    /// - Picking a Sort-By dropdown option resets Filter-By to "Default" first (cascading into
    ///   the full reload above if Filter-By wasn't already Default), THEN applies the chosen
    ///   sort on the freshly-reloaded set. Header-click sort does NOT touch Filter-By at all.
    /// - Archive uses a single DB transaction across all selected branches (all-or-nothing);
    ///   Permanent Delete calls the repository per item independently and reports SqlException
    ///   1547 (FK violation) with a friendlier message.
    /// No permission gating exists on this page today (none added here).
    /// </summary>
    public sealed class BranchPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly BranchRepository _repo = new BranchRepository();
        private readonly string _connectionString = Core.DatabaseConfig.ConnectionString;

        private List<BranchViewDto> _allBranches = new List<BranchViewDto>();
        private List<BranchViewDto> _filteredBranches = new List<BranchViewDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public BranchPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedBranches = new ObservableCollection<BranchViewDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            ArchiveCommand = new RelayCommand(async () => await ArchiveAsync());
            DeleteCommand = new RelayCommand(async () => await PermanentDeleteAsync());
            RefreshCommand = new RelayCommand(LoadBranches);
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(BranchSummaryMode.All));
            FactoryCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == BranchSummaryMode.Factory ? BranchSummaryMode.All : BranchSummaryMode.Factory));
            DepotCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == BranchSummaryMode.Depot ? BranchSummaryMode.All : BranchSummaryMode.Depot));
            CenterCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == BranchSummaryMode.Center ? BranchSummaryMode.All : BranchSummaryMode.Center));
            DistributorCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == BranchSummaryMode.Distributor ? BranchSummaryMode.All : BranchSummaryMode.Distributor));
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _selection = new SelectionTracker<BranchViewDto>(b => b.Selected, (b, s) => b.Selected = s, b => b.BranchId);
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

            _summaryMode = BranchSummaryMode.All;
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsFactoryCardActive));
            OnPropertyChanged(nameof(IsDepotCardActive));
            OnPropertyChanged(nameof(IsCenterCardActive));
            OnPropertyChanged(nameof(IsDistributorCardActive));

            ApplyFilters();
        }

        // ── Clickable summary cards (Total/Factory/Depot/Center/Distributor) ──
        private enum BranchSummaryMode { All, Factory, Depot, Center, Distributor }
        private BranchSummaryMode _summaryMode = BranchSummaryMode.All;

        public bool IsTotalCardActive => _summaryMode == BranchSummaryMode.All;
        public bool IsFactoryCardActive => _summaryMode == BranchSummaryMode.Factory;
        public bool IsDepotCardActive => _summaryMode == BranchSummaryMode.Depot;
        public bool IsCenterCardActive => _summaryMode == BranchSummaryMode.Center;
        public bool IsDistributorCardActive => _summaryMode == BranchSummaryMode.Distributor;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand FactoryCardCommand { get; private set; }
        public RelayCommand DepotCardCommand { get; private set; }
        public RelayCommand CenterCardCommand { get; private set; }
        public RelayCommand DistributorCardCommand { get; private set; }

        private void SetSummaryMode(BranchSummaryMode mode)
        {
            _summaryMode = mode;
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsFactoryCardActive));
            OnPropertyChanged(nameof(IsDepotCardActive));
            OnPropertyChanged(nameof(IsCenterCardActive));
            OnPropertyChanged(nameof(IsDistributorCardActive));
            ApplyFilters();
        }

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
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
                    RequestClearSortGlyphs?.Invoke();

                    if (value == "Default")
                    {
                        LoadBranches(); // full reload, matches CmbFilterBy_SelectedIndexChanged
                        return;
                    }

                    var dir = value == "Most Recently Added" ? ListSortDirection.Descending
                            : value == "Oldest Added" ? ListSortDirection.Ascending
                            : (ListSortDirection?)null;
                    if (dir == null) return;

                    SortDataSource("DateCreated", dir.Value);
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

                // Matches the original Sort-By callback: reset Filter-By to Default first
                // (this alone may trigger a full reload — see SelectedFilterBy), THEN sort.
                SelectedFilterBy = "Default";

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

        /// <summary>Header-click sort — unlike the Sort-By dropdown, never touches Filter-By.</summary>
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
            if (_filteredBranches == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var sorted = direction == ListSortDirection.Ascending
                    ? _filteredBranches.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                    : _filteredBranches.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));

                _filteredBranches = sorted.ToList();
                _sortColumnKey = propertyName;
                _sortDirection = direction;
            }
            catch
            {
                // Ignore sorting errors (matches original's silent catch).
            }
        }

        /// <summary>The View clears all DataGridColumn.SortDirection glyphs when this fires
        /// (matches CmbFilterBy_SelectedIndexChanged's glyph-clear loop).</summary>
        public event Action RequestClearSortGlyphs;

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<BranchViewDto> PagedBranches { get; }

        // ── Selection (tracked against _allBranches, not just the current page) ─────────
        private readonly SelectionTracker<BranchViewDto> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public List<BranchViewDto> GetSelectedBranches() => _selection.GetSelected(_allBranches);
        public void SetBranchSelected(int branchId, bool selected) => _selection.SetSelected(_allBranches, branchId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedBranches, _allBranches, selected);
        public void ClearSelection() => _selection.ClearSelection(_allBranches);

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredBranches?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredBranches.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 branches)";
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
            var data = _filteredBranches ?? new List<BranchViewDto>();
            if (data.Count == 0)
            {
                PagedBranches.Clear();
                PageInfoText = "Page 0 of 0 (0 branches)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedBranches.Clear();
            foreach (var b in paged) PagedBranches.Add(b);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} branches)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Summary cards (Total/Factory/Depot/Center/Distributor) ──────────

        private int _totalCount, _factoryCount, _depotCount, _centerCount, _distributorCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int FactoryCount { get => _factoryCount; private set => SetField(ref _factoryCount, value); }
        public int DepotCount { get => _depotCount; private set => SetField(ref _depotCount, value); }
        public int CenterCount { get => _centerCount; private set => SetField(ref _centerCount, value); }
        public int DistributorCount { get => _distributorCount; private set => SetField(ref _distributorCount, value); }

        private void UpdateSummaryCards(IEnumerable<BranchViewDto> data)
        {
            var list = data?.ToList() ?? new List<BranchViewDto>();
            TotalCount = list.Count;
            FactoryCount = list.Count(x => x != null && x.IsFactory);
            DepotCount = list.Count(x => x != null && x.IsDepot);
            CenterCount = list.Count(x => x != null && x.IsCenter);
            DistributorCount = list.Count(x => x != null && x.IsDistributor);
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddBranch;
        public event Action<Yakult.Inventory.App.Pages.BranchDto> RequestEditBranch;

        /// <summary>Set by the View to a WinForms Yes/No confirmation prompt.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>Synchronous, matches the original LoadBranches() (plain blocking ADO.NET,
        /// never made async in the WinForms version).</summary>
        public void LoadBranches()
        {
            try
            {
                var branches = new List<BranchViewDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            b.BranchId,
                            b.Name,
                            b.Description,
                            ISNULL(bdc_co.CompanyName,  'N/A') AS CompanyName,
                            ISNULL(bdc_dept.DeptName,   'N/A') AS DepartmentName,
                            b.IsFactory,
                            b.IsDepot,
                            b.IsCenter,
                            b.CenterRegion,
                            b.IsDistributor,
                            b.DateCreated,
                            u.Name AS CreatedByName
                        FROM dbo.Branch b
                        OUTER APPLY (
                            SELECT TOP 1 c.Name AS CompanyName
                            FROM   dbo.BranchDepartmentCompany bdc
                            JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
                            WHERE  bdc.BranchID = b.BranchId
                            ORDER BY bdc.BranchDeptCompanyID
                        ) bdc_co
                        OUTER APPLY (
                            SELECT TOP 1 d.Name AS DeptName
                            FROM   dbo.BranchDepartmentCompany bdc
                            JOIN   dbo.Department d ON bdc.DepartmentID = d.DeptId
                            WHERE  bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
                            ORDER BY bdc.BranchDeptCompanyID
                        ) bdc_dept
                        LEFT JOIN dbo.[User] u ON b.Createdby = u.UserId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Branch' AND arc.EntityId = b.BranchId AND arc.IsArchived = 1
                        WHERE arc.ArchiveId IS NULL
                        ORDER BY b.DateCreated DESC, b.BranchId DESC", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            branches.Add(new BranchViewDto
                            {
                                BranchId = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                CompanyName = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                DepartmentName = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                                IsFactory = !reader.IsDBNull(5) && reader.GetBoolean(5),
                                IsDepot = !reader.IsDBNull(6) && reader.GetBoolean(6),
                                IsCenter = !reader.IsDBNull(7) && reader.GetBoolean(7),
                                CenterRegion = reader.IsDBNull(8) ? null : reader.GetString(8),
                                IsDistributor = !reader.IsDBNull(9) && reader.GetBoolean(9),
                                DateCreated = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                CreatedByName = reader.IsDBNull(11) ? "N/A" : reader.GetString(11)
                            });
                        }
                    }
                }

                _allBranches = branches;
                ApplyFilters();
                _selection.Sync(_allBranches);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load branches: {ex.Message}");
            }
        }

        /// <summary>No sort re-applied here — matches the original ApplyFilters(), which only
        /// ever filtered by search text and never reapplied an active column sort.</summary>
        private void ApplyFilters()
        {
            if (_allBranches == null) return;

            var filtered = _allBranches.AsEnumerable();

            string searchText = SearchText?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(b =>
                    (b.Name != null && b.Name.ToLower().Contains(searchText)) ||
                    (b.Description != null && b.Description.ToLower().Contains(searchText)) ||
                    (b.CompanyName != null && b.CompanyName.ToLower().Contains(searchText)) ||
                    (b.DepartmentName != null && b.DepartmentName.ToLower().Contains(searchText)) ||
                    (b.CenterRegion != null && b.CenterRegion.ToLower().Contains(searchText)));
            }

            var forSummary = filtered.ToList();
            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            switch (_summaryMode)
            {
                case BranchSummaryMode.Factory: filtered = filtered.Where(b => b.IsFactory); break;
                case BranchSummaryMode.Depot: filtered = filtered.Where(b => b.IsDepot); break;
                case BranchSummaryMode.Center: filtered = filtered.Where(b => b.IsCenter); break;
                case BranchSummaryMode.Distributor: filtered = filtered.Where(b => b.IsDistributor); break;
                // All: no filter
            }

            _filteredBranches = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddBranch?.Invoke();

        private void Edit()
        {
            var checkedItems = _selection.GetSelected(_allBranches);

            if (checkedItems.Count == 0)
            {
                RequestInfo?.Invoke("Information", "Please select at least one branch to edit.");
                return;
            }
            if (checkedItems.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one branch to edit.");
                return;
            }

            _ = OpenEditAsync(checkedItems[0].BranchId);
        }

        private async Task OpenEditAsync(int branchId)
        {
            try
            {
                var branch = await _repo.GetByIdAsync(branchId);
                if (branch == null)
                {
                    RequestError?.Invoke("Error", "Branch not found.");
                    return;
                }
                RequestEditBranch?.Invoke(branch);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error opening edit dialog: {ex.Message}");
            }
        }

        private async Task ArchiveAsync()
        {
            var checkedItems = _selection.GetSelected(_allBranches);

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one branch to archive.");
                return;
            }

            string message = checkedItems.Count == 1
                ? $"Are you sure you want to archive this branch?\n\nBranch: {checkedItems[0].Name}\n\nThe branch will be moved to the archive."
                : $"Are you sure you want to archive {checkedItems.Count} branches?\n\nAll selected branches will be moved to the archive.";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Archive", message) ?? false;
            if (!confirm) return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            string insertArchiveSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Branch', @BranchId, 1, GETDATE(), @ArchivedBy, 'Archived from Branch page')";

                            foreach (var branch in checkedItems)
                            {
                                using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BranchId", branch.BranchId);
                                    cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();

                            foreach (var branch in checkedItems)
                                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Branch", branch.BranchId, $"Branch '{branch.Name}' archived");

                            string successMsg = checkedItems.Count == 1
                                ? "Branch archived successfully!"
                                : $"{checkedItems.Count} branches archived successfully!";
                            RequestInfo?.Invoke("Success", successMsg);
                            LoadBranches();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error archiving branch(es):\n\n{ex.Message}");
            }
        }

        private async Task PermanentDeleteAsync()
        {
            var checkedItems = _selection.GetSelected(_allBranches);

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one branch to delete.");
                return;
            }

            string itemList = string.Join("\n", checkedItems.Select(b => $"• {b.Name}"));
            string countText = checkedItems.Count == 1 ? "this branch" : $"these {checkedItems.Count} branches";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Permanent Deletion",
                "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                $"This will PERMANENTLY delete {countText}:\n\n{itemList}\n\n" +
                "This action CANNOT be undone!\n\n" +
                "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                "⚠️ Use 'Archive' button instead for normal records.\n\n" +
                "Are you absolutely sure you want to permanently delete?") ?? false;
            if (!confirm) return;

            bool doubleCheck = ConfirmYesNo?.Invoke("Final Confirmation",
                $"FINAL CONFIRMATION\n\n{checkedItems.Count} branch(es) will be permanently deleted and cannot be recovered.\n\nAre you absolutely certain?") ?? false;
            if (!doubleCheck) return;

            int successCount = 0;
            int failureCount = 0;
            var failedItems = new List<string>();

            foreach (var branch in checkedItems)
            {
                try
                {
                    bool success = await _repo.DeleteAsync(branch.BranchId);
                    if (success)
                    {
                        successCount++;
                        ActivityLogger.Log(ActivityLogger.Actions.Delete, "Branch", branch.BranchId, $"Branch '{branch.Name}' permanently deleted");
                    }
                    else
                    {
                        failureCount++;
                        failedItems.Add($"• {branch.Name} - Unknown error");
                    }
                }
                catch (SqlException ex)
                {
                    failureCount++;
                    string reason = Yakult.Inventory.App.Helpers.ForeignKeyErrorHelper.IsForeignKeyViolation(ex)
                        ? Yakult.Inventory.App.Helpers.ForeignKeyErrorHelper.BuildShortReason(ex)
                        : ex.Message;
                    failedItems.Add($"• {branch.Name} - {reason}");
                }
                catch (Exception ex)
                {
                    failureCount++;
                    failedItems.Add($"• {branch.Name} - {ex.Message}");
                }
            }

            if (successCount > 0 && failureCount == 0)
            {
                RequestInfo?.Invoke("Deleted Successfully", $"✔ All {successCount} branch(es) permanently deleted!\n\n✔ Removed from database");
            }
            else if (successCount > 0 && failureCount > 0)
            {
                RequestWarning?.Invoke("Partial Success",
                    $"⚠️ PARTIAL SUCCESS\n\n✔ Successfully deleted: {successCount} branch(es)\n✗ Failed to delete: {failureCount} branch(es)\n\n" +
                    $"Failed items:\n{string.Join("\n", failedItems)}\n\nTip: Use 'Archive' instead for items with related records.");
            }
            else if (failureCount > 0)
            {
                RequestError?.Invoke("Deletion Failed",
                    $"✗ Failed to delete all {failureCount} branch(es)\n\nFailed items:\n{string.Join("\n", failedItems)}\n\nTip: Use 'Archive' instead for items with related records.");
            }

            if (successCount > 0)
                LoadBranches();
        }
    }
}
