using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Repositories
{
    public class RequestRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public RequestRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public int AddRequest(Pages.RequestDto request)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Insert into Request table
                        const string sqlRequest = @"
                    INSERT INTO dbo.Request
                        (DateRequested, Description, Remarks, Status, EntryType, Quantity, IssuedQty,
                         DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId,
                         ComId, DeptId, BranchId,
                         SubmissionSessionId, ConditionID, RequestSource, ReceivedById, WorkflowType)
                    VALUES
                        (@DateRequested, @Description, @Remarks, @Status, @EntryType, @Quantity, @IssuedQty,
                         @DateCreated, @CreatedBy, @DateModified, @ModifiedBy, @ItemId, @EmpId,
                         @ComId, @DeptId, @BranchId,
                         @SubmissionSessionId, @ConditionID, @RequestSource, @ReceivedById, @WorkflowType);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newRequestId;
                        using (var cmd = new SqlCommand(sqlRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@DateRequested", (object)request.DateRequested ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Description", (object)request.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", (object)request.Remarks ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Status", request.Status);
                            cmd.Parameters.AddWithValue("@EntryType", request.EntryType); // Should be "Negative"
                            cmd.Parameters.AddWithValue("@Quantity", request.Quantity);
                            cmd.Parameters.AddWithValue("@IssuedQty", request.IssuedQty);
                            cmd.Parameters.AddWithValue("@DateCreated", request.DateCreated);
                            cmd.Parameters.AddWithValue("@CreatedBy", request.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@DateModified", request.DateCreated);
                            cmd.Parameters.AddWithValue("@ModifiedBy", request.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                            cmd.Parameters.AddWithValue("@EmpId", (object)request.EmpId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ComId", (object)request.ComId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DeptId", (object)request.DeptId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BranchId", (object)request.BranchId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SubmissionSessionId", (object)request.SubmissionSessionId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ConditionID", (object)request.ConditionID ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RequestSource", (object)request.RequestSource ?? "PORTAL");
                            cmd.Parameters.AddWithValue("@ReceivedById", (object)request.ReceivedById ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@WorkflowType", (object)request.WorkflowType ?? DBNull.Value);

                            newRequestId = (int)cmd.ExecuteScalar();
                        }

                        // PORTAL REQUEST DETECTION: Skip inventory operations for intent-only portal requests
                        // Portal requests are marked with [PORTAL] tag in Description field
                        // These are intent-only requests that don't allocate inventory immediately
                        bool isPortalRequest = !string.IsNullOrEmpty(request.Description)
                                               && request.Description.Contains("[PORTAL]");

                        if (isPortalRequest)
                        {
                            // Portal requests skip inventory allocation
                            // They will be allocated later when converted to actual Sets
                            transaction.Commit();
                            ActivityLogger.Log(request.CreatedByUserId, ActivityLogger.Actions.Create,
                                "Request", newRequestId,
                                $"[Portal] Request #{newRequestId} created for {request.EmployeeName}");
                            return newRequestId;
                        }

                        // CRITICAL: Get item properties to determine correct EntryType
                        // NOTE: AcquisitionType is informational only - no longer enforced
                        // Business rule change: Requests and Invoices can now accept any item type
                        bool affectsInventory = false;
                        const string sqlGetItemProps = @"SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(sqlGetItemProps, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    affectsInventory = !reader.IsDBNull(0) && reader.GetBoolean(0);
                                }
                            }
                        }

                        // ==================================================================================
                        // TRANSACTIONAL INVENTORY CREATION
                        // ==================================================================================
                        // CRITICAL: Only create TRANSACTIONAL Inventory when:
                        //   1. item.AffectsInventory == true (inventory-tracked items only)
                        //   2. request.SetId exists (transactional inventory requires Set association)
                        //
                        // BASELINE inventory was already created when the Item was added to the system.
                        // This is TRANSACTIONAL inventory - recording movement/allocation to a Set.
                        //
                        // If SetId is missing:
                        //   - Do NOT throw error (Request can exist without Set initially)
                        //   - Do NOT create Inventory (wait until Request is added to Set)
                        //   - Inventory will be created later when SetRepository.MaterializeSetItemsFromRequests runs
                        // ==================================================================================
                        if (affectsInventory && request.SetId.HasValue && request.SetId.Value > 0)
                        {
                            // Determine EntryType for TRANSACTIONAL inventory: "Negative" for requests (stock allocation)
                            string inventoryEntryType = Helpers.InventoryHelper.DetermineEntryType(affectsInventory, -request.Quantity);

                            // Step 2: UPDATE existing Inventory record to allocate to request
                            // Attempt to convert an existing positive inventory entry to link to the new request
                            const string sqlUpdateInventory = @"
    UPDATE dbo.Inventory
    SET EntryType = @EntryType,
        ReqId = @ReqId,
        Description = @Description,
        DatePosted = @DatePosted,
        PostedBy = @PostedBy
    WHERE ItemId = @ItemId
      AND SetId = @SetId
      AND EntryType = 'Positive'
      AND ReqId IS NULL";

                            using (var cmd = new SqlCommand(sqlUpdateInventory, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EntryType", inventoryEntryType);
                                cmd.Parameters.AddWithValue("@ReqId", newRequestId);
                                cmd.Parameters.AddWithValue("@Description",
                                    $"Requested for {request.EmployeeName} - Request #{newRequestId}" +
                                    (string.IsNullOrWhiteSpace(request.Description) ? "" : $" - {request.Description}"));
                                cmd.Parameters.AddWithValue("@DatePosted", request.DateRequested ?? request.DateCreated);
                                cmd.Parameters.AddWithValue("@PostedBy", request.CreatedByUserId);
                                cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                                cmd.Parameters.AddWithValue("@SetId", request.SetId.Value);

                                int rowsAffected = cmd.ExecuteNonQuery();

                                if (rowsAffected == 0)
                                {
                                    // No convertible positive entry found Ã¯Â¿Â½ create a new ledger row instead, linked to the request
                                    const string sqlInsertInventory = @"
            INSERT INTO dbo.Inventory
                (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId, Active)
            VALUES
                (@DescriptionIns, @EntryTypeIns, @QuantityIns, @DatePostedIns, @PostedByIns, @ReqIdIns, @ItemIdIns, @SetIdIns, 1)";

                                    using (var ins = new SqlCommand(sqlInsertInventory, con, transaction))
                                    {
                                        ins.Parameters.AddWithValue("@DescriptionIns",
                                            $"Requested for {request.EmployeeName} - Request #{newRequestId}" +
                                            (string.IsNullOrWhiteSpace(request.Description) ? "" : $" - {request.Description}"));
                                        ins.Parameters.AddWithValue("@EntryTypeIns", inventoryEntryType);
                                        ins.Parameters.AddWithValue("@QuantityIns", request.Quantity); // use positive qty but EntryType indicates negative
                                        ins.Parameters.AddWithValue("@DatePostedIns", request.DateRequested ?? request.DateCreated);
                                        ins.Parameters.AddWithValue("@PostedByIns", request.CreatedByUserId);
                                        ins.Parameters.AddWithValue("@ReqIdIns", newRequestId);
                                        ins.Parameters.AddWithValue("@ItemIdIns", request.ItemId);
                                        ins.Parameters.AddWithValue("@SetIdIns", request.SetId.Value);

                                        ins.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        // =====================================================
                        // CRITICAL FIX: DO NOT deduct stock during request creation
                        // =====================================================
                        // Stock deduction moved to fulfillment (Set creation/deployment)
                        // Request creation is inventory READ-ONLY
                        // This prevents double-deduction bug where stock was deducted here
                        // AND again during Set processing (SetRepository.ProcessPendingUpdatesAsync)
                        // =====================================================

                        // Step 3: REMOVED - Stock deduction now happens ONLY at fulfillment
                        // Previously: StockOnHand was decreased here (INCORRECT)
                        // Now: Stock is only decreased when Set is created and deployed
                        // See: SetRepository.ProcessPendingUpdatesAsync() line 160-172

                        transaction.Commit();
                        ActivityLogger.Log(request.CreatedByUserId, ActivityLogger.Actions.Create,
                            "Request", newRequestId,
                            $"Request #{newRequestId} created for {request.EmployeeName}");

                        try
                        {
                            var org = request.EmpId.HasValue ? GetEmployeeOrg(request.EmpId.Value) : null;
                            var serial = !string.IsNullOrWhiteSpace(request.SerialNumber)
                                ? request.SerialNumber
                                : GetItemSerialNumber(request.ItemId);

                            var auditRepo = new ItemAuditTrailRepository();

                            var actionTime = request.DateRequested ?? request.DateCreated;
                            if (actionTime.TimeOfDay == TimeSpan.Zero)
                            {
                                actionTime = request.DateCreated.TimeOfDay == TimeSpan.Zero
                                    ? DateTime.Now
                                    : request.DateCreated;
                            }

                            auditRepo.LogActionAsync(new ItemAuditTrailDto
                            {
                                ItemId = request.ItemId,
                                SerialNumber = serial,
                                Action = "Request Created",
                                ActionTime = actionTime,
                                Direction = "OUT",
                                Status = request.Status,
                                EmployeeName = org?.EmployeeName ?? request.EmployeeName,
                                DepartmentName = org?.DepartmentName,
                                BranchName = org?.BranchName,
                                ReferenceType = "Request",
                                ReferenceId = newRequestId,
                                Notes = $"Requested by {org?.EmployeeName ?? request.EmployeeName} \u2022 Request #{newRequestId}",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            }).GetAwaiter().GetResult();
                        }
                        catch
                        {
                        }

                        // Inventory System "Activity" feed (best-effort; coalesced for batch creates).
                        try
                        {
                            int actorId = request.CreatedByUserId > 0 ? request.CreatedByUserId : AppSession.CurrentUserId;
                            Services.InventoryActivityNotifier.NotifyRequestCreated(
                                newRequestId, request.ItemName, request.Quantity, actorId, request.SerialNumber);
                        }
                        catch
                        {
                        }

                        return newRequestId;
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
        /// Loads all (non-archived, non-Awaiting-Authorization) requests. The optional filter
        /// params drive the Advanced Filters "SET / INVOICE" / "REQUEST" sections on the WPF View
        /// Request page — each is matched server-side (round trip, debounced by the ViewModel)
        /// rather than client-side, since ReferenceCode/ParentTag live on dbo.SetItem and aren't
        /// otherwise selected into RequestDto. All other filtering (search text, status, date
        /// range, category) still happens client-side in RequestPageViewModel.ApplyFilter().
        /// </summary>
        public List<Pages.RequestDto> GetAllRequests(
            string setCodeFilter = null,
            string documentNumberFilter = null,
            string companyFilter = null,
            string departmentFilter = null,
            string branchFilter = null,
            string employeeFilter = null,
            string referenceCodeFilter = null,
            string parentTagFilter = null,
            string reqIdFilter = null)
        {
            var requests = new List<Pages.RequestDto>();
            var pendingPortalResolutions = new List<PortalRequestResolution>();

            var whereClauses = new List<string>
            {
                "arch_req.EntityId IS NULL",
                "arch_itm.EntityId IS NULL",
                "(e.EmpId IS NULL OR arch_emp.EntityId IS NULL)",
                "r.Status != 'Awaiting Authorization'"
            };

            // Set/Invoice-linked filters. SetCode/DocumentNumber/Company/Department/Branch match
            // directly against the request's own single linked Set (r.SetId, already LEFT JOINed
            // as `st`, with Company/Department/Branch already COALESCEd against the requester's
            // org vs. the request's own org). ReferenceCode/ParentTag live on dbo.SetItem (which
            // can have several rows per Set), so those are matched via EXISTS against the specific
            // SetId + ItemId pair this Request represents, rather than a plain JOIN that could fan
            // the result set out to duplicate Request rows.
            if (!string.IsNullOrWhiteSpace(setCodeFilter))
                whereClauses.Add("st.SetCode LIKE @SetCode");
            if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                whereClauses.Add("st.DocumentNumber LIKE @DocumentNumber");
            if (!string.IsNullOrWhiteSpace(companyFilter))
                whereClauses.Add("COALESCE(ec.Name, rc.Name) LIKE @Company");
            if (!string.IsNullOrWhiteSpace(departmentFilter))
                whereClauses.Add("COALESCE(ed.Name, rd.Name) LIKE @Department");
            if (!string.IsNullOrWhiteSpace(branchFilter))
                whereClauses.Add("COALESCE(eb.Name, rb.Name) LIKE @Branch");
            if (!string.IsNullOrWhiteSpace(employeeFilter))
                whereClauses.Add("e.Name LIKE @Employee");
            if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                whereClauses.Add(@"EXISTS (
    SELECT 1 FROM dbo.SetItem si
    WHERE si.SetId = r.SetId AND si.ItemId = r.ItemId AND si.ReferenceCode LIKE @ReferenceCode
)");
            if (!string.IsNullOrWhiteSpace(parentTagFilter))
                whereClauses.Add(@"EXISTS (
    SELECT 1 FROM dbo.SetItem si
    INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si.ParentTagGroupId
    WHERE si.SetId = r.SetId AND si.ItemId = r.ItemId AND ptg.Label LIKE @ParentTag
)");
            if (!string.IsNullOrWhiteSpace(reqIdFilter))
                whereClauses.Add("CAST(r.ReqId AS NVARCHAR(20)) LIKE @ReqId");

            string sql = $@"
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
                    r.ComId,
                    r.DeptId,
                    r.BranchId,
                    r.SetId,
                    r.ConditionID,
                    r.RequestSource,
                    r.SubmissionSessionId,
                    r.ReceivedById,
                    r.WorkflowType,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    i.Category,
                    i.SerialNumber,
                    i.IsTrackedAsset,
                    e.Name AS EmployeeName,
                    e.EmployeeNumber,
                    e.Position AS EmployeePosition,
                    COALESCE(ec.Name, rc.Name) AS CompanyName,
                    COALESCE(ed.Name, rd.Name) AS DepartmentName,
                    COALESCE(eb.Name, rb.Name) AS BranchName,
                    u.Name AS CreatedByName,
                    mu.Name AS ModifiedByName,
                    st.SetCode,
                    cnd.ConditionName,
                    rcv.Name AS ReceivedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT  JOIN dbo.Employee   e  ON e.EmpId     = r.EmpId
                INNER JOIN dbo.[User]     u  ON u.UserId    = r.Createdby
                LEFT  JOIN dbo.[User]     mu ON mu.UserId   = r.ModifiedBy
                LEFT  JOIN dbo.Company    ec ON ec.ComId    = e.ComId
                LEFT  JOIN dbo.Department ed ON ed.DeptId   = e.DeptId
                LEFT  JOIN dbo.Branch     eb ON eb.BranchId = e.BranchId
                LEFT  JOIN dbo.Company    rc ON rc.ComId    = r.ComId
                LEFT  JOIN dbo.Department rd ON rd.DeptId   = r.DeptId
                LEFT  JOIN dbo.Branch     rb ON rb.BranchId = r.BranchId
                LEFT  JOIN dbo.[Set]      st ON st.SetId    = r.SetId
                LEFT  JOIN dbo.Condition  cnd ON cnd.ConditionID = r.ConditionID
                LEFT  JOIN dbo.Employee  rcv ON rcv.EmpId   = r.ReceivedById
                LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId AND arch_req.IsArchived = 1
                LEFT JOIN dbo.ArchiveStatus arch_itm ON arch_itm.EntityType = 'Item'    AND arch_itm.EntityId = i.ItemId AND arch_itm.IsArchived = 1
                LEFT JOIN dbo.ArchiveStatus arch_emp ON arch_emp.EntityType = 'Employee' AND arch_emp.EntityId = e.EmpId AND arch_emp.IsArchived = 1
                WHERE {string.Join(" AND ", whereClauses)}
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                if (!string.IsNullOrWhiteSpace(setCodeFilter))
                    cmd.Parameters.AddWithValue("@SetCode", $"%{setCodeFilter}%");
                if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                    cmd.Parameters.AddWithValue("@DocumentNumber", $"%{documentNumberFilter}%");
                if (!string.IsNullOrWhiteSpace(companyFilter))
                    cmd.Parameters.AddWithValue("@Company", $"%{companyFilter}%");
                if (!string.IsNullOrWhiteSpace(departmentFilter))
                    cmd.Parameters.AddWithValue("@Department", $"%{departmentFilter}%");
                if (!string.IsNullOrWhiteSpace(branchFilter))
                    cmd.Parameters.AddWithValue("@Branch", $"%{branchFilter}%");
                if (!string.IsNullOrWhiteSpace(employeeFilter))
                    cmd.Parameters.AddWithValue("@Employee", $"%{employeeFilter}%");
                if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                    cmd.Parameters.AddWithValue("@ReferenceCode", $"%{referenceCodeFilter}%");
                if (!string.IsNullOrWhiteSpace(parentTagFilter))
                    cmd.Parameters.AddWithValue("@ParentTag", $"%{parentTagFilter}%");
                if (!string.IsNullOrWhiteSpace(reqIdFilter))
                    cmd.Parameters.AddWithValue("@ReqId", $"%{reqIdFilter}%");

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
                        bool isPortalRequest = description != null && description.Contains("[PORTAL]");

                        // Extract model number from portal request description
                        string modelNumber = null;
                        string itemName = reader.GetString(reader.GetOrdinal("ItemName"));
                        string category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category"));
                        string serialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber"));

                        if (isPortalRequest)
                        {
                            // For portal requests, extract model from Description [MODEL:XXX] format
                            modelNumber = ExtractModelFromDescription(description);
                            // Portal requests don't have serial numbers - use N/A
                            serialNumber = "N/A";
                            // Use "Cartridge (Portal Request)" to indicate source
                            category = "Cartridge";
                        }
                        else
                        {
                            // For regular requests, use Item table data
                            modelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber"));
                        }

                        var dto = new Pages.RequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            Description = description,
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            EntryType = reader.IsDBNull(reader.GetOrdinal("EntryType")) ? null : reader.GetString(reader.GetOrdinal("EntryType")),
                            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            CreatedByUserId = reader.GetInt32(reader.GetOrdinal("Createdby")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            ComId = reader.IsDBNull(reader.GetOrdinal("ComId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ComId")),
                            DeptId = reader.IsDBNull(reader.GetOrdinal("DeptId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DeptId")),
                            BranchId = reader.IsDBNull(reader.GetOrdinal("BranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("BranchId")),
                            ItemName = itemName,
                            ModelNumber = modelNumber,
                            Category = category,
                            SerialNumber = serialNumber,
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")) ? null : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                            EmployeePosition = reader.IsDBNull(reader.GetOrdinal("EmployeePosition")) ? null : reader.GetString(reader.GetOrdinal("EmployeePosition")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                            CreatedByName = reader.GetString(reader.GetOrdinal("CreatedByName")),
                            ModifiedByName = reader.IsDBNull(reader.GetOrdinal("ModifiedByName")) ? null : reader.GetString(reader.GetOrdinal("ModifiedByName")),
                            SetId = reader.IsDBNull(reader.GetOrdinal("SetId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode")),
                            ConditionID = reader.IsDBNull(reader.GetOrdinal("ConditionID")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ConditionID")),
                            ConditionName = reader.IsDBNull(reader.GetOrdinal("ConditionName")) ? null : reader.GetString(reader.GetOrdinal("ConditionName")),
                            RequestSource = reader.IsDBNull(reader.GetOrdinal("RequestSource")) ? null : reader.GetString(reader.GetOrdinal("RequestSource")),
                            SubmissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId")),
                            ReceivedById = reader.IsDBNull(reader.GetOrdinal("ReceivedById")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ReceivedById")),
                            ReceivedByName = reader.IsDBNull(reader.GetOrdinal("ReceivedByName")) ? null : reader.GetString(reader.GetOrdinal("ReceivedByName")),
                            IsTrackedAsset = !reader.IsDBNull(reader.GetOrdinal("IsTrackedAsset")) && reader.GetBoolean(reader.GetOrdinal("IsTrackedAsset")),
                            WorkflowType = reader.IsDBNull(reader.GetOrdinal("WorkflowType")) ? null : reader.GetString(reader.GetOrdinal("WorkflowType"))
                        };

                        requests.Add(dto);

                        if (isPortalRequest && !string.IsNullOrWhiteSpace(modelNumber))
                        {
                            pendingPortalResolutions.Add(new PortalRequestResolution
                            {
                                Request = dto,
                                ModelNumber = modelNumber
                            });
                        }
                    }
                }

                // IMPORTANT: For portal cartridge requests, NEVER use ResolveItemNameByModel fallback.
                // The Item.Name from the database join is just a placeholder/reference item
                // (e.g., the first cartridge in the system like "rx 310 cart").
                // The actual cartridge model being requested is in the Description [MODEL:xxx] tag.
                // Display format: "Cartridge (Model: XXX)" to clearly show this is model-based.
                foreach (var pending in pendingPortalResolutions)
                {
                    pending.Request.ItemName = $"Cartridge (Model: {pending.ModelNumber})";
                }
            }

            return requests;
        }

        /// <summary>
        /// Extracts the cartridge model number from portal request Description field.
        /// Format: [PORTAL] [MODEL:XXX] PICKUP Ã¢â€ â€™ Branch, Department
        /// </summary>
        private string ExtractModelFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            // Look for [MODEL:XXX] pattern
            int modelStart = description.IndexOf("[MODEL:");
            if (modelStart == -1)
                return null;

            int modelValueStart = modelStart + "[MODEL:".Length;
            int modelEnd = description.IndexOf("]", modelValueStart);

            if (modelEnd == -1)
                return null;

            return description.Substring(modelValueStart, modelEnd - modelValueStart).Trim();
        }

        /// <summary>
        /// Resolves the actual Item Name based on the model number.
        /// Used for portal requests to find the correct cartridge name.
        /// </summary>
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

        public List<Pages.RequestDto> GetCartridgeRequests()
        {
            var requests = new List<Pages.RequestDto>();
            var pendingPortalResolutions = new List<PortalRequestResolution>();

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
                    r.ComId,
                    r.DeptId,
                    r.BranchId,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    i.Category,
                    i.SerialNumber,
                    e.Name AS EmployeeName,
                    e.EmployeeNumber,
                    COALESCE(ec.Name, rc.Name) AS CompanyName,
                    COALESCE(ed.Name, rd.Name) AS DepartmentName,
                    COALESCE(eb.Name, rb.Name) AS BranchName,
                    u.Name AS CreatedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT  JOIN dbo.Employee   e  ON e.EmpId     = r.EmpId
                INNER JOIN dbo.[User]     u  ON u.UserId    = r.Createdby
                LEFT  JOIN dbo.Company    ec ON ec.ComId    = e.ComId
                LEFT  JOIN dbo.Department ed ON ed.DeptId   = e.DeptId
                LEFT  JOIN dbo.Branch     eb ON eb.BranchId = e.BranchId
                LEFT  JOIN dbo.Company    rc ON rc.ComId    = r.ComId
                LEFT  JOIN dbo.Department rd ON rd.DeptId   = r.DeptId
                LEFT  JOIN dbo.Branch     rb ON rb.BranchId = r.BranchId
                LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId AND arch_req.IsArchived = 1
                LEFT JOIN dbo.ArchiveStatus arch_itm ON arch_itm.EntityType = 'Item'    AND arch_itm.EntityId = i.ItemId AND arch_itm.IsArchived = 1
                LEFT JOIN dbo.ArchiveStatus arch_emp ON arch_emp.EntityType = 'Employee' AND arch_emp.EntityId = e.EmpId AND arch_emp.IsArchived = 1
                WHERE arch_req.EntityId IS NULL
                  AND arch_itm.EntityId IS NULL
                  AND (e.EmpId IS NULL OR arch_emp.EntityId IS NULL)
                  AND i.Category = 'Cartridge'
                  AND r.Status != 'Awaiting Authorization'
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
                        bool isPortalRequest = description != null && description.Contains("[PORTAL]");

                        // Extract model number from portal request description
                        string modelNumber = null;
                        string itemName = reader.GetString(reader.GetOrdinal("ItemName"));
                        string category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category"));
                        string serialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber"));

                        if (isPortalRequest)
                        {
                            // For portal requests, extract model from Description [MODEL:XXX] format
                            modelNumber = ExtractModelFromDescription(description);
                            // Portal requests don't have serial numbers - use N/A
                            serialNumber = "N/A";
                            // Use "Cartridge (Portal Request)" to indicate source
                            category = "Cartridge";
                        }
                        else
                        {
                            // For regular requests, use Item table data
                            modelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber"));
                        }

                        var dto = new Pages.RequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            Description = description,
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            EntryType = reader.IsDBNull(reader.GetOrdinal("EntryType")) ? null : reader.GetString(reader.GetOrdinal("EntryType")),
                            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            CreatedByUserId = reader.GetInt32(reader.GetOrdinal("Createdby")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            ComId = reader.IsDBNull(reader.GetOrdinal("ComId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ComId")),
                            DeptId = reader.IsDBNull(reader.GetOrdinal("DeptId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DeptId")),
                            BranchId = reader.IsDBNull(reader.GetOrdinal("BranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("BranchId")),
                            ItemName = itemName,
                            ModelNumber = modelNumber,
                            Category = category,
                            SerialNumber = serialNumber,
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")) ? null : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                            CreatedByName = reader.GetString(reader.GetOrdinal("CreatedByName"))
                        };

                        requests.Add(dto);

                        if (isPortalRequest && !string.IsNullOrWhiteSpace(modelNumber))
                        {
                            pendingPortalResolutions.Add(new PortalRequestResolution
                            {
                                Request = dto,
                                ModelNumber = modelNumber
                            });
                        }
                    }
                }

                // IMPORTANT: For portal cartridge requests, NEVER use ResolveItemNameByModel fallback.
                // The Item.Name from the database join is just a placeholder/reference item
                // (e.g., the first cartridge in the system like "rx 310 cart").
                // The actual cartridge model being requested is in the Description [MODEL:xxx] tag.
                // Display format: "Cartridge (Model: XXX)" to clearly show this is model-based.
                foreach (var pending in pendingPortalResolutions)
                {
                    pending.Request.ItemName = $"Cartridge (Model: {pending.ModelNumber})";
                }
            }

            return requests;
        }

        public Pages.RequestDto GetRequestById(int reqId)
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
                    r.ComId,
                    r.DeptId,
                    r.BranchId,
                    r.ConditionID,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    i.Category,
                    i.SerialNumber,
                    e.Name AS EmployeeName,
                    e.EmployeeNumber,
                    COALESCE(ec.Name, rc.Name) AS CompanyName,
                    COALESCE(ed.Name, rd.Name) AS DepartmentName,
                    COALESCE(eb.Name, rb.Name) AS BranchName,
                    u.Name AS CreatedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT  JOIN dbo.Employee   e  ON e.EmpId     = r.EmpId
                INNER JOIN dbo.[User]     u  ON u.UserId    = r.Createdby
                LEFT  JOIN dbo.Company    ec ON ec.ComId    = e.ComId
                LEFT  JOIN dbo.Department ed ON ed.DeptId   = e.DeptId
                LEFT  JOIN dbo.Branch     eb ON eb.BranchId = e.BranchId
                LEFT  JOIN dbo.Company    rc ON rc.ComId    = r.ComId
                LEFT  JOIN dbo.Department rd ON rd.DeptId   = r.DeptId
                LEFT  JOIN dbo.Branch     rb ON rb.BranchId = r.BranchId
                WHERE r.ReqId = @ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Pages.RequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            EntryType = reader.IsDBNull(reader.GetOrdinal("EntryType")) ? null : reader.GetString(reader.GetOrdinal("EntryType")),
                            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            CreatedByUserId = reader.GetInt32(reader.GetOrdinal("Createdby")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            ComId = reader.IsDBNull(reader.GetOrdinal("ComId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ComId")),
                            DeptId = reader.IsDBNull(reader.GetOrdinal("DeptId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DeptId")),
                            BranchId = reader.IsDBNull(reader.GetOrdinal("BranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("BranchId")),
                            ConditionID = reader.IsDBNull(reader.GetOrdinal("ConditionID")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ConditionID")),
                            ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")) ? null : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                            CreatedByName = reader.GetString(reader.GetOrdinal("CreatedByName"))
                        };
                    }
                }
            }

            return null;
        }

        public void UpdateRequest(Pages.RequestDto request)
        {
            Pages.RequestDto before = null;
            try
            {
                before = GetRequestById(request.ReqId);
            }
            catch
            {
                // Intentionally ignore. Update must still proceed.
            }

            const string sql = @"
                UPDATE dbo.Request
                SET
                    DateRequested = @DateRequested,
                    Description = @Description,
                    Remarks = @Remarks,
                    Status = @Status,
                    EntryType = @EntryType,
                    Quantity = @Quantity,
                    DateModified = @DateModified,
                    Modifiedby = @Modifiedby,
                    ItemId = @ItemId,
                    EmpId = @EmpId,
                    ComId = @ComId,
                    DeptId = @DeptId,
                    BranchId = @BranchId,
                    ConditionID = @ConditionID
                WHERE ReqId = @ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", request.ReqId);
                cmd.Parameters.AddWithValue("@DateRequested", (object)request.DateRequested ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", (object)request.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Remarks", (object)request.Remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", request.Status);
                cmd.Parameters.AddWithValue("@EntryType", (object)request.EntryType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantity", request.Quantity);
                cmd.Parameters.AddWithValue("@DateModified", DateTime.Now);
                cmd.Parameters.AddWithValue("@Modifiedby", request.ModifiedByUserId);
                cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                cmd.Parameters.AddWithValue("@EmpId", (object)request.EmpId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ComId", (object)request.ComId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeptId", (object)request.DeptId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId", (object)request.BranchId ?? DBNull.Value);
                // ConditionID is optional - only applicable for hardware items with condition tracking
                cmd.Parameters.AddWithValue("@ConditionID", (object)request.ConditionID ?? DBNull.Value);

                con.Open();
                cmd.ExecuteNonQuery();
            }
            ActivityLogger.Log(request.ModifiedByUserId, ActivityLogger.Actions.Update,
                "Request", request.ReqId,
                $"Request #{request.ReqId} updated");

            try
            {
                if (before != null && before.EmpId != request.EmpId)
                {
                    var after = GetRequestById(request.ReqId);
                    if (after != null && after.ItemId > 0)
                    {
                        var oldOrg = before.EmpId.HasValue ? GetEmployeeOrg(before.EmpId.Value) : null;
                        var newOrg = after.EmpId.HasValue ? GetEmployeeOrg(after.EmpId.Value) : null;
                        var auditRepo = new ItemAuditTrailRepository();
                        var now = DateTime.Now;

                        auditRepo.LogActionAsync(new ItemAuditTrailDto
                        {
                            ItemId = after.ItemId,
                            SerialNumber = after.SerialNumber,
                            Action = "Ownership Transfer Initiated",
                            ActionTime = now.AddSeconds(-1),
                            Direction = "OUT",
                            Status = "In Progress",
                            EmployeeName = oldOrg?.EmployeeName ?? before.EmployeeName,
                            DepartmentName = oldOrg?.DepartmentName,
                            BranchName = oldOrg?.BranchName,
                            ReferenceType = "Request",
                            ReferenceId = request.ReqId,
                            SetCode = null,
                            Notes = $"Request #{request.ReqId} reassigned from {before.EmployeeName} to {after.EmployeeName}",
                            CreatedBy = AppSession.CurrentUserName ?? "System"
                        }).GetAwaiter().GetResult();

                        auditRepo.LogActionAsync(new ItemAuditTrailDto
                        {
                            ItemId = after.ItemId,
                            SerialNumber = after.SerialNumber,
                            Action = "Ownership Transfer Completed",
                            ActionTime = now,
                            Direction = "IN",
                            Status = "Completed",
                            EmployeeName = newOrg?.EmployeeName ?? after.EmployeeName,
                            DepartmentName = newOrg?.DepartmentName,
                            BranchName = newOrg?.BranchName,
                            ReferenceType = "Request",
                            ReferenceId = request.ReqId,
                            SetCode = null,
                            Notes = $"Request #{request.ReqId} reassigned from {before.EmployeeName} to {after.EmployeeName}",
                            CreatedBy = AppSession.CurrentUserName ?? "System"
                        }).GetAwaiter().GetResult();
                    }
                }
            }
            catch
            {
            }
        }

        private EmployeeOrg GetEmployeeOrg(int empId)
        {
            const string sql = @"
                SELECT
                    e.Name AS EmployeeName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName
                FROM dbo.Employee e
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                WHERE e.EmpId = @EmpId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EmpId", empId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new EmployeeOrg
                    {
                        EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                        DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                        BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName"))
                    };
                }
            }
        }

        private class EmployeeOrg
        {
            public string EmployeeName { get; set; }
            public string DepartmentName { get; set; }
            public string BranchName { get; set; }
        }

        private class PortalRequestResolution
        {
            public Pages.RequestDto Request { get; set; }
            public string ModelNumber { get; set; }
        }

        private string GetItemSerialNumber(int itemId)
        {
            const string sql = @"SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId";
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                con.Open();
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return null;
                return Convert.ToString(result);
            }
        }

        public void DeleteRequest(int reqId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Delete related Inventory entries first (if any)
                        using (var cmd = new SqlCommand(
                            "DELETE FROM dbo.Inventory WHERE ReqId = @ReqId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.ExecuteNonQuery();
                        }

                        // Step 2: Delete the Request
                        using (var cmd = new SqlCommand(
                            "DELETE FROM dbo.Request WHERE ReqId = @ReqId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.ExecuteNonQuery();
                        }

                        // Step 3: Clean up ArchiveStatus record (if exists)
                        using (var cmd = new SqlCommand(
                            "DELETE FROM ArchiveStatus WHERE EntityType = 'Request' AND EntityId = @ReqId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<Pages.RequestDto> GetRequestsByStatus(string status)
        {
            var requests = new List<Pages.RequestDto>();

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
                    e.Name AS EmployeeName,
                    COALESCE(ec.Name, rc.Name) AS CompanyName,
                    u.Name AS CreatedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT  JOIN dbo.Employee   e  ON e.EmpId     = r.EmpId
                INNER JOIN dbo.[User]     u  ON u.UserId    = r.Createdby
                LEFT  JOIN dbo.Company    ec ON ec.ComId    = e.ComId
                LEFT  JOIN dbo.Company    rc ON rc.ComId    = r.ComId
                WHERE r.Status = @Status
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Status", status);
                con.Open();
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        requests.Add(new Pages.RequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
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
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            CreatedByName = reader.GetString(reader.GetOrdinal("CreatedByName"))
                        });
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Deletes a request and restores the stock quantity back to the Item
        /// </summary>
        public async Task<bool> DeleteRequestAndRestoreStock(int reqId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Get the request details before deleting (to know item and quantity)
                        string getRequestQuery = @"
                            SELECT r.ItemId, r.Quantity, r.SetId, i.AffectsInventory
                            FROM dbo.Request r
                            INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                            WHERE r.ReqId = @ReqId";

                        int itemId = 0;
                        int quantity = 0;
                        int? setId = null;
                        bool affectsInventory = false;

                        using (SqlCommand cmd = new SqlCommand(getRequestQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    itemId = reader.GetInt32(0);
                                    quantity = reader.GetInt32(1);
                                    setId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                    affectsInventory = !reader.IsDBNull(3) && reader.GetBoolean(3);
                                }
                                else
                                {
                                    throw new Exception("Request not found");
                                }
                            }
                        }

                        // =====================================================
                        // CRITICAL FIX: DO NOT restore stock during request deletion
                        // =====================================================
                        // Since request creation no longer deducts stock,
                        // request deletion should NOT restore stock
                        // Stock is only affected during fulfillment (Set processing)
                        // =====================================================

                        // Step 2: REMOVED - Stock restoration no longer needed
                        // Previously: StockOnHand was restored here (INCORRECT - was fixing wrong deduction)
                        // Now: Stock was never deducted during request creation, so no restoration needed
                        // Stock changes only happen at fulfillment/cancellation of Sets

                        // Step 3: Delete related Inventory entries (if any)
                        using (SqlCommand cmd = new SqlCommand(
                            "DELETE FROM dbo.Inventory WHERE ReqId = @ReqId", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        
                        // Step 4: Delete the request
                        string deleteRequestQuery = "DELETE FROM dbo.Request WHERE ReqId = @ReqId";
                        using (SqlCommand cmd = new SqlCommand(deleteRequestQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        
                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Error deleting request and restoring stock: {ex.Message}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Gets request details for display in confirmation dialog
        /// </summary>
        public async Task<(string ItemName, int Quantity, string EmployeeName)> GetRequestDetailsForDelete(int reqId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                
                string query = @"
                    SELECT i.Name, r.Quantity, e.Name
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                    WHERE r.ReqId = @ReqId";
                
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return (reader.GetString(0), reader.GetInt32(1), reader.GetString(2));
                        }
                    }
                }
            }
            
            return (null, 0, null);
        }

        public bool SubmitRequest(int reqId, int userId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Get the request details and check current stock
                        // CRITICAL: Include AffectsInventory to determine if inventory insert is needed
                        string getRequestSql = @"
                            SELECT
                                r.ReqId,
                                r.ItemId,
                                r.Quantity,
                                r.EmpId,
                                r.EntryType,
                                r.Description,
                                r.SetId,
                                i.Name AS ItemName,
                                i.ItemType,
                                i.StockOnHand,
                                i.AffectsInventory,
                                e.Name AS EmployeeName
                            FROM dbo.Request r
                            INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                            INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                            WHERE r.ReqId = @ReqId";

                        Pages.RequestDto request = null;
                        int currentStock = 0;
                        string itemType = null;
                        bool affectsInventory = false;
                        int? setId = null;

                        using (var cmd = new SqlCommand(getRequestSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);

                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    request = new Pages.RequestDto
                                    {
                                        ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                                        ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                        Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                                        EmpId = reader.GetInt32(reader.GetOrdinal("EmpId")),
                                        EntryType = reader.IsDBNull(reader.GetOrdinal("EntryType")) ? null : reader.GetString(reader.GetOrdinal("EntryType")),
                                        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                                        ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                                        EmployeeName = reader.GetString(reader.GetOrdinal("EmployeeName")),
                                        SetId = reader.IsDBNull(reader.GetOrdinal("SetId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("SetId"))
                                    };
                                    currentStock = reader.GetInt32(reader.GetOrdinal("StockOnHand"));
                                    itemType = reader.IsDBNull(reader.GetOrdinal("ItemType")) ? null : reader.GetString(reader.GetOrdinal("ItemType"));
                                    affectsInventory = !reader.IsDBNull(reader.GetOrdinal("AffectsInventory")) && reader.GetBoolean(reader.GetOrdinal("AffectsInventory"));
                                    setId = reader.IsDBNull(reader.GetOrdinal("SetId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("SetId"));
                                }
                            }
                        }

                        if (request == null)
                            return false;

                        // 2. Validate stock availability
                        if (currentStock < request.Quantity)
                        {
                            throw new InvalidOperationException(
                                $"Insufficient stock! Available: {currentStock}, Requested: {request.Quantity}");
                        }

                        // 3. Update Request status to "Submitted"
                        string updateRequestSql = @"
                            UPDATE dbo.Request 
                            SET Status = 'Submitted', 
                                DateModified = GETDATE(),
                                Modifiedby = @UserId
                            WHERE ReqId = @ReqId";

                        using (var cmd = new SqlCommand(updateRequestSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Create TRANSACTIONAL Inventory entry (the ledger record)
                        // CRITICAL: Only create TRANSACTIONAL Inventory when:
                        //   1. item affects inventory
                        //   2. SetId exists (transactional inventory requires Set association)
                        // If SetId is missing, skip inventory creation (Request exists without Set association)
                        if (affectsInventory && setId.HasValue && setId.Value > 0)
                        {
                            // Determine EntryType for TRANSACTIONAL inventory
                            string inventoryEntryType = Helpers.InventoryHelper.DetermineEntryType(affectsInventory, -request.Quantity);

                            string insertInventorySql = @"
                                INSERT INTO dbo.Inventory
                                    (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId, Active)
                                VALUES
                                    (@Description, @EntryType, @Quantity, GETDATE(), @UserId, @ReqId, @ItemId, @SetId, 1)";

                            using (var cmd = new SqlCommand(insertInventorySql, connection, transaction))
                            {
                                // Use description from request, or create default
                                string inventoryDescription = !string.IsNullOrWhiteSpace(request.Description)
                                    ? request.Description
                                    : $"{request.ItemName} - Request by {request.EmployeeName}";

                                cmd.Parameters.AddWithValue("@Description", inventoryDescription);
                                cmd.Parameters.AddWithValue("@EntryType", inventoryEntryType);
                                cmd.Parameters.AddWithValue("@Quantity", request.Quantity);
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                                cmd.Parameters.AddWithValue("@SetId", setId.Value);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // =====================================================
                        // CRITICAL FIX: DO NOT deduct stock during request save/update
                        // =====================================================
                        // Stock deduction moved to fulfillment (Set creation/deployment)
                        // Request save/update is inventory READ-ONLY
                        // =====================================================

                        // Step 5: REMOVED - Stock deduction now happens ONLY at fulfillment
                        // Previously: StockOnHand was decreased here (INCORRECT)
                        // Now: Stock is only decreased when Set is created and deployed
                        // See: SetRepository.ProcessPendingUpdatesAsync() line 160-172

                        // Step 6: CARTRIDGE REFILL LOGIC: If this is a cartridge replacement request,
                        // mark the old cartridge (being replaced) as "For Refill"
                        // Check if the item being requested is a cartridge
                        string checkCartridgeSql = @"
                            SELECT Category 
                            FROM dbo.Item 
                            WHERE ItemId = @ItemId";

                        string category = null;
                        using (var cmd = new SqlCommand(checkCartridgeSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                            var result = cmd.ExecuteScalar();
                            category = result?.ToString();
                        }

                        // If this is a cartridge request, find the old cartridge assigned to this employee
                        // and mark it as "For Refill" (assuming the old one is being returned)
                        if (category != null && category.Equals("Cartridge", StringComparison.OrdinalIgnoreCase))
                        {
                            // Find the most recent active cartridge assigned to this employee
                            // (via previous requests/sets) that is NOT the current request item
                            string findOldCartridgeSql = @"
                                SELECT TOP 1 i.ItemId
                                FROM dbo.Item i
                                INNER JOIN dbo.Request r ON i.ItemId = r.ItemId
                                WHERE r.EmpId = @EmpId
                                  AND i.Category = 'Cartridge'
                                  AND i.Active = 1
                                  AND i.ItemId != @CurrentItemId
                                  AND r.Status = 'Submitted'
                                ORDER BY r.DateModified DESC";

                            int? oldCartridgeId = null;
                            using (var cmd = new SqlCommand(findOldCartridgeSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EmpId", (object)request.EmpId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@CurrentItemId", request.ItemId);
                                var result = cmd.ExecuteScalar();
                                if (result != null && result != DBNull.Value)
                                {
                                    oldCartridgeId = Convert.ToInt32(result);
                                }
                            }

                            // =====================================================
                            // CRITICAL FIX: Do NOT mutate Item during cartridge return
                            // =====================================================
                            // REMOVED: Setting Item.Active = 0 and RefillStatus = 'For Refill'
                            //
                            // CARTRIDGE LIFECYCLE RULES:
                            // 1. Cartridge issued Ã¢â€ â€™ Stock deducted at fulfillment (SetRepository)
                            // 2. Cartridge returned Ã¢â€ â€™ INSERT to EmptyCartridge (NOT Item mutation)
                            // 3. Cartridge refilled Ã¢â€ â€™ Stock restored (CartridgeRefillRepository)
                            //
                            // Return flow is handled by FulfillCartridgeExchange which:
                            // - INSERTs into dbo.EmptyCartridge for WIP tracking
                            // - Does NOT touch Item.StockOnHand
                            // - Does NOT archive or deactivate Items
                            // - Returned cartridges are "in transit" until refill confirmed
                            // =====================================================

                            // LEGACY CODE REMOVED (was setting Active=0, RefillStatus='For Refill')
                            // if (oldCartridgeId.HasValue)
                            // {
                            //     UPDATE dbo.Item SET Active = 0, RefillStatus = 'For Refill' ...
                            // }
                            //
                            // This is no longer needed - return tracking now uses EmptyCartridge table
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all active employees for populating dropdowns.
        /// Returns employee ID, name, and position.
        /// </summary>
        public async Task<List<EmployeeDto>> GetAllEmployeesAsync()
        {
            var employees = new List<EmployeeDto>();

            const string sql = @"
                SELECT
                    e.EmpId,
                    e.Name,
                    e.Position,
                    e.EmployeeNumber
                FROM dbo.Employee e
                ORDER BY e.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        employees.Add(new EmployeeDto
                        {
                            EmpId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Position = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            EmployeeNumber = reader.IsDBNull(3) ? null : reader.GetString(3)
                        });
                    }
                }
            }

            return employees;
        }

        /// <summary>Employee list scoped to a Company/Branch/Department, matched by display name
        /// (as shown on ViewSetDetailPage's TxtCompany/TxtBranch/TxtDepartment) rather than ID,
        /// since the page only has the resolved names at hand when switching Dept-&gt;Employee mode.
        /// Pass null/blank for any dimension that shouldn't filter (e.g. Branch is optional).
        /// Fixes the "Employee dropdown shows everyone" bug — GetAllEmployeesAsync (above) is
        /// unfiltered and was being used for this picker regardless of the Set's current org unit.</summary>
        public async Task<List<EmployeeDto>> GetEmployeesByOrgNamesAsync(string companyName, string branchName, string deptName)
        {
            var employees = new List<EmployeeDto>();

            var sql = @"
                SELECT e.EmpId, e.Name, e.Position, e.EmployeeNumber
                FROM dbo.Employee e
                LEFT JOIN dbo.Company c ON c.ComId = e.ComId
                LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId
                LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
                WHERE (@CompanyName IS NULL OR c.Name = @CompanyName)"
                + (string.IsNullOrWhiteSpace(branchName) ? string.Empty : " AND b.Name = @BranchName")
                + (string.IsNullOrWhiteSpace(deptName) ? string.Empty : " AND d.Name = @DeptName")
                + " ORDER BY e.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CompanyName", string.IsNullOrWhiteSpace(companyName) ? (object)DBNull.Value : companyName.Trim());
                if (!string.IsNullOrWhiteSpace(branchName)) cmd.Parameters.AddWithValue("@BranchName", branchName.Trim());
                if (!string.IsNullOrWhiteSpace(deptName)) cmd.Parameters.AddWithValue("@DeptName", deptName.Trim());

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        employees.Add(new EmployeeDto
                        {
                            EmpId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Position = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            EmployeeNumber = reader.IsDBNull(3) ? null : reader.GetString(3)
                        });
                    }
                }
            }

            return employees;
        }

        /// <summary>
        /// Updates the status of all Hardware requests in a Set to "Submitted"
        /// Called after QR Code or PDF generation
        /// </summary>
        public async Task UpdateHardwareRequestsToSubmittedAsync(int setId)
        {
            const string sql = @"
                UPDATE r
                SET r.Status = 'Submitted',
                    r.DateModified = GETDATE(),
                    r.ModifiedBy = @ModifiedBy
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                WHERE r.SetId = @SetId
                AND i.ItemType = 'Hardware'
                AND r.Status != 'Submitted'";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    cmd.Parameters.AddWithValue("@ModifiedBy",
                        Session.AppSession.CurrentUserId > 0 ? Session.AppSession.CurrentUserId : 1); // Fallback to 1 if no user

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ── Partially Fulfilled / Unfulfilled Requests ──────────────────────────
        // Mirrors the Cartridge Management "Partially Fulfilled" / "Unfulfilled
        // Cartridge Exchanges" fulfillment-tracking pages, adapted for plain
        // dbo.Request rows. A Request row is already the finest-grained unit
        // (unlike cartridge requests, which needed a child table), so fulfillment
        // is tracked directly via Request.IssuedQty vs Request.Quantity.

        // Bucketing happens at the Set level, not per-row: if a Set has a mix of issued
        // and un-issued lines it counts as "Partially Fulfilled" as a whole (even the
        // still-zero rows show up there), and only a Set where NOTHING has been issued
        // anywhere counts as "Unfulfilled". A Set fully issued everywhere (or already
        // Dispatched) is fully Fulfilled and disappears from both. Requests with no
        // SetId are their own single-row group, so they behave the same as before.
        /// <summary>
        /// SET/INVOICE + REQUEST advanced-filter conditions are inlined into the Scoped CTE's own
        /// WHERE clause (before GroupAgg's bucket computation), matching the "filter narrows what's
        /// on screen" semantics used everywhere else — see ItemsPageViewModel.Queries.cs for the
        /// sibling pattern. Company/Branch/Department here refer to the SET's own fields
        /// (st.ComId/CurrentBranchId/CurrentDepartmentId), which are deliberately separate joins
        /// (co/setBr/setDept) from the existing e/b/d/rb/rd joins already used for the requester's
        /// own Employee/Branch/Department (displayed as EmployeeName/BranchName/DepartmentName).
        /// </summary>
        private static string BuildFulfillmentTrackedRequestsCte(List<string> setConditions)
        {
            string extraWhere = setConditions.Count > 0 ? " AND " + string.Join(" AND ", setConditions) : "";

            return @"
            ;WITH Scoped AS (
                SELECT
                    r.ReqId,
                    r.SetId,
                    r.SubmissionSessionId,
                    r.DateCreated,
                    r.Quantity,
                    r.IssuedQty,
                    r.Remarks,
                    r.Status,
                    r.ItemId,
                    i.Name AS ItemName,
                    e.Name AS EmployeeName,
                    COALESCE(b.Name, rb.Name) AS BranchName,
                    COALESCE(d.Name, rd.Name) AS DepartmentName,
                    st.Status AS SetStatus,
                    ISNULL(CAST(r.SetId AS VARCHAR(20)), 'R' + CAST(r.ReqId AS VARCHAR(20))) AS GroupKey
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT  JOIN dbo.Employee   e  ON e.EmpId      = r.EmpId
                LEFT  JOIN dbo.Branch     b  ON b.BranchId   = e.BranchId
                LEFT  JOIN dbo.Department d  ON d.DeptId     = e.DeptId
                LEFT  JOIN dbo.Branch     rb ON rb.BranchId  = r.BranchId
                LEFT  JOIN dbo.Department rd ON rd.DeptId    = r.DeptId
                LEFT  JOIN dbo.[Set]      st ON st.SetId      = r.SetId
                LEFT  JOIN dbo.Company    co ON co.ComId      = st.ComId
                LEFT  JOIN dbo.Branch     setBr ON setBr.BranchId = st.CurrentBranchId
                LEFT  JOIN dbo.Department setDept ON setDept.DeptId = st.CurrentDepartmentId
                WHERE r.Active = 1
                  -- Only Ink/Toner/Print Head consumables are in scope here — cartridges have
                  -- their own dedicated fulfillment portal under Cartridge Management, and no
                  -- other item category goes through the ""out of stock, come back later""
                  -- workflow this view exists for. Category is free-typed with no fixed
                  -- dropdown list, so match case/space-insensitively rather than by exact
                  -- string (mirrors the same normalization trick used elsewhere in this
                  -- codebase for matching the ""Cellphone"" category).
                  AND (
                        REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
                     OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
                     OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
                      )" + extraWhere + @"
            ),
            GroupAgg AS (
                SELECT
                    GroupKey,
                    MAX(CASE WHEN IssuedQty > 0 THEN 1 ELSE 0 END) AS AnyIssued,
                    -- A Set that has already gone out the door fulfills every Request in it,
                    -- regardless of what IssuedQty says (handles historical data recorded
                    -- before IssuedQty existed, and any dispatch path that doesn't sync it).
                    MIN(CASE WHEN IssuedQty >= Quantity OR SetStatus = 'Dispatched' THEN 1 ELSE 0 END) AS AllFulfilled
                FROM Scoped
                GROUP BY GroupKey
            )";
        }

        private List<Pages.RequestDto> ReadFulfillmentTrackedRequests(
            string bucketWhereClause,
            string setCodeFilter = null,
            string documentNumberFilter = null,
            string companyFilter = null,
            string departmentFilter = null,
            string branchFilter = null,
            string employeeFilter = null,
            string referenceCodeFilter = null,
            string parentTagFilter = null,
            string reqIdFilter = null)
        {
            var requests = new List<Pages.RequestDto>();

            var setConditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(setCodeFilter)) setConditions.Add("st.SetCode LIKE @SetCode");
            if (!string.IsNullOrWhiteSpace(documentNumberFilter)) setConditions.Add("st.DocumentNumber LIKE @DocumentNumber");
            if (!string.IsNullOrWhiteSpace(companyFilter)) setConditions.Add("co.Name LIKE @Company");
            if (!string.IsNullOrWhiteSpace(departmentFilter)) setConditions.Add("setDept.Name LIKE @Department");
            if (!string.IsNullOrWhiteSpace(branchFilter)) setConditions.Add("setBr.Name LIKE @Branch");
            if (!string.IsNullOrWhiteSpace(employeeFilter)) setConditions.Add("e.Name LIKE @Employee");
            if (!string.IsNullOrWhiteSpace(reqIdFilter)) setConditions.Add("CAST(r.ReqId AS NVARCHAR(20)) LIKE @ReqId");

            var setItemConditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(referenceCodeFilter)) setItemConditions.Add("si2.ReferenceCode LIKE @ReferenceCode");
            if (!string.IsNullOrWhiteSpace(parentTagFilter)) setItemConditions.Add("ptg2.Label LIKE @ParentTag");
            if (setItemConditions.Count > 0)
            {
                setConditions.Add($@"EXISTS (
                    SELECT 1 FROM dbo.SetItem si2
                    LEFT JOIN dbo.SetItemParentTagGroup ptg2 ON ptg2.ParentTagGroupId = si2.ParentTagGroupId
                    WHERE si2.SetId = r.SetId AND {string.Join(" AND ", setItemConditions)}
                )");
            }

            string sql = BuildFulfillmentTrackedRequestsCte(setConditions) + @"
                SELECT s.*
                FROM Scoped s
                INNER JOIN GroupAgg g ON g.GroupKey = s.GroupKey
                WHERE g.AllFulfilled = 0
                  AND " + bucketWhereClause + @"
                ORDER BY s.SetId, s.ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                if (!string.IsNullOrWhiteSpace(setCodeFilter))
                    cmd.Parameters.AddWithValue("@SetCode", $"%{setCodeFilter}%");
                if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                    cmd.Parameters.AddWithValue("@DocumentNumber", $"%{documentNumberFilter}%");
                if (!string.IsNullOrWhiteSpace(companyFilter))
                    cmd.Parameters.AddWithValue("@Company", $"%{companyFilter}%");
                if (!string.IsNullOrWhiteSpace(departmentFilter))
                    cmd.Parameters.AddWithValue("@Department", $"%{departmentFilter}%");
                if (!string.IsNullOrWhiteSpace(branchFilter))
                    cmd.Parameters.AddWithValue("@Branch", $"%{branchFilter}%");
                if (!string.IsNullOrWhiteSpace(employeeFilter))
                    cmd.Parameters.AddWithValue("@Employee", $"%{employeeFilter}%");
                if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                    cmd.Parameters.AddWithValue("@ReferenceCode", $"%{referenceCodeFilter}%");
                if (!string.IsNullOrWhiteSpace(parentTagFilter))
                    cmd.Parameters.AddWithValue("@ParentTag", $"%{parentTagFilter}%");
                if (!string.IsNullOrWhiteSpace(reqIdFilter))
                    cmd.Parameters.AddWithValue("@ReqId", $"%{reqIdFilter}%");

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        requests.Add(new Pages.RequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            SetId = reader.IsDBNull(reader.GetOrdinal("SetId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("SetId")),
                            SubmissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId")),
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            IssuedQty = reader.GetInt32(reader.GetOrdinal("IssuedQty")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName"))
                        });
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Every Request belonging to a Set where at least one line has something issued
        /// and at least one line still doesn't — i.e. the Set as a whole is in progress.
        /// </summary>
        public List<Pages.RequestDto> GetPartiallyFulfilledRequestsFullSet(
            string setCodeFilter = null,
            string documentNumberFilter = null,
            string companyFilter = null,
            string departmentFilter = null,
            string branchFilter = null,
            string employeeFilter = null,
            string referenceCodeFilter = null,
            string parentTagFilter = null,
            string reqIdFilter = null)
            => ReadFulfillmentTrackedRequests("g.AnyIssued = 1",
                setCodeFilter, documentNumberFilter, companyFilter, departmentFilter,
                branchFilter, employeeFilter, referenceCodeFilter, parentTagFilter, reqIdFilter);

        /// <summary>
        /// Every Request belonging to a Set where NOTHING has been issued anywhere yet.
        /// </summary>
        public List<Pages.RequestDto> GetUnfulfilledRequestsFullSet(
            string setCodeFilter = null,
            string documentNumberFilter = null,
            string companyFilter = null,
            string departmentFilter = null,
            string branchFilter = null,
            string employeeFilter = null,
            string referenceCodeFilter = null,
            string parentTagFilter = null,
            string reqIdFilter = null)
            => ReadFulfillmentTrackedRequests("g.AnyIssued = 0",
                setCodeFilter, documentNumberFilter, companyFilter, departmentFilter,
                branchFilter, employeeFilter, referenceCodeFilter, parentTagFilter, reqIdFilter);

        /// <summary>
        /// Records additional issued quantity against a Request. Caps at the requested
        /// Quantity — callers should clamp the amount to the remaining pending qty first.
        /// </summary>
        public void FulfillRequest(int reqId, int additionalIssuedQty, int modifiedByUserId, string remarks)
        {
            const string sql = @"
                UPDATE dbo.Request
                SET IssuedQty     = CASE
                                        WHEN IssuedQty + @AdditionalIssuedQty > Quantity THEN Quantity
                                        ELSE IssuedQty + @AdditionalIssuedQty
                                     END,
                    Remarks       = ISNULL(@Remarks, Remarks),
                    DateModified  = (SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time'),
                    ModifiedBy    = @ModifiedBy
                WHERE ReqId = @ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                cmd.Parameters.AddWithValue("@AdditionalIssuedQty", additionalIssuedQty);
                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            int itemId = 0;
            string serial = null;
            int issuedTotal = 0;
            int requestedQty = 0;
            string itemName = null;
            int? requesterUserId = null;

            try
            {
                using (var lookup = new SqlConnection(GetConnectionString()))
                using (var lookupCmd = new SqlCommand(@"
                    SELECT r.ItemId, i.SerialNumber, r.IssuedQty, r.Quantity, i.Name, r.CreatedBy
                    FROM dbo.Request r
                    LEFT JOIN dbo.Item i ON i.ItemId = r.ItemId
                    WHERE r.ReqId = @ReqId", lookup))
                {
                    lookupCmd.Parameters.AddWithValue("@ReqId", reqId);
                    lookup.Open();
                    using (var reader = lookupCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            itemId = Convert.ToInt32(reader[0]);
                            serial = reader[1] == DBNull.Value ? null : Convert.ToString(reader[1]);
                            issuedTotal = Convert.ToInt32(reader[2]);
                            requestedQty = Convert.ToInt32(reader[3]);
                            itemName = reader[4] == DBNull.Value ? null : Convert.ToString(reader[4]);
                            requesterUserId = reader[5] == DBNull.Value ? (int?)null : Convert.ToInt32(reader[5]);
                        }
                    }
                }
                if (itemId > 0)
                {
                    new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
                    {
                        ItemId = itemId,
                        SerialNumber = serial,
                        Action = "Request Fulfilled",
                        ActionTime = DateTime.Now,
                        Direction = "OUT",
                        Status = "Completed",
                        ReferenceType = "Request",
                        ReferenceId = reqId,
                        Notes = $"Issued {additionalIssuedQty} more unit(s) for Request #{reqId} (total issued {issuedTotal}).",
                        CreatedBy = AppSession.CurrentUserName ?? "System"
                    }).GetAwaiter().GetResult();
                }
            }
            catch { /* best-effort: audit must not break fulfillment */ }

            try
            {
                if (requesterUserId.HasValue && requesterUserId.Value > 0)
                {
                    string item = string.IsNullOrWhiteSpace(itemName) ? "your item" : $"\"{itemName}\"";
                    string notifType, notifTitle, notifMsg;
                    if (issuedTotal <= 0)
                    {
                        notifType  = NotificationType.RequestUnfulfilled;
                        notifTitle = "Request Unfulfilled";
                        notifMsg   = $"Your request for {item} (Req #{reqId}) could not be fulfilled due to insufficient stock.";
                    }
                    else if (issuedTotal < requestedQty)
                    {
                        notifType  = NotificationType.RequestPartiallyFulfilled;
                        notifTitle = "Request Partially Fulfilled";
                        notifMsg   = $"Your request for {item} (Req #{reqId}) was partially fulfilled: {issuedTotal} of {requestedQty} issued.";
                    }
                    else
                    {
                        notifType  = NotificationType.RequestFulfilled;
                        notifTitle = "Request Fulfilled";
                        notifMsg   = $"Your request for {item} (Req #{reqId}) has been fulfilled in full.";
                    }

                    // NOTE: ReferenceId is intentionally left null. RequesterPortalForm.NavigateToRequestDetail
                    // treats these notification types' ReferenceId as a CartridgeAuthorization.AuthorizationId
                    // (the cartridge-exchange flow's fulfillment notifications use it that way) — this
                    // plain item-borrow Request has no such row, so reusing ReqId here would misroute the click.
                    new NotificationRepository().Create(new NotificationCreateDto
                    {
                        UserId           = requesterUserId.Value,
                        Title            = notifTitle,
                        Message          = notifMsg,
                        NotificationType = notifType,
                        ActorUserId      = modifiedByUserId
                    });
                }
            }
            catch { /* notification is non-critical */ }

            ActivityLogger.Log(modifiedByUserId, ActivityLogger.Actions.Update,
                "Request", reqId, $"Issued {additionalIssuedQty} more unit(s) for Request #{reqId}");
        }

        /// <summary>
        /// Current stock on hand for an item, used to cap the fulfill dialog's issuable qty.
        /// </summary>
        public int GetItemStockOnHand(int itemId)
        {
            const string sql = "SELECT ISNULL(StockOnHand, 0) FROM dbo.Item WHERE ItemId = @ItemId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                con.Open();
                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }
    }
}
