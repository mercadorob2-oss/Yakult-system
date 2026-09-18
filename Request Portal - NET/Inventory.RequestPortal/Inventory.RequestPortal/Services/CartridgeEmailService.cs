using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Inventory.RequestPortal.Services
{
    /// <summary>
    /// Sends cartridge request notification emails for the web portal.
    ///
    /// Recipient resolution rules (identical to WinForms CartridgeRequestEmailService):
    ///   1. Employee primary email  (dbo.EmployeeEmail → dbo.EmailAddress)
    ///   2. Fallback: branch email  (dbo.Branch.EmailId → dbo.EmailAddress)
    ///
    /// SMTP configuration comes from appsettings.json "Smtp" section so that both
    /// portals can be pointed at the same mail server without duplicating DB logic.
    ///
    /// Never throws – all errors are logged so they never surface to the requester.
    /// </summary>
    public class CartridgeEmailService : ICartridgeEmailService
    {
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CartridgeEmailService> _logger;

        public CartridgeEmailService(
            IConnectionStringProvider connectionStringProvider,
            IConfiguration configuration,
            ILogger<CartridgeEmailService> logger)
        {
            _connectionStringProvider = connectionStringProvider;
            _configuration = configuration;
            _logger = logger;
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public async Task SendRequestSubmittedAsync(
            int empId,
            string employeeName,
            string branchName,
            string departmentName,
            string fulfillmentMethod,
            string cartridgeCondition,
            List<CartridgeRequestItemViewModel> items,
            List<int> requestIds,
            string additionalRemarks)
        {
            try
            {
                _logger.LogInformation(
                    "CartridgeEmailService: Preparing notification for EmpId={EmpId}, Requests=[{Ids}]",
                    empId, string.Join(",", requestIds));

                // ── Resolve recipient ──────────────────────────────────────────
                string recipient = await ResolveRecipientAsync(empId);

                if (string.IsNullOrWhiteSpace(recipient))
                {
                    _logger.LogWarning(
                        "CartridgeEmailService: No recipient email for EmpId={EmpId} – " +
                        "email skipped for Requests=[{Ids}]",
                        empId, string.Join(",", requestIds));
                    return;
                }

                _logger.LogInformation(
                    "CartridgeEmailService: Resolved recipient={Recipient}", recipient);

                // ── Build email ────────────────────────────────────────────────
                string subject =
                    $"Cartridge Request Submitted – {employeeName} " +
                    $"(#{string.Join(", #", requestIds)})";

                string body = BuildEmailBody(
                    employeeName, branchName, departmentName,
                    fulfillmentMethod, cartridgeCondition, items, requestIds, additionalRemarks);

                // ── Send ───────────────────────────────────────────────────────
                _logger.LogInformation(
                    "CartridgeEmailService: Sending to {Recipient} for Requests=[{Ids}]",
                    recipient, string.Join(",", requestIds));

                await SendEmailAsync(recipient, subject, body);

                _logger.LogInformation(
                    "CartridgeEmailService: Email sent to {Recipient} for Requests=[{Ids}]",
                    recipient, string.Join(",", requestIds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CartridgeEmailService: Unexpected error for EmpId={EmpId}, Requests=[{Ids}]",
                    empId, string.Join(",", requestIds));
            }
        }

        // ── Send Notifications (Cartridge Management) ─────────────────────────────

        public (string Subject, string Body) BuildFulfillmentNotificationEmail(
            FulfilledSetNotificationDto row,
            FulfilledCartridgeDetailDto? detail,
            string? notes)
        {
            string setCode = row.SetCode ?? "N/A";
            string createdAt = row.CreatedAt == DateTime.MinValue ? "N/A" : row.CreatedAt.ToString("MM/dd/yyyy HH:mm");
            string distributionMethod = string.IsNullOrWhiteSpace(row.DistributionMethod) || row.DistributionMethod == "N/A"
                ? "N/A" : row.DistributionMethod;
            string receivedBy = string.IsNullOrWhiteSpace(row.ReceivedByName) ? "N/A" : row.ReceivedByName;

            var modelRows = new StringBuilder();
            int totalBrandNew = 0, totalRefilled = 0, totalPending = 0;

            var models = detail?.Models ?? new List<FulfilledCartridgeModelLineDto>();
            if (models.Count == 0)
            {
                // Fall back to the Set-level totals when no per-model breakdown is available.
                int returnedEmpty = row.IssuedBrandNewQty + row.IssuedRefilledQty;
                totalBrandNew = row.IssuedBrandNewQty;
                totalRefilled = row.IssuedRefilledQty;
                modelRows.Append(BuildModelRow("—", returnedEmpty, row.IssuedBrandNewQty, row.IssuedRefilledQty, 0));
            }
            else if (models.Count == 1)
            {
                var m = models[0];
                int brandNew = row.IssuedBrandNewQty;
                int refilled = row.IssuedRefilledQty;
                int pending = Math.Max(0, m.RequestedQty - brandNew - refilled);
                totalBrandNew += brandNew;
                totalRefilled += refilled;
                totalPending += pending;
                modelRows.Append(BuildModelRow(m.CartridgeModel, m.RequestedQty, brandNew, refilled, pending));
            }
            else
            {
                foreach (var m in models)
                {
                    bool isFulfilled = string.Equals(m.Status, "Fulfilled", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(m.Status, "Completed", StringComparison.OrdinalIgnoreCase);
                    bool isPending = string.Equals(m.Status, "Pending", StringComparison.OrdinalIgnoreCase);
                    int issued = isPending ? 0 : (isFulfilled ? m.RequestedQty : m.RequestedQty / 2);
                    int pending = Math.Max(0, m.RequestedQty - issued);
                    totalBrandNew += issued;
                    totalPending += pending;
                    modelRows.Append(BuildModelRow(m.CartridgeModel, m.RequestedQty, issued, 0, pending));
                }
            }

            int totalIssuedFull = totalBrandNew + totalRefilled;
            string distributionStatus = totalIssuedFull == 0
                ? "Pending Distribution"
                : totalPending > 0
                    ? "Partially Distributed"
                    : "Distributed and Dispatched";

            string subject = $"Cartridge Fulfillment Notification – Set {setCode} ({distributionStatus})";

            string notesRow = string.Empty;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                string safeNotes = WebUtility.HtmlEncode(notes).Replace("\r\n", "<br>").Replace("\n", "<br>");
                notesRow =
                    "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Notes:</strong></td>" +
                    $"<td style=\"padding:8px 0;\">{safeNotes}</td></tr>";
            }

            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body { font-family: Arial, sans-serif; color: #333; margin: 0; padding: 0; }
  .container { max-width: 640px; margin: 20px auto; }
  .header { background-color: #0066cc; color: white; padding: 20px 24px; border-radius: 4px 4px 0 0; }
  .header h2 { margin: 0; font-size: 20px; }
  .content { background: #f9f9f9; padding: 24px; border: 1px solid #ddd; border-top: none; }
  .footer { text-align: center; padding: 12px; font-size: 11px; color: #999; }
  table.info { width: 100%; border-collapse: collapse; margin-top: 8px; }
  table.info td { padding: 7px 10px; border-bottom: 1px solid #eee; font-size: 14px; }
  table.info td.label { font-weight: bold; color: #555; width: 170px; }
  table.models { width: 100%; border-collapse: collapse; margin-top: 12px; }
  table.models th { background: #0066cc; color: white; padding: 8px 10px; text-align: center; font-size: 12px; }
  table.models th:first-child { text-align: left; }
</style>
</head>
<body>
<div class='container'>
  <div class='header'><h2>Cartridge Fulfillment Notification</h2></div>
  <div class='content'>");

            sb.AppendFormat(
                "<p>Dear <strong>{0}</strong>,</p><p>Your cartridge exchange (Set <strong>{1}</strong>) is now <strong>{2}</strong>.</p>",
                WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(row.RequesterName) ? "Requester" : row.RequesterName),
                WebUtility.HtmlEncode(setCode),
                WebUtility.HtmlEncode(distributionStatus));

            sb.Append("<table class='info'>");
            sb.AppendFormat(
                "<tr><td class='label'>Requester:</td><td>{0}</td></tr>" +
                "<tr><td class='label'>Company:</td><td>{1}</td></tr>" +
                "<tr><td class='label'>Branch / Department:</td><td>{2}</td></tr>" +
                "<tr><td class='label'>Distribution Method:</td><td>{3}</td></tr>" +
                "<tr><td class='label'>Received By:</td><td>{4}</td></tr>" +
                "<tr><td class='label'>Date:</td><td>{5}</td></tr>{6}",
                WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(row.RequesterName) ? "N/A" : row.RequesterName),
                WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(row.CompanyName) ? "N/A" : row.CompanyName),
                WebUtility.HtmlEncode(row.BranchDept),
                WebUtility.HtmlEncode(distributionMethod),
                WebUtility.HtmlEncode(receivedBy),
                WebUtility.HtmlEncode(createdAt),
                notesRow);
            sb.Append("</table>");

            sb.Append(
                "<table class='models'><tr><th>Cartridge Model</th><th>Returned Empty</th>" +
                "<th>Brand New</th><th>Refilled</th><th>Pending</th></tr>");
            sb.Append(modelRows);
            sb.Append("</table>");

            sb.Append(
                "<p style='margin-top:20px; color:#777; font-size:13px;'>" +
                "Please contact the IT Department if you have any questions regarding this fulfillment.</p>" +
                "</div>" +
                "<div class='footer'>This is an automated notification from Yakult Inventory Management System. " +
                "Please do not reply to this email.</div>" +
                "</div></body></html>");

            return (subject, sb.ToString());
        }

        private static string BuildModelRow(string model, int returnedEmpty, int brandNew, int refilled, int pending)
        {
            return
                "<tr>" +
                $"<td style=\"border:1px solid #ddd; padding:8px;\">{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(model) ? "N/A" : model)}</td>" +
                $"<td style=\"border:1px solid #ddd; padding:8px; text-align:center;\">{returnedEmpty}</td>" +
                $"<td style=\"border:1px solid #ddd; padding:8px; text-align:center;\">{brandNew}</td>" +
                $"<td style=\"border:1px solid #ddd; padding:8px; text-align:center;\">{refilled}</td>" +
                $"<td style=\"border:1px solid #ddd; padding:8px; text-align:center;\">{pending}</td>" +
                "</tr>";
        }

        public async Task<string?> SendRenderedEmailAsync(int empId, string subject, string htmlBody)
        {
            try
            {
                string recipient = await ResolveRecipientAsync(empId);
                if (string.IsNullOrWhiteSpace(recipient))
                    return "No recipient email found for this requester (no employee or branch email on file).";

                await SendEmailAsync(recipient, subject, htmlBody);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CartridgeEmailService.SendRenderedEmailAsync failed for EmpId={EmpId}", empId);
                return $"Email failed: {ex.Message}";
            }
        }

        // ── Recipient resolution ───────────────────────────────────────────────────

        private async Task<string> ResolveRecipientAsync(int empId)
        {
            try
            {
                string cs = _connectionStringProvider.GetConnectionString();

                // 1. Employee primary email
                if (empId > 0)
                {
                    string empEmail = await GetEmployeePrimaryEmailAsync(cs, empId);
                    if (!string.IsNullOrWhiteSpace(empEmail))
                    {
                        _logger.LogInformation(
                            "CartridgeEmailService: Using employee email for EmpId={EmpId}", empId);
                        return empEmail;
                    }

                    _logger.LogInformation(
                        "CartridgeEmailService: No employee email for EmpId={EmpId}, trying branch",
                        empId);
                }

                // 2. Branch email fallback
                int? branchId = await GetEmployeeBranchIdAsync(cs, empId);
                if (branchId.HasValue)
                {
                    string branchEmail = await GetBranchEmailAsync(cs, branchId.Value);
                    if (!string.IsNullOrWhiteSpace(branchEmail))
                    {
                        _logger.LogInformation(
                            "CartridgeEmailService: Using branch email for BranchId={BranchId}",
                            branchId);
                        return branchEmail;
                    }

                    _logger.LogWarning(
                        "CartridgeEmailService: No branch email for BranchId={BranchId}", branchId);
                }
                else
                {
                    _logger.LogWarning(
                        "CartridgeEmailService: Could not resolve BranchId for EmpId={EmpId}", empId);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CartridgeEmailService.ResolveRecipientAsync failed for EmpId={EmpId}", empId);
                return null;
            }
        }

        /// <summary>Employee primary email from dbo.EmployeeEmail → dbo.EmailAddress.</summary>
        private async Task<string> GetEmployeePrimaryEmailAsync(string connectionString, int empId)
        {
            const string sql = @"
                SELECT TOP 1 ea.EmailAddress
                FROM dbo.EmployeeEmail ee
                INNER JOIN dbo.EmailAddress ea ON ee.EmailId = ea.EmailId
                WHERE ee.EmpId = @EmpId
                  AND ee.IsActive = 1
                  AND ea.IsActive = 1
                ORDER BY ee.EmailId";

            using var con = new SqlConnection(connectionString);
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@EmpId", empId);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        /// <summary>Employee's BranchId from dbo.Employee.</summary>
        private async Task<int?> GetEmployeeBranchIdAsync(string connectionString, int empId)
        {
            using var con = new SqlConnection(connectionString);
            await con.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT BranchId FROM dbo.Employee WHERE EmpId = @EmpId", con);
            cmd.Parameters.AddWithValue("@EmpId", empId);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
                return Convert.ToInt32(result);
            return null;
        }

        /// <summary>Branch email from dbo.Branch.EmailId → dbo.EmailAddress.</summary>
        private async Task<string> GetBranchEmailAsync(string connectionString, int branchId)
        {
            const string sql = @"
                SELECT ea.EmailAddress
                FROM dbo.Branch b
                INNER JOIN dbo.EmailAddress ea ON b.EmailId = ea.EmailId
                WHERE b.BranchId = @BranchId
                  AND ea.IsActive = 1";

            using var con = new SqlConnection(connectionString);
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@BranchId", branchId);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        // ── SMTP sending ───────────────────────────────────────────────────────────

        private async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            var smtpSection = _configuration.GetSection("Smtp");

            string host = smtpSection["Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning(
                    "CartridgeEmailService: Smtp:Host is not configured in appsettings.json – email skipped");
                return;
            }

            string portStr  = smtpSection["Port"]     ?? "587";
            string username = smtpSection["Username"] ?? string.Empty;
            string password = smtpSection["Password"] ?? string.Empty;
            string fromAddr = smtpSection["From"]     ?? username;
            string fromName = smtpSection["FromName"] ?? "Yakult Inventory System";
            bool   useSsl   = bool.TryParse(smtpSection["EnableSsl"], out bool ssl) && ssl;

            if (!int.TryParse(portStr, out int port))
                port = 587;

            using var message = new MailMessage();
            message.From    = new MailAddress(fromAddr, fromName);
            message.Subject = subject;
            message.Body    = htmlBody;
            message.IsBodyHtml = true;
            message.BodyEncoding    = Encoding.UTF8;
            message.SubjectEncoding = Encoding.UTF8;
            message.To.Add(to);

            using var smtp = new SmtpClient(host, port);
            smtp.EnableSsl             = useSsl;
            smtp.DeliveryMethod        = SmtpDeliveryMethod.Network;
            smtp.UseDefaultCredentials = false;

            if (!string.IsNullOrWhiteSpace(username))
                smtp.Credentials = new System.Net.NetworkCredential(username, password);

            await smtp.SendMailAsync(message);
        }

        // ── Email body ─────────────────────────────────────────────────────────────

        private string BuildEmailBody(
            string employeeName,
            string branchName,
            string departmentName,
            string fulfillmentMethod,
            string cartridgeCondition,
            List<CartridgeRequestItemViewModel> items,
            List<int> requestIds,
            string additionalRemarks)
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body { font-family: Arial, sans-serif; color: #333; margin: 0; padding: 0; }
  .container { max-width: 600px; margin: 20px auto; }
  .header { background-color: #0066cc; color: white; padding: 20px 24px; border-radius: 4px 4px 0 0; }
  .header h2 { margin: 0; font-size: 20px; }
  .content { background: #f9f9f9; padding: 24px; border: 1px solid #ddd; border-top: none; }
  .footer { text-align: center; padding: 12px; font-size: 11px; color: #999; }
  .badge { background: #28a745; color: white; padding: 3px 10px; border-radius: 3px; font-size: 12px; font-weight: bold; }
  table.info { width: 100%; border-collapse: collapse; margin-top: 8px; }
  table.info td { padding: 7px 10px; border-bottom: 1px solid #eee; font-size: 14px; }
  table.info td.label { font-weight: bold; color: #555; width: 160px; }
  table.items { width: 100%; border-collapse: collapse; margin-top: 8px; }
  table.items th { background: #0066cc; color: white; padding: 8px 10px; text-align: left; font-size: 13px; }
  table.items td { padding: 7px 10px; border-bottom: 1px solid #eee; font-size: 13px; }
  table.items tr:last-child td { border-bottom: none; }
</style>
</head>
<body>
<div class='container'>
  <div class='header'><h2>Cartridge Request Received</h2></div>
  <div class='content'>
");

            sb.AppendFormat(
                "<p>Dear <strong>{0}</strong>,</p>" +
                "<p>Your cartridge request has been <span class='badge'>SUBMITTED</span> and is now <strong>Under Review</strong>.</p>",
                WebUtility.HtmlEncode(employeeName));

            sb.Append("<p><strong>Request Details:</strong></p><table class='info'>");
            sb.AppendFormat(
                "<tr><td class='label'>Employee:</td><td>{0}</td></tr>" +
                "<tr><td class='label'>Branch:</td><td>{1}</td></tr>" +
                "<tr><td class='label'>Department:</td><td>{2}</td></tr>" +
                "<tr><td class='label'>Fulfillment:</td><td>{3}</td></tr>" +
                "<tr><td class='label'>Request ID(s):</td><td>{4}</td></tr>",
                WebUtility.HtmlEncode(employeeName),
                WebUtility.HtmlEncode(branchName ?? "N/A"),
                WebUtility.HtmlEncode(departmentName ?? "N/A"),
                WebUtility.HtmlEncode(fulfillmentMethod ?? "PICKUP"),
                string.Join(", ", requestIds.Select(id => $"#{id}")));
            sb.Append("</table>");

            if (items != null && items.Count > 0)
            {
                sb.Append(
                    "<p style='margin-top:16px'><strong>Requested Items:</strong></p>" +
                    "<table class='items'><tr><th>Cartridge Model</th><th>Qty</th><th>Condition</th></tr>");

                foreach (var item in items)
                {
                    sb.AppendFormat(
                        "<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                        WebUtility.HtmlEncode(item.CartridgeModel ?? "(unknown)"),
                        item.Quantity,
                        WebUtility.HtmlEncode(cartridgeCondition ?? "N/A"));
                }

                sb.Append("</table>");
            }

            if (!string.IsNullOrWhiteSpace(additionalRemarks))
            {
                sb.AppendFormat(
                    "<p style='margin-top:14px'><strong>Additional Remarks:</strong> {0}</p>",
                    WebUtility.HtmlEncode(additionalRemarks));
            }

            sb.Append(
                "<p style='margin-top:20px; color:#777; font-size:13px;'>" +
                "The IT team will process your request and notify you when it is ready for pickup or delivery.</p>" +
                "</div>" +
                "<div class='footer'>This is an automated notification from Yakult Inventory Management System. " +
                "Please do not reply to this email.</div>" +
                "</div></body></html>");

            return sb.ToString();
        }
    }
}
