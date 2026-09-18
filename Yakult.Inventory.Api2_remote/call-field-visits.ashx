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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

/// <summary>
/// API handler for IT Call Monitoring field visits
/// State machine: Scheduled → Completed / Cancelled, Cancelled → Scheduled (reschedule)
/// Methods:
///   GET    /call-field-visits.ashx?ticketId=123           — Get field visit for a ticket
///   POST   /call-field-visits.ashx (JSON body)            — Schedule a new field visit (409 if a visit already exists, including Cancelled: reschedule those via PUT)
///   PUT    /call-field-visits.ashx (JSON body)            — Update field visit status (Completed/Cancelled/Scheduled reschedule)
/// </summary>
public class CallFieldVisitsHandler : IHttpHandler, IRequiresSessionState
{
    private static readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        NullValueHandling = NullValueHandling.Include,
        Converters = { new IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ" } }
    };

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
        var visits = visit == null ? new FieldVisit[0] : new[] { visit };
        var attachments = GetFieldVisitAttachments(ticketId.Value);
        // Return both fieldVisit (legacy) and visits (mobile) for compat
        RespondJson(ctx, 200, new { success = true, fieldVisit = visit, visits = visits, attachments = attachments });
    }

    /// <summary>POST /call-field-visits.ashx — Schedule field visit (also accepts fieldVisitId+newStatus/action as PUT fallback when IIS blocks PUT verb)</summary>
    private void HandlePost(HttpContext ctx)
    {
        // Peek raw body to detect PUT-style update tunneled via POST (mobile fallback when PUT is blocked by IIS)
        string raw = null;
        try { ctx.Request.InputStream.Position = 0; using (var sr = new System.IO.StreamReader(ctx.Request.InputStream, System.Text.Encoding.UTF8, true, 1024, true)) raw = sr.ReadToEnd(); ctx.Request.InputStream.Position = 0; } catch { }
        bool isUpdate = !string.IsNullOrWhiteSpace(raw) && raw.IndexOf("\"fieldVisitId\"", StringComparison.OrdinalIgnoreCase) >= 0
            && (raw.IndexOf("\"newStatus\"", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("\"action\"", StringComparison.OrdinalIgnoreCase) >= 0);
        if (isUpdate)
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
            RespondJson(ctx, 201, new { success = true, fieldVisit = visit, visits = new[] { visit }, message = "Field visit scheduled" });
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

    /// <summary>PUT /call-field-visits.ashx — Update status (Complete/Cancel/Reschedule). Accepts both newStatus and action (mobile).</summary>
    private void HandlePut(HttpContext ctx)
    {
        var body = ReadJsonBody<UpdateStatusRequest>(ctx);
        string effStatus = body != null ? body.EffectiveStatus : null;
        if (body == null || !body.fieldVisitId.HasValue || string.IsNullOrWhiteSpace(effStatus))
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
        if (effStatus != "Completed" && effStatus != "Cancelled" && effStatus != "Scheduled")
        {
            RespondError(ctx, 400, "Invalid status. Only 'Completed', 'Cancelled' or 'Scheduled' (reschedule) allowed.");
            return;
        }

        try
        {
            // Decode and validate the signature BEFORE the status change so a
            // bad payload can never yield "Completed" with no signature stored.
            // Completed requires a usable signature (mirrors the desktop gate;
            // sign-off is blocked for unsigned visits either way).
            byte[] sigBytes = null;
            var wantsSignature = !string.IsNullOrWhiteSpace(body.customerSignatureBase64);
            if (wantsSignature)
            {
                try { sigBytes = Convert.FromBase64String(body.customerSignatureBase64); }
                catch { RespondError(ctx, 400, "Invalid signature data."); return; }
                if (sigBytes == null || sigBytes.Length == 0)
                {
                    if (effStatus == "Completed") { RespondError(ctx, 400, "Customer signature is required to complete a visit."); return; }
                    wantsSignature = false;
                }
            }
            if (effStatus == "Completed" && !wantsSignature)
            {
                RespondError(ctx, 400, "Customer signature is required to complete a visit.");
                return;
            }

            var visit = UpdateFieldVisitStatus(
                body.fieldVisitId.Value,
                effStatus,
                userId,
                body.notes,
                body.technicianEmpId,
                body.scheduledAt
            );
            // Persist signature plus its history row atomically; failures
            // surface instead of returning success with nothing stored.
            if (wantsSignature)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {
                            using (var sigCmd = new SqlCommand("UPDATE dbo.CallFieldVisit SET CustomerSignature=@Sig, UpdatedAt=SYSUTCDATETIME() WHERE FieldVisitId=@Id", conn, tx))
                            {
                                sigCmd.Parameters.AddWithValue("@Sig", sigBytes);
                                sigCmd.Parameters.AddWithValue("@Id", body.fieldVisitId.Value);
                                sigCmd.ExecuteNonQuery();
                            }
                            using (var histCmd = new SqlCommand("INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note) VALUES (@TicketId,@UserId,'FieldVisitSignature','','Saved',NULL)", conn, tx))
                            {
                                histCmd.Parameters.AddWithValue("@TicketId", visit.ticketId);
                                histCmd.Parameters.AddWithValue("@UserId", userId);
                                histCmd.ExecuteNonQuery();
                            }
                            tx.Commit();
                        }
                    }
                    // Refresh visit to include signature flag if needed (HasSignature not in Map, but keep)
                    visit = GetFieldVisitById(body.fieldVisitId.Value) ?? visit;
                }
                catch (Exception sigEx)
                {
                    RespondError(ctx, 500, "Status updated but signature could not be saved: " + sigEx.Message);
                    return;
                }
            }
            RespondJson(ctx, 200, new { success = true, fieldVisit = visit, visits = new[] { visit }, message = "Field work updated: " + effStatus });
        }
        catch (SqlException ex)
        {
            if (ex.Number == 51012) { RespondError(ctx, 400, ex.Message); return; }
            if (ex.Number == 51013) { RespondError(ctx, 404, ex.Message); return; }
            if (ex.Number == 51014) { RespondError(ctx, 409, ex.Message); return; }
            if (ex.Number == 51015) { RespondError(ctx, 400, ex.Message); return; }
            throw;
        }
    }

    // ── Database Methods ──

    private FieldVisit GetFieldVisitById(int fieldVisitId)
    {
        string sql = @"
            SELECT 
                v.FieldVisitId, v.TicketId, v.TechnicianEmpId, te.Name AS TechnicianName, v.Status, 
                v.ScheduledAt, v.CompletedAt, v.Notes, 
                CASE WHEN DATALENGTH(v.CustomerSignature) > 0 THEN 1 ELSE 0 END AS HasSignature,
                v.CreatedByUserId, v.CreatedAt, v.UpdatedAt
            FROM dbo.CallFieldVisit v
            LEFT JOIN dbo.Employee te ON te.EmpId = v.TechnicianEmpId
            WHERE v.FieldVisitId = @FieldVisitId";
        using (var conn = GetConnection())
        using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@FieldVisitId", fieldVisitId);
            conn.Open();
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                    return MapFieldVisit(reader);
                return null;
            }
        }
    }

    private FieldVisit GetFieldVisit(int ticketId)
    {
        string sql = @"
            SELECT 
                v.FieldVisitId, v.TicketId, v.TechnicianEmpId, te.Name AS TechnicianName, v.Status, 
                v.ScheduledAt, v.CompletedAt, v.Notes, 
                CASE WHEN DATALENGTH(v.CustomerSignature) > 0 THEN 1 ELSE 0 END AS HasSignature,
                v.CreatedByUserId, v.CreatedAt, v.UpdatedAt
            FROM dbo.CallFieldVisit v
            LEFT JOIN dbo.Employee te ON te.EmpId = v.TechnicianEmpId
            WHERE v.TicketId = @TicketId";

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

    private FieldVisit UpdateFieldVisitStatus(int fieldVisitId, string newStatus, int userId, string notes, int? technicianEmpId = null, DateTime? scheduledAt = null)
    {
        using (var conn = GetConnection())
        using (var cmd = new SqlCommand("dbo.sp_Call_FieldVisit_SetStatus", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@FieldVisitId", fieldVisitId);
            cmd.Parameters.AddWithValue("@NewStatus", newStatus);
            cmd.Parameters.AddWithValue("@ChangedByUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", (object)notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TechnicianEmpId", (object)technicianEmpId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ScheduledAt", (object)scheduledAt ?? DBNull.Value);

            conn.Open();
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                    return MapFieldVisit(reader);
                throw new Exception("Failed to update field visit status");
            }
        }
    }

    private List<FieldVisitAttachment> GetFieldVisitAttachments(int ticketId)
    {
        var list = new List<FieldVisitAttachment>();
        string sql = @"
            SELECT a.AttachmentId, a.FieldVisitId, a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt, u.Name AS UploadedByName
            FROM dbo.CallFieldVisitAttachment a
            INNER JOIN dbo.CallFieldVisit v ON v.FieldVisitId = a.FieldVisitId
            LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
            LEFT JOIN dbo.Employee e ON e.EmpId = u.EmpId
            WHERE v.TicketId = @TicketId
            ORDER BY a.UploadedAt DESC, a.AttachmentId DESC";
        using (var conn = GetConnection())
        using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@TicketId", ticketId);
            conn.Open();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new FieldVisitAttachment
                    {
                        attachmentId = r.GetInt32(r.GetOrdinal("AttachmentId")),
                        fieldVisitId = r.GetInt32(r.GetOrdinal("FieldVisitId")),
                        fileName = r.IsDBNull(r.GetOrdinal("FileName")) ? null : r.GetString(r.GetOrdinal("FileName")),
                        mimeType = r.IsDBNull(r.GetOrdinal("MimeType")) ? null : r.GetString(r.GetOrdinal("MimeType")),
                        fileSizeBytes = r.IsDBNull(r.GetOrdinal("FileSizeBytes")) ? (int?)null : Convert.ToInt32(r["FileSizeBytes"]),
                        uploadedAt = r.IsDBNull(r.GetOrdinal("UploadedAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("UploadedAt")),
                        uploadedByName = r.IsDBNull(r.GetOrdinal("UploadedByName")) ? null : r.GetString(r.GetOrdinal("UploadedByName"))
                    });
                }
            }
        }
        return list;
    }

    private FieldVisit MapFieldVisit(SqlDataReader r)
    {
        // Handle both old (without joins) and new (with joins) column sets
        bool hasTechName = false;
        bool hasSig = false;
        try { r.GetOrdinal("TechnicianName"); hasTechName = true; } catch { }
        try { r.GetOrdinal("HasSignature"); hasSig = true; } catch { }
        return new FieldVisit
        {
            fieldVisitId = r.GetInt32(r.GetOrdinal("FieldVisitId")),
            ticketId = r.GetInt32(r.GetOrdinal("TicketId")),
            technicianEmpId = r.IsDBNull(r.GetOrdinal("TechnicianEmpId")) ? (int?)null : r.GetInt32(r.GetOrdinal("TechnicianEmpId")),
            technicianName = hasTechName && !r.IsDBNull(r.GetOrdinal("TechnicianName")) ? r.GetString(r.GetOrdinal("TechnicianName")) : null,
            status = r.GetString(r.GetOrdinal("Status")),
            scheduledAt = r.IsDBNull(r.GetOrdinal("ScheduledAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ScheduledAt")),
            completedAt = r.IsDBNull(r.GetOrdinal("CompletedAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("CompletedAt")),
            notes = r.IsDBNull(r.GetOrdinal("Notes")) ? null : r.GetString(r.GetOrdinal("Notes")),
            hasSignature = hasSig ? (!r.IsDBNull(r.GetOrdinal("HasSignature")) && Convert.ToInt32(r["HasSignature"]) == 1) : false,
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
                if (string.IsNullOrWhiteSpace(json)) return null;
                try { return JsonConvert.DeserializeObject<T>(json, _jsonSettings); } catch { return _json.Deserialize<T>(json); }
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
        ctx.Response.Write(JsonConvert.SerializeObject(data, _jsonSettings));
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
        public string action { get; set; } // mobile alias for newStatus
        public string notes { get; set; }
        public string customerSignatureBase64 { get; set; }
        public int? technicianEmpId { get; set; }
        public DateTime? scheduledAt { get; set; }
        public string EffectiveStatus { get { return !string.IsNullOrWhiteSpace(newStatus) ? newStatus : action; } }
    }

    private class FieldVisit
    {
        public int fieldVisitId { get; set; }
        public int ticketId { get; set; }
        public int? technicianEmpId { get; set; }
        public string technicianName { get; set; }
        public string status { get; set; }
        public DateTime? scheduledAt { get; set; }
        public DateTime? completedAt { get; set; }
        public string notes { get; set; }
        public bool hasSignature { get; set; }
        public int? createdByUserId { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }

    private class FieldVisitAttachment
    {
        public int attachmentId { get; set; }
        public int fieldVisitId { get; set; }
        public string fileName { get; set; }
        public string mimeType { get; set; }
        public int? fileSizeBytes { get; set; }
        public DateTime? uploadedAt { get; set; }
        public string uploadedByName { get; set; }
    }
}
