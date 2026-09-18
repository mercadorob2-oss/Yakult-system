using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>
/// Authentication and authorization shared by the API2 IT Call Monitoring handlers.
/// The legacy handlers previously accepted a caller-supplied userId and did not validate
/// the bearer token. This helper validates the same JWT issued by mobile-auth-login.ashx,
/// reloads the account from the database, and checks the existing Call IT access model.
/// </summary>
public sealed class CallTicketApiUser
{
    public int UserId { get; set; }
    public int? EmployeeId { get; set; }
    public string Username { get; set; }
    public bool IsItAuthorized { get; set; }
}

public static class CallTicketApiSecurity
{
    private const string DefaultSecret = "DEV_YAKULT_SECRET_7326_ABC123XYZ789";

    /// <summary>
    /// Requires a valid mobile JWT and an active account, but deliberately does not impose
    /// IT Call access. Repair requesters use this path for their own tickets; technician-only
    /// endpoints layer TryRequireIt on top.
    /// </summary>
    public static bool TryRequireAuthenticated(
        HttpContext context,
        string connectionString,
        out CallTicketApiUser user,
        out int statusCode,
        out string message)
    {
        user = null;
        statusCode = 401;
        message = "Unauthorized";

        var token = ReadBearerToken(context);
        if (string.IsNullOrWhiteSpace(token))
            return false;

        JObject claims;
        if (!TryDecodeJwt(token, out claims))
        {
            message = "Invalid or expired token";
            return false;
        }

        int userId;
        if (claims["sub"] == null || !int.TryParse(claims["sub"].ToString(), out userId) || userId <= 0)
        {
            message = "Token subject is invalid";
            return false;
        }

        try
        {
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
SELECT TOP 1
    u.UserId,
    u.EmpId,
    u.Name,
    CAST(CASE WHEN
        ISNULL(u.IsDeveloper, 0) = 1
        OR ISNULL(u.IsSuperAdmin, 0) = 1
        OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE 'IT%'
        OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%INFORMATION TECHNOLOGY%'
        OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%I.T.%'
        OR EXISTS (
            SELECT 1
            FROM dbo.UserRole ur
            INNER JOIN dbo.Role r ON r.RoleId = ur.RoleId
            WHERE ur.UserId = u.UserId
              AND ISNULL(r.IsActive, 1) = 1
              AND r.RoleName IN ('Admin', 'Developer', 'IT Manager', 'Supervisor', 'Tech Support')
        )
        THEN 1 ELSE 0 END AS bit) AS IsItAuthorized
FROM dbo.[User] u
LEFT JOIN dbo.Employee e ON e.EmpId = u.EmpId
LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
WHERE u.UserId = @UserId
  AND ISNULL(u.IsActive, 1) = 1", con))
                {
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            message = "Account is inactive or not found";
                            return false;
                        }

                        user = new CallTicketApiUser
                        {
                            UserId = Convert.ToInt32(reader["UserId"]),
                            EmployeeId = reader["EmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["EmpId"]),
                            Username = reader["Name"] == DBNull.Value ? string.Empty : reader["Name"].ToString(),
                            IsItAuthorized = reader["IsItAuthorized"] != DBNull.Value && Convert.ToBoolean(reader["IsItAuthorized"])
                        };
                        return true;
                    }
                }
            }
        }
        catch
        {
            statusCode = 503;
            message = "Authorization service is unavailable";
            return false;
        }
    }

    public static bool TryRequireIt(
        HttpContext context,
        string connectionString,
        out CallTicketApiUser user,
        out int statusCode,
        out string message)
    {
        if (!TryRequireAuthenticated(context, connectionString, out user, out statusCode, out message))
            return false;

        if (!user.IsItAuthorized)
        {
            user = null;
            statusCode = 403;
            message = "This account is not authorized for IT Call Monitoring";
            return false;
        }

        return true;
    }

    public static bool IsItEmployee(SqlConnection connection, int employeeId)
    {
        using (var cmd = new SqlCommand(@"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM dbo.Employee e
    LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
    WHERE e.EmpId = @EmployeeId
      AND ISNULL(e.Active, 1) = 1
      AND (
          UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE 'IT%'
          OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%INFORMATION TECHNOLOGY%'
          OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%I.T.%'
      )
) THEN 1 ELSE 0 END", connection))
        {
            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }
    }

    private static string ReadBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers["Authorization"] ?? string.Empty;
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization.Substring(7).Trim()
            : string.Empty;
    }

    private static bool TryDecodeJwt(string token, out JObject claims)
    {
        claims = null;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var signingInput = parts[0] + "." + parts[1];
            byte[] expected;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
                ConfigurationManager.AppSettings["AuthTokenSecret"] ?? DefaultSecret)))
            {
                expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
            }

            var supplied = Base64UrlDecode(parts[2]);
            if (supplied == null || expected.Length != supplied.Length) return false;
            var difference = 0;
            for (var i = 0; i < expected.Length; i++) difference |= expected[i] ^ supplied[i];
            if (difference != 0) return false;

            claims = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            var exp = claims["exp"] == null ? null : claims["exp"].ToString();
            DateTime expires;
            if (!string.IsNullOrWhiteSpace(exp) &&
                DateTime.TryParse(exp, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out expires) && expires <= DateTime.UtcNow)
                return false;

            return true;
        }
        catch
        {
            claims = null;
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
            case 1: return null;
        }
        return Convert.FromBase64String(normalized);
    }
}
