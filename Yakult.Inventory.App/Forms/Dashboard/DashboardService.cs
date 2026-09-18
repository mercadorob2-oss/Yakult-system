﻿﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;


namespace Yakult.Inventory.App.Forms.Dashboard
{
    /// <summary>
    /// Service for fetching and aggregating dashboard data across all scopes
    /// Handles all SQL queries and data transformations
    /// </summary>
    public class DashboardService
    {
        private readonly string _connectionString;
        private bool? _hasArchiveFunction;

        public DashboardService()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
        }

        private bool HasArchiveFunction(SqlConnection con)
        {
            if (_hasArchiveFunction.HasValue)
                return _hasArchiveFunction.Value;

            using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.fn_IsEntityArchived') IS NULL THEN 0 ELSE 1 END", con))
            {
                _hasArchiveFunction = Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                return _hasArchiveFunction.Value;
            }
        }

        private string ArchiveFilter(SqlConnection con, string entityType, string entityIdSql)
        {
            return HasArchiveFunction(con)
                ? $"dbo.fn_IsEntityArchived('{entityType}', {entityIdSql}) = 0"
                : "1=1";
        }

        private static string NormalizeRequestStatus(string status)
        {
            var value = (status ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            if (value.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Under Review", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("UnderReview", StringComparison.OrdinalIgnoreCase))
            {
                return "Under Review";
            }

            if (value.Equals("On Hold", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("OnHold", StringComparison.OrdinalIgnoreCase))
            {
                return "On Hold";
            }

            return value;
        }

        private static string NormalizeChartLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized;
        }

        /// <summary>
        /// Get complete dashboard data for any scope (main entry point)
        /// </summary>
        public DashboardData GetDashboardData(DashboardScope scope, DashboardFilter filter = null)
        {
            switch (scope)
            {
                case DashboardScope.Categories:
                    return GetCategoriesDashboardData(filter);
                case DashboardScope.Items:
                    return GetItemsDashboardData(filter);
                case DashboardScope.InventoryMovements:
                    return GetInventoryMovementsDashboardData(filter);
                case DashboardScope.Requests:
                    return GetRequestsDashboardData(filter);
                case DashboardScope.Renewals:
                    return GetRenewalsDashboardData(filter);
                case DashboardScope.InvoicesServices:
                    return GetInvoicesServicesDashboardData(filter);
                case DashboardScope.Employees:
                    return GetEmployeesDashboardData(filter);
                case DashboardScope.Vendors:
                    return GetVendorsDashboardData(filter);
                case DashboardScope.ArchivedEntities:
                    return GetArchivedEntitiesDashboardData(filter);
                default:
                    throw new ArgumentException($"Unknown dashboard scope: {scope}");
            }
        }

        // ===== SCOPE: Categories =====

        /// <summary>
        /// Calls usp_RefreshDashboardCategoryCache to pre-aggregate category data.
        /// Run this on a background thread before loading the dashboard.
        /// </summary>
        public void RefreshCategoryCache()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("usp_RefreshDashboardCategoryCache", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 120;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Cache refresh failed: {ex.Message}");
            }
        }

        private List<(int Id, string Name, int Stock, int Good, int Damaged, int Total, int Serialized)>
            ReadCategoryRows(SqlConnection con)
        {
            var rows = new List<(int, string, int, int, int, int, int)>();

            // Use pre-aggregated cache table if populated; fall back to the view otherwise.
            const string sqlCache = @"
                SELECT CategoryId, CategoryName, ActiveStock,
                       GoodCount, DamagedCount, TotalItems_All, SerializedItems
                FROM   dbo.DashboardCategoryCache
                WHERE  TotalItems_All > 0
                ORDER  BY CategoryName";

            const string sqlView = @"
                SELECT CategoryId, CategoryName,
                       ISNULL(ActiveStock,    0),
                       ISNULL(GoodCount,      0),
                       ISNULL(DamagedCount,   0),
                       ISNULL(TotalItems_All, 0),
                       ISNULL(SerializedItems,0)
                FROM   dbo.vw_CategoryCombinedSummary
                WHERE  TotalItems_All > 0
                ORDER  BY CategoryName";

            bool cacheExists = false;
            try
            {
                using (var chk = new SqlCommand(
                    "SELECT OBJECT_ID('dbo.DashboardCategoryCache')", con))
                    cacheExists = chk.ExecuteScalar() != DBNull.Value
                                  && chk.ExecuteScalar() != null;
            }
            catch { /* table may not exist yet */ }

            string sql = cacheExists ? sqlCache : sqlView;

            // If using the cache table, check it has rows (might be empty on first run)
            if (cacheExists)
            {
                try
                {
                    using (var cnt = new SqlCommand(
                        "SELECT COUNT(1) FROM dbo.DashboardCategoryCache", con))
                    {
                        int count = Convert.ToInt32(cnt.ExecuteScalar());
                        if (count == 0) sql = sqlView;   // cache not yet populated
                    }
                }
                catch { sql = sqlView; }
            }

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add((
                        reader.IsDBNull(0) ? 0   : reader.GetInt32(0),
                        reader.IsDBNull(1) ? ""  : reader.GetString(1),
                        reader.IsDBNull(2) ? 0   : reader.GetInt32(2),
                        reader.IsDBNull(3) ? 0   : reader.GetInt32(3),
                        reader.IsDBNull(4) ? 0   : reader.GetInt32(4),
                        reader.IsDBNull(5) ? 0   : reader.GetInt32(5),
                        reader.IsDBNull(6) ? 0   : reader.GetInt32(6)
                    ));
                }
            }
            return rows;
        }

        private DashboardData GetCategoriesDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.Categories,
                Title = "\U0001F4C1 Categories Dashboard",
                PrimaryChartTitle = "CATEGORY DISTRIBUTION (Total Items)",
                SecondaryChartTitle = "STOCK LEVELS BY CATEGORY (Active Stock)",
                TertiaryChartTitle = "CONDITION BREAKDOWN",
                TableTitle = "\U0001F4CB TOP CATEGORIES BY STOCK"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    var raw = ReadCategoryRows(con);
                    // Map to named tuple for readability
                    var summaries = raw.Select(r => (
                        Id:      r.Id,
                        Name:    r.Name,
                        Stock:   r.Stock,
                        Good:    r.Good,
                        Damaged: r.Damaged,
                        Total:   r.Total
                    )).ToList();

                    // KPIs
                    int totalItems   = summaries.Sum(s => s.Total);
                    int totalStock   = summaries.Sum(s => s.Stock);
                    int totalGood    = summaries.Sum(s => s.Good);
                    int totalDamaged = summaries.Sum(s => s.Damaged);

                    data.Kpis.Add(new DashboardKpi { Title = "Total Items",     Value = totalItems,          AccentColor = Color.FromArgb(52,  152, 219), Icon = "\U0001F4E6" });
                    data.Kpis.Add(new DashboardKpi { Title = "Active Stock",    Value = totalStock,          AccentColor = Color.FromArgb(46,  204, 113), Icon = "\u2705"     });
                    data.Kpis.Add(new DashboardKpi { Title = "Categories",      Value = summaries.Count,     AccentColor = Color.FromArgb(155,  89, 182), Icon = "\U0001F4C1" });
                    data.Kpis.Add(new DashboardKpi { Title = "Good Condition",  Value = totalGood,           AccentColor = Color.FromArgb(26,  188, 156), Icon = "\U0001F44D" });
                    data.Kpis.Add(new DashboardKpi { Title = "Damaged Items",   Value = totalDamaged,        AccentColor = Color.FromArgb(231,  76,  60), Icon = "\u26A0\uFE0F" });

                    // Charts
                    data.PrimaryChartMetrics   = summaries.OrderByDescending(s => s.Total).Take(6)
                        .Select(s => new DashboardMetric { Label = s.Name, Value = s.Total }).ToList();
                    data.SecondaryChartMetrics = summaries.OrderByDescending(s => s.Stock).Take(8)
                        .Select(s => new DashboardMetric { Label = s.Name, Value = s.Stock }).ToList();
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Good",    Value = totalGood    });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Damaged", Value = totalDamaged });

                    // Quick stats
                    int lowStock  = summaries.Count(s => s.Stock > 0 && s.Stock < 5);
                    int outOfStock = summaries.Count(s => s.Stock <= 0);
                    int inStock    = summaries.Count - lowStock - outOfStock;

                    data.Stat1 = new DashboardQuickStat { Label = "Out of Stock", Value = outOfStock.ToString(), Color = Color.FromArgb(231, 76,  60)  };
                    data.Stat2 = new DashboardQuickStat { Label = "Low Stock",    Value = lowStock.ToString(),   Color = Color.FromArgb(241, 196, 15)  };
                    data.Stat3 = new DashboardQuickStat { Label = "In Stock",     Value = inStock.ToString(),    Color = Color.FromArgb(46,  204, 113) };

                    // Table data
                    foreach (var s in summaries.OrderByDescending(x => x.Stock))
                    {
                        string status     = s.Stock <= 0 ? "\U0001F534 Out of Stock" : s.Stock < 5 ? "\U0001F7E1 Low Stock" : "\U0001F7E2 In Stock";
                        var    statusColor = s.Stock <= 0 ? Color.FromArgb(231, 76, 60) : s.Stock < 5 ? Color.FromArgb(241, 196, 15) : Color.FromArgb(46, 204, 113);

                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1        = s.Name,
                            Col2        = s.Stock.ToString(),
                            Col3        = s.Good.ToString(),
                            Col4        = s.Damaged.ToString(),
                            Col5        = s.Total.ToString(),
                            StatusBadge = status,
                            StatusColor = statusColor
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Categories dashboard error: {ex}");
            }

            return data;
        }

        public object GetDrillDownItems(DrillDownRequest request)
        {
            if (request == null)
                return new List<object>();

            string filterKey = request.FilterKey;
            System.Diagnostics.Debug.WriteLine($"[Service.GetDrillDownItems] Context={request.Context}, FilterKey={filterKey}");

            try
            {
                switch (request.Context)
                {
                    case DrillDownContext.Categories:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Category summaries...");

                        var summaries = new List<CategorySummaryDto>();
                        using (var con = new SqlConnection(_connectionString))
                        {
                            con.Open();
                            foreach (var r in ReadCategoryRows(con))
                            {
                                summaries.Add(new CategorySummaryDto
                                {
                                    CategoryId      = r.Id,
                                    CategoryName    = r.Name,
                                    ActiveStock     = r.Stock,
                                    GoodCount       = r.Good,
                                    DamagedCount    = r.Damaged,
                                    TotalItems      = r.Total,
                                    SerializedItems = r.Serialized,
                                    RequestedItems  = 0
                                });
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"[Service] Category summaries fetched: {summaries.Count} items");

                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            var key = filterKey.Trim();

                            if (key.Equals("Active Stock", StringComparison.OrdinalIgnoreCase))
                            {
                                summaries = summaries.Where(s => s.ActiveStock > 0).ToList();
                            }
                            else if (key.Equals("Out of Stock", StringComparison.OrdinalIgnoreCase))
                            {
                                summaries = summaries.Where(s => s.ActiveStock <= 0).ToList();
                            }
                            else if (key.Equals("Low Stock", StringComparison.OrdinalIgnoreCase))
                            {
                                summaries = summaries.Where(s => s.ActiveStock > 0 && s.ActiveStock < 5).ToList();
                            }
                            else if (key.Equals("In Stock", StringComparison.OrdinalIgnoreCase))
                            {
                                summaries = summaries.Where(s => s.ActiveStock >= 5).ToList();
                            }
                            else if (key.Equals("Good Condition", StringComparison.OrdinalIgnoreCase))
                            {
                                summaries = summaries.Where(s => s.GoodCount > 0).ToList();
                            }
                            else if (key.Equals("Damaged Items", StringComparison.OrdinalIgnoreCase))
                            {
                                summaries = summaries.Where(s => s.DamagedCount > 0).ToList();
                            }
                            else if (key.Equals("Total Items", StringComparison.OrdinalIgnoreCase) ||
                                     key.Equals("Categories", StringComparison.OrdinalIgnoreCase))
                            {
                                // Show all
                            }
                            else
                            {
                                // Treat as CategoryName (e.g., chart segment click)
                                summaries = summaries.Where(s => string.Equals(s.CategoryName, key, StringComparison.OrdinalIgnoreCase)).ToList();
                            }
                        }

                        return summaries;
                    }

                    case DrillDownContext.Items:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Items...");
                        var items = new List<ItemDto>();
                        using (var con = new SqlConnection(_connectionString))
                        {
                            con.Open();
                            System.Diagnostics.Debug.WriteLine($"[Service] SQL connection opened");
                            var whereClauses = new List<string> { "1 = 1", "arch.EntityId IS NULL" };
                            bool needsCategoryParam = false;

                            if (!string.IsNullOrWhiteSpace(filterKey))
                            {
                                var key = filterKey.Trim();

                                if (key.StartsWith("Item:", StringComparison.OrdinalIgnoreCase))
                                {
                                    var label = key.Substring("Item:".Length).Trim();
                                    string categoryNameFromLabel = string.Empty;
                                    string itemNameFromLabel = label;

                                    // Bar chart label format is "{Category} - {ItemName}"
                                    int sepIndex = label.IndexOf(" - ", StringComparison.OrdinalIgnoreCase);
                                    if (sepIndex > 0 && sepIndex + 3 < label.Length)
                                    {
                                        categoryNameFromLabel = label.Substring(0, sepIndex);
                                        itemNameFromLabel = label.Substring(sepIndex + 3);
                                    }

                                    categoryNameFromLabel = NormalizeChartLabel(categoryNameFromLabel);
                                    itemNameFromLabel = NormalizeChartLabel(itemNameFromLabel);
                                    label = NormalizeChartLabel(label);

                                    var summaries = new List<ItemStockSummaryDto>();

                                    using (var cmd = new SqlCommand(@"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    COALESCE(ic.Name, i.Category) AS CategoryName,
    ISNULL(i.StockOnHand, 0) AS ActiveStock,
    ISNULL((
        SELECT SUM(r.Quantity)
        FROM dbo.Request r
        WHERE r.ItemId = i.ItemId AND r.Status IN ('Pending', 'Approved', 'Under Review')
    ), 0) AS RequestedItems,
    CASE WHEN i.ConditionId = 1 THEN 1 ELSE 0 END AS GoodCount,
    CASE WHEN i.ConditionId = 2 THEN 1 ELSE 0 END AS DamagedCount,
    CASE WHEN i.IsTrackedAsset = 1 THEN 'Yes' ELSE 'No' END AS FixedAsset
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE arch.EntityId IS NULL
  AND (
        -- Preferred: exact category + item name match (chart label is Category - ItemName)
        (
            @CategoryNameTrim <> ''
            AND LTRIM(RTRIM(COALESCE(ic.Name, i.Category))) = @CategoryNameTrim
            AND LTRIM(RTRIM(i.Name)) = @ItemNameTrim
        )
        OR
        -- Fallback: match by item name only
        (
            LTRIM(RTRIM(i.Name)) = @ItemNameTrim
            OR LTRIM(RTRIM(i.Name)) LIKE @ItemNameLike
        )
        OR
        -- Last resort: match the full label (in case Item.Name actually stores the prefix)
        (
            LTRIM(RTRIM(i.Name)) = @FullLabelTrim
            OR LTRIM(RTRIM(i.Name)) LIKE @FullLabelLike
        )
      )
ORDER BY i.Name ASC, i.ItemId DESC;", con))
                                    {
                                        cmd.Parameters.AddWithValue("@CategoryNameTrim", categoryNameFromLabel ?? string.Empty);
                                        cmd.Parameters.AddWithValue("@ItemNameTrim", itemNameFromLabel ?? string.Empty);
                                        cmd.Parameters.AddWithValue("@ItemNameLike", $"%{itemNameFromLabel}%");
                                        cmd.Parameters.AddWithValue("@FullLabelTrim", label ?? string.Empty);
                                        cmd.Parameters.AddWithValue("@FullLabelLike", $"%{label}%");

                                        using (var reader = cmd.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                summaries.Add(new ItemStockSummaryDto
                                                {
                                                    ItemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                                    ItemName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                                    CategoryName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                                    ActiveStock = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                                    RequestedItems = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                                    GoodCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                                    DamagedCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                                    FixedAsset = reader.IsDBNull(7) ? null : reader.GetString(7)
                                                });
                                            }
                                        }
                                    }

                                    return summaries;
                                }

                                if (key.Equals("Categories", StringComparison.OrdinalIgnoreCase))
                                {
                                    var categories = new List<ItemCategoryBreakdownDto>();
                                    using (var cmd = new SqlCommand(@"
SELECT
    COALESCE(ic.Name, i.Category) AS CategoryName,
    COUNT(1) AS TotalItems,
    SUM(CASE WHEN i.Active = 1 THEN 1 ELSE 0 END) AS ActiveItems,
    SUM(CASE WHEN i.Active = 0 THEN 1 ELSE 0 END) AS InactiveItems,
    SUM(ISNULL(i.StockOnHand, 0)) AS TotalStock
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE arch.EntityId IS NULL
GROUP BY COALESCE(ic.Name, i.Category)
HAVING COALESCE(ic.Name, i.Category) IS NOT NULL AND LTRIM(RTRIM(COALESCE(ic.Name, i.Category))) <> ''
ORDER BY TotalItems DESC, CategoryName ASC;", con))
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            categories.Add(new ItemCategoryBreakdownDto
                                            {
                                                CategoryName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                                                TotalItems = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                                ActiveItems = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                                InactiveItems = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                                TotalStock = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                                            });
                                        }
                                    }

                                    return categories;
                                }

                                if (key.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                                    key.Equals("Active Items", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("i.Active = 1");
                                }
                                else if (key.Equals("Inactive", StringComparison.OrdinalIgnoreCase) ||
                                         key.Equals("Inactive Items", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("i.Active = 0");
                                }
                                else if (key.Equals("Hardware", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("i.ItemType = 'Hardware'");
                                }
                                else if (key.Equals("Software", StringComparison.OrdinalIgnoreCase) ||
                                         key.Equals("Software/License", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("(i.ItemType = 'Software' OR i.ItemType = 'Software/License')");
                                }
                                else if (key.Equals("Services", StringComparison.OrdinalIgnoreCase) ||
                                         key.Equals("Service", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("(i.ItemType = 'Services' OR i.ItemType = 'Service')");
                                }
                                else if (key.Equals("Out of Stock", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("i.StockOnHand <= 0");
                                }
                                else if (key.Equals("Low Stock", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("i.StockOnHand > 0 AND i.StockOnHand < 5");
                                }
                                else if (key.Equals("In Stock", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("i.StockOnHand >= 5");
                                }
                                else if (key.Equals("Good Condition", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("(c.ConditionName = 'Good' OR i.ConditionId = 1)");
                                }
                                else if (key.Equals("Damaged Items", StringComparison.OrdinalIgnoreCase))
                                {
                                    whereClauses.Add("(c.ConditionName = 'Damaged' OR i.ConditionId = 2)");
                                }
                                else
                                {
                                    // Treat as Category (chart segment click) / free-form search
                                    whereClauses.Add("COALESCE(ic.Name, i.Category) LIKE @Category");
                                    needsCategoryParam = true;
                                }
                            }

                            string sqlQuery = $@"
SELECT i.ItemId, i.Name, i.Description, COALESCE(ic.Name, i.Category) AS Category, i.ItemType, i.SerialNumber, i.ModelNumber,
       i.Active AS DbActive, i.UnitOfMeasure, i.StockOnHand, i.DateCreated, u1.Name AS CreatedByName,
       i.DateModified, u2.Name AS ModifiedByName,
       CASE WHEN i.Active = 1 AND NOT EXISTS (SELECT 1 FROM dbo.Request r WHERE r.ItemId = i.ItemId)
            THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS EffectiveActive,
       i.Amount, c.ConditionName, i.ConditionId, i.Remarks, i.WarrantyYears,
       i.WarrantyStartDate, i.WarrantyEndDate, i.DatePurchased,
       CASE WHEN i.ConditionId = 1 THEN 'Good' WHEN i.ConditionId = 2 THEN 'Damaged' ELSE NULL END AS LatestStatus,
       su.Remark AS LatestRemark, ISNULL(rh.RepairCount, 0) AS RepairCount,
       lr.RepairAction AS LastRepairAction, i.VendorId, v.VendorName, i.IsTrackedAsset, i.AcquisitionType
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.[User] u1 ON i.CreatedBy = u1.UserId
LEFT JOIN dbo.[User] u2 ON i.ModifiedBy = u2.UserId
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (SELECT SerialNumber, MAX(CreatedAt) AS LatestCreatedAt FROM dbo.SetItemUpdate WHERE Processed = 1 GROUP BY SerialNumber) lu ON lu.SerialNumber = i.SerialNumber
LEFT JOIN dbo.SetItemUpdate su ON su.SerialNumber = i.SerialNumber AND su.CreatedAt = lu.LatestCreatedAt AND su.Processed = 1
LEFT JOIN (SELECT SerialNumber, COUNT(*) AS RepairCount, MAX(CreatedAt) AS LastRepairAt FROM dbo.ItemRepairHistory GROUP BY SerialNumber) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN dbo.ItemRepairHistory lr ON lr.SerialNumber = i.SerialNumber AND lr.CreatedAt = rh.LastRepairAt
WHERE {string.Join(" AND ", whereClauses)}
 ORDER BY i.DateCreated DESC, i.ItemId DESC";

                            using (var cmd = new SqlCommand(sqlQuery, con))
                            {
                                cmd.CommandTimeout = 30; // 30 second timeout
                                if (needsCategoryParam)
                                    cmd.Parameters.AddWithValue("@Category", $"%{filterKey?.Trim()}%");

                                System.Diagnostics.Debug.WriteLine($"[Service] Executing Items SQL query...");
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        items.Add(new ItemDto
                                        {
                                            ItemId = reader.GetInt32(0),
                                            Name = reader.GetString(1),
                                            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                            Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                                            ItemType = reader.IsDBNull(4) ? null : reader.GetString(4),
                                            SerialNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                            ModelNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                            DbActive = reader.GetBoolean(7),
                                            Active = reader.GetBoolean(14),
                                            UnitOfMeasure = reader.IsDBNull(8) ? null : reader.GetString(8),
                                            StockOnHand = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                            DateCreated = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                            CreatedByName = reader.IsDBNull(11) ? "N/A" : reader.GetString(11),
                                            DateModified = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12),
                                            ModifiedByName = reader.IsDBNull(13) ? null : reader.GetString(13),
                                            Amount = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15),
                                            ConditionName = reader.IsDBNull(16) ? null : reader.GetString(16),
                                            ConditionId = reader.IsDBNull(17) ? 1 : reader.GetInt32(17),
                                            Remarks = reader.IsDBNull(18) ? null : reader.GetString(18),
                                            WarrantyYears = reader.GetInt32(19),
                                            WarrantyStartDate = reader.IsDBNull(20) ? (DateTime?)null : reader.GetDateTime(20),
                                            WarrantyEndDate = reader.IsDBNull(21) ? (DateTime?)null : reader.GetDateTime(21),
                                            DatePurchased = reader.IsDBNull(22) ? (DateTime?)null : reader.GetDateTime(22),
                                            LatestStatus = reader.IsDBNull(23) ? null : reader.GetString(23),
                                            LatestRemark = reader.IsDBNull(24) ? null : reader.GetString(24),
                                            RepairCount = reader.IsDBNull(25) ? 0 : reader.GetInt32(25),
                                            LastRepairAction = reader.IsDBNull(26) ? null : reader.GetString(26),
                                            VendorId = reader.IsDBNull(27) ? (int?)null : reader.GetInt32(27),
                                            VendorName = reader.IsDBNull(28) ? null : reader.GetString(28),
                                            IsTrackedAsset = reader.IsDBNull(29) ? false : reader.GetBoolean(29),
                                            AcquisitionType = reader.IsDBNull(30) ? null : reader.GetString(30)
                                        });
                                    }
                                }
                                System.Diagnostics.Debug.WriteLine($"[Service] Items loaded: {items.Count} items");
                            }
                        }
                        return items;
                    }

                    case DrillDownContext.Movements:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Movements...");

                        // IMPORTANT: The Inventory Movements dashboard is sourced from dbo.Inventory and explicitly excludes Fixed Assets.
                        // Use the same data source here so drill-down matches what the dashboard displays.
                        using (var con = new SqlConnection(_connectionString))
                        {
                            con.Open();

                            var key = (filterKey ?? string.Empty).Trim();

                            if (string.IsNullOrWhiteSpace(key) ||
                                key.Equals("Total Movements", StringComparison.OrdinalIgnoreCase))
                            {
                                var all = GetAllMovements(con);
                                System.Diagnostics.Debug.WriteLine($"[Service] Movements fetched: {all.Count} items (All)");
                                return all;
                            }

                            if (key.Equals("Positive", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Qty In", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("IN", StringComparison.OrdinalIgnoreCase))
                            {
                                var positive = GetMovementsByEntryType(con, "Positive");
                                System.Diagnostics.Debug.WriteLine($"[Service] Movements fetched: {positive.Count} items (Positive)");
                                return positive;
                            }

                            if (key.Equals("Negative", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Qty Out", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("OUT", StringComparison.OrdinalIgnoreCase))
                            {
                                var negative = GetMovementsByEntryType(con, "Negative");
                                System.Diagnostics.Debug.WriteLine($"[Service] Movements fetched: {negative.Count} items (Negative)");
                                return negative;
                            }

                            // Otherwise treat as CategoryName (e.g., bar chart click: "TOP CATEGORY BY MOVEMENT").
                            var byCategory = GetMovementsByCategory(con, key);
                            System.Diagnostics.Debug.WriteLine($"[Service] Movements fetched: {byCategory.Count} items (Category={key})");
                            return byCategory;
                        }
                    }

                    case DrillDownContext.Requests:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Requests...");
                        var repo = new RequestRepository();
                        var requests = repo.GetAllRequests();
                        System.Diagnostics.Debug.WriteLine($"[Service] Requests fetched: {requests.Count} items");

                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            var key = filterKey.Trim();
                            bool isStatusKey =
                                key.Equals("Submitted", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Under Review", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("UnderReview", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("On Hold", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("OnHold", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("Approved", StringComparison.OrdinalIgnoreCase);

                            if (isStatusKey)
                            {
                                var normalizedFilter = NormalizeRequestStatus(key);
                                requests = requests
                                    .Where(r => string.Equals(NormalizeRequestStatus(r.Status), normalizedFilter, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }
                            else
                            {
                                // Chart clicks may be "item name" (top requested items) or "category name" (category chart).
                                var needle = key;
                                if (needle.StartsWith("Item:", StringComparison.OrdinalIgnoreCase))
                                    needle = needle.Substring("Item:".Length).Trim();

                                requests = requests.Where(r =>
                                        (!string.IsNullOrWhiteSpace(r.ItemName) &&
                                         r.ItemName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                        (!string.IsNullOrWhiteSpace(r.Category) &&
                                         r.Category.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                                    .ToList();
                            }
                        }

                        return requests;
                    }

                    case DrillDownContext.Renewals:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Renewals...");
                        var repo = new RenewalRepository();
                        var renewals = repo.GetAllRenewals() ?? new List<Yakult.Inventory.App.Models.RenewalDto>();
                        System.Diagnostics.Debug.WriteLine($"[Service] Renewals fetched: {renewals.Count} items");

                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            var key = filterKey.Trim();

                            if (key.StartsWith("Item:", StringComparison.OrdinalIgnoreCase))
                            {
                                // Chart click: Expiring Soon chart labels are item names.
                                // Renewals drill-down should show underlying tracked assets (items) related to the clicked label.
                                var itemName = key.Substring("Item:".Length).Trim();
                                var items = new List<RenewalItemDrillDownDto>();

                                using (var con = new SqlConnection(_connectionString))
                                using (var cmd = new SqlCommand(@"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    COALESCE(ic.Name, i.Category) AS Category,
    i.ItemType,
    i.SerialNumber,
    i.ModelNumber,
    i.EndDate,
    CASE WHEN i.EndDate IS NULL THEN NULL ELSE DATEDIFF(DAY, GETDATE(), i.EndDate) END AS DaysUntilExpiry,
    v.VendorName,
    c.ConditionName
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE arch.EntityId IS NULL
  AND i.IsTrackedAsset = 1
  AND (
        LTRIM(RTRIM(i.Name)) = @ItemNameExact
        OR i.Name LIKE @ItemNameLike
      )
ORDER BY i.EndDate ASC, i.Name ASC, i.ItemId DESC;", con))
                                {
                                    cmd.Parameters.AddWithValue("@ItemNameExact", itemName);
                                    cmd.Parameters.AddWithValue("@ItemNameLike", $"%{itemName}%");
                                    con.Open();
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            items.Add(new RenewalItemDrillDownDto
                                            {
                                                ItemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                                ItemName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                                Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                                                ItemType = reader.IsDBNull(3) ? null : reader.GetString(3),
                                                SerialNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                                ModelNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                                EndDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                                DaysUntilExpiry = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                                VendorName = reader.IsDBNull(8) ? null : reader.GetString(8),
                                                ConditionName = reader.IsDBNull(9) ? null : reader.GetString(9)
                                            });
                                        }
                                    }
                                }

                                return items;
                            }

                            if (string.Equals(key, "Expired", StringComparison.OrdinalIgnoreCase))
                                renewals = repo.GetExpiredRenewals() ?? new List<Yakult.Inventory.App.Models.RenewalDto>();
                            else if (string.Equals(key, "Expiring 30d", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(key, "Expiring Soon", StringComparison.OrdinalIgnoreCase))
                                renewals = repo.GetExpiringSoonRenewals() ?? new List<Yakult.Inventory.App.Models.RenewalDto>();
                            else if (string.Equals(key, "Active", StringComparison.OrdinalIgnoreCase))
                                renewals = repo.GetRenewalsByExpiryStatus("Active") ?? new List<Yakult.Inventory.App.Models.RenewalDto>();
                            else if (string.Equals(key, "On Hold", StringComparison.OrdinalIgnoreCase))
                                renewals = renewals.Where(r => string.Equals(r.Status, "On Hold", StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        return renewals;
                    }

                    case DrillDownContext.Invoices:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Invoices/Sets...");
                        var repo = new SetRepository();
                        var sets = repo.GetAllSetsAsync().ConfigureAwait(false).GetAwaiter().GetResult() ?? new List<SetDto>();
                        System.Diagnostics.Debug.WriteLine($"[Service] Sets fetched: {sets.Count} items");

                        sets = sets.Where(s =>
                            string.Equals(s.SetType, "Invoice", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.SetType, "Software/License", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.SetType, "Service", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.SetType, "Services", StringComparison.OrdinalIgnoreCase)).ToList();

                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            if (string.Equals(filterKey, "Software", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(filterKey, "Software/License", StringComparison.OrdinalIgnoreCase))
                                sets = sets.Where(s => string.Equals(s.SetType, "Software/License", StringComparison.OrdinalIgnoreCase)).ToList();
                            else if (string.Equals(filterKey, "Service", StringComparison.OrdinalIgnoreCase))
                                sets = sets.Where(s => string.Equals(s.SetType, "Service", StringComparison.OrdinalIgnoreCase) || string.Equals(s.SetType, "Services", StringComparison.OrdinalIgnoreCase)).ToList();
                            else if (string.Equals(filterKey, "Approved", StringComparison.OrdinalIgnoreCase))
                                sets = sets.Where(s => string.Equals(s.Status, "Yes", StringComparison.OrdinalIgnoreCase)).ToList();
                            else if (string.Equals(filterKey, "Not Approved", StringComparison.OrdinalIgnoreCase))
                                sets = sets.Where(s => !string.Equals(s.Status, "Yes", StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        return sets;
                    }

                    case DrillDownContext.Employees:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Employees...");
                        var employees = new List<EmployeeViewDto>();
                        using (var con = new SqlConnection(_connectionString))
                        {
                            con.Open();
                            System.Diagnostics.Debug.WriteLine($"[Service] SQL connection opened for Employees");
                            using (var cmd = new SqlCommand(@"
                                 SELECT e.EmpId, e.Name, e.Position, e.Description,
                                        c.Name AS CompanyName, b.Name AS BranchName, d.Name AS DepartmentName,
                                        e.DateCreated, u.Name AS CreatedByName,
                                        e.Active
                                 FROM dbo.Employee e
                                 LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                                 LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                                 LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                                 LEFT JOIN dbo.[User] u ON e.Createdby = u.UserId
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
                                        Active = !reader.IsDBNull(9) && reader.GetBoolean(9)
                                    });
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"[Service] Employees loaded: {employees.Count} items");
                        }

                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            var key = filterKey.Trim();

                            if (string.Equals(key, "Active", StringComparison.OrdinalIgnoreCase))
                            {
                                employees = employees.Where(e => e.Active).ToList();
                            }
                            else if (string.Equals(key, "Inactive", StringComparison.OrdinalIgnoreCase))
                            {
                                employees = employees.Where(e => !e.Active).ToList();
                            }
                            else if (string.Equals(key, "Departments", StringComparison.OrdinalIgnoreCase))
                            {
                                return employees
                                    .GroupBy(e => string.IsNullOrWhiteSpace(e.DepartmentName) ? "N/A" : e.DepartmentName)
                                    .Select(g => new GroupCountDto { Name = g.Key, Count = g.Count() })
                                    .OrderByDescending(x => x.Count)
                                    .ThenBy(x => x.Name)
                                    .ToList();
                            }
                            else if (string.Equals(key, "Branches", StringComparison.OrdinalIgnoreCase))
                            {
                                return employees
                                    .GroupBy(e => string.IsNullOrWhiteSpace(e.BranchName) ? "N/A" : e.BranchName)
                                    .Select(g => new GroupCountDto { Name = g.Key, Count = g.Count() })
                                    .OrderByDescending(x => x.Count)
                                    .ThenBy(x => x.Name)
                                    .ToList();
                            }
                            else if (string.Equals(key, "Companies", StringComparison.OrdinalIgnoreCase))
                            {
                                return employees
                                    .GroupBy(e => string.IsNullOrWhiteSpace(e.CompanyName) ? "N/A" : e.CompanyName)
                                    .Select(g => new GroupCountDto { Name = g.Key, Count = g.Count() })
                                    .OrderByDescending(x => x.Count)
                                    .ThenBy(x => x.Name)
                                    .ToList();
                            }
                            else if (string.Equals(key, "Total Employees", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(key, "Employees", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(key, "Departments", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(key, "Branches", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(key, "Companies", StringComparison.OrdinalIgnoreCase))
                            {
                                // Show all
                            }
                            else
                            {
                                // Treat as Dept/Company/Branch label from charts
                                employees = employees.Where(e =>
                                    string.Equals(e.DepartmentName, key, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(e.CompanyName, key, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(e.BranchName, key, StringComparison.OrdinalIgnoreCase)).ToList();
                            }
                        }

                        return employees;
                    }

                    case DrillDownContext.Vendors:
                    {
                        System.Diagnostics.Debug.WriteLine($"[Service] Fetching Vendors...");
                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            var key = filterKey.Trim();

                            // Summary card click: "Items" should show all vendors, not attempt vendor-name lookup.
                            if (string.Equals(key, "Items", StringComparison.OrdinalIgnoreCase))
                            {
                                var itemsAll = new List<VendorLinkedItemDto>();
                                using (var con = new SqlConnection(_connectionString))
                                using (var cmd = new SqlCommand(@"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    COALESCE(ic.Name, i.Category) AS Category,
    i.ItemType,
    ISNULL(i.StockOnHand, 0) AS StockOnHand,
    i.Active,
    i.SerialNumber,
    i.ModelNumber,
    v.VendorName
FROM dbo.Item i
INNER JOIN dbo.Vendor v ON v.VendorID = i.VendorId
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE arch.EntityId IS NULL
ORDER BY v.VendorName ASC, i.Name ASC, i.ItemId DESC;", con))
                                {
                                    con.Open();
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            itemsAll.Add(new VendorLinkedItemDto
                                            {
                                                ItemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                                ItemName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                                Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                                                ItemType = reader.IsDBNull(3) ? null : reader.GetString(3),
                                                StockOnHand = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                                Active = !reader.IsDBNull(5) && reader.GetBoolean(5),
                                                SerialNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                                ModelNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                                                VendorName = reader.IsDBNull(8) ? null : reader.GetString(8)
                                            });
                                        }
                                    }
                                }

                                return itemsAll;
                            }

                            // Chart click: "Active" / "Inactive" segments should filter vendors themselves
                            if (string.Equals(key, "Active", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(key, "Inactive", StringComparison.OrdinalIgnoreCase))
                            {
                                var repo = new VendorRepository();
                                var vendors = repo.GetAllVendorsAsync().ConfigureAwait(false).GetAwaiter().GetResult() ?? new List<VendorDto>();
                                System.Diagnostics.Debug.WriteLine($"[Service] Vendors fetched: {vendors.Count} items");

                                if (string.Equals(key, "Active", StringComparison.OrdinalIgnoreCase))
                                    vendors = vendors.Where(v => v.IsActive).ToList();
                                else
                                    vendors = vendors.Where(v => !v.IsActive).ToList();

                                return vendors;
                            }

                            // Chart click: vendor name segment -> show items linked to that vendor
                            var items = new List<VendorLinkedItemDto>();
                            using (var con = new SqlConnection(_connectionString))
                            using (var cmd = new SqlCommand(@"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    COALESCE(ic.Name, i.Category) AS Category,
    i.ItemType,
    ISNULL(i.StockOnHand, 0) AS StockOnHand,
    i.Active,
    i.SerialNumber,
    i.ModelNumber,
    v.VendorName
FROM dbo.Item i
INNER JOIN dbo.Vendor v ON v.VendorID = i.VendorId
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE arch.EntityId IS NULL
  AND v.VendorName = @VendorName
ORDER BY i.Name ASC, i.ItemId DESC;", con))
                            {
                                cmd.Parameters.AddWithValue("@VendorName", key);
                                con.Open();
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        items.Add(new VendorLinkedItemDto
                                        {
                                            ItemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                            ItemName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                            Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                                            ItemType = reader.IsDBNull(3) ? null : reader.GetString(3),
                                            StockOnHand = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                            Active = !reader.IsDBNull(5) && reader.GetBoolean(5),
                                            SerialNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                            ModelNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                                            VendorName = reader.IsDBNull(8) ? null : reader.GetString(8)
                                        });
                                    }
                                }
                            }

                            return items;
                        }

                        var repoAll = new VendorRepository();
                        var allVendors = repoAll.GetAllVendorsAsync().ConfigureAwait(false).GetAwaiter().GetResult() ?? new List<VendorDto>();
                        System.Diagnostics.Debug.WriteLine($"[Service] Vendors fetched: {allVendors.Count} items");
                        return allVendors;
                    }

                    case DrillDownContext.Archived:
                    {
                        var archived = new List<ArchivedItemDto>();
                        using (var con = new SqlConnection(_connectionString))
                        {
                            con.Open();
                            string sql = @"SELECT EntityType, EntityId, EntityName, ArchivedBy, ArchivedAt, ArchiveReason
                                           FROM dbo.vw_AllArchivedEntities ORDER BY ArchivedAt DESC";

                            using (var cmd = new SqlCommand(sql, con))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    archived.Add(new ArchivedItemDto
                                    {
                                        EntityType = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                        EntityId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                        EntityName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        ArchivedBy = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                        ArchivedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                                        ArchiveReason = reader.IsDBNull(5) ? "" : reader.GetString(5)
                                    });
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(filterKey))
                        {
                            string key = filterKey.Trim();
                            var normalizedKey = key;
                            if (normalizedKey.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalizedKey.Length > 1)
                                normalizedKey = normalizedKey.Substring(0, normalizedKey.Length - 1);

                            if (!string.Equals(normalizedKey, "Total Archived", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(normalizedKey, "Archived", StringComparison.OrdinalIgnoreCase))
                            {
                                // Donut uses Items vs Others.
                                if (string.Equals(key, "Others", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(normalizedKey, "Other", StringComparison.OrdinalIgnoreCase))
                                {
                                    archived = archived.Where(a => !string.Equals(a.EntityType, "Item", StringComparison.OrdinalIgnoreCase)).ToList();
                                }
                                else
                                {
                                    archived = archived.Where(a => string.Equals(a.EntityType, normalizedKey, StringComparison.OrdinalIgnoreCase)).ToList();
                                }
                            }
                        }

                        return archived;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Service.GetDrillDownItems] ERROR: {ex.Message}\n{ex.StackTrace}");
                throw; // Re-throw to show user the actual error
            }

            System.Diagnostics.Debug.WriteLine($"[Service.GetDrillDownItems] Returning empty list (no matching context)");
            return new List<object>();
        }

        public class EmployeeViewDto
        {
            public int EmpId { get; set; }
            public string Name { get; set; }
            public string Position { get; set; }
            public string Description { get; set; }
            public string CompanyName { get; set; }
            public string BranchName { get; set; }
            public string DepartmentName { get; set; }
            public DateTime DateCreated { get; set; }
            public string CreatedByName { get; set; }
            public bool Active { get; set; }
        }

        public class GroupCountDto
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public class VendorLinkedItemDto
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; }
            public string Category { get; set; }
            public string ItemType { get; set; }
            public int StockOnHand { get; set; }
            public bool Active { get; set; }
            public string SerialNumber { get; set; }
            public string ModelNumber { get; set; }
            public string VendorName { get; set; }
        }

        public class ArchivedItemDto
        {
            public string EntityType { get; set; }
            public int EntityId { get; set; }
            public string EntityName { get; set; }
            public string ArchivedBy { get; set; }
            public DateTime ArchivedAt { get; set; }
            public string ArchiveReason { get; set; }
        }

        public class RenewalItemDrillDownDto
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; }
            public string Category { get; set; }
            public string ItemType { get; set; }
            public string SerialNumber { get; set; }
            public string ModelNumber { get; set; }
            public DateTime? EndDate { get; set; }
            public int? DaysUntilExpiry { get; set; }
            public string VendorName { get; set; }
            public string ConditionName { get; set; }
        }

        // ===== SCOPE: Items =====
        private DashboardData GetItemsDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.Items,
                Title = "\U0001F4E6 Items Dashboard",
                PrimaryChartTitle = "ITEMS BY CATEGORY",
                SecondaryChartTitle = "TOP ITEMS BY CATEGORY",
                TertiaryChartTitle = "ITEM TYPE DISTRIBUTION",
                TableTitle = "\U0001F4CB RECENT ITEMS"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string sqlItems = $@"
                        SELECT
                            i.ItemId,
                            i.Name,
                            i.ItemType,
                            ic.Name AS CategoryName,
                            i.StockOnHand,
                            i.Active
                        FROM dbo.Item i
                        INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                        WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                        ORDER BY i.ItemId DESC";

                    var items = new List<(int ItemId, string Name, string Type, string Category, int Stock, bool Active)>();
                    using (var cmd = new SqlCommand(sqlItems, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add((
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                reader.GetString(3),
                                reader.GetInt32(4),
                                reader.GetBoolean(5)
                            ));
                        }
                    }

                    // KPIs
                    int totalItems = items.Count;
                    int activeItems = items.Count(i => i.Active);
                    int hardwareItems = items.Count(i => string.Equals(i.Type, "Hardware", StringComparison.OrdinalIgnoreCase));
                    int softwareItems = items.Count(i => string.Equals(i.Type, "Software/License", StringComparison.OrdinalIgnoreCase));
                    int servicesItems = items.Count(i => string.Equals(i.Type, "Services", StringComparison.OrdinalIgnoreCase));

                    data.Kpis.Add(new DashboardKpi { Title = "Total Items", Value = totalItems, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F4E6" });
                    data.Kpis.Add(new DashboardKpi { Title = "Active Items", Value = activeItems, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\u2705" });
                    data.Kpis.Add(new DashboardKpi { Title = "Hardware", Value = hardwareItems, AccentColor = Color.FromArgb(155, 89, 182), Icon = "\U0001F4BB" });
                    data.Kpis.Add(new DashboardKpi { Title = "Software", Value = softwareItems, AccentColor = Color.FromArgb(52, 73, 94), Icon = "\U0001F4BF" });
                    data.Kpis.Add(new DashboardKpi { Title = "Services", Value = servicesItems, AccentColor = Color.FromArgb(230, 126, 34), Icon = "\U0001F527" });

                    // Charts
                    // 1) Items by Category
                    var byCategory = items
                        .GroupBy(i => i.Category)
                        .Select(g => new { Category = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    data.PrimaryChartMetrics = byCategory.Take(6)
                        .Select(x => new DashboardMetric { Label = x.Category, Value = x.Count })
                        .ToList();

                    // 2) Top Items by Category (group by Item Name)
                    var topItemsByCategory = items
                        .GroupBy(i => new { i.Category, i.Name })
                        .Select(g => new { Label = $"{g.Key.Category} - {g.Key.Name}", Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.Label)
                        .Take(8)
                        .ToList();
                    data.SecondaryChartMetrics = topItemsByCategory
                        .Select(x => new DashboardMetric { Label = x.Label, Value = x.Count })
                        .ToList();

                    // 3) Item Type Distribution
                    var byType = items
                        .GroupBy(i => i.Type)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    data.TertiaryChartMetrics = byType.Take(4)
                        .Select(x => new DashboardMetric { Label = x.Type, Value = x.Count })
                        .ToList();

                    // Quick stats
                    int inactiveItems = totalItems - activeItems;
                    data.Stat1 = new DashboardQuickStat { Label = "Active", Value = activeItems.ToString(), Color = Color.FromArgb(46, 204, 113) };
                    data.Stat2 = new DashboardQuickStat { Label = "Inactive", Value = inactiveItems.ToString(), Color = Color.FromArgb(231, 76, 60) };
                    data.Stat3 = new DashboardQuickStat { Label = "Categories", Value = byCategory.Count.ToString(), Color = Color.FromArgb(52, 152, 219) };

                    // Table (recent items)
                    foreach (var item in items.Take(20))
                    {
                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = item.Name,
                            Col2 = item.Category,
                            Col3 = item.Type,
                            Col4 = item.Stock.ToString(),
                            StatusBadge = item.Active ? "\u2705 Active" : "\u26A0 Inactive",
                            StatusColor = item.Active ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Items dashboard error: {ex}");
            }

            return data;
        }

        // ===== SCOPE: Inventory Movements =====
        // CRITICAL: Fixed Assets are EXCLUDED from all movement-based analytics
        // Fixed Assets never move in or out and must not be counted as movements
        private DashboardData GetInventoryMovementsDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.InventoryMovements,
                Title = "\U0001F4CA Inventory Movements Dashboard",
                PrimaryChartTitle = "MOVEMENTS BY ENTRY TYPE",
                SecondaryChartTitle = "TOP CATEGORY BY MOVEMENT",
                TertiaryChartTitle = "POSITIVE VS NEGATIVE",
                TableTitle = "\U0001F4CB RECENT MOVEMENTS"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // CRITICAL: Exclude Fixed Assets from movement analytics
                    // Fixed Assets are identified by EntryType = 'Fixed Asset' or 'Fixed Assets'
                    string sql = $@"
                        SELECT
                            inv.InvId,
                            inv.EntryType,
                            i.ItemType,
                            i.Name AS ItemName,
                            ic.Name AS CategoryName,
                            inv.Quantity,
                            CASE
                                WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                                WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                                ELSE inv.Quantity
                            END AS SignedQuantity,
                            inv.DatePosted
                        FROM dbo.Inventory inv
                        LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
                        INNER JOIN dbo.Item i ON i.ItemId = COALESCE(inv.ItemId, r.ItemId)
                        INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                        WHERE inv.Active = 1
                          AND {ArchiveFilter(con, "Inventory", "inv.InvId")}
                          AND {ArchiveFilter(con, "Item", "i.ItemId")}
                          AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                          AND (inv.ReqId IS NULL OR {ArchiveFilter(con, "Request", "inv.ReqId")})
                          AND inv.EntryType NOT IN ('Fixed Asset', 'Fixed Assets')
                        ORDER BY inv.DatePosted DESC";

                    var movements = new List<(int InvId, string EntryType, string ItemType, string ItemName, string CategoryName, int Qty, int SignedQty, DateTime Date)>();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            movements.Add((
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.IsDBNull(2) ? "" : reader.GetString(2),
                                reader.IsDBNull(3) ? "" : reader.GetString(3),
                                reader.IsDBNull(4) ? "" : reader.GetString(4),
                                reader.GetInt32(5),
                                reader.GetInt32(6),
                                reader.GetDateTime(7)
                            ));
                        }
                    }

                    // KPIs - Fixed Assets are excluded
                    int totalMovements = movements.Count;
                    int totalQtyIn = movements.Where(m => string.Equals(m.EntryType, "Positive", StringComparison.OrdinalIgnoreCase)).Sum(m => m.Qty);
                    int totalQtyOut = movements.Where(m => string.Equals(m.EntryType, "Negative", StringComparison.OrdinalIgnoreCase)).Sum(m => m.Qty);
                    int positiveCount = movements.Count(m => string.Equals(m.EntryType, "Positive", StringComparison.OrdinalIgnoreCase));
                    int negativeCount = movements.Count(m => string.Equals(m.EntryType, "Negative", StringComparison.OrdinalIgnoreCase));

                    data.Kpis.Add(new DashboardKpi { Title = "Total Movements", Value = totalMovements, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F4CA" });
                    data.Kpis.Add(new DashboardKpi { Title = "Positive", Value = positiveCount, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\U0001F4E5" });
                    data.Kpis.Add(new DashboardKpi { Title = "Negative", Value = negativeCount, AccentColor = Color.FromArgb(231, 76, 60), Icon = "\U0001F4E4" });
                    data.Kpis.Add(new DashboardKpi { Title = "Qty In", Value = totalQtyIn, AccentColor = Color.FromArgb(26, 188, 156), Icon = "\U0001F4E5" });
                    data.Kpis.Add(new DashboardKpi { Title = "Qty Out", Value = totalQtyOut, AccentColor = Color.FromArgb(230, 126, 34), Icon = "\U0001F4E4" });

                    // Charts
                    // 1) Total Movement by Entry Type (sum of absolute signed quantities)
                    var byEntryType = movements
                        .GroupBy(m => m.EntryType)
                        .Select(g => new { EntryType = g.Key, TotalMove = g.Sum(x => Math.Abs(x.SignedQty)) })
                        .OrderByDescending(x => x.TotalMove)
                        .ToList();
                    data.PrimaryChartMetrics = byEntryType
                        .Select(x => new DashboardMetric { Label = x.EntryType, Value = x.TotalMove })
                        .ToList();

                    // 2) Top Category by Movement (total absolute movement quantity)
                    // CHANGED: Now groups by Category instead of ItemType
                    var byCategory = movements
                        .Where(m => !string.IsNullOrEmpty(m.CategoryName))
                        .GroupBy(m => m.CategoryName)
                        .Select(g => new { Category = g.Key, TotalMove = g.Sum(x => Math.Abs(x.SignedQty)) })
                        .OrderByDescending(x => x.TotalMove)
                        .Take(8)
                        .ToList();
                    data.SecondaryChartMetrics = byCategory
                        .Select(x => new DashboardMetric { Label = x.Category, Value = x.TotalMove })
                        .ToList();

                    // 3) Positive vs Negative (count) - Fixed Assets excluded
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Positive", Value = positiveCount });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Negative", Value = negativeCount });

                    // No Quick Stats (2nd row removed)

                    // Table (recent movements) - Fixed Assets already excluded from query
                    foreach (var m in movements.Take(20))
                    {
                        var statusColor = string.Equals(m.EntryType, "Positive", StringComparison.OrdinalIgnoreCase)
                            ? Color.FromArgb(46, 204, 113)
                            : string.Equals(m.EntryType, "Negative", StringComparison.OrdinalIgnoreCase)
                                ? Color.FromArgb(231, 76, 60)
                                : Color.FromArgb(52, 152, 219);

                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = m.Date.ToString("yyyy-MM-dd"),
                            Col2 = m.ItemName,
                            Col3 = m.CategoryName,
                            Col4 = m.EntryType,
                            Col5 = m.SignedQty.ToString(),
                            StatusBadge = m.EntryType,
                            StatusColor = statusColor
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Inventory movements dashboard error: {ex}");
            }

            return data;
        }

        // ===== SCOPE: Requests =====
        private DashboardData GetRequestsDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.Requests,
                Title = "\U0001F4DD Requests Dashboard",
                PrimaryChartTitle = "REQUESTS BY STATUS",
                SecondaryChartTitle = "TOP REQUESTED ITEMS",
                TertiaryChartTitle = "TOTAL REQUESTS BY CATEGORY (HARDWARE)",
                TableTitle = "\U0001F4CB RECENT REQUESTS"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string sql = $@"
                        SELECT
                            r.ReqId,
                            r.Status,
                            i.Name AS ItemName,
                            ic.Name AS CategoryName,
                            i.ItemType,
                            e.Name AS EmployeeName,
                            r.Quantity,
                            r.DateRequested
                        FROM dbo.Request r
                        INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                        INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                        INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                        WHERE r.Active = 1
                          AND {ArchiveFilter(con, "Request", "r.ReqId")}
                          AND {ArchiveFilter(con, "Item", "r.ItemId")}
                          AND {ArchiveFilter(con, "Employee", "r.EmpId")}
                        ORDER BY r.DateRequested DESC";

                    var requests = new List<(string Status, string Item, string Category, string ItemType, string Employee, int Qty, DateTime Date)>();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add((
                                NormalizeRequestStatus(reader.IsDBNull(1) ? null : reader.GetString(1)),
                                reader.GetString(2),
                                reader.GetString(3),
                                reader.GetString(4),
                                reader.GetString(5),
                                reader.GetInt32(6),
                                reader.GetDateTime(7)
                            ));
                        }
                    }

                    // KPIs
                    int totalRequests = requests.Count;
                    int submittedRequests = requests.Count(r => string.Equals(r.Status, "Submitted", StringComparison.OrdinalIgnoreCase));
                    int underReviewRequests = requests.Count(r => string.Equals(r.Status, "Under Review", StringComparison.OrdinalIgnoreCase));
                    int totalQty = requests.Sum(r => r.Qty);

                    data.Kpis.Add(new DashboardKpi { Title = "Total Requests", Value = totalRequests, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F4DD" });
                    data.Kpis.Add(new DashboardKpi { Title = "Submitted", Value = submittedRequests, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\U0001F4E4" });
                    data.Kpis.Add(new DashboardKpi { Title = "Under Review", Value = underReviewRequests, AccentColor = Color.FromArgb(241, 196, 15), Icon = "\u23F3" });
                    data.Kpis.Add(new DashboardKpi { Title = "Total Quantity", Value = totalQty, AccentColor = Color.FromArgb(52, 73, 94), Icon = "\U0001F4E6" });


                    // Charts
                    // 1) Requests by Status
                    var byStatus = requests
                        .GroupBy(r => NormalizeRequestStatus(r.Status))
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    data.PrimaryChartMetrics = byStatus
                        .Select(x => new DashboardMetric { Label = x.Status, Value = x.Count })
                        .ToList();

                    // 2) Top Requested Items (by quantity)
                    var byItem = requests
                        .GroupBy(r => r.Item)
                        .Select(g => new { Item = g.Key, Qty = g.Sum(x => x.Qty) })
                        .OrderByDescending(x => x.Qty)
                        .Take(8)
                        .ToList();
                    data.SecondaryChartMetrics = byItem
                        .Select(x => new DashboardMetric { Label = x.Item, Value = x.Qty })
                        .ToList();

                    // 3) Total Requests by Category (Hardware)
                    var hardwareByCategory = requests
                        .Where(r => string.Equals(r.ItemType, "Hardware", StringComparison.OrdinalIgnoreCase))
                        .GroupBy(r => r.Category)
                        .Select(g => new { Category = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(8)
                        .ToList();
                    data.TertiaryChartMetrics = hardwareByCategory
                        .Select(x => new DashboardMetric { Label = x.Category, Value = x.Count })
                        .ToList();

                    // No Quick Stats (2nd row removed)


                    // Table
                    foreach (var r in requests.Take(20))
                    {
                        var status = NormalizeRequestStatus(r.Status);
                        var statusColor =
                            string.Equals(status, "Under Review", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(241, 196, 15) :
                            string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(46, 204, 113) :
                            string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(52, 152, 219) :
                            Color.FromArgb(155, 89, 182);

                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = r.Date.ToString("yyyy-MM-dd"),
                            Col2 = r.Employee,
                            Col3 = r.Item,
                            Col4 = r.Category,
                            Col5 = r.Qty.ToString(),
                            StatusBadge = status,
                            StatusColor = statusColor
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Requests dashboard error: {ex}");
            }

            return data;
        }

        // ===== SCOPE: Renewals =====
        // CRITICAL: This is the single source of truth for all Renewals Dashboard data.
        // ALL counts, charts, and tables MUST use the filtered dataset from GetNonArchivedRenewals().
        private DashboardData GetRenewalsDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.Renewals,
                Title = "\U0001F504 Renewals Dashboard",
                PrimaryChartTitle = "RENEWALS BY STATUS",
                SecondaryChartTitle = "EXPIRING SOON (\u226430 DAYS)",
                TertiaryChartTitle = "ACTIVE VS EXPIRED VS ON HOLD",
                TableTitle = "\U0001F4CB UPCOMING RENEWALS"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Single source query - all dashboard data derives from this filtered dataset
                    var renewals = GetNonArchivedRenewals(con);

                    // KPIs - derived from filtered source only
                    int totalRenewals = renewals.Count;
                    int activeRenewals = renewals.Count(r => r.ExpiryStatus == "Active");
                    int expiringSoon = renewals.Count(r => r.DaysLeft.HasValue && r.DaysLeft.Value >= 0 && r.DaysLeft.Value <= 30);
                    int expired = renewals.Count(r => r.ExpiryStatus == "Expired");
                    int onHold = renewals.Count(r => r.RenewalStatus == "On Hold");

                    data.Kpis.Add(new DashboardKpi { Title = "Total Items", Value = totalRenewals, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F504" });
                    data.Kpis.Add(new DashboardKpi { Title = "Active", Value = activeRenewals, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\u2705" });
                    data.Kpis.Add(new DashboardKpi { Title = "Expiring 30d", Value = expiringSoon, AccentColor = Color.FromArgb(241, 196, 15), Icon = "\u23F0" });
                    data.Kpis.Add(new DashboardKpi { Title = "Expired", Value = expired, AccentColor = Color.FromArgb(231, 76, 60), Icon = "\u26A0\uFE0F" });
                    data.Kpis.Add(new DashboardKpi { Title = "On Hold", Value = onHold, AccentColor = Color.FromArgb(155, 89, 182), Icon = "\u23F8" });

                    // Charts - derived from filtered source only
                    var byStatus = renewals
                        .GroupBy(r => r.RenewalStatus)
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    data.PrimaryChartMetrics = byStatus
                        .Select(x => new DashboardMetric { Label = x.Status, Value = x.Count })
                        .ToList();

                    var expiringSoonList = renewals
                        .Where(r => r.DaysLeft.HasValue && r.DaysLeft.Value >= 0 && r.DaysLeft.Value <= 30)
                        .OrderBy(r => r.DaysLeft.Value)
                        .Take(8)
                        .ToList();
                    data.SecondaryChartMetrics = expiringSoonList
                        .Select(x => new DashboardMetric { Label = x.ItemName, Value = x.DaysLeft ?? 0 })
                        .ToList();

                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Active", Value = activeRenewals });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Expired", Value = expired });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "On Hold", Value = onHold });

                    // No Quick Stats (2nd row removed)

                    // Table - derived from filtered source only
                    foreach (var r in renewals
                        .Where(x => x.DaysLeft.HasValue)
                        .OrderBy(x => x.DaysLeft.Value)
                        .Take(20))
                    {
                        var statusColor = (r.DaysLeft ?? int.MaxValue) <= 30 ? Color.FromArgb(231, 76, 60) :
                                         Color.FromArgb(46, 204, 113);

                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = r.ItemName,
                            Col2 = r.ItemType,
                            Col3 = r.StartDate?.ToString("yyyy-MM-dd") ?? "N/A",
                            Col4 = r.EndDate?.ToString("yyyy-MM-dd") ?? "N/A",
                            Col5 = (r.DaysLeft?.ToString() ?? "N/A") + (r.DaysLeft.HasValue ? " days" : ""),
                            Col6 = r.RenewalStatus,
                            StatusBadge = r.ExpiryStatus,
                            StatusColor = statusColor
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Renewals dashboard error: {ex}");
            }

            return data;
        }

        // CRITICAL: Single source query for non-archived renewals
        // This method enforces archive exclusion at the database level - no alternate paths allowed
        private List<(int ItemId, string ItemName, string ItemType, DateTime? StartDate, DateTime? EndDate, string ExpiryStatus, int? DaysLeft, string RenewalStatus)>
            GetNonArchivedRenewals(SqlConnection con)
        {
            // ARCHIVE FILTER ENFORCEMENT: Renewals.IsArchived = 0 is mandatory - archived renewals must NEVER appear on dashboards
            string sql = $@"
                SELECT
                    i.ItemId,
                    i.Name AS ItemName,
                    i.ItemType,
                    i.StartDate,
                    i.EndDate,
                    CASE
                        WHEN i.EndDate IS NULL THEN NULL
                        ELSE DATEDIFF(DAY, GETDATE(), i.EndDate)
                    END AS DaysUntilExpiry,
                    CASE
                        WHEN i.EndDate IS NULL THEN 'No Expiry Date'
                        WHEN i.EndDate < GETDATE() THEN 'Expired'
                        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 30 THEN 'Expiring Soon'
                        ELSE 'Active'
                    END AS ExpiryStatus,
                    ISNULL(latest.RenewalStatus, 'None') AS RenewalStatus
                FROM dbo.Item i
                OUTER APPLY (
                    SELECT TOP 1 r.RenewalStatus
                    FROM dbo.Renewals r
                    WHERE r.ItemId = i.ItemId
                      AND r.IsArchived = 0  -- CRITICAL: Only non-archived renewal records
                    ORDER BY r.CreatedAt DESC
                ) latest
                WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND i.IsTrackedAsset = 1  -- Only tracked assets appear in renewals
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.Renewals r
                      WHERE r.ItemId = i.ItemId
                        AND r.IsArchived = 0  -- CRITICAL: Only items with non-archived renewal records appear
                  )
                ORDER BY
                    CASE
                        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) IS NULL THEN 999999
                        ELSE DATEDIFF(DAY, GETDATE(), i.EndDate)
                    END ASC";

            var renewals = new List<(int ItemId, string ItemName, string ItemType, DateTime? StartDate, DateTime? EndDate, string ExpiryStatus, int? DaysLeft, string RenewalStatus)>();

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    renewals.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2),
                        reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                        reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                        reader.GetString(6),
                        reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                        reader.GetString(7)
                    ));
                }
            }

            return renewals;
        }

        // ===== SCOPE: Invoices/Services =====
        private DashboardData GetInvoicesServicesDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.InvoicesServices,
                Title = "\U0001F4BC Invoices & Services Dashboard",
                PrimaryChartTitle = "INVOICES BY TYPE",
                SecondaryChartTitle = "TOP SOFTWARE/LICENSE",
                TertiaryChartTitle = "INVOICE STATUS",
                TableTitle = "\U0001F4CB RECENT INVOICES"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    var invoiceSets = new List<(int SetId, string SetCode, string SetType, string Status, string DocumentNumber, DateTime? DocumentDate)>();
                    using (var cmdSets = new SqlCommand($@"
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.Status,
    s.DocumentNumber,
    s.DispatchDate AS DocumentDate
FROM dbo.[Set] s
WHERE s.IsInvoice = 1
  AND {ArchiveFilter(con, "Set", "s.SetId")}
ORDER BY s.DispatchDate DESC, s.SetId DESC;", con))
                    using (var readerSets = cmdSets.ExecuteReader())
                    {
                        while (readerSets.Read())
                        {
                            invoiceSets.Add((
                                readerSets.GetInt32(0),
                                readerSets.IsDBNull(1) ? "" : readerSets.GetString(1),
                                readerSets.IsDBNull(2) ? "" : readerSets.GetString(2),
                                readerSets.IsDBNull(3) ? "" : readerSets.GetString(3),
                                readerSets.IsDBNull(4) ? "" : readerSets.GetString(4),
                                readerSets.IsDBNull(5) ? (DateTime?)null : readerSets.GetDateTime(5)
                            ));
                        }
                    }

                    string sql = $@"
                        SELECT *
                        FROM (
                            SELECT
                                ii.SetId,
                                ii.SetCode,
                                ii.SetType,
                                ii.Status,
                                ii.DocumentNumber,
                                ii.DocumentDate,
                                ii.ItemName,
                                ii.Quantity
                            FROM dbo.vw_InvoiceItems ii
                            WHERE {ArchiveFilter(con, "Set", "ii.SetId")}

                            UNION ALL

                            SELECT
                                s.SetId,
                                s.SetCode,
                                s.SetType,
                                s.Status,
                                s.DocumentNumber,
                                s.DispatchDate AS DocumentDate,
                                ISNULL(i.Name, '') AS ItemName,
                                si.Quantity
                            FROM dbo.[Set] s
                            INNER JOIN dbo.SetItem si ON s.SetId = si.SetId
                            LEFT JOIN dbo.Item i ON si.ItemId = i.ItemId
                            WHERE s.IsInvoice = 1
                              AND {ArchiveFilter(con, "Set", "s.SetId")}
                              AND (i.ItemId IS NULL OR {ArchiveFilter(con, "Item", "i.ItemId")})
                        ) x
                        ORDER BY x.DocumentDate DESC";

                    var invoiceLines = new List<(int SetId, string SetCode, string SetType, string Status, string DocumentNumber, DateTime? DocumentDate, string ItemName, int Qty)>();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int qty = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));
                            invoiceLines.Add((
                                reader.GetInt32(0),
                                reader.IsDBNull(1) ? "" : reader.GetString(1),
                                reader.IsDBNull(2) ? "" : reader.GetString(2),
                                reader.IsDBNull(3) ? "" : reader.GetString(3),
                                reader.IsDBNull(4) ? "" : reader.GetString(4),
                                reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                reader.IsDBNull(6) ? "" : reader.GetString(6),
                                qty
                            ));
                        }
                    }

                    int totalInvoices = invoiceSets.Count;
                    int softwareInvoices = invoiceSets.Count(i => string.Equals(i.SetType, "Software/License", StringComparison.OrdinalIgnoreCase));
                    int serviceInvoices = invoiceSets.Count(i => string.Equals(i.SetType, "Service", StringComparison.OrdinalIgnoreCase) || string.Equals(i.SetType, "Services", StringComparison.OrdinalIgnoreCase));
                    int approvedInvoices = invoiceSets.Count(i => string.Equals(i.Status, "Yes", StringComparison.OrdinalIgnoreCase));
                    int notApprovedInvoices = totalInvoices - approvedInvoices;

                    // KPIs
                    data.Kpis.Add(new DashboardKpi { Title = "Total Invoices", Value = totalInvoices, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F4BC" });
                    data.Kpis.Add(new DashboardKpi { Title = "Software", Value = softwareInvoices, AccentColor = Color.FromArgb(155, 89, 182), Icon = "\U0001F4BF" });
                    data.Kpis.Add(new DashboardKpi { Title = "Service", Value = serviceInvoices, AccentColor = Color.FromArgb(230, 126, 34), Icon = "\U0001F527" });
                    data.Kpis.Add(new DashboardKpi { Title = "Approved", Value = approvedInvoices, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\u2705" });
                    data.Kpis.Add(new DashboardKpi { Title = "Not Approved", Value = notApprovedInvoices, AccentColor = Color.FromArgb(231, 76, 60), Icon = "\u26A0" });

                    // Charts
                    // 1) Invoices by Type
                    var byType = invoiceSets
                        .GroupBy(i =>
                            string.Equals(i.SetType, "Service", StringComparison.OrdinalIgnoreCase) || string.Equals(i.SetType, "Services", StringComparison.OrdinalIgnoreCase)
                                ? "Services"
                                : i.SetType)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    data.PrimaryChartMetrics = byType
                        .Select(x => new DashboardMetric { Label = x.Type, Value = x.Count })
                        .ToList();

                    // 2) Top Software/License (group by Item Name)
                    var topSoftware = invoiceLines
                        .Where(x => string.Equals(x.SetType, "Software/License", StringComparison.OrdinalIgnoreCase))
                        .GroupBy(x => x.ItemName)
                        .Select(g => new { ItemName = g.Key, Qty = g.Sum(x => x.Qty) })
                        .OrderByDescending(x => x.Qty)
                        .Take(8)
                        .ToList();
                    data.SecondaryChartMetrics = topSoftware
                        .Select(x => new DashboardMetric { Label = x.ItemName, Value = x.Qty })
                        .ToList();

                    // 3) Invoice Status (Yes/No)
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Yes", Value = approvedInvoices });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "No", Value = notApprovedInvoices });

                    // No Quick Stats (2nd row removed)

                    // Table
                    foreach (var inv in invoiceSets.Take(20))
                    {
                        var statusColor = string.Equals(inv.Status, "Yes", StringComparison.OrdinalIgnoreCase)
                            ? Color.FromArgb(46, 204, 113)
                            : Color.FromArgb(231, 76, 60);

                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = inv.SetCode,
                            Col2 = inv.DocumentNumber,
                            Col3 = inv.SetType,
                            Col4 = inv.DocumentDate.HasValue ? inv.DocumentDate.Value.ToString("yyyy-MM-dd") : "N/A",
                            StatusBadge = inv.Status,
                            StatusColor = statusColor
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invoices/Services dashboard error: {ex}");
            }

            return data;
        }

        // ===== SCOPE: Employees =====
        private DashboardData GetEmployeesDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.Employees,
                Title = "\U0001F465 Employees Dashboard",
                PrimaryChartTitle = "EMPLOYEES BY DEPARTMENT",
                SecondaryChartTitle = "TOTAL EMPLOYEES PER COMPANY",
                TertiaryChartTitle = "ACTIVE VS INACTIVE",
                TableTitle = "\U0001F4CB EMPLOYEE LIST"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string sql = $@"
                        SELECT
                            e.EmpId,
                            e.Name,
                            e.Position,
                            b.Name AS BranchName,
                            d.Name AS DepartmentName,
                            c.Name AS CompanyName,
                            e.Active
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                        LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                        WHERE {ArchiveFilter(con, "Employee", "e.EmpId")}
                        ORDER BY e.Name";

                    var employees = new List<(string Name, string Position, string Branch, string Dept, string Company, bool Active)>();

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            employees.Add((
                                reader.GetString(1),
                                reader.IsDBNull(2) ? "" : reader.GetString(2),
                                reader.IsDBNull(3) ? "" : reader.GetString(3),
                                reader.IsDBNull(4) ? "" : reader.GetString(4),
                                reader.IsDBNull(5) ? "" : reader.GetString(5),
                                reader.GetBoolean(6)
                            ));
                        }
                    }

                    // KPIs
                    int totalEmployees = employees.Count;
                    int activeEmployees = employees.Count(e => e.Active);
                    var depts = employees.GroupBy(e => e.Dept).Count();
                    var branches = employees.GroupBy(e => e.Branch).Count();
                    var companies = employees.GroupBy(e => e.Company).Count();

                    data.Kpis.Add(new DashboardKpi { Title = "Total Employees", Value = totalEmployees, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F465" });
                    data.Kpis.Add(new DashboardKpi { Title = "Active", Value = activeEmployees, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\u2705" });
                    data.Kpis.Add(new DashboardKpi { Title = "Departments", Value = depts, AccentColor = Color.FromArgb(155, 89, 182), Icon = "\U0001F3E2" });
                    data.Kpis.Add(new DashboardKpi { Title = "Branches", Value = branches, AccentColor = Color.FromArgb(230, 126, 34), Icon = "\U0001F3EA" });
                    data.Kpis.Add(new DashboardKpi { Title = "Companies", Value = companies, AccentColor = Color.FromArgb(52, 73, 94), Icon = "\U0001F3ED" });

                    // Charts
                    var byDept = employees.GroupBy(e => e.Dept).Select(g => new { Dept = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(6).ToList();
                    data.PrimaryChartMetrics = byDept.Select(x => new DashboardMetric { Label = x.Dept, Value = x.Count }).ToList();

                    var byCompany = employees.GroupBy(e => e.Company).Select(g => new { Company = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(8).ToList();
                    data.SecondaryChartMetrics = byCompany.Select(x => new DashboardMetric { Label = x.Company, Value = x.Count }).ToList();

                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Active", Value = activeEmployees });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Inactive", Value = totalEmployees - activeEmployees });

                    // No Quick Stats (2nd row removed)

                    // Table
                    foreach (var emp in employees.Take(20))
                    {
                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = emp.Name,
                            Col2 = emp.Position,
                            Col3 = emp.Dept,
                            Col4 = emp.Branch,
                            Col5 = emp.Company,
                            StatusBadge = emp.Active ? "\u2705 Active" : "\u26A0 Inactive",
                            StatusColor = emp.Active ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Employees dashboard error: {ex}");
            }

            return data;
        }

        // ===== SCOPE: Vendors =====
        private DashboardData GetVendorsDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.Vendors,
                Title = "\U0001F3EA Vendors Dashboard",
                PrimaryChartTitle = "VENDORS DISTRIBUTION (ITEM COUNT)",
                SecondaryChartTitle = "TOP VENDORS (INVOICES)",
                TertiaryChartTitle = "ACTIVE VS INACTIVE",
                TableTitle = "\U0001F4CB VENDOR LIST"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string sqlVendors = $@"
                        SELECT
                            v.VendorID,
                            v.VendorName,
                            v.IsActive,
                            COUNT(i.ItemId) AS ItemCount
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.Item i
                          ON v.VendorID = i.VendorId
                         AND {ArchiveFilter(con, "Item", "i.ItemId")}
                        WHERE {ArchiveFilter(con, "Vendor", "v.VendorID")}
                        GROUP BY v.VendorID, v.VendorName, v.IsActive
                        ORDER BY v.VendorName";

                    var vendors = new List<(int VendorId, string Name, bool Active, int ItemCount)>();
                    using (var cmd = new SqlCommand(sqlVendors, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            vendors.Add((
                                reader.GetInt32(0),
                                reader.GetString(1),
                                reader.GetBoolean(2),
                                reader.GetInt32(3)
                            ));
                        }
                    }

                    // Invoice counts per vendor (from vw_InvoiceItems)
                    string sqlInvoiceCounts = @"
                        SELECT
                            x.VendorId,
                            COUNT(DISTINCT x.SetId) AS InvoiceCount
                        FROM (
                            SELECT
                                ii.VendorId,
                                ii.SetId
                            FROM dbo.vw_InvoiceItems ii
                            WHERE ii.VendorId IS NOT NULL
                              AND " + (HasArchiveFunction(con) ? "dbo.fn_IsEntityArchived('Set', ii.SetId) = 0" : "1=1") + @"

                            UNION ALL

                            SELECT
                                i.VendorId,
                                s.SetId
                            FROM dbo.[Set] s
                            INNER JOIN dbo.SetItem si ON s.SetId = si.SetId
                            INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
                            WHERE s.SetType = 'Invoice'
                              AND i.VendorId IS NOT NULL
                              AND " + (HasArchiveFunction(con) ? "dbo.fn_IsEntityArchived('Set', s.SetId) = 0" : "1=1") + @"
                              AND " + (HasArchiveFunction(con) ? "dbo.fn_IsEntityArchived('Item', i.ItemId) = 0" : "1=1") + @"
                        ) x
                        GROUP BY x.VendorId";

                    var invoiceCounts = new Dictionary<int, int>();
                    using (var cmd = new SqlCommand(sqlInvoiceCounts, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            invoiceCounts[reader.GetInt32(0)] = reader.GetInt32(1);
                        }
                    }

                    // KPIs
                    int totalVendors = vendors.Count;
                    int activeVendors = vendors.Count(v => v.Active);
                    int inactiveVendors = totalVendors - activeVendors;
                    int totalItems = vendors.Sum(v => v.ItemCount);

                    data.Kpis.Add(new DashboardKpi { Title = "Total Vendors", Value = totalVendors, AccentColor = Color.FromArgb(52, 152, 219), Icon = "\U0001F3EA" });
                    data.Kpis.Add(new DashboardKpi { Title = "Active", Value = activeVendors, AccentColor = Color.FromArgb(46, 204, 113), Icon = "\u2705" });
                    data.Kpis.Add(new DashboardKpi { Title = "Inactive", Value = inactiveVendors, AccentColor = Color.FromArgb(231, 76, 60), Icon = "\u26A0\uFE0F" });
                    data.Kpis.Add(new DashboardKpi { Title = "Items", Value = totalItems, AccentColor = Color.FromArgb(155, 89, 182), Icon = "\U0001F4E6" });
                    // Charts
                    // 1) Vendors Distribution (Vendor Names) using ItemCount
                    var topByItems = vendors
                        .OrderByDescending(v => v.ItemCount)
                        .ThenBy(v => v.Name)
                        .Take(6)
                        .ToList();
                    data.PrimaryChartMetrics = topByItems
                        .Select(v => new DashboardMetric { Label = v.Name, Value = v.ItemCount })
                        .ToList();

                    // 2) Top Vendors (Invoices)
                    var topByInvoices = vendors
                        .Select(v => new { v.Name, InvoiceCount = invoiceCounts.ContainsKey(v.VendorId) ? invoiceCounts[v.VendorId] : 0 })
                        .OrderByDescending(v => v.InvoiceCount)
                        .ThenBy(v => v.Name)
                        .Take(8)
                        .ToList();
                    data.SecondaryChartMetrics = topByInvoices
                        .Select(v => new DashboardMetric { Label = v.Name, Value = v.InvoiceCount })
                        .ToList();

                    // 3) Active vs Inactive
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Active", Value = activeVendors });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Inactive", Value = inactiveVendors });

                    // No Quick Stats (2nd row removed)

                    // Table
                    foreach (var v in vendors)
                    {
                        int invCount = invoiceCounts.ContainsKey(v.VendorId) ? invoiceCounts[v.VendorId] : 0;
                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = v.Name,
                            Col2 = v.ItemCount.ToString() + " items",
                            Col3 = invCount.ToString() + " invoices",
                            StatusBadge = v.Active ? "\u2705 Active" : "\u26A0 Inactive",
                            StatusColor = v.Active ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vendors dashboard error: {ex}");
            }

            return data;
        }

        // ===== SCOPE: Archived Entities =====
        private DashboardData GetArchivedEntitiesDashboardData(DashboardFilter filter)
        {
            var data = new DashboardData
            {
                Scope = DashboardScope.ArchivedEntities,
                Title = "\U0001F5C4\uFE0F Archived Entities Dashboard",
                PrimaryChartTitle = "ARCHIVED BY TYPE",
                SecondaryChartTitle = "ARCHIVE TIMELINE",
                TertiaryChartTitle = "ENTITY DISTRIBUTION",
                TableTitle = "\U0001F4CB ARCHIVED ENTITIES"
            };

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Use vw_AllArchivedEntities view which includes EntityName computed column
                    string sql = @"
                        SELECT
                            EntityType,
                            EntityId,
                            EntityName,
                            ArchivedBy,
                            ArchivedAt,
                            ArchiveReason
                        FROM dbo.vw_AllArchivedEntities
                        ORDER BY ArchivedAt DESC";

                    var archived = new List<(string Type, int Id, string EntityName, string ArchivedBy, DateTime Date, string Reason)>();

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            archived.Add((
                                reader.GetString(0),
                                reader.GetInt32(1),
                                reader.IsDBNull(2) ? "" : reader.GetString(2),
                                reader.IsDBNull(3) ? "" : reader.GetString(3),
                                reader.GetDateTime(4),
                                reader.IsDBNull(5) ? "" : reader.GetString(5)
                            ));
                        }
                    }

                    // CENTRALIZED: Group all archived entities by type (single source of truth)
                    // Includes: Item, Employee, Company, Branch, Set, Vendor, Department, Renewal, Request, Inventory
                    var byType = archived
                        .GroupBy(a => a.Type)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    // KPIs - dynamically generated from centralized grouping
                    int totalArchived = archived.Count;

                    // Add Total KPI first
                    data.Kpis.Add(new DashboardKpi
                    {
                        Title = "Total Archived",
                        Value = totalArchived,
                        AccentColor = Color.FromArgb(52, 152, 219),
                        Icon = "\U0001F5C4\uFE0F"
                    });

                    // Add top 4 entity types as KPIs (or all if less than 4)
                    var topTypes = byType.Take(4).ToList();
                    var entityIcons = new Dictionary<string, string>
                    {
                        { "Item", "\U0001F4E6" },
                        { "Employee", "\U0001F465" },
                        { "Company", "\U0001F3ED" },
                        { "Branch", "\U0001F3EA" },
                        { "Set", "\U0001F4CB" },
                        { "Vendor", "\U0001F3EA" },
                        { "Department", "\U0001F3E2" },
                        { "Renewal", "\U0001F504" },
                        { "Request", "\U0001F4DD" },
                        { "Inventory", "\U0001F4CA" }
                    };

                    var entityColors = new Color[]
                    {
                        Color.FromArgb(231, 76, 60),   // Red
                        Color.FromArgb(155, 89, 182),  // Purple
                        Color.FromArgb(230, 126, 34),  // Orange
                        Color.FromArgb(52, 73, 94)     // Dark blue
                    };

                    for (int i = 0; i < topTypes.Count; i++)
                    {
                        var type = topTypes[i];
                        data.Kpis.Add(new DashboardKpi
                        {
                            Title = type.Type + "s",
                            Value = type.Count,
                            AccentColor = entityColors[i % entityColors.Length],
                            Icon = entityIcons.ContainsKey(type.Type) ? entityIcons[type.Type] : "\U0001F4C1"
                        });
                    }

                    // Charts - all use centralized byType grouping
                    data.PrimaryChartMetrics = byType.Select(x => new DashboardMetric { Label = x.Type, Value = x.Count }).ToList();
                    data.SecondaryChartMetrics = byType.Select(x => new DashboardMetric { Label = x.Type, Value = x.Count }).ToList();

                    // Tertiary chart (Entity Distribution) - show only "Items" vs "Others"
                    int itemsCount = byType.FirstOrDefault(x => x.Type == "Item")?.Count ?? 0;
                    int othersCount = totalArchived - itemsCount;
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Items", Value = itemsCount });
                    data.TertiaryChartMetrics.Add(new DashboardMetric { Label = "Others", Value = othersCount });

                    // Quick stats
                    int currentMonth = DateTime.Now.Month;
                    int currentYear = DateTime.Now.Year;
                    int thisMonth = archived.Count(a => a.Date.Year == currentYear && a.Date.Month == currentMonth);
                    int thisYear = archived.Count(a => a.Date.Year == currentYear);
                    int pastMonths = totalArchived - thisMonth; // All records excluding current month

                    data.Stat1 = new DashboardQuickStat { Label = "This Month", Value = thisMonth.ToString(), Color = Color.FromArgb(231, 76, 60) };
                    data.Stat2 = new DashboardQuickStat { Label = "This Year", Value = thisYear.ToString(), Color = Color.FromArgb(241, 196, 15) };
                    data.Stat3 = new DashboardQuickStat { Label = "Past Months", Value = pastMonths.ToString(), Color = Color.FromArgb(52, 152, 219) };

                    // Table
                    foreach (var arc in archived.Take(20))
                    {
                        data.TableData.Add(new DashboardTableRow
                        {
                            Col1 = arc.Type,
                            Col2 = arc.EntityName,
                            Col3 = "ID: " + arc.Id.ToString(),
                            Col4 = arc.Date.ToString("yyyy-MM-dd"),
                            Col5 = arc.ArchivedBy,
                            Col6 = arc.Reason,
                            StatusBadge = "Archived",
                            StatusColor = Color.FromArgb(231, 76, 60)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Archived entities dashboard error: {ex}");
            }

            return data;
        }

        /// <summary>
        /// Get filtered drill-down data based on user interaction (chart clicks, card clicks)
        /// Returns a DataTable with Item records matching the specified filters
        /// CRITICAL: Always excludes archived items (IsArchived = 0)
        /// </summary>
        public System.Data.DataTable GetDrillDownItems(DashboardDrillDownRequest request)
        {
            var dt = new System.Data.DataTable();

            // Define result table schema
            dt.Columns.Add("ItemId", typeof(int));
            dt.Columns.Add("Item Name", typeof(string));
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("Item Type", typeof(string));
            dt.Columns.Add("Stock On Hand", typeof(int));
            dt.Columns.Add("Condition", typeof(string));
            dt.Columns.Add("Status", typeof(string));

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Base query - fetch all items with their details
                    // CRITICAL: Archive filter enforced at database level
                    string sql = $@"
                        SELECT
                            i.ItemId,
                            i.Name AS ItemName,
                            ic.Name AS CategoryName,
                            i.ItemType,
                            i.StockOnHand,
                            CASE
                                WHEN i.Condition = 'Good' THEN 'Good'
                                WHEN i.Condition = 'Damaged' THEN 'Damaged'
                                ELSE i.Condition
                            END AS Condition,
                            CASE
                                WHEN i.Active = 1 THEN 'Active'
                                ELSE 'Inactive'
                            END AS Status
                        FROM dbo.Item i
                        INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                        WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                          AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}";

                    // Apply filters based on request type
                    var whereClauses = new List<string>();

                    // Filter by Category
                    if (!string.IsNullOrEmpty(request.CategoryName))
                    {
                        whereClauses.Add("ic.Name = @CategoryName");
                    }

                    // Filter by Condition
                    if (!string.IsNullOrEmpty(request.Condition))
                    {
                        whereClauses.Add("i.Condition = @Condition");
                    }

                    // Filter by Stock Status
                    if (!string.IsNullOrEmpty(request.StockStatus))
                    {
                        switch (request.StockStatus.ToLower())
                        {
                            case "out":
                                whereClauses.Add("i.StockOnHand <= 0");
                                break;
                            case "low":
                                whereClauses.Add("i.StockOnHand > 0 AND i.StockOnHand < 5");
                                break;
                            case "in":
                                whereClauses.Add("i.StockOnHand >= 5");
                                break;
                        }
                    }

                    // Append dynamic WHERE clauses
                    if (whereClauses.Count > 0)
                    {
                        sql += " AND " + string.Join(" AND ", whereClauses);
                    }

                    sql += " ORDER BY i.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        // Add parameters
                        if (!string.IsNullOrEmpty(request.CategoryName))
                        {
                            cmd.Parameters.AddWithValue("@CategoryName", request.CategoryName);
                        }

                        if (!string.IsNullOrEmpty(request.Condition))
                        {
                            cmd.Parameters.AddWithValue("@Condition", request.Condition);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = dt.NewRow();
                                row["ItemId"] = reader.GetInt32(0);
                                row["Item Name"] = reader.GetString(1);
                                row["Category"] = reader.GetString(2);
                                row["Item Type"] = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                row["Stock On Hand"] = reader.GetInt32(4);
                                row["Condition"] = reader.IsDBNull(5) ? "N/A" : reader.GetString(5);
                                row["Status"] = reader.GetString(6);
                                dt.Rows.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDrillDownItems error: {ex}");
            }

            return dt;
        }

        /// <summary>
        /// Get filtered movement drill-down data for Inventory Movements dashboard
        /// Returns a DataTable with movement records matching the specified filters
        /// CRITICAL: Always excludes Fixed Assets (EntryType NOT IN 'Fixed Asset', 'Fixed Assets')
        /// </summary>
        public System.Data.DataTable GetMovementDrillDownData(MovementDrillDownRequest request)
        {
            var dt = new System.Data.DataTable();

            // Define result table schema
            dt.Columns.Add("InvId", typeof(int));
            dt.Columns.Add("Item Code", typeof(string));
            dt.Columns.Add("Item Name", typeof(string));
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("Entry Type", typeof(string));
            dt.Columns.Add("Quantity", typeof(int));
            dt.Columns.Add("Date Posted", typeof(string));

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Base query - fetch all movements with item details
                    // CRITICAL: Fixed Assets are excluded (EntryType NOT IN 'Fixed Asset', 'Fixed Assets')
                    string sql = $@"
                        SELECT
                            inv.InvId,
                            COALESCE(NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''), NULLIF(LTRIM(RTRIM(i.SerialNumber)), ''), CAST(i.ItemId AS nvarchar(50))) AS ItemCode,
                            i.Name AS ItemName,
                            ic.Name AS CategoryName,
                            inv.EntryType,
                            CASE
                                WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                                WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                                ELSE inv.Quantity
                            END AS SignedQuantity,
                            inv.DatePosted
                        FROM dbo.Inventory inv
                        LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
                        INNER JOIN dbo.Item i ON i.ItemId = COALESCE(inv.ItemId, r.ItemId)
                        INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                        WHERE inv.Active = 1
                          AND {ArchiveFilter(con, "Inventory", "inv.InvId")}
                          AND {ArchiveFilter(con, "Item", "i.ItemId")}
                          AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                          AND (inv.ReqId IS NULL OR {ArchiveFilter(con, "Request", "inv.ReqId")})
                          AND inv.EntryType NOT IN ('Fixed Asset', 'Fixed Assets')";

                    // Apply filters based on drill-down type
                    var whereClauses = new List<string>();

                    switch (request.DrillType)
                    {
                        case MovementDrillDownType.Category:
                            if (!string.IsNullOrEmpty(request.CategoryName))
                            {
                                whereClauses.Add("ic.Name = @CategoryName");
                            }
                            break;

                        case MovementDrillDownType.QtyIn:
                        case MovementDrillDownType.Positive:
                            whereClauses.Add("inv.EntryType = 'Positive'");
                            break;

                        case MovementDrillDownType.QtyOut:
                        case MovementDrillDownType.Negative:
                            whereClauses.Add("inv.EntryType = 'Negative'");
                            break;

                        case MovementDrillDownType.AllMovements:
                            // No additional filter - show all movements (Fixed Assets already excluded)
                            break;
                    }

                    // Append dynamic WHERE clauses
                    if (whereClauses.Count > 0)
                    {
                        sql += " AND " + string.Join(" AND ", whereClauses);
                    }

                    sql += " ORDER BY inv.DatePosted DESC";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        // Add parameters
                        if (!string.IsNullOrEmpty(request.CategoryName))
                        {
                            cmd.Parameters.AddWithValue("@CategoryName", request.CategoryName);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = dt.NewRow();
                                row["InvId"] = reader.GetInt32(0);
                                row["Item Code"] = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                row["Item Name"] = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                row["Category"] = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                row["Entry Type"] = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                row["Quantity"] = reader.GetInt32(5);
                                row["Date Posted"] = reader.GetDateTime(6).ToString("yyyy-MM-dd");
                                dt.Rows.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMovementDrillDownData error: {ex}");
            }

            return dt;
        }

        /// <summary>
        /// Get drill-down data as a strongly-typed DTO list (NEW APPROACH)
        /// Returns object to allow different DTO types based on context
        /// The calling code will cast to appropriate type for DataGridView binding
        /// </summary>
        public object GetDrillDownDataAsDto(GenericDrillDownRequest request)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    switch (request.Context)
                    {
                        case LegacyDrillDownContext.ItemsByCategory:
                            return GetItemsByCategory(con, request.FilterValue);

                        case LegacyDrillDownContext.ItemsByStockLevel:
                            return GetItemsByStockLevel(con, request.FilterValue);

                        case LegacyDrillDownContext.ItemsByCondition:
                            return GetItemsByCondition(con, request.FilterValue);

                        case LegacyDrillDownContext.AllItems:
                            return GetAllItems(con);

                        case LegacyDrillDownContext.MovementsByCategory:
                            return GetMovementsByCategory(con, request.FilterValue);

                        case LegacyDrillDownContext.MovementsByEntryType:
                            return GetMovementsByEntryType(con, request.FilterValue);

                        case LegacyDrillDownContext.AllMovements:
                            return GetAllMovements(con);

                        default:
                            return new List<object>(); // Empty list for unsupported contexts
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDrillDownDataAsDto error: {ex}");
                throw;
            }
        }

        // ===== ITEM DRILL-DOWN METHODS (return DTOs) =====

        private List<Pages.ItemDto> GetItemsByCategory(SqlConnection con, string categoryName)
        {
            var items = new List<Pages.ItemDto>();

            string sql = $@"
                SELECT
                    i.ItemId,
                    i.Name,
                    ic.Name AS CategoryName,
                    i.ItemType,
                    i.StockOnHand,
                    ISNULL(cond.Name, 'Unknown') AS ConditionName,
                    CASE WHEN i.Active = 1 THEN 'Active' ELSE 'Inactive' END AS Status,
                    i.SerialNumber,
                    i.ModelNumber
                FROM dbo.Item i
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                LEFT JOIN dbo.ItemCondition cond ON i.ConditionID = cond.ConditionID
                WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                  AND ic.Name = @CategoryName
                ORDER BY i.Name";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CategoryName", categoryName ?? "");

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new Pages.ItemDto
                        {
                            ItemId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Category = reader.GetString(2),
                            ItemType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            StockOnHand = reader.GetInt32(4),
                            ConditionName = reader.GetString(5),
                            Active = reader.GetString(6) == "Active",
                            SerialNumber = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            ModelNumber = reader.IsDBNull(8) ? "" : reader.GetString(8)
                        });
                    }
                }
            }

            return items;
        }

        private List<Pages.ItemDto> GetItemsByStockLevel(SqlConnection con, string stockStatus)
        {
            var items = new List<Pages.ItemDto>();

            string stockFilter = "";
            switch (stockStatus?.ToLower())
            {
                case "out":
                    stockFilter = "i.StockOnHand <= 0";
                    break;
                case "low":
                    stockFilter = "i.StockOnHand > 0 AND i.StockOnHand < 5";
                    break;
                case "in":
                    stockFilter = "i.StockOnHand >= 5";
                    break;
                default:
                    stockFilter = "1=1";
                    break;
            }

            string sql = $@"
                SELECT
                    i.ItemId,
                    i.Name,
                    ic.Name AS CategoryName,
                    i.ItemType,
                    i.StockOnHand,
                    ISNULL(cond.Name, 'Unknown') AS ConditionName,
                    CASE WHEN i.Active = 1 THEN 'Active' ELSE 'Inactive' END AS Status
                FROM dbo.Item i
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                LEFT JOIN dbo.ItemCondition cond ON i.ConditionID = cond.ConditionID
                WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                  AND {stockFilter}
                ORDER BY i.StockOnHand, i.Name";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    items.Add(new Pages.ItemDto
                    {
                        ItemId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Category = reader.GetString(2),
                        ItemType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        StockOnHand = reader.GetInt32(4),
                        ConditionName = reader.GetString(5),
                        Active = reader.GetString(6) == "Active"
                    });
                }
            }

            return items;
        }

        private List<Pages.ItemDto> GetItemsByCondition(SqlConnection con, string condition)
        {
            var items = new List<Pages.ItemDto>();

            string sql = $@"
                SELECT
                    i.ItemId,
                    i.Name,
                    ic.Name AS CategoryName,
                    i.ItemType,
                    i.StockOnHand,
                    ISNULL(cond.Name, 'Unknown') AS ConditionName,
                    CASE WHEN i.Active = 1 THEN 'Active' ELSE 'Inactive' END AS Status
                FROM dbo.Item i
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                LEFT JOIN dbo.ItemCondition cond ON i.ConditionID = cond.ConditionID
                WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                  AND cond.Name = @Condition
                ORDER BY i.Name";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Condition", condition ?? "");

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new Pages.ItemDto
                        {
                            ItemId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Category = reader.GetString(2),
                            ItemType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            StockOnHand = reader.GetInt32(4),
                            ConditionName = reader.GetString(5),
                            Active = reader.GetString(6) == "Active"
                        });
                    }
                }
            }

            return items;
        }

        private List<Pages.ItemDto> GetAllItems(SqlConnection con)
        {
            var items = new List<Pages.ItemDto>();

            string sql = $@"
                SELECT
                    i.ItemId,
                    i.Name,
                    ic.Name AS CategoryName,
                    i.ItemType,
                    i.StockOnHand,
                    ISNULL(cond.Name, 'Unknown') AS ConditionName,
                    CASE WHEN i.Active = 1 THEN 'Active' ELSE 'Inactive' END AS Status
                FROM dbo.Item i
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                LEFT JOIN dbo.ItemCondition cond ON i.ConditionID = cond.ConditionID
                WHERE {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                ORDER BY i.Name";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    items.Add(new Pages.ItemDto
                    {
                        ItemId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Category = reader.GetString(2),
                        ItemType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        StockOnHand = reader.GetInt32(4),
                        ConditionName = reader.GetString(5),
                        Active = reader.GetString(6) == "Active"
                    });
                }
            }

            return items;
        }

        // ===== MOVEMENT DRILL-DOWN METHODS (return DTOs) =====

        private class MovementDto
        {
            public int InvId { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string Category { get; set; }
            public string EntryType { get; set; }
            public int Quantity { get; set; }
            public string DatePosted { get; set; }
        }

        private List<MovementDto> GetMovementsByCategory(SqlConnection con, string categoryName)
        {
            var movements = new List<MovementDto>();

            string sql = $@"
                SELECT
                    inv.InvId,
                    COALESCE(NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''), NULLIF(LTRIM(RTRIM(i.SerialNumber)), ''), CAST(i.ItemId AS nvarchar(50))) AS ItemCode,
                    ISNULL(i.Name, '') AS ItemName,
                    ISNULL(ic.Name, '') AS CategoryName,
                    inv.EntryType,
                    CASE
                        WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                        WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                        ELSE inv.Quantity
                    END AS SignedQuantity,
                    inv.DatePosted
                FROM dbo.Inventory inv
                LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
                INNER JOIN dbo.Item i ON i.ItemId = COALESCE(inv.ItemId, r.ItemId)
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                WHERE inv.Active = 1
                  AND {ArchiveFilter(con, "Inventory", "inv.InvId")}
                  AND {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                  AND (inv.ReqId IS NULL OR {ArchiveFilter(con, "Request", "inv.ReqId")})
                  AND inv.EntryType NOT IN ('Fixed Asset', 'Fixed Assets')
                  AND LTRIM(RTRIM(ic.Name)) = @CategoryNameTrim
                ORDER BY inv.DatePosted DESC";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CategoryNameTrim", (categoryName ?? string.Empty).Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        movements.Add(new MovementDto
                        {
                            InvId = reader.GetInt32(0),
                            ItemCode = reader.GetString(1),
                            ItemName = reader.GetString(2),
                            Category = reader.GetString(3),
                            EntryType = reader.GetString(4),
                            Quantity = reader.GetInt32(5),
                            DatePosted = reader.GetDateTime(6).ToString("yyyy-MM-dd")
                        });
                    }
                }
            }

            return movements;
        }

        private List<MovementDto> GetMovementsByEntryType(SqlConnection con, string entryType)
        {
            var movements = new List<MovementDto>();

            string sql = $@"
                SELECT
                    inv.InvId,
                    COALESCE(NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''), NULLIF(LTRIM(RTRIM(i.SerialNumber)), ''), CAST(i.ItemId AS nvarchar(50))) AS ItemCode,
                    ISNULL(i.Name, '') AS ItemName,
                    ISNULL(ic.Name, '') AS CategoryName,
                    inv.EntryType,
                    CASE
                        WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                        WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                        ELSE inv.Quantity
                    END AS SignedQuantity,
                    inv.DatePosted
                FROM dbo.Inventory inv
                LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
                INNER JOIN dbo.Item i ON i.ItemId = COALESCE(inv.ItemId, r.ItemId)
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                WHERE inv.Active = 1
                  AND {ArchiveFilter(con, "Inventory", "inv.InvId")}
                  AND {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                  AND (inv.ReqId IS NULL OR {ArchiveFilter(con, "Request", "inv.ReqId")})
                  AND inv.EntryType NOT IN ('Fixed Asset', 'Fixed Assets')
                  AND LTRIM(RTRIM(inv.EntryType)) = @EntryTypeTrim
                ORDER BY inv.DatePosted DESC";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EntryTypeTrim", (entryType ?? string.Empty).Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        movements.Add(new MovementDto
                        {
                            InvId = reader.GetInt32(0),
                            ItemCode = reader.GetString(1),
                            ItemName = reader.GetString(2),
                            Category = reader.GetString(3),
                            EntryType = reader.GetString(4),
                            Quantity = reader.GetInt32(5),
                            DatePosted = reader.GetDateTime(6).ToString("yyyy-MM-dd")
                        });
                    }
                }
            }

            return movements;
        }

        private List<MovementDto> GetAllMovements(SqlConnection con)
        {
            var movements = new List<MovementDto>();

            string sql = $@"
                SELECT
                    inv.InvId,
                    COALESCE(NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''), NULLIF(LTRIM(RTRIM(i.SerialNumber)), ''), CAST(i.ItemId AS nvarchar(50))) AS ItemCode,
                    ISNULL(i.Name, '') AS ItemName,
                    ISNULL(ic.Name, '') AS CategoryName,
                    inv.EntryType,
                    CASE
                        WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                        WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                        ELSE inv.Quantity
                    END AS SignedQuantity,
                    inv.DatePosted
                FROM dbo.Inventory inv
                LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
                INNER JOIN dbo.Item i ON i.ItemId = COALESCE(inv.ItemId, r.ItemId)
                INNER JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                WHERE inv.Active = 1
                  AND {ArchiveFilter(con, "Inventory", "inv.InvId")}
                  AND {ArchiveFilter(con, "Item", "i.ItemId")}
                  AND {ArchiveFilter(con, "ItemCategory", "ic.CategoryId")}
                  AND (inv.ReqId IS NULL OR {ArchiveFilter(con, "Request", "inv.ReqId")})
                  AND inv.EntryType NOT IN ('Fixed Asset', 'Fixed Assets')
                ORDER BY inv.DatePosted DESC";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    movements.Add(new MovementDto
                    {
                        InvId = reader.GetInt32(0),
                        ItemCode = reader.GetString(1),
                        ItemName = reader.GetString(2),
                        Category = reader.GetString(3),
                        EntryType = reader.GetString(4),
                        Quantity = reader.GetInt32(5),
                        DatePosted = reader.GetDateTime(6).ToString("yyyy-MM-dd")
                    });
                }
            }

            return movements;
        }
    }
}
