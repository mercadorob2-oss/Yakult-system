<%@ WebHandler Language="C#" Class="RepairForwardCreateHandler" %>
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>
/// Explicitly creates (or retrieves) the one Repair Portal ticket permanently linked to an IT CALL.
/// It intentionally does not change the parent IT CALL status or apply replacement inventory: the
/// caller must review this result and subsequently choose the parent outcome through the standard
/// resolution action. This mirrors the desktop workflow and keeps retries idempotent.
/// </summary>
public sealed class RepairForwardCreateHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "POST, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireTechnician(context, out actor)) return;
        JObject request;
        if (!RepairMobileApiSupport.TryReadObject(context, out request))
        {
            RepairMobileApiSupport.Error(context, 400, "A valid JSON request body is required");
            return;
        }

        var callTicketId = RepairMobileApiSupport.Int(request, "ticketId");
        var resolutionType = RepairMobileApiSupport.Text(request, "resolutionType", 30);
        var remarks = RepairMobileApiSupport.Text(request, "remarks", 2000);
        var repairItemId = RepairMobileApiSupport.Int(request, "repairItemId");
        var oldItemId = RepairMobileApiSupport.Int(request, "oldItemId");
        var useUnlisted = RepairMobileApiSupport.Bool(request, "useUnlistedOldItem", false);
        if (!callTicketId.HasValue || callTicketId.Value <= 0 ||
            (resolutionType != "Service Only" && resolutionType != "Replacement"))
        {
            RepairMobileApiSupport.Error(context, 400, "ticketId and a valid resolutionType are required");
            return;
        }
        if (!useUnlisted && (!repairItemId.HasValue || repairItemId.Value <= 0))
        {
            RepairMobileApiSupport.Error(context, 400, "repairItemId is required");
            return;
        }
        if (resolutionType == "Replacement" && !useUnlisted &&
            (!oldItemId.HasValue || oldItemId.Value != repairItemId.Value))
        {
            RepairMobileApiSupport.Error(context, 400, "For a replacement, the forwarded repair item must be the selected old item");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "CallTicketId") ||
                    !RepairMobileApiSupport.HasProcedure(connection, "dbo.sp_RepairPortal_CreateTicketFromCallTicket"))
                {
                    RepairMobileApiSupport.Error(context, 503, "IT CALL repair forwarding is not installed for this environment");
                    return;
                }

                ForwardCall call;
                if (!TryReadCall(connection, callTicketId.Value, out call))
                {
                    RepairMobileApiSupport.Error(context, 404, "IT Call ticket not found");
                    return;
                }

                // The link procedure is idempotent. Returning the existing ticket before creating an
                // unlisted old item prevents a retry from accumulating duplicate inventory records.
                ForwardResult existing;
                if (TryReadExistingLink(connection, callTicketId.Value, null, out existing))
                {
                    RepairMobileApiSupport.Ok(context, new { success = true, created = false, ticket = ToPayload(existing), oldItemId = existing.ItemId });
                    return;
                }

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Serialize all mobile forward attempts for this parent before an unlisted
                        // item can be created. The link procedure has the same parent-row lock;
                        // taking it here prevents a losing concurrent request from committing an
                        // otherwise orphaned unlisted inventory record.
                        using (var parentLock = new SqlCommand("SELECT TicketId FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK) WHERE TicketId=@TicketId;", connection, transaction))
                        {
                            parentLock.Parameters.Add("@TicketId", SqlDbType.Int).Value = callTicketId.Value;
                            if (parentLock.ExecuteScalar() == null) throw new InvalidOperationException("IT Call ticket not found");
                        }
                        ForwardResult lockedExisting;
                        if (TryReadExistingLink(connection, callTicketId.Value, transaction, out lockedExisting))
                        {
                            transaction.Commit();
                            RepairMobileApiSupport.Ok(context, new { success = true, created = false, ticket = ToPayload(lockedExisting), oldItemId = lockedExisting.ItemId });
                            return;
                        }

                        var effectiveItemId = repairItemId;
                        if (useUnlisted)
                            effectiveItemId = CreateUnlistedOldItem(connection, transaction, request, actor);
                        if (!effectiveItemId.HasValue || effectiveItemId.Value <= 0)
                            throw new InvalidOperationException("No repair item is available");

                        if (!ItemExists(connection, transaction, effectiveItemId.Value))
                        {
                            transaction.Rollback();
                            RepairMobileApiSupport.Error(context, 404, "The selected repair item is unavailable");
                            return;
                        }

                        var caller = ResolveUniqueCaller(connection, transaction, call.CallerName);
                        var requestedCompanyId = caller != null && caller.CompanyId.HasValue ? caller.CompanyId : call.CompanyId;
                        var requestedBranchId = caller != null && caller.BranchId.HasValue ? caller.BranchId : call.BranchId;
                        var requestedDepartmentId = caller != null && caller.DepartmentId.HasValue ? caller.DepartmentId : call.DepartmentId;
                        var hasRequesterOrganization = requestedCompanyId.HasValue && requestedDepartmentId.HasValue;
                        var requestedByType = caller != null && caller.EmployeeId > 0 && hasRequesterOrganization
                            ? "Employee" : (hasRequesterOrganization ? "Department" : null);

                        ForwardResult result;
                        using (var command = new SqlCommand("dbo.sp_RepairPortal_CreateTicketFromCallTicket", connection, transaction))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add("@CallTicketId", SqlDbType.Int).Value = callTicketId.Value;
                            command.Parameters.Add("@ItemId", SqlDbType.Int).Value = effectiveItemId.Value;
                            command.Parameters.Add("@Problem", SqlDbType.NVarChar, 2000).Value = BuildProblem(call, resolutionType, remarks);
                            command.Parameters.Add("@Priority", SqlDbType.NVarChar, 20).Value = NormalizePriority(call.Priority);
                            command.Parameters.Add("@SubmittedByEmpId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
                            command.Parameters.Add("@SubmittedByUserId", SqlDbType.Int).Value = actor.UserId;
                            command.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = actor.UserId;
                            command.Parameters.Add("@RequestedByType", SqlDbType.NVarChar, 20).Value = RepairMobileApiSupport.DbValue(requestedByType);
                            command.Parameters.Add("@RequestedByDeptId", SqlDbType.Int).Value = requestedDepartmentId.HasValue ? (object)requestedDepartmentId.Value : DBNull.Value;
                            command.Parameters.Add("@RequestedByEmpId", SqlDbType.Int).Value = requestedByType == "Employee" ? (object)caller.EmployeeId : DBNull.Value;
                            command.Parameters.Add("@DateReceived", SqlDbType.DateTime2).Value = DBNull.Value;
                            command.Parameters.Add("@RequestedByComId", SqlDbType.Int).Value = requestedCompanyId.HasValue ? (object)requestedCompanyId.Value : DBNull.Value;
                            command.Parameters.Add("@RequestedByBranchId", SqlDbType.Int).Value = requestedBranchId.HasValue ? (object)requestedBranchId.Value : DBNull.Value;
                            using (var reader = command.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("Repair ticket was not returned");
                                result = new ForwardResult
                                {
                                    RepairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                                    TicketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                                    ItemId = effectiveItemId.Value,
                                    Status = RepairMobileApiSupport.StringValue(reader, "Status"),
                                    Priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                                    CreatedAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"])
                                };
                            }
                        }
                        transaction.Commit();
                        RepairMobileApiSupport.Created(context, new
                        {
                            success = true,
                            created = true,
                            ticket = ToPayload(result),
                            oldItemId = effectiveItemId,
                            parentOutcomeRequired = true,
                            message = "Repair ticket created. Choose the parent IT Call outcome to complete the forwarding workflow."
                        });
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        RepairMobileApiSupport.Error(context, 503, "Repair ticket forwarding could not be completed");
                    }
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair ticket forwarding could not be completed");
        }
    }

    private sealed class ForwardCall
    {
        public int TicketId;
        public string TicketCode;
        public string CallerName;
        public string Issue;
        public string Priority;
        public int? CompanyId;
        public int? BranchId;
        public int? DepartmentId;
    }

    private sealed class CallerInfo
    {
        public int EmployeeId;
        public int? CompanyId;
        public int? BranchId;
        public int? DepartmentId;
    }

    private sealed class ForwardResult
    {
        public int RepairTicketId;
        public string TicketCode;
        public int ItemId;
        public string Status;
        public string Priority;
        public string CreatedAt;
    }

    private static object ToPayload(ForwardResult result)
    {
        return result == null ? null : new
        {
            repairTicketId = result.RepairTicketId,
            ticketCode = result.TicketCode,
            itemId = result.ItemId,
            status = result.Status,
            priority = result.Priority,
            createdAt = result.CreatedAt
        };
    }

    private static bool TryReadCall(SqlConnection connection, int ticketId, out ForwardCall call)
    {
        call = null;
        const string sql = @"SELECT TicketId, TicketCode, CallerName, Issue, Priority, ComId, BranchId, DeptId FROM dbo.CallTicket WHERE TicketId=@TicketId;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return false;
                call = new ForwardCall
                {
                    TicketId = Convert.ToInt32(reader["TicketId"]),
                    TicketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                    CallerName = RepairMobileApiSupport.StringValue(reader, "CallerName"),
                    Issue = RepairMobileApiSupport.StringValue(reader, "Issue"),
                    Priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                    CompanyId = RepairMobileApiSupport.IntValue(reader, "ComId"),
                    BranchId = RepairMobileApiSupport.IntValue(reader, "BranchId"),
                    DepartmentId = RepairMobileApiSupport.IntValue(reader, "DeptId")
                };
                return true;
            }
        }
    }

    private static bool TryReadExistingLink(SqlConnection connection, int callTicketId, SqlTransaction transaction, out ForwardResult result)
    {
        result = null;
        const string sql = @"SELECT TOP (1) RepairTicketId, TicketCode, ItemId, Status, Priority, CreatedAt
FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK) WHERE CallTicketId=@CallTicketId;";
        using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@CallTicketId", SqlDbType.Int).Value = callTicketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return false;
                result = new ForwardResult
                {
                    RepairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                    TicketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                    ItemId = Convert.ToInt32(reader["ItemId"]),
                    Status = RepairMobileApiSupport.StringValue(reader, "Status"),
                    Priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                    CreatedAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"])
                };
                return true;
            }
        }
    }

    private static int? CreateUnlistedOldItem(SqlConnection connection, SqlTransaction transaction, JObject request, CallTicketApiUser actor)
    {
        var name = RepairMobileApiSupport.Text(request, "unlistedOldItemName", 200);
        var model = RepairMobileApiSupport.Text(request, "unlistedOldItemModelNumber", 200);
        var serial = RepairMobileApiSupport.Text(request, "unlistedOldItemSerialNumber", 255);
        var unit = RepairMobileApiSupport.Text(request, "unlistedOldItemUnitOfMeasure", 50) ?? "Unit";
        var description = RepairMobileApiSupport.Text(request, "unlistedOldItemDescription", 2000);
        var categoryId = RepairMobileApiSupport.Int(request, "unlistedOldItemCategoryId");
        var categoryName = RepairMobileApiSupport.Text(request, "unlistedOldItemCategoryName", 200);
        var conditionId = RepairMobileApiSupport.Int(request, "oldItemConditionId");
        var conditionRemarks = RepairMobileApiSupport.Text(request, "oldItemConditionRemarks", 2000);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(model) || !categoryId.HasValue || !conditionId.HasValue)
            throw new InvalidOperationException("The unlisted item is incomplete");

        const string sql = @"
INSERT INTO dbo.Item
    (Name, Description, CategoryId, Category, SerialNumber, ModelNumber, UnitOfMeasure,
     ItemType, StockOnHand, Active, DateCreated, CreatedBy, DateModified, ModifiedBy,
     ConditionID, Remarks, AffectsInventory, IsTrackedAsset)
VALUES
    (@Name, @Description, @CategoryId, @CategoryName, @SerialNumber, @ModelNumber, @UnitOfMeasure,
     'Hardware', 0, 1, SYSUTCDATETIME(), @UserId, SYSUTCDATETIME(), @UserId,
     @ConditionId, @ConditionRemarks, 1, 0);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = name;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(description);
            command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId.Value;
            command.Parameters.Add("@CategoryName", SqlDbType.NVarChar, 200).Value = RepairMobileApiSupport.DbValue(categoryName);
            command.Parameters.Add("@SerialNumber", SqlDbType.VarChar, 255).Value = RepairMobileApiSupport.DbValue(serial);
            command.Parameters.Add("@ModelNumber", SqlDbType.NVarChar, 200).Value = model;
            command.Parameters.Add("@UnitOfMeasure", SqlDbType.NVarChar, 50).Value = unit;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@ConditionId", SqlDbType.Int).Value = conditionId.Value;
            command.Parameters.Add("@ConditionRemarks", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(conditionRemarks);
            var value = command.ExecuteScalar();
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }
    }

    private static bool ItemExists(SqlConnection connection, SqlTransaction transaction, int itemId)
    {
        using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.Item WHERE ItemId=@ItemId AND ISNULL(Active,1)=1;", connection, transaction))
        {
            command.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId;
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
    }

    private static CallerInfo ResolveUniqueCaller(SqlConnection connection, SqlTransaction transaction, string callerName)
    {
        if (string.IsNullOrWhiteSpace(callerName) || callerName.Trim().Length < 2) return null;
        using (var count = new SqlCommand("SELECT COUNT(1) FROM dbo.Employee WHERE ISNULL(Active,1)=1 AND LOWER(LTRIM(RTRIM(Name)))=LOWER(LTRIM(RTRIM(@Name)));", connection, transaction))
        {
            count.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = callerName.Trim();
            if (Convert.ToInt32(count.ExecuteScalar()) != 1) return null;
        }
        using (var command = new SqlCommand("SELECT EmpId, ComId, BranchId, DeptId FROM dbo.Employee WHERE ISNULL(Active,1)=1 AND LOWER(LTRIM(RTRIM(Name)))=LOWER(LTRIM(RTRIM(@Name)));", connection, transaction))
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = callerName.Trim();
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new CallerInfo
                {
                    EmployeeId = Convert.ToInt32(reader["EmpId"]),
                    CompanyId = RepairMobileApiSupport.IntValue(reader, "ComId"),
                    BranchId = RepairMobileApiSupport.IntValue(reader, "BranchId"),
                    DepartmentId = RepairMobileApiSupport.IntValue(reader, "DeptId")
                };
            }
        }
    }

    private static string BuildProblem(ForwardCall call, string resolutionType, string remarks)
    {
        var code = string.IsNullOrWhiteSpace(call.TicketCode) ? "IT Call #" + call.TicketId : "IT Call " + call.TicketCode.Trim();
        var result = code + ": " + (call.Issue ?? string.Empty).Trim() + "\r\n\r\nForwarded after " + resolutionType + ".";
        if (!string.IsNullOrWhiteSpace(remarks)) result += "\r\nITCM remarks: " + remarks.Trim();
        return result.Length <= 2000 ? result : result.Substring(0, 2000);
    }

    private static string NormalizePriority(string priority)
    {
        switch ((priority ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "LOW": return "Low";
            case "HIGH": return "High";
            case "CRITICAL": return "Critical";
            default: return "Medium";
        }
    }

    public bool IsReusable { get { return false; } }
}
