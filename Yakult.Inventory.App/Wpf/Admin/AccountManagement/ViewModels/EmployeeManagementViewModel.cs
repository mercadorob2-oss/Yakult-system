using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels
{
    public class EmployeeManagementViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly string _cs;

        private List<EmployeeManagementDto> _all      = new List<EmployeeManagementDto>();
        private List<EmployeeManagementDto> _filtered = new List<EmployeeManagementDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText   = string.Empty;
        private string _filterOption = "All";
        private bool   _showArchived;
        private int    _totalRecords;
        private int    _activeCount;
        private string _pageInfo   = "Page 1 of 1  (0 employees)";
        private bool   _canGoPrev;
        private bool   _canGoNext;
        private string _statusMessage = string.Empty;

        public ObservableCollection<EmployeeManagementDto> PagedRows { get; }
            = new ObservableCollection<EmployeeManagementDto>();

        public List<EmployeeManagementDto> AllRows => _all;

        /// <summary>
        /// Rows matching the current search text, status dropdown, per-column header filters,
        /// and Show Archived checkbox — i.e. everything currently visible in the grid across
        /// all pages. Used so Export reflects what's on screen rather than the full table.
        /// </summary>
        public List<EmployeeManagementDto> GetFilteredRows() => _filtered;

        public int  SelectedCount => _all.Count(e => e.IsSelected);
        public bool HasSelection  => SelectedCount > 0;

        public List<string>            EmailCache { get; private set; } = new List<string>();
        public Dictionary<string, int> EmailIdMap { get; private set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool   IsLoading     { get => _isLoading;     set => SetField(ref _isLoading,     value); }
        public int    TotalRecords  { get => _totalRecords;  set => SetField(ref _totalRecords,  value); }
        public int    ActiveCount   { get => _activeCount;   set => SetField(ref _activeCount,   value); }
        public string PageInfo      { get => _pageInfo;      set => SetField(ref _pageInfo,      value); }
        public bool   CanGoPrev     { get => _canGoPrev;     set => SetField(ref _canGoPrev,     value); }
        public bool   CanGoNext     { get => _canGoNext;     set => SetField(ref _canGoNext,     value); }
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public string FilterOption
        {
            get => _filterOption;
            set { if (SetField(ref _filterOption, value)) ApplyFilter(); }
        }

        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetField(ref _showArchived, value)) ApplyFilter(); }
        }

        public EmployeeManagementViewModel()
        {
            _cs = DatabaseConfig.ConnectionString;
        }

        public async Task LoadAsync()
        {
            _columnFilters.Clear();
            _sortColumn    = null;
            _sortAscending = true;
            IsLoading = true;
            try
            {
                _all = new List<EmployeeManagementDto>();

                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();

                    bool hasDeptEmailTable;
                    using (var chk = new SqlCommand("SELECT CAST(OBJECT_ID('dbo.DepartmentEmail') AS INT)", con))
                    {
                        var res = await chk.ExecuteScalarAsync();
                        hasDeptEmailTable = res != null && res != DBNull.Value;
                    }

                    string deptEmailSelect = hasDeptEmailTable ? "dea.EmailAddress AS DepartmentEmail," : "NULL AS DepartmentEmail,";
                    string deptEmailJoin   = hasDeptEmailTable
                        ? @"LEFT JOIN dbo.DepartmentEmail de
                                   ON de.CompanyName    = c.Name AND de.DepartmentName = d.Name
                            LEFT JOIN dbo.EmailAddress dea
                                   ON dea.EmailId = de.EmailAddressId"
                        : "";

                    string sql = $@"
                        SELECT
                            e.EmpId,
                            e.EmployeeNumber,
                            e.Name,
                            e.Position,
                            e.Active,
                            c.Name  AS CompanyName,
                            b.Name  AS BranchName,
                            d.Name  AS DepartmentName,
                            {deptEmailSelect}
                            branch_ea.EmailAddress AS BranchEmail,
                            emp_ea.EmailAddress    AS PrimaryEmail,
                            CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived,
                            t.Code  AS TitleCode
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Company    c   ON e.ComId    = c.ComId
                        LEFT JOIN dbo.Branch     b   ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.Department d   ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.Title      t   ON e.TitleId  = t.TitleId
                        LEFT JOIN dbo.EmployeeEmail ee
                               ON ee.EmpId = e.EmpId AND ee.IsPrimary = 1 AND ee.IsActive = 1
                        LEFT JOIN dbo.EmailAddress emp_ea
                               ON emp_ea.EmailId = ee.EmailId AND emp_ea.IsActive = 1
                        {deptEmailJoin}
                        LEFT JOIN dbo.DepartmentAccount da_br
                               ON da_br.CompanyName    = c.Name
                              AND da_br.DepartmentName = d.Name
                              AND da_br.BranchName     = b.Name
                        LEFT JOIN dbo.EmailAddress branch_ea
                               ON branch_ea.EmailId = da_br.EmailAddressId
                        LEFT JOIN dbo.ArchiveStatus arc
                               ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new EmployeeManagementDto
                            {
                                EmpId           = reader.GetInt32(0),
                                EmployeeNumber  = reader.IsDBNull(1)  ? "" : reader.GetString(1),
                                Name            = reader.GetString(2),
                                Position        = reader.IsDBNull(3)  ? "" : reader.GetString(3),
                                Active          = reader.GetBoolean(4),
                                CompanyName     = reader.IsDBNull(5)  ? "" : reader.GetString(5),
                                BranchName      = reader.IsDBNull(6)  ? "" : reader.GetString(6),
                                DepartmentName  = reader.IsDBNull(7)  ? "" : reader.GetString(7),
                                DepartmentEmail = reader.IsDBNull(8)  ? "(None)" : reader.GetString(8),
                                BranchEmail     = reader.IsDBNull(9)  ? "(None)" : reader.GetString(9),
                                PrimaryEmail    = reader.IsDBNull(10) ? "(None)" : reader.GetString(10),
                                IsArchived      = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                                TitleCode       = reader.IsDBNull(12) ? "" : reader.GetString(12),
                            };
                            dto.PropertyChanged += OnEmployeeRowPropertyChanged;
                            _all.Add(dto);
                        }
                    }

                    // Email cache for autocomplete
                    var emailList = new List<string>();
                    var emailMap  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    const string emailSql = @"SELECT EmailId, EmailAddress FROM dbo.EmailAddress WHERE IsActive = 1 ORDER BY EmailAddress";
                    using (var cmd = new SqlCommand(emailSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int    id    = reader.GetInt32(0);
                            string addr  = reader.GetString(1);
                            emailList.Add(addr);
                            emailMap[addr] = id;
                        }
                    }
                    EmailCache = emailList;
                    EmailIdMap = emailMap;
                }

                TotalRecords = _all.Count;
                ActiveCount  = _all.Count(e => e.Active && !e.IsArchived);
                _currentPage = 1;
                ApplyFilter();

                // _all was just replaced wholesale with fresh rows (all IsSelected = false).
                // Nothing on the new rows has fired a PropertyChanged yet, so these computed
                // properties won't refresh on their own — notify explicitly or the "N Selected"
                // badge keeps showing a stale count from the discarded rows.
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ApplyFilter()
        {
            var search    = (_searchText ?? "").Trim().ToLowerInvariant();
            var filter    = _filterOption ?? "All";
            bool archived = _showArchived;

            _filtered = _all.Where(emp =>
            {
                if (!archived && emp.IsArchived) return false;

                if (!string.IsNullOrEmpty(search))
                {
                    var tokens = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool matches = tokens.All(token =>
                        (emp.Name           ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.EmployeeNumber ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.TitleCode      ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.Position       ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.CompanyName    ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.DepartmentName ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.BranchName     ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.DepartmentEmail ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.BranchEmail    ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.PrimaryEmail   ?? "").ToLowerInvariant().Contains(token));
                    if (!matches) return false;
                }

                if (filter == "Active Only"   && !emp.Active) return false;
                if (filter == "Inactive Only" &&  emp.Active) return false;

                return true;
            }).ToList();

            // Apply per-column filters on top of search/status filter
            foreach (var kv in _columnFilters)
            {
                string col     = kv.Key;
                var    allowed = kv.Value;
                _filtered = _filtered.Where(emp => allowed.Contains(GetEmployeeColumnValue(emp, col) ?? "")).ToList();
            }

            IEnumerable<EmployeeManagementDto> ordered = _filtered;

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                ordered = _sortAscending
                    ? ordered.OrderBy(e => GetEmployeeColumnValue(e, _sortColumn) ?? "", System.StringComparer.OrdinalIgnoreCase)
                    : ordered.OrderByDescending(e => GetEmployeeColumnValue(e, _sortColumn) ?? "", System.StringComparer.OrdinalIgnoreCase);
            }

            // With Show Archived on, float archived rows to the top (stable sort — preserves
            // the column/name ordering established above within each group).
            if (archived)
                ordered = ordered.OrderByDescending(e => e.IsArchived);

            _filtered = ordered.ToList();

            RebuildPagedRows();
        }

        private static string GetEmployeeColumnValue(EmployeeManagementDto emp, string column)
        {
            switch (column)
            {
                case "Emp #":          return emp.EmployeeNumber;
                case "Title":          return emp.TitleCode;
                case "Name":           return emp.Name;
                case "Position":       return emp.Position;
                case "Company":        return emp.CompanyName;
                case "Dept":           return emp.DepartmentName;
                case "Branch":         return emp.BranchName;
                case "Status":         return emp.Active ? "Active" : "Inactive";
                case "Dept Email":     return emp.DepartmentEmail ?? "";
                case "Branch Email":   return emp.BranchEmail ?? "";
                case "Personal Email": return emp.PrimaryEmail ?? "";
                default:               return null;
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
            _currentPage = 1;
            ApplyFilter();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            _currentPage = 1;
            ApplyFilter();
        }

        public void SetSort(string column, bool ascending)
        {
            _sortColumn    = column;
            _sortAscending = ascending;
            _currentPage   = 1;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            IEnumerable<string> values;
            switch (column)
            {
                case "Emp #":          values = _all.Select(e => e.EmployeeNumber ?? ""); break;
                case "Title":          values = _all.Select(e => e.TitleCode ?? ""); break;
                case "Name":           values = _all.Select(e => e.Name ?? ""); break;
                case "Position":       values = _all.Select(e => e.Position ?? ""); break;
                case "Company":        values = _all.Select(e => e.CompanyName ?? ""); break;
                case "Dept":           values = _all.Select(e => e.DepartmentName ?? ""); break;
                case "Branch":         values = _all.Select(e => e.BranchName ?? ""); break;
                case "Status":         values = _all.Select(e => e.Active ? "Active" : "Inactive"); break;
                case "Dept Email":     values = _all.Select(e => e.DepartmentEmail ?? ""); break;
                case "Branch Email":   values = _all.Select(e => e.BranchEmail ?? ""); break;
                case "Personal Email": values = _all.Select(e => e.PrimaryEmail ?? ""); break;
                default: return new List<string>();
            }
            return values.Where(v => !string.IsNullOrEmpty(v))
                         .Distinct(System.StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v)
                         .ToList();
        }

        public void GoToNextPage()  { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }
        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RebuildPagedRows(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPagedRows(); } }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} employee{(_filtered.Count == 1 ? "" : "s")})";
        }

        // ── Email Save Operations ──────────────────────────────────────────────

        public async Task<int> InsertOrGetEmailIdAsync(string email)
        {
            const string sql = @"
                UPDATE dbo.EmailAddress SET IsActive = 1 WHERE EmailAddress = @Email AND IsActive = 0;
                IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
                    INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive) VALUES (@Email, @Email, 1);
                SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    EmailIdMap[email] = id;
                    if (!EmailCache.Contains(email, StringComparer.OrdinalIgnoreCase))
                        EmailCache.Add(email);
                    return id;
                }
            }
        }

        public async Task SaveDeptEmailAsync(string company, string department, int emailId)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.DepartmentEmail WHERE CompanyName = @Co AND DepartmentName = @Dept)
                    UPDATE dbo.DepartmentEmail SET EmailAddressId = @Id WHERE CompanyName = @Co AND DepartmentName = @Dept
                ELSE
                    INSERT INTO dbo.DepartmentEmail (CompanyName, DepartmentName, EmailAddressId) VALUES (@Co, @Dept, @Id)";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Co",   company);
                    cmd.Parameters.AddWithValue("@Dept", department);
                    cmd.Parameters.AddWithValue("@Id",   emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            string emailText = EmailIdMap.FirstOrDefault(kv => kv.Value == emailId).Key ?? string.Empty;
            foreach (var row in _all.Where(r => r.CompanyName == company && r.DepartmentName == department))
                row.DepartmentEmail = emailText;
        }

        public async Task DeleteDeptEmailAsync(string company, string department)
        {
            const string sql = "DELETE FROM dbo.DepartmentEmail WHERE CompanyName = @Co AND DepartmentName = @Dept";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Co",   company);
                    cmd.Parameters.AddWithValue("@Dept", department);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            foreach (var row in _all.Where(r => r.CompanyName == company && r.DepartmentName == department))
                row.DepartmentEmail = "(None)";
        }

        public async Task SaveBranchEmailAsync(string company, string department, string branch, int emailId)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.DepartmentAccount WHERE CompanyName=@Co AND DepartmentName=@Dept AND BranchName=@Br)
                    UPDATE dbo.DepartmentAccount SET EmailAddressId = @Id
                    WHERE CompanyName=@Co AND DepartmentName=@Dept AND BranchName=@Br
                ELSE
                    INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName, EmailAddressId)
                    VALUES (@Co, @Dept, @Br, @Id)";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Co",   company);
                    cmd.Parameters.AddWithValue("@Dept", department);
                    cmd.Parameters.AddWithValue("@Br",   branch);
                    cmd.Parameters.AddWithValue("@Id",   emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            string emailText = EmailIdMap.FirstOrDefault(kv => kv.Value == emailId).Key ?? string.Empty;
            foreach (var row in _all.Where(r => r.CompanyName == company && r.DepartmentName == department && r.BranchName == branch))
                row.BranchEmail = emailText;
        }

        public async Task DeleteBranchEmailAsync(string company, string department, string branch)
        {
            const string sql = @"
                UPDATE dbo.DepartmentAccount SET EmailAddressId = NULL
                WHERE CompanyName=@Co AND DepartmentName=@Dept AND BranchName=@Br";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Co",   company);
                    cmd.Parameters.AddWithValue("@Dept", department);
                    cmd.Parameters.AddWithValue("@Br",   branch);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            foreach (var row in _all.Where(r => r.CompanyName == company && r.DepartmentName == department && r.BranchName == branch))
                row.BranchEmail = "(None)";
        }

        public async Task SavePersonalEmailAsync(int empId, string email)
        {
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();

                // Always resolve through the idempotent upsert: the EmailIdMap cache can hold a
                // stale id if the address was deleted elsewhere (e.g. Email Configuration), which
                // would make the EmployeeEmail insert fail with FK_EmployeeEmail_Email.
                int emailId = await InsertOrGetEmailIdAsync(email);

                const string clearSql = "UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId";
                using (var cmd = new SqlCommand(clearSql, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // EmailRole is NOT NULL with no default; 'Work' matches the value used by the
                // other EmployeeEmail insert sites (EmployeeManagementPage, EmployeeEmailBindingPage).
                const string upsertSql = @"
                    IF EXISTS (SELECT 1 FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId)
                        UPDATE dbo.EmployeeEmail SET IsPrimary = 1, IsActive = 1 WHERE EmpId = @EmpId AND EmailId = @EmailId
                    ELSE
                        INSERT INTO dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive) VALUES (@EmpId, @EmailId, 'Work', 1, 1)";
                using (var cmd = new SqlCommand(upsertSql, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId",   empId);
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            foreach (var row in _all.Where(r => r.EmpId == empId))
                row.PrimaryEmail = email;
        }

        public async Task DeletePersonalEmailAsync(int empId)
        {
            const string sql = "UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            foreach (var row in _all.Where(r => r.EmpId == empId))
                row.PrimaryEmail = "(None)";
        }

        public async Task ArchiveEmployeeAsync(int empId, string archivedBy)
        {
            const string sql = @"
                INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                VALUES ('Employee', @EmpId, 1, GETDATE(), @By, 'Archived from Employee Management')";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    cmd.Parameters.AddWithValue("@By",    archivedBy ?? "System");
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try { _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }

        // ── Selection ────────────────────────────────────────────────────────

        private void OnEmployeeRowPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(EmployeeManagementDto.IsSelected)) return;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }

        public List<EmployeeManagementDto> GetSelectedItems()
            => _all.Where(e => e.IsSelected).ToList();

        public void SetSelected(int empId, bool selected)
        {
            var row = _all.FirstOrDefault(e => e.EmpId == empId);
            if (row != null) row.IsSelected = selected;
        }

        public void ClearAllSelections()
        {
            foreach (var row in _all.Where(e => e.IsSelected))
                row.IsSelected = false;
        }
    }
}
