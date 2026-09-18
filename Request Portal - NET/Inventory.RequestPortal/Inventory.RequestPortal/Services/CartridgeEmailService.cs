using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
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
