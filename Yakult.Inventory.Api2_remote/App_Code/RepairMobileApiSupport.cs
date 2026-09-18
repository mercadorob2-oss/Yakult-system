using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Shared, deliberately small infrastructure for the JWT-protected mobile Repair Portal handlers.
/// The handlers keep their read/write SQL explicit so their contracts remain reviewable, while this
/// class centralizes safe JSON responses, request parsing, identity-based access checks, and ISO UTC
/// formatting. It intentionally does not add permissive wildcard CORS headers: the native scanner
/// does not need them, and browser origin policy belongs in deployment configuration.
/// </summary>
public static class RepairMobileApiSupport
{
    public static string ConnectionString
    {
        get
        {
            var setting = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"];
            return setting == null ? string.Empty : setting.ConnectionString;
        }
    }

    public static void Prepare(HttpContext context, string methods)
    {
        context.Response.ContentType = "application/json";
        context.Response.TrySkipIisCustomErrors = true;
        context.Response.AddHeader("X-Content-Type-Options", "nosniff");
        context.Response.AddHeader("Cache-Control", "no-store");
        if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 204;
            context.Response.AddHeader("Allow", methods);
        }
    }

    public static bool IsOptions(HttpContext context)
    {
        return string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase);
    }

    public static void Ok(HttpContext context, object payload)
    {
        Write(context, 200, payload);
    }

    public static void Created(HttpContext context, object payload)
    {
        Write(context, 201, payload);
    }

    public static void Error(HttpContext context, int statusCode, string message)
    {
        Write(context, statusCode, new { success = false, message = message });
    }

    public static void Write(HttpContext context, int statusCode, object payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.TrySkipIisCustomErrors = true;
        context.Response.Write(JsonConvert.SerializeObject(payload));
    }

    public static bool TryReadObject(HttpContext context, out JObject request)
    {
        request = null;
        try
        {
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                var body = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(body)) return false;
                request = JObject.Parse(body);
                return true;
            }
        }
        catch
        {
            request = null;
            return false;
        }
    }

    public static string Text(JObject request, string property, int maxLength)
    {
        if (request == null || request[property] == null || request[property].Type == JTokenType.Null)
            return null;
        var value = request[property].ToString().Trim();
        if (value.Length > maxLength) value = value.Substring(0, maxLength);
        return value;
    }

    public static int? Int(JObject request, string property)
    {
        if (request == null || request[property] == null || request[property].Type == JTokenType.Null)
            return null;
        int value;
        return int.TryParse(request[property].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? (int?)value : null;
    }

    public static bool Bool(JObject request, string property, bool fallback)
    {
        if (request == null || request[property] == null || request[property].Type == JTokenType.Null)
            return fallback;
        bool value;
        return bool.TryParse(request[property].ToString(), out value) ? value : fallback;
    }

    public static DateTime? UtcDate(JObject request, string property)
    {
        var value = Text(request, property, 64);
        if (string.IsNullOrWhiteSpace(value)) return null;
        DateTime parsed;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
            return null;
        return parsed;
    }

    public static object DbValue(object value)
    {
        return value ?? DBNull.Value;
    }

    public static string IsoUtc(object value)
    {
        if (value == null || value == DBNull.Value) return null;
        var date = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        if (date.Kind == DateTimeKind.Unspecified)
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return date.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }

    public static string IsoDate(object value)
    {
        if (value == null || value == DBNull.Value) return null;
        return Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string StringValue(IDataRecord record, string column)
    {
        var ordinal = record.GetOrdinal(column);
        return record.IsDBNull(ordinal) ? null : Convert.ToString(record.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static int? IntValue(IDataRecord record, string column)
    {
        var ordinal = record.GetOrdinal(column);
        return record.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(record.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static bool TryRequireAuthenticated(HttpContext context, out CallTicketApiUser actor)
    {
        int statusCode;
        string message;
        if (!CallTicketApiSecurity.TryRequireAuthenticated(context, ConnectionString, out actor, out statusCode, out message))
        {
            Error(context, statusCode, message);
            return false;
        }
        return true;
    }

    public static bool TryRequireTechnician(HttpContext context, out CallTicketApiUser actor)
    {
        int statusCode;
        string message;
        if (!CallTicketApiSecurity.TryRequireIt(context, ConnectionString, out actor, out statusCode, out message))
        {
            Error(context, statusCode, message);
            return false;
        }
        return true;
    }

    /// <summary>Requester visibility is deliberately based on server identity, never a userId from
    /// the client. IT-authorized users can read the technician queue and any individual ticket.</summary>
    public static bool CanReadTicket(SqlConnection connection, int repairTicketId, CallTicketApiUser actor)
    {
        const string sql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM dbo.RepairTicket t
    WHERE t.RepairTicketId = @RepairTicketId
      AND (
          @IsTechnician = 1
          OR t.SubmittedByUserId = @UserId
          OR (@EmployeeId IS NOT NULL AND t.RequestedByEmpId = @EmployeeId)
      )
) THEN 1 ELSE 0 END;";

        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = repairTicketId;
            command.Parameters.Add("@IsTechnician", SqlDbType.Bit).Value = actor.IsItAuthorized;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = actor.EmployeeId.HasValue
                ? (object)actor.EmployeeId.Value : DBNull.Value;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }
    }

    public static bool HasColumn(SqlConnection connection, string tableName, string columnName)
    {
        using (var command = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@TableName, @ColumnName) IS NULL THEN 0 ELSE 1 END;", connection))
        {
            command.Parameters.Add("@TableName", SqlDbType.NVarChar, 256).Value = tableName;
            command.Parameters.Add("@ColumnName", SqlDbType.NVarChar, 128).Value = columnName;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }
    }

    public static bool HasProcedure(SqlConnection connection, string procedureName)
    {
        using (var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@Name, 'P') IS NULL THEN 0 ELSE 1 END;", connection))
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = procedureName;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }
    }

    public static bool HasTable(SqlConnection connection, string tableName)
    {
        using (var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@Name, 'U') IS NULL THEN 0 ELSE 1 END;", connection))
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = tableName;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }
    }

    public static int ClampPageSize(string raw, int fallback, int maximum)
    {
        int value;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return fallback;
        return Math.Max(1, Math.Min(maximum, value));
    }

    public static int ClampPage(string raw)
    {
        int value;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0 ? value : 1;
    }
}
