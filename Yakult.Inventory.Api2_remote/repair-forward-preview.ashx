<%@ WebHandler Language="C#" Class="RepairForwardPreviewHandler" %>
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>Builds (without persisting) the same prefilled Repair Portal intake data used by the
/// desktop IT CALL forwarding service. The mobile client must show this response for review before
/// calling repair-forward-create.ashx.</summary>
public sealed class RepairForwardPreviewHandler : IHttpHandler
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
        if (useUnlisted && string.IsNullOrWhiteSpace(RepairMobileApiSupport.Text(request, "unlistedOldItemName", 200)))
        {
            RepairMobileApiSupport.Error(context, 400, "unlistedOldItemName is required");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                CallTicketInfo call;
                if (!TryReadCallTicket(connection, callTicketId.Value, out call))
                {
                    RepairMobileApiSupport.Error(context, 404, "IT Call ticket not found");
                    return;
                }
                object repairItem;
                if (useUnlisted)
                {
                    repairItem = new
                    {
                        itemId = (int?)null,
                        name = RepairMobileApiSupport.Text(request, "unlistedOldItemName", 200),
                        modelNumber = RepairMobileApiSupport.Text(request, "unlistedOldItemModelNumber", 200),
                        serialNumber = RepairMobileApiSupport.Text(request, "unlistedOldItemSerialNumber", 255),
                        category = RepairMobileApiSupport.Text(request, "unlistedOldItemCategoryName", 200),
                        isUnlisted = true
                    };
                }
                else
                {
                    repairItem = ReadItem(connection, repairItemId.Value);
                    if (repairItem == null)
                    {
                        RepairMobileApiSupport.Error(context, 404, "The selected repair item is unavailable");
                        return;
                    }
                }

                var caller = ResolveUniqueCaller(connection, call.CallerName);
                var requestedByCompanyId = caller != null && caller.CompanyId.HasValue ? caller.CompanyId : call.CompanyId;
                var requestedByBranchId = caller != null && caller.DepartmentId.HasValue ? caller.BranchId : call.BranchId;
                var requestedByDepartmentId = caller != null && caller.DepartmentId.HasValue ? caller.DepartmentId : call.DepartmentId;
                var hasOrg = requestedByCompanyId.HasValue && requestedByDepartmentId.HasValue;
                var requestedByType = caller != null && caller.EmployeeId > 0 && hasOrg ? "Employee" : (hasOrg ? "Department" : null);

                var existing = ReadExistingLink(connection, callTicketId.Value);
                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    existingRepairTicket = existing,
                    preview = new
                    {
                        callTicketId = call.TicketId,
                        callTicketCode = call.TicketCode,
                        repairItem = repairItem,
                        problem = BuildProblem(call, resolutionType, remarks),
                        priority = NormalizePriority(call.Priority),
                        submittedByEmployeeId = actor.EmployeeId,
                        requestedByType = requestedByType,
                        requestedByEmployeeId = requestedByType == "Employee" ? (int?)caller.EmployeeId : null,
                        requestedByDepartmentId = hasOrg ? requestedByDepartmentId : null,
                        requestedByCompanyId = requestedByCompanyId,
                        requestedByBranchId = requestedByBranchId,
                        forwardedAfter = resolutionType,
                        resolutionRemarks = remarks
                    }
                });
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair forwarding preview is temporarily unavailable");
        }
    }

    private sealed class CallTicketInfo
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

    private static bool TryReadCallTicket(SqlConnection connection, int ticketId, out CallTicketInfo call)
    {
        call = null;
        const string sql = @"SELECT TicketId, TicketCode, CallerName, Issue, Priority, ComId, BranchId, DeptId
FROM dbo.CallTicket WHERE TicketId=@TicketId;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return false;
                call = new CallTicketInfo
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

    private static CallerInfo ResolveUniqueCaller(SqlConnection connection, string callerName)
    {
        if (string.IsNullOrWhiteSpace(callerName) || callerName.Trim().Length < 2) return null;
        const string countSql = @"SELECT COUNT(1) FROM dbo.Employee
WHERE ISNULL(Active,1)=1 AND LOWER(LTRIM(RTRIM(Name)))=LOWER(LTRIM(RTRIM(@Name)));";
        using (var count = new SqlCommand(countSql, connection))
        {
            count.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = callerName.Trim();
            if (Convert.ToInt32(count.ExecuteScalar()) != 1) return null;
        }
        const string readSql = @"SELECT EmpId, ComId, BranchId, DeptId FROM dbo.Employee
WHERE ISNULL(Active,1)=1 AND LOWER(LTRIM(RTRIM(Name)))=LOWER(LTRIM(RTRIM(@Name)));";
        using (var command = new SqlCommand(readSql, connection))
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

    private static object ReadItem(SqlConnection connection, int itemId)
    {
        const string sql = @"SELECT ItemId, Name, ModelNumber, SerialNumber, Category FROM dbo.Item
WHERE ItemId=@ItemId AND ISNULL(Active,1)=1;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new
                {
                    itemId = Convert.ToInt32(reader["ItemId"]),
                    name = RepairMobileApiSupport.StringValue(reader, "Name"),
                    modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                    serialNumber = RepairMobileApiSupport.StringValue(reader, "SerialNumber"),
                    category = RepairMobileApiSupport.StringValue(reader, "Category"),
                    isUnlisted = false
                };
            }
        }
    }

    private static object ReadExistingLink(SqlConnection connection, int callTicketId)
    {
        if (!RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "CallTicketId")) return null;
        using (var command = new SqlCommand("SELECT TOP (1) RepairTicketId, TicketCode, ItemId, Status FROM dbo.RepairTicket WHERE CallTicketId=@CallTicketId;", connection))
        {
            command.Parameters.Add("@CallTicketId", SqlDbType.Int).Value = callTicketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new
                {
                    repairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                    ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                    itemId = Convert.ToInt32(reader["ItemId"]),
                    status = RepairMobileApiSupport.StringValue(reader, "Status")
                };
            }
        }
    }

    private static string BuildProblem(CallTicketInfo call, string resolutionType, string remarks)
    {
        var code = string.IsNullOrWhiteSpace(call.TicketCode) ? "IT Call #" + call.TicketId : "IT Call " + call.TicketCode.Trim();
        var problem = code + ": " + (call.Issue ?? string.Empty).Trim() + "\r\n\r\nForwarded after " + resolutionType + ".";
        if (!string.IsNullOrWhiteSpace(remarks)) problem += "\r\nITCM remarks: " + remarks.Trim();
        return problem.Length <= 2000 ? problem : problem.Substring(0, 2000);
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
