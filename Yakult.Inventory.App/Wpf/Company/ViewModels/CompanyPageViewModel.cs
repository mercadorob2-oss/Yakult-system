using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using CompanyDto = Yakult.Inventory.App.Pages.CompanyDto;

namespace Yakult.Inventory.App.WPF.Company.ViewModels
{
    /// <summary>Grid-display DTO, moved here verbatim from the bottom of the original
    /// Pages\Company\ViewCompanyPage.cs. Already had Selected/StatusDisplay in the original
    /// (StatusDisplay here is a REAL stored property set at load time — unlike Category's
    /// "Status" column, sorting by it genuinely works).</summary>
    public class CompanyViewDto
    {
        public bool Selected { get; set; }
        public int ComId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedByName { get; set; }
    }

    /// <summary>
    /// Business logic for the Companies page, ported verbatim from
    /// Pages\Company\ViewCompanyPage.cs. Shares the reload/sort quirks documented in
    /// [[BranchPageViewModel]]/[[DepartmentPageViewModel]]. Distinct behavior specific to this
    /// page:
    /// - Show Inactive is baked into the SQL WHERE clause itself (not an in-memory filter), so
    ///   toggling it triggers a full reload, same as picking Filter-By "Default".
    /// - Toggle Active cascades to linked Departments and Branches (via BranchDepartmentCompany)
    ///   inside one transaction, and warns about dependency counts before deactivating.
    /// - Add performs a transactional Company insert plus optional bulk Department/Branch
    ///   creation (wizard mode) from CompanyDialog.
    /// - Archive loops per-company (not batched): each call shows its own success dialog and
    ///   reloads independently, so a multi-select archive can show several dialogs in a row —
    ///   preserved as-is, not batched into one summary.
    /// - Permanent Delete only counts successes; a failed per-item delete is silently skipped
    ///   (no failure detail collected), matching the original exactly.
    /// No permission gating exists on this page today (none added here).
    /// </summary>
    public sealed class CompanyPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly string _connectionString = Core.DatabaseConfig.ConnectionString;
        private readonly CompanyRepository _repo = new CompanyRepository();

        private List<CompanyViewDto> _allCompanies = new List<CompanyViewDto>();
        private List<CompanyViewDto> _filteredCompanies = new List<CompanyViewDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public CompanyPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedCompanies = new ObservableCollection<CompanyViewDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            ToggleActiveCommand = new RelayCommand(ToggleActive);
            ArchiveCommand = new RelayCommand(Archive);
            DeleteCommand = new RelayCommand(async () => await PermanentDeleteAsync());
            RefreshCommand = new RelayCommand(LoadCompanies);
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(CompanySummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetSummaryMode(CompanySummaryMode.ActiveOnly));
            InactiveCardCommand = new RelayCommand(() => SetSummaryMode(CompanySummaryMode.InactiveOnly));
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _selection = new SelectionTracker<CompanyViewDto>(c => c.Selected, (c, s) => c.Selected = s, c => c.ComId);
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

            _showInactive = false;
            OnPropertyChanged(nameof(ShowInactive));
            _summaryMode = CompanySummaryMode.ActiveOnly;
            NotifySummaryCardsChanged();

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            ApplyFilters();
        }

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        private bool _showInactive;
        public bool ShowInactive
        {
            get => _showInactive;
            set
            {
                if (!SetField(ref _showInactive, value)) return;
                _summaryMode = value ? CompanySummaryMode.All : CompanySummaryMode.ActiveOnly;
                NotifySummaryCardsChanged();
                ApplyFilters();
            }
        }

        // ── Clickable summary cards (Total/Active/Inactive) ──────────────────
        private enum CompanySummaryMode { All, ActiveOnly, InactiveOnly }
        private CompanySummaryMode _summaryMode = CompanySummaryMode.ActiveOnly;

        public bool IsTotalCardActive => _summaryMode == CompanySummaryMode.All;
        public bool IsActiveCardActive => _summaryMode == CompanySummaryMode.ActiveOnly;
        public bool IsInactiveCardActive => _summaryMode == CompanySummaryMode.InactiveOnly;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand ActiveCardCommand { get; private set; }
        public RelayCommand InactiveCardCommand { get; private set; }

        private void SetSummaryMode(CompanySummaryMode mode)
        {
            _summaryMode = mode;
            NotifySummaryCardsChanged();

            bool needsShowInactive = mode != CompanySummaryMode.ActiveOnly;
            if (_showInactive != needsShowInactive)
            {
                _showInactive = needsShowInactive;
                OnPropertyChanged(nameof(ShowInactive));
            }

            ApplyFilters();
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
                    RequestClearSortGlyphs?.Invoke();

                    if (value == "Default")
                    {
                        LoadCompanies();
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

                SelectedFilterBy = "Default"; // may cascade into a full reload

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

        /// <summary>Header-click sort — never touches Filter-By.</summary>
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
            if (_filteredCompanies == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var sorted = direction == ListSortDirection.Ascending
                    ? _filteredCompanies.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                    : _filteredCompanies.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));

                _filteredCompanies = sorted.ToList();
                _sortColumnKey = propertyName;
                _sortDirection = direction;
            }
            catch
            {
                // Ignore sorting errors (matches original's silent catch).
            }
        }

        public event Action RequestClearSortGlyphs;

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<CompanyViewDto> PagedCompanies { get; }

        // ── Selection (tracked against _allCompanies, not just the current page) ────────
        private readonly SelectionTracker<CompanyViewDto> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public List<CompanyViewDto> GetSelectedCompanies() => _selection.GetSelected(_allCompanies);
        public void SetCompanySelected(int comId, bool selected) => _selection.SetSelected(_allCompanies, comId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedCompanies, _allCompanies, selected);
        public void ClearSelection() => _selection.ClearSelection(_allCompanies);

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredCompanies?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredCompanies.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 companies)";
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
        public RelayCommand ToggleActiveCommand { get; }
        public RelayCommand ArchiveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private void UpdatePagination()
        {
            var data = _filteredCompanies ?? new List<CompanyViewDto>();
            if (data.Count == 0)
            {
                PagedCompanies.Clear();
                PageInfoText = "Page 0 of 0 (0 companies)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedCompanies.Clear();
            foreach (var c in paged) PagedCompanies.Add(c);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} companies)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Summary cards ─────────────────────────────────────────────────────

        private int _totalCount, _activeCount, _inactiveCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ActiveCount { get => _activeCount; private set => SetField(ref _activeCount, value); }
        public int InactiveCount { get => _inactiveCount; private set => SetField(ref _inactiveCount, value); }

        private void UpdateSummaryCards(IEnumerable<CompanyViewDto> data)
        {
            var list = data?.ToList() ?? new List<CompanyViewDto>();
            TotalCount = list.Count;
            ActiveCount = list.Count(x => x != null && x.Active);
            InactiveCount = list.Count(x => x != null && !x.Active);
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddCompany;
        public event Action<CompanyDto> RequestEditCompany;
        public event Func<string, string> RequestArchiveReason; // title unused; message -> reason (null if cancelled)

        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>Synchronous, matches the original LoadCompanies() (plain blocking ADO.NET).</summary>
        public void LoadCompanies()
        {
            try
            {
                var companies = new List<CompanyViewDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT
                            c.ComId,
                            c.Name,
                            c.Description,
                            ISNULL(c.Active, 1) AS Active,
                            c.DateCreated,
                            u.Name AS CreatedByName
                        FROM dbo.Company c
                        LEFT JOIN dbo.[User] u ON c.Createdby = u.UserId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Company' AND arc.EntityId = c.ComId AND arc.IsArchived = 1
                        WHERE arc.ArchiveId IS NULL
                        ORDER BY c.DateCreated DESC, c.ComId DESC";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool isActive = reader.GetBoolean(3);
                            companies.Add(new CompanyViewDto
                            {
                                ComId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Active = isActive,
                                StatusDisplay = isActive ? "Active" : "Inactive",
                                DateCreated = reader.GetDateTime(4),
                                CreatedByName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5)
                            });
                        }
                    }
                }

                _allCompanies = companies;
                ApplyFilters();
                _selection.Sync(_allCompanies);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load companies: {ex.Message}");
            }
        }

        /// <summary>No sort re-applied here and no Active filtering (that's baked into the SQL
        /// via ShowInactive) — matches the original ApplyFilters().</summary>
        private void ApplyFilters()
        {
            if (_allCompanies == null) return;

            var filtered = _allCompanies.AsEnumerable();

            string searchText = SearchText?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(c =>
                    (c.Name != null && c.Name.ToLower().Contains(searchText)) ||
                    (c.Description != null && c.Description.ToLower().Contains(searchText)));
            }

            var forSummary = filtered.ToList();
            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            switch (_summaryMode)
            {
                case CompanySummaryMode.ActiveOnly:
                    filtered = filtered.Where(c => c.Active);
                    break;
                case CompanySummaryMode.InactiveOnly:
                    filtered = filtered.Where(c => !c.Active);
                    break;
                // All: no filter
            }

            _filteredCompanies = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddCompany?.Invoke();

        public bool SaveCompany(CompanyDto company)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            int companyId;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO dbo.Company (Name, Description, Active, DateCreated, Createdby)
                                VALUES (@Name, @Description, 1, @DateCreated, @CreatedBy);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Name", company.Name);
                                cmd.Parameters.AddWithValue("@Description", (object)company.Description ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@DateCreated", company.DateCreated);
                                cmd.Parameters.AddWithValue("@CreatedBy", company.CreatedByUserId);
                                companyId = (int)cmd.ExecuteScalar();
                            }

                            if (company.Departments != null && company.Departments.Count > 0)
                            {
                                foreach (var dept in company.Departments)
                                {
                                    using (var cmd = new SqlCommand(@"
                                        INSERT INTO dbo.Department (Name, Description, Active, DateCreated, Createdby)
                                        VALUES (@Name, @Description, 1, @DateCreated, @CreatedBy)", con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@Name", dept.Name);
                                        cmd.Parameters.AddWithValue("@Description", (object)dept.Description ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@DateCreated", dept.DateCreated);
                                        cmd.Parameters.AddWithValue("@CreatedBy", dept.CreatedByUserId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            if (company.Branches != null && company.Branches.Count > 0)
                            {
                                foreach (var branch in company.Branches)
                                {
                                    int branchId;
                                    using (var cmd = new SqlCommand(@"
                                        INSERT INTO dbo.Branch (Name, Description, Active, DateCreated, Createdby)
                                        OUTPUT INSERTED.BranchId
                                        VALUES (@Name, @Description, 1, @DateCreated, @CreatedBy)", con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@Name", branch.Name);
                                        cmd.Parameters.AddWithValue("@Description", (object)branch.Description ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@DateCreated", branch.DateCreated);
                                        cmd.Parameters.AddWithValue("@CreatedBy", branch.CreatedByUserId);
                                        branchId = (int)cmd.ExecuteScalar();
                                    }

                                    using (var bdcCmd = new SqlCommand(@"
                                        INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                                        VALUES (@BranchId, NULL, @ComId)", con, transaction))
                                    {
                                        bdcCmd.Parameters.AddWithValue("@BranchId", branchId);
                                        bdcCmd.Parameters.AddWithValue("@ComId", companyId);
                                        bdcCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                            ActivityLogger.Log(ActivityLogger.Actions.Create, "Company", companyId, $"Company '{company.Name}' created");
                            return true;
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
                RequestError?.Invoke("Error", $"Failed to save company: {ex.Message}");
                return false;
            }
        }

        private void Edit()
        {
            var checkedCompanies = _selection.GetSelected(_allCompanies);

            if (checkedCompanies.Count == 0)
            {
                RequestInfo?.Invoke("Information", "Please select at least one company to edit.");
                return;
            }
            if (checkedCompanies.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one company to edit.");
                return;
            }

            var selected = checkedCompanies[0];
            var companyDto = new CompanyDto
            {
                ComId = selected.ComId,
                Name = selected.Name,
                Description = selected.Description,
                DateCreated = selected.DateCreated,
                CreatedByName = selected.CreatedByName
            };

            RequestEditCompany?.Invoke(companyDto);
        }

        public bool UpdateCompany(CompanyDto company)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.Company
                        SET Name = @Name,
                            Description = @Description,
                            DateModified = @DateModified,
                            Modifiedby = @ModifiedBy
                        WHERE ComId = @ComId", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", company.ComId);
                        cmd.Parameters.AddWithValue("@Name", company.Name);
                        cmd.Parameters.AddWithValue("@Description", (object)company.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateModified", DateTime.Now);
                        cmd.Parameters.AddWithValue("@ModifiedBy", AppSession.CurrentUserId);
                        cmd.ExecuteNonQuery();
                    }
                }
                ActivityLogger.Log(ActivityLogger.Actions.Update, "Company", company.ComId, $"Company '{company.Name}' updated");
                return true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to update company: {ex.Message}");
                return false;
            }
        }

        public string ToggleActiveButtonText
        {
            get
            {
                var first = _allCompanies.FirstOrDefault(c => c.Selected);
                return first != null && first.Active ? "Deactivate" : "Activate";
            }
        }

        /// <summary>CompanyViewDto.Selected isn't itself observable — the View calls this after
        /// any checkbox toggle so ToggleActiveButtonText re-evaluates (matches the original's
        /// DgvCompanies_SelectionChanged recomputing the button text on every checkbox change).</summary>
        public void NotifySelectionChanged() => OnPropertyChanged(nameof(ToggleActiveButtonText));

        private void ToggleActive()
        {
            var checkedCompanies = _selection.GetSelected(_allCompanies);

            if (checkedCompanies.Count == 0)
            {
                RequestInfo?.Invoke("Information", "Please select a company.");
                return;
            }
            if (checkedCompanies.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one company to toggle active status.");
                return;
            }

            var selected = checkedCompanies[0];

            if (selected.Active)
            {
                var dep = CheckCompanyDependencies(selected.ComId);
                string message = $"Are you sure you want to deactivate '{selected.Name}'?\n\n";
                if (dep.HasDependencies)
                {
                    message += "This will also deactivate:\n";
                    if (dep.DepartmentCount > 0) message += $"• {dep.DepartmentCount} department(s)\n";
                    if (dep.BranchCount > 0) message += $"• {dep.BranchCount} branch(es)\n";
                    if (dep.EmployeeCount > 0) message += $"\nNote: {dep.EmployeeCount} employee(s) are linked to this company.\n";
                    message += "\nExisting data will remain visible but grayed out and unselectable in future transactions.";
                }

                bool confirm = ConfirmYesNo?.Invoke("Confirm Deactivate", message) ?? false;
                if (!confirm) return;
            }
            else
            {
                bool confirm = ConfirmYesNo?.Invoke("Confirm Activate",
                    $"Are you sure you want to activate '{selected.Name}'?\n\nThis will also activate all related departments and branches.") ?? false;
                if (!confirm) return;
            }

            if (ToggleCompanyActiveStatus(selected.ComId, !selected.Active))
            {
                string action = selected.Active ? "deactivated" : "activated";
                RequestInfo?.Invoke("Success", $"Company {action} successfully!");
                LoadCompanies();
            }
        }

        private (bool HasDependencies, int DepartmentCount, int BranchCount, int EmployeeCount) CheckCompanyDependencies(int comId)
        {
            int deptCount = 0, branchCount = 0, empCount = 0;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(DISTINCT DepartmentID) FROM dbo.BranchDepartmentCompany WHERE CompanyID = @ComId AND DepartmentID IS NOT NULL", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        deptCount = (int)cmd.ExecuteScalar();
                    }

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(DISTINCT BranchID) FROM dbo.BranchDepartmentCompany WHERE CompanyID = @ComId", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        branchCount = (int)cmd.ExecuteScalar();
                    }

                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Employee WHERE ComId = @ComId", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        empCount = (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error checking dependencies: {ex.Message}");
            }

            return (deptCount > 0 || branchCount > 0 || empCount > 0, deptCount, branchCount, empCount);
        }

        private bool ToggleCompanyActiveStatus(int comId, bool newActiveStatus)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            int userId = AppSession.CurrentUserId;
                            DateTime now = DateTime.Now;

                            using (var cmd = new SqlCommand(@"
                                UPDATE dbo.Company
                                SET Active = @Active,
                                    DateModified = @DateModified,
                                    Modifiedby = @ModifiedBy
                                WHERE ComId = @ComId", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Active", newActiveStatus);
                                cmd.Parameters.AddWithValue("@DateModified", now);
                                cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                                cmd.Parameters.AddWithValue("@ComId", comId);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new SqlCommand(@"
                                UPDATE d
                                SET d.Active = @Active,
                                    d.DateModified = @DateModified,
                                    d.Modifiedby = @ModifiedBy
                                FROM dbo.Department d
                                INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.DepartmentID = d.DeptId
                                WHERE bdc.CompanyID = @ComId", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Active", newActiveStatus);
                                cmd.Parameters.AddWithValue("@DateModified", now);
                                cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                                cmd.Parameters.AddWithValue("@ComId", comId);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new SqlCommand(@"
                                UPDATE b
                                SET b.Active = @Active,
                                    b.DateModified = @DateModified,
                                    b.Modifiedby = @ModifiedBy
                                FROM dbo.Branch b
                                INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.BranchID = b.BranchId
                                WHERE bdc.CompanyID = @ComId", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Active", newActiveStatus);
                                cmd.Parameters.AddWithValue("@DateModified", now);
                                cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                                cmd.Parameters.AddWithValue("@ComId", comId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            ActivityLogger.Log(ActivityLogger.Actions.Update, "Company", comId, $"Company {(newActiveStatus ? "activated" : "deactivated")} (cascaded to departments and branches)");
                            return true;
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
                RequestError?.Invoke("Error", $"Failed to update company status: {ex.Message}");
                return false;
            }
        }

        private void Archive()
        {
            var checkedCompanies = _selection.GetSelected(_allCompanies);

            if (checkedCompanies.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one company to archive.");
                return;
            }

            string message = checkedCompanies.Count == 1
                ? $"Are you sure you want to archive this company?\n\nCompany: {checkedCompanies[0].Name}\nDescription: {checkedCompanies[0].Description ?? "N/A"}\n\nThe company will be moved to the archive."
                : $"Are you sure you want to archive the {checkedCompanies.Count} selected companies?\n\nAll selected companies will be moved to the archive.";

            string reason = RequestArchiveReason?.Invoke(message);
            if (reason == null) return; // dialog cancelled

            foreach (var company in checkedCompanies)
            {
                ArchiveCompany(company.ComId, reason);
            }
        }

        /// <summary>Matches the original: each archive call is independent — its own success
        /// dialog and its own LoadCompanies() reload, not batched across the selection.</summary>
        private void ArchiveCompany(int comId, string reason)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Company', @ComId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ComId", comId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            ActivityLogger.Log(ActivityLogger.Actions.Delete, "Company", comId, $"Company archived. Reason: {reason}");

                            RequestInfo?.Invoke("Success", "Company archived successfully!\n\nYou can view archived companies in the Archive page.");
                            LoadCompanies();
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
                RequestError?.Invoke("Error", $"Error archiving company:\n\n{ex.Message}");
            }
        }

        /// <summary>Matches the original: only counts successes, silently skips failed items
        /// (no per-item failure detail collected).</summary>
        private async Task PermanentDeleteAsync()
        {
            var checkedItems = _selection.GetSelected(_allCompanies);

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one company to delete.");
                return;
            }

            string itemList = string.Join("\n", checkedItems.Select(c => $"• {c.Name}"));
            string countText = checkedItems.Count == 1 ? "this company" : $"these {checkedItems.Count} companies";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Permanent Deletion",
                "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                $"This will PERMANENTLY delete {countText}:\n\n{itemList}\n\n" +
                "This action CANNOT be undone!\n\n" +
                "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                "⚠️ Use 'Archive' button instead for normal records.\n\n" +
                "Are you absolutely sure you want to permanently delete?") ?? false;
            if (!confirm) return;

            bool doubleCheck = ConfirmYesNo?.Invoke("Final Confirmation",
                $"FINAL CONFIRMATION\n\n{checkedItems.Count} company(ies) will be permanently deleted and cannot be recovered.\n\nAre you absolutely certain?") ?? false;
            if (!doubleCheck) return;

            try
            {
                int successCount = 0;

                foreach (var company in checkedItems)
                {
                    var (success, _) = await _repo.DeleteCompanyAsync(company.ComId);
                    if (success)
                    {
                        successCount++;
                        ActivityLogger.Log(ActivityLogger.Actions.Delete, "Company", company.ComId, $"Company '{company.Name}' permanently deleted");
                    }
                }

                if (successCount > 0)
                {
                    RequestInfo?.Invoke("Deleted Successfully", $"{successCount} company(ies) permanently deleted!\n\n✔ Removed from database");
                    LoadCompanies();
                }
                else
                {
                    RequestError?.Invoke("Error", "Failed to delete companies.");
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error deleting company:\n\n{ex.Message}");
            }
        }
    }
}
