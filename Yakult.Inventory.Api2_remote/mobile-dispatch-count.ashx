<%@ WebHandler Language="C#" Class="MobileDispatchCountHandler" %>

using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MobileDispatchCountHandler : IHttpHandler
{
    static string Cs() { var cs = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]; return cs == null ? "" : cs.ConnectionString; }
    static string Secret() { return ConfigurationManager.AppSettings["AuthTokenSecret"] ?? "DEV_YAKULT_SECRET_7326_ABC123XYZ789"; }
    public bool IsReusable { get { return false; } }
    void J(HttpContext c, int s, object d) { c.Response.StatusCode = s; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m, string code) { J(c, s, new { success = false, message = m, error = code }); }
    static string B64E(byte[] d) { return Convert.ToBase64String(d).TrimEnd('=').Replace('+','-').Replace('/','_'); }
    static byte[] B64D(string s) { s=s.Replace('-','+').Replace('_','/'); switch(s.Length%4){case 2:s+="==";break;case 3:s+="=";break;} return Convert.FromBase64String(s); }
    JObject DecodeJwt(string t) { try { var p=t.Split('.'); if(p.Length!=3)return null; var si=p[0]+"."+p[1]; using(var m=new HMACSHA256(Encoding.UTF8.GetBytes(Secret()))){if(B64E(m.ComputeHash(Encoding.UTF8.GetBytes(si)))!=p[2])return null;} var j=JObject.Parse(Encoding.UTF8.GetString(B64D(p[1]))); var e=j["exp"] == null ? null : j["exp"].ToString(); return e!=null&&DateTime.Parse(e)<DateTime.UtcNow?null:j; } catch{return null;} }
    bool Auth(HttpContext c) { var a=c.Request.Headers["Authorization"] ?? ""; var t=a.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)?a.Substring(7).Trim():""; if(string.IsNullOrEmpty(t)||DecodeJwt(t)==null){Err(c,401,"Your session has expired or is invalid. Please log in again.","invalid_token");return false;} return true; }
    public void ProcessRequest(HttpContext c)
    {
        if ((c.Request.HttpMethod ?? "GET").Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)) { c.Response.StatusCode = 204; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.End(); return; }
        if (!Auth(c)) return;
        try { using(var con=new SqlConnection(Cs())) { con.Open(); using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.[Set] WHERE Status='Dispatched' AND CAST(DispatchDate AS DATE)=CAST(GETDATE() AS DATE)",con)) J(c,200,new{todayCount=(int)cmd.ExecuteScalar()}); } }
        catch(Exception ex) { Err(c,500,ex.Message,"server_error"); }
    }
}