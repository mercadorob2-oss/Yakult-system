using System;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Email notification service for Requester Portal.
    /// Sends completion notifications to destination branch email addresses.
    /// REUSES existing SMTP infrastructure - no new email logic.
    ///
    /// NOTE: This is for future use - skip implementation for now per requirements.
    /// </summary>
    public class PortalNotificationService
    {
        private readonly string _connectionString;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly bool _enableSsl;

        public PortalNotificationService()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            // Load SMTP settings from app config
            _smtpHost = System.Configuration.ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.office365.com";
            _smtpPort = int.Parse(System.Configuration.ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
            _smtpUsername = System.Configuration.ConfigurationManager.AppSettings["SmtpUsername"];
            _smtpPassword = System.Configuration.ConfigurationManager.AppSettings["SmtpPassword"];
            _fromEmail = System.Configuration.ConfigurationManager.AppSettings["FromEmail"];
            _enableSsl = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["EnableSsl"] ?? "true");
        }

        /// <summary>
        /// Sends email notification when a portal cartridge request is completed.
        /// Email is sent to the DESTINATION BRANCH EMAIL, NOT the requester.
        /// </summary>
        /// <param name="requestId">The completed request ID</param>
        public void SendCompletionNotification(int requestId)
        {
            // Get request and destination branch details
            var notificationData = GetNotificationData(requestId);

            if (notificationData == null)
            {
                throw new InvalidOperationException($"Request {requestId} not found or not a portal request");
            }

            if (string.IsNullOrWhiteSpace(notificationData.BranchEmail))
            {
                throw new InvalidOperationException($"No email configured for branch: {notificationData.BranchName}");
            }

            // Build email content
            string subject = $"Cartridge Request Completed - Req #{requestId}";
            string body = BuildEmailBody(notificationData);

            // Send email
            SendEmail(notificationData.BranchEmail, subject, body);
        }

        /// <summary>
        /// Retrieves notification data for a completed request.
        /// </summary>
        private NotificationData GetNotificationData(int requestId)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.DateRequested,
                    r.Status,
                    r.Quantity,
                    r.Description,
                    r.Remarks,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    b.BranchId,
                    b.Name AS BranchName,
                    b.Email AS BranchEmail, -- NOTE: May need to add this column
                    d.Name AS DepartmentName,
                    c.Name AS CompanyName,
                    e.Name AS DestinationEmployeeName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                INNER JOIN dbo.Branch b ON e.BranchId = b.BranchId
                INNER JOIN dbo.Department d ON e.DeptId = d.DeptId
                INNER JOIN dbo.Company c ON e.ComId = c.ComId
                WHERE r.ReqId = @ReqId
                  AND r.Description LIKE '[[]PORTAL]%'";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReqId", requestId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new NotificationData
                            {
                                ReqId = reader.GetInt32(0),
                                DateRequested = reader.IsDBNull(1) ? DateTime.Now : reader.GetDateTime(1),
                                Status = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                Description = reader.GetString(4),
                                Remarks = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ItemName = reader.GetString(6),
                                ItemModelNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                                BranchId = reader.GetInt32(8),
                                BranchName = reader.GetString(9),
                                BranchEmail = reader.IsDBNull(10) ? null : reader.GetString(10),
                                DepartmentName = reader.GetString(11),
                                CompanyName = reader.GetString(12),
                                DestinationEmployeeName = reader.GetString(13)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Builds HTML email body for completion notification.
        /// </summary>
        private string BuildEmailBody(NotificationData data)
        {
            // Parse fulfillment method and cartridge condition
            string fulfillmentMethod = data.Description.Contains("PICKUP") ? "PICKUP" : "DELIVERY";
            string cartridgeCondition = "Not Specified";
            if (data.Remarks != null)
            {
                if (data.Remarks.Contains("With Cartridge"))
                    cartridgeCondition = "With Cartridge";
                else if (data.Remarks.Contains("Without Cartridge"))
                    cartridgeCondition = "Without Cartridge";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0066cc; color: white; padding: 15px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; padding: 10px; font-size: 12px; color: #777; }}
        .info-row {{ margin: 10px 0; }}
        .label {{ font-weight: bold; display: inline-block; width: 180px; }}
        .value {{ display: inline-block; }}
        .highlight {{ background-color: #28a745; color: white; padding: 5px 10px; border-radius: 3px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Cartridge Request Completed</h2>
        </div>
        <div class='content'>
            <p><strong>Dear {data.BranchName} Team,</strong></p>
            <p>The following cartridge request has been <span class='highlight'>COMPLETED</span> and is ready for {fulfillmentMethod.ToLower()}:</p>

            <div class='info-row'>
                <span class='label'>Request ID:</span>
                <span class='value'>#{data.ReqId}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Status:</span>
                <span class='value highlight'>{data.Status}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Item:</span>
                <span class='value'>{data.ItemName}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Model Number:</span>
                <span class='value'>{data.ItemModelNumber ?? "N/A"}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Quantity:</span>
                <span class='value'>{data.Quantity}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Cartridge Condition:</span>
                <span class='value'>{cartridgeCondition}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Fulfillment Method:</span>
                <span class='value'>{fulfillmentMethod}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Date Requested:</span>
                <span class='value'>{data.DateRequested:yyyy-MM-dd HH:mm}</span>
            </div>

            <hr style='margin: 20px 0;'/>

            <div class='info-row'>
                <span class='label'>Destination Company:</span>
                <span class='value'>{data.CompanyName}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Destination Branch:</span>
                <span class='value'>{data.BranchName}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Destination Department:</span>
                <span class='value'>{data.DepartmentName}</span>
            </div>

            <hr style='margin: 20px 0;'/>

            <p><strong>Next Steps:</strong></p>
            <ul>
                <li>If <strong>PICKUP</strong>: Coordinate with the inventory team to collect the cartridge.</li>
                <li>If <strong>DELIVERY</strong>: The cartridge will be delivered to your branch address.</li>
            </ul>

            <p>For any questions, please contact the Inventory Management Team.</p>
        </div>
        <div class='footer'>
            <p>This is an automated notification from the Yakult Inventory Management System.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Sends email using SMTP.
        /// REUSES existing SMTP configuration.
        /// </summary>
        private void SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                using (var smtpClient = new SmtpClient(_smtpHost, _smtpPort))
                {
                    smtpClient.EnableSsl = _enableSsl;
                    smtpClient.UseDefaultCredentials = false;
                    smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_fromEmail, "Yakult Inventory System"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    smtpClient.Send(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log error (reuse existing logging infrastructure if available)
                throw new InvalidOperationException($"Failed to send email to {toEmail}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Helper method to check if a request should trigger notification.
        /// Call this when updating request status to COMPLETED.
        /// </summary>
        public bool ShouldSendNotification(int requestId, string oldStatus, string newStatus)
        {
            // Only send notification when status changes TO completed
            if (newStatus != "COMPLETED" || oldStatus == "COMPLETED")
                return false;

            // Check if this is a portal request
            const string sql = @"
                SELECT COUNT(*)
                FROM dbo.Request
                WHERE ReqId = @ReqId
                  AND Description LIKE '[[]PORTAL]%'";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReqId", requestId);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }
    }

    /// <summary>
    /// Internal DTO for notification data.
    /// </summary>
    internal class NotificationData
    {
        public int ReqId { get; set; }
        public DateTime DateRequested { get; set; }
        public string Status { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string Remarks { get; set; }
        public string ItemName { get; set; }
        public string ItemModelNumber { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string BranchEmail { get; set; }
        public string DepartmentName { get; set; }
        public string CompanyName { get; set; }
        public string DestinationEmployeeName { get; set; }
    }
}
