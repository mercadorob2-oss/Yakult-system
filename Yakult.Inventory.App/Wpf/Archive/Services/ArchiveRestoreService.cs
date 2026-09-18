using System;
using System.Data.SqlClient;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.WPF.Archive.Services
{
    /// <summary>
    /// Shared cascade-restore logic for dbo.ArchiveStatus rows — flips IsArchived back to 0
    /// (with RestoredAt/RestoredBy) and reactivates whatever flag the underlying entity table
    /// uses, including the Set/Request child-record cascades. Used by both the single-record
    /// restore in ArchiveDetailsViewModel and the bulk restore on the Archive Records grid.
    /// </summary>
    public static class ArchiveRestoreService
    {
        public static void RestoreEntityCascade(SqlConnection con, SqlTransaction tx, string entityType, int entityId, string restoredBy)
        {
            RestoreArchiveStatus(con, tx, entityType, entityId, restoredBy);

            switch (entityType)
            {
                case "Set":
                    RestoreSetCascade(con, tx, entityId, restoredBy);
                    break;
                case "Request":
                    RestoreRequestCascade(con, tx, entityId, restoredBy);
                    break;
                case "Item":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Item SET Active = 1 WHERE ItemId = @Id",
                        ("@Id", entityId));
                    LogItemRestored(con, tx, entityId, restoredBy);
                    break;
                case "Inventory":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Inventory SET Active = 1 WHERE InvId = @Id",
                        ("@Id", entityId));
                    break;
                case "Company":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Company SET Active = 1 WHERE ComId = @Id",
                        ("@Id", entityId));
                    break;
                case "Department":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Department SET Active = 1 WHERE DeptId = @Id",
                        ("@Id", entityId));
                    break;
                case "Branch":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Branch SET Active = 1 WHERE BranchId = @Id",
                        ("@Id", entityId));
                    break;
                case "Employee":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Employee SET Active = 1 WHERE EmpId = @Id",
                        ("@Id", entityId));
                    break;
                case "Vendor":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Vendor SET IsActive = 1 WHERE VendorID = @Id",
                        ("@Id", entityId));
                    break;
                case "EmptyCartridge":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.EmptyCartridge SET Status = 'Pending', VendorBatchId = NULL WHERE EmptyCartridgeId = @Id",
                        ("@Id", entityId));
                    break;
                case "ItemCategory":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.ItemCategory SET Active = 1 WHERE CategoryId = @Id",
                        ("@Id", entityId));
                    break;
                case "CartridgeModel":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.CartridgeModel SET IsActive = 1 WHERE CartridgeModelId = @Id",
                        ("@Id", entityId));
                    break;
                case "ConsumableModel":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.ConsumableModel SET IsActive = 1 WHERE ConsumableModelId = @Id",
                        ("@Id", entityId));
                    break;
                case "Condition":
                    // dbo.Condition has no active flag — ArchiveStatus is already cleared above
                    break;
                case "Renewal":
                    ExecNonQuery(con, tx,
                        "UPDATE dbo.Renewals SET IsArchived = 0 WHERE RenewalId = @Id",
                        ("@Id", entityId));
                    break;
            }
        }

        private static void LogItemRestored(SqlConnection con, SqlTransaction tx, int itemId, string restoredBy)
        {
            try
            {
                string serial = null;
                using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, tx))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    var result = cmd.ExecuteScalar();
                    serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                }
                ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                {
                    ItemId = itemId,
                    SerialNumber = serial,
                    Action = "Item Restored",
                    ActionTime = DateTime.Now,
                    Direction = "IN",
                    Status = "Completed",
                    ReferenceType = "Item",
                    ReferenceId = itemId,
                    Notes = "Item restored from archive.",
                    CreatedBy = restoredBy ?? "System"
                });
            }
            catch { /* best-effort */ }
        }

        private static void RestoreSetCascade(SqlConnection con, SqlTransaction tx, int setId, string restoredBy)
        {
            ExecNonQuery(con, tx,
                "UPDATE dbo.[Set] SET Active = 1 WHERE SetId = @SetId",
                ("@SetId", setId));

            ExecNonQuery(con, tx, @"
                UPDATE r SET r.Active = 1
                FROM dbo.Request r
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Request' AND a.EntityId = r.ReqId
                WHERE r.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId));

            ExecNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0, a.RestoredAt = SYSDATETIME(), a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                INNER JOIN dbo.Request r ON a.EntityType = 'Request' AND a.EntityId = r.ReqId
                WHERE r.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId),
                ("@RestoredBy", restoredBy ?? "System"));

            ExecNonQuery(con, tx, @"
                UPDATE inv SET inv.Active = 1
                FROM dbo.Inventory inv
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId));

            ExecNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0, a.RestoredAt = SYSDATETIME(), a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                INNER JOIN dbo.Inventory inv ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId),
                ("@RestoredBy", restoredBy ?? "System"));

            ExecNonQuery(con, tx, @"
                UPDATE i SET i.Active = 1
                FROM dbo.Item i
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Item' AND a.EntityId = i.ItemId
                WHERE a.IsArchived = 1
                  AND (   a.ArchiveReason LIKE ('Archived with Set '     + CAST(@SetId AS NVARCHAR(20)) + '%')
                       OR a.ArchiveReason LIKE ('Archived with Invoice ' + CAST(@SetId AS NVARCHAR(20)) + '%'))",
                ("@SetId", setId));

            ExecNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0, a.RestoredAt = SYSDATETIME(), a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                WHERE a.EntityType = 'Item' AND a.IsArchived = 1
                  AND (   a.ArchiveReason LIKE ('Archived with Set '     + CAST(@SetId AS NVARCHAR(20)) + '%')
                       OR a.ArchiveReason LIKE ('Archived with Invoice ' + CAST(@SetId AS NVARCHAR(20)) + '%'))",
                ("@SetId", setId),
                ("@RestoredBy", restoredBy ?? "System"));
        }

        private static void RestoreRequestCascade(SqlConnection con, SqlTransaction tx, int reqId, string restoredBy)
        {
            ExecNonQuery(con, tx,
                "UPDATE dbo.Request SET Active = 1 WHERE ReqId = @ReqId",
                ("@ReqId", reqId));

            ExecNonQuery(con, tx, @"
                UPDATE inv SET inv.Active = 1
                FROM dbo.Inventory inv
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.ReqId = @ReqId AND a.IsArchived = 1",
                ("@ReqId", reqId));

            ExecNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0, a.RestoredAt = SYSDATETIME(), a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                INNER JOIN dbo.Inventory inv ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.ReqId = @ReqId AND a.IsArchived = 1",
                ("@ReqId", reqId),
                ("@RestoredBy", restoredBy ?? "System"));

            ExecNonQuery(con, tx, @"
                UPDATE i SET i.Active = 1
                FROM dbo.Item i
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Item' AND a.EntityId = i.ItemId
                WHERE a.IsArchived = 1
                  AND a.ArchiveReason LIKE ('Archived with Request ' + CAST(@ReqId AS NVARCHAR(20)) + '%')",
                ("@ReqId", reqId));

            ExecNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0, a.RestoredAt = SYSDATETIME(), a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                WHERE a.EntityType = 'Item' AND a.IsArchived = 1
                  AND a.ArchiveReason LIKE ('Archived with Request ' + CAST(@ReqId AS NVARCHAR(20)) + '%')",
                ("@ReqId", reqId),
                ("@RestoredBy", restoredBy ?? "System"));
        }

        private static void RestoreArchiveStatus(SqlConnection con, SqlTransaction tx, string entityType, int entityId, string restoredBy)
        {
            ExecNonQuery(con, tx, @"
                UPDATE dbo.ArchiveStatus
                SET IsArchived = 0, RestoredAt = SYSDATETIME(), RestoredBy = @RestoredBy
                WHERE EntityType = @EntityType AND EntityId = @EntityId AND IsArchived = 1",
                ("@EntityType", (object)entityType),
                ("@EntityId",   (object)entityId),
                ("@RestoredBy", (object)(restoredBy ?? "System")));
        }

        private static void ExecNonQuery(SqlConnection con, SqlTransaction tx, string sql,
            params (string Name, object Value)[] parameters)
        {
            using (var cmd = new SqlCommand(sql, con, tx))
            {
                foreach (var (name, value) in parameters)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
