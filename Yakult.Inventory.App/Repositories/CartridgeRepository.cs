using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    // IMPORTANT:
    // Cartridge ModelNumber must NEVER be used for lookup, grouping, or auto-matching.
    // Model is NOT unique; multiple cartridges can share the same model.
    // All inbound and refill operations must explicitly target a cartridge
    // by SerialNumber or ItemId only.

    public sealed class CartridgeRepository
    {
        private readonly string _connectionString;

        public CartridgeRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");
        }

        #region Cartridge Returns (Quantity-Based, No Serial Numbers)

        /// <summary>
        /// Records a cartridge return.
        /// - Records as "Returned" movement type
        /// - Changes CartridgeType from "Brand New" to "Refill" (one-way)
        /// - Quantity-based, no serial numbers required
        /// - Tied to Employee + Branch + Department for accountability
        /// </summary>
        public int RecordCartridgeReturn(CartridgeReturnDto returnDto)
        {
            // Check which optional columns exist
            bool hasConditionType = ColumnExists("CartridgeMovement", "ConditionType");
            bool hasRemarks = ColumnExists("CartridgeMovement", "Remarks");
            bool hasBranchId = ColumnExists("CartridgeMovement", "BranchId");
            bool hasDeptId = ColumnExists("CartridgeMovement", "DeptId");

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Get the "Refill" CartridgeTypeId (returns always become Refill)
                        int refillTypeId = GetCartridgeTypeId(con, transaction, "Refill");

                        // Build dynamic INSERT based on available columns
                        var columns = new List<string> { "ItemId", "CartridgeTypeId", "MovementType", "Quantity", "EmployeeId", "CreatedAt", "CreatedBy" };
                        var values = new List<string> { "@ItemId", "@CartridgeTypeId", "'Returned'", "@Quantity", "@EmployeeId", "GETDATE()", "@CreatedBy" };

                        if (hasBranchId) { columns.Add("BranchId"); values.Add("@BranchId"); }
                        if (hasDeptId) { columns.Add("DeptId"); values.Add("@DeptId"); }
                        if (hasConditionType) { columns.Add("ConditionType"); values.Add("@ConditionType"); }
                        if (hasRemarks) { columns.Add("Remarks"); values.Add("@Remarks"); }

                        string sql = $@"
                            INSERT INTO dbo.CartridgeMovement ({string.Join(", ", columns)})
                            VALUES ({string.Join(", ", values)});
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int movementId;
                        using (var cmd = new SqlCommand(sql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", returnDto.ItemId);
                            cmd.Parameters.AddWithValue("@CartridgeTypeId", refillTypeId);
                            cmd.Parameters.AddWithValue("@Quantity", returnDto.Quantity);
                            cmd.Parameters.AddWithValue("@EmployeeId", returnDto.EmployeeId);
                            cmd.Parameters.AddWithValue("@CreatedBy", returnDto.CreatedBy);

                            if (hasBranchId) cmd.Parameters.AddWithValue("@BranchId", returnDto.BranchId);
                            if (hasDeptId) cmd.Parameters.AddWithValue("@DeptId", returnDto.DeptId);
                            if (hasConditionType) cmd.Parameters.AddWithValue("@ConditionType", returnDto.ConditionType ?? "Empty");
                            if (hasRemarks) cmd.Parameters.AddWithValue("@Remarks", (object)returnDto.Remarks ?? DBNull.Value);

                            movementId = (int)cmd.ExecuteScalar();
                        }

                        transaction.Commit();
                        return movementId;
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
        /// Records a cartridge issue to an employee.
        /// - Records as "Issued" movement type
        /// - Reduces available quantity
        /// </summary>
        public int RecordCartridgeIssue(CartridgeIssueDto issueDto)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Get the CartridgeTypeId from the Cartridge record
                        int cartridgeTypeId = GetCartridgeTypeIdForItem(con, transaction, issueDto.ItemId);

                        // Insert CartridgeMovement record
                        const string sql = @"
                            INSERT INTO dbo.CartridgeMovement
                                (ItemId, CartridgeTypeId, MovementType, Quantity,
                                 EmployeeId, BranchId, DeptId,
                                 Remarks, CreatedAt, CreatedBy)
                            VALUES
                                (@ItemId, @CartridgeTypeId, 'Issued', @Quantity,
                                 @EmployeeId, @BranchId, @DeptId,
                                 @Remarks, GETDATE(), @CreatedBy);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int movementId;
                        using (var cmd = new SqlCommand(sql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", issueDto.ItemId);
                            cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
                            cmd.Parameters.AddWithValue("@Quantity", issueDto.Quantity);
                            cmd.Parameters.AddWithValue("@EmployeeId", issueDto.EmployeeId);
                            cmd.Parameters.AddWithValue("@BranchId", (object)issueDto.BranchId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DeptId", (object)issueDto.DeptId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", (object)issueDto.Remarks ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CreatedBy", issueDto.CreatedBy);

                            movementId = (int)cmd.ExecuteScalar();
                        }

                        transaction.Commit();
                        return movementId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private int GetCartridgeTypeId(SqlConnection con, SqlTransaction transaction, string typeName)
        {
            const string sql = "SELECT CartridgeTypeId FROM dbo.CartridgeType WHERE CartridgeTypeName = @TypeName";
            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@TypeName", typeName);
                var result = cmd.ExecuteScalar();
                if (result == null)
                    throw new InvalidOperationException($"CartridgeType '{typeName}' not found.");
                return (int)result;
            }
        }

        private int GetCartridgeTypeIdForItem(SqlConnection con, SqlTransaction transaction, int itemId)
        {
            const string sql = "SELECT CartridgeTypeId FROM dbo.Cartridge WHERE ItemId = @ItemId AND IsActive = 1";
            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                var result = cmd.ExecuteScalar();
                if (result == null)
                    throw new InvalidOperationException($"Active Cartridge record not found for ItemId {itemId}.");
                return (int)result;
            }
        }

        #endregion

        #region Lookup Methods

        /// <summary>
        /// Get all active branches for dropdown.
        /// </summary>
        public List<LookupItem> GetBranchesLookup()
        {
            const string sql = @"
                SELECT BranchId, Name
                FROM dbo.Branch
                WHERE Active = 1
                ORDER BY Name";

            var results = new List<LookupItem>();
            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new LookupItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Get all active departments for dropdown.
        /// </summary>
        public List<LookupItem> GetDepartmentsLookup()
        {
            const string sql = @"
                SELECT DeptId, Name
                FROM dbo.Department
                WHERE Active = 1
                ORDER BY Name";

            var results = new List<LookupItem>();
            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new LookupItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Get all active employees for dropdown.
        /// </summary>
        public List<LookupItem> GetEmployeesLookup()
        {
            const string sql = @"
                SELECT EmpId, Name
                FROM dbo.Employee
                WHERE Active = 1
                ORDER BY Name";

            var results = new List<LookupItem>();
            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new LookupItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Get cartridge items available for return (Brand New cartridges that have been issued).
        /// </summary>
        public List<LookupItem> GetCartridgeItemsForReturn()
        {
            const string sql = @"
                SELECT DISTINCT
                    i.ItemId,
                    i.Name
                FROM dbo.Cartridge c
                INNER JOIN dbo.Item i ON i.ItemId = c.ItemId
                WHERE c.IsActive = 1
                ORDER BY i.Name";

            var results = new List<LookupItem>();
            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new LookupItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return results;
        }

        #endregion

        public DataTable GetBrandNewCartridgesAvailability()
        {
            const string sql = @"
;WITH EligibleItems AS (
    SELECT
        i.ItemId,
        i.Name,
        i.VendorId,
        i.StockOnHand
    FROM dbo.Item i
    LEFT JOIN dbo.ArchiveStatus arch
        ON arch.EntityType = 'Item'
       AND arch.EntityId = i.ItemId
       AND arch.IsArchived = 1
    WHERE
        i.Active = 1
        AND arch.EntityId IS NULL
        AND i.Category = 'Cartridge'
        AND i.StockOnHand > 0
        AND i.ConditionId = 1  -- Good condition only
), ItemsWithCartridgeType AS (
    SELECT
        ei.ItemId,
        ei.Name,
        ei.VendorId,
        ei.StockOnHand,
        (SELECT TOP 1 ct.CartridgeTypeName
         FROM dbo.Cartridge c
         INNER JOIN dbo.CartridgeType ct ON ct.CartridgeTypeId = c.CartridgeTypeId
         WHERE c.ItemId = ei.ItemId AND c.IsActive = 1) AS CartridgeTypeName
    FROM EligibleItems ei
)
SELECT
    iwc.Name AS ItemName,
    SUM(iwc.StockOnHand) AS AvailableQuantity,
    MAX(iwc.CartridgeTypeName) AS CartridgeCategory,
    v.VendorName
FROM ItemsWithCartridgeType iwc
LEFT JOIN dbo.Vendor v ON v.VendorId = iwc.VendorId
GROUP BY
    iwc.Name,
    iwc.VendorId,
    v.VendorName
HAVING SUM(iwc.StockOnHand) > 0
ORDER BY iwc.Name;";

            return ExecuteToDataTable(sql, null);
        }

        // Cached schema flags — checked once per app session to avoid repeated INFORMATION_SCHEMA queries on every load.
        private static bool? _cachedHasConditionType;
        private static bool? _cachedHasRemarks;

        public DataTable GetCartridgeMovementHistory(int? itemId, int? employeeId, string movementType)
        {
            // Query with optional new columns (BranchId, DeptId, ConditionType, Remarks)
            // Uses dynamic column check to handle databases that don't have these columns yet
            string sql = @"
SELECT
    cm.CreatedAt,
    i.Name AS ItemName,
    cm.MovementType,
    cm.Quantity,
    ct.CartridgeTypeName AS CartridgeCategory,
    e.Name AS EmployeeName,
    b.Name AS BranchName,
    d.Name AS DepartmentName,
    {0}
    u.Name AS PerformedBy
    {1}
FROM dbo.CartridgeMovement cm
INNER JOIN dbo.Item i ON i.ItemId = cm.ItemId
INNER JOIN dbo.CartridgeType ct ON ct.CartridgeTypeId = cm.CartridgeTypeId
LEFT JOIN dbo.Employee e ON e.EmpId = cm.EmployeeId
LEFT JOIN dbo.Branch b ON b.BranchId = cm.BranchId
LEFT JOIN dbo.Department d ON d.DeptId = cm.DeptId
LEFT JOIN dbo.[User] u ON u.UserId = cm.CreatedBy
WHERE
    (@ItemId IS NULL OR cm.ItemId = @ItemId)
    AND (@EmployeeId IS NULL OR cm.EmployeeId = @EmployeeId)
    AND (@MovementType IS NULL OR cm.MovementType = @MovementType)
ORDER BY cm.CreatedAt DESC, cm.MovementId DESC;";

            // Check if optional columns exist — cached after first call to avoid repeated INFORMATION_SCHEMA roundtrips
            if (_cachedHasConditionType == null)
                _cachedHasConditionType = ColumnExists("CartridgeMovement", "ConditionType");
            if (_cachedHasRemarks == null)
                _cachedHasRemarks = ColumnExists("CartridgeMovement", "Remarks");

            bool hasConditionType = _cachedHasConditionType.Value;
            bool hasRemarks = _cachedHasRemarks.Value;

            string conditionCol = hasConditionType ? "cm.ConditionType," : "NULL AS ConditionType,";
            string remarksCol = hasRemarks ? ", cm.Remarks" : ", NULL AS Remarks";

            sql = string.Format(sql, conditionCol, remarksCol);

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@ItemId", SqlDbType.Int) { Value = (object)itemId ?? DBNull.Value },
                new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = (object)employeeId ?? DBNull.Value },
                new SqlParameter("@MovementType", SqlDbType.VarChar, 30) { Value = (object)movementType ?? DBNull.Value }
            };

            return ExecuteToDataTable(sql, parameters);
        }

        /// <summary>
        /// Checks if a column exists in a table.
        /// </summary>
        private bool ColumnExists(string tableName, string columnName)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", columnName);
                con.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public List<LookupItem> GetCartridgeMovementItemsLookup()
        {
            const string sql = @"
SELECT DISTINCT
    i.ItemId,
    i.Name
FROM dbo.CartridgeMovement cm
INNER JOIN dbo.Item i ON i.ItemId = cm.ItemId
INNER JOIN dbo.Cartridge c ON c.ItemId = cm.ItemId AND c.IsActive = 1
ORDER BY i.Name;";

            var results = new List<LookupItem>();
            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new LookupItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            return results;
        }

        public List<LookupItem> GetCartridgeMovementEmployeeLookup()
        {
            // Updated to return employee names instead of just IDs
            const string sql = @"
SELECT DISTINCT
    e.EmpId,
    e.Name
FROM dbo.CartridgeMovement cm
INNER JOIN dbo.Employee e ON e.EmpId = cm.EmployeeId
WHERE cm.EmployeeId IS NOT NULL
ORDER BY e.Name;";

            var results = new List<LookupItem>();
            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new LookupItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            return results;
        }

        

        #region Portal Cartridge Returns (For Refill Page)

        /// <summary>
        /// Gets cartridge returns from the Request Portal for the refill tracking page.
        /// Queries the Request table for portal requests with "With Cartridge" condition.
        /// These are quantity-based returns with no serial numbers.
        /// </summary>
        public List<CartridgeReturnForRefillDto> GetPortalCartridgeReturnsForRefill()
        {
            var returns = new List<CartridgeReturnForRefillDto>();
            var pendingPortalResolutions = new List<PortalCartridgeResolution>();

            // OPERATIONAL QUEUE: Only show cartridges that still require refill action.
            // Source of truth: EmptyCartridge.RefillStatus (NOT computed or inferred)
            //
            // STRICT FILTERS:
            // - RefillStatus IN ('For Refill', 'Refilling') — never show 'Refilled'
            // - Exclude cartridges belonging to CLOSED vendor batches
            // - INNER JOIN to EmptyCartridge — exclude requests with no tracked refill state
            //   (NULL RefillStatus = data error, do not guess)
            //
            // PERFORMANCE: Pre-aggregated subquery scans EmptyCartridge once.
            // Requires index IX_EmptyCartridge_ReqId on EmptyCartridge(ReqId).
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.Description,
                    i.Name AS CartridgeName,
                    r.Quantity,
                    ec.RefillStatus,
                    r.DateCreated AS DateReturned,
                    r.Remarks,
                    e.Name AS EmployeeName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    r.Status AS PortalStatus
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                INNER JOIN (
                    SELECT ec2.ReqId, ec2.RefillStatus, ec2.VendorBatchId
                    FROM dbo.EmptyCartridge ec2
                    INNER JOIN (
                        SELECT ReqId, MAX(EmptyCartridgeId) AS MaxId
                        FROM dbo.EmptyCartridge
                        WHERE ReqId IS NOT NULL
                        GROUP BY ReqId
                    ) ecMax ON ec2.EmptyCartridgeId = ecMax.MaxId
                ) ec ON ec.ReqId = r.ReqId
                LEFT JOIN dbo.VendorCartridgeBatch vcb ON ec.VendorBatchId = vcb.BatchId
                WHERE r.Description LIKE '[[]PORTAL]%'
                  AND r.Remarks LIKE '%With Cartridge%'
                  AND ec.RefillStatus IN ('For Refill', 'Refilling')
                  AND (vcb.BatchId IS NULL OR vcb.Status != 'Closed')
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.CommandTimeout = 120; // Prevent default 30s timeout on large datasets
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string description = reader.IsDBNull(1) ? null : reader.GetString(1);
                            bool isPortalRequest = description != null && description.Contains("[PORTAL]");
                            string modelNumber = isPortalRequest ? ExtractModelFromDescription(description) : null;

                            var dto = new CartridgeReturnForRefillDto
                            {
                                ReqId = reader.GetInt32(0),
                                CartridgeName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                RefillStatus = reader.GetString(4),
                                DateReturned = reader.GetDateTime(5),
                                Remarks = reader.IsDBNull(6) ? null : reader.GetString(6),
                                EmployeeName = reader.GetString(7),
                                BranchName = reader.IsDBNull(8) ? null : reader.GetString(8),
                                DepartmentName = reader.IsDBNull(9) ? null : reader.GetString(9),
                                PortalStatus = reader.GetString(10)
                            };

                            returns.Add(dto);

                            if (isPortalRequest && !string.IsNullOrWhiteSpace(modelNumber))
                            {
                                pendingPortalResolutions.Add(new PortalCartridgeResolution
                                {
                                    Return = dto,
                                    ModelNumber = modelNumber
                                });
                            }
                        }
                    }
                }

                // IMPORTANT: For portal cartridge returns, NEVER use ResolveItemNameByModel fallback.
                // The Item.Name from the database join is just a placeholder/reference item
                // (e.g., the first cartridge in the system like "rx 310 cart").
                // The actual cartridge model being returned is in the Description [MODEL:xxx] tag.
                // Display format: "Cartridge (Model: XXX)" to clearly show this is model-based.
                foreach (var pending in pendingPortalResolutions)
                {
                    pending.Return.CartridgeName = $"Cartridge (Model: {pending.ModelNumber})";
                }
            }

            return returns;
        }

        private string ExtractModelFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            int modelStart = description.IndexOf("[MODEL:");
            if (modelStart == -1)
                return null;

            int modelValueStart = modelStart + "[MODEL:".Length;
            int modelEnd = description.IndexOf("]", modelValueStart);

            if (modelEnd == -1)
                return null;

            return description.Substring(modelValueStart, modelEnd - modelValueStart).Trim();
        }

        private string ResolveItemNameByModel(string modelNumber, SqlConnection connection)
        {
            if (string.IsNullOrWhiteSpace(modelNumber))
                return null;

            const string sql = @"
                SELECT TOP 1 Name
                FROM dbo.Item
                WHERE (ModelNumber = @ModelNumber OR Name LIKE '%' + @ModelNumber + '%')
                  AND Category = 'Cartridge'
                  AND Active = 1
                ORDER BY
                    CASE WHEN ModelNumber = @ModelNumber THEN 0 ELSE 1 END,
                    DateCreated DESC";

            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@ModelNumber", modelNumber.Trim());
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? result.ToString() : null;
            }
        }

        private sealed class PortalCartridgeResolution
        {
            public CartridgeReturnForRefillDto Return { get; set; }
            public string ModelNumber { get; set; }
        }

        /// <summary>
        /// Updates the refill status for a portal cartridge return.
        /// Updates BOTH EmptyCartridge.RefillStatus (source of truth) AND Request.Status.
        /// Enforces valid state transitions:
        ///   'For Refill' → 'Refilling' → 'Refilled'
        /// </summary>
        public void UpdatePortalReturnRefillStatus(int reqId, string newStatus, int modifiedByUserId)
        {
            // Validate the new status value
            if (newStatus != "For Refill" && newStatus != "Refilling" && newStatus != "Refilled")
                throw new ArgumentException($"Invalid RefillStatus: '{newStatus}'. Must be 'For Refill', 'Refilling', or 'Refilled'.");

            // Determine the required previous state for valid transition
            string requiredPreviousStatus;
            switch (newStatus)
            {
                case "Refilling":
                    requiredPreviousStatus = "For Refill";
                    break;
                case "Refilled":
                    requiredPreviousStatus = "Refilling";
                    break;
                default:
                    requiredPreviousStatus = null; // 'For Refill' is the initial state, no transition check
                    break;
            }

            // Map refill status to request status
            string requestStatus;
            switch (newStatus)
            {
                case "Refilling":
                    requestStatus = "Processing";
                    break;
                case "Refilled":
                    requestStatus = "Completed";
                    break;
                default:
                    requestStatus = "Under Review";
                    break;
            }

            // Update EmptyCartridge.RefillStatus with transition guard, then Request.Status
            // Uses a transaction to keep both tables in sync
            const string sqlUpdateEmptyCartridge = @"
                UPDATE dbo.EmptyCartridge
                SET RefillStatus = @NewRefillStatus,
                    DateModified = GETDATE(),
                    ModifiedBy = @ModifiedBy
                WHERE ReqId = @ReqId
                  AND (@RequiredPreviousStatus IS NULL OR RefillStatus = @RequiredPreviousStatus)";

            const string sqlUpdateRequest = @"
                UPDATE dbo.Request
                SET Status = @RequestStatus,
                    DateModified = GETDATE(),
                    ModifiedBy = @ModifiedBy
                WHERE ReqId = @ReqId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        int rowsAffected;
                        using (var cmd = new SqlCommand(sqlUpdateEmptyCartridge, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@NewRefillStatus", newStatus);
                            cmd.Parameters.AddWithValue("@RequiredPreviousStatus", (object)requiredPreviousStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                            rowsAffected = cmd.ExecuteNonQuery();
                        }

                        if (rowsAffected == 0 && requiredPreviousStatus != null)
                        {
                            throw new InvalidOperationException(
                                $"Cannot transition to '{newStatus}': no EmptyCartridge rows for ReqId {reqId} " +
                                $"with current RefillStatus = '{requiredPreviousStatus}'.");
                        }

                        using (var cmd = new SqlCommand(sqlUpdateRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@RequestStatus", requestStatus);
                            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
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

        #endregion

        private DataTable ExecuteToDataTable(string sql, List<SqlParameter> parameters)
        {
            var table = new DataTable();

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters.ToArray());

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(table);
                }
            }

            return table;
        }
    }
}
