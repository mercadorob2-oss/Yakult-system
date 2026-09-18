<%@ WebHandler Language="C#" Class="RepairTicketLookupHandler" %>
using System;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Standalone handler — deliberately NOT routed through api.ashx's "/api/..." router. On this
// server, IIS has a separate application mounted at the "/api" path that intercepts every
// "/api/..." request before Global.asax's rewrite-to-api.ashx logic ever runs, so anything added
// to api.ashx under an "api/..." route is unreachable. Hit directly instead, mirroring
// set-image-upload.ashx/receipt-upload.ashx's proven-working standalone pattern. JWT-gated (not
// open/QR-token-only) since this is step one of a flow that lets a logged-in technician write
// data (UploadPartPhoto), so both endpoints share the same auth requirement.
public class RepairTicketLookupHandler : IHttpHandler {
    string Cs() { var cs = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]; return cs == null ? "" : cs.ConnectionString; }
    static string DbString(object v) { return v == null || v == DBNull.Value ? null : v.ToString(); }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.TrySkipIisCustomErrors=true; c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.TrySkipIisCustomErrors=true; c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    bool Auth(HttpContext c) {
        CallTicketApiUser actor; int status; string message;
        if(!CallTicketApiSecurity.TryRequireIt(c,Cs(),out actor,out status,out message)) { Err(c,status,message); return false; }
        return true;
    }

    public void ProcessRequest(HttpContext c) {
        c.Response.ContentType="application/json";
        c.Response.TrySkipIisCustomErrors=true;
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=204;c.Response.End();return;}
        if(!Auth(c)) return;

        var ticketCode=(c.Request.QueryString["ticketCode"]??"").Trim();
        var token=(c.Request.QueryString["token"]??"").Trim();
        if(ticketCode=="null")ticketCode="";
        if(token=="null")token="";
        if(string.IsNullOrEmpty(ticketCode)&&string.IsNullOrEmpty(token)){Err(c,400,"ticketCode or token is required");return;}

        Guid? qrToken=null;
        if(!string.IsNullOrEmpty(token)){
            const string prefix="yakult:repair:v1:";
            var t=token.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)?token.Substring(prefix.Length).Trim():token;
            Guid g;
            if(!Guid.TryParse(t,out g)){Err(c,400,"Invalid token format");return;}
            qrToken=g;
        }

        try {
            using(var con=new SqlConnection(Cs())) {
                con.Open();
                var where=qrToken.HasValue?"t.QRToken=@Token":"t.TicketCode=@TicketCode";
                int repairTicketId=0; string ticketCodeOut=null,statusOut=null,itemNameOut=null,serialOut=null,modelOut=null;
                using(var cmd=new SqlCommand("SELECT t.RepairTicketId,t.TicketCode,t.Status,i.Name AS ItemName,i.SerialNumber,i.ModelNumber FROM dbo.RepairTicket t LEFT JOIN dbo.Item i ON i.ItemId=t.ItemId WHERE "+where,con)) {
                    if(qrToken.HasValue) cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken.Value;
                    else cmd.Parameters.Add("@TicketCode",SqlDbType.NVarChar,50).Value=ticketCode;
                    using(var rd=cmd.ExecuteReader()) {
                        if(!rd.Read()){Err(c,404,"Repair ticket not found");return;}
                        repairTicketId=(int)rd["RepairTicketId"];
                        ticketCodeOut=DbString(rd["TicketCode"]); statusOut=DbString(rd["Status"]);
                        itemNameOut=DbString(rd["ItemName"]); serialOut=DbString(rd["SerialNumber"]); modelOut=DbString(rd["ModelNumber"]);
                    }
                }

                var parts=new List<object>();
                using(var cmd=new SqlCommand("SELECT RepairPartId,PartNumber,PartDisplayName,Status FROM dbo.RepairPart WHERE RepairTicketId=@Id ORDER BY PartNumber",con)) {
                    cmd.Parameters.AddWithValue("@Id",repairTicketId);
                    using(var rd=cmd.ExecuteReader())
                        while(rd.Read())
                            parts.Add(new{repairPartId=rd["RepairPartId"],partNumber=rd["PartNumber"],partDisplayName=DbString(rd["PartDisplayName"]),status=DbString(rd["Status"])});
                }

                // Same 50 MB per-ticket evidence cap enforced by repair-part-photo-upload.ashx —
                // returned here so the mobile app can show "X MB used of 50 MB" before the
                // technician even tries to upload, not just reject them after the fact.
                long usedBytes=0;
                using(var cmd=new SqlCommand(
                    "SELECT ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes,0) AS BIGINT)) FROM dbo.RepairTicketAttachment WHERE RepairTicketId=@Id),0) + " +
                    "ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes,0) AS BIGINT)) FROM dbo.RepairPartAttachment WHERE RepairTicketId=@Id),0)",con)) {
                    cmd.Parameters.AddWithValue("@Id",repairTicketId);
                    var result=cmd.ExecuteScalar();
                    usedBytes = result==null||result==DBNull.Value ? 0L : Convert.ToInt64(result);
                }

                var ticket=new{
                    repairTicketId,ticketCode=ticketCodeOut,status=statusOut,itemName=itemNameOut,serialNumber=serialOut,modelNumber=modelOut,
                    evidenceBytesUsed=usedBytes, evidenceBytesLimit=MaxTicketEvidenceBytes
                };

                Ok(c,new{success=true,ticket,parts});
            }
        } catch { Err(c,503,"Repair ticket lookup is temporarily unavailable"); }
    }

    public const long MaxTicketEvidenceBytes = 50L*1024*1024;

    public bool IsReusable{get{return false;}}
}
