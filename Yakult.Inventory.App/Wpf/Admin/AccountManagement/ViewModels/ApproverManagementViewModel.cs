using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels
{
    public sealed class ApproverRow
    {
        public string EmployeeNumber { get; set; }
        public string Title          { get; set; }
        public string EmployeeName   { get; set; }
        public string Position       { get; set; }
        public string Department     { get; set; }
        public string Company        { get; set; }
        public string Branch         { get; set; }
        public string IsApprover     { get; set; }
        public string SystemRoles    { get; set; }
        public bool   IsArchived     { get; set; }
    }

    public sealed class ApproverTitleEntry
    {
        public string PositionTitle { get; set; }
        public string ApprovalRole  { get; set; }
        public int    RolePriority  { get; set; }
    }

    public class ApproverManagementViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly string _cs;

        private List<ApproverRow> _all      = new List<ApproverRow>();
        private List<ApproverRow> _filtered = new List<ApproverRow>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText     = string.Empty;
        private bool   _showArchived;
        private string _pageInfo = "Page 0 of 0 (0 rows)";
        private bool   _canGoPrev;
        private bool   _canGoNext;
        private int    _totalRecords;
        private int    _approverCount;

        public ObservableCollection<ApproverRow> PagedRows { get; } = new ObservableCollection<ApproverRow>();

        public List<ApproverTitleEntry> ApproverTitleEntries { get; private set; } = new List<ApproverTitleEntry>();
        public List<ApproverRow> AllRows => _all;

        public bool   IsLoading      { get => _isLoading;      set => SetField(ref _isLoading,      value); }
        public string PageInfo       { get => _pageInfo;       set => SetField(ref _pageInfo,       value); }
        public bool   CanGoPrev      { get => _canGoPrev;      set => SetField(ref _canGoPrev,      value); }
        public bool   CanGoNext      { get => _canGoNext;      set => SetField(ref _canGoNext,      value); }
        public int    TotalRecords   { get => _totalRecords;   set => SetField(ref _totalRecords,   value); }
        public int    ApproverCount  { get => _approverCount;  set => SetField(ref _approverCount,  value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetField(ref _showArchived, value)) ApplyFilter(); }
        }

        // ── Position groups (fallback when dbo.ApprovalRoleTitle is empty) ────
        private static readonly List<(string Key, string Display)> CoordinatorPositions = new List<(string, string)>
        {
            ("COORDINATOR",                "Coordinator"),
            ("ACCOUNT COORDINATOR",        "Account Coordinator"),
            ("ACTING ACCOUNT COORDINATOR", "Acting Account Coordinator"),
            ("ASST. COORDINATOR",          "Asst. Coordinator"),
            ("ASSISTANT COORDINATOR",      "Assistant Coordinator"),
            ("LADY COORDINATOR",           "Lady Coordinator"),
        };

        private static readonly List<(string Key, string Display)> ManagerPositions = new List<(string, string)>
        {
            ("MANAGER",                  "Manager"),
            ("ASST. MANAGER",            "Asst. Manager"),
            ("ASSISTANT MANAGER",        "Assistant Manager"),
            ("JR. ASST. MANAGER",        "Jr. Asst. Manager"),
            ("JUNIOR ASSISTANT MANAGER", "Junior Asst. Manager"),
            ("ACTING JR. ASST. MANAGER", "Acting Jr. Asst. Manager"),
        };

        private static readonly List<(string Key, string Display)> SupervisorPositions = new List<(string, string)>
        {
            ("SUPERVISOR", "Supervisor"),
        };

        private static readonly HashSet<string> HigherUpPositions = new HashSet<string>(
            CoordinatorPositions.Select(p => p.Key)
                .Concat(ManagerPositions.Select(p => p.Key))
                .Concat(SupervisorPositions.Select(p => p.Key)),
            StringComparer.OrdinalIgnoreCase);

        public ApproverManagementViewModel()
        {
            _cs = DatabaseConfig.ConnectionString;
        }

        private async Task<List<ApproverTitleEntry>> LoadApproverTitlesFromDbAsync(SqlConnection con)
        {
            var results = new List<ApproverTitleEntry>();
            try
            {
                using (var cmd = new SqlCommand(
                    "SELECT PositionTitle, ApprovalRole, RolePriority FROM dbo.ApprovalRoleTitle WHERE IsActive = 1 ORDER BY RolePriority DESC, PositionTitle", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        results.Add(new ApproverTitleEntry
                        {
                            PositionTitle = r.GetString(0),
                            ApprovalRole  = r.GetString(1),
                            RolePriority  = r.GetInt32(2)
                        });
            }
            catch { /* table may not exist yet — caller falls back to hardcoded */ }
            return results;
        }

        public async Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(_cs)) return;

            IsLoading = true;
            try
            {
                var results = new List<ApproverRow>();

                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();

                    ApproverTitleEntries = await LoadApproverTitlesFromDbAsync(con);

                    IEnumerable<string> positionKeys = ApproverTitleEntries.Count > 0
                        ? ApproverTitleEntries.Select(t => t.PositionTitle)
                        : (IEnumerable<string>)HigherUpPositions;

                    var posArray  = positionKeys.ToArray();
                    var posParams = posArray.Select((p, i) => $"@p{i}").ToArray();
                    string inClause = string.Join(", ", posParams);

                    string sql = $@"
                        SELECT
                            e.EmployeeNumber,
                            t.Code,
                            e.Name         AS EmployeeName,
                            e.Position,
                            d.Name         AS Department,
                            c.Name         AS Company,
                            b.Name         AS Branch,
                            CASE WHEN UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause}) THEN 'Yes' ELSE 'No' END AS IsApprover,
                            ISNULL(
                                STUFF((
                                    SELECT ', ' + r.RoleName
                                    FROM dbo.[User] u2
                                    INNER JOIN dbo.UserRole ur ON ur.UserId = u2.UserId
                                    INNER JOIN dbo.Role r      ON r.RoleId  = ur.RoleId AND r.IsActive = 1
                                    WHERE u2.EmpId = e.EmpId
                                    FOR XML PATH(''), TYPE
                                ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
                            'No Account') AS SystemRoles,
                            CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Title        t   ON e.TitleId  = t.TitleId
                        LEFT JOIN dbo.Department   d   ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.Company      c   ON e.ComId    = c.ComId
                        LEFT JOIN dbo.Branch       b   ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause})
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        for (int i = 0; i < posArray.Length; i++)
                            cmd.Parameters.AddWithValue($"@p{i}", posArray[i].ToUpperInvariant());

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(new ApproverRow
                                {
                                    EmployeeNumber = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Title          = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    EmployeeName   = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Position       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Department     = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Company        = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    Branch         = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    IsApprover     = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    SystemRoles    = reader.IsDBNull(8) ? "No Account" : reader.GetString(8),
                                    IsArchived     = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                                });
                            }
                        }
                    }
                }

                _all = results;
                TotalRecords  = _all.Count;
                ApproverCount = _all.Count(r => string.Equals(r.IsApprover, "Yes", StringComparison.OrdinalIgnoreCase));
                _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Produces a sort key so "141X" (3-digit manager code) sorts beside "141"
        // rather than after all 4-digit numbers: pads the leading digit run to 10 chars.
        private static string EmpNumSortKey(string empNum)
        {
            if (string.IsNullOrEmpty(empNum)) return "";
            int i = 0;
            while (i < empNum.Length && char.IsDigit(empNum[i])) i++;
            return empNum.Substring(0, i).PadLeft(10, '0') + empNum.Substring(i).ToUpperInvariant();
        }

        private static string GetColumnValue(ApproverRow r, string column)
        {
            switch (column)
            {
                case "EmployeeNumber": return r.EmployeeNumber;
                case "Title":          return r.Title;
                case "EmployeeName":   return r.EmployeeName;
                case "Position":       return r.Position;
                case "Department":     return r.Department;
                case "Company":        return r.Company;
                case "Branch":         return r.Branch;
                case "IsApprover":     return r.IsApprover;
                case "SystemRoles":    return r.SystemRoles;
                default:               return null;
            }
        }

        public void ApplyFilter()
        {
            string q = (_searchText ?? "").Trim();

            IEnumerable<ApproverRow> filtered = _showArchived
                ? _all
                : _all.Where(r => !r.IsArchived);

            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(x =>
                    x.EmployeeNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.EmployeeName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Position.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Department.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Branch.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                string col = kvp.Key;
                filtered = filtered.Where(r => selected.Contains(GetColumnValue(r, col) ?? "", StringComparer.OrdinalIgnoreCase));
            }

            _filtered = filtered.ToList();

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                Func<ApproverRow, string> key = _sortColumn == "EmployeeNumber"
                    ? (Func<ApproverRow, string>)(r => EmpNumSortKey(r.EmployeeNumber))
                    : (r => GetColumnValue(r, _sortColumn) ?? "");

                _filtered = _sortAscending
                    ? _filtered.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                    : _filtered.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
            }

            _currentPage = 1;
            RebuildPagedRows();
        }

        public HashSet<string> GetColumnFilter(string column)
            => _columnFilters.TryGetValue(column, out var f) ? f : null;

        public void SetColumnFilter(string column, HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                _columnFilters.Remove(column);
            else
                _columnFilters[column] = values;
            ApplyFilter();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            ApplyFilter();
        }

        public void SetSort(string column, bool ascending)
        {
            _sortColumn    = column;
            _sortAscending = ascending;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            return _all.Select(r => GetColumnValue(r, column) ?? "")
                       .Where(v => !string.IsNullOrEmpty(v))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
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
            PageInfo  = _filtered.Count == 0
                ? "Page 0 of 0 (0 rows)"
                : $"Page {_currentPage} of {_totalPages} ({_filtered.Count} rows)";
        }
    }
}
