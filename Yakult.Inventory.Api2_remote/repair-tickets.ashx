<%@ WebHandler Language="C#" Class="RepairTicketsHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>
/// Mobile Repair Portal ticket collection.
/// GET returns requester-owned tickets (scope=mine) or the IT-authorized technician queue
/// (scope=technician). POST creates a shared dbo.RepairTicket record through the same stored
/// procedure used by the desktop portal; actor identity is always derived from the bearer token.
/// </summary>
public sealed class RepairTicketsHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "GET, POST, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;

        if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            List(context);
            return;
        }

        if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            Create(context);
            return;
        }

        RepairMobileApiSupport.Error(context, 405, "Method not allowed");
    }

    private static void List(HttpContext context)
    {
        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;

        var scope = (context.Request.QueryString["scope"] ?? "mine").Trim().ToLowerInvariant();
        if (scope != "mine" && scope != "technician")
        {
            RepairMobileApiSupport.Error(context, 400, "scope must be mine or technician");
            return;
        }
        if (scope == "technician" && !actor.IsItAuthorized)
        {
            RepairMobileApiSupport.Error(context, 403, "Your account is not authorized for the technician repair queue");
            return;
        }

        var page = RepairMobileApiSupport.ClampPage(context.Request.QueryString["page"]);
        var pageSize = RepairMobileApiSupport.ClampPageSize(context.Request.QueryString["pageSize"], 25, 100);
        var status = (context.Request.QueryString["status"] ?? string.Empty).Trim();
        var priority = (context.Request.QueryString["priority"] ?? string.Empty).Trim();
        var search = (context.Request.QueryString["search"] ?? string.Empty).Trim();
        if (status.Length > 30 || priority.Length > 30 || search.Length > 200)
        {
            RepairMobileApiSupport.Error(context, 400, "One or more filters are too long");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                var hasCallLink = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "CallTicketId");
                var callJoin = hasCallLink
                    ? "LEFT JOIN dbo.CallTicket linkedCall ON linkedCall.TicketId = t.CallTicketId"
                    : string.Empty;
                var linkSelect = hasCallLink
                    ? "t.CallTicketId AS LinkedCallTicketId, linkedCall.TicketCode AS LinkedCallTicketCode, linkedCall.Status AS LinkedCallStatus"
                    : "CAST(NULL AS INT) AS LinkedCallTicketId, CAST(NULL AS NVARCHAR(50)) AS LinkedCallTicketCode, CAST(NULL AS NVARCHAR(30)) AS LinkedCallStatus";

                var where = new List<string> { "1 = 1" };
                var parameters = new List<SqlParameter>();
                if (scope == "mine")
                {
                    where.Add("(t.SubmittedByUserId = @UserId OR (@EmployeeId IS NOT NULL AND t.RequestedByEmpId = @EmployeeId))");
                    parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = actor.UserId });
                    parameters.Add(new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value });
                }
                if (!string.IsNullOrWhiteSpace(status))
                {
                    where.Add("t.Status = @Status");
                    parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 30) { Value = status });
                }
                if (!string.IsNullOrWhiteSpace(priority))
                {
                    where.Add("t.Priority = @Priority");
                    parameters.Add(new SqlParameter("@Priority", SqlDbType.NVarChar, 30) { Value = priority });
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    where.Add("(t.TicketCode LIKE @Search OR t.ItemNameSnapshot LIKE @Search OR t.ItemSerialSnapshot LIKE @Search OR t.Problem LIKE @Search)");
                    parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 450) { Value = "%" + search + "%" });
                }

                var whereSql = string.Join(" AND ", where.ToArray());
                var countSql = "SELECT COUNT(1) FROM dbo.RepairTicket t WHERE " + whereSql + ";";
                var totalCount = 0;
                using (var count = new SqlCommand(countSql, connection))
                {
                    count.Parameters.AddRange(CloneParameters(parameters));
                    totalCount = Convert.ToInt32(count.ExecuteScalar());
                }

                var listSql = @"
SELECT
    t.RepairTicketId, t.TicketCode, t.ItemId, t.ItemNameSnapshot, t.ItemSerialSnapshot,
    t.Problem, t.Priority, t.Status, t.DateReceived, t.CreatedAt, t.UpdatedAt, t.CompletedAt,
    i.ModelNumber, i.Category,
    COALESCE(reqEmp.Name, reqDept.Name, subEmp.Name) AS RequesterName,
    assignedEmp.Name AS AssignedTechnicianName,
    " + linkSelect + @"
FROM dbo.RepairTicket t
LEFT JOIN dbo.Item i ON i.ItemId = t.ItemId
LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = t.RequestedByEmpId
LEFT JOIN dbo.Department reqDept ON reqDept.DeptId = t.RequestedByDeptId
LEFT JOIN dbo.Employee subEmp ON subEmp.EmpId = t.SubmittedByEmpId
LEFT JOIN dbo.Employee assignedEmp ON assignedEmp.EmpId = t.AssignedTechEmpId
" + callJoin + @"
WHERE " + whereSql + @"
ORDER BY CASE t.Priority WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END,
         t.UpdatedAt DESC, t.RepairTicketId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

                var tickets = new List<object>();
                using (var command = new SqlCommand(listSql, connection))
                {
                    command.Parameters.AddRange(CloneParameters(parameters));
                    command.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
                    command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tickets.Add(new
                            {
                                repairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                                ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                                itemId = Convert.ToInt32(reader["ItemId"]),
                                itemName = RepairMobileApiSupport.StringValue(reader, "ItemNameSnapshot"),
                                serialNumber = RepairMobileApiSupport.StringValue(reader, "ItemSerialSnapshot"),
                                modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                                category = RepairMobileApiSupport.StringValue(reader, "Category"),
                                problem = RepairMobileApiSupport.StringValue(reader, "Problem"),
                                priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                                status = RepairMobileApiSupport.StringValue(reader, "Status"),
                                requesterName = RepairMobileApiSupport.StringValue(reader, "RequesterName"),
                                assignedTechnicianName = RepairMobileApiSupport.StringValue(reader, "AssignedTechnicianName"),
                                dateReceived = RepairMobileApiSupport.IsoDate(reader["DateReceived"]),
                                createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                                updatedAt = RepairMobileApiSupport.IsoUtc(reader["UpdatedAt"]),
                                completedAt = RepairMobileApiSupport.IsoUtc(reader["CompletedAt"]),
                                linkedCallTicketId = RepairMobileApiSupport.IntValue(reader, "LinkedCallTicketId"),
                                linkedCallTicketCode = RepairMobileApiSupport.StringValue(reader, "LinkedCallTicketCode"),
                                linkedCallStatus = RepairMobileApiSupport.StringValue(reader, "LinkedCallStatus")
                            });
                        }
                    }
                }

                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    scope = scope,
                    tickets = tickets,
                    totalCount = totalCount,
                    page = page,
                    pageSize = pageSize
                });
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair tickets are temporarily unavailable");
        }
    }

    private static void Create(HttpContext context)
    {
        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;

        JObject request;
        if (!RepairMobileApiSupport.TryReadObject(context, out request))
        {
            RepairMobileApiSupport.Error(context, 400, "A valid JSON request body is required");
            return;
        }

        var itemId = RepairMobileApiSupport.Int(request, "itemId");
        var problem = RepairMobileApiSupport.Text(request, "problem", 2000);
        var priority = RepairMobileApiSupport.Text(request, "priority", 20) ?? "Medium";
        var requestedByType = RepairMobileApiSupport.Text(request, "requestedByType", 20);
        var requestedByEmployeeId = RepairMobileApiSupport.Int(request, "requestedByEmployeeId");
        var requestedByDepartmentId = RepairMobileApiSupport.Int(request, "requestedByDepartmentId");
        var requestedByCompanyId = RepairMobileApiSupport.Int(request, "requestedByCompanyId");
        var requestedByBranchId = RepairMobileApiSupport.Int(request, "requestedByBranchId");
        var dateReceived = RepairMobileApiSupport.UtcDate(request, "dateReceived");

        if (!itemId.HasValue || itemId.Value <= 0 || string.IsNullOrWhiteSpace(problem))
        {
            RepairMobileApiSupport.Error(context, 400, "itemId and problem are required");
            return;
        }

        // A requester cannot create a ticket on behalf of another employee. An IT technician may
        // select a requester/department while receiving an item, mirroring desktop intake.
        if (!actor.IsItAuthorized)
        {
            requestedByType = actor.EmployeeId.HasValue ? "Employee" : null;
            requestedByEmployeeId = actor.EmployeeId;
            requestedByDepartmentId = null;
            requestedByCompanyId = null;
            requestedByBranchId = null;
        }

        if (!string.IsNullOrWhiteSpace(requestedByType) &&
            !string.Equals(requestedByType, "Employee", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedByType, "Department", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 400, "requestedByType must be Employee or Department");
            return;
        }
        if (string.Equals(requestedByType, "Employee", StringComparison.OrdinalIgnoreCase) &&
            (!requestedByEmployeeId.HasValue || requestedByEmployeeId.Value <= 0))
        {
            RepairMobileApiSupport.Error(context, 400, "requestedByEmployeeId is required for an employee request");
            return;
        }
        if (string.Equals(requestedByType, "Department", StringComparison.OrdinalIgnoreCase) &&
            (!requestedByDepartmentId.HasValue || !requestedByCompanyId.HasValue))
        {
            RepairMobileApiSupport.Error(context, 400, "Department requests require company and department");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.HasProcedure(connection, "dbo.sp_RepairPortal_CreateTicket"))
                {
                    RepairMobileApiSupport.Error(context, 503, "Repair ticket creation is not installed for this environment");
                    return;
                }

                using (var item = new SqlCommand("SELECT COUNT(1) FROM dbo.Item WHERE ItemId = @ItemId AND ISNULL(Active, 1) = 1;", connection))
                {
                    item.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId.Value;
                    if (Convert.ToInt32(item.ExecuteScalar()) == 0)
                    {
                        RepairMobileApiSupport.Error(context, 404, "The selected item is unavailable");
                        return;
                    }
                }

                using (var command = new SqlCommand("dbo.sp_RepairPortal_CreateTicket", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId.Value;
                    command.Parameters.Add("@Problem", SqlDbType.NVarChar, 2000).Value = problem;
                    command.Parameters.Add("@Priority", SqlDbType.NVarChar, 20).Value = priority;
                    command.Parameters.Add("@SubmittedByEmpId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
                    command.Parameters.Add("@SubmittedByUserId", SqlDbType.Int).Value = actor.UserId;
                    command.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = actor.UserId;
                    command.Parameters.Add("@RequestedByType", SqlDbType.NVarChar, 20).Value = RepairMobileApiSupport.DbValue(requestedByType);
                    command.Parameters.Add("@RequestedByDeptId", SqlDbType.Int).Value = requestedByDepartmentId.HasValue ? (object)requestedByDepartmentId.Value : DBNull.Value;
                    command.Parameters.Add("@RequestedByEmpId", SqlDbType.Int).Value = requestedByEmployeeId.HasValue ? (object)requestedByEmployeeId.Value : DBNull.Value;
                    command.Parameters.Add("@DateReceived", SqlDbType.DateTime2).Value = dateReceived.HasValue ? (object)dateReceived.Value : DBNull.Value;
                    command.Parameters.Add("@RequestedByComId", SqlDbType.Int).Value = requestedByCompanyId.HasValue ? (object)requestedByCompanyId.Value : DBNull.Value;
                    command.Parameters.Add("@RequestedByBranchId", SqlDbType.Int).Value = requestedByBranchId.HasValue ? (object)requestedByBranchId.Value : DBNull.Value;

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            RepairMobileApiSupport.Error(context, 503, "Repair ticket creation did not return a ticket");
                            return;
                        }
                        RepairMobileApiSupport.Created(context, new
                        {
                            success = true,
                            ticket = new
                            {
                                repairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                                ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                                status = RepairMobileApiSupport.StringValue(reader, "Status"),
                                priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                                createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"])
                            }
                        });
                    }
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair ticket creation could not be completed");
        }
    }

    private static SqlParameter[] CloneParameters(List<SqlParameter> parameters)
    {
        var copies = new List<SqlParameter>();
        foreach (var source in parameters)
        {
            var copy = new SqlParameter(source.ParameterName, source.SqlDbType, source.Size) { Value = source.Value };
            copies.Add(copy);
        }
        return copies.ToArray();
    }

    public bool IsReusable { get { return false; } }
}
