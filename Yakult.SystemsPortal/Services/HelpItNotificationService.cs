using System.Net;
using System.Net.Mail;
using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

public sealed record HelpItNotificationResult(bool Sent, string Message);

public interface IHelpItNotificationService
{
    Task<HelpItNotificationResult> NotifyAsync(HelpItTicketSummary ticket, string contactEmail, string detailsUrl);
}

/// <summary>Portal intake notifications have a dedicated support recipient, separate from requester contact details.</summary>
public sealed class HelpItNotificationService(
    IConfiguration configuration,
    ILogger<HelpItNotificationService> logger) : IHelpItNotificationService
{
    public async Task<HelpItNotificationResult> NotifyAsync(HelpItTicketSummary ticket, string contactEmail, string detailsUrl)
    {
        var settings = configuration.GetSection("HelpIt:Email");
        var recipient = settings["SupportRecipient"];
        var server = settings["SmtpServer"];
        var from = settings["FromEmail"];
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning("Help IT notification skipped for {TicketCode}: support recipient or SMTP is not configured.", ticket.TicketCode);
            return new(false, "Your ticket is saved in the IT queue. The email notification is unavailable; you do not need to submit again.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from, settings["FromName"] ?? "Yakult Employee Portal"),
                Subject = $"[IT Support] {ticket.TicketCode} | {ticket.IssueType}",
                Body = BuildBody(ticket, contactEmail, detailsUrl),
                IsBodyHtml = false
            };
            // Never use the organization contact or a global ITD address as the intake recipient.
            message.To.Add(new MailAddress(recipient));
            if (MailAddress.TryCreate(contactEmail, out var replyTo))
                message.ReplyToList.Add(replyTo);
            else
                logger.LogWarning("Help IT notification for {TicketCode} has no usable Reply-To address.", ticket.TicketCode);
            using var smtp = new SmtpClient(server, settings.GetValue("SmtpPort", 587))
            {
                EnableSsl = settings.GetValue("UseSsl", true),
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrWhiteSpace(settings["Username"]))
                smtp.Credentials = new NetworkCredential(settings["Username"], settings["Password"]);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await smtp.SendMailAsync(message, timeout.Token);
            logger.LogInformation("Help IT notification sent for {TicketCode}.", ticket.TicketCode);
            return new(true, "IT Support has been notified by email.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Help IT notification failed for saved ticket {TicketCode}.", ticket.TicketCode);
            return new(false, "Your ticket is saved in the IT queue, but the email notification could not be sent. You do not need to submit again.");
        }
    }

    public static string BuildBody(HelpItTicketSummary ticket, string contactEmail, string detailsUrl) =>
        $"New employee portal ticket: {ticket.TicketCode}\n\n" +
        $"Issue Type: {ticket.IssueType}\nClassification: {ticket.Classification}\n" +
        $"Device / application name: {ticket.DeviceName ?? "Not provided"}\n\nConcern:\n{ticket.Issue}\n\n" +
        $"Requester: {ticket.CallerName}\nCompany: {ticket.Company}\nDepartment: {ticket.Department}\nBranch: {ticket.Branch}\n" +
        $"Contact email: {contactEmail}\nStatus: {ticket.Status}\nPriority: {ticket.Priority}\n\nTrack ticket: {detailsUrl}\n";
}
