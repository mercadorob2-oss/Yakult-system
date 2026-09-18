using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models.ViewModels;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Sends cartridge request submission notification emails.
    ///
    /// Recipient resolution (same rules as EmailReceiptService):
    ///   1. Employee primary email  (dbo.EmployeeEmail → dbo.EmailAddress)
    ///   2. Fallback: branch email  (dbo.Branch.EmailId → dbo.EmailAddress)
    ///
    /// Never throws – all errors are logged so email failures never block the UI.
    /// </summary>
    public class CartridgeRequestEmailService
    {
        private readonly ISmtpService _smtpService;
        private readonly EmailRepository _emailRepository;

        public CartridgeRequestEmailService()
        {
            _emailRepository = new EmailRepository();
            _smtpService = new SmtpService(_emailRepository);
        }

        /// <summary>
        /// Sends a "request submitted" notification for a batch of portal cartridge requests.
        /// Call fire-and-forget (do not await on the UI thread).
        /// </summary>
        public async Task SendRequestSubmittedAsync(
            int empId,
            string employeeName,
            string branchName,
            string departmentName,
            string fulfillmentMethod,
            List<CartridgeRequestItemViewModel> items,
            List<int> requestIds,
            string additionalRemarks)
        {
            try
            {
                Logger.LogInfo(
                    $"CartridgeRequestEmailService: Preparing notification for " +
                    $"EmpId={empId}, Requests=[{string.Join(",", requestIds)}]");

                // ── Resolve recipients (parent/child hierarchy) ────────────────────
                var recipients = await ResolveRecipientsAsync(empId);

                if (recipients.Count == 0)
                {
                    Logger.LogWarning(
                        $"CartridgeRequestEmailService: No recipient email found for EmpId={empId} " +
                        $"– email skipped for Requests=[{string.Join(",", requestIds)}]");
                    return;
                }

                // Join multiple addresses with comma — MailMessage.To.Add() supports this
                string recipientList = string.Join(",", recipients);
                Logger.LogInfo($"CartridgeRequestEmailService: Resolved recipients=[{recipientList}]");

                // ── Build email ────────────────────────────────────────────────────
                string subject =
                    $"Cartridge Request Submitted – {employeeName} " +
                    $"(#{string.Join(", #", requestIds)})";

                string body = BuildEmailBody(
                    employeeName, branchName, departmentName,
                    fulfillmentMethod, items, requestIds, additionalRemarks);

                // ── Send ───────────────────────────────────────────────────────────
                Logger.LogInfo(
                    $"CartridgeRequestEmailService: Sending to [{recipientList}] " +
                    $"for Requests=[{string.Join(",", requestIds)}]");

                bool success = await _smtpService.SendEmailAsync(recipientList, subject, body, isHtml: true);

                if (success)
                    Logger.LogInfo(
                        $"CartridgeRequestEmailService: Email sent to [{recipientList}] " +
                        $"for Requests=[{string.Join(",", requestIds)}]");
                else
                    Logger.LogWarning(
                        $"CartridgeRequestEmailService: SmtpService returned false for [{recipientList}] " +
                        $"– Requests=[{string.Join(",", requestIds)}]");
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"CartridgeRequestEmailService: Unexpected error for EmpId={empId}, " +
                    $"Requests=[{string.Join(",", requestIds)}]",
                    ex);
            }
        }

        // ── Recipient resolution ───────────────────────────────────────────────────
        // Priority: child (personal email, IsPrimary=1) + parent (dept email).
        // Both are returned when both exist; falls back to branch email as last resort.

        private async Task<List<string>> ResolveRecipientsAsync(int empId)
        {
            try
            {
                if (empId > 0)
                {
                    var empEmails = await _emailRepository.GetEmployeeEmailRecipientsAsync(empId);
                    if (empEmails.Count > 0)
                    {
                        Logger.LogInfo(
                            $"CartridgeRequestEmailService: Resolved {empEmails.Count} recipient(s) " +
                            $"for EmpId={empId}: [{string.Join(",", empEmails)}]");
                        return empEmails;
                    }

                    Logger.LogInfo(
                        $"CartridgeRequestEmailService: No employee email for EmpId={empId}, trying branch fallback");
                }

                // Branch email last resort
                int? branchId = await GetEmployeeBranchIdAsync(empId);
                if (branchId.HasValue)
                {
                    string branchEmail = await _emailRepository.GetBranchEmailAsync(branchId.Value);
                    if (!string.IsNullOrWhiteSpace(branchEmail))
                    {
                        Logger.LogInfo(
                            $"CartridgeRequestEmailService: Using branch email for BranchId={branchId}");
                        return new List<string> { branchEmail };
                    }

                    Logger.LogWarning(
                        $"CartridgeRequestEmailService: No branch email for BranchId={branchId}");
                }
                else
                {
                    Logger.LogWarning(
                        $"CartridgeRequestEmailService: Could not resolve BranchId for EmpId={empId}");
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"CartridgeRequestEmailService.ResolveRecipientsAsync failed for EmpId={empId}", ex);
                return new List<string>();
            }
        }

        private async Task<int?> GetEmployeeBranchIdAsync(int empId)
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(
                        "SELECT BranchId FROM dbo.Employee WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            return Convert.ToInt32(result);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"CartridgeRequestEmailService.GetEmployeeBranchIdAsync failed for EmpId={empId}", ex);
                return null;
            }
        }

        // ── Email body ─────────────────────────────────────────────────────────────

        private string BuildEmailBody(
            string employeeName,
            string branchName,
            string departmentName,
            string fulfillmentMethod,
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
  .header { background-color: #9B59B6; color: white; padding: 20px 24px; border-radius: 4px 4px 0 0; }
  .header h2 { margin: 0; font-size: 20px; }
  .content { background: #f9f9f9; padding: 24px; border: 1px solid #ddd; border-top: none; }
  .footer { text-align: center; padding: 12px; font-size: 11px; color: #999; }
  .badge { background: #28a745; color: white; padding: 3px 10px; border-radius: 3px; font-size: 12px; font-weight: bold; }
  table.info { width: 100%; border-collapse: collapse; margin-top: 8px; }
  table.info td { padding: 7px 10px; border-bottom: 1px solid #eee; font-size: 14px; }
  table.info td.label { font-weight: bold; color: #555; width: 160px; }
  table.items { width: 100%; border-collapse: collapse; margin-top: 8px; }
  table.items th { background: #9B59B6; color: white; padding: 8px 10px; text-align: left; font-size: 13px; }
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

            // ── Request summary ────────────────────────────────────────────────
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

            // ── Items table ────────────────────────────────────────────────────
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
                        WebUtility.HtmlEncode(item.CartridgeCondition ?? "N/A"));
                }

                sb.Append("</table>");
            }

            // ── Remarks ────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(additionalRemarks))
            {
                sb.AppendFormat(
                    "<p style='margin-top:14px'><strong>Additional Remarks:</strong> {0}</p>",
                    WebUtility.HtmlEncode(additionalRemarks));
            }

            sb.Append(
                "<p style='margin-top:20px; color:#777; font-size:13px;'>" +
                "The IT team will process your request and notify you when it is ready for pickup or delivery.</p>");

            sb.Append(
                "</div>" +
                "<div class='footer'>This is an automated notification from Yakult Inventory Management System. " +
                "Please do not reply to this email.</div>" +
                "</div></body></html>");

            return sb.ToString();
        }
    }
}
