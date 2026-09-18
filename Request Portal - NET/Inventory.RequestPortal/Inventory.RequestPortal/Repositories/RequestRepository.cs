using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Repository for Request data access.
    /// TRANSLATED FROM: Yakult.Inventory.App/Repositories/RequestRepository.cs
    /// CHANGES: Uses dependency injection for connection string
    /// </summary>
    public class RequestRepository : IRequestRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public RequestRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        /// <summary>
        /// Adds a new request to the database.
        /// COPIED FROM: Yakult.Inventory.App/Repositories/RequestRepository.cs (AddRequest)
        ///
        /// PORTAL REQUEST DETECTION:
        /// If description contains [PORTAL] tag, this is an INTENT-ONLY request:
        /// - DO NOT allocate inventory
        /// - DO NOT decrement stock
        /// - DO NOT attempt serial matching
        /// Portal requests are fulfilled later by IT when they extract the model from [MODEL:XXX]
        /// </summary>
        public int AddRequest(RequestDto request)
        {
            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // PORTAL REQUEST DETECTION: Check if this is an intent-only portal request
                        bool isPortalRequest = !string.IsNullOrEmpty(request.Description)
                            && request.Description.Contains("[PORTAL");

                        // MANDATORY DEBUG LOGGING
                        Console.WriteLine($"[REQUEST SUBMISSION DEBUG]");
                        Console.WriteLine($"  ItemId: {request.ItemId}");
                        Console.WriteLine($"  Description: {request.Description}");
                        Console.WriteLine($"  IsPortalRequest: {isPortalRequest}");
                        Console.WriteLine($"  EntryType (original): {request.EntryType}");

                        // CRITICAL: Verify Item state BEFORE submission
                        const string sqlDebugBefore = @"
                            SELECT ItemId, StockOnHand, Active
                            FROM dbo.Item
                            WHERE ItemId = @ItemId";

                        using (var debugCmd = new SqlCommand(sqlDebugBefore, con, transaction))
                        {
                            debugCmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                            using (var reader = debugCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    Console.WriteLine($"  Item BEFORE: ItemId={reader.GetInt32(0)}, StockOnHand={reader.GetInt32(1)}, Active={reader.GetBoolean(2)}");
                                }
                            }
                        }

                        // Step 1: Insert into Request table
                        // Organization data is accessed via EmpId join to Employee table (not stored directly)
                        const string sqlRequest = @"
                            INSERT INTO dbo.Request
                                (DateRequested, Description, Remarks, Status, EntryType, Quantity,
                                 DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId,
                                 ComId, DeptId, BranchId, SubmissionSessionId,
                                 ReceivedById, WorkflowType)
                            VALUES
                                (@DateRequested, @Description, @Remarks, @Status, @EntryType, @Quantity,
                                 @DateCreated, @CreatedBy, @DateModified, @ModifiedBy, @ItemId, @EmpId,
                                 @ComId, @DeptId, @BranchId, @SubmissionSessionId,
                                 @ReceivedById, @WorkflowType);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newRequestId;
                        using (var cmd = new SqlCommand(sqlRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@DateRequested", request.DateRequested);
                            cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", (object?)request.Remarks ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Status", request.Status);
                            // Portal requests always have EntryType = "None" (no inventory impact)
                            cmd.Parameters.AddWithValue("@EntryType", isPortalRequest ? "None" : (request.EntryType ?? "Negative"));
                            cmd.Parameters.AddWithValue("@Quantity", request.Quantity);
                            cmd.Parameters.AddWithValue("@DateCreated", request.DateCreated);
                            cmd.Parameters.AddWithValue("@CreatedBy", request.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@DateModified", request.DateCreated);
                            cmd.Parameters.AddWithValue("@ModifiedBy", request.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                            cmd.Parameters.AddWithValue("@EmpId", (object?)request.EmpId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ComId", (object?)request.ComId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DeptId", (object?)request.DeptId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BranchId", (object?)request.BranchId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SubmissionSessionId", (object?)request.SubmissionSessionId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ReceivedById", (object?)request.ReceivedById ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@WorkflowType", (object?)request.WorkflowType ?? DBNull.Value);

                            newRequestId = (int)cmd.ExecuteScalar();
                        }

                        // PORTAL REQUESTS: Skip all inventory operations
                        // Portal requests are INTENT-ONLY - no stock allocation, no decrement
                        // IT Fulfillment will extract model from [MODEL:XXX] and create actual items later
                        if (isPortalRequest)
                        {
                            Console.WriteLine($"  PORTAL REQUEST DETECTED - Skipping inventory operations");
                            Console.WriteLine($"  Committing transaction for ReqId: {newRequestId}");

                            transaction.Commit();

                            // CRITICAL DEBUG: Verify Item state AFTER commit (should be unchanged)
                            using (var verifyCmd = new SqlCommand(sqlDebugBefore, con))
                            {
                                verifyCmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                                using (var reader = verifyCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        Console.WriteLine($"  Item AFTER COMMIT: ItemId={reader.GetInt32(0)}, StockOnHand={reader.GetInt32(1)}, Active={reader.GetBoolean(2)}");
                                    }
                                }
                            }

                            return newRequestId;
                        }

                        // ========================================
                        // ARCHITECTURE VIOLATION DETECTED
                        // ========================================
                        //
                        // If execution reaches this point, it means a NON-PORTAL request
                        // was submitted through Inventory.RequestPortal.
                        //
                        // ARCHITECTURAL RULE:
                        // Inventory.RequestPortal is READ-ONLY and should ONLY create
                        // portal requests (tagged with [PORTAL] in Description).
                        //
                        // Non-portal requests with inventory mutations should ONLY
                        // be created through the main Inventory.App (Cartridge Management Portal).
                        //
                        // BLOCKING THIS TO PREVENT INVENTORY CORRUPTION.
                        // ========================================

                        transaction.Rollback();
                        throw new InvalidOperationException(
                            $"ARCHITECTURE VIOLATION: Non-portal request submitted through RequestPortal! " +
                            $"RequestPortal is READ-ONLY and must NOT mutate inventory. " +
                            $"Description: '{request.Description}' " +
                            $"ItemId: {request.ItemId}, Quantity: {request.Quantity}. " +
                            $"All RequestPortal submissions must have [PORTAL] tag. " +
                            $"Request has been rolled back to prevent data corruption.");
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
        /// Gets a request by ID.
        /// COPIED FROM: Yakult.Inventory.App/Repositories/RequestRepository.cs (GetRequestById)
        /// </summary>
        public RequestDto? GetRequestById(int reqId)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.DateRequested,
                    r.Description,
                    r.Remarks,
                    r.Status,
                    r.EntryType,
                    r.Quantity,
                    r.DateCreated,
                    r.Createdby,
                    r.ItemId,
                    r.EmpId,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    i.Category,
                    i.SerialNumber,
                    -- Department-level requests (EmpId NULL) carry ComId/BranchId/DeptId directly
                    -- on the Request row instead of an Employee — fall back to Branch, Department
                    -- so this never comes back blank for those rows.
                    COALESCE(
                        e.Name,
                        NULLIF(LTRIM(RTRIM(
                            ISNULL(b.Name, '') + CASE WHEN b.Name IS NOT NULL AND d.Name IS NOT NULL THEN ', ' ELSE '' END + ISNULL(d.Name, '')
                        )), '')
                    ) AS EmployeeName
                FROM dbo.Request r
                INNER JOIN dbo.Item i      ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Employee e   ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Branch b     ON r.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON r.DeptId = d.DeptId
                WHERE r.ReqId = @ReqId";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new RequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            DateRequested = reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            EntryType = reader.IsDBNull(reader.GetOrdinal("EntryType")) ? null : reader.GetString(reader.GetOrdinal("EntryType")),
                            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            CreatedByUserId = reader.GetInt32(reader.GetOrdinal("Createdby")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName"))
                        };
                    }
                }
            }

            return null;
        }
    }
}
