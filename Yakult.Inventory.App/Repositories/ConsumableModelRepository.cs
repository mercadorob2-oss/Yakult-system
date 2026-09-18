using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for dbo.ConsumableModel — the Ink/Toner/Print Head equivalent of
    /// dbo.CartridgeModel. Simpler than cartridges: no vendor bridge table, no
    /// Brand New/Refilled condition split, single stock number per model.
    /// </summary>
    public class ConsumableModelRepository
    {
        private readonly string _connectionString;

        public ConsumableModelRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        private const string AvailableStockSubquery = @"
            (SELECT ISNULL(SUM(i.StockOnHand), 0)
             FROM dbo.Item i
             WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1)";

        /// <summary>
        /// Gets all active consumable models, optionally filtered to a single category.
        /// </summary>
        public async Task<List<ConsumableModelDto>> GetAllActiveModelsAsync(string category = null)
        {
            var models = new List<ConsumableModelDto>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                string sql = $@"
                    SELECT
                        cm.ConsumableModelId,
                        cm.ModelNumber,
                        cm.Category,
                        cm.IsRequestable,
                        cm.IsActive,
                        cm.CreatedAt,
                        cm.CreatedBy,
                        u.Name AS CreatedByName,
                        {AvailableStockSubquery} AS AvailableStock
                    FROM dbo.ConsumableModel cm
                    LEFT JOIN dbo.[User] u ON cm.CreatedBy = u.UserId
                    WHERE cm.IsActive = 1
                      AND (@Category IS NULL
                           OR REPLACE(LOWER(cm.Category), ' ', '') = REPLACE(LOWER(@Category), ' ', ''))
                    ORDER BY cm.ModelNumber";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Category", (object)category ?? DBNull.Value);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            models.Add(ReadModel(reader));
                        }
                    }
                }
            }

            return models;
        }

        public async Task<ConsumableModelDto> GetByIdAsync(int consumableModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                string sql = $@"
                    SELECT
                        cm.ConsumableModelId,
                        cm.ModelNumber,
                        cm.Category,
                        cm.IsRequestable,
                        cm.IsActive,
                        cm.CreatedAt,
                        cm.CreatedBy,
                        u.Name AS CreatedByName,
                        {AvailableStockSubquery} AS AvailableStock
                    FROM dbo.ConsumableModel cm
                    LEFT JOIN dbo.[User] u ON cm.CreatedBy = u.UserId
                    WHERE cm.ConsumableModelId = @Id";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", consumableModelId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return ReadModel(reader);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a consumable model by model number + category (trimmed, case-insensitive,
        /// AND whitespace-insensitive on Category — so a pre-existing "Print Head" row still
        /// matches a "Printhead" lookup and the importer updates it instead of duplicating).
        /// Active only. Used to block duplicate creation.
        /// </summary>
        public async Task<ConsumableModelDto> FindByModelNumberAsync(string modelNumber, string category)
        {
            if (string.IsNullOrWhiteSpace(modelNumber) || string.IsNullOrWhiteSpace(category))
                return null;

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                string sql = $@"
                    SELECT
                        cm.ConsumableModelId,
                        cm.ModelNumber,
                        cm.Category,
                        cm.IsRequestable,
                        cm.IsActive,
                        cm.CreatedAt,
                        cm.CreatedBy,
                        u.Name AS CreatedByName,
                        {AvailableStockSubquery} AS AvailableStock
                    FROM dbo.ConsumableModel cm
                    LEFT JOIN dbo.[User] u ON cm.CreatedBy = u.UserId
                    WHERE UPPER(LTRIM(RTRIM(cm.ModelNumber))) = UPPER(LTRIM(RTRIM(@ModelNumber)))
                      AND REPLACE(LOWER(cm.Category), ' ', '') = REPLACE(LOWER(@Category), ' ', '')
                      AND cm.IsActive = 1";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelNumber", modelNumber.Trim());
                    cmd.Parameters.AddWithValue("@Category", category);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return ReadModel(reader);
                    }
                }
            }

            return null;
        }

        public async Task<int> CreateAsync(ConsumableModelDto model)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                // UQ_ConsumableModel_ModelNumber_Category is not scoped to IsActive, so an
                // archived row with the same key blocks a blind INSERT. Reactivate it instead
                // if one exists (callers like the CSV importer only look up IsActive=1 rows,
                // so an archived match otherwise looks "missing" and would 23000 here).
                const string findArchivedSql = @"
                    SELECT ConsumableModelId FROM dbo.ConsumableModel
                    WHERE UPPER(LTRIM(RTRIM(ModelNumber))) = UPPER(LTRIM(RTRIM(@ModelNumber)))
                      AND REPLACE(LOWER(Category), ' ', '') = REPLACE(LOWER(@Category), ' ', '')
                      AND IsActive = 0";

                int? archivedId = null;
                using (var cmd = new SqlCommand(findArchivedSql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelNumber", model.ModelNumber);
                    cmd.Parameters.AddWithValue("@Category", model.Category);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        archivedId = Convert.ToInt32(result);
                }

                if (archivedId.HasValue)
                {
                    const string reactivateSql = @"
                        UPDATE dbo.ConsumableModel
                        SET IsRequestable = @IsRequestable, IsActive = 1
                        WHERE ConsumableModelId = @Id";

                    using (var cmd = new SqlCommand(reactivateSql, con))
                    {
                        cmd.Parameters.AddWithValue("@Id", archivedId.Value);
                        cmd.Parameters.AddWithValue("@IsRequestable", model.IsRequestable);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    ActivityLogger.Log(ActivityLogger.Actions.Create, "ConsumableModel", archivedId.Value,
                        $"Consumable model '{model.ModelNumber}' ({model.Category}) reactivated");

                    return archivedId.Value;
                }

                const string sql = @"
                    INSERT INTO dbo.ConsumableModel
                        (ModelNumber, Category, IsRequestable, IsActive, CreatedAt, CreatedBy)
                    VALUES
                        (@ModelNumber, @Category, @IsRequestable, @IsActive, GETDATE(), @CreatedBy);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelNumber", model.ModelNumber);
                    cmd.Parameters.AddWithValue("@Category", model.Category);
                    cmd.Parameters.AddWithValue("@IsRequestable", model.IsRequestable);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedBy", model.CreatedBy);
                    int newId = (int)await cmd.ExecuteScalarAsync();

                    ActivityLogger.Log(ActivityLogger.Actions.Create, "ConsumableModel", newId,
                        $"Consumable model '{model.ModelNumber}' ({model.Category}) created");

                    return newId;
                }
            }
        }

        public async Task UpdateAsync(ConsumableModelDto model)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    UPDATE dbo.ConsumableModel
                    SET
                        ModelNumber   = @ModelNumber,
                        Category      = @Category,
                        IsRequestable = @IsRequestable,
                        IsActive      = @IsActive
                    WHERE ConsumableModelId = @Id";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", model.ConsumableModelId);
                    cmd.Parameters.AddWithValue("@ModelNumber", model.ModelNumber);
                    cmd.Parameters.AddWithValue("@Category", model.Category);
                    cmd.Parameters.AddWithValue("@IsRequestable", model.IsRequestable);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            ActivityLogger.Log(ActivityLogger.Actions.Update, "ConsumableModel", model.ConsumableModelId,
                $"Consumable model '{model.ModelNumber}' ({model.Category}) updated");
        }

        /// <summary>
        /// Soft-deletes a consumable model (IsActive = 0). Linked Item rows keep their
        /// ConsumableModelId — the FK is nullable but there's no lifecycle reason to
        /// clear it, unlike cartridges' hard-delete path.
        /// </summary>
        public async Task ArchiveAsync(int consumableModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = "UPDATE dbo.ConsumableModel SET IsActive = 0 WHERE ConsumableModelId = @Id";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", consumableModelId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            ActivityLogger.Log(ActivityLogger.Actions.Delete, "ConsumableModel", consumableModelId,
                $"Consumable model ID {consumableModelId} archived");
        }

        /// <summary>
        /// Counts Item rows still linked to this model. ConsumableModelId is a nullable FK,
        /// so a delete can always safely unlink them — this is purely informational, used to
        /// warn the user before a permanent delete.
        /// </summary>
        public async Task<int> CheckDependenciesAsync(int consumableModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                const string sql = "SELECT COUNT(*) FROM dbo.Item WHERE ConsumableModelId = @Id";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", consumableModelId);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        /// <summary>
        /// Permanently deletes a consumable model. Unlinks any Item rows first (the FK is
        /// nullable, so the items themselves are preserved) then removes the row.
        /// </summary>
        public async Task DeleteAsync(int consumableModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string unlinkSql = "UPDATE dbo.Item SET ConsumableModelId = NULL WHERE ConsumableModelId = @Id";
                        using (var cmd = new SqlCommand(unlinkSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", consumableModelId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteSql = "DELETE FROM dbo.ConsumableModel WHERE ConsumableModelId = @Id";
                        using (var cmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", consumableModelId);
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

            ActivityLogger.Log(ActivityLogger.Actions.Delete, "ConsumableModel", consumableModelId,
                $"Consumable model ID {consumableModelId} permanently deleted");
        }

        /// <summary>
        /// Total available stock for a model — every active Item row tied to it, summed.
        /// This is the replacement for the fragile Name+CategoryId matching that used to
        /// pool stock across accidental duplicate Item rows.
        /// </summary>
        public async Task<int> GetAvailableStockByModelIdAsync(int consumableModelId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT ISNULL(SUM(StockOnHand), 0)
                    FROM dbo.Item
                    WHERE ConsumableModelId = @Id AND Active = 1";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", consumableModelId);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        /// <summary>
        /// Sums outstanding (requested but not yet issued) quantity across dbo.Request
        /// for Ink/Toner/Print Head items, grouped by ConsumableModelId. StockOnHand is
        /// only decremented when a Request is actually issued (see SetRepository), so a
        /// request sitting at Quantity &gt; IssuedQty is stock that is spoken for but still
        /// physically on hand — this is the figure to subtract from AvailableStock to get
        /// the true "free" stock. Category matching mirrors RequestRepository's
        /// FulfillmentTrackedRequestsCte (free-typed Category column, no fixed dropdown).
        /// </summary>
        public async Task<Dictionary<int, int>> GetOutstandingRequestedQtyByModelIdAsync()
        {
            var result = new Dictionary<int, int>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT i.ConsumableModelId, SUM(r.Quantity - ISNULL(r.IssuedQty, 0)) AS OutstandingQty
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    WHERE r.Active = 1
                      AND i.ConsumableModelId IS NOT NULL
                      AND (r.Quantity - ISNULL(r.IssuedQty, 0)) > 0
                      AND (
                            REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
                         OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
                         OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
                          )
                    GROUP BY i.ConsumableModelId";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        result[reader.GetInt32(0)] = reader.GetInt32(1);
                }
            }

            return result;
        }

        private static ConsumableModelDto ReadModel(SqlDataReader reader)
        {
            return new ConsumableModelDto
            {
                ConsumableModelId = reader.GetInt32(0),
                ModelNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsRequestable = reader.GetBoolean(3),
                IsActive = reader.GetBoolean(4),
                CreatedAt = reader.GetDateTime(5),
                CreatedBy = reader.GetInt32(6),
                CreatedByName = reader.IsDBNull(7) ? null : reader.GetString(7),
                AvailableStock = reader.GetInt32(8)
            };
        }
    }
}
