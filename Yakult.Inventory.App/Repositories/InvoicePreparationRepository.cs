using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Backs the Sub-Type Group staging workflow: Items Page -> Sub-Type Group -> Invoice
    /// Preparation page -> Invoice (dbo.[Set] with IsInvoice=1 + dbo.SetItem). A group
    /// (dbo.InvoicePreparation) is a named bundle of existing dbo.Item rows — created
    /// directly from the Items Page's "Add to Contract/Subscription/License/Service
    /// Group" bulk actions — with its own Reference Code and Begin/End Date overriding
    /// the member items' own dates. Generating an invoice from one or more selected
    /// groups hands their combined items to SoftwareServiceSetDialog.
    /// </summary>
    public class InvoicePreparationRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>Every Sub-Type Group (Draft or Completed), newest first. SupplierName
        /// is derived from the member items' own Vendor — "(Mixed)" when they don't all
        /// share one, blank when none of them have a Vendor set.</summary>
        public List<InvoicePreparationSummaryRow> GetAllGroups()
        {
            const string sql = @"
                SELECT
                    p.PreparationId, p.SubType, p.ReferenceCode, p.BeginDate, p.EndDate,
                    p.Status, p.CreatedAt,
                    COUNT(pi.PreparationItemId) AS ItemCount,
                    ISNULL(SUM(pi.Quantity * pi.UnitPrice), 0) AS Subtotal,
                    CASE
                        WHEN COUNT(DISTINCT i.VendorId) = 0 THEN NULL
                        WHEN COUNT(DISTINCT i.VendorId) > 1 THEN '(Mixed)'
                        ELSE MAX(v.VendorName)
                    END AS SupplierName
                FROM dbo.InvoicePreparation p
                LEFT JOIN dbo.InvoicePreparationItem pi ON pi.PreparationId = p.PreparationId
                LEFT JOIN dbo.Item i ON i.ItemId = pi.ItemId
                LEFT JOIN dbo.Vendor v ON v.VendorID = i.VendorId
                GROUP BY p.PreparationId, p.SubType, p.ReferenceCode, p.BeginDate, p.EndDate, p.Status, p.CreatedAt
                ORDER BY p.CreatedAt DESC";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    var rows = new List<InvoicePreparationSummaryRow>();
                    while (reader.Read())
                    {
                        rows.Add(new InvoicePreparationSummaryRow
                        {
                            PreparationId = reader.GetInt32(reader.GetOrdinal("PreparationId")),
                            SubType = reader["SubType"].ToString(),
                            ReferenceCode = reader["ReferenceCode"] == DBNull.Value ? null : reader["ReferenceCode"].ToString(),
                            BeginDate = reader["BeginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BeginDate"]),
                            EndDate = reader["EndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["EndDate"]),
                            Status = reader["Status"].ToString(),
                            CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                            ItemCount = Convert.ToInt32(reader["ItemCount"]),
                            Subtotal = Convert.ToDecimal(reader["Subtotal"]),
                            SupplierName = reader["SupplierName"] == DBNull.Value ? null : reader["SupplierName"].ToString()
                        });
                    }
                    return rows;
                }
            }
        }

        /// <summary>One Sub-Type Group's header, for the Preview Contents dialog. Same
        /// derived SupplierName rule as <see cref="GetAllGroups"/>.</summary>
        public InvoicePreparationSummaryRow GetGroupById(int preparationId)
        {
            const string sql = @"
                SELECT
                    p.PreparationId, p.SubType, p.ReferenceCode, p.BeginDate, p.EndDate,
                    p.Status, p.CreatedAt,
                    COUNT(pi.PreparationItemId) AS ItemCount,
                    ISNULL(SUM(pi.Quantity * pi.UnitPrice), 0) AS Subtotal,
                    CASE
                        WHEN COUNT(DISTINCT i.VendorId) = 0 THEN NULL
                        WHEN COUNT(DISTINCT i.VendorId) > 1 THEN '(Mixed)'
                        ELSE MAX(v.VendorName)
                    END AS SupplierName
                FROM dbo.InvoicePreparation p
                LEFT JOIN dbo.InvoicePreparationItem pi ON pi.PreparationId = p.PreparationId
                LEFT JOIN dbo.Item i ON i.ItemId = pi.ItemId
                LEFT JOIN dbo.Vendor v ON v.VendorID = i.VendorId
                WHERE p.PreparationId = @PreparationId
                GROUP BY p.PreparationId, p.SubType, p.ReferenceCode, p.BeginDate, p.EndDate, p.Status, p.CreatedAt";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@PreparationId", preparationId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new InvoicePreparationSummaryRow
                        {
                            PreparationId = reader.GetInt32(reader.GetOrdinal("PreparationId")),
                            SubType = reader["SubType"].ToString(),
                            ReferenceCode = reader["ReferenceCode"] == DBNull.Value ? null : reader["ReferenceCode"].ToString(),
                            BeginDate = reader["BeginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BeginDate"]),
                            EndDate = reader["EndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["EndDate"]),
                            Status = reader["Status"].ToString(),
                            CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                            ItemCount = Convert.ToInt32(reader["ItemCount"]),
                            Subtotal = Convert.ToDecimal(reader["Subtotal"]),
                            SupplierName = reader["SupplierName"] == DBNull.Value ? null : reader["SupplierName"].ToString()
                        };
                    }
                }
            }
        }

        public List<InvoicePreparationItemDto> GetGroupItems(int preparationId)
        {
            const string sql = @"
                SELECT
                    pi.PreparationItemId, pi.PreparationId, pi.ItemId,
                    i.Name AS ItemName, i.Description AS ItemDescription,
                    pi.Quantity, pi.UnitPrice, pi.Remarks,
                    i.UnitOfMeasure, i.ModelNumber, i.SerialNumber, i.ItemType,
                    COALESCE(ic.Name, i.Category) AS Category,
                    i.WarrantyStartDate, i.WarrantyEndDate,
                    (SELECT TOP 1 rn.PartNumber FROM dbo.Renewals rn WHERE rn.ItemId = i.ItemId AND rn.IsArchived = 0) AS PartNumber
                FROM dbo.InvoicePreparationItem pi
                LEFT JOIN dbo.Item i ON pi.ItemId = i.ItemId
                LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId = i.CategoryId
                WHERE pi.PreparationId = @PreparationId
                ORDER BY pi.PreparationItemId";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@PreparationId", preparationId);
                    using (var reader = command.ExecuteReader())
                    {
                        var rows = new List<InvoicePreparationItemDto>();
                        while (reader.Read())
                        {
                            rows.Add(new InvoicePreparationItemDto
                            {
                                PreparationItemId = reader.GetInt32(reader.GetOrdinal("PreparationItemId")),
                                PreparationId = reader.GetInt32(reader.GetOrdinal("PreparationId")),
                                ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                                Description = reader["ItemDescription"] == DBNull.Value ? null : reader["ItemDescription"].ToString(),
                                UnitOfMeasure = reader["UnitOfMeasure"] == DBNull.Value ? null : reader["UnitOfMeasure"].ToString(),
                                Quantity = Convert.ToDecimal(reader["Quantity"]),
                                UnitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                                Remarks = reader["Remarks"] == DBNull.Value ? null : reader["Remarks"].ToString(),
                                PartNumber = reader["PartNumber"] == DBNull.Value ? null : reader["PartNumber"].ToString(),
                                ModelNumber = reader["ModelNumber"] == DBNull.Value ? null : reader["ModelNumber"].ToString(),
                                SerialNumber = reader["SerialNumber"] == DBNull.Value ? null : reader["SerialNumber"].ToString(),
                                ItemType = reader["ItemType"] == DBNull.Value ? null : reader["ItemType"].ToString(),
                                Category = reader["Category"] == DBNull.Value ? null : reader["Category"].ToString(),
                                WarrantyStartDate = reader["WarrantyStartDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["WarrantyStartDate"]),
                                WarrantyEndDate = reader["WarrantyEndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["WarrantyEndDate"])
                            });
                        }
                        return rows;
                    }
                }
            }
        }

        /// <summary>Item ids belonging to any group (Draft or Completed) — used by the
        /// Items Page bulk actions to guard that an item can only belong to one group at
        /// a time.</summary>
        public HashSet<int> GetGroupedItemIds()
        {
            const string sql = "SELECT DISTINCT ItemId FROM dbo.InvoicePreparationItem";
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    var ids = new HashSet<int>();
                    while (reader.Read())
                        ids.Add(reader.GetInt32(0));
                    return ids;
                }
            }
        }

        /// <summary>Creates a new Sub-Type Group from the given items — the Items Page's
        /// "Add to Contract/Subscription/License/Service Group" bulk action's landing
        /// point. Guards that no item's own duration (dbo.Item.StartDate/EndDate) exceeds
        /// the given date range, and that none of the items already belong to another
        /// group.</summary>
        /// <summary>
        /// Items that are not eligible to join a group of a given Sub-Type. A group's Sub-Type is
        /// a property of its members, not something the group confers on them — so an item must
        /// already carry the matching Sub-Type before it can join.
        /// </summary>
        public sealed class SubTypeEligibility
        {
            /// <summary>Items with no Sub-Type set — fix with Edit Item or Bulk Edit Item.</summary>
            public List<string> Untagged { get; } = new List<string>();

            /// <summary>Items carrying a different Sub-Type, as "Name (has: License)".</summary>
            public List<string> Mismatched { get; } = new List<string>();

            /// <summary>True when every selected item already matches the group's Sub-Type.</summary>
            public bool AllEligible => Untagged.Count == 0 && Mismatched.Count == 0;
        }

        /// <summary>
        /// Checks the selected items against the Sub-Type of the group they are about to join.
        /// Read-only — used to explain the problem before anything is written, and again inside
        /// <see cref="CreateGroup"/> so no other caller can bypass it.
        /// </summary>
        public SubTypeEligibility CheckSubTypeEligibility(List<int> itemIds, string targetSubType)
        {
            var result = new SubTypeEligibility();
            if (itemIds == null || itemIds.Count == 0 || string.IsNullOrWhiteSpace(targetSubType))
                return result;

            var paramNames = itemIds.Select((id, i) => $"@Id{i}").ToList();
            string sql = $"SELECT ItemId, Name, SubType FROM dbo.Item WHERE ItemId IN ({string.Join(",", paramNames)})";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    for (int i = 0; i < itemIds.Count; i++)
                        command.Parameters.AddWithValue(paramNames[i], itemIds[i]);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader.IsDBNull(1) ? $"Item {reader.GetInt32(0)}" : reader.GetString(1);
                            string current = reader.IsDBNull(2) ? null : reader.GetString(2);

                            if (string.IsNullOrWhiteSpace(current))
                                result.Untagged.Add(name);
                            else if (!string.Equals(current, targetSubType, StringComparison.OrdinalIgnoreCase))
                                result.Mismatched.Add($"{name} (has: {current})");
                        }
                    }
                }
            }

            return result;
        }

        public int CreateGroup(string subType, string referenceCode, DateTime? beginDate, DateTime? endDate,
            List<(int ItemId, decimal Quantity, decimal UnitPrice)> items, int createdBy)
        {
            ItemSubTypeCatalog.ValidateSubType(subType);
            if (items == null || items.Count == 0)
                throw new InvalidOperationException("Select at least one item to form a group.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var itemIds = items.Select(i => i.ItemId).ToList();
                        EnsureItemDatesWithinRange(connection, transaction, beginDate, endDate, itemIds);
                        EnsureItemsNotInAnotherGroup(connection, transaction, itemIds, null);
                        // A group does not confer its Sub-Type on its members — each item must
                        // already carry it. The UI checks this first and explains how to fix it;
                        // this is the backstop so no other caller can create an inconsistent group.
                        EnsureItemsCarrySubType(connection, transaction, itemIds, subType);

                        const string insertHeaderSql = @"
                            INSERT INTO dbo.InvoicePreparation (SubType, ReferenceCode, BeginDate, EndDate, Status, CreatedBy, CreatedAt)
                            VALUES (@SubType, @ReferenceCode, @BeginDate, @EndDate, 'Draft', @CreatedBy, sysutcdatetime());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int preparationId;
                        using (var command = new SqlCommand(insertHeaderSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@SubType", subType);
                            command.Parameters.AddWithValue("@ReferenceCode", string.IsNullOrWhiteSpace(referenceCode) ? (object)DBNull.Value : referenceCode.Trim());
                            command.Parameters.AddWithValue("@BeginDate", (object)beginDate ?? DBNull.Value);
                            command.Parameters.AddWithValue("@EndDate", (object)endDate ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CreatedBy", createdBy);
                            preparationId = (int)command.ExecuteScalar();
                        }

                        const string insertItemSql = @"
                            INSERT INTO dbo.InvoicePreparationItem (PreparationId, ItemId, Quantity, UnitPrice, CreatedBy, CreatedAt)
                            VALUES (@PreparationId, @ItemId, @Quantity, @UnitPrice, @CreatedBy, sysutcdatetime())";
                        foreach (var item in items)
                        {
                            using (var command = new SqlCommand(insertItemSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@PreparationId", preparationId);
                                command.Parameters.AddWithValue("@ItemId", item.ItemId);
                                command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                command.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                                command.Parameters.AddWithValue("@CreatedBy", createdBy);
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        return preparationId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Hard guard: every item joining a Sub-Type group must already carry that Sub-Type.
        /// Throws with the offending item names so the caller can show something actionable.
        /// </summary>
        private static void EnsureItemsCarrySubType(SqlConnection connection, SqlTransaction transaction,
            List<int> itemIds, string subType)
        {
            if (itemIds == null || itemIds.Count == 0) return;

            var paramNames = itemIds.Select((id, i) => $"@Id{i}").ToList();
            string sql = $"SELECT ItemId, Name, SubType FROM dbo.Item WHERE ItemId IN ({string.Join(",", paramNames)})";

            var untagged = new List<string>();
            var mismatched = new List<string>();

            using (var command = new SqlCommand(sql, connection, transaction))
            {
                for (int i = 0; i < itemIds.Count; i++)
                    command.Parameters.AddWithValue(paramNames[i], itemIds[i]);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.IsDBNull(1) ? $"Item {reader.GetInt32(0)}" : reader.GetString(1);
                        string current = reader.IsDBNull(2) ? null : reader.GetString(2);

                        if (string.IsNullOrWhiteSpace(current)) untagged.Add(name);
                        else if (!string.Equals(current, subType, StringComparison.OrdinalIgnoreCase))
                            mismatched.Add($"{name} (has: {current})");
                    }
                }
            }

            if (untagged.Count == 0 && mismatched.Count == 0) return;

            var message = new System.Text.StringBuilder();
            if (untagged.Count > 0)
                message.Append($"{untagged.Count} item(s) have no Sub-Type: {string.Join(", ", untagged.Take(5))}"
                    + (untagged.Count > 5 ? $" and {untagged.Count - 5} more. " : ". "));
            if (mismatched.Count > 0)
                message.Append($"{mismatched.Count} item(s) have a different Sub-Type: {string.Join(", ", mismatched.Take(5))}"
                    + (mismatched.Count > 5 ? $" and {mismatched.Count - 5} more. " : ". "));
            message.Append($"Set their Sub-Type to '{subType}' first.");

            throw new InvalidOperationException(message.ToString());
        }

        /// <summary>Hard guard: when a date range is specified for a group of items, none
        /// of those items' own duration (dbo.Item.StartDate/EndDate) may run longer than
        /// that range — the assigned range is the authoritative period.</summary>
        private static void EnsureItemDatesWithinRange(SqlConnection connection, SqlTransaction transaction, DateTime? beginDate, DateTime? endDate, List<int> itemIds)
        {
            if (!beginDate.HasValue && !endDate.HasValue) return;
            if (itemIds == null || itemIds.Count == 0) return;

            var paramNames = itemIds.Select((id, i) => $"@Id{i}").ToList();
            string sql = $"SELECT ItemId, Name, StartDate, EndDate FROM dbo.Item WHERE ItemId IN ({string.Join(",", paramNames)})";

            var violations = new List<string>();
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                for (int i = 0; i < itemIds.Count; i++)
                    command.Parameters.AddWithValue(paramNames[i], itemIds[i]);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime? itemStart = reader["StartDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["StartDate"]);
                        DateTime? itemEnd = reader["EndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["EndDate"]);

                        bool startsTooEarly = itemStart.HasValue && beginDate.HasValue && itemStart.Value < beginDate.Value;
                        bool endsTooLate = itemEnd.HasValue && endDate.HasValue && itemEnd.Value > endDate.Value;

                        if (startsTooEarly || endsTooLate)
                            violations.Add(reader["Name"].ToString());
                    }
                }
            }

            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The following item(s) have a longer duration than the specified date range: {string.Join(", ", violations)}. " +
                    "Adjust the Begin/End Date or the item's Start/End Date before proceeding.");
            }
        }

        /// <summary>Hard guard: an item already belonging to another Sub-Type Group (or
        /// already invoiced with a Sub-Type) cannot be added to a different group — an
        /// item belongs to at most one group at a time.</summary>
        private static void EnsureItemsNotInAnotherGroup(SqlConnection connection, SqlTransaction transaction, List<int> itemIds, int? excludePreparationId)
        {
            if (itemIds == null || itemIds.Count == 0) return;

            var itemParamNames = itemIds.Select((id, i) => $"@Item{i}").ToList();
            string excludeClause = excludePreparationId.HasValue ? "AND pi.PreparationId <> @ExcludePreparationId" : "";

            string sql = $@"
                SELECT i.Name, p.SubType AS GroupSubType, p.ReferenceCode AS GroupReferenceCode
                FROM dbo.InvoicePreparationItem pi
                JOIN dbo.InvoicePreparation p ON p.PreparationId = pi.PreparationId
                JOIN dbo.Item i ON i.ItemId = pi.ItemId
                WHERE pi.ItemId IN ({string.Join(",", itemParamNames)})
                  {excludeClause}
                UNION
                SELECT i.Name, si.SubType AS GroupSubType, si.ReferenceCode AS GroupReferenceCode
                FROM dbo.SetItem si
                JOIN dbo.Item i ON si.ItemId = i.ItemId
                WHERE si.ItemId IN ({string.Join(",", itemParamNames)})
                  AND si.SubType IS NOT NULL";

            var conflicts = new List<string>();
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                for (int i = 0; i < itemIds.Count; i++)
                    command.Parameters.AddWithValue(itemParamNames[i], itemIds[i]);
                if (excludePreparationId.HasValue)
                    command.Parameters.AddWithValue("@ExcludePreparationId", excludePreparationId.Value);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        conflicts.Add($"{reader["Name"]} (already in {reader["GroupSubType"]} #{reader["GroupReferenceCode"]})");
                }
            }

            if (conflicts.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The following item(s) are already part of another Sub-Type Group: {string.Join(", ", conflicts)}. " +
                    "An item can only belong to one group at a time.");
            }
        }

        /// <summary>Combined member items of the given groups — used to pre-fill
        /// SoftwareServiceSetDialog when the user clicks "Create an Invoice From Them".
        /// Throws if any selected group already generated an invoice.</summary>
        public List<InvoicePreparationItemDto> GetItemsForGroups(List<int> preparationIds)
        {
            if (preparationIds == null || preparationIds.Count == 0)
                throw new InvalidOperationException("Select at least one group to generate an invoice.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                var paramNames = preparationIds.Select((id, i) => $"@Id{i}").ToList();

                const string statusCheckSqlTemplate = @"
                    SELECT PreparationId, SubType, ReferenceCode, Status, GeneratedSetId
                    FROM dbo.InvoicePreparation WHERE PreparationId IN ({0})";
                using (var command = new SqlCommand(string.Format(statusCheckSqlTemplate, string.Join(",", paramNames)), connection))
                {
                    for (int i = 0; i < preparationIds.Count; i++)
                        command.Parameters.AddWithValue(paramNames[i], preparationIds[i]);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var generatedSetId = reader["GeneratedSetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["GeneratedSetId"]);
                            var status = reader["Status"].ToString();
                            if (generatedSetId.HasValue || !string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException(
                                    $"The {reader["SubType"]} group (Reference #{reader["ReferenceCode"]}) has already generated an invoice.");
                        }
                    }
                }

                const string itemsSqlTemplate = @"
                    SELECT pi.PreparationItemId, pi.PreparationId, pi.ItemId,
                           i.Name AS ItemName, i.Description AS ItemDescription, i.UnitOfMeasure,
                           pi.Quantity, pi.UnitPrice, pi.Remarks, p.SubType, p.ReferenceCode, p.BeginDate, p.EndDate,
                           (SELECT TOP 1 rn.PartNumber FROM dbo.Renewals rn WHERE rn.ItemId = i.ItemId AND rn.IsArchived = 0) AS PartNumber
                    FROM dbo.InvoicePreparationItem pi
                    JOIN dbo.InvoicePreparation p ON p.PreparationId = pi.PreparationId
                    LEFT JOIN dbo.Item i ON i.ItemId = pi.ItemId
                    WHERE pi.PreparationId IN ({0})
                    ORDER BY pi.PreparationId, pi.PreparationItemId";
                using (var command = new SqlCommand(string.Format(itemsSqlTemplate, string.Join(",", paramNames)), connection))
                {
                    for (int i = 0; i < preparationIds.Count; i++)
                        command.Parameters.AddWithValue(paramNames[i], preparationIds[i]);

                    using (var reader = command.ExecuteReader())
                    {
                        var rows = new List<InvoicePreparationItemDto>();
                        while (reader.Read())
                        {
                            rows.Add(new InvoicePreparationItemDto
                            {
                                PreparationItemId = reader.GetInt32(reader.GetOrdinal("PreparationItemId")),
                                PreparationId = reader.GetInt32(reader.GetOrdinal("PreparationId")),
                                ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                                Description = reader["ItemDescription"] == DBNull.Value ? null : reader["ItemDescription"].ToString(),
                                UnitOfMeasure = reader["UnitOfMeasure"] == DBNull.Value ? null : reader["UnitOfMeasure"].ToString(),
                                Quantity = Convert.ToDecimal(reader["Quantity"]),
                                UnitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                                Remarks = reader["Remarks"] == DBNull.Value ? null : reader["Remarks"].ToString(),
                                SubType = reader["SubType"].ToString(),
                                ReferenceCode = reader["ReferenceCode"] == DBNull.Value ? null : reader["ReferenceCode"].ToString(),
                                BeginDate = reader["BeginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BeginDate"]),
                                EndDate = reader["EndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["EndDate"]),
                                PartNumber = reader["PartNumber"] == DBNull.Value ? null : reader["PartNumber"].ToString()
                            });
                        }
                        return rows;
                    }
                }
            }
        }

        /// <summary>Marks every given group Completed and points it at the invoice
        /// (dbo.[Set]) that SoftwareServiceSetDialog just created from its items —
        /// called by the Invoice Preparation page right after that dialog returns OK.</summary>
        public void MarkGroupsCompleted(List<int> preparationIds, int setId, int userId)
        {
            if (preparationIds == null || preparationIds.Count == 0) return;

            const string sql = @"
                UPDATE dbo.InvoicePreparation
                SET Status = 'Completed', GeneratedSetId = @SetId, ModifiedBy = @UserId, ModifiedAt = sysutcdatetime()
                WHERE PreparationId = @PreparationId";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var preparationId in preparationIds)
                        {
                            using (var command = new SqlCommand(sql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@SetId", setId);
                                command.Parameters.AddWithValue("@UserId", userId);
                                command.Parameters.AddWithValue("@PreparationId", preparationId);
                                command.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>The reverse of MarkGroupsCompleted — pulls a Sub-Type Group back OFF an
        /// already-created invoice and re-lands it as a brand new Draft group on the
        /// Invoice Preparation page's Not Completed tab, fully intact (same SubType,
        /// Reference Code, dates, and member items/quantities/prices as they currently
        /// stand on the invoice). Deletes the group's dbo.SetItem rows from the invoice
        /// (reversing any inventory stock effects the same way DeleteInvoiceSetItemAsync
        /// does for a single row) and the now-empty dbo.SetItemSubTypeGroup row itself.
        /// Returns the new PreparationId.</summary>
        public int RemoveGroupFromInvoice(int groupId, int removedBy)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string subType, referenceCode = null;
                        DateTime? beginDate = null, endDate = null;
                        int setId;

                        const string groupSql = @"
                            SELECT SetId, SubType, ReferenceCode, BeginDate, EndDate
                            FROM dbo.SetItemSubTypeGroup
                            WHERE GroupId = @GroupId";
                        using (var command = new SqlCommand(groupSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@GroupId", groupId);
                            using (var reader = command.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("Sub-Type Group not found.");

                                setId = reader.GetInt32(0);
                                subType = reader.GetString(1);
                                referenceCode = reader.IsDBNull(2) ? null : reader.GetString(2);
                                beginDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                                endDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
                            }
                        }

                        var members = new List<(int ItemId, decimal Quantity, decimal UnitPrice, bool AffectsInventory)>();
                        const string membersSql = @"
                            SELECT si.ItemId, si.Quantity, si.UnitPrice, ISNULL(i.AffectsInventory, 0)
                            FROM dbo.SetItem si
                            LEFT JOIN dbo.Item i ON i.ItemId = si.ItemId
                            WHERE si.GroupId = @GroupId";
                        using (var command = new SqlCommand(membersSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@GroupId", groupId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    members.Add((
                                        reader.GetInt32(0),
                                        reader.GetDecimal(1),
                                        reader.GetDecimal(2),
                                        !reader.IsDBNull(3) && reader.GetBoolean(3)));
                                }
                            }
                        }

                        if (members.Count == 0)
                            throw new InvalidOperationException("This Sub-Type Group has no items to move back.");

                        // Reverse any inventory stock effects, same as DeleteInvoiceSetItemAsync.
                        foreach (var member in members.Where(m => m.AffectsInventory))
                        {
                            const string restoreStockSql = @"
                                UPDATE dbo.Item SET StockOnHand = StockOnHand + @Quantity WHERE ItemId = @ItemId";
                            using (var command = new SqlCommand(restoreStockSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Quantity", member.Quantity);
                                command.Parameters.AddWithValue("@ItemId", member.ItemId);
                                command.ExecuteNonQuery();
                            }

                            const string deleteInventorySql = @"
                                DELETE FROM dbo.Inventory WHERE SetId = @SetId AND ItemId = @ItemId";
                            using (var command = new SqlCommand(deleteInventorySql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@SetId", setId);
                                command.Parameters.AddWithValue("@ItemId", member.ItemId);
                                command.ExecuteNonQuery();
                            }
                        }

                        const string deleteSetItemsSql = "DELETE FROM dbo.SetItem WHERE GroupId = @GroupId";
                        using (var command = new SqlCommand(deleteSetItemsSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@GroupId", groupId);
                            command.ExecuteNonQuery();
                        }

                        const string deleteGroupSql = "DELETE FROM dbo.SetItemSubTypeGroup WHERE GroupId = @GroupId";
                        using (var command = new SqlCommand(deleteGroupSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@GroupId", groupId);
                            command.ExecuteNonQuery();
                        }

                        const string insertHeaderSql = @"
                            INSERT INTO dbo.InvoicePreparation (SubType, ReferenceCode, BeginDate, EndDate, Status, CreatedBy, CreatedAt)
                            VALUES (@SubType, @ReferenceCode, @BeginDate, @EndDate, 'Draft', @CreatedBy, sysutcdatetime());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int preparationId;
                        using (var command = new SqlCommand(insertHeaderSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@SubType", subType);
                            command.Parameters.AddWithValue("@ReferenceCode", (object)referenceCode ?? DBNull.Value);
                            command.Parameters.AddWithValue("@BeginDate", (object)beginDate ?? DBNull.Value);
                            command.Parameters.AddWithValue("@EndDate", (object)endDate ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CreatedBy", removedBy);
                            preparationId = (int)command.ExecuteScalar();
                        }

                        const string insertItemSql = @"
                            INSERT INTO dbo.InvoicePreparationItem (PreparationId, ItemId, Quantity, UnitPrice, CreatedBy, CreatedAt)
                            VALUES (@PreparationId, @ItemId, @Quantity, @UnitPrice, @CreatedBy, sysutcdatetime())";
                        foreach (var member in members)
                        {
                            using (var command = new SqlCommand(insertItemSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@PreparationId", preparationId);
                                command.Parameters.AddWithValue("@ItemId", member.ItemId);
                                command.Parameters.AddWithValue("@Quantity", member.Quantity);
                                command.Parameters.AddWithValue("@UnitPrice", member.UnitPrice);
                                command.Parameters.AddWithValue("@CreatedBy", removedBy);
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        return preparationId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Called right after InvoiceBuilderDialog's fast-build path creates Sub-Type Groups
        /// directly on dbo.SetItemSubTypeGroup (via ServiceSetRepository, bypassing the Items
        /// Page staging flow entirely) — backfills a matching, already-Completed
        /// dbo.InvoicePreparation + dbo.InvoicePreparationItem record per group, purely so
        /// those groups show up on the Invoice Sub Groups page's Completed tab like every
        /// other invoiced group does. The reverse of RemoveGroupFromInvoice above, except this
        /// writes 'Completed' (with GeneratedSetId already set) instead of 'Draft'.
        /// Best-effort: call this after the invoice's own transaction has already committed —
        /// a failure here must never be treated as the invoice itself having failed.
        /// </summary>
        public void RegisterCompletedGroupsFromSet(int setId, int createdBy)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var groups = new List<(int GroupId, string SubType, string ReferenceCode, DateTime? BeginDate, DateTime? EndDate)>();
                        const string groupsSql = @"
                            SELECT GroupId, SubType, ReferenceCode, BeginDate, EndDate
                            FROM dbo.SetItemSubTypeGroup
                            WHERE SetId = @SetId";
                        using (var command = new SqlCommand(groupsSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@SetId", setId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    groups.Add((
                                        reader.GetInt32(0),
                                        reader.GetString(1),
                                        reader.IsDBNull(2) ? null : reader.GetString(2),
                                        reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                        reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4)));
                                }
                            }
                        }

                        foreach (var group in groups)
                        {
                            // Grouped by ItemId (summed) to respect
                            // UQ_InvoicePreparationItem_PreparationId_ItemId in the rare case the
                            // same Item appears on more than one line within this group.
                            var members = new List<(int ItemId, decimal Quantity, decimal UnitPrice)>();
                            const string membersSql = @"
                                SELECT ItemId, SUM(Quantity) AS Quantity, MAX(UnitPrice) AS UnitPrice
                                FROM dbo.SetItem
                                WHERE GroupId = @GroupId
                                GROUP BY ItemId";
                            using (var command = new SqlCommand(membersSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@GroupId", group.GroupId);
                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                        members.Add((reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2)));
                                }
                            }
                            if (members.Count == 0) continue;

                            const string insertHeaderSql = @"
                                INSERT INTO dbo.InvoicePreparation
                                    (SubType, ReferenceCode, BeginDate, EndDate, Status, GeneratedSetId, CreatedBy, CreatedAt)
                                VALUES
                                    (@SubType, @ReferenceCode, @BeginDate, @EndDate, 'Completed', @SetId, @CreatedBy, sysutcdatetime());
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                            int preparationId;
                            using (var command = new SqlCommand(insertHeaderSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@SubType", group.SubType);
                                command.Parameters.AddWithValue("@ReferenceCode", (object)group.ReferenceCode ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BeginDate", (object)group.BeginDate ?? DBNull.Value);
                                command.Parameters.AddWithValue("@EndDate", (object)group.EndDate ?? DBNull.Value);
                                command.Parameters.AddWithValue("@SetId", setId);
                                command.Parameters.AddWithValue("@CreatedBy", createdBy);
                                preparationId = (int)command.ExecuteScalar();
                            }

                            const string insertItemSql = @"
                                INSERT INTO dbo.InvoicePreparationItem (PreparationId, ItemId, Quantity, UnitPrice, CreatedBy, CreatedAt)
                                VALUES (@PreparationId, @ItemId, @Quantity, @UnitPrice, @CreatedBy, sysutcdatetime())";
                            foreach (var member in members)
                            {
                                using (var command = new SqlCommand(insertItemSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@PreparationId", preparationId);
                                    command.Parameters.AddWithValue("@ItemId", member.ItemId);
                                    command.Parameters.AddWithValue("@Quantity", member.Quantity);
                                    command.Parameters.AddWithValue("@UnitPrice", member.UnitPrice);
                                    command.Parameters.AddWithValue("@CreatedBy", createdBy);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void DeleteGroup(int preparationId)
        {
            const string sql = "DELETE FROM dbo.InvoicePreparation WHERE PreparationId = @Id AND Status = 'Draft'";
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Id", preparationId);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
