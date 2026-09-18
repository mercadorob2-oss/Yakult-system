<%@ WebHandler Language="C#" Class="MobileBorrowHandler" %>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Root-level mobile Borrow Items API. It intentionally does not use /api/Borrow because
/// production IIS has a child /api application that can intercept that path before Global.asax.
/// The contract mirrors the desktop BorrowItemsRepository workflow.
/// </summary>
public sealed class MobileBorrowHandler : IHttpHandler
{
    private const string BorrowColumns = @"
b.BorrowId,b.ItemId,b.SerialNumber,b.ItemName,b.ItemDescription,b.ModelNumber,
b.BorrowedByEmpId,b.BorrowedByEmpName,b.BorrowedByDeptId,b.BorrowedByDeptName,
b.BorrowEncodedByUserId,b.BorrowEncodedByUserName,b.BorrowedAtUtc,
b.ReturnedByEmpId,b.ReturnedByEmpName,b.ReturnedByDeptId,b.ReturnedByDeptName,
b.ReturnEncodedByUserId,b.ReturnEncodedByUserName,b.ReturnedAtUtc";

    private sealed class BorrowRow
    {
        public int BorrowId;
        public int? ItemId;
        public string SerialNumber;
        public string ItemName;
        public string ItemDescription;
        public string ModelNumber;
        public int? BorrowedByEmpId;
        public string BorrowedByEmpName;
        public int? BorrowedByDeptId;
        public string BorrowedByDeptName;
        public int? BorrowEncodedByUserId;
        public string BorrowEncodedByUserName;
        public DateTime? BorrowedAtUtc;
        public int? ReturnedByEmpId;
        public string ReturnedByEmpName;
        public int? ReturnedByDeptId;
        public string ReturnedByDeptName;
        public int? ReturnEncodedByUserId;
        public string ReturnEncodedByUserName;
        public DateTime? ReturnedAtUtc;

        public object ToDto()
        {
            return new
            {
                borrowId = BorrowId,
                itemId = ItemId ?? 0,
                serialNumber = SerialNumber,
                itemName = ItemName,
                itemDescription = ItemDescription,
                modelNumber = ModelNumber,
                borrowedByEmpId = BorrowedByEmpId,
                borrowedByEmpName = BorrowedByEmpName,
                borrowedByDeptId = BorrowedByDeptId,
                borrowedByDeptName = BorrowedByDeptName,
                borrowEncodedByUserId = BorrowEncodedByUserId,
                borrowEncodedByUserName = BorrowEncodedByUserName,
                borrowedAtUtc = BorrowedAtUtc,
                returnedByEmpId = ReturnedByEmpId,
                returnedByEmpName = ReturnedByEmpName,
                returnedByDeptId = ReturnedByDeptId,
                returnedByDeptName = ReturnedByDeptName,
                returnEncodedByUserId = ReturnEncodedByUserId,
                returnEncodedByUserName = ReturnEncodedByUserName,
                returnedAtUtc = ReturnedAtUtc,
                isOpen = !ReturnedAtUtc.HasValue
            };
        }
    }

    private sealed class ItemRow
    {
        public int ItemId;
        public string SerialNumber;
        public string Name;
        public string Description;
        public string ModelNumber;

        public object ToDto()
        {
            return new { itemId = ItemId, serialNumber = SerialNumber, itemName = Name, itemDescription = Description, modelNumber = ModelNumber };
        }
    }

    private sealed class BorrowEligibility
    {
        public bool Eligible;
        public string Code;
        public string Message;
    }

    private sealed class MobileApiIdentity
    {
        public int UserId;
        public string Username;
        public string DisplayName;
    }

    private static bool TryGetIdentity(HttpContext context, out MobileApiIdentity identity)
    {
        identity = null;
        var authorization = context.Request.Headers["Authorization"] ?? string.Empty;
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization.Substring(7).Trim()
            : string.Empty;
        if (token.Length == 0) return false;

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;
            var secret = ConfigurationManager.AppSettings["AuthTokenSecret"];
            if (string.IsNullOrWhiteSpace(secret)) return false;
            var signingInput = parts[0] + "." + parts[1];
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                if (!FixedTimeEquals(Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput))), parts[2])) return false;
            }
            var claims = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            DateTime expiresUtc;
            int userId;
            if (claims["exp"] == null || claims["sub"] == null ||
                !DateTime.TryParse(claims["exp"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expiresUtc) ||
                expiresUtc <= DateTime.UtcNow || !int.TryParse(claims["sub"].ToString(), out userId) || userId <= 0)
                return false;
            identity = new MobileApiIdentity
            {
                UserId = userId,
                Username = claims["username"] == null ? string.Empty : claims["username"].ToString(),
                DisplayName = claims["name"] == null ? string.Empty : claims["name"].ToString()
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
        }
        return Convert.FromBase64String(normalized);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        var difference = 0;
        for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
        return difference == 0;
    }

    public bool IsReusable { get { return false; } }

    private static string Cs()
    {
        var connection = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"];
        return connection == null ? string.Empty : connection.ConnectionString;
    }

    private static void Json(HttpContext context, int status, object body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
        context.Response.AddHeader("Access-Control-Allow-Origin", "*");
        context.Response.Write(JsonConvert.SerializeObject(body));
    }

    private static void Ok(HttpContext context, object body) { Json(context, 200, body); }
    private static void Error(HttpContext context, int status, string message, string code)
    {
        Json(context, status, new { success = false, message = message, error = code });
    }

    private static string Body(HttpContext context)
    {
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding)) return reader.ReadToEnd();
    }

    private static string Text(SqlDataReader reader, string column)
    {
        var value = reader[column];
        return value == DBNull.Value ? null : value.ToString();
    }

    private static int? NullableInt(SqlDataReader reader, string column)
    {
        var value = reader[column];
        return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
    }

    private static DateTime? NullableDate(SqlDataReader reader, string column)
    {
        var value = reader[column];
        return value == DBNull.Value ? (DateTime?)null : DateTime.SpecifyKind(Convert.ToDateTime(value), DateTimeKind.Utc);
    }

    private static bool HasColumn(SqlConnection connection, SqlTransaction transaction, string tableName, string columnName)
    {
        using (var command = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@TableName,@ColumnName) IS NULL THEN 0 ELSE 1 END;", connection, transaction))
        {
            Add(command, "@TableName", tableName);
            Add(command, "@ColumnName", columnName);
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
    }

    private static bool HasTable(SqlConnection connection, SqlTransaction transaction, string objectName)
    {
        using (var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@ObjectName,'U') IS NULL THEN 0 ELSE 1 END;", connection, transaction))
        {
            Add(command, "@ObjectName", objectName);
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
    }

    private static string BuildActiveAssignmentPredicate(SqlConnection connection, SqlTransaction transaction, string itemReference)
    {
        if (!HasTable(connection, transaction, "dbo.Set")) return string.Empty;

        var hasArchiveStatus = HasTable(connection, transaction, "dbo.ArchiveStatus");
        var archiveJoin = hasArchiveStatus
            ? " LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType='Set' AND archS.EntityId=s.SetId AND archS.IsArchived=1"
            : string.Empty;
        var archiveFilter = hasArchiveStatus ? " AND archS.EntityId IS NULL" : string.Empty;
        var clauses = new List<string>();

        if (HasTable(connection, transaction, "dbo.SetItem"))
        {
            clauses.Add(@"SELECT 1
FROM dbo.SetItem si
INNER JOIN dbo.[Set] s ON s.SetId=si.SetId" + archiveJoin + @"
WHERE si.ItemId=" + itemReference + @" AND s.Active=1" + archiveFilter);
        }

        if (HasTable(connection, transaction, "dbo.Request"))
        {
            clauses.Add(@"SELECT 1
FROM dbo.Request r
INNER JOIN dbo.[Set] s ON s.SetId=r.SetId" + archiveJoin + @"
WHERE r.ItemId=" + itemReference + @" AND r.Active=1 AND r.SetId IS NOT NULL AND s.Active=1" + archiveFilter);
        }

        return clauses.Count == 0
            ? string.Empty
            : "NOT EXISTS (" + string.Join(" UNION ALL ", clauses.ToArray()) + ")";
    }

    private static BorrowEligibility EvaluateBorrowEligibility(SqlConnection connection, SqlTransaction transaction, ItemRow item)
    {
        if (item == null || item.ItemId <= 0)
            return new BorrowEligibility { Eligible = false, Code = "item_not_found", Message = "Item not found for the given serial number." };

        if (string.IsNullOrWhiteSpace(item.SerialNumber))
            return new BorrowEligibility { Eligible = false, Code = "serial_required", Message = "This item does not have a usable serial number." };

        if (!HasColumn(connection, transaction, "dbo.Item", "IsBorrowable"))
            return new BorrowEligibility { Eligible = false, Code = "borrow_eligibility_unavailable", Message = "Borrow eligibility is unavailable because the inventory schema is missing IsBorrowable." };

        var active = false;
        var borrowable = false;
        using (var command = new SqlCommand(@"
SELECT TOP 1
    CASE WHEN ISNULL(i.Active,0)=1 THEN 1 ELSE 0 END AS IsActive,
    CASE WHEN ISNULL(i.IsBorrowable,0)=1 THEN 1 ELSE 0 END AS IsBorrowable
FROM dbo.Item i
WHERE i.ItemId=@ItemId;", connection, transaction))
        {
            Add(command, "@ItemId", item.ItemId);
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                    return new BorrowEligibility { Eligible = false, Code = "item_not_found", Message = "Item not found for the given serial number." };
                active = Convert.ToInt32(reader["IsActive"]) == 1;
                borrowable = Convert.ToInt32(reader["IsBorrowable"]) == 1;
            }
        }

        if (!active)
            return new BorrowEligibility { Eligible = false, Code = "item_inactive", Message = "This item is inactive and cannot be borrowed." };
        if (!borrowable)
            return new BorrowEligibility { Eligible = false, Code = "item_not_borrowable", Message = "This item is not marked as borrowable." };

        var assignmentPredicate = BuildActiveAssignmentPredicate(connection, transaction, "@ItemId");
        if (assignmentPredicate.Length > 0)
        {
            using (var command = new SqlCommand(@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Item i
    WHERE i.ItemId=@ItemId AND " + assignmentPredicate + @"
) THEN 1 ELSE 0 END;", connection, transaction))
            {
                Add(command, "@ItemId", item.ItemId);
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    return new BorrowEligibility { Eligible = false, Code = "item_assigned", Message = "This item is assigned to an active Set or Request and cannot be borrowed." };
            }
        }

        return new BorrowEligibility { Eligible = true, Code = null, Message = null };
    }

    private static void Add(SqlCommand command, string name, object value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static int ReadInt(string value, int fallback)
    {
        int parsed;
        return int.TryParse(value, out parsed) ? parsed : fallback;
    }

    private static int? JsonInt(JObject body, string property)
    {
        var token = body[property];
        if (token == null || token.Type == JTokenType.Null) return null;
        int value;
        return int.TryParse(token.ToString(), out value) ? (int?)value : null;
    }

    private static DateTime? JsonUtc(JObject body, string property, out bool valid)
    {
        valid = true;
        var token = body[property];
        if (token == null || token.Type == JTokenType.Null || string.IsNullOrWhiteSpace(token.ToString())) return null;
        DateTime value;
        if (!DateTime.TryParse(token.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
        {
            valid = false;
            return null;
        }
        return value;
    }

    private static BorrowRow ReadBorrow(SqlDataReader reader)
    {
        return new BorrowRow
        {
            BorrowId = Convert.ToInt32(reader["BorrowId"]),
            ItemId = NullableInt(reader, "ItemId"),
            SerialNumber = Text(reader, "SerialNumber"),
            ItemName = Text(reader, "ItemName"),
            ItemDescription = Text(reader, "ItemDescription"),
            ModelNumber = Text(reader, "ModelNumber"),
            BorrowedByEmpId = NullableInt(reader, "BorrowedByEmpId"),
            BorrowedByEmpName = Text(reader, "BorrowedByEmpName"),
            BorrowedByDeptId = NullableInt(reader, "BorrowedByDeptId"),
            BorrowedByDeptName = Text(reader, "BorrowedByDeptName"),
            BorrowEncodedByUserId = NullableInt(reader, "BorrowEncodedByUserId"),
            BorrowEncodedByUserName = Text(reader, "BorrowEncodedByUserName"),
            BorrowedAtUtc = NullableDate(reader, "BorrowedAtUtc"),
            ReturnedByEmpId = NullableInt(reader, "ReturnedByEmpId"),
            ReturnedByEmpName = Text(reader, "ReturnedByEmpName"),
            ReturnedByDeptId = NullableInt(reader, "ReturnedByDeptId"),
            ReturnedByDeptName = Text(reader, "ReturnedByDeptName"),
            ReturnEncodedByUserId = NullableInt(reader, "ReturnEncodedByUserId"),
            ReturnEncodedByUserName = Text(reader, "ReturnEncodedByUserName"),
            ReturnedAtUtc = NullableDate(reader, "ReturnedAtUtc")
        };
    }

    private static ItemRow GetItem(SqlConnection connection, SqlTransaction transaction, string serial)
    {
        using (var command = new SqlCommand(@"SELECT TOP 1 ItemId,SerialNumber,Name,Description,ModelNumber FROM dbo.Item WHERE SerialNumber=@Serial;", connection, transaction))
        {
            Add(command, "@Serial", serial);
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new ItemRow
                {
                    ItemId = Convert.ToInt32(reader["ItemId"]),
                    SerialNumber = Text(reader, "SerialNumber"),
                    Name = Text(reader, "Name"),
                    Description = Text(reader, "Description"),
                    ModelNumber = Text(reader, "ModelNumber")
                };
            }
        }
    }

    private static BorrowRow GetBorrowById(SqlConnection connection, SqlTransaction transaction, int borrowId)
    {
        using (var command = new SqlCommand("SELECT TOP 1 " + BorrowColumns + " FROM dbo.BorrowLog b WHERE b.BorrowId=@BorrowId;", connection, transaction))
        {
            Add(command, "@BorrowId", borrowId);
            using (var reader = command.ExecuteReader()) return reader.Read() ? ReadBorrow(reader) : null;
        }
    }

    private static BorrowRow GetOpenByItem(SqlConnection connection, SqlTransaction transaction, int itemId, bool lockRow)
    {
        var hint = lockRow ? " WITH (UPDLOCK,HOLDLOCK)" : string.Empty;
        using (var command = new SqlCommand("SELECT TOP 1 " + BorrowColumns + " FROM dbo.BorrowLog b" + hint + " WHERE b.ItemId=@ItemId AND b.ReturnedAtUtc IS NULL ORDER BY b.BorrowId DESC;", connection, transaction))
        {
            Add(command, "@ItemId", itemId);
            using (var reader = command.ExecuteReader()) return reader.Read() ? ReadBorrow(reader) : null;
        }
    }

    private static BorrowRow GetOpenBySerial(SqlConnection connection, string serial)
    {
        using (var command = new SqlCommand("SELECT TOP 1 " + BorrowColumns + " FROM dbo.BorrowLog b WHERE b.SerialNumber=@Serial AND b.ReturnedAtUtc IS NULL ORDER BY b.BorrowId DESC;", connection))
        {
            Add(command, "@Serial", serial);
            using (var reader = command.ExecuteReader()) return reader.Read() ? ReadBorrow(reader) : null;
        }
    }

    private static bool SameBorrower(BorrowRow row, bool departmentOnly, int? employeeId, int? departmentId, int encoderId, DateTime actualBorrowedAtUtc)
    {
        if (row == null || row.ReturnedAtUtc.HasValue || !row.BorrowedAtUtc.HasValue) return false;
        var sameBorrower = departmentOnly
            ? !row.BorrowedByEmpId.HasValue && row.BorrowedByDeptId == departmentId
            : row.BorrowedByEmpId == employeeId;
        return sameBorrower && row.BorrowEncodedByUserId == encoderId && Math.Abs((row.BorrowedAtUtc.Value - actualBorrowedAtUtc).TotalMinutes) <= 2;
    }

    private static string GetEncoderName(SqlConnection connection, SqlTransaction transaction, MobileApiIdentity identity)
    {
        using (var command = new SqlCommand("SELECT TOP 1 Name FROM dbo.[User] WHERE UserId=@UserId;", connection, transaction))
        {
            Add(command, "@UserId", identity.UserId);
            var value = command.ExecuteScalar();
            if (value != null && value != DBNull.Value && !string.IsNullOrWhiteSpace(value.ToString())) return value.ToString().Trim();
        }
        if (!string.IsNullOrWhiteSpace(identity.DisplayName)) return identity.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(identity.Username)) return identity.Username.Trim();
        return identity.UserId.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryGetEmployee(SqlConnection connection, SqlTransaction transaction, int employeeId, out string employeeName, out int? departmentId, out string departmentName)
    {
        employeeName = null;
        departmentId = null;
        departmentName = null;
        using (var command = new SqlCommand(@"
SELECT TOP 1 e.EmpId,e.Name,e.DeptId,d.Name AS DepartmentName
FROM dbo.Employee e
LEFT JOIN dbo.Department d ON d.DeptId=e.DeptId
WHERE e.EmpId=@EmployeeId AND ISNULL(e.Active,1)=1;", connection, transaction))
        {
            Add(command, "@EmployeeId", employeeId);
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return false;
                employeeName = Text(reader, "Name");
                departmentId = NullableInt(reader, "DeptId");
                departmentName = Text(reader, "DepartmentName");
                return true;
            }
        }
    }

    private static bool TryGetDepartment(SqlConnection connection, SqlTransaction transaction, int departmentId, out string departmentName)
    {
        departmentName = null;
        using (var command = new SqlCommand("SELECT TOP 1 Name FROM dbo.Department WHERE DeptId=@DepartmentId AND ISNULL(Active,1)=1;", connection, transaction))
        {
            Add(command, "@DepartmentId", departmentId);
            var value = command.ExecuteScalar();
            if (value == null || value == DBNull.Value) return false;
            departmentName = value.ToString().Trim();
            return departmentName.Length > 0;
        }
    }

    private static bool CanDeleteOpenBorrow(SqlConnection connection, int userId)
    {
        try
        {
            using (var command = new SqlCommand(@"
SELECT CAST(CASE WHEN
    EXISTS (SELECT 1 FROM dbo.[User] u WHERE u.UserId=@UserId AND (ISNULL(u.IsDeveloper,0)=1 OR ISNULL(u.IsSuperAdmin,0)=1))
 OR EXISTS (SELECT 1 FROM dbo.UserRole ur INNER JOIN dbo.Role r ON r.RoleId=ur.RoleId WHERE ur.UserId=@UserId AND ISNULL(r.IsActive,1)=1 AND r.RoleName IN ('Admin','Developer'))
THEN 1 ELSE 0 END AS bit);", connection))
            {
                Add(command, "@UserId", userId);
                return Convert.ToBoolean(command.ExecuteScalar());
            }
        }
        catch
        {
            return false;
        }
    }

    private static void Companies(HttpContext context)
    {
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            var rows = new List<object>();
            using (var command = new SqlCommand("SELECT ComId,Name AS CompanyName FROM dbo.Company WHERE ISNULL(Active,1)=1 ORDER BY Name;", connection))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new { comId = Convert.ToInt32(reader["ComId"]), companyName = Text(reader, "CompanyName") });
            Ok(context, rows);
        }
    }

    private static void Branches(HttpContext context)
    {
        var companyId = ReadInt(context.Request.QueryString["companyId"], 0);
        if (companyId <= 0) { Error(context, 400, "companyId is required.", "invalid_company"); return; }
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            var rows = new List<object>();
            using (var command = new SqlCommand(@"
SELECT DISTINCT b.BranchId,bdc.CompanyID AS ComId,b.Name AS BranchName
FROM dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Branch b ON b.BranchId=bdc.BranchID
WHERE bdc.CompanyID=@CompanyId AND ISNULL(b.Active,1)=1
ORDER BY b.Name;", connection))
            {
                Add(command, "@CompanyId", companyId);
                using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add(new { branchId = Convert.ToInt32(reader["BranchId"]), comId = Convert.ToInt32(reader["ComId"]), branchName = Text(reader, "BranchName") });
            }
            Ok(context, rows);
        }
    }

    private static void Departments(HttpContext context)
    {
        var companyId = ReadInt(context.Request.QueryString["companyId"], 0);
        if (companyId <= 0) { Error(context, 400, "companyId is required.", "invalid_company"); return; }
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            var rows = new List<object>();
            using (var command = new SqlCommand(@"
SELECT DISTINCT d.DeptId,bdc.CompanyID AS ComId,d.Name AS DepartmentName
FROM dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Department d ON d.DeptId=bdc.DepartmentID
WHERE bdc.CompanyID=@CompanyId AND ISNULL(d.Active,1)=1
ORDER BY d.Name;", connection))
            {
                Add(command, "@CompanyId", companyId);
                using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add(new { deptId = Convert.ToInt32(reader["DeptId"]), comId = Convert.ToInt32(reader["ComId"]), departmentName = Text(reader, "DepartmentName") });
            }
            Ok(context, rows);
        }
    }

    private static void Employees(HttpContext context)
    {
        var query = (context.Request.QueryString["q"] ?? string.Empty).Trim();
        var companyId = ReadInt(context.Request.QueryString["companyId"], 0);
        var departmentId = ReadInt(context.Request.QueryString["departmentId"], 0);
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            var rows = new List<object>();
            using (var command = new SqlCommand(@"
SELECT TOP 500
    e.EmpId,e.Name AS EmployeeName,
    ISNULL(org.CompanyID,ISNULL(e.ComId,0)) AS ComId,c.Name AS CompanyName,
    e.BranchId,b.Name AS BranchName,e.DeptId,d.Name AS DepartmentName
FROM dbo.Employee e
OUTER APPLY (
    SELECT TOP 1 bdc.CompanyID
    FROM dbo.BranchDepartmentCompany bdc
    WHERE bdc.BranchID=e.BranchId AND (bdc.DepartmentID=e.DeptId OR e.DeptId IS NULL)
    ORDER BY CASE WHEN bdc.DepartmentID=e.DeptId THEN 0 ELSE 1 END,bdc.CompanyID
) org
LEFT JOIN dbo.Company c ON c.ComId=ISNULL(org.CompanyID,e.ComId)
LEFT JOIN dbo.Branch b ON b.BranchId=e.BranchId
LEFT JOIN dbo.Department d ON d.DeptId=e.DeptId
WHERE ISNULL(e.Active,1)=1
  AND (@Query='' OR e.Name LIKE @LikeQuery)
  AND (@CompanyId=0 OR ISNULL(org.CompanyID,ISNULL(e.ComId,0))=@CompanyId)
  AND (@DepartmentId=0 OR e.DeptId=@DepartmentId)
ORDER BY e.Name;", connection))
            {
                Add(command, "@Query", query);
                Add(command, "@LikeQuery", "%" + query + "%");
                Add(command, "@CompanyId", companyId);
                Add(command, "@DepartmentId", departmentId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(new
                    {
                        empId = Convert.ToInt32(reader["EmpId"]),
                        employeeName = Text(reader, "EmployeeName"),
                        comId = Convert.ToInt32(reader["ComId"]),
                        companyName = Text(reader, "CompanyName"),
                        branchId = NullableInt(reader, "BranchId") ?? 0,
                        branchName = Text(reader, "BranchName"),
                        deptId = NullableInt(reader, "DeptId") ?? 0,
                        departmentName = Text(reader, "DepartmentName")
                    });
            }
            Ok(context, rows);
        }
    }

    private static void CreateEmployee(HttpContext context)
    {
        JObject body;
        try { body = JObject.Parse(Body(context)); } catch { Error(context, 400, "Invalid JSON.", "invalid_json"); return; }
        var employeeName = (body["employeeName"] ?? string.Empty).ToString().Trim();
        var companyId = JsonInt(body, "comId") ?? 0;
        var branchId = JsonInt(body, "branchId") ?? 0;
        var departmentId = JsonInt(body, "deptId") ?? 0;
        if (employeeName.Length == 0 || companyId <= 0 || branchId <= 0 || departmentId <= 0)
        {
            Error(context, 400, "Employee name, company, branch, and department are required.", "invalid_employee");
            return;
        }
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                using (var mapping = new SqlCommand("SELECT COUNT(1) FROM dbo.BranchDepartmentCompany WHERE CompanyID=@CompanyId AND BranchID=@BranchId AND DepartmentID=@DepartmentId;", connection, transaction))
                {
                    Add(mapping, "@CompanyId", companyId); Add(mapping, "@BranchId", branchId); Add(mapping, "@DepartmentId", departmentId);
                    if (Convert.ToInt32(mapping.ExecuteScalar()) <= 0) { transaction.Rollback(); Error(context, 400, "The selected company, branch, and department are not a valid assignment.", "invalid_assignment"); return; }
                }
                int employeeId;
                using (var command = new SqlCommand("INSERT INTO dbo.Employee(Name,ComId,BranchId,DeptId,Active) VALUES(@Name,@CompanyId,@BranchId,@DepartmentId,1);SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                {
                    Add(command, "@Name", employeeName); Add(command, "@CompanyId", companyId); Add(command, "@BranchId", branchId); Add(command, "@DepartmentId", departmentId);
                    employeeId = Convert.ToInt32(command.ExecuteScalar());
                }
                transaction.Commit();
                Ok(context, new { empId = employeeId, employeeName = employeeName, comId = companyId, companyName = (string)null, branchId = branchId, branchName = (string)null, deptId = departmentId, departmentName = (string)null });
            }
        }
    }

    private static void SearchModel(HttpContext context)
    {
        var model = (context.Request.QueryString["model"] ?? string.Empty).Trim();
        if (model.Length == 0) { Ok(context, new List<object>()); return; }

        var maxRows = Math.Min(100, Math.Max(1, ReadInt(context.Request.QueryString["maxRows"], 25)));
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            if (!HasColumn(connection, null, "dbo.Item", "IsBorrowable"))
            {
                Ok(context, new List<object>());
                return;
            }

            var assignmentPredicate = BuildActiveAssignmentPredicate(connection, null, "i.ItemId");
            var assignmentSql = assignmentPredicate.Length == 0 ? string.Empty : " AND " + assignmentPredicate;
            var sql = @"
SELECT TOP (@MaxRows)
    i.ItemId,
    i.SerialNumber,
    i.Name,
    i.Description,
    i.ModelNumber
FROM dbo.Item i
WHERE i.IsBorrowable=1
  AND i.Active=1
  AND i.SerialNumber IS NOT NULL
  AND LTRIM(RTRIM(i.SerialNumber))<>''
  AND NOT EXISTS (
      SELECT 1 FROM dbo.BorrowLog bl
      WHERE bl.ItemId=i.ItemId AND bl.ReturnedAtUtc IS NULL
  )" + assignmentSql + @"
  AND i.ModelNumber LIKE @Model
ORDER BY i.ModelNumber,i.SerialNumber;";

            var rows = new List<object>();
            using (var command = new SqlCommand(sql, connection))
            {
                Add(command, "@MaxRows", maxRows);
                Add(command, "@Model", "%" + model + "%");
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new ItemRow
                        {
                            ItemId = Convert.ToInt32(reader["ItemId"]),
                            SerialNumber = Text(reader, "SerialNumber"),
                            Name = Text(reader, "Name"),
                            Description = Text(reader, "Description"),
                            ModelNumber = Text(reader, "ModelNumber")
                        }.ToDto());
                    }
                }
            }
            Ok(context, rows);
        }
    }

    private static void Resolve(HttpContext context)
    {
        var serial = (context.Request.QueryString["serial"] ?? string.Empty).Trim();
        if (serial.Length == 0) { Error(context, 400, "serial is required.", "invalid_serial"); return; }
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            var item = GetItem(connection, null, serial);
            BorrowRow open = null;
            BorrowEligibility eligibility = null;
            if (item != null)
            {
                open = GetOpenByItem(connection, null, item.ItemId, false);
                eligibility = EvaluateBorrowEligibility(connection, null, item);
            }

            var canBorrow = item != null && open == null && eligibility != null && eligibility.Eligible;
            var eligibilityCode = item == null
                ? "item_not_found"
                : (open != null ? "open_borrow_exists" : eligibility.Code);
            var eligibilityMessage = item == null
                ? "Item not found for the given serial number."
                : (open != null ? "This item is already borrowed (open record exists)." : eligibility.Message);

            Ok(context, new
            {
                item = item == null ? null : item.ToDto(),
                openBorrow = open == null ? null : open.ToDto(),
                canBorrow = canBorrow,
                eligibilityCode = eligibilityCode,
                eligibilityMessage = eligibilityMessage
            });
        }
    }

    private static void Borrow(HttpContext context, MobileApiIdentity identity)
    {
        JObject body;
        try { body = JObject.Parse(Body(context)); } catch { Error(context, 400, "Invalid JSON.", "invalid_json"); return; }
        var serial = (body["serialNumber"] ?? string.Empty).ToString().Trim();
        var employeeId = JsonInt(body, "borrowedByEmpId");
        var departmentId = JsonInt(body, "borrowedByDeptId");
        var departmentOnly = !employeeId.HasValue || employeeId.Value <= 0;
        bool validDate;
        var suppliedBorrowedAt = JsonUtc(body, "borrowedAtUtc", out validDate);
        if (serial.Length == 0) { Error(context, 400, "serialNumber is required.", "invalid_serial"); return; }
        if (!validDate) { Error(context, 400, "Borrowed date and time is invalid.", "invalid_borrowed_at"); return; }
        var borrowedAtUtc = suppliedBorrowedAt ?? DateTime.UtcNow;
        if (borrowedAtUtc > DateTime.UtcNow.AddMinutes(1)) { Error(context, 400, "Borrowed date and time cannot be in the future.", "future_borrowed_at"); return; }
        if (departmentOnly && (!departmentId.HasValue || departmentId.Value <= 0)) { Error(context, 400, "Select a department or a borrowing employee.", "borrower_required"); return; }

        try
        {
            using (var connection = new SqlConnection(Cs()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var item = GetItem(connection, transaction, serial);
                    if (item == null) { transaction.Rollback(); Error(context, 404, "Item not found for the given serial number.", "item_not_found"); return; }
                    var existing = GetOpenByItem(connection, transaction, item.ItemId, true);
                    if (existing != null)
                    {
                        if (SameBorrower(existing, departmentOnly, employeeId, departmentId, identity.UserId, borrowedAtUtc))
                        {
                            transaction.Commit();
                            Ok(context, new { success = true, message = "Borrow already logged.", borrow = existing.ToDto() });
                            return;
                        }
                        transaction.Rollback();
                        Error(context, 409, "This item is already borrowed (open record exists).", "open_borrow_exists");
                        return;
                    }

                    var eligibility = EvaluateBorrowEligibility(connection, transaction, item);
                    if (!eligibility.Eligible)
                    {
                        transaction.Rollback();
                        var status = eligibility.Code == "borrow_eligibility_unavailable" ? 503 : 409;
                        Error(context, status, eligibility.Message, eligibility.Code);
                        return;
                    }

                    string borrowerName;
                    string departmentName;
                    int? resolvedDepartmentId;
                    int? resolvedEmployeeId;
                    if (departmentOnly)
                    {
                        if (!TryGetDepartment(connection, transaction, departmentId.Value, out departmentName)) { transaction.Rollback(); Error(context, 400, "Selected department was not found.", "department_not_found"); return; }
                        resolvedEmployeeId = null;
                        resolvedDepartmentId = departmentId;
                        borrowerName = departmentName + " (no specific employee)";
                    }
                    else
                    {
                        if (!TryGetEmployee(connection, transaction, employeeId.Value, out borrowerName, out resolvedDepartmentId, out departmentName)) { transaction.Rollback(); Error(context, 400, "Selected employee was not found or is inactive.", "employee_not_found"); return; }
                        resolvedEmployeeId = employeeId;
                    }

                    var encoderName = GetEncoderName(connection, transaction, identity);
                    int borrowId;
                    using (var command = new SqlCommand(@"
INSERT INTO dbo.BorrowLog(ItemId,SerialNumber,ItemName,ItemDescription,ModelNumber,BorrowedByEmpId,BorrowedByEmpName,BorrowedByDeptId,BorrowedByDeptName,BorrowEncodedByUserId,BorrowEncodedByUserName,BorrowedAtUtc)
VALUES(@ItemId,@SerialNumber,@ItemName,@ItemDescription,@ModelNumber,@BorrowedByEmpId,@BorrowedByEmpName,@BorrowedByDeptId,@BorrowedByDeptName,@BorrowEncodedByUserId,@BorrowEncodedByUserName,@BorrowedAtUtc);
SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                    {
                        Add(command, "@ItemId", item.ItemId); Add(command, "@SerialNumber", item.SerialNumber ?? serial);
                        Add(command, "@ItemName", string.IsNullOrWhiteSpace(item.Name) ? serial : item.Name.Trim()); Add(command, "@ItemDescription", item.Description); Add(command, "@ModelNumber", item.ModelNumber);
                        Add(command, "@BorrowedByEmpId", resolvedEmployeeId); Add(command, "@BorrowedByEmpName", borrowerName); Add(command, "@BorrowedByDeptId", resolvedDepartmentId); Add(command, "@BorrowedByDeptName", departmentName);
                        Add(command, "@BorrowEncodedByUserId", identity.UserId); Add(command, "@BorrowEncodedByUserName", encoderName); Add(command, "@BorrowedAtUtc", borrowedAtUtc);
                        borrowId = Convert.ToInt32(command.ExecuteScalar());
                    }
                    var created = GetBorrowById(connection, transaction, borrowId);
                    transaction.Commit();
                    Ok(context, new { success = true, message = "Borrow logged.", borrow = created.ToDto() });
                }
            }
        }
        catch (SqlException ex)
        {
            if (ex.Number != 2601 && ex.Number != 2627) throw;
            using (var connection = new SqlConnection(Cs()))
            {
                connection.Open();
                var existing = GetOpenBySerial(connection, serial);
                if (SameBorrower(existing, departmentOnly, employeeId, departmentId, identity.UserId, borrowedAtUtc))
                {
                    Ok(context, new { success = true, message = "Borrow already logged.", borrow = existing.ToDto() });
                    return;
                }
            }
            Error(context, 409, "This item is already borrowed (open record exists).", "open_borrow_exists");
        }
    }

    private static void Return(HttpContext context, MobileApiIdentity identity)
    {
        JObject body;
        try { body = JObject.Parse(Body(context)); } catch { Error(context, 400, "Invalid JSON.", "invalid_json"); return; }
        var borrowId = JsonInt(body, "borrowId") ?? 0;
        var employeeId = JsonInt(body, "returnedByEmpId");
        var departmentId = JsonInt(body, "returnedByDeptId");
        var departmentOnly = !employeeId.HasValue || employeeId.Value <= 0;
        if (borrowId <= 0) { Error(context, 400, "borrowId is required.", "invalid_borrow"); return; }
        if (departmentOnly && (!departmentId.HasValue || departmentId.Value <= 0)) { Error(context, 400, "Select a department or a returning employee.", "returner_required"); return; }

        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                var open = GetBorrowById(connection, transaction, borrowId);
                if (open == null) { transaction.Rollback(); Error(context, 404, "Borrow record not found.", "borrow_not_found"); return; }
                if (open.ReturnedAtUtc.HasValue) { transaction.Rollback(); Error(context, 409, "This borrow record is already returned.", "already_returned"); return; }

                string returnerName;
                string departmentName;
                int? resolvedDepartmentId;
                int? resolvedEmployeeId;
                if (departmentOnly)
                {
                    if (!TryGetDepartment(connection, transaction, departmentId.Value, out departmentName)) { transaction.Rollback(); Error(context, 400, "Selected department was not found.", "department_not_found"); return; }
                    resolvedEmployeeId = null;
                    resolvedDepartmentId = departmentId;
                    returnerName = departmentName + " (no specific employee)";
                }
                else
                {
                    if (!TryGetEmployee(connection, transaction, employeeId.Value, out returnerName, out resolvedDepartmentId, out departmentName)) { transaction.Rollback(); Error(context, 400, "Selected employee was not found or is inactive.", "employee_not_found"); return; }
                    resolvedEmployeeId = employeeId;
                }

                var encoderName = GetEncoderName(connection, transaction, identity);
                int affected;
                using (var command = new SqlCommand(@"
UPDATE dbo.BorrowLog SET ReturnedByEmpId=@ReturnedByEmpId,ReturnedByEmpName=@ReturnedByEmpName,ReturnedByDeptId=@ReturnedByDeptId,ReturnedByDeptName=@ReturnedByDeptName,ReturnEncodedByUserId=@ReturnEncodedByUserId,ReturnEncodedByUserName=@ReturnEncodedByUserName,ReturnedAtUtc=SYSUTCDATETIME()
WHERE BorrowId=@BorrowId AND ReturnedAtUtc IS NULL;", connection, transaction))
                {
                    Add(command, "@ReturnedByEmpId", resolvedEmployeeId); Add(command, "@ReturnedByEmpName", returnerName); Add(command, "@ReturnedByDeptId", resolvedDepartmentId); Add(command, "@ReturnedByDeptName", departmentName);
                    Add(command, "@ReturnEncodedByUserId", identity.UserId); Add(command, "@ReturnEncodedByUserName", encoderName); Add(command, "@BorrowId", borrowId);
                    affected = command.ExecuteNonQuery();
                }
                if (affected != 1) { transaction.Rollback(); Error(context, 409, "This borrow was already returned by someone else. Please refresh.", "already_returned"); return; }
                var returned = GetBorrowById(connection, transaction, borrowId);
                transaction.Commit();
                Ok(context, new { success = true, message = "Return logged.", borrow = returned.ToDto() });
            }
        }
    }

    private static void Delete(HttpContext context, MobileApiIdentity identity)
    {
        JObject body;
        try { body = JObject.Parse(Body(context)); } catch { Error(context, 400, "Invalid JSON.", "invalid_json"); return; }
        var borrowId = JsonInt(body, "borrowId") ?? 0;
        if (borrowId <= 0) { Error(context, 400, "borrowId is required.", "invalid_borrow"); return; }
        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            if (!CanDeleteOpenBorrow(connection, identity.UserId)) { Error(context, 403, "Delete Borrow is an admin-only action.", "forbidden"); return; }
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                var open = GetBorrowById(connection, transaction, borrowId);
                if (open == null) { transaction.Rollback(); Error(context, 404, "Borrow record not found.", "borrow_not_found"); return; }
                if (open.ReturnedAtUtc.HasValue) { transaction.Rollback(); Error(context, 409, "Only open borrow records can be deleted.", "closed_borrow"); return; }
                int affected;
                using (var command = new SqlCommand("DELETE FROM dbo.BorrowLog WHERE BorrowId=@BorrowId AND ReturnedAtUtc IS NULL;", connection, transaction)) { Add(command, "@BorrowId", borrowId); affected = command.ExecuteNonQuery(); }
                if (affected != 1) { transaction.Rollback(); Error(context, 409, "This borrow was already returned or deleted by someone else. Please refresh.", "concurrent_change"); return; }
                transaction.Commit();
                Ok(context, new { success = true, message = "Open borrow deleted.", borrow = open.ToDto() });
            }
        }
    }

    private static void Page(HttpContext context, MobileApiIdentity identity, bool history)
    {
        var query = (context.Request.QueryString["serialContains"] ?? string.Empty).Trim();
        var pageIndex = Math.Max(0, ReadInt(context.Request.QueryString["pageIndex"], 0));
        var pageSize = Math.Min(history ? 5000 : 2000, Math.Max(1, ReadInt(context.Request.QueryString["pageSize"], 25)));
        DateTime fromUtc = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime toUtc = DateTime.UtcNow.AddDays(1);
        if (history)
        {
            var fromText = context.Request.QueryString["fromUtc"];
            var toText = context.Request.QueryString["toUtc"];
            if (!string.IsNullOrWhiteSpace(fromText))
            {
                DateTime parsedFrom;
                if (!DateTime.TryParse(fromText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsedFrom)) { Error(context, 400, "fromUtc is invalid.", "invalid_date_range"); return; }
                fromUtc = parsedFrom;
            }
            if (!string.IsNullOrWhiteSpace(toText))
            {
                DateTime parsedTo;
                if (!DateTime.TryParse(toText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsedTo)) { Error(context, 400, "toUtc is invalid.", "invalid_date_range"); return; }
                toUtc = parsedTo;
            }
            if (toUtc <= fromUtc) { Error(context, 400, "History end date must be after the start date.", "invalid_date_range"); return; }
        }

        var where = history
            ? " WHERE b.ReturnedAtUtc IS NOT NULL AND b.BorrowedAtUtc>=@FromUtc AND b.BorrowedAtUtc<@ToUtc"
            : " WHERE b.ReturnedAtUtc IS NULL";
        if (query.Length > 0)
            where += " AND (b.SerialNumber LIKE @Query OR b.ItemName LIKE @Query OR b.ModelNumber LIKE @Query OR b.BorrowedByEmpName LIKE @Query OR b.BorrowedByDeptName LIKE @Query" + (history ? " OR b.ReturnedByEmpName LIKE @Query OR b.ReturnedByDeptName LIKE @Query" : string.Empty) + ")";

        using (var connection = new SqlConnection(Cs()))
        {
            connection.Open();
            var canDelete = CanDeleteOpenBorrow(connection, identity.UserId);
            int total;
            DateTime? oldest = null;
            using (var count = new SqlCommand("SELECT COUNT(1) AS TotalCount" + (history ? string.Empty : ",MIN(b.BorrowedAtUtc) AS OldestBorrowedAtUtc") + " FROM dbo.BorrowLog b" + where + ";", connection))
            {
                Add(count, "@Query", "%" + query + "%"); Add(count, "@FromUtc", fromUtc); Add(count, "@ToUtc", toUtc);
                using (var reader = count.ExecuteReader())
                {
                    reader.Read(); total = Convert.ToInt32(reader["TotalCount"]);
                    if (!history) oldest = reader["OldestBorrowedAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["OldestBorrowedAtUtc"]);
                }
            }
            var rows = new List<object>();
            using (var command = new SqlCommand("SELECT " + BorrowColumns + " FROM dbo.BorrowLog b" + where + " ORDER BY b.BorrowedAtUtc DESC,b.BorrowId DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", connection))
            {
                Add(command, "@Query", "%" + query + "%"); Add(command, "@FromUtc", fromUtc); Add(command, "@ToUtc", toUtc); Add(command, "@Offset", pageIndex * pageSize); Add(command, "@PageSize", pageSize);
                using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add(ReadBorrow(reader).ToDto());
            }
            Ok(context, new { totalCount = total, oldestBorrowedAtUtc = history ? (DateTime?)null : oldest, rows = rows, access = new { canDeleteOpenBorrow = !history && canDelete, canExportCsv = false } });
        }
    }

    public void ProcessRequest(HttpContext context)
    {
        var method = (context.Request.HttpMethod ?? "GET").ToUpperInvariant();
        if (method == "OPTIONS")
        {
            context.Response.StatusCode = 204;
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
            context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            return;
        }

        MobileApiIdentity identity;
        if (!TryGetIdentity(context, out identity))
        {
            Error(context, 401, "Your session has expired or is invalid. Please log in again.", "invalid_token");
            return;
        }

        var operation = (context.Request.QueryString["op"] ?? string.Empty).Trim().ToLowerInvariant();
        try
        {
            if (method == "GET" && operation == "companies") Companies(context);
            else if (method == "GET" && operation == "branches") Branches(context);
            else if (method == "GET" && operation == "departments") Departments(context);
            else if (method == "GET" && operation == "employees") Employees(context);
            else if (method == "POST" && operation == "employees") CreateEmployee(context);
            else if (method == "GET" && operation == "model") SearchModel(context);
            else if (method == "GET" && operation == "resolve") Resolve(context);
            else if (method == "POST" && operation == "borrow") Borrow(context, identity);
            else if (method == "POST" && operation == "return") Return(context, identity);
            else if (method == "POST" && operation == "delete") Delete(context, identity);
            else if (method == "GET" && operation == "open") Page(context, identity, false);
            else if (method == "GET" && operation == "history") Page(context, identity, true);
            else Error(context, 404, "Borrow operation was not found.", "not_found");
        }
        catch (Exception ex)
        {
            Error(context, 500, "Borrow service failed: " + ex.Message, "server_error");
        }
    }
}
