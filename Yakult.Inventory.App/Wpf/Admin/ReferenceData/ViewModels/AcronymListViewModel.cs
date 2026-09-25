using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels
{
    public enum AcronymKind
    {
        Branch,
        Department
    }

    public sealed class AcronymRow : ViewModelBase
    {
        private string _acronym;

        public int    Id     { get; set; }
        public string Name   { get; set; }

        /// <summary>Branch: BranchType. Department: Section (searched, not shown as a column).</summary>
        public string Detail { get; set; }

        public bool   Active { get; set; }
        public string Status => Active ? "Active" : "Inactive";

        public string Acronym { get => _acronym; set => SetField(ref _acronym, value); }
    }

    /// <summary>
    /// Admin Portal > Reference Data > Branch Acronyms / Dept Acronyms (WPF). One view model for
    /// both pages: they differ only in table (dbo.Branch / dbo.Department), the detail column
    /// (BranchType / Section) and the Branch page's Type filter.
    /// </summary>
    public sealed class AcronymListViewModel : ViewModelBase
    {
        public const string AllTypes = "All Types";
        private const int PageSize = 15;

        private readonly string _cs = DatabaseConfig.ConnectionString;

        private List<AcronymRow> _all = new List<AcronymRow>();
        private List<AcronymRow> _filtered = new List<AcronymRow>();
        private int _currentPage = 1;
        private int _totalPages = 1;
        private string _sortColumn;
        private bool _sortAscending = true;

        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _filterOption = "All";
        private string _selectedType = AllTypes;
        private string _pageInfo = string.Empty;
        private bool _canGoPrev;
        private bool _canGoNext;
        private string _statusMessage = string.Empty;

        public AcronymListViewModel(AcronymKind kind)
        {
            Kind = kind;
        }

        public AcronymKind Kind { get; }
        public bool IsBranch => Kind == AcronymKind.Branch;

        public string Title      => IsBranch ? "Branch Acronyms" : "Department Acronyms";
        public string NameHeader => IsBranch ? "Branch Name" : "Department Name";
        public string SearchHint => IsBranch
            ? "Search by branch name, type, or acronym"
            : "Search by department name, section, or acronym";
        private string ItemNounPlural => IsBranch ? "branches" : "departments";
        private string ItemNoun       => IsBranch ? "branch" : "department";

        public ObservableCollection<AcronymRow> PagedRows { get; } = new ObservableCollection<AcronymRow>();
        public ObservableCollection<string> TypeOptions { get; } = new ObservableCollection<string> { AllTypes };

        public bool   IsLoading     { get => _isLoading;     private set => SetField(ref _isLoading, value); }
        public string PageInfo      { get => _pageInfo;      private set => SetField(ref _pageInfo, value); }
        public bool   CanGoPrev     { get => _canGoPrev;     private set => SetField(ref _canGoPrev, value); }
        public bool   CanGoNext     { get => _canGoNext;     private set => SetField(ref _canGoNext, value); }
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        public string FilterOption
        {
            get => _filterOption;
            set { if (SetField(ref _filterOption, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        public string SelectedType
        {
            get => _selectedType;
            set { if (SetField(ref _selectedType, value ?? AllTypes)) { _currentPage = 1; ApplyFilter(); } }
        }

        public async Task LoadAsync(bool keepPage = false)
        {
            IsLoading = true;
            try
            {
                string sql = IsBranch
                    ? "SELECT BranchId, Name, BranchType, Acronym, Active FROM dbo.Branch ORDER BY Name"
                    : "SELECT DeptId, Name, Section, Acronym, Active FROM dbo.Department ORDER BY Name";

                var rows = new List<AcronymRow>();
                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            rows.Add(new AcronymRow
                            {
                                Id      = reader.GetInt32(0),
                                Name    = reader.GetString(1),
                                Detail  = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Acronym = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Active  = reader.GetBoolean(4)
                            });
                        }
                    }
                }

                _all = rows;

                if (IsBranch)
                {
                    string keepType = _selectedType;
                    TypeOptions.Clear();
                    TypeOptions.Add(AllTypes);
                    foreach (var t in _all.Select(r => string.IsNullOrWhiteSpace(r.Detail) ? "(None)" : r.Detail)
                                          .Distinct(StringComparer.OrdinalIgnoreCase)
                                          .OrderBy(t => t))
                        TypeOptions.Add(t);

                    _selectedType = TypeOptions.Contains(keepType) ? keepType : AllTypes;
                    OnPropertyChanged(nameof(SelectedType));
                }

                if (!keepPage) _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ResetSort()
        {
            _sortColumn = null;
            _sortAscending = true;
        }

        public void SetSort(string column, bool ascending)
        {
            _sortColumn = column;
            _sortAscending = ascending;
            _currentPage = 1;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var q = (_searchText ?? "").Trim().ToLowerInvariant();
            var filter = _filterOption ?? "All";
            var type = _selectedType ?? AllTypes;

            IEnumerable<AcronymRow> rows = _all.Where(r =>
            {
                if (!string.IsNullOrEmpty(q))
                {
                    bool match = (r.Name ?? "").ToLowerInvariant().Contains(q)
                              || (r.Detail ?? "").ToLowerInvariant().Contains(q)
                              || (r.Acronym ?? "").ToLowerInvariant().Contains(q);
                    if (!match) return false;
                }

                if (filter == "Active Only"   && !r.Active) return false;
                if (filter == "Inactive Only" &&  r.Active) return false;

                if (IsBranch && type != AllTypes)
                {
                    string val = string.IsNullOrWhiteSpace(r.Detail) ? "(None)" : r.Detail;
                    if (!string.Equals(val, type, StringComparison.OrdinalIgnoreCase)) return false;
                }

                return true;
            });

            switch (_sortColumn)
            {
                case nameof(AcronymRow.Name):
                    rows = _sortAscending ? rows.OrderBy(r => r.Name ?? "", StringComparer.OrdinalIgnoreCase)
                                          : rows.OrderByDescending(r => r.Name ?? "", StringComparer.OrdinalIgnoreCase);
                    break;
                case nameof(AcronymRow.Detail):
                    rows = _sortAscending ? rows.OrderBy(r => r.Detail ?? "", StringComparer.OrdinalIgnoreCase)
                                          : rows.OrderByDescending(r => r.Detail ?? "", StringComparer.OrdinalIgnoreCase);
                    break;
                case nameof(AcronymRow.Acronym):
                    rows = _sortAscending ? rows.OrderBy(r => r.Acronym ?? "", StringComparer.OrdinalIgnoreCase)
                                          : rows.OrderByDescending(r => r.Acronym ?? "", StringComparer.OrdinalIgnoreCase);
                    break;
                case nameof(AcronymRow.Status):
                    rows = _sortAscending ? rows.OrderBy(r => r.Active) : rows.OrderByDescending(r => r.Active);
                    break;
            }

            _filtered = rows.ToList();
            RebuildPage();
        }

        private void RebuildPage()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} {(_filtered.Count == 1 ? ItemNoun : ItemNounPlural)})";
        }

        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RebuildPage(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)            { _currentPage--;             RebuildPage(); } }
        public void GoToNextPage()  { if (_currentPage < _totalPages)  { _currentPage++;             RebuildPage(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPage(); } }

        /// <summary>Saves a new acronym (blank clears it to NULL) and updates the row on success.</summary>
        public async Task SaveAcronymAsync(AcronymRow row, string acronym)
        {
            string value = (acronym ?? "").Trim();
            if (string.Equals(value, row.Acronym ?? "", StringComparison.Ordinal)) return;

            string sql = IsBranch
                ? "UPDATE dbo.Branch SET Acronym = @Acronym WHERE BranchId = @Id"
                : "UPDATE dbo.Department SET Acronym = @Acronym WHERE DeptId = @Id";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Acronym", value.Length == 0 ? (object)DBNull.Value : value);
                    cmd.Parameters.AddWithValue("@Id", row.Id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            row.Acronym = value;
            StatusMessage = value.Length == 0
                ? $"✓  Cleared the acronym for {row.Name}."
                : $"✓  Saved \"{value}\" for {row.Name}.";
        }
    }
}
