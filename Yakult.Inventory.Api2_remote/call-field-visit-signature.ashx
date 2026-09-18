<%@ WebHandler Language="C#" Class="CallFieldVisitSignatureHandler" %>
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Script.Serialization;

public class CallFieldVisitSignatureHandler : IHttpHandler
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

        CallTicketApiUser actor; int authStatus; string authMessage;
        if (!TryResolveUser(ctx, out actor, out authStatus, out authMessage))
        {
            RespondError(ctx, authStatus, authMessage);
            return;
        }

        int fieldVisitId;
        if (!int.TryParse(ctx.Request.QueryString["fieldVisitId"], out fieldVisitId) || fieldVisitId <= 0)
        {
            // also accept visitId alias
            if (!int.TryParse(ctx.Request.QueryString["visitId"], out fieldVisitId) || fieldVisitId <= 0)
            {
                RespondError(ctx, 400, "fieldVisitId is required");
                return;
            }
        }

        try
        {
            using (var conn = new SqlConnection(Cs()))
            using (var cmd = new SqlCommand("SELECT CustomerSignature FROM dbo.CallFieldVisit WHERE FieldVisitId=@Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", fieldVisitId);
                conn.Open();
                var obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value)
                {
                    RespondError(ctx, 404, "No signature on file");
                    return;
                }
                byte[] bytes = (byte[])obj;
                if (string.Equals(ctx.Request.QueryString["raw"], "1", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.ContentType = "image/png";
                    ctx.Response.BinaryWrite(bytes);
                    return;
                }
                string base64 = Convert.ToBase64String(bytes);
                RespondJson(ctx, 200, new { success = true, fieldVisitId = fieldVisitId, base64 = base64, size = bytes.Length, mimeType = "image/png" });
            }
        }
        catch (Exception ex)
        {
            RespondError(ctx, 500, "Failed to load signature: " + ex.Message);
        }
    }

    private bool TryResolveUser(HttpContext ctx, out CallTicketApiUser actor, out int statusCode, out string message)
    {
        if (CallTicketApiSecurity.TryRequireIt(ctx, Cs(), out actor, out statusCode, out message))
            return true;
        if (statusCode == 401 && message == "Unauthorized") message = "Unauthorized - please log in";
        if (ctx.Session != null && ctx.Session["UserId"] != null)
        {
            try { int uid = Convert.ToInt32(ctx.Session["UserId"]); actor = new CallTicketApiUser { UserId = uid, IsItAuthorized = true }; statusCode = 200; message = null; return true; } catch { }
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
