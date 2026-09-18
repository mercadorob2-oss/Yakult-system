using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// PORTED FROM: Yakult.Inventory.App/Repositories/UnfulfilledCartridgeExchangeRepository.cs
    /// (GetPurelyUnfulfilledExchanges, GetPartiallyFulfilledExchangesFullSet, FulfillExchange)
    /// and CartridgeManagementRepository.cs (GetCartridgeModelIdByModelNumber,
    /// GetAvailableIssuableStock). Straight port — no behavior changes.
    /// </summary>
    public class CartridgeFulfillmentRepository : ICartridgeFulfillmentRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public CartridgeFulfillmentRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        private const string SelectColumns = @"
                SELECT
                    u.UnfulfilledId,
                    u.ReqId,
                    u.EmpId,
                    u.BranchId,
                    u.DeptId,
                    u.CartridgeModel,
                    u.RequestedQty,
                    u.ReturnedEmptyQty,
                    u.IssuedFullQty,
                    u.UnfulfilledQty,
                    u.Remarks,
                    u.Status,
                    u.CreatedDate,
                    u.CreatedBy,
                    u.FulfilledDate,
                    u.FulfilledBy,
                    u.FulfilledRemarks,
                    -- Dept-level requests have no requester employee (r.EmpId IS NULL); the row's
                    -- EmpId is a stand-in (the receiver / fulfilling user) only so the FK is
                    -- satisfied, so don't show it as the requester.
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE ISNULL(e.Name, N'') END AS RequesterName,
                    COALESCE(b.Name, rb.Name) AS BranchName,
                    COALESCE(d.Name, rd.Name) AS DepartmentName,
                    uc.Name AS CreatedByName,
                    uf.Name AS FulfilledByName,
                    r.SetId,
                    r.SubmissionSessionId
                FROM dbo.UnfulfilledCartridgeExchange u
                LEFT  JOIN dbo.Employee   e  ON u.EmpId       = e.EmpId
                LEFT  JOIN dbo.Branch     b  ON u.BranchId    = b.BranchId
                LEFT  JOIN dbo.Department d  ON u.DeptId      = d.DeptId
                LEFT  JOIN dbo.[User]     uc ON u.CreatedBy   = uc.UserId
                LEFT  JOIN dbo.[User]     uf ON u.FulfilledBy = uf.UserId
                LEFT  JOIN dbo.Request    r  ON u.ReqId       = r.ReqId
                LEFT  JOIN dbo.Branch     rb ON r.BranchId    = rb.BranchId
                LEFT  JOIN dbo.Department rd ON r.DeptId      = rd.DeptId";

        public async Task<List<UnfulfilledCartridgeExchangeDto>> GetPurelyUnfulfilledExchangesAsync()
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            string sql = SelectColumns + @"
                WHERE u.Status = 'Pending'
                  AND u.IssuedFullQty = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.UnfulfilledCartridgeExchange u2
                      INNER JOIN dbo.Request r2 ON u2.ReqId = r2.ReqId
                      WHERE r.SetId IS NOT NULL AND r.SetId > 0
                        AND r2.SetId = r.SetId
                        AND u2.Status = 'Fulfilled'
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Request r5
                      WHERE r.SetId IS NOT NULL AND r.SetId > 0
                        AND r5.SetId = r.SetId
                        AND r5.ReqId <> u.ReqId
                        AND r5.Status IN ('Fulfilled', 'Completed')
                  )
                ORDER BY u.CreatedDate ASC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(MapToDto(reader));

            return result;
        }

        public async Task<List<UnfulfilledCartridgeExchangeDto>> GetPartiallyFulfilledExchangesFullSetAsync()
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            string sql = SelectColumns + @"
                WHERE
                    (
                        r.SetId IS NOT NULL AND r.SetId > 0
                        AND EXISTS (
                            SELECT 1
                            FROM dbo.UnfulfilledCartridgeExchange u2
                            INNER JOIN dbo.Request r2 ON u2.ReqId = r2.ReqId
                            WHERE r2.SetId = r.SetId
                              AND u2.Status = 'Pending'
                        )
                        AND (
                            EXISTS (
                                SELECT 1
                                FROM dbo.UnfulfilledCartridgeExchange u3
                                INNER JOIN dbo.Request r3 ON u3.ReqId = r3.ReqId
                                WHERE r3.SetId = r.SetId
                                  AND u3.Status = 'Pending'
                                  AND u3.IssuedFullQty > 0
                            )
                            OR
                            EXISTS (
                                SELECT 1
                                FROM dbo.UnfulfilledCartridgeExchange u4
                                INNER JOIN dbo.Request r4 ON u4.ReqId = r4.ReqId
                                WHERE r4.SetId = r.SetId
                                  AND u4.Status = 'Fulfilled'
                            )
                            OR
                            EXISTS (
                                SELECT 1
                                FROM dbo.Request r5
                                WHERE r5.SetId = r.SetId
                                  AND r5.ReqId <> u.ReqId
                                  AND r5.Status IN ('Fulfilled', 'Completed')
                            )
                        )
                    )
                    OR
                    (
                        (r.SetId IS NULL OR r.SetId = 0)
                        AND u.Status = 'Pending'
                        AND u.IssuedFullQty > 0
                        AND u.IssuedFullQty < u.ReturnedEmptyQty
                    )
                ORDER BY
                    COALESCE(CAST(r.SubmissionSessionId AS NVARCHAR(36)), CAST(u.UnfulfilledId AS NVARCHAR(20))),
                    u.CreatedDate ASC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(MapToDto(reader));

            return result;
        }

        public async Task<List<UnfulfilledCartridgeExchangeDto>> GetFulfilledExchangesAsync()
            => await GetExchangesByStatusAsync("Fulfilled");

        public async Task<List<UnfulfilledCartridgeExchangeDto>> GetAllExchangesAsync()
            => await GetExchangesByStatusAsync(null);

        private async Task<List<UnfulfilledCartridgeExchangeDto>> GetExchangesByStatusAsync(string? status)
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            string sql = SelectColumns + @"
                WHERE (@Status IS NULL OR u.Status = @Status)
                ORDER BY u.CreatedDate ASC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(MapToDto(reader));

            return result;
        }

        public async Task<(int PendingCount, int TotalUnfulfilledQty)> GetPendingSummaryAsync()
        {
            const string sql = @"
                SELECT
                    COUNT(*) AS PendingCount,
                    ISNULL(SUM(UnfulfilledQty), 0) AS TotalUnfulfilledQty
                FROM dbo.UnfulfilledCartridgeExchange
                WHERE Status = 'Pending'";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.GetInt32(0), reader.GetInt32(1));

            return (0, 0);
        }

        public async Task<List<(string Model, int PendingCount, int UnfulfilledQty)>> GetPendingSummaryByModelAsync()
        {
            var result = new List<(string, int, int)>();

            const string sql = @"
                SELECT
                    CartridgeModel,
                    COUNT(*) AS PendingCount,
                    SUM(UnfulfilledQty) AS TotalUnfulfilledQty
                FROM dbo.UnfulfilledCartridgeExchange
                WHERE Status = 'Pending'
                GROUP BY CartridgeModel
                ORDER BY CartridgeModel";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((
                    reader.IsDBNull(0) ? "N/A" : reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2)));
            }

            return result;
        }

        public async Task FulfillExchangeAsync(int unfulfilledId, int additionalIssuedQty, int fulfilledBy, string? fulfilledRemarks)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                const string sqlUpdateExchange = @"
                    UPDATE dbo.UnfulfilledCartridgeExchange
                    SET IssuedFullQty = ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty,
                        UnfulfilledQty = ReturnedEmptyQty - (ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty),
                        Status = CASE
                            WHEN ReturnedEmptyQty - (ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty) <= 0
                            THEN 'Fulfilled'
                            ELSE 'Pending'
                        END,
                        FulfilledDate = CASE
                            WHEN ReturnedEmptyQty - (ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty) <= 0
                            THEN GETDATE()
                            ELSE FulfilledDate
                        END,
                        FulfilledBy = @FulfilledBy,
                        FulfilledRemarks = ISNULL(@FulfilledRemarks, FulfilledRemarks)
                    WHERE UnfulfilledId = @UnfulfilledId
                      AND Status = 'Pending'";

                using (var cmd = new SqlCommand(sqlUpdateExchange, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@UnfulfilledId", unfulfilledId);
                    cmd.Parameters.AddWithValue("@AdditionalIssuedQty", additionalIssuedQty);
                    cmd.Parameters.AddWithValue("@FulfilledBy", fulfilledBy);
                    cmd.Parameters.AddWithValue("@FulfilledRemarks", (object?)fulfilledRemarks ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                int reqId = 0;
                bool isFullyFulfilled = false;
                string? cartridgeModel = null;
                int empId = 0;
                int? branchId = null;
                int? deptId = null;
                int unfulfGoodEmptyQty = 0;
                int unfulfDamagedEmptyQty = 0;

                const string sqlGetExchangeInfo = @"
                    SELECT uce.ReqId, uce.CartridgeModel,
                           CASE WHEN uce.UnfulfilledQty <= 0 AND uce.Status = 'Fulfilled' THEN 1 ELSE 0 END AS IsFullyFulfilled,
                           uce.EmpId, uce.BranchId, uce.DeptId,
                           ISNULL(uce.GoodEmptyQty, 0),
                           ISNULL(uce.DamagedEmptyQty, 0)
                    FROM dbo.UnfulfilledCartridgeExchange uce
                    WHERE uce.UnfulfilledId = @UnfulfilledId";

                using (var cmd = new SqlCommand(sqlGetExchangeInfo, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@UnfulfilledId", unfulfilledId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        reqId                 = reader.GetInt32(0);
                        cartridgeModel        = reader.IsDBNull(1) ? null : reader.GetString(1);
                        isFullyFulfilled      = reader.GetInt32(2) == 1;
                        empId                 = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        branchId              = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                        deptId                = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                        unfulfGoodEmptyQty    = reader.GetInt32(6);
                        unfulfDamagedEmptyQty = reader.GetInt32(7);
                    }
                }

                if (additionalIssuedQty > 0 && !string.IsNullOrWhiteSpace(cartridgeModel))
                {
                    int cartridgeModelId = 0;
                    bool isRefillable = false;
                    int goodConditionId = 0;
                    int damagedConditionId = 0;
                    int emptyConditionId = 0;

                    const string sqlGetConditions = @"
                        SELECT MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                               MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                               MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                        FROM dbo.Condition
                        WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')";

                    using (var cmd = new SqlCommand(sqlGetConditions, con, transaction))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            goodConditionId    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            damagedConditionId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            emptyConditionId   = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        }
                    }

                    const string sqlGetModel = @"
                        SELECT CartridgeModelId, ISNULL(IsRefillable, 0)
                        FROM dbo.CartridgeModel
                        WHERE UPPER(LTRIM(RTRIM(ModelNumber))) = UPPER(LTRIM(RTRIM(@ModelNumber)))
                          AND IsActive = 1";

                    using (var cmd = new SqlCommand(sqlGetModel, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ModelNumber", cartridgeModel);
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            cartridgeModelId = reader.GetInt32(0);
                            isRefillable     = reader.GetBoolean(1);
                        }
                    }

                    if (cartridgeModelId > 0)
                    {
                        bool hasSplit = (unfulfGoodEmptyQty + unfulfDamagedEmptyQty) == additionalIssuedQty;
                        var batches = hasSplit
                            ? new (int qty, int condId, string condStatus)[]
                              {
                                  (unfulfGoodEmptyQty,    goodConditionId    > 0 ? goodConditionId    : emptyConditionId, "GOOD"),
                                  (unfulfDamagedEmptyQty, damagedConditionId > 0 ? damagedConditionId : emptyConditionId, "DAMAGED")
                              }
                            : new (int qty, int condId, string condStatus)[]
                              {
                                  (additionalIssuedQty, emptyConditionId, "GOOD")
                              };

                        const string sqlInsertEmpty = @"
                            INSERT INTO dbo.EmptyCartridge
                                (CartridgeModelId, VendorId, VendorBatchId, Quantity, ConditionId, Status,
                                 RefillStatus, ReturnedAt, ReturnedBy, ReqId, EmpId, BranchId, DeptId,
                                 Remarks, CreatedDate, CreatedBy, ConditionStatus)
                            VALUES
                                (@CartridgeModelId, NULL, NULL, 1, @ConditionId, 'Pending',
                                 @RefillStatus, GETDATE(), @BranchId, @ReqId, @EmpId, @BranchId, @DeptId,
                                 @Remarks, GETDATE(), @CreatedBy, @ConditionStatus)";

                        foreach (var (batchQty, batchCondId, batchCondStatus) in batches)
                        {
                            for (int i = 0; i < batchQty; i++)
                            {
                                using var cmd = new SqlCommand(sqlInsertEmpty, con, transaction);
                                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                                cmd.Parameters.AddWithValue("@ConditionId",     batchCondId == 0 ? (object)DBNull.Value : (object)batchCondId);
                                cmd.Parameters.AddWithValue("@ConditionStatus", (object)batchCondStatus);
                                cmd.Parameters.AddWithValue("@RefillStatus",    isRefillable && batchCondStatus != "DAMAGED" ? (object)"For Refill" : DBNull.Value);
                                cmd.Parameters.AddWithValue("@ReqId",           reqId);
                                cmd.Parameters.AddWithValue("@EmpId",           empId);
                                cmd.Parameters.AddWithValue("@BranchId",        (object?)branchId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@DeptId",          (object?)deptId   ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Remarks",         $"Empty returned (unfulfilled exchange) - Request #{reqId}");
                                cmd.Parameters.AddWithValue("@CreatedBy",       fulfilledBy);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                if (isFullyFulfilled && reqId > 0)
                {
                    const string sqlUpdateRequestStatus = @"
                        IF NOT EXISTS (
                            SELECT 1
                            FROM dbo.UnfulfilledCartridgeExchange
                            WHERE ReqId = @ReqId
                              AND Status = 'Pending'
                        )
                        BEGIN
                            UPDATE dbo.Request
                            SET Status = 'Submitted',
                                DateModified = GETDATE(),
                                ModifiedBy = @FulfilledBy
                            WHERE ReqId = @ReqId
                              AND Status IN ('Pending', 'Under Review', 'Processing');

                            IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                            BEGIN
                                UPDATE dbo.CartridgeRequestModel
                                SET Status = 'Fulfilled',
                                    ProcessedDate = GETDATE(),
                                    ProcessedBy = @FulfilledBy
                                WHERE ReqId = @ReqId
                                  AND UPPER(LTRIM(RTRIM(CartridgeModel))) = UPPER(LTRIM(RTRIM(@CartridgeModel)))
                                  AND ISNULL(NULLIF(Status, ''), 'Pending') IN ('Pending', 'Under Review', 'Processing');
                            END
                        END";

                    using var cmd = new SqlCommand(sqlUpdateRequestStatus, con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@CartridgeModel", (object?)cartridgeModel ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FulfilledBy", fulfilledBy);
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

        public async Task<int?> GetCartridgeModelIdByModelNumberAsync(string modelNumber)
        {
            if (string.IsNullOrWhiteSpace(modelNumber))
                return null;

            const string sql = @"
                SELECT CartridgeModelId
                FROM dbo.CartridgeModel
                WHERE UPPER(LTRIM(RTRIM(ModelNumber))) = UPPER(LTRIM(RTRIM(@ModelNumber)))
                  AND IsActive = 1";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ModelNumber", modelNumber.Trim());
            await con.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : (int)result;
        }

        public async Task<int> GetAvailableIssuableStockAsync(int? cartridgeModelId)
        {
            if (!cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return 0;

            const string sql = @"
                SELECT ISNULL(SUM(ISNULL(i.StockOnHand, 0)), 0)
                FROM dbo.Item i
                WHERE i.Category = 'Cartridge'
                  AND i.CartridgeModelId = @CartridgeModelId
                  AND i.Active = 1";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
            await con.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        // PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
        // GetAvailableIssuableStockByCondition. "Brand New" = RefillStatus IS NULL (the
        // authoritative marker); "Refilled" = RefillStatus = 'Available' (set by
        // BatchAddItemDialog / CompleteRefillAndRestockAsync on desktop).
        public async Task<int> GetAvailableIssuableStockByConditionAsync(int? cartridgeModelId, string condition)
        {
            if (!cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return 0;
            if (string.IsNullOrWhiteSpace(condition))
                return 0;

            string sql;
            if (condition.Equals("Brand New", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    SELECT ISNULL(SUM(ISNULL(i.StockOnHand, 0)), 0)
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.CartridgeModelId = @CartridgeModelId
                      AND i.Active = 1
                      AND i.RefillStatus IS NULL";
            }
            else if (condition.Equals("Refilled", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    SELECT ISNULL(SUM(ISNULL(i.StockOnHand, 0)), 0)
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.CartridgeModelId = @CartridgeModelId
                      AND i.Active = 1
                      AND i.RefillStatus = 'Available'";
            }
            else
            {
                return 0;
            }

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
            await con.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private static UnfulfilledCartridgeExchangeDto MapToDto(SqlDataReader reader)
        {
            var dto = new UnfulfilledCartridgeExchangeDto
            {
                UnfulfilledId = reader.GetInt32(reader.GetOrdinal("UnfulfilledId")),
                ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                EmpId = reader.GetInt32(reader.GetOrdinal("EmpId")),
                BranchId = reader.IsDBNull(reader.GetOrdinal("BranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("BranchId")),
                DeptId = reader.IsDBNull(reader.GetOrdinal("DeptId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DeptId")),
                CartridgeModel = reader.IsDBNull(reader.GetOrdinal("CartridgeModel")) ? "N/A" : reader.GetString(reader.GetOrdinal("CartridgeModel")),
                RequestedQty = reader.GetInt32(reader.GetOrdinal("RequestedQty")),
                ReturnedEmptyQty = reader.GetInt32(reader.GetOrdinal("ReturnedEmptyQty")),
                IssuedFullQty = reader.GetInt32(reader.GetOrdinal("IssuedFullQty")),
                UnfulfilledQty = reader.GetInt32(reader.GetOrdinal("UnfulfilledQty")),
                Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "Pending" : reader.GetString(reader.GetOrdinal("Status")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                CreatedBy = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                FulfilledDate = reader.IsDBNull(reader.GetOrdinal("FulfilledDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("FulfilledDate")),
                FulfilledBy = reader.IsDBNull(reader.GetOrdinal("FulfilledBy")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("FulfilledBy")),
                FulfilledRemarks = reader.IsDBNull(reader.GetOrdinal("FulfilledRemarks")) ? null : reader.GetString(reader.GetOrdinal("FulfilledRemarks")),
                RequesterName = reader.IsDBNull(reader.GetOrdinal("RequesterName")) ? "Unknown Employee" : reader.GetString(reader.GetOrdinal("RequesterName")),
                BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName")) ? null : reader.GetString(reader.GetOrdinal("CreatedByName")),
                FulfilledByName = reader.IsDBNull(reader.GetOrdinal("FulfilledByName")) ? null : reader.GetString(reader.GetOrdinal("FulfilledByName"))
            };

            int setIdOrd = reader.GetOrdinal("SetId");
            dto.SetId = reader.IsDBNull(setIdOrd) ? (int?)null : reader.GetInt32(setIdOrd);

            int sessionOrd = reader.GetOrdinal("SubmissionSessionId");
            dto.SubmissionSessionId = reader.IsDBNull(sessionOrd) ? (Guid?)null : reader.GetGuid(sessionOrd);

            return dto;
        }
    }
}
