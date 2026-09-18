<%@ WebHandler Language="C#" Class="RepairEvidenceDownloadHandler" %>
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;

/// <summary>Streams one Repair Portal attachment after validating the caller can read its parent
/// ticket. Binary evidence is never included in list/detail JSON; it is available only through
/// this JWT-protected download endpoint.</summary>
public sealed class RepairEvidenceDownloadHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "GET, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;

        int attachmentId;
        int partAttachmentId;
        var hasTicketAttachment = int.TryParse(context.Request.QueryString["attachmentId"], out attachmentId) && attachmentId > 0;
        var hasPartAttachment = int.TryParse(context.Request.QueryString["partAttachmentId"], out partAttachmentId) && partAttachmentId > 0;
        if (hasTicketAttachment == hasPartAttachment)
        {
            RepairMobileApiSupport.Error(context, 400, "Specify exactly one attachmentId or partAttachmentId");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                var sql = hasTicketAttachment
                    ? @"SELECT RepairTicketId, FileName, MimeType, FileBytes FROM dbo.RepairTicketAttachment WHERE AttachmentId = @AttachmentId;"
                    : @"SELECT RepairTicketId, FileName, MimeType, FileBytes FROM dbo.RepairPartAttachment WHERE PartAttachmentId = @AttachmentId;";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = hasTicketAttachment ? attachmentId : partAttachmentId;
                    int ticketId;
                    string fileName;
                    string mimeType;
                    byte[] bytes;
                    using (var reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        if (!reader.Read())
                        {
                            RepairMobileApiSupport.Error(context, 404, "Evidence attachment not found");
                            return;
                        }
                        ticketId = Convert.ToInt32(reader["RepairTicketId"]);
                        if (reader.IsDBNull(reader.GetOrdinal("FileBytes")))
                        {
                            RepairMobileApiSupport.Error(context, 404, "Evidence file is unavailable");
                            return;
                        }
                        fileName = SafeFileName(RepairMobileApiSupport.StringValue(reader, "FileName"));
                        mimeType = SafeMimeType(RepairMobileApiSupport.StringValue(reader, "MimeType"));
                        bytes = (byte[])reader["FileBytes"];
                    }
                    if (!RepairMobileApiSupport.CanReadTicket(connection, ticketId, actor))
                    {
                        RepairMobileApiSupport.Error(context, 404, "Evidence attachment not found");
                        return;
                    }

                    context.Response.Clear();
                    context.Response.TrySkipIisCustomErrors = true;
                    context.Response.ContentType = mimeType;
                    context.Response.AddHeader("X-Content-Type-Options", "nosniff");
                    context.Response.AddHeader("Cache-Control", "no-store");
                    context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName.Replace("\"", string.Empty) + "\"");
                    context.Response.AddHeader("Content-Length", bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    context.Response.BinaryWrite(bytes);
                }
            }
        }
        catch
        {
            if (!context.Response.IsClientConnected) return;
            RepairMobileApiSupport.Error(context, 503, "Evidence download is temporarily unavailable");
        }
    }

    private static string SafeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "repair-evidence.bin" : safe;
    }

    private static string SafeMimeType(string mimeType)
    {
        var value = (mimeType ?? string.Empty).Trim().ToLowerInvariant();
        switch (value)
        {
            case "image/jpeg": case "image/png": case "image/webp": case "image/gif":
            case "video/mp4": case "video/webm": case "video/quicktime":
            case "application/pdf": case "text/plain": case "application/msword":
            case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
            case "application/vnd.ms-excel": case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                return value;
            default:
                return "application/octet-stream";
        }
    }

    public bool IsReusable { get { return false; } }
}
