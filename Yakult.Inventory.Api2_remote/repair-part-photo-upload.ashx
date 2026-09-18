<%@ WebHandler Language="C#" Class="RepairPartPhotoUploadHandler" %>
using System;
using System.Web;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Standalone handler — see repair-ticket-lookup.ashx for why this isn't routed through
// api.ashx's "/api/..." router (that path is intercepted by a separate IIS application on this
// server before Global.asax ever runs). Mirrors set-image-upload.ashx's base64-JSON upload
// pattern, but JWT-gated (not open) since this writes permanently to dbo.RepairPartAttachment
// via the same stored proc the WPF desktop app's AddPartAttachmentAsync already uses.
public class RepairPartPhotoUploadHandler : IHttpHandler {
    string Cs() { var cs = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]; return cs == null ? "" : cs.ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.TrySkipIisCustomErrors=true; c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.TrySkipIisCustomErrors=true; c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }
    string Body(HttpContext c) { using (var r = new StreamReader(c.Request.InputStream, c.Request.ContentEncoding)) return r.ReadToEnd(); }
    static string TokString(JToken t) { return t == null ? null : t.ToString(); }
    static int TokInt(JToken t) { return t == null ? 0 : t.Value<int>(); }

    CallTicketApiUser _actor;
    bool Auth(HttpContext c) {
        int status; string message;
        if(!CallTicketApiSecurity.TryRequireIt(c,Cs(),out _actor,out status,out message)) { Err(c,status,message); return false; }
        return true;
    }
    int ActorUserId() { return _actor == null ? 0 : _actor.UserId; }

    public void ProcessRequest(HttpContext c) {
        c.Response.ContentType="application/json";
        c.Response.TrySkipIisCustomErrors=true;
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=204;c.Response.End();return;}
        if(c.Request.HttpMethod!="POST"){Err(c,405,"Only POST is allowed");return;}
        if(!Auth(c)) return;
        if(c.Request.ContentLength > MaxRequestBytes){Err(c,413,"The evidence upload request is too large");return;}

        JObject j;
        try { j = JObject.Parse(Body(c)); } catch { Err(c,400,"Invalid JSON"); return; }

        var partId = TokInt(j["repair_part_id"]);
        var imageBase64 = TokString(j["image_base64"]);
        if(partId==0 || string.IsNullOrWhiteSpace(imageBase64)){Err(c,400,"repair_part_id and image_base64 are required");return;}

        var mimeType = (TokString(j["mime_type"]) ?? "image/jpeg").Trim().ToLowerInvariant();
        var fileName = TokString(j["file_name"]);

        byte[] bytes;
        try {
            var data = imageBase64.Contains(",") ? imageBase64.Substring(imageBase64.IndexOf(',')+1) : imageBase64;
            bytes = Convert.FromBase64String(data);
        } catch { Err(c,400,"Invalid image data"); return; }

        if(bytes.Length == 0 || bytes.Length > MaxFileBytes){Err(c,413,"Each evidence file must be no larger than 20 MB");return;}
        if(!IsAllowedMime(mimeType)){Err(c,400,"This evidence file type is not allowed");return;}
        var attachmentType = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image" : (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "Video" : "Document");
        var defaultExt = attachmentType == "Video" ? "mp4" : (attachmentType == "Image" ? "jpg" : "pdf");
        if(string.IsNullOrWhiteSpace(fileName))
            fileName = string.Format("mobile_{0:yyyyMMdd_HHmmss}_{1}.{2}", DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0,8), defaultExt);
        else
            fileName = Path.GetFileName(fileName).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        if(string.IsNullOrWhiteSpace(fileName))
            fileName = string.Format("mobile_{0:yyyyMMdd_HHmmss}_{1}.{2}", DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0,8), defaultExt);

        try {
            using(var con=new SqlConnection(Cs())) {
                con.Open();

                int repairTicketId=0;
                using(var chk=new SqlCommand("SELECT RepairTicketId FROM dbo.RepairPart WHERE RepairPartId=@Id",con)) {
                    chk.Parameters.AddWithValue("@Id",partId);
                    var v=chk.ExecuteScalar();
                    if(v==null||v==DBNull.Value){Err(c,404,"Repair part not found");return;}
                    repairTicketId=(int)v;
                }

                // 50 MB per-ticket evidence cap — same figure repair-ticket-lookup.ashx reports so
                // the mobile app can show usage before the technician even tries to upload; this is
                // the actual enforcement point (the client-side check is just a courtesy heads-up).
                long usedBytes;
                using(var cmd=new SqlCommand(
                    "SELECT ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes,0) AS BIGINT)) FROM dbo.RepairTicketAttachment WHERE RepairTicketId=@Id),0) + " +
                    "ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes,0) AS BIGINT)) FROM dbo.RepairPartAttachment WHERE RepairTicketId=@Id),0)",con)) {
                    cmd.Parameters.AddWithValue("@Id",repairTicketId);
                    var result=cmd.ExecuteScalar();
                    usedBytes = result==null||result==DBNull.Value ? 0L : Convert.ToInt64(result);
                }

                if(usedBytes+bytes.Length > MaxTicketEvidenceBytes){
                    var usedMb=usedBytes/(1024*1024);
                    var limitMb=MaxTicketEvidenceBytes/(1024*1024);
                    Err(c,413,string.Format("This ticket has reached its {0} MB evidence limit ({1} MB used). Delete some existing evidence or contact IT to raise the limit.",limitMb,usedMb));
                    return;
                }

                using(var cmd=new SqlCommand("dbo.sp_RepairPortal_AddPartAttachment",con){CommandType=CommandType.StoredProcedure}) {
                    cmd.Parameters.AddWithValue("@RepairPartId",partId);
                    cmd.Parameters.AddWithValue("@AttachmentType",attachmentType);
                    cmd.Parameters.AddWithValue("@FileName",fileName);
                    cmd.Parameters.AddWithValue("@MimeType",mimeType);
                    cmd.Parameters.AddWithValue("@FileBytes",bytes);
                    cmd.Parameters.AddWithValue("@FileSizeBytes",bytes.Length);
                    cmd.Parameters.AddWithValue("@UploadedByUserId", ActorUserId() > 0 ? (object)ActorUserId() : DBNull.Value);
                    using(var rd=cmd.ExecuteReader()) {
                        if(rd.Read()){
                            Ok(c,new{
                                success=true,
                                partAttachmentId=rd.GetInt32(0),
                                evidenceBytesUsed=usedBytes+bytes.Length,
                                evidenceBytesLimit=MaxTicketEvidenceBytes
                            });
                            return;
                        }
                    }
                }
                Err(c,500,"Upload failed");
            }
        } catch { Err(c,503,"Evidence upload could not be completed"); }
    }

    public const long MaxTicketEvidenceBytes = 50L*1024*1024;
    public const int MaxFileBytes = 20*1024*1024;
    public const int MaxRequestBytes = 30*1024*1024;

    static bool IsAllowedMime(string mimeType) {
        switch((mimeType ?? string.Empty).Trim().ToLowerInvariant()) {
            case "image/jpeg":
            case "image/png":
            case "image/webp":
            case "image/gif":
            case "video/mp4":
            case "video/webm":
            case "video/quicktime":
            case "application/pdf":
            case "text/plain":
            case "application/msword":
            case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
            case "application/vnd.ms-excel":
            case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                return true;
            default:
                return false;
        }
    }

    public bool IsReusable{get{return false;}}
}
