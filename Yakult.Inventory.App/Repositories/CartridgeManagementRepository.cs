using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    // IMPORTANT:
    // Returned empty cartridges must always reference a valid ItemId.
    // If the cartridge does not exist, it is auto-registered with
    // safe default values (SerialNumber = 'N/A', Vendor = 'N/A').
    // This prevents NULL violations while preserving audit integrity.
    //
    // Cartridge Management strictly follows the Request → Set lifecycle
    // used by ViewSetDetailPage.cs (Upgrade Lifecycle).
    // Requests represent intent; fulfillment records physical cartridge movements.
    // Each cartridge exchange is recorded as individual CartridgeMovement rows.

    /// <summary>
    /// Repository for Cartridge Management IT fulfillment operations.
    /// Handles pending request queries and cartridge exchange fulfillment.
    /// </summary>
    public sealed class CartridgeManagementRepository
    {
        private readonly string _connectionString;

        public CartridgeManagementRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");
        }

        private static string EnsureCartridgeSuffix(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return "Cartridge";

            string trimmed = itemName.Trim();
            if (trimmed.IndexOf("Cartridge", StringComparison.OrdinalIgnoreCase) >= 0)
                return trimmed;

            return $"{trimmed} Cartridge";
        }

        /// <summary>
        /// Gets pending cartridge requests for IT fulfillment.
        ///
        /// WORKFLOW RULE: Only shows requests that have NEVER been attempted.
        /// Once a fulfillment attempt is made (request appears in UnfulfilledCartridgeExchange),
        /// the request is permanently moved to UnfulfilledCartridgeExchangesPage and will
        /// NEVER return to this pending queue.
        ///
        /// Filters:
        /// - Category = 'Cartridge'
        /// - Status in ('Pending', 'Under Review', 'Processing')
        /// - No existing UnfulfilledCartridgeExchange record (excludes attempted requests)
        /// - No CartridgeMovement records (excludes requests with fulfillment history)
        ///
        /// PORTAL REQUESTS: For portal requests, the TYPED model number is stored in the
        /// Description field as [MODEL:xxx]. This takes priority over the Item's ModelNumber.
        /// </summary>
        public List<CartridgeRequestDto> GetPendingCartridgeRequests()
        {
            var requests = new List<CartridgeRequestDto>();

            const string sql = @"
                -- PERF: Precompute request IDs with fulfillment history ONCE instead of
                -- re-scanning dbo.CartridgeMovement per pending request via a correlated LIKE.
                -- Remarks encode the source request as 'Request #<id>'; parse it out here so
                -- the lookup below is a PK semi-join instead of a per-row table scan.
                IF OBJECT_ID('tempdb..#MovedReqIds') IS NOT NULL DROP TABLE #MovedReqIds;
                CREATE TABLE #MovedReqIds (ReqId INT PRIMARY KEY);

                INSERT INTO #MovedReqIds (ReqId)
                SELECT DISTINCT parsed.ReqId
                FROM (
                    SELECT TRY_CAST(
                        SUBSTRING(
                            cm.Remarks,
                            CHARINDEX('Request #', cm.Remarks) + 9,
                            PATINDEX('%[^0-9]%', SUBSTRING(cm.Remarks, CHARINDEX('Request #', cm.Remarks) + 9, 10) + 'X') - 1
                        ) AS INT
                    ) AS ReqId
                    FROM dbo.CartridgeMovement cm
                    WHERE cm.MovementType IN ('Issued', 'Returned')
                      AND cm.Remarks LIKE '%Request #%'
                ) parsed
                WHERE parsed.ReqId IS NOT NULL;

                IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                BEGIN
                    SELECT
                        r.ReqId,
                        r.ItemId,
                        -- Resolve CartridgeModelId from the REQUESTED model, not from the placeholder Item.
                        -- Priority: CartridgeRequestModel.CartridgeModel > [MODEL:xxx] in Description > Item.CartridgeModelId.
                        -- The placeholder Item (SELECT TOP 1) always points to the first/default inventory record;
                        -- using i.CartridgeModelId directly would silently resolve to the wrong model.
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
                        e.Name AS EmployeeName,
                        c.Name AS CompanyName,
                        b.Name AS BranchName,
                        d.Name AS DepartmentName,
                        -- Count ALL CartridgeRequestModel rows across ALL requests in the same session
                        -- (including already-fulfilled ones) to determine the original model count.
                        -- CRM rows are never deleted, so this always reflects the original submission size.
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
                    -- Resolve effective CartridgeModelId: CRM model string > [MODEL:xxx] description tag.
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
                    -- Explicit workflow ownership (dbo.Request.WorkflowType), assigned once at
                    -- submission time, replaces the old i.Category='Cartridge' inclusion check.
                    -- That old check wrongly included cartridge lines from MIXED submissions
                    -- (which resolve to a real cartridge Item but belong to Request & Set
                    -- Management, not here) since it never verified submission composition.
                    WHERE r.WorkflowType = 'CartridgeManagement'
                      AND r.Status IN ('Pending', 'Under Review', 'Processing')
                      AND (
                           crm.RequestModelId IS NULL
                           OR ISNULL(NULLIF(crm.Status, ''), 'Pending') IN ('Pending', 'Under Review', 'Processing')
                      )
                      -- AUTHORIZATION GATE: Only show requests with an Approved supervisor authorization.
                      -- Requests that are Pending, Rejected, or have no authorization record are excluded.
                      AND EXISTS (
                          SELECT 1 FROM dbo.CartridgeAuthorization ca
                          WHERE ca.SubmissionSessionId = r.SubmissionSessionId
                            AND ca.Status = 'Approved'
                      )
                      -- CRITICAL WORKFLOW FIX: Exclude requests that have been attempted
                      -- Once a request has any fulfillment attempt, it moves permanently to UnfulfilledCartridgeExchangesPage
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.UnfulfilledCartridgeExchange uce
                          WHERE uce.ReqId = r.ReqId
                      )
                      -- CRITICAL WORKFLOW FIX: Exclude requests that have CartridgeMovement records
                      -- This ensures requests with fulfillment history never return to pending queue
                      AND NOT EXISTS (
                          SELECT 1 FROM #MovedReqIds m WHERE m.ReqId = r.ReqId
                      )
                    ORDER BY r.DateCreated ASC, r.ReqId ASC, crm.RequestModelId ASC;
                END
                ELSE
                BEGIN
                    SELECT
                        r.ReqId,
                        r.ItemId,
                        COALESCE(eff_cm.CartridgeModelId, i.CartridgeModelId) AS CartridgeModelId,
                        i.Name AS ItemName,
                        i.ModelNumber AS ItemModelNumber,
                        r.Description,
                        r.SubmissionSessionId,
                        CAST(NULL AS INT) AS RequestModelId,
                        CAST(NULL AS NVARCHAR(100)) AS RequestedModel,
                        r.Quantity,
                        r.Remarks AS ConditionType,
                        'Good' AS PhysicalCondition,
                        0 AS GoodEmptyQty,
                        0 AS DamagedEmptyQty,
                        r.Status,
                        r.DateCreated,
                        r.EmpId,
                        e.Name AS EmployeeName,
                        c.Name AS CompanyName,
                        b.Name AS BranchName,
                        d.Name AS DepartmentName,
                        -- Legacy branch: CartridgeRequestModel does not exist.
                        -- Fall back to counting Request rows with same SubmissionSessionId.
                        ISNULL((
                            SELECT COUNT(*)
                            FROM dbo.Request r_orig
                            INNER JOIN dbo.Item i_orig ON r_orig.ItemId = i_orig.ItemId
                            WHERE r_orig.SubmissionSessionId = r.SubmissionSessionId
                              AND i_orig.Category = 'Cartridge'
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
                    OUTER APPLY (
                        SELECT TOP 1 cm_eff.CartridgeModelId
                        FROM dbo.CartridgeModel cm_eff
                        WHERE cm_eff.IsActive = 1
                          AND UPPER(LTRIM(RTRIM(cm_eff.ModelNumber))) = UPPER(LTRIM(RTRIM(
                              CASE
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
                    -- Explicit workflow ownership, see comment in the CartridgeRequestModel-exists
                    -- branch above for why this replaces i.Category='Cartridge'.
                    WHERE r.WorkflowType = 'CartridgeManagement'
                      AND r.Status IN ('Pending', 'Under Review', 'Processing')
                      -- AUTHORIZATION GATE: Only show requests with an Approved supervisor authorization.
                      AND EXISTS (
                          SELECT 1 FROM dbo.CartridgeAuthorization ca
                          WHERE ca.SubmissionSessionId = r.SubmissionSessionId
                            AND ca.Status = 'Approved'
                      )
                      -- CRITICAL WORKFLOW FIX: Exclude requests that have been attempted
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.UnfulfilledCartridgeExchange uce
                          WHERE uce.ReqId = r.ReqId
                      )
                      -- CRITICAL WORKFLOW FIX: Exclude requests that have fulfillment history
                      AND NOT EXISTS (
                          SELECT 1 FROM #MovedReqIds m WHERE m.ReqId = r.ReqId
                      )
                    ORDER BY r.DateCreated ASC;
                END";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.CommandTimeout = 60;
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
                        string itemModelNumber = reader.IsDBNull(reader.GetOrdinal("ItemModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ItemModelNumber"));

                        int? requestModelId = reader.IsDBNull(reader.GetOrdinal("RequestModelId"))
                            ? (int?)null
                            : reader.GetInt32(reader.GetOrdinal("RequestModelId"));

                        string requestedModel = reader.IsDBNull(reader.GetOrdinal("RequestedModel"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("RequestedModel"));

                        string typedModelFromDescription = ExtractTypedModelFromDescription(description);

                        // Multi-model: the per-line model comes from dbo.CartridgeRequestModel
                        // Single-model (legacy): fall back to [MODEL:xxx] tag or Item model.
                        string effectiveModelNumber =
                            (!string.IsNullOrWhiteSpace(requestedModel) ? requestedModel : null)
                            ?? typedModelFromDescription
                            ?? itemModelNumber;

                        // NULL-safe reads for all fields that could be NULL
                        string itemName = reader.IsDBNull(reader.GetOrdinal("ItemName"))
                            ? "Unknown Item"
                            : reader.GetString(reader.GetOrdinal("ItemName"));

                        int quantity = reader.IsDBNull(reader.GetOrdinal("Quantity"))
                            ? 0
                            : reader.GetInt32(reader.GetOrdinal("Quantity"));

                        string status = reader.IsDBNull(reader.GetOrdinal("Status"))
                            ? "Pending"
                            : reader.GetString(reader.GetOrdinal("Status"));

                        string employeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName"))
                            ? "Unknown Employee"
                            : reader.GetString(reader.GetOrdinal("EmployeeName"));

                        Guid? submissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId"))
                            ? (Guid?)null
                            : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId"));

                        int? cartridgeModelId = reader.IsDBNull(reader.GetOrdinal("CartridgeModelId"))
                            ? (int?)null
                            : reader.GetInt32(reader.GetOrdinal("CartridgeModelId"));

                        int originalSubmissionCount = reader.IsDBNull(reader.GetOrdinal("OriginalSubmissionCount"))
                            ? 0
                            : reader.GetInt32(reader.GetOrdinal("OriginalSubmissionCount"));

                        requests.Add(new CartridgeRequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            CartridgeModelId = cartridgeModelId,
                            ItemName = itemName,
                            ModelNumber = effectiveModelNumber,
                            TypedModelNumber = !string.IsNullOrWhiteSpace(requestedModel) ? requestedModel : typedModelFromDescription,
                            RequestModelId = requestModelId,
                            Quantity = quantity,
                            ConditionType = reader.IsDBNull(reader.GetOrdinal("ConditionType")) ? null : reader.GetString(reader.GetOrdinal("ConditionType")),
                            PhysicalCondition = reader.IsDBNull(reader.GetOrdinal("PhysicalCondition")) ? "Good" : reader.GetString(reader.GetOrdinal("PhysicalCondition")),
                            GoodEmptyQty = reader.IsDBNull(reader.GetOrdinal("GoodEmptyQty")) ? 0 : reader.GetInt32(reader.GetOrdinal("GoodEmptyQty")),
                            DamagedEmptyQty = reader.IsDBNull(reader.GetOrdinal("DamagedEmptyQty")) ? 0 : reader.GetInt32(reader.GetOrdinal("DamagedEmptyQty")),
                            Status = status,
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? 0 : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            EmployeeName = employeeName,
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            SubmissionSessionId = submissionSessionId,
                            OriginalSubmissionCount = originalSubmissionCount,
                            Description = description,
                            DistributionMethod = reader.IsDBNull(reader.GetOrdinal("DistributionMethod")) ? "N/A" : reader.GetString(reader.GetOrdinal("DistributionMethod")),
                            ReceivedByName = reader.IsDBNull(reader.GetOrdinal("ReceivedByName")) ? null : reader.GetString(reader.GetOrdinal("ReceivedByName")),
                            AdditionalRemarks = reader.IsDBNull(reader.GetOrdinal("AdditionalRemarks")) ? null : reader.GetString(reader.GetOrdinal("AdditionalRemarks"))
                        });
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Extracts the typed model number from portal request Description.
        /// Format: [PORTAL] [MODEL:xxx] ...
        /// Returns the model string (xxx) or null if not found.
        /// </summary>
        private string ExtractTypedModelFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            // Look for [MODEL:xxx] pattern
            const string modelPrefix = "[MODEL:";
            int startIndex = description.IndexOf(modelPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
                return null;

            startIndex += modelPrefix.Length;
            int endIndex = description.IndexOf(']', startIndex);
            if (endIndex < 0)
                return null;

            string model = description.Substring(startIndex, endIndex - startIndex).Trim();
            return string.IsNullOrWhiteSpace(model) ? null : model;
        }

        /// <summary>
        /// Gets cartridges that can be returned (empty cartridges from employee).
        /// These are cartridges with Status = 'OUTBOUND' or 'IN_USE' matching the model.
        /// </summary>
        public List<CartridgeSelectionItem> GetReturnableCartridges(int itemId)
        {
            var items = new List<CartridgeSelectionItem>();

            // Get cartridges of the same model that are currently out (issued to employees)
            const string sql = @"
                SELECT
                    i.ItemId,
                    i.SerialNumber,
                    i.ModelNumber,
                    i.Name + ' - ' + ISNULL(i.SerialNumber, 'No S/N') AS DisplayName
                FROM dbo.Item i
                WHERE i.Category = 'Cartridge'
                  AND i.Active = 1
                  AND (
                      i.ModelNumber = (SELECT ModelNumber FROM dbo.Item WHERE ItemId = @ItemId)
                      OR i.Name = (SELECT Name FROM dbo.Item WHERE ItemId = @ItemId)
                  )
                  AND ISNULL(i.RefillStatus, '') IN ('OUTBOUND', 'IN_USE', 'Issued', '')
                ORDER BY i.SerialNumber, i.Name";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new CartridgeSelectionItem
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName"))
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Gets CartridgeModelId by model number string (for backward compatibility where only model number is available)
        /// </summary>
        public int? GetCartridgeModelIdByModelNumber(string modelNumber)
        {
            if (string.IsNullOrWhiteSpace(modelNumber))
                return null;

            const string sql = @"
                SELECT CartridgeModelId
                FROM dbo.CartridgeModel
                WHERE UPPER(LTRIM(RTRIM(ModelNumber))) = UPPER(LTRIM(RTRIM(@ModelNumber)))
                  AND IsActive = 1";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ModelNumber", modelNumber.Trim());
                con.Open();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return (int)result;
            }

            return null;
        }

        public int GetAvailableIssuableStock(int? cartridgeModelId)
        {
            if (!cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return 0;

            // SINGLE SOURCE OF TRUTH: dbo.Item
            // DEPRECATED: dbo.Cartridge (no longer synchronized, contains stale data)
            //
            // AVAILABILITY RULES:
            // - Data source: dbo.Item ONLY
            // - Filters: Category, CartridgeModelId, Active
            // - Do NOT filter: RefillStatus, StockType, ModelNumber string, Request.Condition, StockOnHand > 0, VendorId
            //
            // DELTA-BASED STOCK SYSTEM:
            // - Positive StockOnHand rows = received or refilled stock
            // - Negative StockOnHand rows = issued or deducted stock
            // - Net available stock = SUM(StockOnHand) across ALL rows (no filtering by > 0)
            // - Filter BEFORE aggregation: Category, CartridgeModelId, Active
            // - Aggregate AFTER filtering: SUM all StockOnHand values (positive AND negative)

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                // Calculate availability (Active=1 only)
                const string sql = @"
                    SELECT ISNULL(SUM(ISNULL(i.StockOnHand, 0)), 0)
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.CartridgeModelId = @CartridgeModelId
                      AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
                      AND i.Active = 1";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
                    int totalAvailable = Convert.ToInt32(cmd.ExecuteScalar());
                    return totalAvailable;
                }
            }
        }

        public int GetAvailableIssuableStockByCondition(int? cartridgeModelId, string condition)
        {
            if (!cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return 0;

            if (string.IsNullOrWhiteSpace(condition))
                return 0;

            // Map condition names to database values
            // "Brand New" = Item.RefillStatus IS NULL  (null is the Brand New marker; backward-compatible)
            // "Refilled"  = Item.RefillStatus = 'Available'  (set by BatchAddItemDialog and CompleteRefillAndRestockAsync)
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                string sql;
                if (condition.Equals("Brand New", StringComparison.OrdinalIgnoreCase))
                {
                    // Brand New: RefillStatus IS NULL (the authoritative marker)
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
                    // Refilled: RefillStatus = 'Available' (set by BatchAddItemDialog and refill reception)
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

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
                    int totalAvailable = Convert.ToInt32(cmd.ExecuteScalar());
                    return totalAvailable;
                }
            }
        }

        public List<int> GetIssuableItemIdsByCondition(int? cartridgeModelId, string condition, int quantity)
        {
            var result = new List<int>();
            if (quantity <= 0 || !cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return result;

            if (string.IsNullOrWhiteSpace(condition))
                return result;

            string sql;
            if (condition.Equals("Brand New", StringComparison.OrdinalIgnoreCase))
            {
                // Brand New: RefillStatus IS NULL (the authoritative marker)
                sql = @"
                    SELECT
                        i.ItemId,
                        i.DateCreated,
                        ISNULL(i.StockOnHand, 0) AS AvailableStock
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
                // Refilled: RefillStatus = 'Available' (set by BatchAddItemDialog and refill reception)
                sql = @"
                    SELECT
                        i.ItemId,
                        i.DateCreated,
                        ISNULL(i.StockOnHand, 0) AS AvailableStock
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

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    int remaining = quantity;
                    while (remaining > 0 && reader.Read())
                    {
                        int itemId = reader.GetInt32(0);
                        int availableStock = reader.GetInt32(2);
                        if (availableStock <= 0)
                            continue;

                        int take = Math.Min(availableStock, remaining);
                        for (int i = 0; i < take; i++)
                            result.Add(itemId);

                        remaining -= take;
                    }
                }
            }

            return result;
        }

        public List<int> GetIssuableItemIdsForQuantity(int? cartridgeModelId, int quantity)
        {
            var result = new List<int>();
            if (quantity <= 0 || !cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return result;

            // SINGLE SOURCE OF TRUTH: dbo.Item
            // DEPRECATED: dbo.Cartridge (no longer synchronized, contains stale data)
            //
            // ALIGNED WITH: GetAvailableIssuableStock logic
            // Only return items that have actual available stock after subtracting active requests
            // Filters: Category, CartridgeModelId, Active, AvailableStock > 0 (for issuance selection)
            // Do NOT filter: RefillStatus, StockType
            const string sql = @"
                SELECT
                    i.ItemId,
                    i.DateCreated,
                    ISNULL(i.StockOnHand, 0) AS AvailableStock
                FROM dbo.Item i
                WHERE i.Category = 'Cartridge'
                  AND i.CartridgeModelId = @CartridgeModelId
                  AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
                  AND i.Active = 1
                  AND ISNULL(i.StockOnHand, 0) > 0
                ORDER BY i.DateCreated ASC, i.ItemId ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    int remaining = quantity;
                    while (remaining > 0 && reader.Read())
                    {
                        int itemId = reader.GetInt32(0);
                        int availableStock = reader.GetInt32(2);
                        if (availableStock <= 0)
                            continue;

                        int take = Math.Min(availableStock, remaining);
                        for (int i = 0; i < take; i++)
                            result.Add(itemId);

                        remaining -= take;
                    }
                }
            }

            return result;
        }

        public List<CartridgeSelectionItem> GetUnavailableIssuedCartridges(List<int> issuedItemIds)
        {
            var result = new List<CartridgeSelectionItem>();
            if (issuedItemIds == null || issuedItemIds.Count == 0)
                return result;

            var distinctIds = issuedItemIds.Where(x => x > 0).Distinct().ToList();
            if (distinctIds.Count == 0)
                return result;

            var parameters = new List<string>();
            for (int i = 0; i < distinctIds.Count; i++)
                parameters.Add($"@p{i}");

            var sql = $@"
                SELECT
                    i.ItemId,
                    i.SerialNumber,
                    i.ModelNumber,
                    i.Name + ' - ' + ISNULL(i.SerialNumber, 'No S/N') AS DisplayName
                FROM dbo.Item i
                WHERE i.ItemId IN ({string.Join(",", parameters)})
                  AND (
                       i.Active <> 1
                    OR i.StockOnHand <= 0
                    OR ISNULL(i.RefillStatus, '') IN ('OUTBOUND', 'IN_USE', 'Issued', 'For Refill', 'Disposed')
                  )
                ORDER BY i.SerialNumber, i.ItemId;";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                for (int i = 0; i < distinctIds.Count; i++)
                    cmd.Parameters.AddWithValue($"@p{i}", distinctIds[i]);

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new CartridgeSelectionItem
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName"))
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets cartridges that can be issued (full/available cartridges).
        /// These are cartridges with Status = 'AVAILABLE' or 'FULL' matching the model.
        /// </summary>
        public List<CartridgeSelectionItem> GetIssuableCartridges(int? cartridgeModelId)
        {
            var items = new List<CartridgeSelectionItem>();

            if (!cartridgeModelId.HasValue || cartridgeModelId.Value <= 0)
                return items;

            // Get available cartridges of the same model using CartridgeModelId FK
            const string sql = @"
                SELECT
                    i.ItemId,
                    i.SerialNumber,
                    i.ModelNumber,
                    i.Name + ' - ' + ISNULL(i.SerialNumber, 'No S/N') AS DisplayName
                FROM dbo.Item i
                WHERE i.Category = 'Cartridge'
                  AND i.CartridgeModelId = @CartridgeModelId
                  AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'  -- returned empties held by IT, not stock
                  AND i.Active = 1
                  AND i.StockOnHand > 0
                  AND ISNULL(i.RefillStatus, '') NOT IN ('OUTBOUND', 'IN_USE', 'Issued', 'For Refill', 'Disposed')
                ORDER BY i.DateCreated ASC, i.ItemId ASC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId.Value);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new CartridgeSelectionItem
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName"))
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Fulfills a cartridge exchange request.
        ///
        /// IMPORTANT:
        /// - IT-selected ISSUED cartridges are recorded with actual ItemIds
        /// - Returned EMPTY cartridges: mark the same cartridges that were issued as "For Refill"
        /// - dbo.CartridgeMovement.ItemId is NOT NULL - all movements must have valid ItemIds
        ///
        /// All operations in a single transaction.
        /// </summary>
        /// <param name="reqId">Request ID being fulfilled</param>
        /// <param name="returnedQuantity">Number of empties to record as returned</param>
        /// <param name="issuedItemIds">IT-selected FULL cartridge ItemIds</param>
        /// <param name="userId">User performing the fulfillment</param>
        /// <param name="requestModelId">Optional: RequestModelId for multi-model requests</param>
        /// <param name="fulfillmentRemarks">Optional: Fulfillment remarks</param>
        /// <param name="actualIssuedQty">Optional: Actual quantity being issued (for aggregation fix)</param>
        public int FulfillCartridgeExchange(int reqId, int returnedQuantity, List<int> issuedItemIds, int userId, int? requestModelId = null, string fulfillmentRemarks = null, int? actualIssuedQty = null)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Get request details for movement records
                        int empId = 0;
                        int? branchId = null;
                        int? deptId = null;
                        int? existingSetId = null;

                        const string sqlGetRequest = @"
                            SELECT r.EmpId, e.BranchId, e.DeptId, r.SetId
                            FROM dbo.Request r
                            INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                            WHERE r.ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlGetRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    empId = reader.GetInt32(0);
                                    branchId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                                    deptId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                    existingSetId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                                }
                            }
                        }

                        // IMPORTANT: For multi-model portal cartridge requests, ensure they share one Set
                        // This must happen BEFORE creating individual Sets to avoid duplicates
                        // EnsureSharedSetForPortalCartridgeGroup will group related requests and return
                        // the shared SetId (or 0 if no grouping needed, e.g., legacy requests)
                        if (!existingSetId.HasValue || existingSetId.Value == 0)
                        {
                            int groupSetId = EnsureSharedSetForPortalCartridgeGroup(reqId, userId);
                            if (groupSetId > 0)
                            {
                                existingSetId = groupSetId;
                            }
                        }

                        // Create and link a Set for this fulfillment (Request → Set lifecycle)
                        int setId;
                        if (existingSetId.HasValue && existingSetId.Value > 0)
                        {
                            setId = existingSetId.Value;
                        }
                        else
                        {
                            const string sqlCreateSet = @"
                                DECLARE @NewSetIds TABLE (SetId INT);
                                INSERT INTO dbo.[Set] (CreatedBy, CreatedAt, Remarks, Status)
                                OUTPUT INSERTED.SetId INTO @NewSetIds
                                VALUES (@CreatedBy, GETDATE(), @Remarks, @Status);
                                SELECT SetId FROM @NewSetIds;";

                            using (var cmd = new SqlCommand(sqlCreateSet, con, transaction))
                            {
                                var setRemarks = string.IsNullOrWhiteSpace(fulfillmentRemarks)
                                    ? $"Cartridge Exchange - Request #{reqId}"
                                    : fulfillmentRemarks.Trim();

                                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                                cmd.Parameters.AddWithValue("@Remarks", setRemarks);
                                cmd.Parameters.AddWithValue("@Status", "Pending");
                                setId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            const string sqlLinkRequestToSet = @"
                                UPDATE dbo.Request
                                SET SetId = @SetId
                                WHERE ReqId = @ReqId";

                            using (var cmd = new SqlCommand(sqlLinkRequestToSet, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.ExecuteNonQuery();
                            }

                            // Mirror SetRepository.AddRequestToSetAsync behavior for first request
                            const string sqlUpdateSetReqIdAndType = @"
                                UPDATE s
                                SET s.ReqId = @ReqId,
                                    s.SetType = 'Cartridge'
                                FROM dbo.[Set] s
                                INNER JOIN dbo.Request r ON r.ReqId = @ReqId
                                INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                                WHERE s.SetId = @SetId AND s.ReqId IS NULL";

                            using (var cmd = new SqlCommand(sqlUpdateSetReqIdAndType, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // FIXED: Return the SAME cartridges that were issued, don't create new ones
                        // Find the issued cartridges for this request and mark them as returned
                        // AGGREGATION FIX: Handle both individual items and aggregated quantities
                        List<int> returnedItemIds = new List<int>();

                        if (issuedItemIds.Count > 0)
                        {
                            if (returnedQuantity > 0 && issuedItemIds.Count >= returnedQuantity)
                            {
                                returnedItemIds = issuedItemIds.Take(returnedQuantity).ToList();
                            }
                            else
                            {
                                returnedItemIds.Add(issuedItemIds[0]);
                            }
                        }
                        else
                        {
                            const string sqlFindIssuedForRequest = @"
                                SELECT TOP (@Qty) i.ItemId
                                FROM dbo.Item i
                                INNER JOIN dbo.CartridgeMovement cm ON i.ItemId = cm.ItemId
                                WHERE i.Category = 'Cartridge'
                                  AND cm.MovementType = 'Issued'
                                  AND cm.Remarks LIKE '%Request #' + CAST(@ReqId AS NVARCHAR) + '%'
                                ORDER BY cm.CreatedAt DESC";

                            using (var cmd = new SqlCommand(sqlFindIssuedForRequest, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Qty", Math.Max(returnedQuantity, 1));
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                        returnedItemIds.Add(reader.GetInt32(0));
                                }
                            }
                        }

                        // =====================================================
                        // CARTRIDGE RETURN FLOW: Insert to EmptyCartridge
                        // =====================================================
                        // CARTRIDGE LIFECYCLE RULES:
                        // 1. Issue → Stock deducted at fulfillment (SetRepository)
                        // 2. Return → INSERT to EmptyCartridge (THIS CODE)
                        // 3. Refill → INSERT new Item, restore stock (CartridgeRefillRepository)
                        //
                        // CRITICAL: During return, do NOT:
                        // - Modify Item.StockOnHand (stock unchanged until refill)
                        // - Set Item.Active = 0 (do not archive items)
                        // - Update Item.RefillStatus (legacy approach, not used)
                        // - Clone or duplicate Item rows
                        //
                        // CORRECT: During return, ONLY:
                        // - INSERT into dbo.EmptyCartridge (WIP tracking)
                        // - Record movement in CartridgeMovement (audit trail)
                        // - Leave Item table UNTOUCHED (items remain as-is)
                        //
                        // Returned cartridges are "in transit" until refill confirmed
                        // =====================================================

                        // GUARD: Only process returns when quantity > 0
                        if (returnedQuantity > 0 && returnedItemIds.Count > 0)
                        {
                            // Get CartridgeModelId and VendorId from first returned item
                            int cartridgeModelId = 0;
                            int vendorId = 0;
                            int conditionId = 0;    // EMPTY condition (legacy fallback)
                            int goodConditionId = 0;
                            int damagedConditionId = 0;
                            int goodEmptyQty = 0;
                            int damagedEmptyQty = 0;
                            bool isRefillableModel = false;

                            const string sqlGetConditions = @"
                                SELECT
                                    MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                                    MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                                    MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                                FROM dbo.Condition
                                WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')";

                            using (var cmd = new SqlCommand(sqlGetConditions, con, transaction))
                            {
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        goodConditionId    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                        damagedConditionId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                        conditionId        = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                    }
                                }
                            }

                            if (requestModelId.HasValue && requestModelId.Value > 0)
                            {
                                const string sqlGetModelFromRequestModel = @"
                                    IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                                    BEGIN
                                        SELECT TOP 1
                                            cm.CartridgeModelId,
                                            vcm.VendorId,
                                            ISNULL(crm.GoodEmptyQty,    0),
                                            ISNULL(crm.DamagedEmptyQty, 0)
                                        FROM dbo.CartridgeRequestModel crm
                                        INNER JOIN dbo.CartridgeModel cm
                                            ON LTRIM(RTRIM(crm.CartridgeModel)) = cm.ModelNumber
                                        LEFT JOIN dbo.VendorCartridgeModel vcm
                                            ON vcm.CartridgeModelId = cm.CartridgeModelId AND vcm.IsActive = 1
                                        WHERE crm.RequestModelId = @RequestModelId;
                                    END";

                                using (var cmd = new SqlCommand(sqlGetModelFromRequestModel, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            cartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                            vendorId         = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                            goodEmptyQty     = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                            damagedEmptyQty  = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                                            isRefillableModel = cartridgeModelId > 0;
                                        }
                                    }
                                }
                            }

                            if ((!requestModelId.HasValue || requestModelId.Value <= 0) && cartridgeModelId <= 0)
                            {
                                const string sqlGetModelFromRequest = @"
                                    SELECT TOP 1
                                        cm.CartridgeModelId,
                                        vcm.VendorId
                                    FROM dbo.Request r
                                    CROSS APPLY (
                                        SELECT LTRIM(RTRIM(
                                            SUBSTRING(
                                                r.Description,
                                                CHARINDEX('[MODEL:', r.Description) + 7,
                                                CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) - (CHARINDEX('[MODEL:', r.Description) + 7)
                                            )
                                        )) AS ModelNumber
                                    ) x
                                    INNER JOIN dbo.CartridgeModel cm
                                        ON cm.ModelNumber = x.ModelNumber
                                    LEFT JOIN dbo.VendorCartridgeModel vcm
                                        ON vcm.CartridgeModelId = cm.CartridgeModelId AND vcm.IsActive = 1
                                    WHERE r.ReqId = @ReqId
                                      AND r.Description IS NOT NULL
                                      AND CHARINDEX('[MODEL:', r.Description) > 0
                                      AND CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) > 0
                                      AND CHARINDEX('[MODELS:', r.Description) = 0";

                                using (var cmd = new SqlCommand(sqlGetModelFromRequest, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            cartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                            vendorId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                            isRefillableModel = cartridgeModelId > 0;
                                        }
                                    }
                                }
                            }

                            const string sqlGetItemInfo = @"
                                SELECT
                                    i.CartridgeModelId,
                                    COALESCE(vcm.VendorId, i.VendorId) AS VendorId,
                                    c.ConditionId,
                                    CASE WHEN cm.CartridgeModelId IS NOT NULL THEN 1 ELSE 0 END AS IsRefillable
                                FROM dbo.Item i
                                LEFT JOIN dbo.Condition c ON c.ConditionName = 'EMPTY'
                                LEFT JOIN dbo.CartridgeModel cm ON i.CartridgeModelId = cm.CartridgeModelId
                                LEFT JOIN dbo.VendorCartridgeModel vcm
                                    ON vcm.CartridgeModelId = cm.CartridgeModelId AND vcm.IsActive = 1
                                WHERE i.ItemId = @ItemId";

                            if (cartridgeModelId <= 0)
                            {
                                using (var cmd = new SqlCommand(sqlGetItemInfo, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId", returnedItemIds[0]);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            cartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                            vendorId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                            conditionId = reader.IsDBNull(2) ? conditionId : reader.GetInt32(2);
                                            isRefillableModel = reader.GetInt32(3) == 1;
                                        }
                                    }
                                }
                            }

                            // Re-evaluate isRefillableModel using the authoritative CartridgeModel.IsRefillable
                            // flag. Earlier lookup paths set this based only on cartridgeModelId > 0.
                            // Also capture ModelNumber for use in the IT inventory row (non-refillable path).
                            isRefillableModel = false;
                            string modelNumber = string.Empty;
                            if (cartridgeModelId > 0)
                            {
                                using (var cmd = new SqlCommand("SELECT IsRefillable, ModelNumber FROM dbo.CartridgeModel WHERE CartridgeModelId = @ModelId", con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            isRefillableModel = !reader.IsDBNull(0) && reader.GetBoolean(0);
                                            modelNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                                        }
                                    }
                                }
                            }

                            // All returned cartridges create an EmptyCartridge row.
                            // IsRefillable controls RefillStatus and downstream batching eligibility only.
                            if (cartridgeModelId > 0)
                            {
                                // Non-refillable models: create the IT custody Item FIRST so its ItemId
                                // can be written into SourceItemId on each EmptyCartridge row.  The
                                // dispose/sell workflow later uses SourceItemId to find the inventory row
                                // to decrement and to anchor the Inventory / CartridgeMovement records.
                                int? itCustodyItemId = null;
                                if (!isRefillableModel)
                                {
                                    const string sqlInsertITItem = @"
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
                                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                    using (var cmd = new SqlCommand(sqlInsertITItem, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@Name",             $"{modelNumber} - Returned Empty");
                                        cmd.Parameters.AddWithValue("@Description",      $"Returned empty cartridge (non-refillable) - IT custody - Request #{reqId}");
                                        cmd.Parameters.AddWithValue("@Quantity",          returnedQuantity);
                                        cmd.Parameters.AddWithValue("@CartridgeModelId",  cartridgeModelId);
                                        cmd.Parameters.AddWithValue("@ModelNumber",       modelNumber);
                                        cmd.Parameters.AddWithValue("@ConditionID",       conditionId > 0 ? (object)conditionId : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Remarks",           $"Non-refillable - IT custody - Request #{reqId}");
                                        cmd.Parameters.AddWithValue("@CreatedBy",         userId);
                                        var idResult = cmd.ExecuteScalar();
                                        itCustodyItemId = idResult != null && idResult != DBNull.Value
                                            ? Convert.ToInt32(idResult) : (int?)null;
                                    }
                                }

                                // INSERT one EmptyCartridge row per physical unit returned.
                                // VendorId is NULL — vendor is unknown at the empty stage and is
                                // assigned only when a refill batch is created by Purchasing.
                                // VendorBatchId is NULL — batch is assigned manually via the
                                // manual batch-assignment wizard after a batch is created.
                                // RefillStatus is 'For Refill' only for refillable models; NULL otherwise.
                                // SourceItemId links each non-refillable unit to its IT custody Item row.
                                const string sqlInsertEmpty = @"
                                    INSERT INTO dbo.EmptyCartridge
                                        (CartridgeModelId, VendorId, VendorBatchId, Quantity, ConditionId, ConditionStatus, Status,
                                         RefillStatus, ReturnedAt, ReturnedBy, ReqId, EmpId, BranchId, DeptId,
                                         Remarks, CreatedDate, CreatedBy, SourceItemId)
                                    VALUES
                                        (@CartridgeModelId, NULL, NULL, 1, @ConditionId, @ConditionStatus, 'Pending',
                                         @RefillStatus, GETDATE(), @ReturnedBy, @ReqId, @EmpId, @BranchId, @DeptId,
                                         @Remarks, GETDATE(), @CreatedBy, @SourceItemId)";

                                // When portal supplies a good/damaged split that matches returnedQuantity,
                                // create each group with the appropriate ConditionId and ConditionStatus.
                                // Otherwise fall back to a single loop, deriving ConditionStatus from
                                // conditionId so the column is always populated (never NULL).
                                bool hasSplit = returnedQuantity > 0
                                    && (goodEmptyQty + damagedEmptyQty) == returnedQuantity;

                                var emptyBatches = hasSplit
                                    ? new (int qty, int condId, string condStatus)[]
                                      {
                                          (goodEmptyQty,    goodConditionId    > 0 ? goodConditionId    : conditionId, "GOOD"),
                                          (damagedEmptyQty, damagedConditionId > 0 ? damagedConditionId : conditionId, "DAMAGED")
                                      }
                                    : new (int qty, int condId, string condStatus)[]
                                      {
                                          // Derive ConditionStatus from conditionId so it is never NULL.
                                          // Only the explicit Damaged condition ID maps to 'DAMAGED';
                                          // EMPTY, Good, unknown, or NULL all default to 'GOOD'.
                                          (returnedQuantity, conditionId,
                                           damagedConditionId > 0 && conditionId == damagedConditionId
                                               ? "DAMAGED" : "GOOD")
                                      };

                                foreach (var (batchQty, batchCondId, batchCondStatus) in emptyBatches)
                                {
                                    for (int i = 0; i < batchQty; i++)
                                    {
                                        // Damaged cartridges must never enter the refill queue.
                                        // RefillStatus = 'For Refill' is set only when the model is
                                        // refillable AND this specific cartridge is not damaged.
                                        string refillStatus = isRefillableModel && batchCondStatus != "DAMAGED"
                                            ? "For Refill" : null;

                                        using (var cmd = new SqlCommand(sqlInsertEmpty, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                                            cmd.Parameters.AddWithValue("@ConditionId",     batchCondId == 0 ? (object)DBNull.Value : batchCondId);
                                            cmd.Parameters.AddWithValue("@ConditionStatus", (object)batchCondStatus ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@RefillStatus",    (object)refillStatus ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@ReturnedBy", empId > 0 ? (object)empId : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@ReqId",      reqId);
                                            cmd.Parameters.AddWithValue("@EmpId",      empId > 0 ? (object)empId : DBNull.Value);
                                            cmd.Parameters.AddWithValue("@BranchId",   (object)branchId ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@DeptId",     (object)deptId ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@Remarks",    $"Empty returned - Request #{reqId}");
                                            cmd.Parameters.AddWithValue("@CreatedBy",  userId);
                                            cmd.Parameters.AddWithValue("@SourceItemId", itCustodyItemId.HasValue ? (object)itCustodyItemId.Value : DBNull.Value);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // Record movement for audit trail (existing behavior preserved)
                            RecordCartridgeMovement(con, transaction, returnedItemIds[0], MovementType.ReturnForRefill,
                                returnedQuantity, empId, branchId, deptId, "EMPTY", reqId, userId,
                                $"Returned {returnedQuantity} Empty Cartridge(s) - Request #{reqId}");
                        }

                        // =====================================================
                        // END SURGICAL INSERTION
                        // =====================================================

                        // Record IT-selected issued cartridges (MovementType = Issue)
                        // AGGREGATION FIX: Handle both individual items and aggregated quantities
                        int totalIssuedQty = actualIssuedQty ?? issuedItemIds.Count;

                        // CRITICAL FIX: Only record 'Issued' movements if cartridges were actually issued
                        // This prevents "Issued Full" from showing non-zero values in partially fulfilled summaries
                        // when nothing was actually issued due to insufficient stock
                        if (totalIssuedQty > 0 && issuedItemIds.Count > 0)
                        {
                            if (issuedItemIds.Count == 1 && actualIssuedQty.HasValue && actualIssuedQty.Value > 1)
                        {
                            // Aggregated case: One Item row represents multiple physical units
                            var itemId = issuedItemIds[0];
                            RecordCartridgeMovement(con, transaction, itemId, MovementType.Issue,
                                actualIssuedQty.Value, empId, branchId, deptId, "FULL", reqId, userId,
                                $"Issued {actualIssuedQty.Value} cartridge(s) to employee - Request #{reqId}");

                            // REMOVED: Do NOT set RefillStatus='OUTBOUND' on partial issue from aggregated stock
                            // Items with StockOnHand > 0 should remain available for future issues
                            // RefillStatus is a legacy field - stock availability is tracked by StockOnHand
                            DecreaseItemStock(con, transaction, itemId, actualIssuedQty.Value, userId);
                        }
                        else
                        {
                                // Individual case: Each ItemId represents one physical cartridge
                                foreach (var itemId in issuedItemIds)
                                {
                                    RecordCartridgeMovement(con, transaction, itemId, MovementType.Issue,
                                        1, empId, branchId, deptId, "FULL", reqId, userId,
                                        $"Issued to employee - Request #{reqId}");

                                    // REMOVED: Do NOT set RefillStatus='OUTBOUND' during issue
                                    // Stock availability is tracked by StockOnHand, not RefillStatus
                                    // Setting OUTBOUND on the entire Item prevents remaining stock from being issued
                                    DecreaseItemStock(con, transaction, itemId, 1, userId);
                                }
                            }
                        }

                        // Update per-model tracking row (multi-model requests)
                        if (requestModelId.HasValue && requestModelId.Value > 0)
                        {
                            // CRITICAL FIX: Use actualIssuedQty if provided, otherwise count ItemIds
                            // This handles cases where one Item row represents multiple physical units
                            int qtyToIncrement = actualIssuedQty ?? issuedItemIds.Count;

                            // CRITICAL FIX: Increment IssuedFullQty instead of overwriting it
                            // This preserves historical issuance data across multiple fulfillment operations
                            const string sqlUpdateRequestModel = @"
                                IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                                BEGIN
                                    UPDATE dbo.CartridgeRequestModel
                                    SET
                                        IssuedFullQty = ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty,
                                        ReturnedEmptyQty = @ReturnedEmptyQty,
                                        UnfulfilledQty = RequestedQty - (ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty),
                                        Status = CASE
                                            WHEN RequestedQty - (ISNULL(IssuedFullQty, 0) + @AdditionalIssuedQty) <= 0
                                            THEN 'Fulfilled'
                                            ELSE 'Pending'
                                        END,
                                        Remarks = COALESCE(NULLIF(@Remarks, ''), Remarks),
                                        ProcessedDate = GETDATE(),
                                        ProcessedBy = @ProcessedBy
                                    WHERE RequestModelId = @RequestModelId;
                                END";

                            using (var cmd = new SqlCommand(sqlUpdateRequestModel, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                                cmd.Parameters.AddWithValue("@AdditionalIssuedQty", qtyToIncrement);
                                cmd.Parameters.AddWithValue("@ReturnedEmptyQty", returnedQuantity);
                                cmd.Parameters.AddWithValue("@Remarks", (object)fulfillmentRemarks ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@ProcessedBy", userId);
                                cmd.ExecuteNonQuery();
                            }

                            // CRITICAL SYNCHRONIZATION FIX: Update UnfulfilledCartridgeExchange table
                            // This prevents fulfilled requests from reappearing as pending
                            // Get the cartridge model number and requested quantity for this request
                            // IMPORTANT: Use ModelNumber (not CartridgeModel display name) for lookup compatibility
                            string cartridgeModel = null;
                            int requestedQty = 0;
                            const string sqlGetModel = @"
                                SELECT ISNULL(cm.ModelNumber, crm.CartridgeModel) AS ModelNumber, crm.RequestedQty
                                FROM dbo.CartridgeRequestModel crm
                                LEFT JOIN dbo.CartridgeModel cm ON LTRIM(RTRIM(cm.ModelNumber)) = LTRIM(RTRIM(crm.CartridgeModel))
                                WHERE crm.RequestModelId = @RequestModelId";

                            using (var cmd = new SqlCommand(sqlGetModel, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        cartridgeModel = reader.IsDBNull(0) ? null : reader.GetString(0);
                                        requestedQty = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(cartridgeModel))
                            {
                                // CRITICAL FIX: MERGE (upsert) UnfulfilledCartridgeExchange record
                                // This automatically CREATES the record if it doesn't exist (fixing PROBLEM 2)
                                // and UPDATES if it already exists
                                const string sqlSyncUnfulfilled = @"
                                    IF OBJECT_ID('dbo.UnfulfilledCartridgeExchange', 'U') IS NOT NULL
                                    BEGIN
                                        MERGE dbo.UnfulfilledCartridgeExchange AS target
                                        USING (
                                            SELECT
                                                @ReqId AS ReqId,
                                                @CartridgeModel AS CartridgeModel,
                                                @RequestedQty AS RequestedQty,
                                                @ReturnedQty AS ReturnedEmptyQty,
                                                @AdditionalIssuedQty AS AdditionalIssuedQty,
                                                @EmpId AS EmpId,
                                                @BranchId AS BranchId,
                                                @DeptId AS DeptId,
                                                @ProcessedBy AS ProcessedBy,
                                                @Remarks AS Remarks
                                        ) AS source
                                        ON target.ReqId = source.ReqId
                                           AND UPPER(LTRIM(RTRIM(target.CartridgeModel))) = UPPER(LTRIM(RTRIM(source.CartridgeModel)))
                                        WHEN MATCHED AND target.Status = 'Pending' THEN
                                            UPDATE SET
                                                IssuedFullQty = ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty,
                                                UnfulfilledQty = target.ReturnedEmptyQty - (ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty),
                                                Status = CASE
                                                    WHEN target.ReturnedEmptyQty - (ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty) <= 0
                                                    THEN 'Fulfilled'
                                                    ELSE 'Pending'
                                                END,
                                                FulfilledDate = CASE
                                                    WHEN target.ReturnedEmptyQty - (ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty) <= 0
                                                    THEN GETDATE()
                                                    ELSE target.FulfilledDate
                                                END,
                                                FulfilledBy = source.ProcessedBy,
                                                FulfilledRemarks = COALESCE(NULLIF(source.Remarks, ''), target.FulfilledRemarks)
                                        WHEN NOT MATCHED THEN
                                            INSERT (ReqId, EmpId, BranchId, DeptId, CartridgeModel, RequestedQty, ReturnedEmptyQty,
                                                    IssuedFullQty, UnfulfilledQty, Remarks, Status, CreatedDate, CreatedBy)
                                            VALUES (source.ReqId, source.EmpId, source.BranchId, source.DeptId, source.CartridgeModel,
                                                    source.RequestedQty, source.ReturnedEmptyQty, source.AdditionalIssuedQty,
                                                    source.ReturnedEmptyQty - source.AdditionalIssuedQty,
                                                    source.Remarks,
                                                    CASE WHEN source.ReturnedEmptyQty - source.AdditionalIssuedQty <= 0 THEN 'Fulfilled' ELSE 'Pending' END,
                                                    GETDATE(), source.ProcessedBy);
                                    END";

                                using (var cmd = new SqlCommand(sqlSyncUnfulfilled, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                                    cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel);
                                    cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);
                                    cmd.Parameters.AddWithValue("@ReturnedQty", returnedQuantity);
                                    cmd.Parameters.AddWithValue("@AdditionalIssuedQty", qtyToIncrement);
                                    cmd.Parameters.AddWithValue("@EmpId", empId);
                                    cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ProcessedBy", userId);
                                    cmd.Parameters.AddWithValue("@Remarks", (object)fulfillmentRemarks ?? DBNull.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // CRITICAL SYNCHRONIZATION FIX: For legacy single-model requests (no requestModelId),
                        // also synchronize UnfulfilledCartridgeExchange table
                        if (!requestModelId.HasValue || requestModelId.Value == 0)
                        {
                            int qtyToIncrement = actualIssuedQty ?? issuedItemIds.Count;

                            // Get cartridge model and requested quantity for this request
                            string cartridgeModel = null;
                            int requestedQty = 0;
                            const string sqlGetRequestModel = @"
                                SELECT i.ModelNumber, r.Quantity
                                FROM dbo.Request r
                                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                                WHERE r.ReqId = @ReqId";

                            using (var cmd = new SqlCommand(sqlGetRequestModel, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        cartridgeModel = reader.IsDBNull(0) ? null : reader.GetString(0);
                                        requestedQty = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(cartridgeModel))
                            {
                                // CRITICAL FIX: MERGE (upsert) UnfulfilledCartridgeExchange for legacy requests
                                // This automatically CREATES the record if it doesn't exist (fixing PROBLEM 2)
                                // and UPDATES if it already exists
                                const string sqlSyncUnfulfilledLegacy = @"
                                    IF OBJECT_ID('dbo.UnfulfilledCartridgeExchange', 'U') IS NOT NULL
                                    BEGIN
                                        MERGE dbo.UnfulfilledCartridgeExchange AS target
                                        USING (
                                            SELECT
                                                @ReqId AS ReqId,
                                                @CartridgeModel AS CartridgeModel,
                                                @RequestedQty AS RequestedQty,
                                                @ReturnedQty AS ReturnedEmptyQty,
                                                @AdditionalIssuedQty AS AdditionalIssuedQty,
                                                @EmpId AS EmpId,
                                                @BranchId AS BranchId,
                                                @DeptId AS DeptId,
                                                @ProcessedBy AS ProcessedBy,
                                                @Remarks AS Remarks
                                        ) AS source
                                        ON target.ReqId = source.ReqId
                                           AND UPPER(LTRIM(RTRIM(target.CartridgeModel))) = UPPER(LTRIM(RTRIM(source.CartridgeModel)))
                                        WHEN MATCHED AND target.Status = 'Pending' THEN
                                            UPDATE SET
                                                IssuedFullQty = ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty,
                                                UnfulfilledQty = target.ReturnedEmptyQty - (ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty),
                                                Status = CASE
                                                    WHEN target.ReturnedEmptyQty - (ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty) <= 0
                                                    THEN 'Fulfilled'
                                                    ELSE 'Pending'
                                                END,
                                                FulfilledDate = CASE
                                                    WHEN target.ReturnedEmptyQty - (ISNULL(target.IssuedFullQty, 0) + source.AdditionalIssuedQty) <= 0
                                                    THEN GETDATE()
                                                    ELSE target.FulfilledDate
                                                END,
                                                FulfilledBy = source.ProcessedBy,
                                                FulfilledRemarks = COALESCE(NULLIF(source.Remarks, ''), target.FulfilledRemarks)
                                        WHEN NOT MATCHED THEN
                                            INSERT (ReqId, EmpId, BranchId, DeptId, CartridgeModel, RequestedQty, ReturnedEmptyQty,
                                                    IssuedFullQty, UnfulfilledQty, Remarks, Status, CreatedDate, CreatedBy)
                                            VALUES (source.ReqId, source.EmpId, source.BranchId, source.DeptId, source.CartridgeModel,
                                                    source.RequestedQty, source.ReturnedEmptyQty, source.AdditionalIssuedQty,
                                                    source.ReturnedEmptyQty - source.AdditionalIssuedQty,
                                                    source.Remarks,
                                                    CASE WHEN source.ReturnedEmptyQty - source.AdditionalIssuedQty <= 0 THEN 'Fulfilled' ELSE 'Pending' END,
                                                    GETDATE(), source.ProcessedBy);
                                    END";

                                using (var cmd = new SqlCommand(sqlSyncUnfulfilledLegacy, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                                    cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel);
                                    cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);
                                    cmd.Parameters.AddWithValue("@ReturnedQty", returnedQuantity);
                                    cmd.Parameters.AddWithValue("@AdditionalIssuedQty", qtyToIncrement);
                                    cmd.Parameters.AddWithValue("@EmpId", empId);
                                    cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ProcessedBy", userId);
                                    cmd.Parameters.AddWithValue("@Remarks", (object)fulfillmentRemarks ?? DBNull.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Update parent request status.
                        // - Legacy single-model: mark Submitted immediately.
                        // - Multi-model: mark Submitted only when all line items are no longer pending.
                        const string sqlUpdateRequest = @"
                            DECLARE @TotalRequested INT = 0;
                            DECLARE @TotalIssued    INT = 0;
                            DECLARE @UnprocessedModels INT = 0;

                            IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                               AND EXISTS (SELECT 1 FROM dbo.CartridgeRequestModel WHERE ReqId = @ReqId)
                            BEGIN
                                SELECT
                                    @TotalRequested    = SUM(RequestedQty),
                                    @TotalIssued       = SUM(ISNULL(IssuedFullQty, 0)),
                                    @UnprocessedModels = COUNT(CASE WHEN IssuedFullQty IS NULL THEN 1 END)
                                FROM dbo.CartridgeRequestModel
                                WHERE ReqId = @ReqId;

                                IF NOT EXISTS (
                                    SELECT 1
                                    FROM dbo.CartridgeRequestModel
                                    WHERE ReqId = @ReqId
                                      AND ISNULL(NULLIF(Status, ''), 'Pending') IN ('Pending', 'Under Review', 'Processing')
                                )
                                BEGIN
                                    -- All models fully fulfilled
                                    UPDATE dbo.Request
                                    SET Status = 'Submitted',
                                        DateModified = GETDATE(),
                                        ModifiedBy = @UserId
                                    WHERE ReqId = @ReqId;
                                END
                                ELSE
                                BEGIN
                                    -- Some models still pending; compute granular status
                                    UPDATE dbo.Request
                                    SET Status = CASE
                                        WHEN @TotalIssued = 0 AND @UnprocessedModels = 0 THEN 'Unfulfilled'
                                        WHEN @TotalIssued > 0 AND @TotalIssued < @TotalRequested THEN 'Partially Fulfilled'
                                        WHEN Status IN ('Pending', 'Under Review') THEN 'Processing'
                                        ELSE Status
                                    END,
                                    DateModified = GETDATE(),
                                    ModifiedBy = @UserId
                                    WHERE ReqId = @ReqId;
                                END
                            END
                            ELSE
                            BEGIN
                                UPDATE dbo.Request
                                SET Status = 'Submitted',
                                    DateModified = GETDATE(),
                                    ModifiedBy = @UserId
                                WHERE ReqId = @ReqId;
                            END";

                        using (var cmd = new SqlCommand(sqlUpdateRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // CRITICAL: Update Set status to Dispatched/Partial/Pending before SMTP
                        // Cartridge Sets auto-dispatch (do NOT require QR generation)
                        // - All requests Submitted/Completed → Set = "Dispatched" (auto-dispatch)
                        // - Some Submitted/Completed, some Pending/Processing → Set = "Partial"
                        // - All Pending/Processing → Set = "Pending"
                        const string sqlUpdateSetStatus = @"
                            DECLARE @TotalRequests INT = 0;
                            DECLARE @SubmittedRequests INT = 0;

                            SELECT @TotalRequests = COUNT(*)
                            FROM dbo.Request
                            WHERE SetId = @SetId;

                            SELECT @SubmittedRequests = COUNT(*)
                            FROM dbo.Request
                            WHERE SetId = @SetId
                              AND Status IN ('Submitted', 'Completed');

                            UPDATE dbo.[Set]
                            SET Status = CASE
                                WHEN @SubmittedRequests = @TotalRequests THEN 'Dispatched'
                                WHEN @SubmittedRequests > 0 THEN 'Partial'
                                ELSE 'Pending'
                            END
                            WHERE SetId = @SetId;";

                        using (var cmd = new SqlCommand(sqlUpdateSetStatus, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            cmd.ExecuteNonQuery();
                        }

                        // CRITICAL: Materialize dbo.SetItem from dbo.Request before finalizing
                        // This ensures dbo.SetItem is populated for SMTP emails
                        // Canonical flow: dbo.Request → dbo.Set → Set.Status update → dbo.SetItem → SMTP
                        // SetItem must be materialized exactly once when Set is finalized
                        //
                        // ItemCode priority for cartridge items:
                        //   1. [MODEL:XXX] tag extracted from Request.Description (set by portal at submit time)
                        //   2. CartridgeModel.ModelNumber via Item.CartridgeModelId FK (authoritative catalog)
                        //   3. Item.ModelNumber (last-resort generic fallback)
                        //
                        // GUARD: RAISERROR + rollback if any cartridge item resolves to NULL ItemCode.
                        // This prevents silent placeholder persistence (the root cause of the UNKNOWN bug).
                        const string sqlMaterializeSetItem = @"
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
                                    -- ItemCode: [MODEL:XXX] tag → CartridgeModel.ModelNumber → Item.ModelNumber
                                    -- NULLIF(...,'') ensures an empty extraction is treated the same as NULL.
                                    -- The CHARINDEX(']',...) > 0 guard prevents negative-length SUBSTRING.
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
                                    -- Description: always 'Cartridge Exchange - <model>' for cartridge items.
                                    -- Uses same COALESCE chain as ItemCode for consistency.
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

                                -- Guard: if any cartridge SetItem still has NULL ItemCode, the item has no
                                -- model information at all. Reject now so the transaction rolls back cleanly
                                -- rather than persisting a placeholder that breaks downstream features
                                -- (e.g. Assign Returns to Refill Batches groups returns by ItemCode/model).
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
                            END";

                        using (var cmd = new SqlCommand(sqlMaterializeSetItem, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            cmd.ExecuteNonQuery();
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
            }
        }

        public (bool Success, string Message) ForceDeleteCartridgeRequest(int reqId, int userId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string sqlCheck = @"
                            SELECT TOP 1 i.Category
                            FROM dbo.Request r
                            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                            WHERE r.ReqId = @ReqId";

                        string category;
                        using (var cmd = new SqlCommand(sqlCheck, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            var result = cmd.ExecuteScalar();
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

                        var modelLines = new List<(int? RequestModelId, string Model, int Qty, string Condition)>();
                        if (TableExists(con, transaction, "dbo.CartridgeRequestModel"))
                        {
                            const string sqlModels = @"
                                SELECT RequestModelId, CartridgeModel, RequestedQty, Remarks
                                FROM dbo.CartridgeRequestModel
                                WHERE ReqId = @ReqId
                                ORDER BY RequestModelId";

                            using (var cmd = new SqlCommand(sqlModels, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int? requestModelId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                                        string model = reader.IsDBNull(1) ? null : reader.GetString(1);
                                        int qty = reader.GetInt32(2);
                                        string condition = reader.IsDBNull(3) ? null : reader.GetString(3);
                                        modelLines.Add((requestModelId, model, qty, condition));
                                    }
                                }
                            }
                        }

                        // Both remark formats used by FulfillCartridgeExchange:
                        //   Individual:   "Issued to employee - Request #{reqId}"
                        //   Aggregated:   "Issued {qty} cartridge(s) to employee - Request #{reqId}"
                        // Both end with "to employee - Request #{reqId}", so match on that suffix.
                        string issuedRemarksPattern = $"%to employee%Request #{reqId}%";

                        const string sqlGetIssuedMovements = @"
                            SELECT cm.ItemId, cm.Quantity
                            FROM dbo.CartridgeMovement cm
                            WHERE cm.MovementType = 'Issued'
                              AND cm.Remarks LIKE @RemarksPattern";

                        var issuedMovements = new List<(int ItemId, int Qty)>();
                        using (var cmd = new SqlCommand(sqlGetIssuedMovements, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@RemarksPattern", issuedRemarksPattern);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                    issuedMovements.Add((reader.GetInt32(0), reader.GetInt32(1)));
                            }
                        }

                        foreach (var (itemId, qty) in issuedMovements)
                        {
                            // Mirror the exact inverse of DecreaseItemStock: add back what was subtracted.
                            // Do NOT touch Active or RefillStatus — those are not modified on issue.
                            const string sqlRestoreIssued = @"
                                UPDATE dbo.Item
                                SET StockOnHand = ISNULL(StockOnHand, 0) + @Qty,
                                    DateModified = GETDATE(),
                                    ModifiedBy = @UserId
                                WHERE ItemId = @ItemId";

                            using (var cmd = new SqlCommand(sqlRestoreIssued, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Qty", qty);
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Reverse VendorCartridgeBatch.ReturnedQty before deleting EmptyCartridge rows.
                        // FulfillCartridgeExchange increments ReturnedQty per batch; undo that here.
                        if (TableExists(con, transaction, "dbo.EmptyCartridge") &&
                            TableExists(con, transaction, "dbo.VendorCartridgeBatch"))
                        {
                            const string sqlGetBatchQtys = @"
                                SELECT VendorBatchId, SUM(Quantity) AS TotalQty
                                FROM dbo.EmptyCartridge
                                WHERE ReqId = @ReqId
                                  AND VendorBatchId IS NOT NULL
                                GROUP BY VendorBatchId";

                            var batchQtys = new List<(int BatchId, int Qty)>();
                            using (var cmd = new SqlCommand(sqlGetBatchQtys, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                        batchQtys.Add((reader.GetInt32(0), reader.GetInt32(1)));
                                }
                            }

                            foreach (var (batchId, qty) in batchQtys)
                            {
                                const string sqlRevertBatch = @"
                                    UPDATE dbo.VendorCartridgeBatch
                                    SET ReturnedQty = CASE WHEN ReturnedQty >= @Qty THEN ReturnedQty - @Qty ELSE 0 END
                                    WHERE BatchId = @BatchId";
                                using (var cmd = new SqlCommand(sqlRevertBatch, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                                    cmd.Parameters.AddWithValue("@Qty", qty);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Delete auto-recorded returned-empty rows from EmptyCartridge.
                        if (TableExists(con, transaction, "dbo.EmptyCartridge"))
                        {
                            const string sqlDeleteEmptyCartridge = @"DELETE FROM dbo.EmptyCartridge WHERE ReqId = @ReqId";
                            using (var cmd = new SqlCommand(sqlDeleteEmptyCartridge, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Delete all CartridgeMovement rows tied to this request (both issue and return).
                        // FulfillCartridgeExchange uses "Request #{reqId}" in every remark it writes.
                        const string sqlDeleteRequestMovements = @"
                            DELETE FROM dbo.CartridgeMovement
                            WHERE MovementType IN ('Issued', 'Returned')
                              AND Remarks LIKE @RequestPattern";

                        using (var cmd = new SqlCommand(sqlDeleteRequestMovements, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@RequestPattern", $"%Request #{reqId}%");
                            cmd.ExecuteNonQuery();
                        }

                        if (TableExists(con, transaction, "dbo.Inventory"))
                        {
                            const string sqlDeleteInventory = @"DELETE FROM dbo.Inventory WHERE ReqId = @ReqId";
                            using (var cmd = new SqlCommand(sqlDeleteInventory, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        if (TableExists(con, transaction, "dbo.UnfulfilledCartridgeExchange"))
                        {
                            const string sqlDeleteUnfulfilled = @"DELETE FROM dbo.UnfulfilledCartridgeExchange WHERE ReqId = @ReqId";
                            using (var cmd = new SqlCommand(sqlDeleteUnfulfilled, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        if (TableExists(con, transaction, "dbo.CartridgeRequestModel"))
                        {
                            const string sqlDeleteChild = @"DELETE FROM dbo.CartridgeRequestModel WHERE ReqId = @ReqId";
                            using (var cmd = new SqlCommand(sqlDeleteChild, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        const string sqlDeleteRequest = @"DELETE FROM dbo.Request WHERE ReqId = @ReqId";
                        int deleted;
                        using (var cmd = new SqlCommand(sqlDeleteRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            deleted = cmd.ExecuteNonQuery();
                        }

                        if (deleted == 0)
                        {
                            transaction.Rollback();
                            return (false, "Request not found or already deleted.");
                        }

                        transaction.Commit();

                        var lineCount = modelLines.Count;
                        var issuedCount = issuedMovements.Sum(x => x.Qty);
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

        private static bool TableExists(SqlConnection con, SqlTransaction transaction, string fullTableName)
        {
            const string sql = @"SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NULL THEN 0 ELSE 1 END";
            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@TableName", fullTableName);
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        /// <summary>
        /// Groups multi-model cartridge requests from the same portal submission into a single Set.
        /// Portal submissions create multiple Request rows (one per model) that should share one Set.
        /// Grouping criteria: same SubmissionSessionId (UNIQUEIDENTIFIER from Request Portal).
        /// Legacy requests with NULL SubmissionSessionId are treated as standalone (return 0).
        /// </summary>
        public int EnsureSharedSetForPortalCartridgeGroup(int reqId, int userId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Get info about this request
                        int? existingSetId = null;
                        Guid? submissionSessionId = null;

                        const string sqlGetRequestInfo = @"
                            SELECT r.SetId, r.SubmissionSessionId
                            FROM dbo.Request r
                            WHERE r.ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlGetRequestInfo, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    existingSetId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                                    submissionSessionId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
                                }
                            }
                        }

                        // If already has a Set, return it
                        if (existingSetId.HasValue && existingSetId.Value > 0)
                            return existingSetId.Value;

                        // Check if this is a portal request with SubmissionSessionId
                        if (!submissionSessionId.HasValue)
                        {
                            // Legacy request with NULL SubmissionSessionId, no grouping needed
                            return 0; // Caller will create individual Set
                        }

                        // Find related portal requests from same submission session
                        // Grouping criteria: same SubmissionSessionId
                        var relatedReqIds = new List<int>();
                        const string sqlFindRelated = @"
                            SELECT r.ReqId, r.SetId
                            FROM dbo.Request r
                            INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                            WHERE r.SubmissionSessionId = @SubmissionSessionId
                              AND i.Category = 'Cartridge'
                            ORDER BY r.DateCreated ASC, r.ReqId ASC";

                        int? groupSetId = null;
                        using (var cmd = new SqlCommand(sqlFindRelated, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId.Value);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int relatedReqId = reader.GetInt32(0);
                                    int? relatedSetId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);

                                    relatedReqIds.Add(relatedReqId);

                                    // If any related request already has a Set, use it
                                    if (!groupSetId.HasValue && relatedSetId.HasValue && relatedSetId.Value > 0)
                                    {
                                        groupSetId = relatedSetId.Value;
                                    }
                                }
                            }
                        }

                        // If no existing Set found in the group, create one
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
                                groupSetId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // Update the Set with the first request's ID for reference
                            const string sqlUpdateSetReqId = @"
                                UPDATE s
                                SET s.ReqId = @FirstReqId,
                                    s.SetType = 'Cartridge'
                                FROM dbo.[Set] s
                                WHERE s.SetId = @SetId AND s.ReqId IS NULL";

                            using (var cmd = new SqlCommand(sqlUpdateSetReqId, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetId", groupSetId.Value);
                                cmd.Parameters.AddWithValue("@FirstReqId", relatedReqIds.FirstOrDefault());
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Link all related requests to the shared Set
                        foreach (var relReqId in relatedReqIds)
                        {
                            const string sqlLinkRequest = @"
                                UPDATE dbo.Request
                                SET SetId = @SetId
                                WHERE ReqId = @ReqId AND (SetId IS NULL OR SetId = 0)";

                            using (var cmd = new SqlCommand(sqlLinkRequest, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", relReqId);
                                cmd.Parameters.AddWithValue("@SetId", groupSetId.Value);
                                cmd.ExecuteNonQuery();
                            }
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
            }
        }

        /// <summary>
        /// Encode-on-Return: Auto-registers a returned empty cartridge in inventory.
        /// Creates both dbo.Item and dbo.Cartridge records with safe defaults.
        /// 
        /// IMPORTANT:
        /// Returned empty cartridges must always reference a valid ItemId.
        /// If the cartridge does not exist, it is auto-registered with
        /// safe default values (SerialNumber = 'N/A', Vendor = 'N/A').
        /// This prevents NULL violations while preserving audit integrity.
        /// </summary>
        /// <returns>The newly created ItemId</returns>
        private int AutoRegisterReturnedCartridge(
            SqlConnection con,
            SqlTransaction transaction,
            string modelName,
            string modelNumber,
            int modelCategoryId,
            int conditionId,
            int? vendorId,
            int cartridgeTypeId,
            int reqId,
            int userId)
        {
            // Step 1: Insert into dbo.Item with safe defaults
            // IMPORTANT:
            // dbo.Item has enabled triggers.
            // SQL Server does NOT allow OUTPUT INSERTED without INTO when triggers exist.
            // Always use OUTPUT INSERTED INTO a table variable and SELECT from it.
            const string sqlInsertItem = @"
                DECLARE @NewItemIds TABLE (ItemId INT);

                INSERT INTO dbo.Item
                    (Name, Description, Active, UnitOfMeasure, StockOnHand,
                     DateCreated, CreatedBy, DateModified, ModifiedBy,
                     Category, SerialNumber, CategoryId, ModelNumber,
                     Amount, ItemType, StartDate, EndDate,
                     ConditionID, VendorId, Remarks,
                     WarrantyYears, DurationYears, DurationStartDate, DurationEndDate,
                     DatePurchased, LicenseNumber,
                     WarrantyStartDate, AffectsInventory, AcquisitionType, IsTrackedAsset,
                     RefillStatus)
                OUTPUT INSERTED.ItemId INTO @NewItemIds
                VALUES
                    (@Name, @Description, 1, @UnitOfMeasure, 1,
                     GETDATE(), @CreatedBy, GETDATE(), @ModifiedBy,
                     'Cartridge', @SerialNumber, @CategoryId, @ModelNumber,
                     0, 'Hardware', GETDATE(), NULL,
                     @ConditionID, @VendorId, @Remarks,
                     0, 0, NULL, NULL,
                     NULL, NULL,
                     GETDATE(), 1, NULL, 0,
                     'For Refill');

                SELECT ItemId FROM @NewItemIds;";

            int newItemId;
            using (var cmd = new SqlCommand(sqlInsertItem, con, transaction))
            {
                cmd.Parameters.AddWithValue("@Name", EnsureCartridgeSuffix(modelName));
                cmd.Parameters.AddWithValue("@Description", "Returned Empty Cartridge");
                cmd.Parameters.AddWithValue("@UnitOfMeasure", "Unit");
                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                cmd.Parameters.AddWithValue("@CategoryId", modelCategoryId);
                cmd.Parameters.AddWithValue("@ModelNumber", string.IsNullOrWhiteSpace(modelNumber) ? "N/A" : modelNumber);
                cmd.Parameters.AddWithValue("@SerialNumber", DBNull.Value);
                cmd.Parameters.AddWithValue("@ConditionID", conditionId);
                cmd.Parameters.AddWithValue("@VendorId", (object)vendorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Remarks", "Returned Empty Cartridge");

                newItemId = (int)cmd.ExecuteScalar();
            }

            // Step 2: Insert into dbo.Cartridge
            const string sqlInsertCartridge = @"
                INSERT INTO dbo.Cartridge
                    (ItemId, CartridgeTypeId, IsActive)
                VALUES
                    (@ItemId, @CartridgeTypeId, 1)";

            using (var cmd = new SqlCommand(sqlInsertCartridge, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", newItemId);
                cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
                cmd.ExecuteNonQuery();
            }

            return newItemId;
        }

        private int GetConditionIdByNameOrDefault(SqlConnection con, SqlTransaction transaction, string conditionName, int defaultConditionId)
        {
            const string sql = @"
                SELECT TOP 1 ConditionId
                FROM dbo.[Condition]
                WHERE ConditionName = @ConditionName
                ORDER BY ConditionId";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ConditionName", conditionName);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            return defaultConditionId;
        }

        private int? GetVendorIdByNameOrNull(SqlConnection con, SqlTransaction transaction, string vendorName)
        {
            const string sql = @"
                SELECT TOP 1 VendorID
                FROM dbo.Vendor
                WHERE VendorName = @VendorName
                ORDER BY VendorID";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@VendorName", vendorName);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            return null;
        }

        private void RecordCartridgeMovement(
            SqlConnection con,
            SqlTransaction transaction,
            int itemId,
            MovementType movementType,
            int quantity,
            int empId,
            int? branchId,
            int? deptId,
            string conditionType,
            int reqId,
            int userId,
            string remarks)
        {
            // Get CartridgeTypeId for the item
            int cartridgeTypeId = GetCartridgeTypeIdForItem(con, transaction, itemId);

            string dbMovementType = ToDbCartridgeMovementType(movementType);

            const string sql = @"
                INSERT INTO dbo.CartridgeMovement
                    (ItemId, CartridgeTypeId, MovementType, Quantity,
                     EmployeeId, BranchId, DeptId, ConditionType,
                     Remarks, CreatedAt, CreatedBy)
                VALUES
                    (@ItemId, @CartridgeTypeId, @MovementType, @Quantity,
                     @EmployeeId, @BranchId, @DeptId, @ConditionType,
                     @Remarks, GETDATE(), @CreatedBy)";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
                cmd.Parameters.AddWithValue("@MovementType", dbMovementType);
                cmd.Parameters.AddWithValue("@Quantity", quantity);
                cmd.Parameters.AddWithValue("@EmployeeId", empId);
                cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ConditionType", conditionType);
                cmd.Parameters.AddWithValue("@Remarks", remarks);
                cmd.Parameters.AddWithValue("@CreatedBy", userId);

                cmd.ExecuteNonQuery();
            }
        }

        private static string ToDbCartridgeMovementType(MovementType movementType)
        {
            // DB constraint uses specific string values (see CartridgeRepository):
            // 'Issued', 'Returned', 'StockIn', 'RefillIn', 'Adjustment'
            switch (movementType)
            {
                case MovementType.Issue:
                    return "Issued";
                case MovementType.ReturnForRefill:
                    return "Returned";
                case MovementType.RefillComplete:
                    return "RefillIn";
                case MovementType.Dispose:
                    return "Adjustment";
                default:
                    return movementType.ToString();
            }
        }

        private int GetCartridgeTypeIdForItem(SqlConnection con, SqlTransaction transaction, int itemId)
        {
            // Try to get from Cartridge table first
            const string sqlCartridge = @"
                SELECT CartridgeTypeId FROM dbo.Cartridge
                WHERE ItemId = @ItemId AND IsActive = 1";

            using (var cmd = new SqlCommand(sqlCartridge, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return (int)result;
            }

            // Default to "Refill" type if not found
            const string sqlDefault = @"
                SELECT CartridgeTypeId FROM dbo.CartridgeType
                WHERE CartridgeTypeName = 'Refill'";

            using (var cmd = new SqlCommand(sqlDefault, con, transaction))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return (int)result;
            }

            // Last resort: return 1
            return 1;
        }

        private void UpdateItemRefillStatus(SqlConnection con, SqlTransaction transaction, int itemId, string status, int userId)
        {
            const string sql = @"
                UPDATE dbo.Item
                SET RefillStatus = @Status,
                    DateModified = GETDATE(),
                    ModifiedBy = @UserId
                WHERE ItemId = @ItemId";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Decreases stock for an item during fulfillment.
        ///
        /// CRITICAL RULES:
        /// - ONLY decreases StockOnHand
        /// - DOES NOT set Active = 0 (Active flag is for archival ONLY, NOT reservation)
        /// - DOES NOT set RefillStatus (legacy field, not used for availability)
        /// - Item remains Active=1 even when StockOnHand reaches 0
        ///
        /// Availability is determined by StockOnHand, not Active flag.
        /// </summary>
        private void DecreaseItemStock(SqlConnection con, SqlTransaction transaction, int itemId, int quantity, int userId)
        {
            // CRITICAL: Only decrease StockOnHand - DO NOT touch Active flag
            const string sql = @"
                UPDATE dbo.Item
                SET StockOnHand = CASE WHEN StockOnHand >= @Quantity THEN StockOnHand - @Quantity ELSE 0 END,
                    DateModified = GETDATE(),
                    ModifiedBy = @UserId
                WHERE ItemId = @ItemId";
            // NOTE: Active flag is NOT modified - it remains 1 even when stock is depleted

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@Quantity", quantity);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Fulfills a cartridge exchange request with separate Brand New and Refilled quantities.
        /// Persists IssuedBrandNewQty and IssuedRefilledQty to dbo.Set.
        /// Deducts stock separately by condition.
        /// </summary>
        public int FulfillCartridgeExchangeByCondition(
            int reqId,
            int returnedQuantity,
            List<int> issuedBrandNewIds,
            List<int> issuedRefilledIds,
            int issuedBrandNewQty,
            int issuedRefilledQty,
            int userId,
            int? requestModelId = null,
            string fulfillmentRemarks = null)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Get request details
                        int empId = 0;
                        int? branchId = null;
                        int? deptId = null;
                        int? existingSetId = null;

                        // dbo.UnfulfilledCartridgeExchange.EmpId has an FK to dbo.Employee and is NOT
                        // NULL, but a request's own EmpId can be NULL or orphaned (dept-level, or a
                        // simulated / test request). Resolve a guaranteed-valid EmpId: the requester
                        // → the pickup receiver → the user doing the fulfillment. LEFT JOIN so
                        // branch/dept and SetId are still read even when the requester isn't a live Employee.
                        const string sqlGetRequest = @"
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
                            WHERE r.ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlGetRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    empId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                    branchId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                                    deptId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                    existingSetId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                                }
                            }
                        }

                        // Ensure shared Set for multi-model portal requests
                        if (!existingSetId.HasValue || existingSetId.Value == 0)
                        {
                            int groupSetId = EnsureSharedSetForPortalCartridgeGroup(reqId, userId);
                            if (groupSetId > 0)
                            {
                                existingSetId = groupSetId;
                            }
                        }

                        // Create or use existing Set
                        int setId;
                        if (existingSetId.HasValue && existingSetId.Value > 0)
                        {
                            setId = existingSetId.Value;
                        }
                        else
                        {
                            const string sqlCreateSet = @"
                                DECLARE @NewSetIds TABLE (SetId INT);
                                INSERT INTO dbo.[Set] (CreatedBy, CreatedAt, Remarks, Status, IssuedBrandNewQty, IssuedRefilledQty)
                                OUTPUT INSERTED.SetId INTO @NewSetIds
                                VALUES (@CreatedBy, GETDATE(), @Remarks, @Status, @IssuedBrandNewQty, @IssuedRefilledQty);
                                SELECT SetId FROM @NewSetIds;";

                            using (var cmd = new SqlCommand(sqlCreateSet, con, transaction))
                            {
                                var setRemarks = string.IsNullOrWhiteSpace(fulfillmentRemarks)
                                    ? $"Cartridge Exchange - Request #{reqId}"
                                    : fulfillmentRemarks.Trim();

                                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                                cmd.Parameters.AddWithValue("@Remarks", setRemarks);
                                cmd.Parameters.AddWithValue("@Status", "Pending");
                                cmd.Parameters.AddWithValue("@IssuedBrandNewQty", issuedBrandNewQty);
                                cmd.Parameters.AddWithValue("@IssuedRefilledQty", issuedRefilledQty);
                                setId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            const string sqlLinkRequestToSet = @"
                                UPDATE dbo.Request
                                SET SetId = @SetId
                                WHERE ReqId = @ReqId";

                            using (var cmd = new SqlCommand(sqlLinkRequestToSet, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.ExecuteNonQuery();
                            }

                            const string sqlUpdateSetReqIdAndType = @"
                                UPDATE s
                                SET s.ReqId = @ReqId,
                                    s.SetType = 'Cartridge'
                                FROM dbo.[Set] s
                                INNER JOIN dbo.Request r ON r.ReqId = @ReqId
                                INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                                WHERE s.SetId = @SetId AND s.ReqId IS NULL";

                            using (var cmd = new SqlCommand(sqlUpdateSetReqIdAndType, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Update existing Set with issued quantities
                        if (existingSetId.HasValue && existingSetId.Value > 0)
                        {
                            const string sqlUpdateSetQty = @"
                                UPDATE dbo.[Set]
                                SET IssuedBrandNewQty = ISNULL(IssuedBrandNewQty, 0) + @IssuedBrandNewQty,
                                    IssuedRefilledQty = ISNULL(IssuedRefilledQty, 0) + @IssuedRefilledQty
                                WHERE SetId = @SetId";

                            using (var cmd = new SqlCommand(sqlUpdateSetQty, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@IssuedBrandNewQty", issuedBrandNewQty);
                                cmd.Parameters.AddWithValue("@IssuedRefilledQty", issuedRefilledQty);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Combine all issued IDs
                        var allIssuedIds = new List<int>();
                        allIssuedIds.AddRange(issuedBrandNewIds);
                        allIssuedIds.AddRange(issuedRefilledIds);

                        // Process Brand New cartridges
                        foreach (var itemId in issuedBrandNewIds)
                        {
                            DecreaseItemStock(con, transaction, itemId, 1, userId);
                            RecordCartridgeMovement(
                                con, transaction, itemId, MovementType.Issue, 1,
                                empId, branchId, deptId, "Brand New",
                                reqId, userId, $"Issued Brand New - Request #{reqId}");
                        }

                        // Process Refilled cartridges
                        foreach (var itemId in issuedRefilledIds)
                        {
                            DecreaseItemStock(con, transaction, itemId, 1, userId);
                            RecordCartridgeMovement(
                                con, transaction, itemId, MovementType.Issue, 1,
                                empId, branchId, deptId, "Refilled",
                                reqId, userId, $"Issued Refilled - Request #{reqId}");
                        }

                        // Handle returned empty cartridges (same logic as original method)
                        List<int> returnedItemIds = new List<int>();
                        if (allIssuedIds.Count > 0)
                        {
                            if (returnedQuantity > 0 && allIssuedIds.Count >= returnedQuantity)
                            {
                                returnedItemIds = allIssuedIds.Take(returnedQuantity).ToList();
                            }
                            else if (allIssuedIds.Count > 0)
                            {
                                returnedItemIds.Add(allIssuedIds[0]);
                            }
                        }

                        // Process empty cartridge returns (if applicable)
                        // CRITICAL: Use totalIssuedQty (IssuedBrandNewQty + IssuedRefilledQty) for returned empties
                        int totalIssuedQty = issuedBrandNewQty + issuedRefilledQty;
                        if (totalIssuedQty > 0 && returnedItemIds.Count > 0)
                        {
                            int cartridgeModelId = 0;
                            int vendorId = 0;
                            int conditionId = 0;    // EMPTY condition (legacy fallback)
                            int goodConditionId = 0;
                            int damagedConditionId = 0;
                            int goodEmptyQty = 0;
                            int damagedEmptyQty = 0;
                            bool isRefillableModel = false;

                            const string sqlGetConditions2 = @"
                                SELECT
                                    MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                                    MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                                    MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                                FROM dbo.Condition
                                WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')";

                            using (var cmd = new SqlCommand(sqlGetConditions2, con, transaction))
                            {
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        goodConditionId    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                        damagedConditionId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                        conditionId        = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                    }
                                }
                            }

                            if (requestModelId.HasValue && requestModelId.Value > 0)
                            {
                                const string sqlGetModelFromRequestModel = @"
                                    IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                                    BEGIN
                                        SELECT TOP 1
                                            cm.CartridgeModelId,
                                            vcm.VendorId,
                                            ISNULL(crm.GoodEmptyQty,    0),
                                            ISNULL(crm.DamagedEmptyQty, 0)
                                        FROM dbo.CartridgeRequestModel crm
                                        INNER JOIN dbo.CartridgeModel cm ON LTRIM(RTRIM(crm.CartridgeModel)) = cm.ModelNumber
                                        LEFT JOIN dbo.VendorCartridgeModel vcm
                                            ON vcm.CartridgeModelId = cm.CartridgeModelId AND vcm.IsActive = 1
                                        WHERE crm.RequestModelId = @RequestModelId;
                                    END";

                                using (var cmd = new SqlCommand(sqlGetModelFromRequestModel, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            cartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                            vendorId         = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                            goodEmptyQty     = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                            damagedEmptyQty  = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                                            isRefillableModel = cartridgeModelId > 0;
                                        }
                                    }
                                }
                            }

                            if (cartridgeModelId <= 0)
                            {
                                const string sqlGetItemInfo = @"
                                    SELECT i.CartridgeModelId, COALESCE(vcm.VendorId, i.VendorId) AS VendorId,
                                           CASE WHEN cm.CartridgeModelId IS NOT NULL THEN 1 ELSE 0 END AS IsRefillable
                                    FROM dbo.Item i
                                    LEFT JOIN dbo.CartridgeModel cm ON i.CartridgeModelId = cm.CartridgeModelId
                                    LEFT JOIN dbo.VendorCartridgeModel vcm
                                        ON vcm.CartridgeModelId = cm.CartridgeModelId AND vcm.IsActive = 1
                                    WHERE i.ItemId = @ItemId";

                                using (var cmd = new SqlCommand(sqlGetItemInfo, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId", returnedItemIds[0]);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            cartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                            vendorId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                            isRefillableModel = reader.GetInt32(2) == 1;
                                        }
                                    }
                                }
                            }

                            // One EmptyCartridge row per returned unit, split by the declared Good/Damaged
                            // counts when they add up to the issued quantity (legacy single EMPTY batch otherwise).
                            // RecordReturnedEmpties is shared with mixed-submission cartridge lines.
                            if (cartridgeModelId > 0)
                            {
                                bool hasSplit2 = totalIssuedQty > 0
                                    && (goodEmptyQty + damagedEmptyQty) == totalIssuedQty;

                                var emptyBatches2 = hasSplit2
                                    ? new (int qty, int condId, string condStatus)[]
                                      {
                                          (goodEmptyQty,    goodConditionId    > 0 ? goodConditionId    : conditionId, "GOOD"),
                                          (damagedEmptyQty, damagedConditionId > 0 ? damagedConditionId : conditionId, "DAMAGED")
                                      }
                                    : new (int qty, int condId, string condStatus)[]
                                      {
                                          (totalIssuedQty, conditionId, "GOOD")
                                      };

                                RecordReturnedEmpties(
                                    con, transaction, reqId, cartridgeModelId, emptyBatches2,
                                    returnedItemIds[0], empId, branchId, deptId, userId, conditionId);
                            }
                        }

                        // Update request status for this row, then recompute all sibling rows
                        // in the same submission session so the group shows the correct aggregate.
                        const string sqlUpdateRequestStatus = @"
                            -- Update this request's individual status
                            UPDATE dbo.Request
                            SET Status = CASE
                                WHEN @TotalIssuedQty >= Quantity THEN 'Fulfilled'
                                WHEN @TotalIssuedQty > 0 THEN 'Partially Fulfilled'
                                ELSE 'Unfulfilled'
                            END
                            WHERE ReqId = @ReqId;

                            -- If this request belongs to a multi-model session, recompute
                            -- all sibling statuses so the group reflects the true aggregate.
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

                                -- Mixed session: mark all non-Fulfilled rows as Partially Fulfilled
                                IF @SessionFulfilled > 0 AND @SessionFulfilled < @SessionTotal
                                BEGIN
                                    UPDATE dbo.Request
                                    SET Status = 'Partially Fulfilled'
                                    WHERE SubmissionSessionId = @SessionId
                                      AND Status IN ('Unfulfilled', 'Under Review', 'Processing');
                                END
                            END";

                        using (var cmd = new SqlCommand(sqlUpdateRequestStatus, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@TotalIssuedQty", totalIssuedQty);
                            cmd.ExecuteNonQuery();
                        }

                        // CRITICAL: Update Set status to Dispatched/Partial/Pending before SMTP
                        // Cartridge Sets auto-dispatch (do NOT require QR generation)
                        // - All requests Fulfilled → Set = "Dispatched" (auto-dispatch, triggers email)
                        // - Some Fulfilled, some Pending → Set = "Partial"
                        // - All Pending → Set = "Pending"
                        const string sqlUpdateSetStatus = @"
                            DECLARE @TotalRequests INT = 0;
                            DECLARE @FulfilledRequests INT = 0;

                            SELECT @TotalRequests = COUNT(*)
                            FROM dbo.Request
                            WHERE SetId = @SetId;

                            SELECT @FulfilledRequests = COUNT(*)
                            FROM dbo.Request
                            WHERE SetId = @SetId
                              AND Status IN ('Fulfilled', 'Completed');

                            UPDATE dbo.[Set]
                            SET Status = CASE
                                WHEN @FulfilledRequests = @TotalRequests THEN 'Dispatched'
                                WHEN @FulfilledRequests > 0 THEN 'Partial'
                                ELSE 'Pending'
                            END
                            WHERE SetId = @SetId;";

                        using (var cmd = new SqlCommand(sqlUpdateSetStatus, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            cmd.ExecuteNonQuery();
                        }

                        // CRITICAL: Materialize dbo.SetItem from dbo.Request before finalizing
                        // This ensures dbo.SetItem is populated for SMTP emails
                        //
                        // ItemCode priority for cartridge items:
                        //   1. [MODEL:XXX] tag extracted from Request.Description (set by portal at submit time)
                        //   2. CartridgeModel.ModelNumber via Item.CartridgeModelId FK (authoritative catalog)
                        //   3. Item.ModelNumber (last-resort generic fallback)
                        //
                        // GUARD: RAISERROR + rollback if any cartridge item resolves to NULL ItemCode.
                        // This prevents silent placeholder persistence (the root cause of the UNKNOWN bug).
                        // NOTE: Keep this SQL in sync with the identical block in FulfillCartridgeExchange.
                        const string sqlMaterializeSetItem = @"
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
                                    -- ItemCode: [MODEL:XXX] tag → CartridgeModel.ModelNumber → Item.ModelNumber
                                    -- NULLIF(...,'') ensures an empty extraction is treated the same as NULL.
                                    -- The CHARINDEX(']',...) > 0 guard prevents negative-length SUBSTRING.
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
                                    -- Description: always 'Cartridge Exchange - <model>' for cartridge items.
                                    -- Uses same COALESCE chain as ItemCode for consistency.
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

                                -- Guard: if any cartridge SetItem still has NULL ItemCode, the item has no
                                -- model information at all. Reject now so the transaction rolls back cleanly
                                -- rather than persisting a placeholder that breaks downstream features.
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
                            END";

                        using (var cmd = new SqlCommand(sqlMaterializeSetItem, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            cmd.ExecuteNonQuery();
                        }

                        // If not fully fulfilled, insert into UnfulfilledCartridgeExchange so the
                        // NOT EXISTS gate in GetPendingCartridgeRequests removes this request from
                        // the pending queue and routes it to the Unfulfilled Cartridges page.
                        int unfulfilledQty = returnedQuantity - totalIssuedQty;
                        if (unfulfilledQty > 0)
                        {
                            if (empId <= 0)
                                throw new InvalidOperationException(
                                    $"Cannot record the unfulfilled portion of request #{reqId}: no employee " +
                                    "could be resolved (the request has no linked employee or receiver, and the " +
                                    "fulfilling user has no employee record).");

                            string unfulfCartridgeModel = string.Empty;
                            int unfulfGoodEmptyQty    = 0;
                            int unfulfDamagedEmptyQty = 0;

                            if (requestModelId.HasValue && requestModelId.Value > 0)
                            {
                                const string sqlGetCrmInfo = @"
                                    SELECT crm.CartridgeModel,
                                           ISNULL(crm.GoodEmptyQty, 0),
                                           ISNULL(crm.DamagedEmptyQty, 0)
                                    FROM dbo.CartridgeRequestModel crm
                                    WHERE crm.RequestModelId = @RequestModelId";

                                using (var cmd = new SqlCommand(sqlGetCrmInfo, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RequestModelId", requestModelId.Value);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            unfulfCartridgeModel  = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                                            unfulfGoodEmptyQty    = reader.GetInt32(1);
                                            unfulfDamagedEmptyQty = reader.GetInt32(2);
                                        }
                                    }
                                }
                            }

                            const string sqlInsertUnfulfilled = @"
                                INSERT INTO dbo.UnfulfilledCartridgeExchange
                                    (ReqId, EmpId, BranchId, DeptId, CartridgeModel,
                                     RequestedQty, ReturnedEmptyQty, IssuedFullQty, UnfulfilledQty,
                                     GoodEmptyQty, DamagedEmptyQty,
                                     Remarks, Status, CreatedDate, CreatedBy)
                                VALUES
                                    (@ReqId, @EmpId, @BranchId, @DeptId, @CartridgeModel,
                                     @RequestedQty, @ReturnedEmptyQty, @IssuedFullQty, @UnfulfilledQty,
                                     @GoodEmptyQty, @DamagedEmptyQty,
                                     @Remarks, 'Pending', GETDATE(), @CreatedBy)";

                            using (var cmd = new SqlCommand(sqlInsertUnfulfilled, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId",            reqId);
                                cmd.Parameters.AddWithValue("@EmpId",            empId);
                                cmd.Parameters.AddWithValue("@BranchId",         (object)branchId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@DeptId",           (object)deptId   ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@CartridgeModel",   string.IsNullOrWhiteSpace(unfulfCartridgeModel) ? (object)DBNull.Value : (object)unfulfCartridgeModel);
                                cmd.Parameters.AddWithValue("@RequestedQty",     returnedQuantity);
                                cmd.Parameters.AddWithValue("@ReturnedEmptyQty", returnedQuantity);
                                cmd.Parameters.AddWithValue("@IssuedFullQty",    totalIssuedQty);
                                cmd.Parameters.AddWithValue("@UnfulfilledQty",   unfulfilledQty);
                                cmd.Parameters.AddWithValue("@GoodEmptyQty",     unfulfGoodEmptyQty);
                                cmd.Parameters.AddWithValue("@DamagedEmptyQty",  unfulfDamagedEmptyQty);
                                cmd.Parameters.AddWithValue("@Remarks",          string.IsNullOrWhiteSpace(fulfillmentRemarks) ? $"Unfulfilled - Request #{reqId}" : fulfillmentRemarks);
                                cmd.Parameters.AddWithValue("@CreatedBy",        userId);
                                cmd.ExecuteNonQuery();
                            }
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
            }
        }

        /// <summary>
        /// Records returned empty cartridges for a fulfilled request: one dbo.EmptyCartridge row
        /// per unit (Good units of refillable models are queued 'For Refill'), an IT custody Item
        /// for non-refillable models, and a ReturnForRefill movement anchored on
        /// <paramref name="anchorItemId"/>. Shared by the Cartridge Exchange
        /// (FulfillCartridgeExchangeByCondition) and cartridge lines of mixed submissions
        /// (IssueMixedCartridgeLine) so both return empties exactly the same way.
        /// </summary>
        private void RecordReturnedEmpties(
            SqlConnection con,
            SqlTransaction transaction,
            int reqId,
            int cartridgeModelId,
            IEnumerable<(int qty, int condId, string condStatus)> batches,
            int anchorItemId,
            int empId,
            int? branchId,
            int? deptId,
            int userId,
            int emptyConditionId)
        {
            var batchList = batches.Where(b => b.qty > 0).ToList();
            int totalQty = batchList.Sum(b => b.qty);
            if (cartridgeModelId <= 0 || totalQty <= 0)
                return;

            // Authoritative CartridgeModel.IsRefillable flag, plus ModelNumber for the IT
            // inventory row (non-refillable path).
            bool isRefillableModel = false;
            string modelNumber = string.Empty;
            using (var cmd = new SqlCommand("SELECT IsRefillable, ModelNumber FROM dbo.CartridgeModel WHERE CartridgeModelId = @ModelId", con, transaction))
            {
                cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        isRefillableModel = !reader.IsDBNull(0) && reader.GetBoolean(0);
                        modelNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    }
                }
            }

            // Non-refillable models: create the IT custody Item FIRST so its ItemId
            // can be written into SourceItemId on each EmptyCartridge row.  The
            // dispose/sell workflow later uses SourceItemId to find the inventory row
            // to decrement and to anchor the Inventory / CartridgeMovement records.
            int? itCustodyItemId = null;
            if (!isRefillableModel)
            {
                const string sqlInsertITItem = @"
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
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(sqlInsertITItem, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@Name",             $"{modelNumber} - Returned Empty");
                    cmd.Parameters.AddWithValue("@Description",      $"Returned empty cartridge (non-refillable) - IT custody - Request #{reqId}");
                    cmd.Parameters.AddWithValue("@Quantity",          totalQty);
                    cmd.Parameters.AddWithValue("@CartridgeModelId",  cartridgeModelId);
                    cmd.Parameters.AddWithValue("@ModelNumber",       modelNumber);
                    cmd.Parameters.AddWithValue("@ConditionID",       emptyConditionId > 0 ? (object)emptyConditionId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks",           $"Non-refillable - IT custody - Request #{reqId}");
                    cmd.Parameters.AddWithValue("@CreatedBy",         userId);
                    var idResult = cmd.ExecuteScalar();
                    itCustodyItemId = idResult != null && idResult != DBNull.Value
                        ? Convert.ToInt32(idResult) : (int?)null;
                }
            }

            // INSERT one EmptyCartridge row per physical unit returned.
            // VendorId is NULL — vendor is unknown at the empty stage and is
            // assigned only when a refill batch is created by Purchasing.
            // VendorBatchId is NULL — batch is assigned manually after creation.
            // RefillStatus is 'For Refill' only for refillable models; NULL otherwise.
            // SourceItemId links each non-refillable unit to its IT custody Item row.
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
                    using (var cmd = new SqlCommand(sqlInsertEmpty, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                        cmd.Parameters.AddWithValue("@ConditionId", batchCondId == 0 ? (object)DBNull.Value : batchCondId);
                        cmd.Parameters.AddWithValue("@ConditionStatus", (object)batchCondStatus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@RefillStatus", isRefillableModel && batchCondStatus != "DAMAGED" ? (object)"For Refill" : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ReturnedBy", empId > 0 ? (object)empId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ReqId", reqId);
                        cmd.Parameters.AddWithValue("@EmpId", empId > 0 ? (object)empId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Remarks", $"Empty returned - Request #{reqId}");
                        cmd.Parameters.AddWithValue("@CreatedBy", userId);
                        cmd.Parameters.AddWithValue("@SourceItemId", itCustodyItemId.HasValue ? (object)itCustodyItemId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Record movement for audit trail
            RecordCartridgeMovement(
                con, transaction, anchorItemId, MovementType.ReturnForRefill,
                totalQty, empId, branchId, deptId, "EMPTY",
                reqId, userId, $"Returned {totalQty} Empty Cartridge(s) - Request #{reqId}");
        }

        // ── Cartridge lines inside MIXED portal submissions ──────────────────────────
        //
        // A mixed submission (cartridges plus Ink / Toner / Printhead) belongs to Request & Set
        // Management and is tracked by dbo.Request.IssuedQty, not by the Cartridge Management
        // queue. Its cartridge lines are still cartridge EXCHANGES though: IT issues Brand New /
        // Refilled units picked FIFO by condition, every unit gets a CartridgeMovement, and the
        // requester's empties come back as dbo.EmptyCartridge rows, the same as the Cartridge
        // Exchange. The differences are process only: the issued quantity goes to
        // Request.IssuedQty, any shortfall stays pending in Request & Set Management (no
        // UnfulfilledCartridgeExchange row), and the Set is the submission's ordinary Set.

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
            -- The model the requester picked, from the [MODEL:xxx] tag the portal writes. The
            -- resolved Item can be a fallback row when that model has no Item of its own.
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

            -- Empties already returned by earlier partial fulfillments of this line.
            SELECT
                ISNULL(SUM(CASE WHEN ec.ConditionStatus = 'DAMAGED' THEN ec.Quantity ELSE 0 END), 0),
                ISNULL(SUM(CASE WHEN ec.ConditionStatus = 'DAMAGED' THEN 0 ELSE ec.Quantity END), 0)
            FROM dbo.EmptyCartridge ec
            WHERE ec.ReqId = @ReqId;";

        /// <summary>
        /// Reads what is needed to fulfill one cartridge line of a mixed portal submission:
        /// its requested model, pending quantity, declared and already-returned empties, and the
        /// issuable Brand New / Refilled stock of that model. Returns null when the request does
        /// not exist; check <see cref="MixedCartridgeLineInfo.IsExchangeLine"/> before using it.
        /// </summary>
        public MixedCartridgeLineInfo GetMixedCartridgeLineInfo(int reqId)
        {
            MixedCartridgeLineInfo info;
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                info = ReadMixedCartridgeLine(con, null, reqId);
            }

            if (info != null && info.IsExchangeLine && info.CartridgeModelId.HasValue)
            {
                info.AvailableBrandNew = Math.Max(0, GetAvailableIssuableStockByCondition(info.CartridgeModelId, "Brand New"));
                info.AvailableRefilled = Math.Max(0, GetAvailableIssuableStockByCondition(info.CartridgeModelId, "Refilled"));
            }
            return info;
        }

        private static MixedCartridgeLineInfo ReadMixedCartridgeLine(SqlConnection con, SqlTransaction transaction, int reqId)
        {
            using (var cmd = new SqlCommand(SqlResolveMixedCartridgeLine, con, transaction))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
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

                    if (reader.NextResult() && reader.Read())
                    {
                        info.DeclaredGood    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        info.DeclaredDamaged = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }
                    if (reader.NextResult() && reader.Read())
                    {
                        info.ReturnedDamaged = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        info.ReturnedGood    = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }
                    return info;
                }
            }
        }

        /// <summary>
        /// Fulfills a cartridge line of a mixed portal submission the way the Cartridge Exchange
        /// does: picks Brand New / Refilled units FIFO, deducts their stock, records an Issue
        /// movement per unit, and records one returned empty per issued unit (split by the
        /// requester's declared Good / Damaged counts). Adds the issued quantity to
        /// dbo.Request.IssuedQty. All in one transaction; throws if stock or the pending quantity
        /// changed since the screen was loaded.
        /// </summary>
        public void IssueMixedCartridgeLine(int reqId, int issuedBrandNewQty, int issuedRefilledQty, int userId, string remarks)
        {
            if (issuedBrandNewQty < 0 || issuedRefilledQty < 0)
                throw new ArgumentException("Issued quantities cannot be negative.");
            int totalIssued = issuedBrandNewQty + issuedRefilledQty;
            if (totalIssued <= 0)
                return;

            var preview = GetMixedCartridgeLineInfo(reqId)
                ?? throw new InvalidOperationException($"Request #{reqId} was not found.");
            if (!preview.IsExchangeLine)
                throw new InvalidOperationException($"Request #{reqId} is not a cartridge line of a mixed portal submission.");
            if (!preview.CartridgeModelId.HasValue)
                throw new InvalidOperationException(
                    $"Request #{reqId}: the cartridge model '{preview.ModelNumber}' is not registered in Cartridge Models, so no stock can be issued for it.");

            // Same FIFO unit selection the Cartridge Exchange uses.
            var bnIds = GetIssuableItemIdsByCondition(preview.CartridgeModelId, "Brand New", issuedBrandNewQty);
            var rfIds = GetIssuableItemIdsByCondition(preview.CartridgeModelId, "Refilled", issuedRefilledQty);
            if (bnIds.Count < issuedBrandNewQty)
                throw new InvalidOperationException($"Only {bnIds.Count} Brand New {preview.ModelNumber} in stock (tried to issue {issuedBrandNewQty}).");
            if (rfIds.Count < issuedRefilledQty)
                throw new InvalidOperationException($"Only {rfIds.Count} Refilled {preview.ModelNumber} in stock (tried to issue {issuedRefilledQty}).");

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Lock the row and re-check the pending quantity inside the transaction
                        // so two IT users cannot over-issue the same line.
                        using (var cmd = new SqlCommand(
                            "SELECT Quantity, ISNULL(IssuedQty, 0) FROM dbo.Request WITH (UPDLOCK, ROWLOCK) WHERE ReqId = @ReqId",
                            con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException($"Request #{reqId} was not found.");
                                int pending = reader.GetInt32(0) - reader.GetInt32(1);
                                if (totalIssued > pending)
                                    throw new InvalidOperationException(
                                        $"Request #{reqId} only has {Math.Max(0, pending)} unit(s) pending (tried to issue {totalIssued}). Refresh and try again.");
                            }
                        }

                        var line = ReadMixedCartridgeLine(con, transaction, reqId);

                        // Requester / branch / department, resolved the same way as the Cartridge Exchange.
                        int empId = 0;
                        int? branchId = null;
                        int? deptId = null;
                        const string sqlGetRequest = @"
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
                            WHERE r.ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlGetRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    empId    = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                    branchId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                                    deptId   = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                }
                            }
                        }

                        foreach (var itemId in bnIds)
                        {
                            DecreaseItemStock(con, transaction, itemId, 1, userId);
                            RecordCartridgeMovement(
                                con, transaction, itemId, MovementType.Issue, 1,
                                empId, branchId, deptId, "Brand New",
                                reqId, userId, $"Issued Brand New - Request #{reqId}");
                        }

                        foreach (var itemId in rfIds)
                        {
                            DecreaseItemStock(con, transaction, itemId, 1, userId);
                            RecordCartridgeMovement(
                                con, transaction, itemId, MovementType.Issue, 1,
                                empId, branchId, deptId, "Refilled",
                                reqId, userId, $"Issued Refilled - Request #{reqId}");
                        }

                        // One empty comes back per full cartridge issued. Use up the declared Good
                        // empties first, then the Damaged ones; anything beyond what was declared is
                        // recorded like an undeclared Cartridge Exchange (EMPTY condition, GOOD).
                        var (goodConditionId, damagedConditionId, emptyConditionId) = GetEmptyConditionIds(con, transaction);
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
                        RecordReturnedEmpties(
                            con, transaction, reqId, preview.CartridgeModelId.Value, batches,
                            anchorItemId, empId, branchId, deptId, userId, emptyConditionId);

                        const string sqlIssue = @"
                            UPDATE dbo.Request
                            SET IssuedQty     = ISNULL(IssuedQty, 0) + @Issued,
                                Remarks       = ISNULL(@Remarks, Remarks),
                                DateModified  = (SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time'),
                                ModifiedBy    = @ModifiedBy
                            WHERE ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlIssue, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@Issued", totalIssued);
                            cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                            cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim());
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

        private static (int good, int damaged, int empty) GetEmptyConditionIds(SqlConnection con, SqlTransaction transaction)
        {
            const string sql = @"
                SELECT
                    MAX(CASE WHEN ConditionName = 'Good'    THEN ConditionId END),
                    MAX(CASE WHEN ConditionName = 'Damaged' THEN ConditionId END),
                    MAX(CASE WHEN ConditionName = 'EMPTY'   THEN ConditionId END)
                FROM dbo.Condition
                WHERE ConditionName IN ('Good', 'Damaged', 'EMPTY')";

            using (var cmd = new SqlCommand(sql, con, transaction))
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read())
                    return (0, 0, 0);
                return (
                    reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2));
            }
        }

        // ── Receiver helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the ReceivedById stored on the first request belonging to the given
        /// submission session (all requests in a session share the same value).
        /// Returns null for DELIVERY requests or legacy records.
        /// </summary>
        public int? GetReceivedByIdForSession(Guid submissionSessionId)
        {
            const string sql = @"
                SELECT TOP 1 ReceivedById
                FROM dbo.Request
                WHERE SubmissionSessionId = @SessionId
                  AND ReceivedById IS NOT NULL";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SessionId", submissionSessionId);
                    var result = cmd.ExecuteScalar();
                    return result != null && result != DBNull.Value ? (int?)Convert.ToInt32(result) : null;
                }
            }
        }

        /// <summary>
        /// Returns the ReceivedById stored on the given request.
        /// Returns null if not set.
        /// </summary>
        public int? GetReceivedByIdForRequest(int reqId)
        {
            const string sql = @"
                SELECT ReceivedById
                FROM dbo.Request
                WHERE ReqId = @ReqId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    var result = cmd.ExecuteScalar();
                    return result != null && result != DBNull.Value ? (int?)Convert.ToInt32(result) : null;
                }
            }
        }

        /// <summary>
        /// Updates ReceivedById on all requests that share the given submission session.
        /// Called when IT confirms or changes the receiver in the Confirm Receiver dialog.
        /// </summary>
        public void UpdateReceivedByForSession(Guid submissionSessionId, int? receivedById, int modifiedByUserId)
        {
            const string sql = @"
                UPDATE dbo.Request
                SET ReceivedById = @ReceivedById,
                    DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
                    ModifiedBy    = @ModifiedBy
                WHERE SubmissionSessionId = @SessionId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReceivedById", (object)receivedById ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                    cmd.Parameters.AddWithValue("@SessionId", submissionSessionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Updates ReceivedById on the single request (for non-session / single-model exchanges).
        /// </summary>
        public void UpdateReceivedByForRequest(int reqId, int? receivedById, int modifiedByUserId)
        {
            const string sql = @"
                UPDATE dbo.Request
                SET ReceivedById = @ReceivedById,
                    DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
                    ModifiedBy    = @ModifiedBy
                WHERE ReqId = @ReqId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReceivedById", (object)receivedById ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Returns fulfilled/partially-fulfilled portal Sets for the Send Notifications page.
        /// Shows sets from the last 90 days that originated from the Request Portal.
        /// </summary>
        public List<FulfilledSetNotificationDto> GetFulfilledSetsForNotification()
        {
            var list = new List<FulfilledSetNotificationDto>();

            const string sql = @"
                SELECT TOP 300
                    s.SetId,
                    ISNULL(s.SetCode, '') AS SetCode,
                    CASE
                        WHEN ISNULL(s.Status, '') = 'Dispatched' THEN 'Dispatched'
                        WHEN ISNULL(s.Status, '') = 'Partial'    THEN 'Partial'
                        WHEN (ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0)) > 0 THEN 'Partial'
                        ELSE 'Pending'
                    END AS SetStatus,
                    s.CreatedAt,
                    TOP_REQ.ReqId,
                    TOP_REQ.EmpId,
                    TOP_REQ.SubmissionSessionId,
                    TOP_REQ.ReceivedById,
                    ISNULL(recv_emp.Name, '') AS ReceivedByName,
                    ISNULL(req_emp.Name, '') AS RequesterName,
                    ISNULL(co.Name, '') AS CompanyName,
                    ISNULL(b.Name, '') AS BranchName,
                    ISNULL(d.Name, '') AS DepartmentName,
                    CASE
                        WHEN TOP_REQ.Description LIKE '%PICKUP%' THEN 'PICKUP'
                        WHEN TOP_REQ.Description LIKE '%DELIVERY%' THEN 'DELIVERY'
                        ELSE 'N/A'
                    END AS DistributionMethod,
                    ISNULL(s.IssuedBrandNewQty, 0) AS IssuedBrandNewQty,
                    ISNULL(s.IssuedRefilledQty, 0) AS IssuedRefilledQty
                FROM dbo.[Set] s
                CROSS APPLY (
                    SELECT TOP 1 r.ReqId, r.EmpId, r.SubmissionSessionId, r.ReceivedById, r.Description,
                                 r.ComId, r.BranchId, r.DeptId
                    FROM dbo.Request r
                    WHERE r.SetId = s.SetId
                    ORDER BY r.ReqId
                ) AS TOP_REQ
                LEFT JOIN dbo.Employee req_emp ON req_emp.EmpId = TOP_REQ.EmpId
                LEFT JOIN dbo.Company   co      ON co.ComId     = COALESCE(req_emp.ComId,    TOP_REQ.ComId)
                LEFT JOIN dbo.Branch    b       ON b.BranchId   = COALESCE(req_emp.BranchId, TOP_REQ.BranchId)
                LEFT JOIN dbo.Department d      ON d.DeptId     = COALESCE(req_emp.DeptId,   TOP_REQ.DeptId)
                LEFT JOIN dbo.Employee recv_emp ON recv_emp.EmpId = TOP_REQ.ReceivedById
                WHERE TOP_REQ.Description LIKE '%[[]PORTAL]%'
                  AND s.CreatedAt >= DATEADD(day, -90, GETDATE())
                ORDER BY s.CreatedAt DESC";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new FulfilledSetNotificationDto
                        {
                            SetId              = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode            = reader.GetString(reader.GetOrdinal("SetCode")),
                            SetStatus          = reader.GetString(reader.GetOrdinal("SetStatus")),
                            CreatedAt          = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            ReqId              = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            EmpId              = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? 0 : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            SubmissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId")),
                            ReceivedById       = reader.IsDBNull(reader.GetOrdinal("ReceivedById")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ReceivedById")),
                            ReceivedByName     = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                            RequesterName      = reader.GetString(reader.GetOrdinal("RequesterName")),
                            CompanyName        = reader.GetString(reader.GetOrdinal("CompanyName")),
                            BranchName         = reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName     = reader.GetString(reader.GetOrdinal("DepartmentName")),
                            DistributionMethod  = reader.GetString(reader.GetOrdinal("DistributionMethod")),
                            IssuedBrandNewQty   = reader.GetInt32(reader.GetOrdinal("IssuedBrandNewQty")),
                            IssuedRefilledQty   = reader.GetInt32(reader.GetOrdinal("IssuedRefilledQty"))
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns fulfilled portal cartridge sets for the history / reprint page.
        /// Looks back 365 days and returns up to 500 rows, newest first.
        /// </summary>
        public List<FulfilledCartridgeRowDto> GetFulfilledCartridgeHistory()
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
                    ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0)
                        AS TotalIssuedQty
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

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new FulfilledCartridgeRowDto
                        {
                            SetId          = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode        = reader.GetString(reader.GetOrdinal("SetCode")),
                            FulfilledAt    = reader.IsDBNull(reader.GetOrdinal("FulfilledAt"))
                                             ? DateTime.MinValue
                                             : reader.GetDateTime(reader.GetOrdinal("FulfilledAt")),
                            ReqId          = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            RequesterName  = reader.GetString(reader.GetOrdinal("RequesterName")),
                            CompanyName    = reader.GetString(reader.GetOrdinal("CompanyName")),
                            BranchName     = reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                            CartridgeModel = reader.GetString(reader.GetOrdinal("CartridgeModel")),
                            Quantity       = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            ReceivedByName = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                            TotalIssuedQty = reader.GetInt32(reader.GetOrdinal("TotalIssuedQty")),
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns the full detail (header + every requested cartridge model line) for a
        /// fulfilled Set, used by the "Open" preview action on the Fulfilled Cartridges page
        /// so multi-model requests can be reviewed in full before/after reprinting.
        /// </summary>
        public FulfilledCartridgeDetailDto GetFulfilledCartridgeDetail(int setId)
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
                    ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0)
                        AS TotalIssuedQty
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

            FulfilledCartridgeDetailDto detail = null;

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                using (var cmd = new SqlCommand(headerSql, con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            detail = new FulfilledCartridgeDetailDto
                            {
                                SetId          = reader.GetInt32(reader.GetOrdinal("SetId")),
                                SetCode        = reader.GetString(reader.GetOrdinal("SetCode")),
                                FulfilledAt    = reader.IsDBNull(reader.GetOrdinal("FulfilledAt"))
                                                 ? DateTime.MinValue
                                                 : reader.GetDateTime(reader.GetOrdinal("FulfilledAt")),
                                ReqId          = reader.GetInt32(reader.GetOrdinal("ReqId")),
                                RequesterName  = reader.GetString(reader.GetOrdinal("RequesterName")),
                                CompanyName    = reader.GetString(reader.GetOrdinal("CompanyName")),
                                BranchName     = reader.GetString(reader.GetOrdinal("BranchName")),
                                DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                                ReceivedByName = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                                TotalIssuedQty = reader.GetInt32(reader.GetOrdinal("TotalIssuedQty")),
                            };
                        }
                    }
                }

                if (detail == null) return null;

                using (var cmd = new SqlCommand(modelsSql, con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detail.Models.Add(new FulfilledCartridgeModelLineDto
                            {
                                CartridgeModel  = reader.GetString(reader.GetOrdinal("CartridgeModel")),
                                RequestedQty    = reader.GetInt32(reader.GetOrdinal("RequestedQty")),
                                GoodEmptyQty    = reader.GetInt32(reader.GetOrdinal("GoodEmptyQty")),
                                DamagedEmptyQty = reader.GetInt32(reader.GetOrdinal("DamagedEmptyQty")),
                                Status          = reader.GetString(reader.GetOrdinal("Status")),
                            });
                        }
                    }
                }
            }

            return detail;
        }

        /// <summary>
        /// Returns all active employees for the Received By lookup ComboBox.
        /// </summary>
        public List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> GetActiveEmployeesForReceiver()
        {
            var list = new List<(int, string, string, string, string, string)>();

            const string sql = @"
                SELECT e.EmpId, e.Name, ISNULL(e.EmployeeNumber, '') AS EmployeeNumber,
                       ISNULL(e.Position, '') AS Position,
                       ISNULL(b.Name, '') AS BranchName,
                       ISNULL(d.Name, '') AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.GetString(5)
                        ));
                    }
                }
            }

            return list;
        }

        public List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> GetApproverEmployees()
        {
            var list = new List<(int, string, string, string, string, string)>();

            const string sql = @"
                SELECT e.EmpId, e.Name, ISNULL(e.EmployeeNumber, '') AS EmployeeNumber,
                       ISNULL(e.Position, '') AS Position,
                       ISNULL(b.Name, '') AS BranchName,
                       ISNULL(d.Name, '') AS DepartmentName
                FROM dbo.Employee e
                INNER JOIN dbo.ApprovalRoleTitle art
                    ON UPPER(LTRIM(RTRIM(e.Position))) = UPPER(LTRIM(RTRIM(art.PositionTitle)))
                    AND art.IsActive = 1
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.GetString(5)
                        ));
                    }
                }
            }

            return list;
        }
    }

    /// <summary>
    /// Represents a fulfilled portal Set shown on the Send Notifications page.
    /// </summary>
    /// <summary>
    /// A cartridge line of a mixed portal submission, as fulfilled on Mixed Request Exchange /
    /// Unfulfilled / Partially Fulfilled Requests. See CartridgeManagementRepository.IssueMixedCartridgeLine.
    /// </summary>
    public class MixedCartridgeLineInfo
    {
        public int    ReqId            { get; set; }
        public int    Quantity         { get; set; }
        public int    IssuedQty        { get; set; }
        /// <summary>Cartridge item, Request and Set Management workflow, portal submission.</summary>
        public bool   IsExchangeLine   { get; set; }
        public int?   CartridgeModelId { get; set; }
        public string ModelNumber      { get; set; }
        public int    DeclaredGood     { get; set; }
        public int    DeclaredDamaged  { get; set; }
        public int    ReturnedGood     { get; set; }
        public int    ReturnedDamaged  { get; set; }
        public int    AvailableBrandNew { get; set; }
        public int    AvailableRefilled { get; set; }

        public int PendingQty => Math.Max(0, Quantity - IssuedQty);
    }

    public class FulfilledSetNotificationDto
    {
        public int    SetId              { get; set; }
        public string SetCode            { get; set; }
        public string SetStatus          { get; set; }
        public DateTime CreatedAt        { get; set; }
        public int    ReqId              { get; set; }
        public int    EmpId              { get; set; }
        public Guid?  SubmissionSessionId { get; set; }
        public int?   ReceivedById       { get; set; }
        public string ReceivedByName     { get; set; }
        public string RequesterName      { get; set; }
        public string CompanyName        { get; set; }
        public string BranchName         { get; set; }
        public string DepartmentName     { get; set; }
        public string DistributionMethod  { get; set; }
        public int    IssuedBrandNewQty  { get; set; }
        public int    IssuedRefilledQty  { get; set; }
    }

    /// <summary>
    /// One row in the Fulfilled Cartridges history page. Contains enough data to
    /// reconstruct a CartridgeTransmittalViewModel for reprinting.
    /// </summary>
    public class FulfilledCartridgeRowDto
    {
        public int      SetId          { get; set; }
        public string   SetCode        { get; set; }
        public DateTime FulfilledAt    { get; set; }
        public int      ReqId          { get; set; }
        public string   RequesterName  { get; set; }
        public string   CompanyName    { get; set; }
        public string   BranchName     { get; set; }
        public string   DepartmentName { get; set; }
        public string   CartridgeModel { get; set; }
        public int      Quantity       { get; set; }
        public string   ReceivedByName { get; set; }
        public int      TotalIssuedQty { get; set; }

        public string FulfilledAtDisplay =>
            FulfilledAt == DateTime.MinValue ? "" : FulfilledAt.ToString("MM/dd/yyyy");
    }

    /// <summary>
    /// Full detail for a fulfilled Set — header info plus every requested cartridge
    /// model line — shown by the "Open" preview action on the Fulfilled Cartridges page.
    /// </summary>
    public class FulfilledCartridgeDetailDto
    {
        public int      SetId          { get; set; }
        public string   SetCode        { get; set; }
        public DateTime FulfilledAt    { get; set; }
        public int      ReqId          { get; set; }
        public string   RequesterName  { get; set; }
        public string   CompanyName    { get; set; }
        public string   BranchName     { get; set; }
        public string   DepartmentName { get; set; }
        public string   ReceivedByName { get; set; }
        public int      TotalIssuedQty { get; set; }

        public List<FulfilledCartridgeModelLineDto> Models { get; } = new List<FulfilledCartridgeModelLineDto>();

        public string FulfilledAtDisplay =>
            FulfilledAt == DateTime.MinValue ? "" : FulfilledAt.ToString("MM/dd/yyyy");
    }

    /// <summary>
    /// One cartridge model line item within a fulfilled Set's detail preview.
    /// </summary>
    public class FulfilledCartridgeModelLineDto
    {
        public string CartridgeModel  { get; set; }
        public int    RequestedQty    { get; set; }
        public int    GoodEmptyQty    { get; set; }
        public int    DamagedEmptyQty { get; set; }
        public string Status          { get; set; }
    }
}
