using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeModelRepository.cs and
    /// Wpf/CartridgeManagement/ViewModels/ViewCartridgesViewModel.cs (FetchItemsAsync).
    /// Vendor conversion (ConvertToConsumableModelAsync) is out of scope for this web port —
    /// only Cartridge Models CRUD + View Cartridges are mirrored here.
    /// </summary>
    public class CartridgeMasterDataRepository : ICartridgeMasterDataRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public CartridgeMasterDataRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

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

        public async Task<List<CartridgeModelDto>> GetAllActiveModelsAsync()
        {
            var models = new List<CartridgeModelDto>();

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
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

            using var cmd = new SqlCommand(sql, con);
            using var reader = await cmd.ExecuteReaderAsync();
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

            return models;
        }

        public async Task<CartridgeModelDto?> GetModelByIdAsync(int cartridgeModelId)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
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

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

            using var reader = await cmd.ExecuteReaderAsync();
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

            return null;
        }

        public async Task<CartridgeModelDto?> FindModelByModelNumberAsync(string modelNumber)
        {
            if (string.IsNullOrWhiteSpace(modelNumber)) return null;

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
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

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ModelNumber", modelNumber.Trim());

            using var reader = await cmd.ExecuteReaderAsync();
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

            return null;
        }

        public async Task<int> CreateModelAsync(CartridgeModelDto model)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();
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
                    cmd.Parameters.AddWithValue("@ModelNumber",   (object?)model.ModelNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsRequestable", model.IsRequestable);
                    cmd.Parameters.AddWithValue("@IsRefillable",  model.IsRefillable);
                    cmd.Parameters.AddWithValue("@IsActive",      model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedBy",     model.CreatedBy);
                    newId = (int)(await cmd.ExecuteScalarAsync())!;
                }

                if (model.VendorId.HasValue)
                {
                    const string bridgeSql = @"
                        INSERT INTO dbo.VendorCartridgeModel
                            (VendorId, CartridgeModelId, IsActive, CreatedDate, CreatedBy)
                        VALUES
                            (@VendorId, @CartridgeModelId, 1, GETDATE(), @CreatedBy)";

                    using var cmd = new SqlCommand(bridgeSql, con, transaction);
                    cmd.Parameters.AddWithValue("@VendorId",         model.VendorId.Value);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", newId);
                    cmd.Parameters.AddWithValue("@CreatedBy",        model.CreatedBy);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return newId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateModelAsync(CartridgeModelDto model)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();
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
                    cmd.Parameters.AddWithValue("@ModelNumber",      (object?)model.ModelNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsRequestable",    model.IsRequestable);
                    cmd.Parameters.AddWithValue("@IsRefillable",     model.IsRefillable);
                    cmd.Parameters.AddWithValue("@IsActive",         model.IsActive);
                    await cmd.ExecuteNonQueryAsync();
                }

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

                    using var cmd = new SqlCommand(bridgeSql, con, transaction);
                    cmd.Parameters.AddWithValue("@VendorId",         model.VendorId.Value);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", model.CartridgeModelId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task ArchiveModelAsync(int cartridgeModelId, string reason, bool deactivate, string archivedBy)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();
            try
            {
                const string insertSql = @"
                    INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                    VALUES ('CartridgeModel', @Id, 1, GETDATE(), @ArchivedBy, @Reason)";

                using (var cmd = new SqlCommand(insertSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id",         cartridgeModelId);
                    cmd.Parameters.AddWithValue("@ArchivedBy", (object?)archivedBy ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Reason",     reason);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (deactivate)
                {
                    const string deactivateSql = "UPDATE dbo.CartridgeModel SET IsActive = 0 WHERE CartridgeModelId = @Id";
                    using var cmd = new SqlCommand(deactivateSql, con, transaction);
                    cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool HasItems, int ItemCount, int EmptyCartridgeCount, int BatchLineCount)> CheckModelDependenciesAsync(int cartridgeModelId)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM dbo.Item WHERE CartridgeModelId = @Id) AS ItemCount,
                    (SELECT COUNT(*) FROM dbo.EmptyCartridge WHERE CartridgeModelId = @Id) AS EmptyCartridgeCount,
                    (SELECT COUNT(*) FROM dbo.VendorCartridgeBatchLine WHERE CartridgeModelId = @Id) AS BatchLineCount";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            int itemCount           = reader.GetInt32(0);
            int emptyCartridgeCount = reader.GetInt32(1);
            int batchLineCount      = reader.GetInt32(2);
            return (itemCount > 0, itemCount, emptyCartridgeCount, batchLineCount);
        }

        public async Task SetModelActiveAsync(int cartridgeModelId, bool isActive)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            const string sql = "UPDATE dbo.CartridgeModel SET IsActive = @IsActive WHERE CartridgeModelId = @Id";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IsActive", isActive);
            cmd.Parameters.AddWithValue("@Id", cartridgeModelId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteModelAsync(int cartridgeModelId)
        {
            var (_, _, emptyCartridgeCount, batchLineCount) = await CheckModelDependenciesAsync(cartridgeModelId);
            if (emptyCartridgeCount > 0 || batchLineCount > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot permanently delete this model: {emptyCartridgeCount} EmptyCartridge record(s) and " +
                    $"{batchLineCount} vendor batch line record(s) still reference it. These represent physical " +
                    "inventory / audit history and cannot be auto-unlinked. Resolve those records first " +
                    "(dispose, sell, or return them) or mark the model Inactive instead.");
            }

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();
            try
            {
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

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<CartridgeItemViewDto>> GetCartridgeItemsAsync(bool includeInactive)
        {
            var items = new List<CartridgeItemViewDto>();

            string activeClause = includeInactive ? "" : "\n  AND i.Active = 1";

            string sql = @"
SELECT
    i.ItemId, i.Name, i.SerialNumber, i.ModelNumber, i.Active,
    ISNULL(i.StockOnHand, 0) AS StockOnHand,
    i.DateCreated, u1.Name AS CreatedByName,
    i.Amount, c.ConditionName, i.Remarks,
    CASE WHEN i.ConditionId = 1 THEN 'Good' WHEN i.ConditionId = 2 THEN 'Damaged' ELSE NULL END AS LatestStatus,
    ISNULL(rh.RepairCount, 0) AS RepairCount,
    v.VendorName
FROM dbo.Item i
LEFT JOIN dbo.[User] u1 ON i.CreatedBy = u1.UserId
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (
    SELECT SerialNumber, COUNT(*) AS RepairCount
    FROM dbo.ItemRepairHistory GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
WHERE i.Category = 'Cartridge'
  AND arch.EntityId IS NULL" + activeClause + @"
ORDER BY i.DateCreated DESC, i.ItemId DESC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new CartridgeItemViewDto
                {
                    ItemId         = reader.GetInt32(0),
                    Name           = reader.GetString(1),
                    SerialNumber   = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ModelNumber    = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Active         = reader.GetBoolean(4),
                    StockOnHand    = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    DateCreated    = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                    CreatedByName  = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                    Amount         = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                    ConditionName  = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Remarks        = reader.IsDBNull(10) ? null : reader.GetString(10),
                    LatestStatus   = reader.IsDBNull(11) ? null : reader.GetString(11),
                    RepairCount    = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    VendorName     = reader.IsDBNull(13) ? null : reader.GetString(13)
                });
            }

            return items;
        }
    }
}
