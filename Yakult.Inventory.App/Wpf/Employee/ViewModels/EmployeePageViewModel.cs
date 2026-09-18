using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Employee.ViewModels
{
    /// <summary>Grid-display DTO, moved here verbatim from the bottom of the original
    /// Pages\Employee\ViewEmployeePage.cs. <c>Selected</c> is new (checkbox binding).</summary>
    public class EmployeeViewDto
    {
        public bool Selected { get; set; }
        public int EmpId { get; set; }
        public string Name { get; set; }
        public string TitleCode { get; set; }
        public string Position { get; set; }
        public string Description { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedByName { get; set; }
        public string EmployeeNumber { get; set; }
    }

    /// <summary>
    /// Business logic for the Employees page, ported verbatim from
    /// Pages\Employee\ViewEmployeePage.cs. Sort/Filter-By behavior mirrors
    /// [[CategoryPageViewModel]] exactly (same DefaultListPageTemplate.Create-based original):
    /// ApplyFilters() re-sorts on every call (either by the active column-sort or, absent one,
    /// by the Filter-By fallback where "Default" behaves like "Most Recently Added"), and the
    /// Sort-By dropdown does NOT reset Filter-By (only picking a Filter-By option clears the
    /// column-sort marker).
    ///
    /// NOTE: the original file also defines BtnExport_Click/BtnDownloadTemplate_Click/
    /// BtnImport_Click (Excel export/import via ClosedXML/ExcelDataReader) plus their support
    /// methods — but none of those handlers are ever wired to a button in BuildUiWithTemplate().
    /// They are dead/unreachable code on THIS page (the real, reachable Excel import/export UI
    /// lives on the separate Pages\Admin\Account-Management\EmployeeManagementPage.cs, out of
    /// scope for this conversion). Dropped here, matching the "leave unreachable functionality
    /// out" precedent used elsewhere in this project (e.g. Warranty's orphaned Generate Report
    /// button).
    ///
    /// UpdateEmployee's department/branch-change audit trail cascade (logging an
    /// ItemAuditTrailDto entry per open request when Department/Branch changes) IS preserved
    /// exactly, including the synchronous .GetAwaiter().GetResult() blocking call.
    /// No permission gating exists on this page today (none added here).
    /// </summary>
    public sealed class EmployeePageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly string _connectionString = Core.DatabaseConfig.ConnectionString;

        private List<EmployeeViewDto> _allEmployees = new List<EmployeeViewDto>();
        private List<EmployeeViewDto> _filteredEmployees = new List<EmployeeViewDto>();

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public EmployeePageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedEmployees = new ObservableCollection<EmployeeViewDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            ArchiveCommand = new RelayCommand(Archive);
            DeleteCommand = new RelayCommand(PermanentDelete);
            RefreshCommand = new RelayCommand(LoadEmployees);
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });
            ResetFiltersCommand = new RelayCommand(ResetFilters);
        }

        public RelayCommand ResetFiltersCommand { get; private set; }

        /// <summary>Clears every filter control (search, Filter By, Sort By, and any per-column
        /// Excel-style filters) back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _columnFilters.Clear();

            _sortColumnKey = null;
            _sortDirection = null;

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

        public ObservableCollection<string> FilterByOptions { get; }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    // Mirrors the FilterByComboBox.SelectedIndexChanged handler wired in
                    // BuildUiWithTemplate: clears the column-sort marker, then re-filters.
                    _sortColumnKey = null;
                    _sortDirection = null;
                    ApplyFilters();
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

                // Does NOT reset Filter-By (matches the original Sort-By callback).
                _sortColumnKey = value.ColumnKey;
                _sortDirection = value.Direction;
                ApplyFilters();
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

        /// <summary>Header-click sort.</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            _sortColumnKey = columnKey;
            _sortDirection = direction;
            ApplyFilters();

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

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<EmployeeViewDto> PagedEmployees { get; }

        // ── Selection (tracked against _allEmployees, not just the current page, so a
        //    selection made on one page survives paging/filtering — mirrors ItemsPageViewModel) ──
        private int _selectedCount;
        public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
        public bool HasSelection => SelectedCount > 0;

        private void SyncSelectionState()
        {
            SelectedCount = _allEmployees.Count(e => e != null && e.Selected);
            OnPropertyChanged(nameof(HasSelection));
        }

        public List<EmployeeViewDto> GetSelectedEmployees() => _allEmployees.Where(e => e.Selected).ToList();

        public void SetEmployeeSelected(int empId, bool selected)
        {
            var emp = _allEmployees.FirstOrDefault(e => e.EmpId == empId);
            if (emp == null) return;

            emp.Selected = selected;
            SyncSelectionState();
        }

        // Scoped to the current page only — matches the header checkbox's
        // "Select / deselect all rows on this page" tooltip.
        public void SetPageSelected(bool selected)
        {
            foreach (var emp in PagedEmployees)
                emp.Selected = selected;

            SyncSelectionState();
        }

        public void ClearSelection()
        {
            foreach (var emp in _allEmployees)
                emp.Selected = false;

            SyncSelectionState();
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredEmployees?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredEmployees.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 employees)";
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
            var data = _filteredEmployees ?? new List<EmployeeViewDto>();
            if (data.Count == 0)
            {
                PagedEmployees.Clear();
                PageInfoText = "Page 0 of 0 (0 employees)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedEmployees.Clear();
            foreach (var e in paged) PagedEmployees.Add(e);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} employees)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddEmployee;
        public event Action<EmployeeDto> RequestEditEmployee;

        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>Synchronous, matches the original LoadEmployees() (plain blocking ADO.NET).</summary>
        public void LoadEmployees()
        {
            try
            {
                var employees = new List<EmployeeViewDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            e.EmpId,
                            e.Name,
                            e.Position,
                            e.Description,
                            c.Name AS CompanyName,
                            b.Name AS BranchName,
                            d.Name AS DepartmentName,
                            e.DateCreated,
                            u.Name AS CreatedByName,
                            e.EmployeeNumber,
                            t.Code AS TitleCode
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                        LEFT JOIN dbo.[User] u ON e.Createdby = u.UserId
                        LEFT JOIN dbo.Title t ON e.TitleId = t.TitleId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE arc.ArchiveId IS NULL
                        ORDER BY e.DateCreated DESC, e.EmpId DESC", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            employees.Add(new EmployeeViewDto
                            {
                                EmpId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Position = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                                CompanyName = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                                BranchName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                DepartmentName = reader.IsDBNull(6) ? "N/A" : reader.GetString(6),
                                DateCreated = reader.GetDateTime(7),
                                CreatedByName = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                                EmployeeNumber = reader.IsDBNull(9) ? null : reader.GetString(9),
                                TitleCode = reader.IsDBNull(10) ? null : reader.GetString(10)
                            });
                        }
                    }
                }

                _allEmployees = employees;
                ApplyFilters();
                SyncSelectionState();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load employees: {ex.Message}");
            }
        }

        private void ApplyFilters()
        {
            if (_allEmployees == null) return;

            var filtered = _allEmployees.AsEnumerable();

            string searchText = SearchText?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(e =>
                    (e.Name != null && e.Name.ToLower().Contains(searchText)) ||
                    (e.EmployeeNumber != null && e.EmployeeNumber.ToLower().Contains(searchText)) ||
                    (e.TitleCode != null && e.TitleCode.ToLower().Contains(searchText)) ||
                    (e.Position != null && e.Position.ToLower().Contains(searchText)) ||
                    (e.Description != null && e.Description.ToLower().Contains(searchText)) ||
                    (e.CompanyName != null && e.CompanyName.ToLower().Contains(searchText)) ||
                    (e.BranchName != null && e.BranchName.ToLower().Contains(searchText)) ||
                    (e.DepartmentName != null && e.DepartmentName.ToLower().Contains(searchText)));
            }

            _filteredEmployees = filtered.ToList();

            // Apply per-column header filters on top of the search text.
            foreach (var kv in _columnFilters)
            {
                string col = kv.Key;
                var allowed = kv.Value;
                _filteredEmployees = _filteredEmployees.Where(x => allowed.Contains(GetEmployeeColumnValue(x, col) ?? "")).ToList();
            }

            if (_sortColumnKey != null)
            {
                var propInfo = typeof(EmployeeViewDto).GetProperty(_sortColumnKey);
                if (propInfo != null)
                {
                    _filteredEmployees = _sortDirection == ListSortDirection.Ascending
                        ? _filteredEmployees.OrderBy(x => propInfo.GetValue(x, null)).ToList()
                        : _filteredEmployees.OrderByDescending(x => propInfo.GetValue(x, null)).ToList();
                }
            }
            else
            {
                if (SelectedFilterBy == "Most Recently Added" || SelectedFilterBy == "Default")
                    _filteredEmployees = _filteredEmployees.OrderByDescending(x => x.DateCreated).ToList();
                else if (SelectedFilterBy == "Oldest Added")
                    _filteredEmployees = _filteredEmployees.OrderBy(x => x.DateCreated).ToList();
            }

            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Column header filters ────────────────────────────────────────────

        private static string GetEmployeeColumnValue(EmployeeViewDto emp, string column)
        {
            switch (column)
            {
                case "ID":            return emp.EmpId.ToString();
                case "Name":          return emp.Name;
                case "Title":         return emp.TitleCode;
                case "Employee #":    return emp.EmployeeNumber;
                case "Position":      return emp.Position;
                case "Description":   return emp.Description;
                case "Company":       return emp.CompanyName;
                case "Branch":        return emp.BranchName;
                case "Department":    return emp.DepartmentName;
                case "Date Created":  return emp.DateCreated.ToString("MM/dd/yyyy");
                case "Created By":    return emp.CreatedByName;
                default:              return null;
            }
        }

        public HashSet<string> GetColumnFilter(string column)
            => _columnFilters.TryGetValue(column, out var f) ? f : null;

        public void SetColumnFilter(string column, HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                _columnFilters.Remove(column);
            else
                _columnFilters[column] = values;
            ApplyFilters();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            ApplyFilters();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            IEnumerable<string> values;
            switch (column)
            {
                case "ID":            values = _allEmployees.Select(e => e.EmpId.ToString()); break;
                case "Name":          values = _allEmployees.Select(e => e.Name ?? ""); break;
                case "Title":         values = _allEmployees.Select(e => e.TitleCode ?? ""); break;
                case "Employee #":    values = _allEmployees.Select(e => e.EmployeeNumber ?? ""); break;
                case "Position":      values = _allEmployees.Select(e => e.Position ?? ""); break;
                case "Description":   values = _allEmployees.Select(e => e.Description ?? ""); break;
                case "Company":       values = _allEmployees.Select(e => e.CompanyName ?? ""); break;
                case "Branch":        values = _allEmployees.Select(e => e.BranchName ?? ""); break;
                case "Department":    values = _allEmployees.Select(e => e.DepartmentName ?? ""); break;
                case "Date Created":  values = _allEmployees.Select(e => e.DateCreated.ToString("MM/dd/yyyy")); break;
                case "Created By":    values = _allEmployees.Select(e => e.CreatedByName ?? ""); break;
                default: return new List<string>();
            }
            return values.Where(v => !string.IsNullOrEmpty(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v)
                         .ToList();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddEmployee?.Invoke();

        public bool SaveEmployee(EmployeeDto emp)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Employee (Name, Position, Description, DateCreated, Createdby, ComId, DeptId, BranchId, EmployeeNumber, TitleId)
                        VALUES (@Name, @Position, @Description, @DateCreated, @CreatedBy, @ComId, @DeptId, @BranchId, @EmployeeNumber, @TitleId);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", emp.Name);
                        cmd.Parameters.AddWithValue("@Position", (object)emp.Position ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description", (object)emp.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateCreated", emp.DateCreated);
                        cmd.Parameters.AddWithValue("@CreatedBy", emp.CreatedByUserId);
                        cmd.Parameters.AddWithValue("@ComId", emp.CompanyId);
                        cmd.Parameters.AddWithValue("@DeptId", emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId", emp.BranchId);
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber);
                        cmd.Parameters.AddWithValue("@TitleId", emp.TitleId.HasValue ? (object)emp.TitleId.Value : DBNull.Value);
                        int newEmpId = (int)cmd.ExecuteScalar();
                        ActivityLogger.Log(ActivityLogger.Actions.Create, "Employee", newEmpId, $"Employee '{emp.Name}' created");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to save employee: {ex.Message}");
                return false;
            }
        }

        private void Edit()
        {
            var checkedEmployees = _allEmployees.Where(e => e.Selected).ToList();

            if (checkedEmployees.Count == 0)
            {
                RequestInfo?.Invoke("Information", "Please select at least one employee to edit.");
                return;
            }
            if (checkedEmployees.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one employee to edit.");
                return;
            }

            var empDto = LoadEmployeeForEdit(checkedEmployees[0].EmpId);
            if (empDto == null) return;

            RequestEditEmployee?.Invoke(empDto);
        }

        public EmployeeDto LoadEmployeeForEdit(int empId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT e.EmpId, e.Name, e.Position, e.Description, e.DateCreated, e.ComId, e.DeptId, e.BranchId, u.Name AS CreatedByName, e.EmployeeNumber, e.TitleId
                        FROM dbo.Employee e
                        LEFT JOIN dbo.[User] u ON e.Createdby = u.UserId
                        WHERE e.EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new EmployeeDto
                                {
                                    EmpId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Position = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    DateCreated = reader.GetDateTime(4),
                                    CompanyId = reader.GetInt32(5),
                                    DepartmentId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                    BranchId = reader.GetInt32(7),
                                    CreatedByName = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                                    EmployeeNumber = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    TitleId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load employee: {ex.Message}");
            }
            return null;
        }

        public bool UpdateEmployee(EmployeeDto emp)
        {
            try
            {
                var before = LoadEmployeeForEdit(emp.EmpId);
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.Employee
                        SET Name = @Name,
                            Position = @Position,
                            Description = @Description,
                            ComId = @ComId,
                            DeptId = @DeptId,
                            BranchId = @BranchId,
                            EmployeeNumber = @EmployeeNumber,
                            TitleId = @TitleId
                        WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", emp.EmpId);
                        cmd.Parameters.AddWithValue("@Name", emp.Name);
                        cmd.Parameters.AddWithValue("@Position", (object)emp.Position ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description", (object)emp.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ComId", emp.CompanyId);
                        cmd.Parameters.AddWithValue("@DeptId", emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId", emp.BranchId);
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber);
                        cmd.Parameters.AddWithValue("@TitleId", emp.TitleId.HasValue ? (object)emp.TitleId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                if (before != null && (before.DepartmentId != emp.DepartmentId || before.BranchId != emp.BranchId))
                {
                    var oldNames = GetDeptBranchNames(before.DepartmentId, before.BranchId);
                    var newNames = GetDeptBranchNames(emp.DepartmentId, emp.BranchId);

                    var requests = GetRequestsForEmployee(emp.EmpId);
                    var auditRepo = new ItemAuditTrailRepository();
                    var now = DateTime.Now;

                    foreach (var r in requests)
                    {
                        if (!r.ItemId.HasValue)
                            continue;

                        var action = before.DepartmentId != emp.DepartmentId && before.BranchId != emp.BranchId
                            ? "Department/Branch Updated"
                            : (before.DepartmentId != emp.DepartmentId ? "Department Transfer" : "Branch Relocation");

                        var notes = before.DepartmentId != emp.DepartmentId && before.BranchId != emp.BranchId
                            ? $"Employee moved from {oldNames.DepartmentName} / {oldNames.BranchName} to {newNames.DepartmentName} / {newNames.BranchName}"
                            : (before.DepartmentId != emp.DepartmentId
                                ? $"Department changed from {oldNames.DepartmentName} to {newNames.DepartmentName}"
                                : $"Branch changed from {oldNames.BranchName} to {newNames.BranchName}");

                        auditRepo.LogActionAsync(new ItemAuditTrailDto
                        {
                            ItemId = r.ItemId,
                            SerialNumber = r.SerialNumber,
                            Action = action,
                            ActionTime = now,
                            Status = "Completed",
                            EmployeeId = emp.EmpId,
                            EmployeeName = emp.Name,
                            DepartmentId = emp.DepartmentId,
                            DepartmentName = newNames.DepartmentName,
                            BranchId = emp.BranchId,
                            BranchName = newNames.BranchName,
                            ReferenceType = "Employee",
                            ReferenceId = emp.EmpId,
                            SetCode = r.SetCode,
                            Notes = notes,
                            CreatedBy = AppSession.CurrentUserName ?? "System"
                        }).GetAwaiter().GetResult();
                    }
                }
                ActivityLogger.Log(ActivityLogger.Actions.Update, "Employee", emp.EmpId, $"Employee '{emp.Name}' updated");
                return true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to update employee: {ex.Message}");
                return false;
            }
        }

        private OrgNames GetDeptBranchNames(int? deptId, int branchId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT
                        (SELECT Name FROM dbo.Department WHERE DeptId = @DeptId) AS DepartmentName,
                        (SELECT Name FROM dbo.Branch WHERE BranchId = @BranchId) AS BranchName", con))
                {
                    cmd.Parameters.AddWithValue("@DeptId", deptId.HasValue ? (object)deptId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return new OrgNames { DepartmentName = null, BranchName = null };

                        return new OrgNames
                        {
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName"))
                        };
                    }
                }
            }
        }

        private List<EmployeeRequestItem> GetRequestsForEmployee(int empId)
        {
            var results = new List<EmployeeRequestItem>();
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT r.ReqId, r.ItemId, i.SerialNumber, r.SetId, s.SetCode
                    FROM dbo.Request r
                    LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
                    LEFT JOIN dbo.[Set] s ON r.SetId = s.SetId
                    WHERE r.EmpId = @EmpId", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new EmployeeRequestItem
                            {
                                ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                                ItemId = reader.IsDBNull(reader.GetOrdinal("ItemId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ItemId")),
                                SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                                SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"))
                            });
                        }
                    }
                }
            }
            return results;
        }

        private class OrgNames
        {
            public string DepartmentName { get; set; }
            public string BranchName { get; set; }
        }

        private class EmployeeRequestItem
        {
            public int ReqId { get; set; }
            public int? ItemId { get; set; }
            public string SerialNumber { get; set; }
            public string SetCode { get; set; }
        }

        private void Archive()
        {
            var checkedEmployees = _allEmployees.Where(e => e.Selected).ToList();

            if (checkedEmployees.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one employee to archive.");
                return;
            }

            string message = checkedEmployees.Count == 1
                ? $"Are you sure you want to archive this employee?\n\nName: {checkedEmployees[0].Name}\nPosition: {checkedEmployees[0].Position ?? "N/A"}\nCompany: {checkedEmployees[0].CompanyName}\n\nThe employee will be moved to the archive."
                : $"Are you sure you want to archive the {checkedEmployees.Count} selected employees?\n\nAll selected employees will be moved to the archive.";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Archive", message) ?? false;
            if (!confirm) return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            string insertArchiveSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Employee', @EmpId, 1, GETDATE(), @ArchivedBy, 'Archived from Employee page')";

                            foreach (var employee in checkedEmployees)
                            {
                                using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@EmpId", employee.EmpId);
                                    cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();

                            foreach (var emp in checkedEmployees)
                                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Employee", emp.EmpId, $"Employee '{emp.Name}' archived");

                            string successMsg = checkedEmployees.Count == 1
                                ? "Employee archived successfully!"
                                : $"{checkedEmployees.Count} employees archived successfully!";

                            RequestInfo?.Invoke("Success", successMsg);
                            LoadEmployees();
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
                RequestError?.Invoke("Error", $"Error archiving employee(s):\n\n{ex.Message}");
            }
        }

        private void PermanentDelete()
        {
            var checkedEmployees = _allEmployees.Where(e => e.Selected).ToList();

            if (checkedEmployees.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one employee to delete.");
                return;
            }

            string message = checkedEmployees.Count == 1
                ? "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                  "This will PERMANENTLY delete this employee:\n\n" +
                  $"Name: {checkedEmployees[0].Name}\n" +
                  $"Position: {checkedEmployees[0].Position ?? "N/A"}\n" +
                  $"Company: {checkedEmployees[0].CompanyName}\n\n" +
                  "This action CANNOT be undone!\n\n" +
                  "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                  "⚠️ Use 'Archive' button instead for normal records.\n\n" +
                  "Are you absolutely sure you want to permanently delete?"
                : "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                  $"This will PERMANENTLY delete {checkedEmployees.Count} employee(s).\n\n" +
                  "This action CANNOT be undone!\n\n" +
                  "⚠️ Only proceed if these were DATA ENTRY ERRORS.\n" +
                  "⚠️ Use 'Archive' button instead for normal records.\n\n" +
                  "Are you absolutely sure you want to permanently delete the selected employees?";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Permanent Deletion", message) ?? false;
            if (!confirm) return;

            int successCount = 0;
            var failedEmployees = new List<string>();

            foreach (var employee in checkedEmployees)
            {
                if (DeleteEmployee(employee.EmpId))
                    successCount++;
                else
                    failedEmployees.Add(employee.Name);
            }

            if (failedEmployees.Count == 0)
            {
                string successMessage = checkedEmployees.Count == 1
                    ? "Employee permanently deleted!\n\n✔ Employee removed from database"
                    : $"{successCount} employees permanently deleted!\n\n✔ Employees removed from database";

                RequestInfo?.Invoke("Deleted Successfully", successMessage);
            }
            else
            {
                RequestWarning?.Invoke("Partial Success",
                    $"Completed with {successCount} success(es) and {failedEmployees.Count} failure(s).\n\n" +
                    "Failed employees:\n" + string.Join("\n", failedEmployees.Take(10)) +
                    (failedEmployees.Count > 10 ? $"\n... and {failedEmployees.Count - 10} more" : ""));
            }

            LoadEmployees();
        }

        /// <summary>Matches the original exactly: catches its own exceptions and shows an
        /// immediate error dialog, returning false rather than throwing.</summary>
        private bool DeleteEmployee(int empId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.Employee WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);
                        cmd.ExecuteNonQuery();
                    }
                }
                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Employee", empId, $"Employee ID {empId} permanently deleted");
                return true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to delete employee: {ex.Message}\n\nNote: Cannot delete if there are requests assigned to this employee.");
                return false;
            }
        }
    }
}
