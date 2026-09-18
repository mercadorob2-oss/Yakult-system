using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Pages.Inventory;

namespace Yakult.Inventory.App.WPF.Inventory.ViewModels
{
    /// <summary>Tabs 0-3: Category Stock Summary, Hardware, Software/License, Services —
    /// read-only summary grids, ported verbatim from BuildTab1..4_*/Load*Data/ApplyFilters*/
    /// UpdatePagination* in the original ViewInventoryPage.cs.</summary>
    public sealed partial class InventoryPageViewModel
    {
        private readonly SimpleTabState<CategoryStockDto> _categoryStock = new SimpleTabState<CategoryStockDto>();
        private readonly SimpleTabState<HardwareInventoryDto> _hardware = new SimpleTabState<HardwareInventoryDto>();
        private readonly SimpleTabState<SoftwareLicenseInventoryDto> _softwareLicense = new SimpleTabState<SoftwareLicenseInventoryDto>();
        private readonly SimpleTabState<ServicesInventoryDto> _services = new SimpleTabState<ServicesInventoryDto>();

        public ObservableCollection<CategoryStockDto> PagedCategoryStock => _categoryStock.Paged;
        public ObservableCollection<HardwareInventoryDto> PagedHardware => _hardware.Paged;
        public ObservableCollection<SoftwareLicenseInventoryDto> PagedSoftwareLicense => _softwareLicense.Paged;
        public ObservableCollection<ServicesInventoryDto> PagedServices => _services.Paged;

        public event Action<string> ErrorOccurred;

        // ── Tab 0: Category Stock Summary ────────────────────────────────────

        private void LoadCategoryStockData()
        {
            try
            {
                var all = new System.Collections.Generic.List<CategoryStockDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            c.CategoryId,
                            c.Name AS CategoryName,
                            ISNULL(item_agg.TotalStock,    0) AS TotalStock,
                            ISNULL(item_agg.ActiveItems,   0) AS ActiveItems,
                            ISNULL(req_agg.RequestedItems, 0) AS RequestedItems
                        FROM dbo.ItemCategory c
                        LEFT JOIN (
                            SELECT CategoryId,
                                   SUM(StockOnHand)       AS TotalStock,
                                   COUNT(DISTINCT ItemId)  AS ActiveItems
                            FROM dbo.Item
                            WHERE Active = 1
                            GROUP BY CategoryId
                        ) item_agg ON item_agg.CategoryId = c.CategoryId
                        LEFT JOIN (
                            SELECT i.CategoryId, SUM(r.Quantity) AS RequestedItems
                            FROM dbo.Request r
                            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                            WHERE r.Status IN ('Pending', 'Approved')
                            GROUP BY i.CategoryId
                        ) req_agg ON req_agg.CategoryId = c.CategoryId
                        WHERE c.Active = 1
                        ORDER BY c.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            all.Add(new CategoryStockDto
                            {
                                CategoryId = reader.GetInt32(0),
                                CategoryName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                TotalStock = reader.GetInt32(2),
                                ActiveItems = reader.GetInt32(3),
                                RequestedItems = reader.GetInt32(4)
                            });
                        }
                    }
                }

                _categoryStock.All = all;
                _categoryStock.Filtered = all;
                _categoryStock.CurrentPage = 1;
                _categoryStock.Loaded = true;
                _categoryStock.UpdatePage();
                if (ActiveTabIndex == 0) { RaiseActivePagingChanged(); RebuildSortByOptionsRequested?.Invoke(); }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error loading category stock data: {ex.Message}");
            }
        }

        private void ApplyFiltersCategoryStock()
        {
            if (_categoryStock.All == null) return;

            string searchText = SearchText?.Trim().ToLower() ?? "";
            _categoryStock.Filtered = string.IsNullOrWhiteSpace(searchText)
                ? _categoryStock.All
                : _categoryStock.All.Where(c => c.CategoryName != null && c.CategoryName.ToLower().Contains(searchText)).ToList();

            _categoryStock.Sort();
            _categoryStock.CurrentPage = 1;
            _categoryStock.UpdatePage();
        }

        // ── Tab 1: Hardware ───────────────────────────────────────────────────

        private void LoadHardwareData()
        {
            try
            {
                var all = new System.Collections.Generic.List<HardwareInventoryDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            i.ItemId,
                            i.Name AS ItemName,
                            c.Name AS CategoryName,
                            i.StockOnHand AS ActiveStock,
                            ISNULL(req_agg.RequestedItems, 0) AS RequestedItems,
                            CASE WHEN i.ConditionId = 1 THEN 1 ELSE 0 END AS GoodCount,
                            CASE WHEN i.ConditionId = 2 THEN 1 ELSE 0 END AS DamagedCount,
                            CASE WHEN i.IsTrackedAsset = 1 THEN 'Yes' ELSE 'No' END AS FixedAsset
                        FROM dbo.Item i
                        LEFT JOIN dbo.ItemCategory c ON i.CategoryId = c.CategoryId
                        LEFT JOIN (
                            SELECT ItemId, SUM(Quantity) AS RequestedItems
                            FROM dbo.Request
                            WHERE Status IN ('Pending', 'Approved')
                            GROUP BY ItemId
                        ) req_agg ON req_agg.ItemId = i.ItemId
                        WHERE i.ItemType = 'Hardware' AND i.Active = 1
                        ORDER BY i.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            all.Add(new HardwareInventoryDto
                            {
                                ItemId = reader.GetInt32(0),
                                ItemName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                CategoryName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                ActiveStock = reader.GetInt32(3),
                                RequestedItems = reader.GetInt32(4),
                                GoodCount = reader.GetInt32(5),
                                DamagedCount = reader.GetInt32(6),
                                FixedAsset = reader.GetString(7)
                            });
                        }
                    }
                }

                _hardware.All = all;
                _hardware.Filtered = all;
                _hardware.CurrentPage = 1;
                _hardware.Loaded = true;
                _hardware.UpdatePage();
                if (ActiveTabIndex == 1) { RaiseActivePagingChanged(); RebuildSortByOptionsRequested?.Invoke(); }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error loading hardware data: {ex.Message}");
            }
        }

        private void ApplyFiltersHardware()
        {
            if (_hardware.All == null) return;

            string searchText = SearchText?.Trim().ToLower() ?? "";
            _hardware.Filtered = string.IsNullOrWhiteSpace(searchText)
                ? _hardware.All
                : _hardware.All.Where(h =>
                    (h.ItemName != null && h.ItemName.ToLower().Contains(searchText)) ||
                    (h.CategoryName != null && h.CategoryName.ToLower().Contains(searchText))).ToList();

            _hardware.Sort();
            _hardware.CurrentPage = 1;
            _hardware.UpdatePage();
        }

        // ── Tab 2: Software/License ──────────────────────────────────────────

        private void LoadSoftwareLicenseData()
        {
            try
            {
                var all = new System.Collections.Generic.List<SoftwareLicenseInventoryDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            i.ItemId,
                            i.Name AS ItemName,
                            c.Name AS CategoryName,
                            i.StockOnHand AS ActiveStock,
                            ISNULL(req_agg.RequestedItems, 0) AS RequestedItems,
                            ISNULL(i.WarrantyYears, 0) AS WarrantyYears,
                            COALESCE(i.WarrantyStartDate, lr.NewStartDate) AS WarrantyStartDate,
                            COALESCE(i.WarrantyEndDate,   lr.NewEndDate)   AS WarrantyEndDate,
                            ISNULL(ren_cnt.RenewalCount,  0) AS RenewalCount
                        FROM dbo.Item i
                        LEFT JOIN dbo.ItemCategory c ON i.CategoryId = c.CategoryId
                        LEFT JOIN (
                            SELECT ItemId, SUM(Quantity) AS RequestedItems
                            FROM dbo.Request
                            WHERE Status IN ('Pending', 'Approved')
                            GROUP BY ItemId
                        ) req_agg ON req_agg.ItemId = i.ItemId
                        OUTER APPLY (
                            SELECT TOP 1 ren.NewStartDate, ren.NewEndDate
                            FROM dbo.Renewals ren
                            WHERE ren.ItemId = i.ItemId AND ren.IsArchived = 0
                            ORDER BY ren.CreatedAt DESC
                        ) lr
                        LEFT JOIN (
                            SELECT ItemId, COUNT(*) AS RenewalCount
                            FROM dbo.Renewals
                            GROUP BY ItemId
                        ) ren_cnt ON ren_cnt.ItemId = i.ItemId
                        WHERE i.ItemType IN ('Software', 'License', 'Software/License') AND i.Active = 1
                        ORDER BY i.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            all.Add(new SoftwareLicenseInventoryDto
                            {
                                ItemId = reader.GetInt32(0),
                                ItemName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                CategoryName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                ActiveStock = reader.GetInt32(3),
                                RequestedItems = reader.GetInt32(4),
                                WarrantyYears = reader.GetInt32(5),
                                WarrantyStartDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                WarrantyEndDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                RenewalCount = reader.GetInt32(8)
                            });
                        }
                    }
                }

                _softwareLicense.All = all;
                _softwareLicense.Filtered = all;
                _softwareLicense.CurrentPage = 1;
                _softwareLicense.Loaded = true;
                _softwareLicense.UpdatePage();
                if (ActiveTabIndex == 2) { RaiseActivePagingChanged(); RebuildSortByOptionsRequested?.Invoke(); }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error loading software/license data: {ex.Message}");
            }
        }

        private void ApplyFiltersSoftwareLicense()
        {
            if (_softwareLicense.All == null) return;

            string searchText = SearchText?.Trim().ToLower() ?? "";
            _softwareLicense.Filtered = string.IsNullOrWhiteSpace(searchText)
                ? _softwareLicense.All
                : _softwareLicense.All.Where(s =>
                    (s.ItemName != null && s.ItemName.ToLower().Contains(searchText)) ||
                    (s.CategoryName != null && s.CategoryName.ToLower().Contains(searchText))).ToList();

            _softwareLicense.Sort();
            _softwareLicense.CurrentPage = 1;
            _softwareLicense.UpdatePage();
        }

        // ── Tab 3: Services ───────────────────────────────────────────────────

        private void LoadServicesData()
        {
            try
            {
                var all = new System.Collections.Generic.List<ServicesInventoryDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            i.ItemId,
                            i.Name AS ItemName,
                            c.Name AS CategoryName,
                            i.StockOnHand AS ActiveStock,
                            ISNULL(req_agg.RequestedItems, 0) AS RequestedItems,
                            ISNULL(i.WarrantyYears, 0) AS WarrantyYears,
                            COALESCE(i.WarrantyStartDate, lr.NewStartDate) AS WarrantyStartDate,
                            COALESCE(i.WarrantyEndDate,   lr.NewEndDate)   AS WarrantyEndDate,
                            ISNULL(ren_cnt.RenewalCount,  0) AS RenewalCount
                        FROM dbo.Item i
                        LEFT JOIN dbo.ItemCategory c ON i.CategoryId = c.CategoryId
                        LEFT JOIN (
                            SELECT ItemId, SUM(Quantity) AS RequestedItems
                            FROM dbo.Request
                            WHERE Status IN ('Pending', 'Approved')
                            GROUP BY ItemId
                        ) req_agg ON req_agg.ItemId = i.ItemId
                        OUTER APPLY (
                            SELECT TOP 1 ren.NewStartDate, ren.NewEndDate
                            FROM dbo.Renewals ren
                            WHERE ren.ItemId = i.ItemId AND ren.IsArchived = 0
                            ORDER BY ren.CreatedAt DESC
                        ) lr
                        LEFT JOIN (
                            SELECT ItemId, COUNT(*) AS RenewalCount
                            FROM dbo.Renewals
                            GROUP BY ItemId
                        ) ren_cnt ON ren_cnt.ItemId = i.ItemId
                        WHERE i.ItemType IN ('Services', 'Service') AND i.Active = 1
                        ORDER BY i.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            all.Add(new ServicesInventoryDto
                            {
                                ItemId = reader.GetInt32(0),
                                ItemName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                CategoryName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                ActiveStock = reader.GetInt32(3),
                                RequestedItems = reader.GetInt32(4),
                                WarrantyYears = reader.GetInt32(5),
                                WarrantyStartDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                WarrantyEndDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                RenewalCount = reader.GetInt32(8)
                            });
                        }
                    }
                }

                _services.All = all;
                _services.Filtered = all;
                _services.CurrentPage = 1;
                _services.Loaded = true;
                _services.UpdatePage();
                if (ActiveTabIndex == 3) { RaiseActivePagingChanged(); RebuildSortByOptionsRequested?.Invoke(); }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error loading services data: {ex.Message}");
            }
        }

        private void ApplyFiltersServices()
        {
            if (_services.All == null) return;

            string searchText = SearchText?.Trim().ToLower() ?? "";
            _services.Filtered = string.IsNullOrWhiteSpace(searchText)
                ? _services.All
                : _services.All.Where(s =>
                    (s.ItemName != null && s.ItemName.ToLower().Contains(searchText)) ||
                    (s.CategoryName != null && s.CategoryName.ToLower().Contains(searchText))).ToList();

            _services.Sort();
            _services.CurrentPage = 1;
            _services.UpdatePage();
        }
    }
}
