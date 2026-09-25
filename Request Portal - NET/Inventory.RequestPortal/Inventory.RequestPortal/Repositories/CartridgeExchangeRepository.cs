using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
    /// Straight port of the methods behind the desktop "Cartridge Exchange" page — only the
    /// modern branch (dbo.CartridgeRequestModel present) is ported; the legacy fallback branch
    /// in the desktop SQL is dead in this database (table has existed since the multi-model
    /// portal submission feature shipped).
    /// </summary>
    public class CartridgeExchangeRepository : ICartridgeExchangeRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public CartridgeExchangeRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        private static string? ExtractTypedModelFromDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return null;

            const string modelPrefix = "[MODEL:";
            int startIndex = description.IndexOf(modelPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0) return null;

            startIndex += modelPrefix.Length;
            int endIndex = description.IndexOf(']', startIndex);
            if (endIndex < 0) return null;

            string model = description.Substring(startIndex, endIndex - startIndex).Trim();
            return string.IsNullOrWhiteSpace(model) ? null : model;
        }

        public async Task<List<CartridgeRequestDto>> GetPendingCartridgeRequestsAsync()
        {
            var requests = new List<CartridgeRequestDto>();

            const string sql = @"
                SELECT
                    r.ReqId,
                    r.ItemId,
                    COALESCE(eff_cm.CartridgeModelId, i.CartridgeModelId) AS CartridgeModelId,
                    i.Name AS ItemName,
                    i.ModelNumber AS ItemModelNumber,
                    r.Description,
                    r.SubmissionSessionId,
                    crm.RequestModelId,
                    crm.CartridgeModel AS RequestedModel,
                    COALESCE(crm.RequestedQty, r.Quantity) AS Quantity,
                    COALESCE(NULLIF(crm.Remarks, ''), r.Remarks) AS ConditionType,
                    CASE
                        WHEN ISNULL(crm.GoodEmptyQty, 0) > 0 AND ISNULL(crm.DamagedEmptyQty, 0) > 0
                            THEN 'Good: ' + CAST(ISNULL(crm.GoodEmptyQty, 0) AS NVARCHAR) + ' / Damaged: ' + CAST(ISNULL(crm.DamagedEmptyQty, 0) AS NVARCHAR)
                        WHEN ISNULL(crm.DamagedEmptyQty, 0) > 0
                            THEN 'Damaged: ' + CAST(crm.DamagedEmptyQty AS NVARCHAR)
                        WHEN ISNULL(crm.GoodEmptyQty, 0) > 0
                            THEN 'Good: ' + CAST(crm.GoodEmptyQty AS NVARCHAR)
                        ELSE 'Good'
                    END AS PhysicalCondition,
                    ISNULL(crm.GoodEmptyQty, 0) AS GoodEmptyQty,
                    ISNULL(crm.DamagedEmptyQty, 0) AS DamagedEmptyQty,
                    COALESCE(NULLIF(crm.Status, ''), r.Status) AS Status,
                    r.DateCreated,
                    r.EmpId,
                    -- Dept-level requests have no requester employee (r.EmpId IS NULL).
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS EmployeeName,
                    c.Name AS CompanyName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    ISNULL((
                        SELECT COUNT(*)
                        FROM dbo.CartridgeRequestModel crm_orig
                        INNER JOIN dbo.Request r_orig ON crm_orig.ReqId = r_orig.ReqId
                        WHERE r_orig.SubmissionSessionId = r.SubmissionSessionId
                    ), 0) AS OriginalSubmissionCount,
                    CASE
                        WHEN r.Description LIKE '%PICKUP%'   THEN 'PICKUP'
                        WHEN r.Description LIKE '%DELIVERY%' THEN 'DELIVERY'
                        ELSE 'N/A'
                    END AS DistributionMethod,
                    ISNULL(recv.Name, 'N/A') AS ReceivedByName,
                    CASE
                        WHEN CHARINDEX(' | ', r.Remarks) > 0
                        THEN LTRIM(RTRIM(SUBSTRING(r.Remarks, CHARINDEX(' | ', r.Remarks) + 3, LEN(r.Remarks))))
                        ELSE NULL
                    END AS AdditionalRemarks
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Company c    ON COALESCE(e.ComId,    r.ComId)    = c.ComId
                LEFT JOIN dbo.Branch b     ON COALESCE(e.BranchId, r.BranchId) = b.BranchId
                LEFT JOIN dbo.Department d ON COALESCE(e.DeptId,   r.DeptId)   = d.DeptId
                LEFT JOIN dbo.Employee recv ON r.ReceivedById = recv.EmpId
                LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                OUTER APPLY (
                    SELECT TOP 1 cm_eff.CartridgeModelId
                    FROM dbo.CartridgeModel cm_eff
                    WHERE cm_eff.IsActive = 1
                      AND UPPER(LTRIM(RTRIM(cm_eff.ModelNumber))) = UPPER(LTRIM(RTRIM(
                          CASE
                              WHEN NULLIF(LTRIM(RTRIM(crm.CartridgeModel)), '') IS NOT NULL
                                  THEN crm.CartridgeModel
                              WHEN r.Description IS NOT NULL
                                AND CHARINDEX('[MODEL:', r.Description) > 0
                                AND CHARINDEX('[MODELS:', r.Description) = 0
                                AND CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) > 0
                              THEN LTRIM(RTRIM(SUBSTRING(
                                  r.Description,
                                  CHARINDEX('[MODEL:', r.Description) + 7,
                                  CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7)
                                    - (CHARINDEX('[MODEL:', r.Description) + 7)
                              )))
                              ELSE NULL
                          END
                      )))
                ) eff_cm
                WHERE r.WorkflowType = 'CartridgeManagement'
                  AND r.Status IN ('Pending', 'Under Review', 'Processing')
                  AND (
                       crm.RequestModelId IS NULL
                       OR ISNULL(NULLIF(crm.Status, ''), 'Pending') IN ('Pending', 'Under Review', 'Processing')
                  )
                  AND EXISTS (
                      SELECT 1 FROM dbo.CartridgeAuthorization ca
                      WHERE ca.SubmissionSessionId = r.SubmissionSessionId
                        AND ca.Status = 'Approved'
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.UnfulfilledCartridgeExchange uce
                      WHERE uce.ReqId = r.ReqId
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.CartridgeMovement cm
                      WHERE cm.Remarks LIKE '%Request #' + CAST(r.ReqId AS NVARCHAR) + '%'
                        AND cm.MovementType IN ('Issued', 'Returned')
                  )
                ORDER BY r.DateCreated ASC, r.ReqId ASC, crm.RequestModelId ASC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string? description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
                string? itemModelNumber = reader.IsDBNull(reader.GetOrdinal("ItemModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ItemModelNumber"));
                int? requestModelId = reader.IsDBNull(reader.GetOrdinal("RequestModelId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("RequestModelId"));
                string? requestedModel = reader.IsDBNull(reader.GetOrdinal("RequestedModel")) ? null : reader.GetString(reader.GetOrdinal("RequestedModel"));
                string? typedModelFromDescription = ExtractTypedModelFromDescription(description);

                string? effectiveModelNumber =
                    (!string.IsNullOrWhiteSpace(requestedModel) ? requestedModel : null)
                    ?? typedModelFromDescription
                    ?? itemModelNumber;

                requests.Add(new CartridgeRequestDto
                {
                    ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                    ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                    CartridgeModelId = reader.IsDBNull(reader.GetOrdinal("CartridgeModelId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CartridgeModelId")),
                    ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? "Unknown Item" : reader.GetString(reader.GetOrdinal("ItemName")),
                    ModelNumber = effectiveModelNumber,
                    TypedModelNumber = !string.IsNullOrWhiteSpace(requestedModel) ? requestedModel : typedModelFromDescription,
                    RequestModelId = requestModelId,
                    Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? 0 : reader.GetInt32(reader.GetOrdinal("Quantity")),
                    ConditionType = reader.IsDBNull(reader.GetOrdinal("ConditionType")) ? null : reader.GetString(reader.GetOrdinal("ConditionType")),
                    PhysicalCondition = reader.IsDBNull(reader.GetOrdinal("PhysicalCondition")) ? "Good" : reader.GetString(reader.GetOrdinal("PhysicalCondition")),
                    GoodEmptyQty = reader.IsDBNull(reader.GetOrdinal("GoodEmptyQty")) ? 0 : reader.GetInt32(reader.GetOrdinal("GoodEmptyQty")),
                    DamagedEmptyQty = reader.IsDBNull(reader.GetOrdinal("DamagedEmptyQty")) ? 0 : reader.GetInt32(reader.GetOrdinal("DamagedEmptyQty")),
                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "Pending" : reader.GetString(reader.GetOrdinal("Status")),
                    DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                    EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? 0 : reader.GetInt32(reader.GetOrdinal("EmpId")),
                    EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? "Unknown Employee" : reader.GetString(reader.GetOrdinal("EmployeeName")),
                    CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                    BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                    SubmissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId")),
                    OriginalSubmissionCount = reader.IsDBNull(reader.GetOrdinal("OriginalSubmissionCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("OriginalSubmissionCount")),
                    Description = description,
                    DistributionMethod = reader.IsDBNull(reader.GetOrdinal("DistributionMethod")) ? "N/A" : reader.GetString(reader.GetOrdinal("DistributionMethod")),
                    ReceivedByName = reader.IsDBNull(reader.GetOrdinal("ReceivedByName")) ? null : reader.GetString(reader.GetOrdinal("ReceivedByName")),
                    AdditionalRemarks = reader.IsDBNull(reader.GetOrdinal("AdditionalRemarks")) ? null : reader.GetString(reader.GetOrdinal("AdditionalRemarks"))
                });
            }

            return requests;
        }

        public async Task<int> GetAvailableIssuableStockByConditionAsync(int? cartridgeModelId, string condition)
        {
            if (!cartridgeModelId.HasValue || cartridgeModelId.Value <= 0 || string.IsNullOrWhiteSpace(condition))
                return 0;

            string sql;
            if (condition.Equals("Brand New", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    SELECT ISNULL(SUM(ISNULL(i.StockOnHand, 0)), 0)
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.CartridgeModelId = @CartridgeModelId
                      AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
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
                      AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
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

        public async Task<List<int>> GetIssuableItemIdsByConditionAsync(int? cartridgeModelId, string condition, int quantity)
        {
            var result = new List<int>();
            if (quantity <= 0 || !cartridgeModelId.HasValue || cartridgeModelId.Value <= 0 || string.IsNullOrWhiteSpace(condition))
                return result;

            string sql;
            if (condition.Equals("Brand New", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    SELECT i.ItemId, ISNULL(i.StockOnHand, 0) AS AvailableStock
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.CartridgeModelId = @CartridgeModelId
                      AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
                      AND i.Active = 1
                      AND i.RefillStatus IS NULL
                      AND ISNULL(i.StockOnHand, 0) > 0
                    ORDER BY i.DateCreated ASC, i.ItemId ASC";
            }
            else if (condition.Equals("Refilled", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"
                    SELECT i.ItemId, ISNULL(i.StockOnHand, 0) AS AvailableStock
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.CartridgeModelId = @CartridgeModelId
                      AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
                      AND i.Active = 1
                      AND i.RefillStatus = 'Available'
                      AND ISNULL(i.StockOnHand, 0) > 0
                    ORDER BY i.DateCreated ASC, i.ItemId ASC";
            }
            else
            {
                return result;
            }

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            int remaining = quantity;
            while (remaining > 0 && await reader.ReadAsync())
            {
                int itemId = reader.GetInt32(0);
                int availableStock = reader.GetInt32(1);
                if (availableStock <= 0) continue;

                int take = Math.Min(availableStock, remaining);
                for (int i = 0; i < take; i++) result.Add(itemId);
                remaining -= take;
            }

            return result;
        }

        public async Task<int> EnsureSharedSetForPortalCartridgeGroupAsync(int reqId, int userId)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                int? existingSetId = null;
                Guid? submissionSessionId = null;

                using (var cmd = new SqlCommand("SELECT r.SetId, r.SubmissionSessionId FROM dbo.Request r WHERE r.ReqId = @ReqId", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        existingSetId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                        submissionSessionId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
                    }
                }

                if (existingSetId.HasValue && existingSetId.Value > 0)
                {
                    transaction.Commit();
                    return existingSetId.Value;
                }

                if (!submissionSessionId.HasValue)
                {
                    transaction.Commit();
                    return 0;
                }

                var relatedReqIds = new List<int>();
                int? groupSetId = null;

                const string sqlFindRelated = @"
                    SELECT r.ReqId, r.SetId
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    WHERE r.SubmissionSessionId = @SubmissionSessionId
                      AND i.Category = 'Cartridge'
                    ORDER BY r.DateCreated ASC, r.ReqId ASC";

                using (var cmd = new SqlCommand(sqlFindRelated, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId.Value);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int relatedReqId = reader.GetInt32(0);
                        int? relatedSetId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                        relatedReqIds.Add(relatedReqId);
                        if (!groupSetId.HasValue && relatedSetId.HasValue && relatedSetId.Value > 0)
                            groupSetId = relatedSetId.Value;
                    }
                }

                if (!groupSetId.HasValue)
                {
                    const string sqlCreateSet = @"
                        DECLARE @NewSetIds TABLE (SetId INT);
                        INSERT INTO dbo.[Set] (CreatedBy, CreatedAt, Remarks, Status)
                        OUTPUT INSERTED.SetId INTO @NewSetIds
                        VALUES (@CreatedBy, GETDATE(), @Remarks, 'Pending');
                        SELECT SetId FROM @NewSetIds;";

                    using (var cmd = new SqlCommand(sqlCreateSet, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@CreatedBy", userId);
                        cmd.Parameters.AddWithValue("@Remarks", $"Multi-model cartridge exchange - Session {submissionSessionId.Value:N}");
                        groupSetId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    const string sqlUpdateSetReqId = @"
                        UPDATE s
                        SET s.ReqId = @FirstReqId, s.SetType = 'Cartridge'
                        FROM dbo.[Set] s
                        WHERE s.SetId = @SetId AND s.ReqId IS NULL";

                    using (var cmd = new SqlCommand(sqlUpdateSetReqId, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", groupSetId.Value);
                        cmd.Parameters.AddWithValue("@FirstReqId", relatedReqIds.FirstOrDefault());
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                foreach (var relReqId in relatedReqIds)
                {
                    using var cmd = new SqlCommand(
                        "UPDATE dbo.Request SET SetId = @SetId WHERE ReqId = @ReqId AND (SetId IS NULL OR SetId = 0)", con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", relReqId);
                    cmd.Parameters.AddWithValue("@SetId", groupSetId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return groupSetId.Value;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static async Task DecreaseItemStockAsync(SqlConnection con, SqlTransaction transaction, int itemId, int quantity, int userId)
        {
            const string sql = @"
                UPDATE dbo.Item
                SET StockOnHand = CASE WHEN StockOnHand >= @Quantity THEN StockOnHand - @Quantity ELSE 0 END,
                    DateModified = GETDATE(),
                    ModifiedBy = @UserId
                WHERE ItemId = @ItemId";

            using var cmd = new SqlCommand(sql, con, transaction);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@Quantity", quantity);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<int> GetCartridgeTypeIdForItemAsync(SqlConnection con, SqlTransaction transaction, int itemId)
        {
            using (var cmd = new SqlCommand("SELECT CartridgeTypeId FROM dbo.Cartridge WHERE ItemId = @ItemId AND IsActive = 1", con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) return (int)result;
            }

            using (var cmd = new SqlCommand("SELECT CartridgeTypeId FROM dbo.CartridgeType WHERE CartridgeTypeName = 'Refill'", con, transaction))
            {
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) return (int)result;
            }

            return 1;
        }

        private static async Task RecordCartridgeMovementAsync(
            SqlConnection con, SqlTransaction transaction, int itemId, string movementType, int quantity,
            int empId, int? branchId, int? deptId, string conditionType, string remarks, int userId)
        {
            int cartridgeTypeId = await GetCartridgeTypeIdForItemAsync(con, transaction, itemId);

            const string sql = @"
                INSERT INTO dbo.CartridgeMovement
                    (ItemId, CartridgeTypeId, MovementType, Quantity,
                     EmployeeId, BranchId, DeptId, ConditionType,
                     Remarks, CreatedAt, CreatedBy)
                VALUES
                    (@ItemId, @CartridgeTypeId, @MovementType, @Quantity,
                     @EmployeeId, @BranchId, @DeptId, @ConditionType,
                     @Remarks, GETDATE(), @CreatedBy)";

            using var cmd = new SqlCommand(sql, con, transaction);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
            cmd.Parameters.AddWithValue("@MovementType", movementType);
            cmd.Parameters.AddWithValue("@Quantity", quantity);
            cmd.Parameters.AddWithValue("@EmployeeId", empId);
            cmd.Parameters.AddWithValue("@BranchId", (object?)branchId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DeptId", (object?)deptId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ConditionType", conditionType);
            cmd.Parameters.AddWithValue("@Remarks", remarks);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> FulfillCartridgeExchangeByConditionAsync(
            int reqId, int returnedQuantity,
            List<int> issuedBrandNewIds, List<int> issuedRefilledIds,
            int issuedBrandNewQty, int issuedRefilledQty,
            int userId, int? requestModelId, string? fulfillmentRemarks)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                int empId = 0;
                int? branchId = null;
                int? deptId = null;
                int? existingSetId = null;

                // dbo.UnfulfilledCartridgeExchange.EmpId has an FK to dbo.Employee and is NOT NULL,
                // but a request's own EmpId can be NULL or orphaned (dept-level, or a simulated /
                // test request). Resolve a guaranteed-valid EmpId: the requester → the pickup
                // receiver → the IT user who is doing the fulfillment. LEFT JOIN so branch/dept
                // and SetId are still read even when the requester isn't a live Employee.
                using (var cmd = new SqlCommand(@"
                    SELECT
                        COALESCE(
                            (SELECT e1.EmpId FROM dbo.Employee e1 WHERE e1.EmpId = r.EmpId),
                            (SELECT e2.EmpId FROM dbo.Employee e2 WHERE e2.EmpId = r.ReceivedById),
                            (SELECT TOP 1 u.EmpId FROM dbo.[User] u WHERE u.UserId = @UserId AND u.EmpId IS NOT NULL),
                            0
                        ) AS ResolvedEmpId,
                        COALESCE(e.BranchId, r.BranchId) AS BranchId,
                        COALESCE(e.DeptId,   r.DeptId)   AS DeptId,
                        r.SetId
                    FROM dbo.Request r
                    LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                    WHERE r.ReqId = @ReqId", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        empId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        branchId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                        deptId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                        existingSetId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                    }
                }

                if (!existingSetId.HasValue || existingSetId.Value == 0)
                {
                    // NOTE: this helper opens its OWN connection/transaction and commits
                    // independently — matching desktop's EnsureSharedSetForPortalCartridgeGroup
                    // exactly (a pre-existing quirk, not something introduced here). If it commits
                    // and the outer transaction below later rolls back, this linkage can survive.
                    int groupSetId = await EnsureSharedSetForPortalCartridgeGroupAsync(reqId, userId);
                    if (groupSetId > 0) existingSetId = groupSetId;
                }

                int setId;
                if (existingSetId.HasValue && existingSetId.Value > 0)
                {
                    setId = existingSetId.Value;

                    using var cmd = new SqlCommand(@"
                        UPDATE dbo.[Set]
                        SET IssuedBrandNewQty = ISNULL(IssuedBrandNewQty, 0) + @IssuedBrandNewQty,
                            IssuedRefilledQty = ISNULL(IssuedRefilledQty, 0) + @IssuedRefilledQty
                        WHERE SetId = @SetId", con, transaction);
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    cmd.Parameters.AddWithValue("@IssuedBrandNewQty", issuedBrandNewQty);
                    cmd.Parameters.AddWithValue("@IssuedRefilledQty", issuedRefilledQty);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var setRemarks = string.IsNullOrWhiteSpace(fulfillmentRemarks)
                        ? $"Cartridge Exchange - Request #{reqId}"
                        : fulfillmentRemarks.Trim();

                    using (var cmd = new SqlCommand(@"
                        DECLARE @NewSetIds TABLE (SetId INT);
                        INSERT INTO dbo.[Set] (CreatedBy, CreatedAt, Remarks, Status, IssuedBrandNewQty, IssuedRefilledQty)
                        OUTPUT INSERTED.SetId INTO @NewSetIds
                        VALUES (@CreatedBy, GETDATE(), @Remarks, @Status, @IssuedBrandNewQty, @IssuedRefilledQty);
                        SELECT SetId FROM @NewSetIds;", con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@CreatedBy", userId);
                        cmd.Parameters.AddWithValue("@Remarks", setRemarks);
                        cmd.Parameters.AddWithValue("@Status", "Pending");
                        cmd.Parameters.AddWithValue("@IssuedBrandNewQty", issuedBrandNewQty);
                        cmd.Parameters.AddWithValue("@IssuedRefilledQty", issuedRefilledQty);
                        setId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    using (var cmd = new SqlCommand("UPDATE dbo.Request SET SetId = @SetId WHERE ReqId = @ReqId", con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ReqId", reqId);
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = new SqlCommand(@"
                        UPDATE s
                        SET s.ReqId = @ReqId, s.SetType = 'Cartridge'
                        FROM dbo.[Set] s
                        INNER JOIN dbo.Request r ON r.ReqId = @ReqId
                        INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                        WHERE s.SetId = @SetId AND s.ReqId IS NULL", con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ReqId", reqId);
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                foreach (var itemId in issuedBrandNewIds)
                {
                    await DecreaseItemStockAsync(con, transaction, itemId, 1, userId);
                    await RecordCartridgeMovementAsync(con, transaction, itemId, "Issued", 1,
                        empId, branchId, deptId, "Brand New", $"Issued Brand New - Request #{reqId}", userId);
                }

                foreach (var itemId in issuedRefilledIds)
                {
                    await DecreaseItemStockAsync(con, transaction, itemId, 1, userId);
                    await RecordCartridgeMovementAsync(con, transaction, itemId, "Issued", 1,
                        empId, branchId, deptId, "Refilled", $"Issued Refilled - Request #{reqId}", userId);
                }

                var allIssuedIds = new List<int>();
                allIssuedIds.AddRange(issuedBrandNewIds);
                allIssuedIds.AddRange(issuedRefilledIds);

                var returnedItemIds = new List<int>();
                if (allIssuedIds.Count > 0)
                {
                    if (returnedQuantity > 0 && allIssuedIds.Count >= returnedQuantity)
                        returnedItemIds = allIssuedIds.Take(returnedQuantity).ToList();
                    else
                        returnedItemIds.Add(allIssuedIds[0]);
                }

                int totalIssuedQty = issuedBrandNewQty + issuedRefilledQty;

                if (totalIssuedQty > 0 && returnedItemIds.Count > 0)
                {
                    int cartridgeModelId = 0;
                    int conditionId = 0, goodConditionId = 0, damagedConditionId = 0;
                    int goodEmptyQty = 0, damagedEmptyQty = 0;

                    using (var cmd = new SqlCommand(@"
                        SELECT
                            MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                            MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                            MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                        FROM dbo.Condition
                        WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')", con, transaction))
                    {
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            goodConditionId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            damagedConditionId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            conditionId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        }
                    }

                    if (requestModelId.HasValue && requestModelId.Value > 0)
                    {
                        using var cmd = new SqlCommand(@"
                            SELECT TOP 1 cm.CartridgeModelId, ISNULL(crm.GoodEmptyQty, 0), ISNULL(crm.DamagedEmptyQty, 0)
                            FROM dbo.CartridgeRequestModel crm
                            INNER JOIN dbo.CartridgeModel cm ON LTRIM(RTRIM(crm.CartridgeModel)) = cm.ModelNumber
                            WHERE crm.RequestModelId = @RequestModelId", con, transaction);
                        cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            cartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            goodEmptyQty = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            damagedEmptyQty = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        }
                    }

                    if (cartridgeModelId <= 0)
                    {
                        using var cmd = new SqlCommand("SELECT i.CartridgeModelId FROM dbo.Item i WHERE i.ItemId = @ItemId", con, transaction);
                        cmd.Parameters.AddWithValue("@ItemId", returnedItemIds[0]);
                        var result = await cmd.ExecuteScalarAsync();
                        cartridgeModelId = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }

                    // One EmptyCartridge row per returned unit, split by the declared Good/Damaged counts when
                    // they add up to the issued quantity (legacy single EMPTY batch otherwise).
                    // RecordReturnedEmptiesAsync is shared with mixed-submission cartridge lines.
                    if (cartridgeModelId > 0)
                    {
                        bool hasSplit = totalIssuedQty > 0 && (goodEmptyQty + damagedEmptyQty) == totalIssuedQty;
                        var emptyBatches = hasSplit
                            ? new (int qty, int condId, string condStatus)[]
                              {
                                  (goodEmptyQty,    goodConditionId    > 0 ? goodConditionId    : conditionId, "GOOD"),
                                  (damagedEmptyQty, damagedConditionId > 0 ? damagedConditionId : conditionId, "DAMAGED")
                              }
                            : new (int qty, int condId, string condStatus)[] { (totalIssuedQty, conditionId, "GOOD") };

                        await RecordReturnedEmptiesAsync(con, transaction, reqId, cartridgeModelId, emptyBatches,
                            returnedItemIds[0], empId, branchId, deptId, userId, conditionId);
                    }
                }

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.Request
                    SET Status = CASE
                        WHEN @TotalIssuedQty >= Quantity THEN 'Fulfilled'
                        WHEN @TotalIssuedQty > 0 THEN 'Partially Fulfilled'
                        ELSE 'Unfulfilled'
                    END
                    WHERE ReqId = @ReqId;

                    IF EXISTS (SELECT 1 FROM dbo.Request WHERE ReqId = @ReqId AND SubmissionSessionId IS NOT NULL)
                    BEGIN
                        DECLARE @SessionId UNIQUEIDENTIFIER;
                        SELECT @SessionId = SubmissionSessionId FROM dbo.Request WHERE ReqId = @ReqId;

                        DECLARE @SessionFulfilled INT = (
                            SELECT COUNT(*) FROM dbo.Request
                            WHERE SubmissionSessionId = @SessionId AND Status = 'Fulfilled');
                        DECLARE @SessionTotal INT = (
                            SELECT COUNT(*) FROM dbo.Request
                            WHERE SubmissionSessionId = @SessionId);

                        IF @SessionFulfilled > 0 AND @SessionFulfilled < @SessionTotal
                        BEGIN
                            UPDATE dbo.Request
                            SET Status = 'Partially Fulfilled'
                            WHERE SubmissionSessionId = @SessionId
                              AND Status IN ('Unfulfilled', 'Under Review', 'Processing');
                        END
                    END", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@TotalIssuedQty", totalIssuedQty);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(@"
                    DECLARE @TotalRequests INT = 0;
                    DECLARE @FulfilledRequests INT = 0;

                    SELECT @TotalRequests = COUNT(*) FROM dbo.Request WHERE SetId = @SetId;
                    SELECT @FulfilledRequests = COUNT(*) FROM dbo.Request WHERE SetId = @SetId AND Status IN ('Fulfilled', 'Completed');

                    UPDATE dbo.[Set]
                    SET Status = CASE
                        WHEN @FulfilledRequests = @TotalRequests THEN 'Dispatched'
                        WHEN @FulfilledRequests > 0 THEN 'Partial'
                        ELSE 'Pending'
                    END
                    WHERE SetId = @SetId;", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM dbo.SetItem WHERE SetId = @SetId)
                    BEGIN
                        INSERT dbo.SetItem (
                            SetId, ItemId, ItemCode, Description,
                            Quantity, UnitOfMeasure, UnitPrice, Amount,
                            LineStartDate, LineEndDate, CreatedBy
                        )
                        SELECT
                            r.SetId,
                            r.ItemId,
                            COALESCE(
                                NULLIF(LTRIM(RTRIM(CASE
                                    WHEN OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                                    THEN (
                                        SELECT TOP 1 LTRIM(RTRIM(crm.CartridgeModel))
                                        FROM dbo.CartridgeRequestModel crm
                                        WHERE crm.ReqId = r.ReqId
                                        ORDER BY crm.RequestModelId
                                    )
                                    ELSE NULL
                                END)), ''),
                                NULLIF(LTRIM(RTRIM(CASE
                                    WHEN r.Description LIKE '%[MODEL:%'
                                         AND CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) > 0
                                    THEN SUBSTRING(
                                        r.Description,
                                        CHARINDEX('[MODEL:', r.Description) + 7,
                                        CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7)
                                            - (CHARINDEX('[MODEL:', r.Description) + 7)
                                    )
                                    ELSE NULL
                                END)), ''),
                                NULLIF(cm.ModelNumber, ''),
                                NULLIF(i.ModelNumber, '')
                            ) AS ItemCode,
                            CASE
                                WHEN i.Category = 'Cartridge' THEN
                                    'Cartridge Exchange - ' + COALESCE(
                                        NULLIF(LTRIM(RTRIM(CASE
                                            WHEN OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                                            THEN (
                                                SELECT TOP 1 LTRIM(RTRIM(crm.CartridgeModel))
                                                FROM dbo.CartridgeRequestModel crm
                                                WHERE crm.ReqId = r.ReqId
                                                ORDER BY crm.RequestModelId
                                            )
                                            ELSE NULL
                                        END)), ''),
                                        NULLIF(LTRIM(RTRIM(CASE
                                            WHEN r.Description LIKE '%[MODEL:%'
                                                 AND CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) > 0
                                            THEN SUBSTRING(
                                                r.Description,
                                                CHARINDEX('[MODEL:', r.Description) + 7,
                                                CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7)
                                                    - (CHARINDEX('[MODEL:', r.Description) + 7)
                                            )
                                            ELSE NULL
                                        END)), ''),
                                        NULLIF(cm.ModelNumber, ''),
                                        NULLIF(i.ModelNumber, ''),
                                        'Unknown Model'
                                    )
                                ELSE ISNULL(i.Name, 'Unknown Item')
                            END AS Description,
                            r.Quantity,
                            'Unit' AS UnitOfMeasure,
                            r.UnitPrice,
                            r.Quantity * r.UnitPrice,
                            r.DateRequested,
                            NULL,
                            r.CreatedBy
                        FROM dbo.Request r
                        LEFT JOIN dbo.Item i ON i.ItemId = r.ItemId
                        LEFT JOIN dbo.CartridgeModel cm ON i.CartridgeModelId = cm.CartridgeModelId
                        WHERE r.SetId = @SetId;

                        IF EXISTS (
                            SELECT 1
                            FROM dbo.SetItem si
                            INNER JOIN dbo.Item i ON i.ItemId = si.ItemId
                            WHERE si.SetId = @SetId
                              AND i.Category = 'Cartridge'
                              AND (si.ItemCode IS NULL OR LTRIM(RTRIM(si.ItemCode)) = '')
                        )
                        BEGIN
                            RAISERROR('SetItem materialization failed: cartridge item has no model number. Ensure CartridgeModel is configured before fulfilling.', 16, 1);
                        END
                    END", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    await cmd.ExecuteNonQueryAsync();
                }

                int unfulfilledQty = returnedQuantity - totalIssuedQty;
                if (unfulfilledQty > 0)
                {
                    if (empId <= 0)
                        throw new InvalidOperationException(
                            $"Cannot record the unfulfilled portion of request #{reqId}: no employee could be " +
                            "resolved (the request has no linked employee or receiver, and the fulfilling user " +
                            "has no employee record).");

                    string unfulfCartridgeModel = string.Empty;
                    int unfulfGoodEmptyQty = 0, unfulfDamagedEmptyQty = 0;

                    if (requestModelId.HasValue && requestModelId.Value > 0)
                    {
                        using var cmd = new SqlCommand(@"
                            SELECT crm.CartridgeModel, ISNULL(crm.GoodEmptyQty, 0), ISNULL(crm.DamagedEmptyQty, 0)
                            FROM dbo.CartridgeRequestModel crm
                            WHERE crm.RequestModelId = @RequestModelId", con, transaction);
                        cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            unfulfCartridgeModel = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                            unfulfGoodEmptyQty = reader.GetInt32(1);
                            unfulfDamagedEmptyQty = reader.GetInt32(2);
                        }
                    }

                    using var insCmd = new SqlCommand(@"
                        INSERT INTO dbo.UnfulfilledCartridgeExchange
                            (ReqId, EmpId, BranchId, DeptId, CartridgeModel,
                             RequestedQty, ReturnedEmptyQty, IssuedFullQty, UnfulfilledQty,
                             GoodEmptyQty, DamagedEmptyQty,
                             Remarks, Status, CreatedDate, CreatedBy)
                        VALUES
                            (@ReqId, @EmpId, @BranchId, @DeptId, @CartridgeModel,
                             @RequestedQty, @ReturnedEmptyQty, @IssuedFullQty, @UnfulfilledQty,
                             @GoodEmptyQty, @DamagedEmptyQty,
                             @Remarks, 'Pending', GETDATE(), @CreatedBy)", con, transaction);
                    insCmd.Parameters.AddWithValue("@ReqId", reqId);
                    insCmd.Parameters.AddWithValue("@EmpId", empId);
                    insCmd.Parameters.AddWithValue("@BranchId", (object?)branchId ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@DeptId", (object?)deptId ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@CartridgeModel", string.IsNullOrWhiteSpace(unfulfCartridgeModel) ? (object)DBNull.Value : unfulfCartridgeModel);
                    insCmd.Parameters.AddWithValue("@RequestedQty", returnedQuantity);
                    insCmd.Parameters.AddWithValue("@ReturnedEmptyQty", returnedQuantity);
                    insCmd.Parameters.AddWithValue("@IssuedFullQty", totalIssuedQty);
                    insCmd.Parameters.AddWithValue("@UnfulfilledQty", unfulfilledQty);
                    insCmd.Parameters.AddWithValue("@GoodEmptyQty", unfulfGoodEmptyQty);
                    insCmd.Parameters.AddWithValue("@DamagedEmptyQty", unfulfDamagedEmptyQty);
                    insCmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(fulfillmentRemarks) ? $"Unfulfilled - Request #{reqId}" : fulfillmentRemarks);
                    insCmd.Parameters.AddWithValue("@CreatedBy", userId);
                    await insCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return setId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Records returned empty cartridges: one dbo.EmptyCartridge row per unit (Good units of
        /// refillable models are queued 'For Refill'), an IT custody Item for non-refillable models,
        /// and a Returned movement anchored on <paramref name="anchorItemId"/>. Shared by the
        /// Cartridge Exchange and cartridge lines of mixed submissions.
        /// MATCHES: Yakult.Inventory.App CartridgeManagementRepository.RecordReturnedEmpties
        /// </summary>
        private static async Task RecordReturnedEmptiesAsync(
            SqlConnection con, SqlTransaction transaction, int reqId, int cartridgeModelId,
            IEnumerable<(int qty, int condId, string condStatus)> batches,
            int anchorItemId, int empId, int? branchId, int? deptId, int userId, int emptyConditionId)
        {
            var batchList = batches.Where(b => b.qty > 0).ToList();
            int totalQty = batchList.Sum(b => b.qty);
            if (cartridgeModelId <= 0 || totalQty <= 0)
                return;

            bool isRefillableModel = false;
            string modelNumber = string.Empty;
            using (var cmd = new SqlCommand("SELECT IsRefillable, ModelNumber FROM dbo.CartridgeModel WHERE CartridgeModelId = @ModelId", con, transaction))
            {
                cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    isRefillableModel = !reader.IsDBNull(0) && reader.GetBoolean(0);
                    modelNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                }
            }

            int? itCustodyItemId = null;
            if (!isRefillableModel)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Item
                        (Name, Description, Active, UnitOfMeasure, StockOnHand,
                         DateCreated, CreatedBy, DateModified, ModifiedBy,
                         Category, CategoryId, ModelNumber, CartridgeModelId,
                         ConditionID, VendorId, RefillStatus, Remarks,
                         Amount, ItemType, StartDate, AffectsInventory, IsTrackedAsset)
                    SELECT
                        @Name, @Description, 1, 'Unit', @Quantity,
                        GETDATE(), @CreatedBy, GETDATE(), @CreatedBy,
                        'Cartridge',
                        (SELECT TOP 1 CategoryId FROM dbo.Item WHERE Category = 'Cartridge' AND CategoryId IS NOT NULL),
                        @ModelNumber, @CartridgeModelId,
                        COALESCE(@ConditionID, (SELECT TOP 1 ConditionId FROM dbo.Condition ORDER BY ConditionId ASC)),
                        NULL, NULL, @Remarks,
                        0, 'Hardware', GETDATE(), 1, 0;
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", con, transaction);
                cmd.Parameters.AddWithValue("@Name", $"{modelNumber} - Returned Empty");
                cmd.Parameters.AddWithValue("@Description", $"Returned empty cartridge (non-refillable) - IT custody - Request #{reqId}");
                cmd.Parameters.AddWithValue("@Quantity", totalQty);
                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                cmd.Parameters.AddWithValue("@ModelNumber", modelNumber);
                cmd.Parameters.AddWithValue("@ConditionID", emptyConditionId > 0 ? (object)emptyConditionId : DBNull.Value);
                cmd.Parameters.AddWithValue("@Remarks", $"Non-refillable - IT custody - Request #{reqId}");
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                var idResult = await cmd.ExecuteScalarAsync();
                itCustodyItemId = idResult != null && idResult != DBNull.Value ? Convert.ToInt32(idResult) : (int?)null;
            }

            const string sqlInsertEmpty = @"
                INSERT INTO dbo.EmptyCartridge
                    (CartridgeModelId, VendorId, VendorBatchId, Quantity, ConditionId, Status,
                     RefillStatus, ReturnedAt, ReturnedBy, ReqId, EmpId, BranchId, DeptId,
                     Remarks, CreatedDate, CreatedBy, SourceItemId, ConditionStatus)
                VALUES
                    (@CartridgeModelId, NULL, NULL, 1, @ConditionId, 'Pending',
                     @RefillStatus, GETDATE(), @ReturnedBy, @ReqId, @EmpId, @BranchId, @DeptId,
                     @Remarks, GETDATE(), @CreatedBy, @SourceItemId, @ConditionStatus)";

            foreach (var (batchQty, batchCondId, batchCondStatus) in batchList)
            {
                for (int i = 0; i < batchQty; i++)
                {
                    using var cmd = new SqlCommand(sqlInsertEmpty, con, transaction);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                    cmd.Parameters.AddWithValue("@ConditionId", batchCondId == 0 ? (object)DBNull.Value : batchCondId);
                    cmd.Parameters.AddWithValue("@ConditionStatus", (object)batchCondStatus);
                    cmd.Parameters.AddWithValue("@RefillStatus", isRefillableModel && batchCondStatus != "DAMAGED" ? (object)"For Refill" : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReturnedBy", empId > 0 ? (object)empId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@EmpId", empId > 0 ? (object)empId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchId", (object?)branchId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DeptId", (object?)deptId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", $"Empty returned - Request #{reqId}");
                    cmd.Parameters.AddWithValue("@CreatedBy", userId);
                    cmd.Parameters.AddWithValue("@SourceItemId", itCustodyItemId.HasValue ? (object)itCustodyItemId.Value : DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await RecordCartridgeMovementAsync(con, transaction, anchorItemId, "Returned", totalQty,
                empId, branchId, deptId, "EMPTY", $"Returned {totalQty} Empty Cartridge(s) - Request #{reqId}", userId);
        }

        // ── Cartridge lines inside MIXED portal submissions ──────────────────────────
        // MATCHES: Yakult.Inventory.App CartridgeManagementRepository (GetMixedCartridgeLineInfo /
        // IssueMixedCartridgeLine). A mixed submission belongs to Request & Set Management and is
        // tracked by Request.IssuedQty, but its cartridge lines are still exchanges: Brand New /
        // Refilled units picked FIFO, a movement per unit, and the requester's empties returned.

        private const string SqlResolveMixedCartridgeLine = @"
            SELECT
                r.ReqId,
                r.Quantity,
                ISNULL(r.IssuedQty, 0) AS IssuedQty,
                CAST(CASE WHEN i.Category = 'Cartridge'
                           AND r.WorkflowType = 'RequestSetManagement'
                           AND r.SubmissionSessionId IS NOT NULL
                          THEN 1 ELSE 0 END AS BIT) AS IsExchangeLine,
                COALESCE(cm_tag.CartridgeModelId, i.CartridgeModelId) AS CartridgeModelId,
                COALESCE(cm_tag.ModelNumber, cm_item.ModelNumber, NULLIF(i.ModelNumber, ''), i.Name) AS ModelNumber
            FROM dbo.Request r
            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
            LEFT JOIN dbo.CartridgeModel cm_item ON cm_item.CartridgeModelId = i.CartridgeModelId
            OUTER APPLY (
                SELECT CASE
                    WHEN CHARINDEX('[MODEL:', r.Description) > 0
                     AND CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) > 0
                    THEN LTRIM(RTRIM(SUBSTRING(
                            r.Description,
                            CHARINDEX('[MODEL:', r.Description) + 7,
                            CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7)
                                - (CHARINDEX('[MODEL:', r.Description) + 7))))
                END AS Tag
            ) t
            OUTER APPLY (
                SELECT TOP 1 cm.CartridgeModelId, cm.ModelNumber
                FROM dbo.CartridgeModel cm
                WHERE LTRIM(RTRIM(cm.ModelNumber)) = COALESCE(
                          (SELECT TOP 1 NULLIF(LTRIM(RTRIM(crm.CartridgeModel)), '')
                           FROM dbo.CartridgeRequestModel crm
                           WHERE crm.ReqId = r.ReqId
                           ORDER BY crm.RequestModelId),
                          t.Tag)
                ORDER BY cm.IsActive DESC, cm.CartridgeModelId
            ) cm_tag
            WHERE r.ReqId = @ReqId;

            -- Declared empties: the requester's Good / Damaged counts, kept in
            -- dbo.CartridgeRequestModel for every portal cartridge line (cartridge-only or mixed).
            SELECT ISNULL(SUM(crm.GoodEmptyQty), 0), ISNULL(SUM(crm.DamagedEmptyQty), 0)
            FROM dbo.CartridgeRequestModel crm
            WHERE crm.ReqId = @ReqId;

            SELECT
                ISNULL(SUM(CASE WHEN ec.ConditionStatus = 'DAMAGED' THEN ec.Quantity ELSE 0 END), 0),
                ISNULL(SUM(CASE WHEN ec.ConditionStatus = 'DAMAGED' THEN 0 ELSE ec.Quantity END), 0)
            FROM dbo.EmptyCartridge ec
            WHERE ec.ReqId = @ReqId;";

        private static async Task<MixedCartridgeLineInfo?> ReadMixedCartridgeLineAsync(SqlConnection con, SqlTransaction? transaction, int reqId)
        {
            using var cmd = new SqlCommand(SqlResolveMixedCartridgeLine, con, transaction);
            cmd.Parameters.AddWithValue("@ReqId", reqId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var info = new MixedCartridgeLineInfo
            {
                ReqId            = reader.GetInt32(0),
                Quantity         = reader.GetInt32(1),
                IssuedQty        = reader.GetInt32(2),
                IsExchangeLine   = reader.GetBoolean(3),
                CartridgeModelId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                ModelNumber      = reader.IsDBNull(5) ? null : reader.GetString(5)
            };

            if (await reader.NextResultAsync() && await reader.ReadAsync())
            {
                info.DeclaredGood    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                info.DeclaredDamaged = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }
            if (await reader.NextResultAsync() && await reader.ReadAsync())
            {
                info.ReturnedDamaged = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                info.ReturnedGood    = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }
            return info;
        }

        public async Task<MixedCartridgeLineInfo?> GetMixedCartridgeLineInfoAsync(int reqId)
        {
            MixedCartridgeLineInfo? info;
            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                await con.OpenAsync();
                info = await ReadMixedCartridgeLineAsync(con, null, reqId);
            }

            if (info != null && info.IsExchangeLine && info.CartridgeModelId.HasValue)
            {
                info.AvailableBrandNew = Math.Max(0, await GetAvailableIssuableStockByConditionAsync(info.CartridgeModelId, "Brand New"));
                info.AvailableRefilled = Math.Max(0, await GetAvailableIssuableStockByConditionAsync(info.CartridgeModelId, "Refilled"));
            }
            return info;
        }

        public async Task IssueMixedCartridgeLineAsync(int reqId, int issuedBrandNewQty, int issuedRefilledQty, int userId, string? remarks)
        {
            if (issuedBrandNewQty < 0 || issuedRefilledQty < 0)
                throw new ArgumentException("Issued quantities cannot be negative.");
            int totalIssued = issuedBrandNewQty + issuedRefilledQty;
            if (totalIssued <= 0)
                return;

            await RequestFulfillmentRepository.EnsurePortalRequestApprovedAsync(
                _connectionStringProvider.GetConnectionString(), reqId);

            var preview = await GetMixedCartridgeLineInfoAsync(reqId)
                ?? throw new InvalidOperationException($"Request #{reqId} was not found.");
            if (!preview.IsExchangeLine)
                throw new InvalidOperationException($"Request #{reqId} is not a cartridge line of a mixed portal submission.");
            if (!preview.CartridgeModelId.HasValue)
                throw new InvalidOperationException(
                    $"Request #{reqId}: the cartridge model '{preview.ModelNumber}' is not registered in Cartridge Models, so no stock can be issued for it.");

            var bnIds = await GetIssuableItemIdsByConditionAsync(preview.CartridgeModelId, "Brand New", issuedBrandNewQty);
            var rfIds = await GetIssuableItemIdsByConditionAsync(preview.CartridgeModelId, "Refilled", issuedRefilledQty);
            if (bnIds.Count < issuedBrandNewQty)
                throw new InvalidOperationException($"Only {bnIds.Count} Brand New {preview.ModelNumber} in stock (tried to issue {issuedBrandNewQty}).");
            if (rfIds.Count < issuedRefilledQty)
                throw new InvalidOperationException($"Only {rfIds.Count} Refilled {preview.ModelNumber} in stock (tried to issue {issuedRefilledQty}).");

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();
            try
            {
                using (var cmd = new SqlCommand(
                    "SELECT Quantity, ISNULL(IssuedQty, 0) FROM dbo.Request WITH (UPDLOCK, ROWLOCK) WHERE ReqId = @ReqId",
                    con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException($"Request #{reqId} was not found.");
                    int pending = reader.GetInt32(0) - reader.GetInt32(1);
                    if (totalIssued > pending)
                        throw new InvalidOperationException(
                            $"Request #{reqId} only has {Math.Max(0, pending)} unit(s) pending (tried to issue {totalIssued}). Refresh and try again.");
                }

                var line = (await ReadMixedCartridgeLineAsync(con, transaction, reqId))!;

                int empId = 0;
                int? branchId = null;
                int? deptId = null;
                using (var cmd = new SqlCommand(@"
                    SELECT
                        COALESCE(
                            (SELECT e1.EmpId FROM dbo.Employee e1 WHERE e1.EmpId = r.EmpId),
                            (SELECT e2.EmpId FROM dbo.Employee e2 WHERE e2.EmpId = r.ReceivedById),
                            (SELECT TOP 1 u.EmpId FROM dbo.[User] u WHERE u.UserId = @UserId AND u.EmpId IS NOT NULL),
                            0
                        ) AS ResolvedEmpId,
                        COALESCE(e.BranchId, r.BranchId) AS BranchId,
                        COALESCE(e.DeptId,   r.DeptId)   AS DeptId
                    FROM dbo.Request r
                    LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                    WHERE r.ReqId = @ReqId", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        empId    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        branchId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                        deptId   = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                    }
                }

                foreach (var itemId in bnIds)
                {
                    await DecreaseItemStockAsync(con, transaction, itemId, 1, userId);
                    await RecordCartridgeMovementAsync(con, transaction, itemId, "Issued", 1,
                        empId, branchId, deptId, "Brand New", $"Issued Brand New - Request #{reqId}", userId);
                }

                foreach (var itemId in rfIds)
                {
                    await DecreaseItemStockAsync(con, transaction, itemId, 1, userId);
                    await RecordCartridgeMovementAsync(con, transaction, itemId, "Issued", 1,
                        empId, branchId, deptId, "Refilled", $"Issued Refilled - Request #{reqId}", userId);
                }

                // One empty back per full cartridge issued: declared Good first, then declared
                // Damaged, then any undeclared unit (recorded like an undeclared Cartridge Exchange).
                int goodConditionId = 0, damagedConditionId = 0, emptyConditionId = 0;
                using (var cmd = new SqlCommand(@"
                    SELECT
                        MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                        MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                        MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                    FROM dbo.Condition
                    WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')", con, transaction))
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        goodConditionId    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        damagedConditionId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        emptyConditionId   = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    }
                }

                int goodNow    = Math.Min(totalIssued, Math.Max(0, line.DeclaredGood - line.ReturnedGood));
                int damagedNow = Math.Min(totalIssued - goodNow, Math.Max(0, line.DeclaredDamaged - line.ReturnedDamaged));
                int plainNow   = totalIssued - goodNow - damagedNow;

                var batches = new (int qty, int condId, string condStatus)[]
                {
                    (goodNow,    goodConditionId    > 0 ? goodConditionId    : emptyConditionId, "GOOD"),
                    (damagedNow, damagedConditionId > 0 ? damagedConditionId : emptyConditionId, "DAMAGED"),
                    (plainNow,   emptyConditionId,                                               "GOOD")
                };

                int anchorItemId = bnIds.Count > 0 ? bnIds[0] : rfIds[0];
                await RecordReturnedEmptiesAsync(con, transaction, reqId, preview.CartridgeModelId.Value, batches,
                    anchorItemId, empId, branchId, deptId, userId, emptyConditionId);

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.Request
                    SET IssuedQty     = ISNULL(IssuedQty, 0) + @Issued,
                        Remarks       = ISNULL(@Remarks, Remarks),
                        DateModified  = (SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time'),
                        ModifiedBy    = @ModifiedBy
                    WHERE ReqId = @ReqId", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@Issued", totalIssued);
                    cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                    cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim());
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

        public async Task<List<FulfilledCartridgeRowDto>> GetFulfilledCartridgeHistoryAsync()
        {
            var list = new List<FulfilledCartridgeRowDto>();

            const string sql = @"
                SELECT TOP 500
                    s.SetId,
                    ISNULL(s.SetCode, '') AS SetCode,
                    s.CreatedAt           AS FulfilledAt,
                    TOP_REQ.ReqId,
                    ISNULL(req_emp.Name,  '') AS RequesterName,
                    ISNULL(co.Name,       '') AS CompanyName,
                    ISNULL(b.Name,        '') AS BranchName,
                    ISNULL(d.Name,        '') AS DepartmentName,
                    ISNULL(NULLIF(LTRIM(RTRIM(CRM_MODEL.CartridgeModel)), ''), '') AS CartridgeModel,
                    ISNULL(CRM_MODEL.RequestedQty, ISNULL(TOP_REQ.Quantity, 0)) AS Quantity,
                    ISNULL(recv_emp.Name, '') AS ReceivedByName,
                    ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0) AS TotalIssuedQty
                FROM dbo.[Set] s
                CROSS APPLY (
                    SELECT TOP 1 r.ReqId, r.EmpId, r.ReceivedById, r.Description, r.Quantity,
                                 r.ComId, r.DeptId, r.BranchId
                    FROM dbo.Request r
                    WHERE r.SetId = s.SetId
                    ORDER BY r.ReqId
                ) AS TOP_REQ
                OUTER APPLY (
                    SELECT STRING_AGG(crm.CartridgeModel, ', ') WITHIN GROUP (ORDER BY crm.CartridgeModel) AS CartridgeModel,
                           SUM(crm.RequestedQty) AS RequestedQty
                    FROM dbo.CartridgeRequestModel crm
                    WHERE crm.ReqId = TOP_REQ.ReqId
                ) AS CRM_MODEL
                LEFT JOIN dbo.Employee  req_emp  ON req_emp.EmpId  = TOP_REQ.EmpId
                LEFT JOIN dbo.Company   co       ON co.ComId       = ISNULL(req_emp.ComId,  TOP_REQ.ComId)
                LEFT JOIN dbo.Branch    b        ON b.BranchId     = ISNULL(req_emp.BranchId, TOP_REQ.BranchId)
                LEFT JOIN dbo.Department d       ON d.DeptId       = ISNULL(req_emp.DeptId,  TOP_REQ.DeptId)
                LEFT JOIN dbo.Employee  recv_emp ON recv_emp.EmpId = TOP_REQ.ReceivedById
                WHERE TOP_REQ.Description LIKE '%[[]PORTAL]%'
                  AND s.CreatedAt >= DATEADD(day, -365, GETDATE())
                  AND (ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0) > 0
                       OR ISNULL(s.Status, '') = 'Dispatched')
                ORDER BY s.CreatedAt DESC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FulfilledCartridgeRowDto
                {
                    SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                    SetCode = reader.GetString(reader.GetOrdinal("SetCode")),
                    FulfilledAt = reader.IsDBNull(reader.GetOrdinal("FulfilledAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("FulfilledAt")),
                    ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                    RequesterName = reader.GetString(reader.GetOrdinal("RequesterName")),
                    CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    CartridgeModel = reader.GetString(reader.GetOrdinal("CartridgeModel")),
                    Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                    ReceivedByName = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                    TotalIssuedQty = reader.GetInt32(reader.GetOrdinal("TotalIssuedQty"))
                });
            }

            return list;
        }

        public async Task<FulfilledCartridgeDetailDto?> GetFulfilledCartridgeDetailAsync(int setId)
        {
            const string headerSql = @"
                SELECT
                    s.SetId,
                    ISNULL(s.SetCode, '') AS SetCode,
                    s.CreatedAt           AS FulfilledAt,
                    TOP_REQ.ReqId,
                    ISNULL(req_emp.Name,  '') AS RequesterName,
                    ISNULL(co.Name,       '') AS CompanyName,
                    ISNULL(b.Name,        '') AS BranchName,
                    ISNULL(d.Name,        '') AS DepartmentName,
                    ISNULL(recv_emp.Name, '') AS ReceivedByName,
                    ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0) AS TotalIssuedQty
                FROM dbo.[Set] s
                CROSS APPLY (
                    SELECT TOP 1 r.ReqId, r.EmpId, r.ReceivedById, r.ComId, r.DeptId, r.BranchId
                    FROM dbo.Request r
                    WHERE r.SetId = s.SetId
                    ORDER BY r.ReqId
                ) AS TOP_REQ
                LEFT JOIN dbo.Employee  req_emp  ON req_emp.EmpId  = TOP_REQ.EmpId
                LEFT JOIN dbo.Company   co       ON co.ComId       = ISNULL(req_emp.ComId,  TOP_REQ.ComId)
                LEFT JOIN dbo.Branch    b        ON b.BranchId     = ISNULL(req_emp.BranchId, TOP_REQ.BranchId)
                LEFT JOIN dbo.Department d       ON d.DeptId       = ISNULL(req_emp.DeptId,  TOP_REQ.DeptId)
                LEFT JOIN dbo.Employee  recv_emp ON recv_emp.EmpId = TOP_REQ.ReceivedById
                WHERE s.SetId = @SetId";

            const string modelsSql = @"
                SELECT crm.CartridgeModel, crm.RequestedQty, ISNULL(crm.GoodEmptyQty, 0) AS GoodEmptyQty,
                       ISNULL(crm.DamagedEmptyQty, 0) AS DamagedEmptyQty, ISNULL(crm.Status, '') AS Status
                FROM dbo.Request r
                INNER JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                WHERE r.SetId = @SetId
                ORDER BY crm.CartridgeModel";

            FulfilledCartridgeDetailDto? detail = null;

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();

            using (var cmd = new SqlCommand(headerSql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    detail = new FulfilledCartridgeDetailDto
                    {
                        SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                        SetCode = reader.GetString(reader.GetOrdinal("SetCode")),
                        FulfilledAt = reader.IsDBNull(reader.GetOrdinal("FulfilledAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("FulfilledAt")),
                        ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                        RequesterName = reader.GetString(reader.GetOrdinal("RequesterName")),
                        CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
                        BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                        DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                        ReceivedByName = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                        TotalIssuedQty = reader.GetInt32(reader.GetOrdinal("TotalIssuedQty"))
                    };
                }
            }

            if (detail == null) return null;

            using (var cmd = new SqlCommand(modelsSql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    detail.Models.Add(new FulfilledCartridgeModelLineDto
                    {
                        CartridgeModel = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        RequestedQty = reader.GetInt32(1),
                        GoodEmptyQty = reader.GetInt32(2),
                        DamagedEmptyQty = reader.GetInt32(3),
                        Status = reader.GetString(4)
                    });
                }
            }

            return detail;
        }

        private static async Task<bool> TableExistsAsync(SqlConnection con, SqlTransaction transaction, string fullTableName)
        {
            const string sql = "SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NULL THEN 0 ELSE 1 END";
            using var cmd = new SqlCommand(sql, con, transaction);
            cmd.Parameters.AddWithValue("@TableName", fullTableName);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
        }

        public async Task<(bool Success, string Message)> ForceDeleteCartridgeRequestAsync(int reqId, int userId)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                string? category;
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 i.Category
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                    WHERE r.ReqId = @ReqId", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    var result = await cmd.ExecuteScalarAsync();
                    category = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                }

                if (string.IsNullOrWhiteSpace(category))
                {
                    transaction.Rollback();
                    return (false, "Request not found.");
                }

                if (!string.Equals(category, "Cartridge", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Rollback();
                    return (false, "Selected request is not a cartridge request.");
                }

                int lineCount = 0;
                if (await TableExistsAsync(con, transaction, "dbo.CartridgeRequestModel"))
                {
                    using var cmd = new SqlCommand(@"
                        SELECT RequestModelId FROM dbo.CartridgeRequestModel
                        WHERE ReqId = @ReqId ORDER BY RequestModelId", con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) lineCount++;
                }

                // Both remark formats used by FulfillCartridgeExchange:
                //   Individual:   "Issued to employee - Request #{reqId}"
                //   Aggregated:   "Issued {qty} cartridge(s) to employee - Request #{reqId}"
                // Both end with "to employee - Request #{reqId}", so match on that suffix.
                string issuedRemarksPattern = $"%to employee%Request #{reqId}%";

                var issuedMovements = new List<(int ItemId, int Qty)>();
                using (var cmd = new SqlCommand(@"
                    SELECT cm.ItemId, cm.Quantity
                    FROM dbo.CartridgeMovement cm
                    WHERE cm.MovementType = 'Issued'
                      AND cm.Remarks LIKE @RemarksPattern", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@RemarksPattern", issuedRemarksPattern);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        issuedMovements.Add((reader.GetInt32(0), reader.GetInt32(1)));
                }

                foreach (var (itemId, qty) in issuedMovements)
                {
                    // Mirror the exact inverse of DecreaseItemStock: add back what was subtracted.
                    // Do NOT touch Active or RefillStatus — those are not modified on issue.
                    using var cmd = new SqlCommand(@"
                        UPDATE dbo.Item
                        SET StockOnHand = ISNULL(StockOnHand, 0) + @Qty,
                            DateModified = GETDATE(),
                            ModifiedBy = @UserId
                        WHERE ItemId = @ItemId", con, transaction);
                    cmd.Parameters.AddWithValue("@Qty", qty);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Reverse VendorCartridgeBatch.ReturnedQty before deleting EmptyCartridge rows.
                // FulfillCartridgeExchange increments ReturnedQty per batch; undo that here.
                if (await TableExistsAsync(con, transaction, "dbo.EmptyCartridge") &&
                    await TableExistsAsync(con, transaction, "dbo.VendorCartridgeBatch"))
                {
                    var batchQtys = new List<(int BatchId, int Qty)>();
                    using (var cmd = new SqlCommand(@"
                        SELECT VendorBatchId, SUM(Quantity) AS TotalQty
                        FROM dbo.EmptyCartridge
                        WHERE ReqId = @ReqId
                          AND VendorBatchId IS NOT NULL
                        GROUP BY VendorBatchId", con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ReqId", reqId);
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                            batchQtys.Add((reader.GetInt32(0), reader.GetInt32(1)));
                    }

                    foreach (var (batchId, qty) in batchQtys)
                    {
                        using var cmd = new SqlCommand(@"
                            UPDATE dbo.VendorCartridgeBatch
                            SET ReturnedQty = CASE WHEN ReturnedQty >= @Qty THEN ReturnedQty - @Qty ELSE 0 END
                            WHERE BatchId = @BatchId", con, transaction);
                        cmd.Parameters.AddWithValue("@BatchId", batchId);
                        cmd.Parameters.AddWithValue("@Qty", qty);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // Delete auto-recorded returned-empty rows from EmptyCartridge.
                if (await TableExistsAsync(con, transaction, "dbo.EmptyCartridge"))
                {
                    using var cmd = new SqlCommand("DELETE FROM dbo.EmptyCartridge WHERE ReqId = @ReqId", con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Delete all CartridgeMovement rows tied to this request (both issue and return).
                // FulfillCartridgeExchange uses "Request #{reqId}" in every remark it writes.
                using (var cmd = new SqlCommand(@"
                    DELETE FROM dbo.CartridgeMovement
                    WHERE MovementType IN ('Issued', 'Returned')
                      AND Remarks LIKE @RequestPattern", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@RequestPattern", $"%Request #{reqId}%");
                    await cmd.ExecuteNonQueryAsync();
                }

                if (await TableExistsAsync(con, transaction, "dbo.Inventory"))
                {
                    using var cmd = new SqlCommand("DELETE FROM dbo.Inventory WHERE ReqId = @ReqId", con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (await TableExistsAsync(con, transaction, "dbo.UnfulfilledCartridgeExchange"))
                {
                    using var cmd = new SqlCommand("DELETE FROM dbo.UnfulfilledCartridgeExchange WHERE ReqId = @ReqId", con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (await TableExistsAsync(con, transaction, "dbo.CartridgeRequestModel"))
                {
                    using var cmd = new SqlCommand("DELETE FROM dbo.CartridgeRequestModel WHERE ReqId = @ReqId", con, transaction);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    await cmd.ExecuteNonQueryAsync();
                }

                int deleted;
                using (var cmd = new SqlCommand("DELETE FROM dbo.Request WHERE ReqId = @ReqId", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    deleted = await cmd.ExecuteNonQueryAsync();
                }

                if (deleted == 0)
                {
                    transaction.Rollback();
                    return (false, "Request not found or already deleted.");
                }

                transaction.Commit();

                int issuedCount = issuedMovements.Sum(x => x.Qty);
                return (true, $"Force delete completed. Lines: {lineCount}, Issued restored: {issuedCount}.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error during force delete: {ex.Message}");
            }
        }
    }
}
