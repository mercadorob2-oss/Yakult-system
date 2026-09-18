<%@ WebHandler Language="C#" Class="CallFieldVisitPhotoUploadHandler" %>
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>Photo upload for a Call Field Visit (signature also via call-field-visits.ashx PUT).
/// POST {fieldVisitId, fileName?, mimeType?, fileBase64}
/// Requires technician auth (any IT tech, per choice 3.b) and ticket readability.
/// Mirrors repair-part-photo-upload.ashx thumbnail logic (400px, 85% JPEG).</summary>
public sealed class CallFieldVisitPhotoUploadHandler : IHttpHandler
{
    private const long MaxFileBytes = 20L * 1024 * 1024; // 20 MB per file
    private const int MaxFilesPerVisit = 20;

    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "POST, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed"); return;
        }
        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;
        if (!actor.IsItAuthorized)
        {
            RepairMobileApiSupport.Error(context, 403, "Technician access required"); return;
        }
        JObject body;
        if (!RepairMobileApiSupport.TryReadObject(context, out body))
        {
            RepairMobileApiSupport.Error(context, 400, "A valid JSON request body is required"); return;
        }
        var fieldVisitId = RepairMobileApiSupport.Int(body, "fieldVisitId") ?? RepairMobileApiSupport.Int(body, "FieldVisitId");
        var fileName = RepairMobileApiSupport.Text(body, "fileName", 255) ?? RepairMobileApiSupport.Text(body, "FileName", 255) ?? "visit-photo.jpg";
        var mimeType = RepairMobileApiSupport.Text(body, "mimeType", 100) ?? RepairMobileApiSupport.Text(body, "MimeType", 100) ?? "image/jpeg";
        var fileBase64 = RepairMobileApiSupport.Text(body, "fileBase64", 8000000) ?? RepairMobileApiSupport.Text(body, "FileBase64", 8000000) ?? RepairMobileApiSupport.Text(body, "image_base64", 8000000);

        if (!fieldVisitId.HasValue || fieldVisitId.Value <= 0) { RepairMobileApiSupport.Error(context, 400, "fieldVisitId is required"); return; }
        if (string.IsNullOrWhiteSpace(fileBase64)) { RepairMobileApiSupport.Error(context, 400, "fileBase64 is required"); return; }

        byte[] fileBytes;
        try { fileBytes = Convert.FromBase64String(fileBase64); }
        catch { RepairMobileApiSupport.Error(context, 400, "fileBase64 is not valid base64"); return; }
        if (fileBytes.Length == 0 || fileBytes.Length > MaxFileBytes) { RepairMobileApiSupport.Error(context, 400, "File size must be 1 byte to 20 MB"); return; }
        // Sniff content: declared mime is not trusted. PNG and JPEG only.
        mimeType = SniffPhotoMimeType(fileBytes);
        if (mimeType == null) { RepairMobileApiSupport.Error(context, 400, "Only PNG and JPEG photos are accepted"); return; }

        try
        {
            using (var conn = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                conn.Open();
                if (!RepairMobileApiSupport.HasTable(conn, "dbo.CallFieldVisitAttachment"))
                {
                    RepairMobileApiSupport.Error(context, 503, "Field visit attachments not installed"); return;
                }
                // Verify visit and ticket readability
                int ticketId = 0;
                using (var cmd = new SqlCommand("SELECT TicketId, Status FROM dbo.CallFieldVisit WHERE FieldVisitId=@Id;", conn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = fieldVisitId.Value;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) { RepairMobileApiSupport.Error(context, 404, "Field visit not found"); return; }
                        ticketId = Convert.ToInt32(r["TicketId"]);
                        var visitStatus = (r["Status"] as string ?? string.Empty).Trim();
                        if (visitStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)) { RepairMobileApiSupport.Error(context, 409, "Photos cannot be added to a Completed visit"); return; }
                    }
                }
                // Reuse same check as call-field-visits.ashx (inline to avoid cross-handler call)
                if (!actor.IsItAuthorized)
                {
                    using (var cmd = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.CallTicket t WHERE t.TicketId=@TicketId AND (t.CreatedByUserId=@UserId OR t.AssignedToEmpId=@EmpId)) THEN 1 ELSE 0 END;", conn))
                    {
                        cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = actor.UserId;
                        cmd.Parameters.Add("@EmpId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
                        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) { RepairMobileApiSupport.Error(context, 404, "Call ticket not found"); return; }
                    }
                }
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.CallFieldVisitAttachment WHERE FieldVisitId=@Id;", conn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = fieldVisitId.Value;
                    if (Convert.ToInt32(cmd.ExecuteScalar()) >= MaxFilesPerVisit) { RepairMobileApiSupport.Error(context, 409, "Maximum 20 photos per visit"); return; }
                }

                byte[] thumb = null;
                if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    try { thumb = CreateThumbnailBytes(fileBytes); } catch { thumb = null; }
                }

                const string sql = @"
INSERT dbo.CallFieldVisitAttachment (FieldVisitId, FileName, MimeType, FileBytes, ThumbnailBytes, FileSizeBytes, UploadedByUserId)
VALUES (@VisitId, @FileName, @MimeType, @FileBytes, @Thumb, @Size, @UserId);
SELECT SCOPE_IDENTITY();";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@VisitId", SqlDbType.Int).Value = fieldVisitId.Value;
                    cmd.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = fileName;
                    cmd.Parameters.Add("@MimeType", SqlDbType.NVarChar, 100).Value = mimeType;
                    cmd.Parameters.Add("@FileBytes", SqlDbType.VarBinary, -1).Value = fileBytes;
                    cmd.Parameters.Add("@Thumb", SqlDbType.VarBinary, -1).Value = thumb != null ? (object)thumb : DBNull.Value;
                    cmd.Parameters.Add("@Size", SqlDbType.Int).Value = fileBytes.Length;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = actor.UserId;
                    var id = Convert.ToInt32(cmd.ExecuteScalar());
                    RepairMobileApiSupport.Created(context, new { success = true, message = "Photo uploaded", attachmentId = id, fieldVisitId = fieldVisitId.Value, fileSizeBytes = fileBytes.Length });
                }
            }
        }
        catch { RepairMobileApiSupport.Error(context, 503, "Photo upload could not be completed"); }
    }

    private static string SniffPhotoMimeType(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < 4)
            return null;
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        if (fileBytes.Length >= 8
            && fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47
            && fileBytes[4] == 0x0D && fileBytes[5] == 0x0A && fileBytes[6] == 0x1A && fileBytes[7] == 0x0A)
            return "image/png";
        // JPEG signature: FF D8 FF
        if (fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
            return "image/jpeg";
        return null;
    }

    private static byte[] CreateThumbnailBytes(byte[] src, int maxDim = 400, long quality = 85L)
    {
        using (var ms = new MemoryStream(src))
        using (var img = Image.FromStream(ms))
        {
            var scale = Math.Min((double)maxDim / img.Width, (double)maxDim / img.Height);
            if (scale >= 1) scale = 1;
            var w = Math.Max(1, (int)(img.Width * scale));
            var h = Math.Max(1, (int)(img.Height * scale));
            using (var thumb = new Bitmap(w, h))
            using (var g = Graphics.FromImage(thumb))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, w, h);
                var enc = ImageCodecInfo.GetImageEncoders();
                ImageCodecInfo jpeg = null;
                foreach (var e in enc) if (e.FormatID == ImageFormat.Jpeg.Guid) { jpeg = e; break; }
                using (var outMs = new MemoryStream())
                using (var eps = new EncoderParameters(1))
                {
                    eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                    if (jpeg != null) thumb.Save(outMs, jpeg, eps);
                    else thumb.Save(outMs, ImageFormat.Jpeg);
                    return outMs.ToArray();
                }
            }
        }
    }

    public bool IsReusable { get { return false; } }
}
