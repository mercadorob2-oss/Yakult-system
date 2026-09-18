using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for dbo.CartridgeModel table operations.
    /// Vendor association is managed via dbo.VendorCartridgeModel (bridge table).
    /// </summary>
    public class CartridgeModelRepository
    {
        private readonly string _connectionString;

        public CartridgeModelRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        // ─────────────────────────────────────────────────────────────
        // Scalar subqueries used throughout to surface vendor info from
        // the VendorCartridgeModel bridge table without duplicating rows.
        // TOP 1 ... ORDER BY VendorId gives a deterministic pick when
        // multiple vendors are linked to the same model.
        // ─────────────────────────────────────────────────────────────
        private const string VendorIdSubquery = @"
            (SELECT TOP 1 vcm.VendorId
             FROM dbo.VendorCartridgeModel vcm
             WHERE vcm.CartridgeModelId = cm.CartridgeModelId AND vcm.IsActive = 1
             ORDER BY vcm.VendorId)";

        private const string VendorNameSubquery = @"
            STUFF((
                SELECT ', ' + v2.VendorName
                FROM dbo.VendorCartridgeModel vcm2
                INNER JOIN dbo.Vendor v2 ON vcm2.VendorId = v2.VendorID
                WHERE vcm2.CartridgeModelId = cm.CartridgeModelId AND vcm2.IsActive = 1
                ORDER BY v2.VendorName
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')";

        private const string AvailableStockSubquery = @"
            (SELECT ISNULL(SUM(i.StockOnHand), 0)
             FROM dbo.Item i
             WHERE i.CartridgeModelId = cm.CartridgeModelId AND i.Active = 1)";

        /// <summary>
        /// Gets all active cartridge models. VendorId / VendorName are resolved
        /// via the VendorCartridgeModel bridge table.
        /// </summary>
        public async Task<List<CartridgeModelDto>> GetAllActiveModelsAsync()
        {
            var models = new List<CartridgeModelDto>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                string sql = $@"
                    SELECT
                        cm.CartridgeModelId,
                        cm.ModelNumber,
                        {VendorIdSubquery}   AS VendorId,
                        {VendorNameSubquery} AS VendorName,
                        cm.IsRequestable,
                        cm.IsRefillable,
                        cm.IsActive,
                        cm.CreatedAt,
                        cm.CreatedBy,
                        u.Name AS CreatedByName,
                        {AvailableStockSubquery} AS AvailableStock
                    FROM dbo.CartridgeModel cm
                    LEFT JOIN dbo.[User] u ON cm.CreatedBy = u.UserId
                    WHERE cm.IsActive = 1
                    ORDER BY cm.ModelNumber";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        models.Add(new CartridgeModelDto
                        {
                            CartridgeModelId = reader.GetInt32(0),
                            ModelNumber      = reader.IsDBNull(1) ? null : reader.GetString(1),
                            VendorId         = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                            VendorName       = reader.IsDBNull(3) ? null : reader.GetString(3),
                            IsRequestable    = reader.GetBoolean(4),
                            IsRefillable     = reader.GetBoolean(5),
                            IsActive         = reader.GetBoolean(6),
                            CreatedAt        = reader.GetDateTime(7),
                            CreatedBy        = reader.GetInt32(8),
                            CreatedByName    = reader.IsDBNull(9) ? null : reader.GetString(9),
                            AvailableStock   = reader.GetInt32(10)
                        });
                    }
                }
            }

            return models;
        }

        /// <summary>
        /// Gets a cartridge model by ID. VendorId / VendorName are resolved via bridge table.
        /// </summary>
        public async Task<CartridgeModelDto> GetByIdAsync(int cartridgeModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                string sql = $@"
                    SELECT
                        cm.CartridgeModelId,
                        cm.ModelNumber,
                        {VendorIdSubquery}   AS VendorId,
                        {VendorNameSubquery} AS VendorName,
                        cm.IsRequestable,
                        cm.IsRefillable,
                        cm.IsActive,
                        cm.CreatedAt,
                        cm.CreatedBy,
                        u.Name AS CreatedByName
                    FROM dbo.CartridgeModel cm
                    LEFT JOIN dbo.[User] u ON cm.CreatedBy = u.UserId
                    WHERE cm.CartridgeModelId = @CartridgeModelId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new CartridgeModelDto
                            {
                                CartridgeModelId = reader.GetInt32(0),
                                ModelNumber      = reader.IsDBNull(1) ? null : reader.GetString(1),
                                VendorId         = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                VendorName       = reader.IsDBNull(3) ? null : reader.GetString(3),
                                IsRequestable    = reader.GetBoolean(4),
                                IsRefillable     = reader.GetBoolean(5),
                                IsActive         = reader.GetBoolean(6),
                                CreatedAt        = reader.GetDateTime(7),
                                CreatedBy        = reader.GetInt32(8),
                                CreatedByName    = reader.IsDBNull(9) ? null : reader.GetString(9)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a cartridge model by model number (exact match, active only).
        /// VendorId / VendorName resolved via bridge table.
        /// </summary>
        public async Task<CartridgeModelDto> FindByModelNumberAsync(string modelNumber)
        {
            if (string.IsNullOrWhiteSpace(modelNumber))
                return null;

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                string sql = $@"
                    SELECT
                        cm.CartridgeModelId,
                        cm.ModelNumber,
                        {VendorIdSubquery}   AS VendorId,
                        {VendorNameSubquery} AS VendorName,
                        cm.IsRequestable,
                        cm.IsRefillable,
                        cm.IsActive,
                        cm.CreatedAt,
                        cm.CreatedBy,
                        u.Name AS CreatedByName
                    FROM dbo.CartridgeModel cm
                    LEFT JOIN dbo.[User] u ON cm.CreatedBy = u.UserId
                    WHERE UPPER(LTRIM(RTRIM(cm.ModelNumber))) = UPPER(LTRIM(RTRIM(@ModelNumber)))
                      AND cm.IsActive = 1";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelNumber", modelNumber.Trim());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new CartridgeModelDto
                            {
                                CartridgeModelId = reader.GetInt32(0),
                                ModelNumber      = reader.IsDBNull(1) ? null : reader.GetString(1),
                                VendorId         = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                VendorName       = reader.IsDBNull(3) ? null : reader.GetString(3),
                                IsRequestable    = reader.GetBoolean(4),
                                IsRefillable     = reader.GetBoolean(5),
                                IsActive         = reader.GetBoolean(6),
                                CreatedAt        = reader.GetDateTime(7),
                                CreatedBy        = reader.GetInt32(8),
                                CreatedByName    = reader.IsDBNull(9) ? null : reader.GetString(9)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new cartridge model and, if model.VendorId is set, inserts the
        /// corresponding VendorCartridgeModel bridge row in the same transaction.
        /// Returns the new CartridgeModelId.
        /// </summary>
        public async Task<int> CreateAsync(CartridgeModelDto model)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
                            INSERT INTO dbo.CartridgeModel
                                (ModelNumber, IsRequestable, IsRefillable, IsActive, CreatedAt, CreatedBy)
                            VALUES
                                (@ModelNumber, @IsRequestable, @IsRefillable, @IsActive, GETDATE(), @CreatedBy);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newId;
                        using (var cmd = new SqlCommand(sql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ModelNumber",   (object)model.ModelNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsRequestable", model.IsRequestable);
                            cmd.Parameters.AddWithValue("@IsRefillable",  model.IsRefillable);
                            cmd.Parameters.AddWithValue("@IsActive",      model.IsActive);
                            cmd.Parameters.AddWithValue("@CreatedBy",     model.CreatedBy);
                            newId = (int)await cmd.ExecuteScalarAsync();
                        }

                        if (model.VendorId.HasValue)
                        {
                            const string bridgeSql = @"
                                INSERT INTO dbo.VendorCartridgeModel
                                    (VendorId, CartridgeModelId, IsActive, CreatedDate, CreatedBy)
                                VALUES
                                    (@VendorId, @CartridgeModelId, 1, GETDATE(), @CreatedBy)";

                            using (var cmd = new SqlCommand(bridgeSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@VendorId",         model.VendorId.Value);
                                cmd.Parameters.AddWithValue("@CartridgeModelId", newId);
                                cmd.Parameters.AddWithValue("@CreatedBy",        model.CreatedBy);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return newId;
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
        /// Updates an existing cartridge model. Vendor association in VendorCartridgeModel
        /// is replaced atomically: existing rows for this model are deleted, then a new row
        /// is inserted if model.VendorId is set.
        /// </summary>
        public async Task UpdateAsync(CartridgeModelDto model)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
                            UPDATE dbo.CartridgeModel
                            SET
                                ModelNumber   = @ModelNumber,
                                IsRequestable = @IsRequestable,
                                IsRefillable  = @IsRefillable,
                                IsActive      = @IsActive
                            WHERE CartridgeModelId = @CartridgeModelId";

                        using (var cmd = new SqlCommand(sql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartridgeModelId", model.CartridgeModelId);
                            cmd.Parameters.AddWithValue("@ModelNumber",      (object)model.ModelNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsRequestable",    model.IsRequestable);
                            cmd.Parameters.AddWithValue("@IsRefillable",     model.IsRefillable);
                            cmd.Parameters.AddWithValue("@IsActive",         model.IsActive);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Replace bridge table entries for this model
                        const string deleteBridgeSql =
                            "DELETE FROM dbo.VendorCartridgeModel WHERE CartridgeModelId = @CartridgeModelId";
                        using (var cmd = new SqlCommand(deleteBridgeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartridgeModelId", model.CartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (model.VendorId.HasValue)
                        {
                            const string bridgeSql = @"
                                INSERT INTO dbo.VendorCartridgeModel
                                    (VendorId, CartridgeModelId, IsActive, CreatedDate)
                                VALUES
                                    (@VendorId, @CartridgeModelId, 1, GETDATE())";

                            using (var cmd = new SqlCommand(bridgeSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@VendorId",         model.VendorId.Value);
                                cmd.Parameters.AddWithValue("@CartridgeModelId", model.CartridgeModelId);
                                await cmd.ExecuteNonQueryAsync();
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

        /// <summary>
        /// Archives a cartridge model by inserting into dbo.ArchiveStatus and optionally marking it inactive.
        /// </summary>
        public async Task ArchiveAsync(int cartridgeModelId, string reason, bool deactivate, string archivedBy)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string insertSql = @"
                            INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                            VALUES ('CartridgeModel', @Id, 1, GETDATE(), @ArchivedBy, @Reason)";

                        using (var cmd = new SqlCommand(insertSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id",         cartridgeModelId);
                            cmd.Parameters.AddWithValue("@ArchivedBy", (object)archivedBy ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Reason",     (object)reason ?? DBNull.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (deactivate)
                        {
                            const string deactivateSql = "UPDATE dbo.CartridgeModel SET IsActive = 0 WHERE CartridgeModelId = @Id";
                            using (var cmd = new SqlCommand(deactivateSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                                await cmd.ExecuteNonQueryAsync();
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

        /// <summary>
        /// Checks what still references this cartridge model. ItemCount covers dbo.Item, whose
        /// CartridgeModelId FK is nullable and safely cleared on delete. EmptyCartridgeCount and
        /// BatchLineCount cover dbo.EmptyCartridge / dbo.VendorCartridgeBatchLine, whose
        /// CartridgeModelId FK is NOT NULL — those rows are physical inventory / audit history and
        /// block a permanent delete outright (see DeleteAsync).
        /// </summary>
        public async Task<(bool hasItems, int itemCount, int emptyCartridgeCount, int batchLineCount)> CheckDependenciesAsync(int cartridgeModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                const string sql = @"
                    SELECT
                        (SELECT COUNT(*) FROM dbo.Item WHERE CartridgeModelId = @Id) AS ItemCount,
                        (SELECT COUNT(*) FROM dbo.EmptyCartridge WHERE CartridgeModelId = @Id) AS EmptyCartridgeCount,
                        (SELECT COUNT(*) FROM dbo.VendorCartridgeBatchLine WHERE CartridgeModelId = @Id) AS BatchLineCount";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();
                        int itemCount           = reader.GetInt32(0);
                        int emptyCartridgeCount = reader.GetInt32(1);
                        int batchLineCount      = reader.GetInt32(2);
                        return (itemCount > 0, itemCount, emptyCartridgeCount, batchLineCount);
                    }
                }
            }
        }

        /// <summary>
        /// Permanently deletes a cartridge model.
        /// Nulls CartridgeModelId on any linked Item / VendorCartridgeBatch rows first to preserve
        /// those records (both FKs are nullable). Also removes bridge table rows and archive status
        /// records. Refuses (before touching anything) if EmptyCartridge or VendorCartridgeBatchLine
        /// rows still reference this model — those FKs are NOT NULL, so the rows can't be unlinked,
        /// only deleted outright, which would destroy physical-inventory/audit history.
        /// </summary>
        public async Task DeleteAsync(int cartridgeModelId)
        {
            var (_, _, emptyCartridgeCount, batchLineCount) = await CheckDependenciesAsync(cartridgeModelId);
            if (emptyCartridgeCount > 0 || batchLineCount > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot permanently delete this model: {emptyCartridgeCount} EmptyCartridge record(s) and " +
                    $"{batchLineCount} vendor batch line record(s) still reference it. These represent physical " +
                    "inventory / audit history and cannot be auto-unlinked. Resolve those records first " +
                    "(dispose, sell, or return them) or mark the model Inactive instead.");
            }

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Unlink any items that reference this model (preserve the items themselves)
                        const string unlinkSql = "UPDATE dbo.Item SET CartridgeModelId = NULL WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(unlinkSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Unlink any vendor batches that reference this model (nullable FK, preserve the batch)
                        const string unlinkBatchSql = "UPDATE dbo.VendorCartridgeBatch SET CartridgeModelId = NULL WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(unlinkBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Remove bridge table rows (FK would otherwise block the delete)
                        const string deleteBridgeSql =
                            "DELETE FROM dbo.VendorCartridgeModel WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteBridgeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Remove any archive status entries for this model
                        const string deleteArchiveSql = @"
                            DELETE FROM dbo.ArchiveStatus
                            WHERE EntityType = 'CartridgeModel' AND EntityId = @Id";
                        using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Delete the model itself
                        const string deleteSql = "DELETE FROM dbo.CartridgeModel WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
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

        /// <summary>
        /// Permanently deletes a cartridge model AND everything blocking a normal DeleteAsync —
        /// EmptyCartridge rows (physical returned-cartridge inventory/audit history) and their
        /// ItemLifecycleDecisionCartridge junction rows, plus VendorCartridgeBatchLine rows.
        /// This destroys that history outright; use only when the operator has explicitly chosen
        /// "force delete" after DeleteAsync/CheckDependenciesAsync reported it as blocked.
        /// </summary>
        public async Task ForceDeleteAsync(int cartridgeModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Junction rows referencing this model's EmptyCartridge rows must go first
                        // (NOT NULL FK, no cascade).
                        const string deleteDecisionCartridgeSql = @"
                            DELETE FROM dbo.ItemLifecycleDecisionCartridge
                            WHERE EmptyCartridgeId IN (SELECT EmptyCartridgeId FROM dbo.EmptyCartridge WHERE CartridgeModelId = @Id)";
                        using (var cmd = new SqlCommand(deleteDecisionCartridgeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteEmptyCartridgeSql = "DELETE FROM dbo.EmptyCartridge WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteEmptyCartridgeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteBatchLineSql = "DELETE FROM dbo.VendorCartridgeBatchLine WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteBatchLineSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Unlink items/batches that reference this model (nullable FKs, preserve the rows)
                        const string unlinkSql = "UPDATE dbo.Item SET CartridgeModelId = NULL WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(unlinkSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string unlinkBatchSql = "UPDATE dbo.VendorCartridgeBatch SET CartridgeModelId = NULL WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(unlinkBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteBridgeSql = "DELETE FROM dbo.VendorCartridgeModel WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteBridgeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteArchiveSql = @"
                            DELETE FROM dbo.ArchiveStatus
                            WHERE EntityType = 'CartridgeModel' AND EntityId = @Id";
                        using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteSql = "DELETE FROM dbo.CartridgeModel WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
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

        /// <summary>
        /// Converts a cartridge model into a dbo.ConsumableModel row (Ink / Toner / Print Head),
        /// moving its linked Item rows across and deleting the CartridgeModel afterwards.
        /// Refuses if EmptyCartridge or VendorCartridgeBatchLine rows still reference the model
        /// (NOT NULL FKs, same restriction as DeleteAsync) since that refill/audit history has
        /// no equivalent on the ConsumableModel side and would otherwise be silently orphaned.
        /// IsRefillable and any vendor bridge rows are dropped — Ink/Toner/Print Head models
        /// don't track a refill cycle or a single vendor per model.
        /// Returns the new ConsumableModelId.
        /// </summary>
        public async Task<int> ConvertToConsumableModelAsync(
            int cartridgeModelId, string modelNumber, string newCategory,
            bool isRequestable, bool isActive, int convertedBy)
        {
            var (_, _, emptyCartridgeCount, batchLineCount) = await CheckDependenciesAsync(cartridgeModelId);
            if (emptyCartridgeCount > 0 || batchLineCount > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot convert this model: {emptyCartridgeCount} EmptyCartridge record(s) and " +
                    $"{batchLineCount} vendor batch line record(s) still reference it. These represent " +
                    "physical inventory / audit history with no equivalent on the Ink/Toner/Print Head " +
                    "side. Resolve those records first or leave this model as a Cartridge.");
            }

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string insertSql = @"
                            INSERT INTO dbo.ConsumableModel
                                (ModelNumber, Category, IsRequestable, IsActive, CreatedAt, CreatedBy)
                            VALUES
                                (@ModelNumber, @Category, @IsRequestable, @IsActive, GETDATE(), @CreatedBy);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newConsumableModelId;
                        using (var cmd = new SqlCommand(insertSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ModelNumber",   modelNumber);
                            cmd.Parameters.AddWithValue("@Category",     newCategory);
                            cmd.Parameters.AddWithValue("@IsRequestable", isRequestable);
                            cmd.Parameters.AddWithValue("@IsActive",      isActive);
                            cmd.Parameters.AddWithValue("@CreatedBy",     convertedBy);
                            newConsumableModelId = (int)await cmd.ExecuteScalarAsync();
                        }

                        const string moveItemsSql = @"
                            UPDATE dbo.Item
                            SET ConsumableModelId = @NewId, CartridgeModelId = NULL
                            WHERE CartridgeModelId = @OldId";
                        using (var cmd = new SqlCommand(moveItemsSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@NewId", newConsumableModelId);
                            cmd.Parameters.AddWithValue("@OldId", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteBridgeSql =
                            "DELETE FROM dbo.VendorCartridgeModel WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteBridgeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteArchiveSql = @"
                            DELETE FROM dbo.ArchiveStatus
                            WHERE EntityType = 'CartridgeModel' AND EntityId = @Id";
                        using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteSql = "DELETE FROM dbo.CartridgeModel WHERE CartridgeModelId = @Id";
                        using (var cmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return newConsumableModelId;
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
        /// Gets cartridge models with available stock counts.
        /// VendorId / VendorName are resolved via the VendorCartridgeModel bridge table.
        /// Used for dropdown selection in fulfillment.
        /// </summary>
        public async Task<List<CartridgeModelAvailabilityViewModel>> GetModelsWithAvailabilityAsync()
        {
            var models = new List<CartridgeModelAvailabilityViewModel>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                // ALIGNED WITH: ViewCartridgesPage EffectiveActive / EffectiveStock logic
                // Effective stock = StockOnHand minus quantities already allocated to active requests
                // This ensures only truly fulfillable inventory is counted
                string sql = $@"
                    SELECT
                        cm.CartridgeModelId,
                        cm.ModelNumber,
                        {VendorIdSubquery}   AS VendorId,
                        {VendorNameSubquery} AS VendorName,
                        ISNULL(SUM(CASE
                            WHEN i.Active = 1
                             AND ISNULL(i.RefillStatus, '') NOT IN ('OUTBOUND', 'IN_USE', 'Issued', 'For Refill', 'Disposed')
                             AND (
                                CASE
                                    WHEN i.SerialNumber IS NULL THEN
                                        i.StockOnHand - ISNULL((SELECT SUM(r.Quantity) FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Active = 1), 0)
                                    ELSE
                                        i.StockOnHand
                                END
                             ) > 0
                            THEN
                                CASE
                                    WHEN i.SerialNumber IS NULL THEN
                                        i.StockOnHand - ISNULL((SELECT SUM(r.Quantity) FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Active = 1), 0)
                                    ELSE
                                        i.StockOnHand
                                END
                            ELSE 0
                        END), 0) AS AvailableQuantity
                    FROM dbo.CartridgeModel cm
                    LEFT JOIN dbo.Item i ON cm.CartridgeModelId = i.CartridgeModelId AND i.Category = 'Cartridge'
                    WHERE cm.IsActive = 1
                    GROUP BY cm.CartridgeModelId, cm.ModelNumber
                    HAVING ISNULL(SUM(CASE
                        WHEN i.Active = 1
                         AND ISNULL(i.RefillStatus, '') NOT IN ('OUTBOUND', 'IN_USE', 'Issued', 'For Refill', 'Disposed')
                         AND (
                            CASE
                                WHEN i.SerialNumber IS NULL THEN
                                    i.StockOnHand - ISNULL((SELECT SUM(r.Quantity) FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Active = 1), 0)
                                ELSE
                                    i.StockOnHand
                            END
                         ) > 0
                        THEN
                            CASE
                                WHEN i.SerialNumber IS NULL THEN
                                    i.StockOnHand - ISNULL((SELECT SUM(r.Quantity) FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Active = 1), 0)
                                ELSE
                                    i.StockOnHand
                            END
                        ELSE 0
                    END), 0) > 0
                    ORDER BY cm.ModelNumber";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        models.Add(new CartridgeModelAvailabilityViewModel
                        {
                            CartridgeModelId  = reader.GetInt32(0),
                            ModelNumber       = reader.IsDBNull(1) ? null : reader.GetString(1),
                            VendorId          = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                            VendorName        = reader.IsDBNull(3) ? null : reader.GetString(3),
                            AvailableQuantity = reader.GetInt32(4)
                        });
                    }
                }
            }

            return models;
        }
    }

    /// <summary>
    /// ViewModel for cartridge models with availability
    /// </summary>
    public class CartridgeModelAvailabilityViewModel
    {
        public int CartridgeModelId { get; set; }
        public string ModelNumber { get; set; }
        public int? VendorId { get; set; }
        public string VendorName { get; set; }
        public int AvailableQuantity { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(VendorName)
            ? $"{ModelNumber} (Available: {AvailableQuantity})"
            : $"{ModelNumber} - {VendorName} (Available: {AvailableQuantity})";
    }
}
