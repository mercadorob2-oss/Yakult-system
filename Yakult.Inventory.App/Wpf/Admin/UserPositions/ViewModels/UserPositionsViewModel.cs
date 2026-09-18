using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.UserPositions.ViewModels
{
    public sealed class UserPositionRow
    {
        public string Position      { get; set; }
        public string CompanyName   { get; set; }
        public string BranchName    { get; set; }
        public int    EmployeeCount { get; set; }
    }

    public class UserPositionsViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly string _cs;

        private List<UserPositionRow> _all      = new List<UserPositionRow>();
        private List<UserPositionRow> _filtered = new List<UserPositionRow>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText   = string.Empty;
        private string _pageInfo     = "Page 0 of 0 (0 rows)";
        private bool   _canGoPrev;
        private bool   _canGoNext;
        private int    _totalRecords;
        private int    _totalEmployees;

        public ObservableCollection<UserPositionRow> PagedRows { get; } = new ObservableCollection<UserPositionRow>();

        public List<UserPositionRow> AllRows => _all;

        public bool   IsLoading      { get => _isLoading;      set => SetField(ref _isLoading,      value); }
        public string PageInfo       { get => _pageInfo;       set => SetField(ref _pageInfo,       value); }
        public bool   CanGoPrev      { get => _canGoPrev;      set => SetField(ref _canGoPrev,      value); }
        public bool   CanGoNext      { get => _canGoNext;      set => SetField(ref _canGoNext,      value); }
        public int    TotalRecords   { get => _totalRecords;   set => SetField(ref _totalRecords,   value); }
        public int    TotalEmployees { get => _totalEmployees; set => SetField(ref _totalEmployees, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public UserPositionsViewModel()
        {
            _cs = DatabaseConfig.ConnectionString;
        }

        public async Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(_cs)) return;

            IsLoading = true;
            try
            {
                var results = new List<UserPositionRow>();

                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT Position, CompanyName, BranchName, EmployeeCount
                        FROM   dbo.vw_UserPositionSummary
                        ORDER  BY Position, CompanyName, BranchName";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new UserPositionRow
                            {
                                Position      = reader["Position"].ToString(),
                                CompanyName   = reader["CompanyName"].ToString(),
                                BranchName    = reader["BranchName"].ToString(),
                                EmployeeCount = Convert.ToInt32(reader["EmployeeCount"])
                            });
                        }
                    }
                }

                _all = results;
                TotalRecords   = _all.Count;
                TotalEmployees = _all.Sum(r => r.EmployeeCount);
                _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string GetColumnValue(UserPositionRow r, string column)
        {
            switch (column)
            {
                case "Position": return r.Position;
                case "Company":  return r.CompanyName;
                case "Branch":   return r.BranchName;
                case "Count":    return r.EmployeeCount.ToString();
                default:         return null;
            }
        }

        public void ApplyFilter()
        {
            string q = (_searchText ?? "").Trim();

            IEnumerable<UserPositionRow> filtered = _all;

            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(x =>
                    (x.Position ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (x.CompanyName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (x.BranchName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
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
                Func<UserPositionRow, string> key = _sortColumn == "Count"
                    ? (Func<UserPositionRow, string>)(r => r.EmployeeCount.ToString("D10"))
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

        /// <summary>
        /// Renames a position across every employee currently assigned to it within the
        /// given company/branch. Returns the number of affected employee rows.
        /// </summary>
        public async Task<int> RenamePositionAsync(UserPositionRow row, string newPosition)
        {
            int affected;
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();

                const string sql = @"
                    UPDATE e
                    SET    e.Position = @NewPosition
                    FROM   dbo.Employee e
                    INNER JOIN dbo.Company c ON e.ComId    = c.ComId    AND c.Name = @CompanyName
                    INNER JOIN dbo.Branch  b ON e.BranchId = b.BranchId AND b.Name = @BranchName
                    WHERE  (@OldPosition = '(No Position)' AND e.Position IS NULL)
                        OR e.Position = @OldPosition";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@NewPosition", newPosition);
                    cmd.Parameters.AddWithValue("@OldPosition", row.Position);
                    cmd.Parameters.AddWithValue("@CompanyName", row.CompanyName);
                    cmd.Parameters.AddWithValue("@BranchName",  row.BranchName);
                    affected = await cmd.ExecuteNonQueryAsync();
                }
            }

            foreach (var r in _all.Where(r =>
                r.Position    == row.Position &&
                r.CompanyName == row.CompanyName &&
                r.BranchName  == row.BranchName))
            {
                r.Position = newPosition;
            }

            return affected;
        }
    }
}
