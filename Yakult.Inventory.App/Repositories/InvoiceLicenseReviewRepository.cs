using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Data access for the Invoice License Review page. Reads exclusively from the read-only
    /// dbo.vw_InvoiceNonLicensedItems view (see DATABASES\...\dbo\Views\vw_InvoiceNonLicensedItems.sql)
    /// and writes exclusively to dbo.Item.LicenseReviewStatus/ReviewedBy/ReviewedAt — never to the
    /// view itself, and never to any other invoice/Set/SetItem/Request column.
    /// </summary>
    public class InvoiceLicenseReviewRepository
    {
        public InvoiceLicenseReviewRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time,
            // so constructing this repository before the DB is configured doesn't crash.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public sealed class ReviewItemDto
        {
            public int ItemId { get; set; }
            public int SetId { get; set; }
            public int? ReqId { get; set; }
            public string InvoiceNumber { get; set; }
            public string PONumber { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public string Status { get; set; }
            public string Site { get; set; }
            public string CompanyName { get; set; }
            /// <summary>The whole invoice's total (dbo.[Set].TotalAmountDue) — not this line's
            /// amount. Same value repeats across every pending row that shares an InvoiceNumber.</summary>
            public decimal InvoiceTotalAmount { get; set; }
            public string Department { get; set; }
            public string Employee { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string ItemDescription { get; set; }
            public string ItemType { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public string CategoryName { get; set; }
            public string LegacyCategoryText { get; set; }
            public decimal Quantity { get; set; }
            public string Unit { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
            public string VendorName { get; set; }
            public string ConditionName { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public sealed class ReviewDecision
        {
            /// <summary>The underlying dbo.Item.ItemId being classified (NOT a SetItem/Request row —
            /// classification is per-item, so it applies to every invoice line for that item).</summary>
            public int ItemId { get; set; }

            /// <summary>Either "Licensed" or "NonLicensed" — matches the CK_Item_LicenseReviewStatus
            /// constraint in Migration_Item_AddLicenseReviewStatus.sql.</summary>
            public string Status { get; set; }
        }

        /// <summary>Loads the current review queue — every unreviewed Hardware invoice line,
        /// straight from dbo.vw_InvoiceNonLicensedItems. Read-only.</summary>
        public async Task<List<ReviewItemDto>> GetPendingReviewItemsAsync()
        {
            var results = new List<ReviewItemDto>();

            const string sql = @"
                SELECT
                    ItemId, SetId, ReqId, InvoiceNumber, PONumber, InvoiceDate, Status, Site,
                    CompanyName, InvoiceTotalAmount, Department, Employee, ItemCode, ItemName, ItemDescription,
                    ItemType, ModelNumber, SerialNumber, CategoryName, LegacyCategoryText,
                    Quantity, Unit, UnitPrice, LineTotal, VendorName, ConditionName, CreatedAt
                FROM dbo.vw_InvoiceNonLicensedItems
                ORDER BY InvoiceDate DESC, InvoiceNumber, ItemName;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(sql, connection) { CommandTimeout = 60 })
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new ReviewItemDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            ReqId = reader.IsDBNull(reader.GetOrdinal("ReqId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ReqId")),
                            InvoiceNumber = reader.IsDBNull(reader.GetOrdinal("InvoiceNumber")) ? null : reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                            PONumber = reader.IsDBNull(reader.GetOrdinal("PONumber")) ? null : reader.GetString(reader.GetOrdinal("PONumber")),
                            InvoiceDate = reader.IsDBNull(reader.GetOrdinal("InvoiceDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("InvoiceDate")),
                            Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? null : reader.GetString(reader.GetOrdinal("Status")),
                            Site = reader.IsDBNull(reader.GetOrdinal("Site")) ? null : reader.GetString(reader.GetOrdinal("Site")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            InvoiceTotalAmount = reader.IsDBNull(reader.GetOrdinal("InvoiceTotalAmount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("InvoiceTotalAmount")),
                            Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? null : reader.GetString(reader.GetOrdinal("Department")),
                            Employee = reader.IsDBNull(reader.GetOrdinal("Employee")) ? null : reader.GetString(reader.GetOrdinal("Employee")),
                            ItemCode = reader.IsDBNull(reader.GetOrdinal("ItemCode")) ? null : reader.GetValue(reader.GetOrdinal("ItemCode")).ToString(),
                            ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? null : reader.GetString(reader.GetOrdinal("ItemName")),
                            ItemDescription = reader.IsDBNull(reader.GetOrdinal("ItemDescription")) ? null : reader.GetString(reader.GetOrdinal("ItemDescription")),
                            ItemType = reader.IsDBNull(reader.GetOrdinal("ItemType")) ? null : reader.GetString(reader.GetOrdinal("ItemType")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
                            LegacyCategoryText = reader.IsDBNull(reader.GetOrdinal("LegacyCategoryText")) ? null : reader.GetString(reader.GetOrdinal("LegacyCategoryText")),
                            Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? 0m : reader.GetDecimal(reader.GetOrdinal("Quantity")),
                            Unit = reader.IsDBNull(reader.GetOrdinal("Unit")) ? null : reader.GetString(reader.GetOrdinal("Unit")),
                            UnitPrice = reader.IsDBNull(reader.GetOrdinal("UnitPrice")) ? 0m : reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                            LineTotal = reader.IsDBNull(reader.GetOrdinal("LineTotal")) ? 0m : reader.GetDecimal(reader.GetOrdinal("LineTotal")),
                            VendorName = reader.IsDBNull(reader.GetOrdinal("VendorName")) ? null : reader.GetString(reader.GetOrdinal("VendorName")),
                            ConditionName = reader.IsDBNull(reader.GetOrdinal("ConditionName")) ? null : reader.GetString(reader.GetOrdinal("ConditionName")),
                            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Commits pending classification decisions to dbo.Item — the only table this feature
        /// ever writes to. Never touches dbo.[Set], dbo.SetItem, dbo.Request, or the view.
        /// Each row is only updated while its LicenseReviewStatus is still NULL, so a decision
        /// made concurrently by another session (or already saved earlier) is not silently
        /// overwritten — the return value reports how many rows were actually applied so the
        /// caller can tell the user if some were skipped.
        /// </summary>
        public async Task<int> SaveReviewDecisionsAsync(IReadOnlyList<ReviewDecision> decisions, int reviewedByUserId)
        {
            if (decisions == null || decisions.Count == 0)
                return 0;

            const string sql = @"
                UPDATE dbo.Item
                SET LicenseReviewStatus = @Status,
                    ReviewedBy = @ReviewedBy,
                    ReviewedAt = SYSUTCDATETIME()
                WHERE ItemId = @ItemId
                  AND LicenseReviewStatus IS NULL;";

            int totalApplied = 0;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var decision in decisions)
                        {
                            if (decision == null || decision.ItemId <= 0)
                                continue;
                            if (decision.Status != "Licensed" && decision.Status != "NonLicensed")
                                throw new ArgumentException($"Invalid review status '{decision.Status}' for ItemId {decision.ItemId}.");

                            using (var command = new SqlCommand(sql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ItemId", decision.ItemId);
                                command.Parameters.AddWithValue("@Status", decision.Status);
                                command.Parameters.AddWithValue("@ReviewedBy", reviewedByUserId);

                                totalApplied += await command.ExecuteNonQueryAsync();
                            }

                            string serial = null;
                            using (var lookup = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", connection, transaction))
                            {
                                lookup.Parameters.AddWithValue("@ItemId", decision.ItemId);
                                var result = lookup.ExecuteScalar();
                                serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                            }
                            ItemAuditTrailWriter.TryLog(connection, transaction, new ItemAuditTrailDto
                            {
                                ItemId = decision.ItemId,
                                SerialNumber = serial,
                                Action = "License Review Classified",
                                ActionTime = DateTime.Now,
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = decision.ItemId,
                                Notes = $"Invoice license review classified as '{decision.Status}'.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }

            if (totalApplied > 0)
            {
                ActivityLogger.Log(
                    ActivityLogger.Actions.Update,
                    "Item",
                    0,
                    $"Invoice License Review: classified {totalApplied} item(s) ({string.Join(", ", CountByStatus(decisions))}).");
            }

            return totalApplied;
        }

        private static IEnumerable<string> CountByStatus(IReadOnlyList<ReviewDecision> decisions)
        {
            int licensed = 0, nonLicensed = 0;
            foreach (var d in decisions)
            {
                if (d.Status == "Licensed") licensed++;
                else if (d.Status == "NonLicensed") nonLicensed++;
            }
            if (licensed > 0) yield return $"{licensed} Licensed";
            if (nonLicensed > 0) yield return $"{nonLicensed} NonLicensed";
        }
    }
}
