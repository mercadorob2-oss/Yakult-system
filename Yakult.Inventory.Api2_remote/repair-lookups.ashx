<%@ WebHandler Language="C#" Class="RepairLookupsHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;

/// <summary>Small, pageless lookup endpoints used by mobile repair intake and technician actions.
/// All values are server-sourced; no ownership or actor identifiers are accepted from the client.</summary>
public sealed class RepairLookupsHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "GET, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;
        var kind = (context.Request.QueryString["kind"] ?? string.Empty).Trim().ToLowerInvariant();
        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                switch (kind)
                {
                    case "items":
                        ItemLookup(context, connection);
                        return;
                    case "spares":
                        if (!actor.IsItAuthorized)
                        {
                            RepairMobileApiSupport.Error(context, 403, "Your account is not authorized for technician lookups");
                            return;
                        }
                        SpareItemLookup(context, connection);
                        return;
                    case "companies":
                        OrganizationLookup(context, connection, "SELECT ComId AS Id, Name FROM dbo.Company WHERE ISNULL(Active,1)=1 ORDER BY Name;", null, null);
                        return;
                    case "branches":
                        OrganizationLookup(context, connection, @"SELECT DISTINCT b.BranchId AS Id, b.Name
FROM dbo.Branch b
INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.BranchID = b.BranchId
WHERE ISNULL(b.Active,1)=1 AND (@CompanyId IS NULL OR bdc.CompanyID=@CompanyId)
ORDER BY b.Name;", "@CompanyId", ParseOptionalId(context, "companyId"));
                        return;
                    case "departments":
                        OrganizationLookup(context, connection, @"SELECT DISTINCT d.DeptId AS Id, d.Name
FROM dbo.Department d
INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.DepartmentID = d.DeptId
WHERE ISNULL(d.Active,1)=1
  AND bdc.DepartmentID IS NOT NULL
  AND (@CompanyId IS NULL OR bdc.CompanyID=@CompanyId)
  AND (@BranchId IS NULL OR bdc.BranchID=@BranchId)
ORDER BY d.Name;", "@CompanyId", ParseOptionalId(context, "companyId"), "@BranchId", ParseOptionalId(context, "branchId"));
                        return;
                    case "employees":
                        OrganizationLookup(context, connection, @"SELECT EmpId AS Id, Name FROM dbo.Employee WHERE ISNULL(Active,1)=1
AND (@DepartmentId IS NULL OR DeptId=@DepartmentId)
AND (@CompanyId IS NULL OR ComId=@CompanyId)
AND (@BranchId IS NULL OR BranchId=@BranchId)
ORDER BY Name;", "@DepartmentId", ParseOptionalId(context, "departmentId"), "@CompanyId", ParseOptionalId(context, "companyId"), "@BranchId", ParseOptionalId(context, "branchId"));
                        return;
                    case "technicians":
                    case "vendors":
                        if (!actor.IsItAuthorized)
                        {
                            RepairMobileApiSupport.Error(context, 403, "Your account is not authorized for technician lookups");
                            return;
                        }
                        if (kind == "technicians") TechnicianLookup(context, connection); else VendorLookup(context, connection);
                        return;
                    default:
                        RepairMobileApiSupport.Error(context, 400, "kind must be items, spares, companies, branches, departments, employees, technicians, or vendors");
                        return;
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair lookup data is temporarily unavailable");
        }
    }

    private static void ItemLookup(HttpContext context, SqlConnection connection)
    {
        var query = (context.Request.QueryString["query"] ?? string.Empty).Trim();
        if (query.Length > 200)
        {
            RepairMobileApiSupport.Error(context, 400, "query is too long");
            return;
        }
        const string sql = @"
SELECT TOP (50) ItemId, Name, ModelNumber, SerialNumber, Category, StockOnHand, ConditionID
FROM dbo.Item
WHERE ISNULL(Active,1)=1
  AND (@Query='' OR Name LIKE @LikeQuery OR ModelNumber LIKE @LikeQuery OR SerialNumber LIKE @LikeQuery)
ORDER BY Name, ModelNumber, SerialNumber;";
        var items = new List<object>();
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@Query", SqlDbType.NVarChar, 200).Value = query;
            command.Parameters.Add("@LikeQuery", SqlDbType.NVarChar, 450).Value = "%" + query + "%";
            using (var reader = command.ExecuteReader())
                while (reader.Read()) items.Add(new
                {
                    id = Convert.ToInt32(reader["ItemId"]),
                    name = RepairMobileApiSupport.StringValue(reader, "Name"),
                    modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                    serialNumber = RepairMobileApiSupport.StringValue(reader, "SerialNumber"),
                    category = RepairMobileApiSupport.StringValue(reader, "Category"),
                    stockOnHand = RepairMobileApiSupport.IntValue(reader, "StockOnHand"),
                    conditionId = RepairMobileApiSupport.IntValue(reader, "ConditionID")
                });
        }
        RepairMobileApiSupport.Ok(context, new { success = true, items = items });
    }

    private static void SpareItemLookup(HttpContext context, SqlConnection connection)
    {
        if (!RepairMobileApiSupport.HasColumn(connection, "dbo.Item", "IsBorrowable"))
        {
            RepairMobileApiSupport.Error(context, 503, "Borrowable spare item support is not installed for this environment");
            return;
        }
        var query = (context.Request.QueryString["query"] ?? string.Empty).Trim();
        if (query.Length > 200)
        {
            RepairMobileApiSupport.Error(context, 400, "query is too long");
            return;
        }
        const string sql = @"
SELECT TOP (50) i.ItemId, i.Name, i.ModelNumber, i.SerialNumber, i.Category, i.StockOnHand, i.ConditionID
FROM dbo.Item i
WHERE ISNULL(i.Active,1)=1
  AND ISNULL(i.IsBorrowable,0)=1
  AND i.SerialNumber IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.BorrowLog bl WHERE bl.ItemId=i.ItemId AND bl.ReturnedAtUtc IS NULL)
  AND NOT EXISTS (
        SELECT 1
        FROM dbo.SetItem si
        INNER JOIN dbo.[Set] s ON s.SetId=si.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType='Set' AND archS.EntityId=s.SetId AND archS.IsArchived=1
        WHERE si.ItemId=i.ItemId AND ISNULL(s.Active,1)=1 AND archS.EntityId IS NULL
        UNION ALL
        SELECT 1
        FROM dbo.Request r
        INNER JOIN dbo.[Set] s ON s.SetId=r.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType='Set' AND archS.EntityId=s.SetId AND archS.IsArchived=1
        WHERE r.ItemId=i.ItemId AND ISNULL(r.Active,1)=1 AND r.SetId IS NOT NULL
          AND ISNULL(s.Active,1)=1 AND archS.EntityId IS NULL
  )
  AND (@Query='' OR i.Name LIKE @LikeQuery OR i.ModelNumber LIKE @LikeQuery OR i.SerialNumber LIKE @LikeQuery)
ORDER BY i.Name, i.ModelNumber, i.SerialNumber;";
        var items = new List<object>();
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@Query", SqlDbType.NVarChar, 200).Value = query;
            command.Parameters.Add("@LikeQuery", SqlDbType.NVarChar, 450).Value = "%" + query + "%";
            using (var reader = command.ExecuteReader())
                while (reader.Read()) items.Add(new
                {
                    id = Convert.ToInt32(reader["ItemId"]),
                    name = RepairMobileApiSupport.StringValue(reader, "Name"),
                    modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                    serialNumber = RepairMobileApiSupport.StringValue(reader, "SerialNumber"),
                    category = RepairMobileApiSupport.StringValue(reader, "Category"),
                    stockOnHand = RepairMobileApiSupport.IntValue(reader, "StockOnHand"),
                    conditionId = RepairMobileApiSupport.IntValue(reader, "ConditionID")
                });
        }
        RepairMobileApiSupport.Ok(context, new { success = true, items = items });
    }

    private static void OrganizationLookup(HttpContext context, SqlConnection connection, string sql, string parameterName, int? parameterValue, string secondParameterName = null, int? secondParameterValue = null, string thirdParameterName = null, int? thirdParameterValue = null)
    {
        var items = new List<object>();
        using (var command = new SqlCommand(sql, connection))
        {
            if (parameterName != null) command.Parameters.Add(parameterName, SqlDbType.Int).Value = parameterValue.HasValue ? (object)parameterValue.Value : DBNull.Value;
            if (secondParameterName != null) command.Parameters.Add(secondParameterName, SqlDbType.Int).Value = secondParameterValue.HasValue ? (object)secondParameterValue.Value : DBNull.Value;
            if (thirdParameterName != null) command.Parameters.Add(thirdParameterName, SqlDbType.Int).Value = thirdParameterValue.HasValue ? (object)thirdParameterValue.Value : DBNull.Value;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) items.Add(new { id = Convert.ToInt32(reader["Id"]), name = RepairMobileApiSupport.StringValue(reader, "Name") });
        }
        RepairMobileApiSupport.Ok(context, new { success = true, items = items });
    }

    private static void TechnicianLookup(HttpContext context, SqlConnection connection)
    {
        const string sql = @"
SELECT DISTINCT e.EmpId AS Id, e.Name
FROM dbo.Employee e
LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
LEFT JOIN dbo.[User] u ON u.EmpId = e.EmpId AND ISNULL(u.IsActive,1)=1
LEFT JOIN dbo.UserRole ur ON ur.UserId = u.UserId
LEFT JOIN dbo.Role r ON r.RoleId = ur.RoleId AND ISNULL(r.IsActive,1)=1
WHERE ISNULL(e.Active,1)=1
  AND (UPPER(ISNULL(d.Name,'')) LIKE 'IT%' OR UPPER(ISNULL(d.Name,'')) LIKE '%INFORMATION TECHNOLOGY%'
       OR r.RoleName IN ('Admin','Developer','IT Manager','Supervisor','Tech Support'))
ORDER BY e.Name;";
        OrganizationLookup(context, connection, sql, null, null);
    }

    private static void VendorLookup(HttpContext context, SqlConnection connection)
    {
        const string sql = "SELECT VendorID AS Id, VendorName AS Name FROM dbo.Vendor WHERE ISNULL(Active,1)=1 ORDER BY VendorName;";
        OrganizationLookup(context, connection, sql, null, null);
    }

    private static int? ParseOptionalId(HttpContext context, string name)
    {
        int value;
        return int.TryParse(context.Request.QueryString[name], out value) && value > 0 ? (int?)value : null;
    }

    public bool IsReusable { get { return false; } }
}
