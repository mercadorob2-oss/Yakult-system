using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Forms.Dashboard
{
    /// <summary>
    /// Dashboard analytics scope with data source mappings
    /// </summary>
    public enum DashboardScope
    {
        /// <summary>Categories - Source: vw_CategoryCombinedSummary</summary>
        Categories,

        /// <summary>Items - Source: Item + vw_CategoryCombinedSummary</summary>
        Items,

        /// <summary>Inventory Movements - Source: Inventory (EntryType Positive/Negative)</summary>
        InventoryMovements,

        /// <summary>Requests - Source: Request (join Item, Employee; Active only)</summary>
        Requests,

        /// <summary>Renewals - Source: Item + Renewals (WHERE IsArchived = 0 - archived renewals NEVER appear)</summary>
        Renewals,

        /// <summary>Invoices/Services - Source: vw_InvoiceItems</summary>
        InvoicesServices,

        /// <summary>Employees - Source: Employee (join Branch, Dept, Company; Active only)</summary>
        Employees,

        /// <summary>Vendors - Source: Vendor + VendorItemCategory</summary>
        Vendors,

        /// <summary>Archived Entities - Source: vw_AllArchivedEntities</summary>
        ArchivedEntities
    }

    /// <summary>
    /// Represents a single metric for dashboard visualization
    /// </summary>
    public class DashboardMetric
    {
        public string Label { get; set; }
        public int Value { get; set; }
        public int CategoryId { get; set; }  // For category-specific metrics
    }

    /// <summary>
    /// Represents a Key Performance Indicator (KPI) card
    /// </summary>
    public class DashboardKpi
    {
        public string Title { get; set; }
        public int Value { get; set; }
        public System.Drawing.Color AccentColor { get; set; }
        public string Icon { get; set; }
    }

    /// <summary>
    /// Dashboard filter for date range, status, category, etc.
    /// </summary>
    public class DashboardFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
        public int? CategoryId { get; set; }
        public bool IncludeArchived { get; set; }

        public DashboardFilter()
        {
            IncludeArchived = false;
        }
    }

    /// <summary>
    /// Category-specific dashboard data
    /// </summary>
    public class CategoryDashboardData
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int ActiveStock { get; set; }
        public int RequestedItems { get; set; }
        public int GoodCount { get; set; }
        public int DamagedCount { get; set; }
        public int SerializedItems { get; set; }
        public int TotalItems { get; set; }
    }

    /// <summary>
    /// Complete dashboard data package
    /// </summary>
    public class DashboardData
    {
        public DashboardScope Scope { get; set; }
        public string Title { get; set; }
        public List<DashboardKpi> Kpis { get; set; }
        public List<DashboardMetric> PrimaryChartMetrics { get; set; }  // Pie chart
        public List<DashboardMetric> SecondaryChartMetrics { get; set; } // Bar chart
        public List<DashboardMetric> TertiaryChartMetrics { get; set; }  // Donut chart (2 values)
        public List<DashboardTableRow> TableData { get; set; }

        // Chart titles (dynamic per scope)
        public string PrimaryChartTitle { get; set; }
        public string SecondaryChartTitle { get; set; }
        public string TertiaryChartTitle { get; set; }
        public string TableTitle { get; set; }

        // Quick stats (3 badges below KPIs)
        public DashboardQuickStat Stat1 { get; set; }
        public DashboardQuickStat Stat2 { get; set; }
        public DashboardQuickStat Stat3 { get; set; }

        // Legacy properties for backward compatibility
        public List<DashboardMetric> CategoryDistribution => PrimaryChartMetrics;
        public List<DashboardMetric> StockLevels => SecondaryChartMetrics;
        public List<CategoryDashboardData> CategorySummaries { get; set; }
        public int TotalGood { get; set; }
        public int TotalDamaged { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int InStockCount { get; set; }

        public DashboardData()
        {
            Kpis = new List<DashboardKpi>();
            PrimaryChartMetrics = new List<DashboardMetric>();
            SecondaryChartMetrics = new List<DashboardMetric>();
            TertiaryChartMetrics = new List<DashboardMetric>();
            TableData = new List<DashboardTableRow>();
            CategorySummaries = new List<CategoryDashboardData>();
        }
    }

    /// <summary>
    /// Quick stat badge (colored badge with label + value)
    /// </summary>
    public class DashboardQuickStat
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public System.Drawing.Color Color { get; set; }
    }

    /// <summary>
    /// Generic table row for dashboard tables (supports up to 7 columns)
    /// </summary>
    public class DashboardTableRow
    {
        public string Col1 { get; set; }
        public string Col2 { get; set; }
        public string Col3 { get; set; }
        public string Col4 { get; set; }
        public string Col5 { get; set; }
        public string Col6 { get; set; }
        public string Col7 { get; set; }
        public string StatusBadge { get; set; }  // For status column
        public System.Drawing.Color StatusColor { get; set; }
        public System.Drawing.Color RowColor { get; set; }  // For highlighting
    }

    /// <summary>
    /// Drill-down filter type for dashboard analytics
    /// </summary>
    public enum DrillDownFilterType
    {
        /// <summary>Category-based filter (e.g., Mouse, Keyboard)</summary>
        Category,

        /// <summary>Stock level filter (Out of Stock, Low Stock, In Stock)</summary>
        StockLevel,

        /// <summary>Condition filter (Good, Damaged)</summary>
        Condition,

        /// <summary>Summary card filter (combines multiple criteria)</summary>
        SummaryCard
    }

    /// <summary>
    /// Request model for drill-down data queries
    /// All properties are nullable to support flexible filtering
    /// </summary>
    public class DashboardDrillDownRequest
    {
        /// <summary>Type of filter being applied</summary>
        public DrillDownFilterType FilterType { get; set; }

        /// <summary>Category name for category-based filtering (nullable)</summary>
        public string CategoryName { get; set; }

        /// <summary>Condition filter: "Good" or "Damaged" (nullable)</summary>
        public string Condition { get; set; }

        /// <summary>Stock status filter: "Out", "Low", or "In" (nullable)</summary>
        public string StockStatus { get; set; }

        /// <summary>Dashboard scope context for proper filtering</summary>
        public DashboardScope Scope { get; set; }

        /// <summary>Title to display in drill-down dialog</summary>
        public string DisplayTitle { get; set; }
    }

    /// <summary>
    /// Movement drill-down filter type for Inventory Movements dashboard
    /// </summary>
    public enum MovementDrillDownType
    {
        /// <summary>Filter by category name</summary>
        Category,

        /// <summary>Show all Qty In movements (Positive entry type)</summary>
        QtyIn,

        /// <summary>Show all Qty Out movements (Negative entry type)</summary>
        QtyOut,

        /// <summary>Show all Positive movements</summary>
        Positive,

        /// <summary>Show all Negative movements</summary>
        Negative,

        /// <summary>Show all Fixed Assets movements</summary>
        FixedAssets,

        /// <summary>Show all movements (no filter)</summary>
        AllMovements
    }

    /// <summary>
    /// Request model for movement-based drill-down queries
    /// CRITICAL: Fixed Assets are EXCLUDED from all movement analytics
    /// </summary>
    public class MovementDrillDownRequest
    {
        /// <summary>Type of movement filter being applied</summary>
        public MovementDrillDownType DrillType { get; set; }

        /// <summary>Category name for category-based filtering (nullable)</summary>
        public string CategoryName { get; set; }

        /// <summary>Title to display in drill-down dialog</summary>
        public string DisplayTitle { get; set; }
    }

    public enum LegacyDrillDownContext
    {
        ItemsByCategory,
        ItemsByStockLevel,
        ItemsByCondition,
        AllItems,

        MovementsByCategory,
        MovementsByEntryType,
        AllMovements,

        RequestsByStatus,
        RequestsByCategory,
        AllRequests,

        EmployeesByDepartment,
        EmployeesByCompany,
        EmployeesByBranch,
        AllEmployees,

        VendorsByStatus,
        AllVendors,

        RenewalsByStatus,
        ExpiringSoon,
        AllRenewals,

        InvoicesByType,
        InvoicesByStatus,
        AllInvoices,

        ArchivedByType,
        AllArchived
    }

    public enum DrillDownContext
    {
        Categories,
        Items,
        Movements,
        Requests,
        Renewals,
        Invoices,
        Employees,
        Vendors,
        Archived
    }

    public class DrillDownRequest
    {
        public DrillDownContext Context { get; set; }
        public string FilterKey { get; set; }
    }

    public class SimpleDrillDownDto
    {
        public string ItemName { get; set; }
        public string SourcePage { get; set; }
    }

    /// <summary>
    /// Generic drill-down request that works for all dashboard types
    /// Uses DTO-based approach instead of manual DataTable construction
    /// </summary>
    public class GenericDrillDownRequest
    {
        /// <summary>Context determines what data to fetch</summary>
        public LegacyDrillDownContext Context { get; set; }

        /// <summary>Filter value (e.g., category name, status, etc.)</summary>
        public string FilterValue { get; set; }

        /// <summary>Secondary filter (e.g., for complex filtering)</summary>
        public string SecondaryFilter { get; set; }

        /// <summary>Dashboard scope for context-aware filtering</summary>
        public DashboardScope Scope { get; set; }

        /// <summary>Title to display in drill-down dialog</summary>
        public string DisplayTitle { get; set; }
    }
}
