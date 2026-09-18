<%@ WebHandler Language="C#" Class="MobileBorrowHomeHandler" %>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MobileBorrowHomeHandler : IHttpHandler
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
        int rc; if (!int.TryParse(c.Request.QueryString["recentCount"] ?? "5", out rc)) rc = 5; if (rc < 1) rc = 1; if (rc > 25) rc = 25;
        try {
            using (var con = new SqlConnection(Cs())) { con.Open();
                int oc=0, odc=0, rt=0; object oldest=null;
                using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL",con)) oc=(int)cmd.ExecuteScalar();
                using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL AND BorrowedAtUtc<DATEADD(day,-7,GETUTCDATE())",con)) odc=(int)cmd.ExecuteScalar();
                using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc>=CAST(GETUTCDATE() AS DATE)",con)) rt=(int)cmd.ExecuteScalar();
                using(var cmd=new SqlCommand("SELECT MIN(BorrowedAtUtc) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL",con)) { var v=cmd.ExecuteScalar(); if(v!=DBNull.Value) oldest=v; }
                var recent=new List<object>();
                using(var cmd=new SqlCommand("SELECT TOP(@N) BorrowId,ItemId,SerialNumber,ItemName,ItemDescription,ModelNumber,BorrowedByEmpId,BorrowedByEmpName,BorrowedByDeptId,BorrowedByDeptName,BorrowEncodedByUserId,BorrowEncodedByUserName,BorrowedAtUtc,ReturnedAtUtc FROM dbo.BorrowLog ORDER BY BorrowedAtUtc DESC",con)){
                    cmd.Parameters.AddWithValue("@N",rc);
                    using(var r=cmd.ExecuteReader()) while(r.Read()) recent.Add(new{borrowId=r["BorrowId"],itemId=r["ItemId"],serialNumber=r["SerialNumber"],itemName=r["ItemName"],itemDescription=r["ItemDescription"],modelNumber=r["ModelNumber"],borrowedByEmpId=r["BorrowedByEmpId"],borrowedByEmpName=r["BorrowedByEmpName"],borrowedByDeptId=r["BorrowedByDeptId"],borrowedByDeptName=r["BorrowedByDeptName"],borrowEncodedByUserId=r["BorrowEncodedByUserId"],borrowEncodedByUserName=r["BorrowEncodedByUserName"],borrowedAtUtc=r["BorrowedAtUtc"],returnedAtUtc=r["ReturnedAtUtc"],isOpen=r["ReturnedAtUtc"]==DBNull.Value});
                }
                J(c,200,new{openCount=oc,overdueCount=odc,returnedTodayCount=rt,oldestOpenBorrowedAtUtc=oldest,recentRows=recent,access=new{canDeleteOpenBorrow=false,canExportCsv=false}});
            }
        } catch(Exception ex) { Err(c,500,ex.Message,"server_error"); }
    }
}