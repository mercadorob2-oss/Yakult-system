<%@ WebHandler Language="C#" Class="RepairTicketEvidenceUploadHandler" %>
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>JWT-authorized ticket-level Repair Portal evidence upload. Part evidence continues to
/// use repair-part-photo-upload.ashx; this endpoint covers full-unit images, video, and documents.
/// Binary data is never logged or echoed back to the caller.</summary>
public sealed class RepairTicketEvidenceUploadHandler : IHttpHandler
{
    private const long MaxTicketEvidenceBytes = 50L * 1024L * 1024L;
    private const int MaxFileBytes = 20 * 1024 * 1024;

    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "POST, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireTechnician(context, out actor)) return;
        if (context.Request.ContentLength > 30 * 1024 * 1024)
        {
            RepairMobileApiSupport.Error(context, 413, "The evidence upload request is too large");
            return;
        }
        JObject request;
        if (!RepairMobileApiSupport.TryReadObject(context, out request))
        {
            RepairMobileApiSupport.Error(context, 400, "A valid JSON request body is required");
            return;
        }
        var ticketId = RepairMobileApiSupport.Int(request, "repairTicketId");
        var fileName = RepairMobileApiSupport.Text(request, "fileName", 260);
        var mimeType = RepairMobileApiSupport.Text(request, "mimeType", 100) ?? "image/jpeg";
        var base64 = RepairMobileApiSupport.Text(request, "fileBase64", 30000000);
        if (!ticketId.HasValue || ticketId.Value <= 0 || string.IsNullOrWhiteSpace(base64))
        {
            RepairMobileApiSupport.Error(context, 400, "repairTicketId and fileBase64 are required");
            return;
        }

        byte[] bytes;
        try
        {
            var payload = base64.Contains(",") ? base64.Substring(base64.IndexOf(',') + 1) : base64;
            bytes = Convert.FromBase64String(payload);
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 400, "Evidence data is invalid");
            return;
        }
        if (bytes.Length == 0 || bytes.Length > MaxFileBytes)
        {
            RepairMobileApiSupport.Error(context, 413, "Each evidence file must be no larger than 20 MB");
            return;
        }
        if (!IsAllowedMime(mimeType))
        {
            RepairMobileApiSupport.Error(context, 400, "This evidence file type is not allowed");
            return;
        }
        var attachmentType = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image"
            : mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "Video" : "Document";
        fileName = SafeFileName(fileName, attachmentType);

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.CanReadTicket(connection, ticketId.Value, actor))
                {
                    RepairMobileApiSupport.Error(context, 404, "Repair ticket not found");
                    return;
                }
                var usedBytes = GetUsedEvidenceBytes(connection, ticketId.Value);
                if (usedBytes + bytes.Length > MaxTicketEvidenceBytes)
                {
                    RepairMobileApiSupport.Error(context, 413, "This ticket has reached its 50 MB evidence limit");
                    return;
                }
                using (var command = new SqlCommand("dbo.sp_RepairPortal_AddAttachment", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId.Value;
                    command.Parameters.Add("@AttachmentType", SqlDbType.VarChar, 10).Value = attachmentType;
                    command.Parameters.Add("@FileName", SqlDbType.NVarChar, 260).Value = fileName;
                    command.Parameters.Add("@MimeType", SqlDbType.NVarChar, 100).Value = mimeType;
                    command.Parameters.Add("@FileBytes", SqlDbType.VarBinary, -1).Value = bytes;
                    command.Parameters.Add("@FileSizeBytes", SqlDbType.Int).Value = bytes.Length;
                    command.Parameters.Add("@UploadedByUserId", SqlDbType.Int).Value = actor.UserId;
                    object attachmentId = command.ExecuteScalar();
                    RepairMobileApiSupport.Created(context, new
                    {
                        success = true,
                        attachmentId = attachmentId == null || attachmentId == DBNull.Value ? (int?)null : Convert.ToInt32(attachmentId),
                        evidenceBytesUsed = usedBytes + bytes.Length,
                        evidenceBytesLimit = MaxTicketEvidenceBytes
                    });
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Evidence upload could not be completed");
        }
    }

    private static long GetUsedEvidenceBytes(SqlConnection connection, int ticketId)
    {
        const string sql = @"
SELECT ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes,0) AS BIGINT)) FROM dbo.RepairTicketAttachment WHERE RepairTicketId=@TicketId),0)
     + ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes,0) AS BIGINT)) FROM dbo.RepairPartAttachment WHERE RepairTicketId=@TicketId),0);";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            return Convert.ToInt64(command.ExecuteScalar());
        }
    }

    private static bool IsAllowedMime(string mimeType)
    {
        switch ((mimeType ?? string.Empty).Trim().ToLowerInvariant())
        {
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

    private static string SafeFileName(string fileName, string attachmentType)
    {
        var fallback = "mobile_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        if (attachmentType == "Image") fallback += ".jpg";
        else if (attachmentType == "Video") fallback += ".mp4";
        else fallback += ".pdf";
        if (string.IsNullOrWhiteSpace(fileName)) return fallback;
        var safe = Path.GetFileName(fileName).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }

    public bool IsReusable { get { return false; } }
}
