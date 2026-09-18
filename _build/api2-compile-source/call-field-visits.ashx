<%@ WebHandler Language="C#" Class="CallFieldVisitsHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;

/// <summary>
/// API handler for IT Call Monitoring field visits
/// State machine: Scheduled → Completed / Cancelled, Cancelled → Scheduled (reschedule)
/// Methods:
///   GET    /call-field-visits.ashx?ticketId=123           — Get field visit for a ticket
///   POST   /call-field-visits.ashx (JSON body)            — Schedule a new field visit (reschedules if existing is Cancelled)
///   PUT    /call-field-visits.ashx (JSON body)            — Update field visit status (Completed/Cancelled/Scheduled reschedule)
/// </summary>
public class CallFieldVisitsHandler : IHttpHandler, IRequiresSessionState
{
    private static readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    private string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }

    public void ProcessRequest(HttpContext ctx)
    {
        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
        ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization");
        ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS");
        if (ctx.Request.HttpMethod == "OPTIONS") { ctx.Response.StatusCode = 200; ctx.Response.End(); return; }

        try
        {
            string method = ctx.Request.HttpMethod.ToUpper();
            
            if (method == "GET")
                HandleGet(ctx);
            else if (method == "POST")
                HandlePost(ctx);
            else if (method == "PUT")
                HandlePut(ctx);
            else
                RespondError(ctx, 405, "Method not allowed");
        }
        catch (Exception ex)
        {
            RespondError(ctx, 500, "Internal server error: " + ex.Message);
        }
    }

    /// <summary>GET /call-field-visits.ashx?ticketId=123</summary>
    private void HandleGet(HttpContext ctx)
    {
        int? ticketId = ParseInt(ctx.Request.QueryString["ticketId"]);
        if (!ticketId.HasValue)
        {
            RespondError(ctx, 400, "ticketId is required");
            return;
        }

        // GET is read-only — allow any authenticated user (mobile or web). Uncomment the block below to enforce auth.
        // int userId; int authStatus; string authMessage;
        // if (!TryResolveUserId(ctx, out userId, out authStatus, out authMessage)) { RespondError(ctx, authStatus, authMessage); return; }

        var visit = GetFieldVisit(ticketId.Value);
        RespondJson(ctx, 200, new { success = true, fieldVisit = visit });
    }

    /// <summary>POST /call-field-visits.ashx — Schedule field visit (also accepts fieldVisitId+newStatus as PUT fallback when IIS blocks PUT verb)</summary>
    private void HandlePost(HttpContext ctx)
    {
        // Peek raw body to detect PUT-style update tunneled via POST (mobile fallback when PUT is blocked by IIS)
        string raw = null;
        try { ctx.Request.InputStream.Position = 0; using (var sr = new System.IO.StreamReader(ctx.Request.InputStream, System.Text.Encoding.UTF8, true, 1024, true)) raw = sr.ReadToEnd(); ctx.Request.InputStream.Position = 0; } catch { }
        if (!string.IsNullOrWhiteSpace(raw) && raw.IndexOf("\"fieldVisitId\"", StringComparison.OrdinalIgnoreCase) >= 0 && raw.IndexOf("\"newStatus\"", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            HandlePut(ctx);
            return;
        }

        var body = ReadJsonBody<ScheduleRequest>(ctx);
        if (body == null || !body.ticketId.HasValue)
        {
            RespondError(ctx, 400, "ticketId is required");
            return;
        }

        int userId; int authStatus; string authMessage;
        if (!TryResolveUserId(ctx, out userId, out authStatus, out authMessage))
        {
            RespondError(ctx, authStatus, authMessage);
            return;
        }

        try
        {
            var visit = ScheduleFieldVisit(
                body.ticketId.Value,
                body.technicianEmpId,
                body.scheduledAt,
                body.notes,
                userId
            );
            RespondJson(ctx, 201, new { success = true, fieldVisit = visit });
        }
        catch (SqlException ex)
        {
            // Map business THROWs from the proc to proper HTTP status (not 500)
            if (ex.Number == 51010) { RespondError(ctx, 404, ex.Message); return; }
            if (ex.Number == 51011) { RespondError(ctx, 409, ex.Message); return; }
            if (ex.Number == 51012) { RespondError(ctx, 400, ex.Message); return; }
            throw;
        }
    }

    /// <summary>PUT /call-field-visits.ashx — Update status (Complete/Cancel)</summary>
    private void HandlePut(HttpContext ctx)
    {
        var body = ReadJsonBody<UpdateStatusRequest>(ctx);
        if (body == null || !body.fieldVisitId.HasValue || string.IsNullOrWhiteSpace(body.newStatus))
        {
            RespondError(ctx, 400, "fieldVisitId and newStatus are required");
            return;
        }

        int userId; int authStatus; string authMessage;
        if (!TryResolveUserId(ctx, out userId, out authStatus, out authMessage))
        {
            RespondError(ctx, authStatus, authMessage);
            return;
        }

        // Validate status — allow Scheduled for reschedule (Cancelled → Scheduled)
        if (body.newStatus != "Completed" && body.newStatus != "Cancelled" && body.newStatus != "Scheduled")
        {
            RespondError(ctx, 400, "Invalid status. Only 'Completed', 'Cancelled' or 'Scheduled' (reschedule) allowed.");
            return;
        }

        try
        {
            var visit = UpdateFieldVisitStatus(
                body.fieldVisitId.Value,
                body.newStatus,
                userId,
                body.notes
            );
            RespondJson(ctx, 200, new { success = true, fieldVisit = visit });
        }
        catch (SqlException ex)
        {
            if (ex.Number == 51013) { RespondError(ctx, 404, ex.Message); return; }
            if (ex.Number == 51014) { RespondError(ctx, 409, ex.Message); return; }
            if (ex.Number == 51015) { RespondError(ctx, 400, ex.Message); return; }
            throw;
        }
    }

    // ── Database Methods ──

    private FieldVisit GetFieldVisit(int ticketId)
    {
        string sql = @"
            SELECT 
                FieldVisitId, TicketId, TechnicianEmpId, Status, 
                ScheduledAt, CompletedAt, Notes, 
                CreatedByUserId, CreatedAt, UpdatedAt
            FROM dbo.CallFieldVisit
            WHERE TicketId = @TicketId";

        using (var conn = GetConnection())
        using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@TicketId", ticketId);
            conn.Open();
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                    return MapFieldVisit(reader);
                return null;
            }
        }
    }

    private FieldVisit ScheduleFieldVisit(int ticketId, int? technicianEmpId, DateTime? scheduledAt, string notes, int userId)
    {
        using (var conn = GetConnection())
        using (var cmd = new SqlCommand("dbo.sp_Call_FieldVisit_Schedule", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@TicketId", ticketId);
            cmd.Parameters.AddWithValue("@TechnicianEmpId", (object)technicianEmpId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ScheduledAt", (object)scheduledAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes", (object)notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedByUserId", userId);

            conn.Open();
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                    return MapFieldVisit(reader);
                throw new Exception("Failed to schedule field visit");
            }
        }
    }

    private FieldVisit UpdateFieldVisitStatus(int fieldVisitId, string newStatus, int userId, string notes)
    {
        using (var conn = GetConnection())
        using (var cmd = new SqlCommand("dbo.sp_Call_FieldVisit_SetStatus", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@FieldVisitId", fieldVisitId);
            cmd.Parameters.AddWithValue("@NewStatus", newStatus);
            cmd.Parameters.AddWithValue("@ChangedByUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", (object)notes ?? DBNull.Value);

            conn.Open();
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                    return MapFieldVisit(reader);
                throw new Exception("Failed to update field visit status");
            }
        }
    }

    private FieldVisit MapFieldVisit(SqlDataReader r)
    {
        return new FieldVisit
        {
            fieldVisitId = r.GetInt32(r.GetOrdinal("FieldVisitId")),
            ticketId = r.GetInt32(r.GetOrdinal("TicketId")),
            technicianEmpId = r.IsDBNull(r.GetOrdinal("TechnicianEmpId")) ? (int?)null : r.GetInt32(r.GetOrdinal("TechnicianEmpId")),
            status = r.GetString(r.GetOrdinal("Status")),
            scheduledAt = r.IsDBNull(r.GetOrdinal("ScheduledAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ScheduledAt")),
            completedAt = r.IsDBNull(r.GetOrdinal("CompletedAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("CompletedAt")),
            notes = r.IsDBNull(r.GetOrdinal("Notes")) ? null : r.GetString(r.GetOrdinal("Notes")),
            createdByUserId = r.IsDBNull(r.GetOrdinal("CreatedByUserId")) ? (int?)null : r.GetInt32(r.GetOrdinal("CreatedByUserId")),
            createdAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
            updatedAt = r.GetDateTime(r.GetOrdinal("UpdatedAt"))
        };
    }

    // ── Helper Methods ──

    private SqlConnection GetConnection()
    {
        return new SqlConnection(Cs());
    }

    /// <summary>Resolves caller identity from web Session or mobile Bearer JWT (CallTicketApiSecurity).</summary>
    private bool TryResolveUserId(HttpContext ctx, out int userId, out int statusCode, out string message)
    {
        // 1) Web portal session (legacy)
        if (ctx.Session != null && ctx.Session["UserId"] != null)
        {
            try { userId = Convert.ToInt32(ctx.Session["UserId"]); statusCode = 200; message = null; return true; }
            catch { }
        }
        // 2) Mobile JWT — reuse the same auth as every other call-* handler
        CallTicketApiUser actor;
        if (CallTicketApiSecurity.TryRequireIt(ctx, Cs(), out actor, out statusCode, out message))
        {
            userId = actor.UserId;
            return true;
        }
        // Preserve legacy 56-byte message expected by existing mobile builds ("Unauthorized - please log in")
        if (statusCode == 401 && message == "Unauthorized") message = "Unauthorized - please log in";
        // Fallback: also allow any authenticated user (not just IT) — uncomment if field visits should be requester-visible
        // if (CallTicketApiSecurity.TryRequireAuthenticated(ctx, Cs(), out actor, out statusCode, out message)) { userId = actor.UserId; return true; }
        userId = 0;
        return false;
    }

    private T ReadJsonBody<T>(HttpContext ctx) where T : class
    {
        try
        {
            using (var reader = new System.IO.StreamReader(ctx.Request.InputStream))
            {
                string json = reader.ReadToEnd();
                return _json.Deserialize<T>(json);
            }
        }
        catch
        {
            return null;
        }
    }

    private int? ParseInt(string s)
    {
        int val;
        if (int.TryParse(s, out val))
            return val;
        return null;
    }

    private void RespondJson(HttpContext ctx, int statusCode, object data)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        ctx.Response.Write(_json.Serialize(data));
    }

    private void RespondError(HttpContext ctx, int statusCode, string message)
    {
        // Return both "error" and "message" for mobile/desktop compat (mobile parses "message", legacy parses "error")
        RespondJson(ctx, statusCode, new { success = false, error = message, message = message });
    }

    public bool IsReusable { get { return false; } }

    // ── DTOs ──

    private class ScheduleRequest
    {
        public int? ticketId { get; set; }
        public int? technicianEmpId { get; set; }
        public DateTime? scheduledAt { get; set; }
        public string notes { get; set; }
    }

    private class UpdateStatusRequest
    {
        public int? fieldVisitId { get; set; }
        public string newStatus { get; set; }
        public string notes { get; set; }
    }

    private class FieldVisit
    {
        public int fieldVisitId { get; set; }
        public int ticketId { get; set; }
        public int? technicianEmpId { get; set; }
        public string status { get; set; }
        public DateTime? scheduledAt { get; set; }
        public DateTime? completedAt { get; set; }
        public string notes { get; set; }
        public int? createdByUserId { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
}
