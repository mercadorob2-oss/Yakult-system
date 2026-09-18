using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    public class DisposedSoldReportRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Returns all Executed dispose/sell decisions, optionally filtered
        /// by date range, decision type, and/or category.
        /// </summary>
        /// <param name="from">Inclusive start date (UTC). Null = no lower bound.</param>
        /// <param name="to">Inclusive end date (UTC). Null = no upper bound.</param>
        /// <param name="decisionType">
        ///   "DISPOSE", "SELL", or null/empty for both.
        /// </param>
        /// <param name="categoryId">Filter by ItemCategory. Null = all categories.</param>
        public List<DisposedSoldItemDto> GetReport(
            DateTime? from          = null,
            DateTime? to            = null,
            string    decisionType  = null,
            int?      categoryId    = null)
        {
            const string sql =@"
                SELECT
                    DecisionId, BatchId, DecidedAt, DecisionTypeName, Quantity,
                    RecipientName, SaleAmount, Remarks,
                    ItemId, ItemName, ItemModelNumber, SerialNumber,
                    CategoryId, CategoryName,
                    ConditionName,
                    DecidedByName,
                    EmptyCartridgeId, CartridgeModelId,
                    CartridgeModelNumber, CartridgeBrand,
                    DisposalCompanyName, VendorName, CartridgeReturnedAt
                FROM dbo.vw_DisposedSoldItems
                WHERE (@From           IS NULL OR DecidedAt        >= @From)
                  AND (@To             IS NULL OR DecidedAt        <= @To)
                  AND (@DecisionType   IS NULL OR DecisionTypeName  = @DecisionType)
                  AND (@CategoryId     IS NULL OR CategoryId        = @CategoryId)
                ORDER BY DecidedAt DESC";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                return conn.Query<DisposedSoldItemDto>(sql, new
                {
                    From         = from.HasValue      ? (object)from.Value              : null,
                    To           = to.HasValue        ? (object)to.Value.Date.AddDays(1).AddTicks(-1) : null,
                    DecisionType = string.IsNullOrWhiteSpace(decisionType) ? null : decisionType.Trim().ToUpper(),
                    CategoryId   = categoryId.HasValue ? (object)categoryId.Value        : null
                }).AsList();
            }
        }

        /// <summary>
        /// Returns a single decision record by DecisionId.
        /// Returns null if not found.
        /// </summary>
        public DisposedSoldItemDto GetById(int decisionId)
        {
            const string sql = @"
                SELECT
                    DecisionId, DecidedAt, DecisionTypeName, Quantity,
                    RecipientName, SaleAmount, Remarks,
                    ItemId, ItemName, ItemModelNumber, SerialNumber,
                    CategoryId, CategoryName,
                    ConditionName,
                    DecidedByName,
                    EmptyCartridgeId, CartridgeModelId,
                    CartridgeModelNumber, CartridgeBrand,
                    DisposalCompanyName, CartridgeReturnedAt
                FROM dbo.vw_DisposedSoldItems
                WHERE DecisionId = @DecisionId";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                return conn.QueryFirstOrDefault<DisposedSoldItemDto>(sql, new { DecisionId = decisionId });
            }
        }

        /// <summary>
        /// Summary counts and totals grouped by decision type, for dashboard cards.
        /// </summary>
        public DisposedSoldSummary GetSummary(DateTime? from = null, DateTime? to = null)
        {
            const string sql = @"
                SELECT
                    COUNT(*)                                         AS TotalCount,
                    SUM(CASE WHEN DecisionTypeName = 'DISPOSE' THEN 1 ELSE 0 END) AS DisposedCount,
                    SUM(CASE WHEN DecisionTypeName = 'SELL'    THEN 1 ELSE 0 END) AS SoldCount,
                    ISNULL(SUM(CASE WHEN DecisionTypeName = 'SELL' THEN SaleAmount ELSE 0 END), 0) AS TotalSaleAmount
                FROM dbo.vw_DisposedSoldItems
                WHERE (@From IS NULL OR DecidedAt >= @From)
                  AND (@To   IS NULL OR DecidedAt <= @To)";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                return conn.QueryFirstOrDefault<DisposedSoldSummary>(sql, new
                {
                    From = from.HasValue ? (object)from.Value                              : null,
                    To   = to.HasValue   ? (object)to.Value.Date.AddDays(1).AddTicks(-1)  : null
                }) ?? new DisposedSoldSummary();
            }
        }
    }

    public class DisposedSoldSummary
    {
        public int     TotalCount      { get; set; }
        public int     DisposedCount   { get; set; }
        public int     SoldCount       { get; set; }
        public decimal TotalSaleAmount { get; set; }
    }
}
