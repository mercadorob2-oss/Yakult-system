using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Read-only data access for the "Non-Licensed Invoices" page — the companion report to
    /// Invoice License Review. Reads exclusively from dbo.vw_ConfirmedNonLicensedInvoiceItems,
    /// which surfaces every Hardware invoice line a staff member has confirmed as NonLicensed.
    /// Nothing here ever writes to the database.
    /// </summary>
    public class NonLicensedInvoiceRepository
    {
        public NonLicensedInvoiceRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time,
            // so constructing this repository before the DB is configured doesn't crash.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public sealed class ConfirmedNonLicensedItemDto
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
            public string ReviewedByName { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        /// <summary>Loads every confirmed non-licensed invoice line, straight from
        /// dbo.vw_ConfirmedNonLicensedInvoiceItems. Read-only.</summary>
        public async Task<List<ConfirmedNonLicensedItemDto>> GetConfirmedNonLicensedItemsAsync()
        {
            var results = new List<ConfirmedNonLicensedItemDto>();

            const string sql = @"
                SELECT
                    ItemId, SetId, ReqId, InvoiceNumber, PONumber, InvoiceDate, Status, Site,
                    CompanyName, InvoiceTotalAmount, Department, Employee, ItemCode, ItemName, ItemDescription,
                    ItemType, ModelNumber, SerialNumber, CategoryName, LegacyCategoryText,
                    Quantity, Unit, UnitPrice, LineTotal, VendorName, ConditionName,
                    ReviewedByName, ReviewedAt, CreatedAt
                FROM dbo.vw_ConfirmedNonLicensedInvoiceItems
                ORDER BY ReviewedAt DESC, InvoiceNumber, ItemName;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(sql, connection) { CommandTimeout = 60 })
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new ConfirmedNonLicensedItemDto
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
                            ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
                            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        });
                    }
                }
            }

            return results;
        }
    }
}
