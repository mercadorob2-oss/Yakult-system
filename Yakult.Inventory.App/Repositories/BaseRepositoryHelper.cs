using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Base repository helper providing shared ordering and query utilities
    /// for consistent behavior across all repositories
    /// </summary>
    public static class BaseRepositoryHelper
    {
        /// <summary>
        /// Standard ordering pattern for GetAll methods:
        /// ORDER BY [DateCreated/CreatedDate/CreatedAt] DESC, [PrimaryKey] DESC
        ///
        /// This ensures newly added items always appear at the top of grids in ViewPages,
        /// providing immediate visual feedback to users when they add new records.
        /// </summary>
        /// <param name="dateColumn">Name of the date column (e.g., "DateCreated", "CreatedDate", "CreatedAt")</param>
        /// <param name="idColumn">Name of the primary key column (e.g., "CategoryId", "ComId", "VendorID")</param>
        /// <param name="tableAlias">Optional table alias (e.g., "c" for "c.DateCreated")</param>
        /// <returns>SQL ORDER BY clause</returns>
        public static string GetStandardOrderByClause(string dateColumn, string idColumn, string tableAlias = null)
        {
            string prefix = string.IsNullOrEmpty(tableAlias) ? "" : tableAlias + ".";
            return $"ORDER BY {prefix}{dateColumn} DESC, {prefix}{idColumn} DESC";
        }

        /// <summary>
        /// Gets the standard ordering clause for common entity types
        /// </summary>
        /// <param name="entityType">Entity type name (e.g., "Category", "Company", "Vendor")</param>
        /// <param name="tableAlias">Optional table alias</param>
        /// <returns>SQL ORDER BY clause</returns>
        public static string GetStandardOrderByClauseForEntity(string entityType, string tableAlias = null)
        {
            var mapping = new Dictionary<string, (string dateCol, string idCol)>
            {
                { "Category", ("DateCreated", "CategoryId") },
                { "Company", ("DateCreated", "ComId") },
                { "Vendor", ("CreatedDate", "VendorID") },
                { "Branch", ("DateCreated", "BranchId") },
                { "Department", ("DateCreated", "DeptId") },
                { "Employee", ("DateCreated", "EmpId") },
                { "Item", ("DateCreated", "ItemId") },
                { "Request", ("DateCreated", "ReqId") },
                { "Set", ("CreatedAt", "SetId") },
                { "Invoice", ("InvoiceDate", "InvoiceId") }
            };

            if (mapping.TryGetValue(entityType, out var columns))
            {
                return GetStandardOrderByClause(columns.dateCol, columns.idCol, tableAlias);
            }

            throw new ArgumentException($"Unknown entity type: {entityType}. Please use GetStandardOrderByClause directly.");
        }

        /// <summary>
        /// Orders a list in-memory by newest first (descending by date and ID)
        /// Use this when you need to apply ordering to data already loaded from the database
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="items">List to order</param>
        /// <param name="dateSelector">Function to select the date property</param>
        /// <param name="idSelector">Function to select the ID property</param>
        /// <returns>Ordered list</returns>
        public static IEnumerable<T> OrderByNewestFirst<T>(
            IEnumerable<T> items,
            Func<T, DateTime> dateSelector,
            Func<T, int> idSelector)
        {
            return items.OrderByDescending(dateSelector).ThenByDescending(idSelector);
        }

        /// <summary>
        /// IMPORTANT GUIDELINES FOR REPOSITORY DEVELOPERS:
        ///
        /// 1. All GetAll/GetAllAsync methods MUST order by newest first using:
        ///    ORDER BY [DateColumn] DESC, [PrimaryKey] DESC
        ///
        /// 2. Use GetStandardOrderByClause or GetStandardOrderByClauseForEntity to generate the clause
        ///
        /// 3. For lookup/dropdown methods (e.g., GetActiveItemsForDropdown),
        ///    ordering by Name/DisplayField is acceptable for better UX
        ///
        /// 4. Example usage in a repository:
        ///
        ///    string query = @"
        ///        SELECT * FROM dbo.ItemCategory c
        ///        WHERE c.Active = 1";
        ///
        ///    query += " " + BaseRepositoryHelper.GetStandardOrderByClauseForEntity("Category", "c");
        ///
        ///    // Produces: ORDER BY c.DateCreated DESC, c.CategoryId DESC
        ///
        /// 5. This ensures users immediately see newly added items at the top of grids,
        ///    providing clear visual confirmation of their actions.
        /// </summary>
        private const string DEVELOPER_GUIDELINES = @"
            REPOSITORY ORDERING STANDARD:
            =============================
            All GetAll methods must return newest records first.
            Use BaseRepositoryHelper methods for consistency.
            See class documentation for examples.
        ";
    }
}
