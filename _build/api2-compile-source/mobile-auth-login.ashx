<%@ WebHandler Language="C#" Class="MobileAuthLoginHandler" %>

using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MobileAuthLoginHandler : IHttpHandler
{
    static string Cs() { var cs = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]; return cs == null ? "" : cs.ConnectionString; }
    static string Secret() { return ConfigurationManager.AppSettings["AuthTokenSecret"] ?? "DEV_YAKULT_SECRET_7326_ABC123XYZ789"; }
    static int ExpHrs() { int h; return int.TryParse(ConfigurationManager.AppSettings["AuthTokenExpiryHours"], out h) ? h : 12; }

    public bool IsReusable { get { return false; } }

    void J(HttpContext c, int s, object d) { c.Response.StatusCode = s; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Ok(HttpContext c, object d) { J(c, 200, d); }
    void Err(HttpContext c, int s, string m, string code) { J(c, s, new { success = false, message = m, error = code }); }
    string Body(HttpContext c) { using (var r = new StreamReader(c.Request.InputStream, c.Request.ContentEncoding)) return r.ReadToEnd(); }

    static string B64E(byte[] d) { return Convert.ToBase64String(d).TrimEnd('=').Replace('+','-').Replace('/','_'); }
    string MakeJwt(int uid, string uname, string name, string email) {
        var h = B64E(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { alg = "HS256", typ = "JWT" })));
        var exp = DateTime.UtcNow.AddHours(ExpHrs()).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var p = B64E(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { sub = uid, username = uname, name = name, email = email, exp = exp })));
        var si = h + "." + p;
        using (var m = new HMACSHA256(Encoding.UTF8.GetBytes(Secret()))) return si + "." + B64E(m.ComputeHash(Encoding.UTF8.GetBytes(si)));
    }

    static byte[] Salt() { var b = new byte[16]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(b); return b; }
    static byte[] Hash(string pw, byte[] sl) { using (var s = SHA256.Create()) { var pb = Encoding.UTF8.GetBytes(pw); var d = new byte[pb.Length + sl.Length]; Buffer.BlockCopy(pb, 0, d, 0, pb.Length); Buffer.BlockCopy(sl, 0, d, pb.Length, sl.Length); return s.ComputeHash(d); } }
    static bool Same(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; var diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0; }
    static bool Verify(string pw, byte[] h, byte[] s) { return h != null && s != null && Same(Hash(pw, s), h); }
    static void MigrateUserPassword(SqlConnection con, int userId, string pw) {
        var ns = Salt(); var nh = Hash(pw, ns);
        using (var cmd = new SqlCommand("UPDATE dbo.[User] SET PasswordHash=@H,PasswordSalt=@S,[Password]=NULL WHERE UserId=@Id", con)) {
            cmd.Parameters.AddWithValue("@H", nh); cmd.Parameters.AddWithValue("@S", ns); cmd.Parameters.AddWithValue("@Id", userId); cmd.ExecuteNonQuery();
        }
    }

    public void ProcessRequest(HttpContext c)
    {
        if ((c.Request.HttpMethod ?? "GET").Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)) { c.Response.StatusCode = 204; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.End(); return; }
        if (!(c.Request.HttpMethod ?? "GET").Equals("POST", StringComparison.OrdinalIgnoreCase)) { Err(c, 405, "Method not allowed", "method_not_allowed"); return; }

        JObject j;
        try { j = JObject.Parse(Body(c)); } catch { Err(c, 400, "Invalid JSON", "invalid_json"); return; }
        var u = (j["username"] ?? "").ToString().Trim();
        var pw = (j["password"] ?? "").ToString().Trim();
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(pw)) { Err(c, 400, "Username and password required", "missing_credentials"); return; }

        try {
            using (var con = new SqlConnection(Cs())) { con.Open();
                int? uid = null; string nm = null, em = null; string dbg = "no_user_found";

                int? userId = null; string userName = null, userEmail = null; bool hasLegacy = false; byte[] userHash = null, userSalt = null;
                using (var cmd = new SqlCommand(@"SELECT UserId,Name,EmailAddress,PasswordHash,PasswordSalt,CASE WHEN [Password] IS NOT NULL THEN 1 ELSE 0 END AS HasLegacy FROM dbo.[User] WHERE Name COLLATE Latin1_General_CS_AS=@U COLLATE Latin1_General_CS_AS AND ISNULL(IsActive,1)=1", con)) {
                    cmd.Parameters.AddWithValue("@U", u);
                    using (var r = cmd.ExecuteReader()) if (r.Read()) { userId = Convert.ToInt32(r["UserId"]); userName = r["Name"] == DBNull.Value ? null : r["Name"].ToString(); userEmail = r["EmailAddress"] == DBNull.Value ? null : r["EmailAddress"].ToString(); userHash = r["PasswordHash"] as byte[]; userSalt = r["PasswordSalt"] as byte[]; hasLegacy = Convert.ToInt32(r["HasLegacy"]) == 1; }
                }

                if (userId != null) {
                    int legacyMatch = 0;
                    if (hasLegacy) using (var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.[User] WHERE UserId=@Id AND [Password]=CONVERT(VARBINARY(MAX),@Pw)", con)) { cmd.Parameters.AddWithValue("@Id", userId.Value); cmd.Parameters.AddWithValue("@Pw", pw); legacyMatch = Convert.ToInt32(cmd.ExecuteScalar()); }
                    if (legacyMatch > 0) { MigrateUserPassword(con, userId.Value, pw); uid = userId; nm = userName; em = userEmail; }
                    else if (Verify(pw, userHash, userSalt)) { uid = userId; nm = userName; em = userEmail; }
                    else dbg = "user_found_pw_mismatch";
                }

                if (uid == null) {
                    int? deptId = null, deptUserId = null; string deptName = null, deptEmail = null; byte[] deptUserHash = null, deptUserSalt = null, deptHash = null, deptSalt = null;
                    using (var cmd = new SqlCommand(@"SELECT da.Id,u.UserId,u.Name,u.EmailAddress,u.PasswordHash,u.PasswordSalt,da.PasswordHash AS DaHash,da.PasswordSalt AS DaSalt FROM dbo.DepartmentAccount da INNER JOIN dbo.[User] u ON u.UserId=da.UserId WHERE da.Username COLLATE Latin1_General_CS_AS=@U COLLATE Latin1_General_CS_AS AND da.UserId IS NOT NULL AND ISNULL(da.IsActive,1)=1 AND ISNULL(u.IsActive,1)=1", con)) {
                        cmd.Parameters.AddWithValue("@U", u); using (var r = cmd.ExecuteReader()) if (r.Read()) { deptId = Convert.ToInt32(r["Id"]); deptUserId = Convert.ToInt32(r["UserId"]); deptName = r["Name"] == DBNull.Value ? null : r["Name"].ToString(); deptEmail = r["EmailAddress"] == DBNull.Value ? null : r["EmailAddress"].ToString(); deptUserHash = r["PasswordHash"] as byte[]; deptUserSalt = r["PasswordSalt"] as byte[]; deptHash = r["DaHash"] as byte[]; deptSalt = r["DaSalt"] as byte[]; }
                    }
                    if (deptUserId != null) {
                        var ok = Verify(pw, deptUserHash, deptUserSalt) || Verify(pw, deptHash, deptSalt);
                        if (ok) { uid = deptUserId; nm = deptName; em = deptEmail; if (deptUserHash == null || deptUserSalt == null) { var ns = Salt(); var nh = Hash(pw, ns); using (var up = new SqlCommand("UPDATE dbo.[User] SET PasswordHash=@H,PasswordSalt=@S WHERE UserId=@Id; UPDATE dbo.DepartmentAccount SET PasswordHash=@H,PasswordSalt=@S WHERE Id=@Did", con)) { up.Parameters.AddWithValue("@H", nh); up.Parameters.AddWithValue("@S", ns); up.Parameters.AddWithValue("@Id", uid.Value); up.Parameters.AddWithValue("@Did", deptId.Value); up.ExecuteNonQuery(); } } }
                        else dbg = "dept_found_pw_mismatch";
                    }
                }

                if (uid != null) { var tk = MakeJwt(uid.Value, u, nm ?? u, em ?? ""); Ok(c, new { success = true, message = "Login successful", token = tk, expiresUtc = DateTime.UtcNow.AddHours(ExpHrs()).ToString("yyyy-MM-ddTHH:mm:ssZ"), userId = uid.Value, name = nm ?? u, email = em ?? "" }); }
                else J(c, 401, new { success = false, message = "Invalid username or password", error = "invalid_credentials", debug = dbg });
            }
        } catch (Exception ex) { Err(c, 500, ex.Message, "server_error"); }
    }
}