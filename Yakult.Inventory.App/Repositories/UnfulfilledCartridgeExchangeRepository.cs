using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for UnfulfilledCartridgeExchange operations.
    /// Supports FIFO fulfillment ordering based on CreatedDate.
    /// </summary>
    public sealed class UnfulfilledCartridgeExchangeRepository
    {
        private readonly string _connectionString;

        public UnfulfilledCartridgeExchangeRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");
        }

        /// <summary>
        /// Creates an unfulfilled cartridge exchange record.
        /// Called when IssuedFullQty < ReturnedEmptyQty.
        /// </summary>
        public int CreateUnfulfilledExchange(
            int reqId,
            int empId,
            int? branchId,
            int? deptId,
            string cartridgeModel,
            int requestedQty,
            int returnedEmptyQty,
            int issuedFullQty,
            string remarks,
            int createdBy,
            int goodEmptyQty = 0,
            int damagedEmptyQty = 0)
        {
            int unfulfilledQty = returnedEmptyQty - issuedFullQty;

            const string sql = @"
                INSERT INTO dbo.UnfulfilledCartridgeExchange
                    (ReqId, EmpId, BranchId, DeptId, CartridgeModel,
                     RequestedQty, ReturnedEmptyQty, IssuedFullQty, UnfulfilledQty,
                     GoodEmptyQty, DamagedEmptyQty,
                     Remarks, Status, CreatedDate, CreatedBy)
                OUTPUT INSERTED.UnfulfilledId
                VALUES
                    (@ReqId, @EmpId, @BranchId, @DeptId, @CartridgeModel,
                     @RequestedQty, @ReturnedEmptyQty, @IssuedFullQty, @UnfulfilledQty,
                     @GoodEmptyQty, @DamagedEmptyQty,
                     @Remarks, 'Pending', GETDATE(), @CreatedBy)";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                cmd.Parameters.AddWithValue("@EmpId", empId);
                cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel ?? "N/A");
                cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);
                cmd.Parameters.AddWithValue("@ReturnedEmptyQty", returnedEmptyQty);
                cmd.Parameters.AddWithValue("@IssuedFullQty", issuedFullQty);
                cmd.Parameters.AddWithValue("@UnfulfilledQty", unfulfilledQty);
                cmd.Parameters.AddWithValue("@GoodEmptyQty", goodEmptyQty);
                cmd.Parameters.AddWithValue("@DamagedEmptyQty", damagedEmptyQty);
                cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

                con.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        public void UpsertPendingExchange(
            int reqId,
            int empId,
            int? branchId,
            int? deptId,
            string cartridgeModel,
            int requestedQty,
            int returnedEmptyQty,
            int issuedFullQty,
            string remarks,
            int createdBy,
            int goodEmptyQty = 0,
            int damagedEmptyQty = 0)
        {
            int unfulfilledQty = returnedEmptyQty - issuedFullQty;
            if (unfulfilledQty <= 0)
            {
                ClosePendingExchange(reqId, cartridgeModel, issuedFullQty, returnedEmptyQty, createdBy, remarks);
                return;
            }

            const string sql = @"
                IF EXISTS (
                    SELECT 1
                    FROM dbo.UnfulfilledCartridgeExchange
                    WHERE ReqId = @ReqId
                      AND Status = 'Pending'
                      AND UPPER(LTRIM(RTRIM(CartridgeModel))) = UPPER(LTRIM(RTRIM(@CartridgeModel)))
                )
                BEGIN
                    UPDATE dbo.UnfulfilledCartridgeExchange
                    SET EmpId = @EmpId,
                        BranchId = @BranchId,
                        DeptId = @DeptId,
                        RequestedQty = @RequestedQty,
                        ReturnedEmptyQty = @ReturnedEmptyQty,
                        IssuedFullQty = @IssuedFullQty,
                        UnfulfilledQty = @UnfulfilledQty,
                        GoodEmptyQty = @GoodEmptyQty,
                        DamagedEmptyQty = @DamagedEmptyQty,
                        Remarks = @Remarks,
                        CreatedBy = @CreatedBy
                    WHERE ReqId = @ReqId
                      AND Status = 'Pending'
                      AND UPPER(LTRIM(RTRIM(CartridgeModel))) = UPPER(LTRIM(RTRIM(@CartridgeModel)));
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.UnfulfilledCartridgeExchange
                        (ReqId, EmpId, BranchId, DeptId, CartridgeModel,
                         RequestedQty, ReturnedEmptyQty, IssuedFullQty, UnfulfilledQty,
                         GoodEmptyQty, DamagedEmptyQty,
                         Remarks, Status, CreatedDate, CreatedBy)
                    VALUES
                        (@ReqId, @EmpId, @BranchId, @DeptId, @CartridgeModel,
                         @RequestedQty, @ReturnedEmptyQty, @IssuedFullQty, @UnfulfilledQty,
                         @GoodEmptyQty, @DamagedEmptyQty,
                         @Remarks, 'Pending', GETDATE(), @CreatedBy);
                END";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                cmd.Parameters.AddWithValue("@EmpId", empId);
                cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel ?? "N/A");
                cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);
                cmd.Parameters.AddWithValue("@ReturnedEmptyQty", returnedEmptyQty);
                cmd.Parameters.AddWithValue("@IssuedFullQty", issuedFullQty);
                cmd.Parameters.AddWithValue("@UnfulfilledQty", unfulfilledQty);
                cmd.Parameters.AddWithValue("@GoodEmptyQty", goodEmptyQty);
                cmd.Parameters.AddWithValue("@DamagedEmptyQty", damagedEmptyQty);
                cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Closes a pending exchange by marking it as fully fulfilled.
        ///
        /// CRITICAL WORKFLOW FIX: Also updates parent Request.Status to 'Submitted'
        /// when all exchanges for the request are fulfilled, ensuring fulfilled requests
        /// never reappear in the pending requests queue.
        /// </summary>
        public void ClosePendingExchange(
            int reqId,
            string cartridgeModel,
            int issuedFullQty,
            int returnedEmptyQty,
            int fulfilledBy,
            string fulfilledRemarks = null)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Close the unfulfilled exchange
                        const string sqlCloseExchange = @"
                            UPDATE dbo.UnfulfilledCartridgeExchange
                            SET IssuedFullQty = @IssuedFullQty,
                                UnfulfilledQty = 0,
                                Status = 'Fulfilled',
                                FulfilledDate = GETDATE(),
                                FulfilledBy = @FulfilledBy,
                                FulfilledRemarks = ISNULL(@FulfilledRemarks, FulfilledRemarks)
                            WHERE ReqId = @ReqId
                              AND Status = 'Pending'
                              AND UPPER(LTRIM(RTRIM(CartridgeModel))) = UPPER(LTRIM(RTRIM(@CartridgeModel)))";

                        using (var cmd = new SqlCommand(sqlCloseExchange, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel ?? "");
                            cmd.Parameters.AddWithValue("@IssuedFullQty", issuedFullQty);
                            cmd.Parameters.AddWithValue("@FulfilledBy", fulfilledBy);
                            cmd.Parameters.AddWithValue("@FulfilledRemarks", (object)fulfilledRemarks ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // CRITICAL WORKFLOW FIX: Update Request status to 'Fulfilled' if all exchanges fulfilled
                        const string sqlUpdateRequestStatus = @"
                            -- Check if all UnfulfilledCartridgeExchange records for this request are fulfilled
                            IF NOT EXISTS (
                                SELECT 1
                                FROM dbo.UnfulfilledCartridgeExchange
                                WHERE ReqId = @ReqId
                                  AND Status = 'Pending'
                            )
                            BEGIN
                                -- All exchanges fulfilled, mark request as Fulfilled
                                UPDATE dbo.Request
                                SET Status = 'Fulfilled',
                                    DateModified = GETDATE(),
                                    ModifiedBy = @FulfilledBy
                                WHERE ReqId = @ReqId
                                  AND Status IN ('Pending', 'Under Review', 'Processing', 'Unfulfilled', 'Partially Fulfilled');

                                -- Also update CartridgeRequestModel if it exists
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

                        using (var cmd = new SqlCommand(sqlUpdateRequestStatus, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel ?? "");
                            cmd.Parameters.AddWithValue("@FulfilledBy", fulfilledBy);
                            cmd.ExecuteNonQuery();
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
        /// Gets all pending unfulfilled exchanges ordered by FIFO (oldest first).
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetPendingExchanges()
        {
            return GetExchangesByStatus("Pending");
        }

        /// <summary>
        /// Gets only "purely" pending exchanges — those that have NOT received any
        /// partial issuance and are NOT part of a session where a sibling row is
        /// already Fulfilled.
        ///
        /// Rows that satisfy the partial-fulfillment criteria belong on the
        /// Partially Fulfilled page and are excluded here to avoid double-listing.
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetPurelyUnfulfilledExchanges()
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            const string sql = @"
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
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS RequesterName,
                    b.Name  AS BranchName,
                    d.Name  AS DepartmentName,
                    uc.Name AS CreatedByName,
                    uf.Name AS FulfilledByName,
                    r.SetId,
                    r.SubmissionSessionId
                FROM dbo.UnfulfilledCartridgeExchange u
                LEFT  JOIN dbo.Employee   e  ON u.EmpId     = e.EmpId
                LEFT  JOIN dbo.Branch     b  ON u.BranchId  = b.BranchId
                LEFT  JOIN dbo.Department d  ON u.DeptId    = d.DeptId
                LEFT  JOIN dbo.[User]     uc ON u.CreatedBy = uc.UserId
                LEFT  JOIN dbo.[User]     uf ON u.FulfilledBy = uf.UserId
                LEFT  JOIN dbo.Request    r  ON u.ReqId     = r.ReqId
                WHERE u.Status = 'Pending'
                  -- Exclude: this row already received a partial issuance
                  AND u.IssuedFullQty = 0
                  -- Exclude: part of a Set where a sibling row is Fulfilled via the queue
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.UnfulfilledCartridgeExchange u2
                      INNER JOIN dbo.Request r2 ON u2.ReqId = r2.ReqId
                      WHERE r.SetId IS NOT NULL AND r.SetId > 0
                        AND r2.SetId = r.SetId
                        AND u2.Status = 'Fulfilled'
                  )
                  -- Exclude: part of a Set where another request was fully served on the
                  --          first pass (FulfillCartridgeExchangeByCondition sets Status =
                  --          'Fulfilled' when TotalIssuedQty >= Quantity).
                  --          This is the multi-model case: Model1 fully filled, Model2 pending.
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Request r5
                      WHERE r.SetId IS NOT NULL AND r.SetId > 0
                        AND r5.SetId = r.SetId
                        AND r5.ReqId <> u.ReqId
                        AND r5.Status IN ('Fulfilled', 'Completed')
                  )
                ORDER BY u.CreatedDate ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(MapToDtoWithSession(reader));
                }
            }

            return result;
        }

        /// <summary>
        /// Gets partially fulfilled exchanges at the REQUEST level.
        /// A pending row is included if:
        ///   (a) The row itself has been partially issued (IssuedFullQty &gt; 0), OR
        ///   (b) Another model in the same multi-model request has already been fulfilled
        ///       — meaning this unfulfilled model is the "remaining" part of a partial request.
        /// Ordered by CreatedDate ASC (FIFO).
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetPartiallyFulfilledExchanges()
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            const string sql = @"
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
                    -- Dept-level requests (r.EmpId IS NULL) have no requester employee; the row's
                    -- EmpId is only a stand-in so the FK is satisfied, so label it instead.
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS RequesterName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    uc.Name AS CreatedByName,
                    uf.Name AS FulfilledByName
                FROM dbo.UnfulfilledCartridgeExchange u
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                LEFT JOIN dbo.Request  r ON u.ReqId = r.ReqId
                LEFT JOIN dbo.Branch b ON u.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON u.DeptId = d.DeptId
                LEFT JOIN dbo.[User] uc ON u.CreatedBy = uc.UserId
                LEFT JOIN dbo.[User] uf ON u.FulfilledBy = uf.UserId
                WHERE u.Status = 'Pending'
                  AND (
                    -- Single-model partial: this row itself received some cartridges
                    (u.IssuedFullQty > 0 AND u.IssuedFullQty < u.ReturnedEmptyQty)
                    OR
                    -- Multi-model partial: a sibling model in the same request was already fulfilled
                    EXISTS (
                        SELECT 1
                        FROM dbo.UnfulfilledCartridgeExchange u2
                        WHERE u2.ReqId = u.ReqId
                          AND u2.Status = 'Fulfilled'
                    )
                  )
                ORDER BY u.CreatedDate ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(MapToDto(reader));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all fulfilled exchanges.
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetFulfilledExchanges()
        {
            return GetExchangesByStatus("Fulfilled");
        }

        /// <summary>
        /// Gets all exchanges (pending and fulfilled).
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetAllExchanges()
        {
            return GetExchangesByStatus(null);
        }

        private List<UnfulfilledCartridgeExchangeDto> GetExchangesByStatus(string status)
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            string sql = @"
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
                    -- Dept-level requests (r.EmpId IS NULL) have no requester employee; the row's
                    -- EmpId is only a stand-in so the FK is satisfied, so label it instead.
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS RequesterName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    uc.Name AS CreatedByName,
                    uf.Name AS FulfilledByName
                FROM dbo.UnfulfilledCartridgeExchange u
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                LEFT JOIN dbo.Request  r ON u.ReqId = r.ReqId
                LEFT JOIN dbo.Branch b ON u.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON u.DeptId = d.DeptId
                LEFT JOIN dbo.[User] uc ON u.CreatedBy = uc.UserId
                LEFT JOIN dbo.[User] uf ON u.FulfilledBy = uf.UserId
                WHERE (@Status IS NULL OR u.Status = @Status)
                ORDER BY u.CreatedDate ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Status", (object)status ?? DBNull.Value);

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(MapToDto(reader));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets pending unfulfilled exchanges for a specific cartridge model (FIFO order).
        /// Used for fulfilling pending exchanges when stock becomes available.
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetPendingExchangesByModel(string cartridgeModel)
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            const string sql = @"
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
                    -- Dept-level requests (r.EmpId IS NULL) have no requester employee; the row's
                    -- EmpId is only a stand-in so the FK is satisfied, so label it instead.
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS RequesterName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    uc.Name AS CreatedByName,
                    uf.Name AS FulfilledByName
                FROM dbo.UnfulfilledCartridgeExchange u
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                LEFT JOIN dbo.Request  r ON u.ReqId = r.ReqId
                LEFT JOIN dbo.Branch b ON u.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON u.DeptId = d.DeptId
                LEFT JOIN dbo.[User] uc ON u.CreatedBy = uc.UserId
                LEFT JOIN dbo.[User] uf ON u.FulfilledBy = uf.UserId
                WHERE u.Status = 'Pending'
                  AND UPPER(LTRIM(RTRIM(u.CartridgeModel))) = UPPER(LTRIM(RTRIM(@CartridgeModel)))
                ORDER BY u.CreatedDate ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel ?? "");

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(MapToDto(reader));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a single unfulfilled exchange by ID.
        /// </summary>
        public UnfulfilledCartridgeExchangeDto GetById(int unfulfilledId)
        {
            const string sql = @"
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
                    -- Dept-level requests (r.EmpId IS NULL) have no requester employee; the row's
                    -- EmpId is only a stand-in so the FK is satisfied, so label it instead.
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS RequesterName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    uc.Name AS CreatedByName,
                    uf.Name AS FulfilledByName
                FROM dbo.UnfulfilledCartridgeExchange u
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                LEFT JOIN dbo.Request  r ON u.ReqId = r.ReqId
                LEFT JOIN dbo.Branch b ON u.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON u.DeptId = d.DeptId
                LEFT JOIN dbo.[User] uc ON u.CreatedBy = uc.UserId
                LEFT JOIN dbo.[User] uf ON u.FulfilledBy = uf.UserId
                WHERE u.UnfulfilledId = @UnfulfilledId";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@UnfulfilledId", unfulfilledId);

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapToDto(reader);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Fulfills a pending exchange (partially or fully).
        /// Updates IssuedFullQty and recalculates UnfulfilledQty.
        /// If UnfulfilledQty becomes 0, marks as Fulfilled.
        ///
        /// CRITICAL WORKFLOW FIX: Also updates parent Request.Status to 'Submitted'
        /// when the exchange is fully fulfilled, ensuring fulfilled requests
        /// never reappear in the pending requests queue.
        /// </summary>
        public void FulfillExchange(
            int unfulfilledId,
            int additionalIssuedQty,
            int fulfilledBy,
            string fulfilledRemarks = null)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Update the UnfulfilledCartridgeExchange record
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
                            cmd.Parameters.AddWithValue("@FulfilledRemarks", (object)fulfilledRemarks ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // CRITICAL WORKFLOW FIX: Get ReqId and check if fully fulfilled
                        int reqId = 0;
                        bool isFullyFulfilled = false;
                        string cartridgeModel = null;
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
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
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
                        }

                        // Insert EmptyCartridge rows for the empties returned with this fulfillment,
                        // splitting Good vs Damaged using the stored breakdown from the original request.
                        if (additionalIssuedQty > 0 && !string.IsNullOrWhiteSpace(cartridgeModel))
                        {
                            int cartridgeModelId  = 0;
                            bool isRefillable     = false;
                            int goodConditionId   = 0;
                            int damagedConditionId = 0;
                            int emptyConditionId  = 0;

                            const string sqlGetConditions = @"
                                SELECT MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                                       MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                                       MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                                FROM dbo.Condition
                                WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')";

                            using (var cmd = new SqlCommand(sqlGetConditions, con, transaction))
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
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
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        cartridgeModelId = reader.GetInt32(0);
                                        isRefillable     = reader.GetBoolean(1);
                                    }
                                }
                            }

                            if (cartridgeModelId > 0)
                            {
                                // Use stored Good/Damaged split when the qtys exactly match what's being issued.
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
                                        using (var cmd = new SqlCommand(sqlInsertEmpty, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                                            cmd.Parameters.AddWithValue("@ConditionId",     batchCondId == 0 ? (object)DBNull.Value : (object)batchCondId);
                                            cmd.Parameters.AddWithValue("@ConditionStatus", (object)batchCondStatus);
                                            cmd.Parameters.AddWithValue("@RefillStatus",    isRefillable && batchCondStatus != "DAMAGED" ? (object)"For Refill" : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@ReqId",           reqId);
                                            cmd.Parameters.AddWithValue("@EmpId",           empId);
                                            cmd.Parameters.AddWithValue("@BranchId",        (object)branchId ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@DeptId",          (object)deptId   ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@Remarks",         $"Empty returned (unfulfilled exchange) - Request #{reqId}");
                                            cmd.Parameters.AddWithValue("@CreatedBy",       fulfilledBy);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        // CRITICAL WORKFLOW FIX: If this exchange is fully fulfilled,
                        // check if ALL exchanges for this request are fulfilled
                        // If so, update Request.Status to 'Submitted' to prevent it from
                        // reappearing in the pending requests queue
                        if (isFullyFulfilled && reqId > 0)
                        {
                            // For multi-model requests, check if ALL models are fulfilled
                            const string sqlUpdateRequestStatus = @"
                                -- Check if all UnfulfilledCartridgeExchange records for this request are fulfilled
                                IF NOT EXISTS (
                                    SELECT 1
                                    FROM dbo.UnfulfilledCartridgeExchange
                                    WHERE ReqId = @ReqId
                                      AND Status = 'Pending'
                                )
                                BEGIN
                                    -- All exchanges fulfilled, mark request as Submitted
                                    UPDATE dbo.Request
                                    SET Status = 'Submitted',
                                        DateModified = GETDATE(),
                                        ModifiedBy = @FulfilledBy
                                    WHERE ReqId = @ReqId
                                      AND Status IN ('Pending', 'Under Review', 'Processing');

                                    -- Also update CartridgeRequestModel if it exists
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

                            using (var cmd = new SqlCommand(sqlUpdateRequestStatus, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@CartridgeModel", (object)cartridgeModel ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@FulfilledBy", fulfilledBy);
                                cmd.ExecuteNonQuery();
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
        /// Fulfills pending exchanges for a model using FIFO order.
        /// Distributes available stock across pending exchanges oldest-first.
        /// Returns the number of cartridges actually issued.
        /// </summary>
        public int FulfillPendingExchangesFifo(
            string cartridgeModel,
            int availableStock,
            int fulfilledBy,
            string fulfilledRemarks = null)
        {
            if (availableStock <= 0)
                return 0;

            int totalIssued = 0;
            int remainingStock = availableStock;

            var pendingExchanges = GetPendingExchangesByModel(cartridgeModel);

            foreach (var exchange in pendingExchanges)
            {
                if (remainingStock <= 0)
                    break;

                int toIssue = Math.Min(exchange.UnfulfilledQty, remainingStock);
                if (toIssue > 0)
                {
                    FulfillExchange(exchange.UnfulfilledId, toIssue, fulfilledBy, fulfilledRemarks);
                    totalIssued += toIssue;
                    remainingStock -= toIssue;
                }
            }

            return totalIssued;
        }

        /// <summary>
        /// Gets summary statistics for unfulfilled exchanges.
        /// </summary>
        public (int PendingCount, int TotalUnfulfilledQty) GetPendingSummary()
        {
            const string sql = @"
                SELECT 
                    COUNT(*) AS PendingCount,
                    ISNULL(SUM(UnfulfilledQty), 0) AS TotalUnfulfilledQty
                FROM dbo.UnfulfilledCartridgeExchange
                WHERE Status = 'Pending'";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (
                            reader.GetInt32(0),
                            reader.GetInt32(1)
                        );
                    }
                }
            }

            return (0, 0);
        }

        /// <summary>
        /// Gets pending summary by cartridge model.
        /// </summary>
        public List<(string Model, int PendingCount, int UnfulfilledQty)> GetPendingSummaryByModel()
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

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add((
                            reader.IsDBNull(0) ? "N/A" : reader.GetString(0),
                            reader.GetInt32(1),
                            reader.GetInt32(2)
                        ));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the full exchange set for all partially-fulfilled sessions.
        /// Returns ALL rows (Pending + Fulfilled) for every session that is partially fulfilled,
        /// so the caller can display the complete picture — fulfilled rows read-only, pending rows actionable.
        ///
        /// A session is "partially fulfilled" when:
        ///   - It has at least one Pending row, AND
        ///   - Either: a Pending row has IssuedFullQty &gt; 0 (single-model partial),
        ///             OR another row in the same session is already Fulfilled (multi-model partial).
        ///
        /// For legacy requests (no SubmissionSessionId): falls back to the same single-row
        /// partial logic — Pending row with IssuedFullQty &gt; 0.
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetPartiallyFulfilledExchangesFullSet()
        {
            var result = new List<UnfulfilledCartridgeExchangeDto>();

            const string sql = @"
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
                    CASE WHEN r.EmpId IS NULL THEN N'(Dept Level)' ELSE e.Name END AS RequesterName,
                    b.Name  AS BranchName,
                    d.Name  AS DepartmentName,
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
                WHERE
                    -- Set-based partial: include ALL rows that share the same SetId.
                    -- SetId is written to dbo.Request by EnsureSharedSetForPortalCartridgeGroup
                    -- when the multi-model exchange is committed, so it is the reliable link.
                    (
                        r.SetId IS NOT NULL AND r.SetId > 0
                        -- Set must still have at least one Pending unfulfilled row
                        AND EXISTS (
                            SELECT 1
                            FROM dbo.UnfulfilledCartridgeExchange u2
                            INNER JOIN dbo.Request r2 ON u2.ReqId = r2.ReqId
                            WHERE r2.SetId = r.SetId
                              AND u2.Status = 'Pending'
                        )
                        AND (
                            -- (a) A pending row in this Set was partially issued
                            EXISTS (
                                SELECT 1
                                FROM dbo.UnfulfilledCartridgeExchange u3
                                INNER JOIN dbo.Request r3 ON u3.ReqId = r3.ReqId
                                WHERE r3.SetId = r.SetId
                                  AND u3.Status = 'Pending'
                                  AND u3.IssuedFullQty > 0
                            )
                            OR
                            -- (b) Another row in this Set is already Fulfilled via the queue
                            EXISTS (
                                SELECT 1
                                FROM dbo.UnfulfilledCartridgeExchange u4
                                INNER JOIN dbo.Request r4 ON u4.ReqId = r4.ReqId
                                WHERE r4.SetId = r.SetId
                                  AND u4.Status = 'Fulfilled'
                            )
                            OR
                            -- (c) Another request in the same Set was fully served on the first
                            --     pass: FulfillCartridgeExchangeByCondition sets Request.Status =
                            --     'Fulfilled' when TotalIssuedQty >= Quantity.  No
                            --     UnfulfilledCartridgeExchange row is created in that case, so
                            --     conditions (a) and (b) would miss it.
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
                    -- Legacy / single-model partial (no Set): pending row with a partial
                    -- issuance that was never grouped into a Set.
                    (
                        (r.SetId IS NULL OR r.SetId = 0)
                        AND u.Status = 'Pending'
                        AND u.IssuedFullQty > 0
                        AND u.IssuedFullQty < u.ReturnedEmptyQty
                    )
                ORDER BY
                    COALESCE(CAST(r.SubmissionSessionId AS NVARCHAR(36)), CAST(u.UnfulfilledId AS NVARCHAR(20))),
                    u.CreatedDate ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(MapToDtoWithSession(reader));
                    }
                }
            }

            return result;
        }

        private static UnfulfilledCartridgeExchangeDto MapToDto(SqlDataReader reader)
        {
            return new UnfulfilledCartridgeExchangeDto
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
        }

        private static UnfulfilledCartridgeExchangeDto MapToDtoWithSession(SqlDataReader reader)
        {
            var dto = MapToDto(reader);

            int setIdOrd = reader.GetOrdinal("SetId");
            dto.SetId = reader.IsDBNull(setIdOrd) ? (int?)null : reader.GetInt32(setIdOrd);

            int sessionOrd = reader.GetOrdinal("SubmissionSessionId");
            dto.SubmissionSessionId = reader.IsDBNull(sessionOrd) ? (Guid?)null : reader.GetGuid(sessionOrd);

            return dto;
        }
    }
}
