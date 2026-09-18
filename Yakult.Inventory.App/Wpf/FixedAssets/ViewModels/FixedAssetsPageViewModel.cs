using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.FixedAssets.ViewModels
{
    /// <summary>
    /// Business logic for the Fixed Assets browse page, ported verbatim from
    /// Pages\Fixed-Asset\ViewFixedAssetsPage.cs (raw SQL, in-memory reflection-based
    /// search/sort, Filter-By fallback sort). No paging, no permission gating and no
    /// action buttons exist on this page today — none are added here.
    /// </summary>
    public sealed class FixedAssetsPageViewModel : ViewModelBase
    {
        private readonly string _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

        private List<object> _allRecords = new List<object>();
        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public FixedAssetsPageViewModel()
        {
            FilterOptions = new ObservableCollection<string> { "Items", "Requests", "Sets", "Invoices" };
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            FilteredRecords = new ObservableCollection<object>();

            RefreshCommand = new RelayCommand(LoadData);
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _selectedFilter = "Items";
            _selectedFilterBy = "Default";
        }

        public RelayCommand ResetFiltersCommand { get; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _sortColumnKey = null;
            _sortDirection = null;

            ApplyFilters();
        }

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        private string _selectedFilter;
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetField(ref _selectedFilter, value))
                {
                    _sortColumnKey = null;
                    _sortDirection = null;
                    LoadData();
                }
            }
        }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    _sortColumnKey = null;
                    _sortDirection = null;
                    ApplyFilters();
                }
            }
        }

        public ObservableCollection<string> FilterOptions { get; }
        public ObservableCollection<string> FilterByOptions { get; }

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

                _sortColumnKey = value.ColumnKey;
                _sortDirection = value.Direction;
                ApplyFilters();
            }
        }

        public ListSortDirection? CurrentSortDirection => _sortDirection;
        public string CurrentSortColumnKey => _sortColumnKey;

        /// <summary>Called by the View when the grid's own SortByOptions need rebuilding
        /// (columns changed) — populates the list and selects the option matching the
        /// current sort state (or the resolved default), without re-triggering a sort.</summary>
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
                selected = selected ?? options.FirstOrDefault();

                SelectedSortBy = selected;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's DataGrid.Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            _sortDirection = (_sortColumnKey == columnKey && _sortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            _sortColumnKey = columnKey;

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

        // ── Data ──────────────────────────────────────────────────────────────

        public ObservableCollection<object> FilteredRecords { get; private set; }

        private string _totalRecordsText = "Total Records: 0";
        public string TotalRecordsText
        {
            get => _totalRecordsText;
            private set => SetField(ref _totalRecordsText, value);
        }

        public event Action<string> ErrorOccurred;

        public RelayCommand RefreshCommand { get; }

        public void LoadData()
        {
            try
            {
                _allRecords = new List<object>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    switch (SelectedFilter)
                    {
                        case "Items":
                            LoadFixedAssetItems(con);
                            break;
                        case "Requests":
                            LoadFixedAssetRequests(con);
                            break;
                        case "Sets":
                            LoadFixedAssetSets(con);
                            break;
                        case "Invoices":
                            LoadFixedAssetInvoices(con);
                            break;
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        private void LoadFixedAssetItems(SqlConnection con)
        {
            string sql = @"
                SELECT i.ItemId, i.Name, i.Category, i.ItemType, i.ModelNumber, i.SerialNumber, i.DateCreated
                FROM dbo.Item i
                LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId
                WHERE i.IsTrackedAsset = 1
                  AND arch.EntityId IS NULL
                ORDER BY i.DateCreated DESC";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allRecords.Add(new
                    {
                        ItemId = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Category = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ItemType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ModelNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        SerialNumber = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        DateCreated = reader.GetDateTime(6)
                    });
                }
            }
        }

        private void LoadFixedAssetRequests(SqlConnection con)
        {
            string sql = @"
                SELECT r.ReqId, i.Name AS ItemName, i.Category, e.Name AS EmployeeName, r.Status, r.DateRequested
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId
                LEFT JOIN dbo.ArchiveStatus arch_itm ON arch_itm.EntityType = 'Item' AND arch_itm.EntityId = i.ItemId
                LEFT JOIN dbo.ArchiveStatus arch_emp ON arch_emp.EntityType = 'Employee' AND arch_emp.EntityId = e.EmpId
                WHERE i.IsTrackedAsset = 1
                  AND arch_req.EntityId IS NULL
                  AND arch_itm.EntityId IS NULL
                  AND arch_emp.EntityId IS NULL
                ORDER BY r.DateRequested DESC";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allRecords.Add(new
                    {
                        ReqId = reader.GetInt32(0),
                        ItemName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Category = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        EmployeeName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Status = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        DateRequested = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5)
                    });
                }
            }
        }

        private void LoadFixedAssetSets(SqlConnection con)
        {
            string sql = @"
                SELECT DISTINCT s.SetId, s.SetCode, s.SetType, u.Name AS CreatedByName, s.CreatedAt
                FROM dbo.[Set] s
                LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
                INNER JOIN dbo.Request r ON s.SetId = r.SetId
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT JOIN dbo.ArchiveStatus arch_set ON arch_set.EntityType = 'Set' AND arch_set.EntityId = s.SetId
                LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId
                WHERE i.IsTrackedAsset = 1
                  AND arch_set.EntityId IS NULL
                  AND (r.ReqId IS NULL OR arch_req.EntityId IS NULL)
                ORDER BY s.CreatedAt DESC";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allRecords.Add(new
                    {
                        SetId = reader.GetInt32(0),
                        SetCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        SetType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        CreatedByName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4)
                    });
                }
            }
        }

        private void LoadFixedAssetInvoices(SqlConnection con)
        {
            string sql = @"
                SELECT DISTINCT s.SetId, s.SetCode, s.DocumentNumber, s.TotalAmountDue, s.CreatedAt
                FROM dbo.[Set] s
                INNER JOIN dbo.Request r ON s.SetId = r.SetId
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT JOIN dbo.ArchiveStatus arch_set ON arch_set.EntityType = 'Set' AND arch_set.EntityId = s.SetId
                LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId
                WHERE i.IsTrackedAsset = 1
                  AND s.DocumentNumber IS NOT NULL
                  AND arch_set.EntityId IS NULL
                  AND (r.ReqId IS NULL OR arch_req.EntityId IS NULL)
                ORDER BY s.CreatedAt DESC";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allRecords.Add(new
                    {
                        SetId = reader.GetInt32(0),
                        SetCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        DocumentNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        TotalAmountDue = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                        CreatedAt = reader.GetDateTime(4)
                    });
                }
            }
        }

        private void ApplyFilters()
        {
            if (_allRecords == null) return;

            IEnumerable<object> filtered = _allRecords;

            string searchText = SearchText?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(record =>
                {
                    var properties = record.GetType().GetProperties();
                    return properties.Any(prop =>
                    {
                        var value = prop.GetValue(record)?.ToString() ?? "";
                        return value.ToLower().Contains(searchText);
                    });
                });
            }

            var list = filtered.ToList();

            if (_sortColumnKey != null && list.Count > 0)
            {
                var prop = list[0].GetType().GetProperty(_sortColumnKey);
                if (prop != null)
                {
                    list = _sortDirection == ListSortDirection.Ascending
                        ? list.OrderBy(x => prop.GetValue(x, null)).ToList()
                        : list.OrderByDescending(x => prop.GetValue(x, null)).ToList();
                }
            }
            else if (list.Count > 0)
            {
                var firstItem = list[0];
                var dateProperty = firstItem.GetType().GetProperty("DateCreated")
                    ?? firstItem.GetType().GetProperty("CreatedAt")
                    ?? firstItem.GetType().GetProperty("DateRequested");

                if (dateProperty != null)
                {
                    if (SelectedFilterBy == "Most Recently Added" || SelectedFilterBy == "Default")
                        list = list.OrderByDescending(x => dateProperty.GetValue(x, null)).ToList();
                    else if (SelectedFilterBy == "Oldest Added")
                        list = list.OrderBy(x => dateProperty.GetValue(x, null)).ToList();
                }
            }

            FilteredRecords = new ObservableCollection<object>(list);
            OnPropertyChanged(nameof(FilteredRecords));
            TotalRecordsText = $"Total Records: {list.Count}";
        }

        /// <summary>Pre-fills the search box and applies the filter. Called from the home page
        /// global search to land the user on this page with results pre-filtered.</summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchText = query;
        }
    }
}
