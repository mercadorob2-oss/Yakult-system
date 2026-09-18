<%@ WebHandler Language="C#" Class="CallFieldVisitPhotoHandler" %>
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.Script.Serialization;

public class CallFieldVisitPhotoHandler : IHttpHandler
{
    private static readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }

    public void ProcessRequest(HttpContext ctx)
    {
        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
        ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization");
        ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
        if (ctx.Request.HttpMethod == "OPTIONS") { ctx.Response.StatusCode = 200; ctx.Response.End(); return; }
        if (ctx.Request.HttpMethod != "GET") { RespondError(ctx, 405, "Method not allowed"); return; }

        // Auth: reuse same as field visits
        CallTicketApiUser actor; int authStatus; string authMessage;
        if (!TryResolveUser(ctx, out actor, out authStatus, out authMessage))
        {
            RespondError(ctx, authStatus, authMessage);
            return;
        }

        int attachmentId;
        if (!int.TryParse(ctx.Request.QueryString["attachmentId"], out attachmentId) || attachmentId <= 0)
        {
            RespondError(ctx, 400, "attachmentId is required");
            return;
        }
        bool thumb = string.Equals(ctx.Request.QueryString["thumb"], "1", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ctx.Request.QueryString["thumbnail"], "1", StringComparison.OrdinalIgnoreCase);

        try
        {
            using (var conn = new SqlConnection(Cs()))
            using (var cmd = new SqlCommand(thumb ?
                "SELECT FileName, MimeType, COALESCE(ThumbnailBytes, FileBytes) as DataBytes FROM dbo.CallFieldVisitAttachment WHERE AttachmentId=@Id" :
                "SELECT FileName, MimeType, FileBytes as DataBytes FROM dbo.CallFieldVisitAttachment WHERE AttachmentId=@Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", attachmentId);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read() || r["DataBytes"] == DBNull.Value)
                    {
                        RespondError(ctx, 404, "Attachment not found");
                        return;
                    }
                    string fileName = r["FileName"] as string ?? string.Format("photo-{0}.jpg", attachmentId);
                    string mime = r["MimeType"] as string ?? "image/jpeg";
                    byte[] bytes = (byte[])r["DataBytes"];

                    // If ?raw=1 return binary directly, else JSON with base64 for mobile
                    if (string.Equals(ctx.Request.QueryString["raw"], "1", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.ContentType = mime;
                        ctx.Response.AddHeader("Content-Disposition", "inline; filename=\"" + fileName + "\"");
                        ctx.Response.BinaryWrite(bytes);
                        return;
                    }
                    string base64 = Convert.ToBase64String(bytes);
                    RespondJson(ctx, 200, new { success = true, attachmentId = attachmentId, fileName = fileName, mimeType = mime, base64 = base64, size = bytes.Length });
                }
            }
        }
        catch (Exception ex)
        {
            RespondError(ctx, 500, "Failed to load photo: " + ex.Message);
        }
    }

    private bool TryResolveUser(HttpContext ctx, out CallTicketApiUser actor, out int statusCode, out string message)
    {
        // Try mobile JWT first
        if (CallTicketApiSecurity.TryRequireIt(ctx, Cs(), out actor, out statusCode, out message))
            return true;
        if (statusCode == 401 && message == "Unauthorized") message = "Unauthorized - please log in";
        // Fallback to session
        if (ctx.Session != null && ctx.Session["UserId"] != null)
        {
            try
            {
                int uid = Convert.ToInt32(ctx.Session["UserId"]);
                actor = new CallTicketApiUser { UserId = uid, IsItAuthorized = true };
                statusCode = 200; message = null;
                return true;
            }
            catch { }
        }
        actor = null;
        return false;
    }

    private void RespondJson(HttpContext ctx, int statusCode, object data)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        ctx.Response.Write(_json.Serialize(data));
    }
    private void RespondError(HttpContext ctx, int statusCode, string message)
    {
        RespondJson(ctx, statusCode, new { success = false, error = message, message = message });
    }
    public bool IsReusable { get { return false; } }
}
