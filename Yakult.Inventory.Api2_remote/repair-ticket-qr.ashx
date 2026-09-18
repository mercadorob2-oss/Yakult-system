<%@ WebHandler Language="C#" Class="RepairTicketQrHandler" %>
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;

/// <summary>Returns the existing Repair Ticket QR token and, when present and reasonably small,
/// its desktop-generated image.  It does not create a new token or alter the shared QR record.</summary>
public sealed class RepairTicketQrHandler : IHttpHandler
{
    private const int MaxEmbeddedQrBytes = 1024 * 1024;

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
        int ticketId;
        if (!int.TryParse(context.Request.QueryString["ticketId"], out ticketId) || ticketId <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "ticketId is required");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.CanReadTicket(connection, ticketId, actor))
                {
                    RepairMobileApiSupport.Error(context, 404, "Repair ticket not found");
                    return;
                }
                if (!RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "QRToken"))
                {
                    RepairMobileApiSupport.Error(context, 503, "Repair ticket QR support is not installed for this environment");
                    return;
                }
                var hasData = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "QRData");
                var hasImage = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "QRImageData");
                var sql = "SELECT QRToken, " +
                          (hasData ? "QRData" : "CAST(NULL AS NVARCHAR(MAX))") + " AS QRData, " +
                          (hasImage ? "QRImageData" : "CAST(NULL AS VARBINARY(MAX))") + " AS QRImageData " +
                          "FROM dbo.RepairTicket WHERE RepairTicketId=@TicketId;";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            RepairMobileApiSupport.Error(context, 404, "Repair ticket not found");
                            return;
                        }
                        byte[] image = reader.IsDBNull(reader.GetOrdinal("QRImageData")) ? null : (byte[])reader["QRImageData"];
                        RepairMobileApiSupport.Ok(context, new
                        {
                            success = true,
                            ticketId = ticketId,
                            qrToken = reader.IsDBNull(reader.GetOrdinal("QRToken")) ? null : reader["QRToken"].ToString(),
                            qrData = RepairMobileApiSupport.StringValue(reader, "QRData"),
                            hasImage = image != null && image.Length > 0,
                            imageBase64 = image != null && image.Length > 0 && image.Length <= MaxEmbeddedQrBytes ? Convert.ToBase64String(image) : null,
                            imageOmittedForSize = image != null && image.Length > MaxEmbeddedQrBytes
                        });
                    }
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair ticket QR data is temporarily unavailable");
        }
    }

    public bool IsReusable { get { return false; } }
}
