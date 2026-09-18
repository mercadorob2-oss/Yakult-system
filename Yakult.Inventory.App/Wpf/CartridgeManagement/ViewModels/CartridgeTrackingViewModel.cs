using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class CartridgeMovementRow
    {
        public string DateTimeStr  { get; set; }
        public string ItemName     { get; set; }
        public string MovementType { get; set; }
        public string Quantity     { get; set; }
        public string Category     { get; set; }
        public string EmployeeName { get; set; }
        public string BranchName   { get; set; }
        public string Department   { get; set; }
        public string Condition    { get; set; }
        public string PerformedBy  { get; set; }
        public string Remarks      { get; set; }
    }

    public class CartridgeTrackingViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private readonly CartridgeRepository _repository = new CartridgeRepository();

        private List<CartridgeMovementRow> _all      = new List<CartridgeMovementRow>();
        private List<CartridgeMovementRow> _filtered = new List<CartridgeMovementRow>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText = string.Empty;
        private int    _totalRecords;
        private string _pageInfo = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        private LookupItem _selectedItemLookup;
        private LookupItem _selectedEmployeeLookup;
        private string     _selectedMovementType = "All";

        public ObservableCollection<CartridgeMovementRow> PagedRows { get; }
            = new ObservableCollection<CartridgeMovementRow>();

        public ObservableCollection<LookupItem> Items     { get; } = new ObservableCollection<LookupItem>();
        public ObservableCollection<LookupItem> Employees { get; } = new ObservableCollection<LookupItem>();
        public List<string> MovementTypes { get; } = new List<string> { "All", "StockIn", "Issued", "Returned", "RefillIn", "Adjustment" };

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public int    TotalRecords { get => _totalRecords; set => SetField(ref _totalRecords, value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev    { get => _canGoPrev;    set => SetField(ref _canGoPrev,    value); }
        public bool   CanGoNext    { get => _canGoNext;    set => SetField(ref _canGoNext,    value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilterFromTop(); }
        }

        public LookupItem SelectedItemLookup
        {
            get => _selectedItemLookup;
            set { if (SetField(ref _selectedItemLookup, value)) _ = LoadDataAsync(); }
        }

        public LookupItem SelectedEmployeeLookup
        {
            get => _selectedEmployeeLookup;
            set { if (SetField(ref _selectedEmployeeLookup, value)) _ = LoadDataAsync(); }
        }

        public string SelectedMovementType
        {
            get => _selectedMovementType;
            set { if (SetField(ref _selectedMovementType, value)) _ = LoadDataAsync(); }
        }

        public async Task InitAsync()
        {
            await LoadLookupsAsync();
            await LoadDataAsync();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                var rawItems = await Task.Run(() => _repository.GetCartridgeMovementItemsLookup());
                var rawEmps  = await Task.Run(() => _repository.GetCartridgeMovementEmployeeLookup());

                Items.Clear();
                Items.Add(new LookupItem { Id = 0, Name = "All" });
                foreach (var i in rawItems ?? new List<LookupItem>()) Items.Add(i);

                Employees.Clear();
                Employees.Add(new LookupItem { Id = 0, Name = "All" });
                foreach (var e in rawEmps ?? new List<LookupItem>()) Employees.Add(e);

                // Set selections without triggering a reload (field backing only)
                _selectedItemLookup     = Items.Count > 0     ? Items[0]     : null;
                _selectedEmployeeLookup = Employees.Count > 0 ? Employees[0] : null;
                _selectedMovementType   = "All";

                OnPropertyChanged(nameof(SelectedItemLookup));
                OnPropertyChanged(nameof(SelectedEmployeeLookup));
                OnPropertyChanged(nameof(SelectedMovementType));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load filters:\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                int?   itemId     = (_selectedItemLookup?.Id     > 0) ? _selectedItemLookup.Id     : (int?)null;
                int?   employeeId = (_selectedEmployeeLookup?.Id > 0) ? _selectedEmployeeLookup.Id : (int?)null;
                string movement   = (_selectedMovementType == "All" || string.IsNullOrEmpty(_selectedMovementType))
                                        ? null
                                        : _selectedMovementType;

                var table = await Task.Run(() =>
                    _repository.GetCartridgeMovementHistory(itemId, employeeId, movement));

                _all = ConvertTable(table);
                TotalRecords = _all.Count;

                ApplyFilterFromTop();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static List<CartridgeMovementRow> ConvertTable(DataTable table)
        {
            var result = new List<CartridgeMovementRow>();
            if (table == null) return result;

            foreach (DataRow row in table.Rows)
            {
                string GetStr(string col) => table.Columns.Contains(col) ? (row[col] as string ?? string.Empty) : string.Empty;

                string dateStr = string.Empty;
                if (table.Columns.Contains("CreatedAt") && row["CreatedAt"] is DateTime dt)
                    dateStr = dt.ToString("yyyy-MM-dd HH:mm");

                result.Add(new CartridgeMovementRow
                {
                    DateTimeStr  = dateStr,
                    ItemName     = GetStr("ItemName"),
                    MovementType = GetStr("MovementType"),
                    Quantity     = table.Columns.Contains("Quantity") ? row["Quantity"]?.ToString() ?? "" : "",
                    Category     = GetStr("CartridgeCategory"),
                    EmployeeName = GetStr("EmployeeName"),
                    BranchName   = GetStr("BranchName"),
                    Department   = GetStr("DepartmentName"),
                    Condition    = GetStr("ConditionType"),
                    PerformedBy  = GetStr("PerformedBy"),
                    Remarks      = GetStr("Remarks"),
                });
            }

            return result;
        }

        private void ApplyFilterFromTop()
        {
            _currentPage = 1;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q    = (_searchText ?? string.Empty).Trim();
            bool   hasQ = !string.IsNullOrWhiteSpace(q);

            _filtered = hasQ
                ? _all.Where(r =>
                    Contains(r.ItemName,     q) || Contains(r.MovementType, q) ||
                    Contains(r.EmployeeName, q) || Contains(r.BranchName,   q) ||
                    Contains(r.Department,   q) || Contains(r.PerformedBy,  q) ||
                    Contains(r.Remarks,      q)).ToList()
                : new List<CartridgeMovementRow>(_all);

            _totalPages = Math.Max(1, (int)Math.Ceiling((double)_filtered.Count / PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} records)";
            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
        }

        public void GoToPrevPage() { if (_currentPage > 1)          { _currentPage--; RebuildPagedRows(); } }
        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }

        private static bool Contains(string source, string q) =>
            source != null && source.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
