using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.WPF.Archive.Services
{
    /// <summary>
    /// Permanently removes an already-archived dbo.ArchiveStatus row and its underlying entity
    /// row. Deliberately generic (table + PK column per EntityType, mirroring
    /// ArchiveDetailsViewModel.BuildEntitySql's type list) rather than a per-type cascade —
    /// these are soft-deleted records already excluded from every active view, so a plain DELETE
    /// is normally safe. If some other table still holds a live FK into the row, SQL Server
    /// refuses with a REFERENCE constraint error, which the caller surfaces per-record rather
    /// than this service guessing at a destructive cascade.
    ///
    /// EntityType "Item" is the one type known to routinely have real dependents (Inventory,
    /// SetItem, Request, Renewals, CartridgeMovement, Cartridge) even after archiving, so it
    /// delegates to ItemRepository.ForceDeleteItemWithRelations — the same cascade already used
    /// by the Items page's force-delete flow — instead of the generic single-table delete.
    /// </summary>
    public static class ArchivePermanentDeleteService
    {
        private static readonly Dictionary<string, (string Table, string PkColumn)> EntityMap =
            new Dictionary<string, (string Table, string PkColumn)>(StringComparer.OrdinalIgnoreCase)
        {
            { "Item",           ("dbo.Item",           "ItemId") },
            { "Set",            ("dbo.[Set]",          "SetId") },
            { "Request",        ("dbo.Request",        "ReqId") },
            { "Inventory",      ("dbo.Inventory",      "InvId") },
            { "Department",     ("dbo.Department",     "DeptId") },
            { "Branch",         ("dbo.Branch",         "BranchId") },
            { "Employee",       ("dbo.Employee",       "EmpId") },
            { "Company",        ("dbo.Company",        "ComId") },
            { "Vendor",         ("dbo.Vendor",          "VendorID") },
            { "EmptyCartridge", ("dbo.EmptyCartridge", "EmptyCartridgeId") },
            { "CartridgeModel", ("dbo.CartridgeModel", "CartridgeModelId") },
            { "ConsumableModel",("dbo.ConsumableModel","ConsumableModelId") },
            { "ItemCategory",   ("dbo.ItemCategory",   "CategoryId") },
            { "Condition",      ("dbo.Condition",      "ConditionId") },
            { "Renewal",        ("dbo.Renewals",       "RenewalId") },
        };

        public static async Task DeleteAsync(string connectionString, string entityType, int entityId)
        {
            if (!EntityMap.TryGetValue(entityType, out var mapping))
                throw new InvalidOperationException($"Permanent delete is not supported for entity type '{entityType}'.");

            if (string.Equals(entityType, "Item", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureArchivedAsync(connectionString, entityType, entityId);

                var (success, message) = await new ItemRepository().ForceDeleteItemWithRelations(entityId);
                if (!success)
                    throw new InvalidOperationException(message);
                return;
            }

            using (var con = new SqlConnection(connectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // Safety guard: refuse unless the row is actually archived — never
                        // let this path touch a live record.
                        using (var check = new SqlCommand(
                            "SELECT COUNT(*) FROM dbo.ArchiveStatus WHERE EntityType = @Type AND EntityId = @Id AND IsArchived = 1",
                            con, tx))
                        {
                            check.Parameters.AddWithValue("@Type", entityType);
                            check.Parameters.AddWithValue("@Id", entityId);
                            int archivedCount = (int)await check.ExecuteScalarAsync();
                            if (archivedCount == 0)
                                throw new InvalidOperationException("This record is not currently archived — refusing to permanently delete it.");
                        }

                        using (var cmd = new SqlCommand($"DELETE FROM {mapping.Table} WHERE {mapping.PkColumn} = @Id", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", entityId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (var cmd = new SqlCommand(
                            "DELETE FROM dbo.ArchiveStatus WHERE EntityType = @Type AND EntityId = @Id", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@Type", entityType);
                            cmd.Parameters.AddWithValue("@Id", entityId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Sets that still reference this Item, either directly (dbo.SetItem) or via one of the
        /// Item's Request rows (dbo.[Set].ReqId) — the two paths that block ForceDeleteItemWithRelations.
        /// Used to turn a raw REFERENCE constraint failure into a message naming the actual SetCode(s).
        /// </summary>
        public static async Task<List<string>> GetBlockingSetCodesAsync(string connectionString, int itemId)
        {
            var codes = new List<string>();
            const string sql = @"
                SELECT DISTINCT s.SetCode
                FROM dbo.[Set] s
                WHERE s.SetId IN (SELECT si.SetId FROM dbo.SetItem si WHERE si.ItemId = @ItemId)
                   OR s.ReqId IN (SELECT r.ReqId FROM dbo.Request r WHERE r.ItemId = @ItemId)
                ORDER BY s.SetCode";

            using (var con = new SqlConnection(connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                            codes.Add(reader.IsDBNull(0) ? "(unnamed set)" : reader.GetString(0));
                    }
                }
            }

            return codes;
        }

        private static async Task EnsureArchivedAsync(string connectionString, string entityType, int entityId)
        {
            using (var con = new SqlConnection(connectionString))
            {
                await con.OpenAsync();
                using (var check = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.ArchiveStatus WHERE EntityType = @Type AND EntityId = @Id AND IsArchived = 1", con))
                {
                    check.Parameters.AddWithValue("@Type", entityType);
                    check.Parameters.AddWithValue("@Id", entityId);
                    int archivedCount = (int)await check.ExecuteScalarAsync();
                    if (archivedCount == 0)
                        throw new InvalidOperationException("This record is not currently archived — refusing to permanently delete it.");
                }
            }
        }
    }
}
