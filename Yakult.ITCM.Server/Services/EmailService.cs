using System.Net.Mail;
using System.Text.RegularExpressions;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Models;

namespace Yakult.ITCM.Server.Services;

public sealed class EmailSendResult
{
    public bool SentSuccessfully { get; }
    public bool WasSkipped { get; }
    public int RecipientCount { get; }
    public string? Message { get; }

    private EmailSendResult(bool sent, bool skipped, int recipients, string? message)
    {
        SentSuccessfully = sent;
        WasSkipped = skipped;
        RecipientCount = recipients;
        Message = message;
    }

    public static EmailSendResult Sent(int recipients) => new(true, false, recipients, null);
    public static EmailSendResult Skipped(string reason) => new(false, true, 0, reason);
    public static EmailSendResult Failed(string error) => new(false, false, 0, error);
}

public sealed class EmailService
{
    private readonly IItcmRepository _repo;
    private readonly ILogger<EmailService> _logger;

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<key2>[A-Za-z0-9_]+)\s*\}",
        RegexOptions.Compiled);

    public EmailService(IItcmRepository repo, ILogger<EmailService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<EmailSendResult> NotifyReminderAsync(int ticketId, int? triggeredByUserId)
    {
        var rules = await _repo.GetNotificationRulesAsync();
        if (rules is null || !rules.NotifyOnReminder)
            return await SkipAndLogAsync(ticketId, "Reminder",
                "Reminder notifications are disabled.", triggeredByUserId);

        var template = await _repo.GetEmailTemplateByTypeAsync("Reminder");
        if (template is null || !template.IsActive)
            return await SkipAndLogAsync(ticketId, "Reminder",
                "Reminder template is missing or inactive.", triggeredByUserId);

        return await SendUsingTicketAsync(ticketId, "Reminder", template,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            triggeredByUserId);
    }

    public async Task<EmailSendResult> NotifyStatusChangeAsync(
        int ticketId, string oldStatus, string newStatus, string? note, int? triggeredByUserId,
        IEnumerable<int>? employeeRecipientEmpIds = null)
    {
        var rules = await _repo.GetNotificationRulesAsync();
        if (rules is null)
            return await SkipAndLogAsync(ticketId, "StatusUpdate",
                "Notification rules are not configured.", triggeredByUserId);

        var isEscalated = string.Equals(newStatus, "Escalated", StringComparison.OrdinalIgnoreCase);
        var templateType = isEscalated ? "Escalation" : "StatusUpdate";

        if (isEscalated && !rules.NotifyOnEscalation)
            return await SkipAndLogAsync(ticketId, "Escalation",
                "Escalation notifications are disabled.", triggeredByUserId);

        if (!isEscalated && !rules.NotifyOnStatusChange)
            return await SkipAndLogAsync(ticketId, "StatusUpdate",
                "Status change notifications are disabled.", triggeredByUserId);

        var template = await _repo.GetEmailTemplateByTypeAsync(templateType);
        if (template is null || !template.IsActive)
            return await SkipAndLogAsync(ticketId, templateType,
                $"{templateType} template is missing or inactive.", triggeredByUserId);

        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OldStatus"] = oldStatus ?? string.Empty,
            ["NewStatus"] = newStatus ?? string.Empty,
            ["Note"] = note ?? string.Empty
        };

        return await SendUsingTicketAsync(ticketId, templateType, template,
            placeholders, triggeredByUserId,
            successStatus: "Sent", employeeRecipientEmpIds: employeeRecipientEmpIds);
    }

    public async Task<EmailSendResult> NotifyAssignmentAsync(
        int ticketId,
        int assignedToEmpId,
        int? triggeredByUserId,
        int? previousAssignedToEmpId = null,
        string? previousAssignee = null)
    {
        var isReassignment = previousAssignedToEmpId.HasValue
            && previousAssignedToEmpId.Value > 0
            && previousAssignedToEmpId.Value != assignedToEmpId;
        var emailType = isReassignment ? "Reassignment" : "Assignment";

        if (assignedToEmpId <= 0)
            return await SkipAndLogAsync(ticketId, emailType, "No IT employee was assigned.", triggeredByUserId);

        var template = await _repo.GetEmailTemplateByTypeAsync(emailType);
        if (template is null || !template.IsActive)
            return await SkipAndLogAsync(ticketId, emailType, $"{emailType} template is missing or inactive.", triggeredByUserId);

        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (isReassignment)
        {
            placeholders["PreviousAssignee"] = string.IsNullOrWhiteSpace(previousAssignee)
                ? $"Employee #{previousAssignedToEmpId!.Value}"
                : previousAssignee.Trim();
        }

        return await SendUsingTicketAsync(ticketId, emailType, template,
            placeholders,
            triggeredByUserId,
            successStatus: "Sent",
            employeeRecipientEmpIds: new[] { assignedToEmpId });
    }

    public async Task SendTestEmailAsync(SmtpSenderConfig sender, string testRecipient)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (string.IsNullOrWhiteSpace(testRecipient))
            throw new ArgumentException("A test recipient is required.", nameof(testRecipient));

        var to = new System.Net.Mail.MailAddress(testRecipient.Trim()).Address;
        await SendEmailAsync(
            sender,
            [to],
            "[IT Call Monitoring] SMTP test",
            "This is a test message from the ITCM Server email configuration page. " +
            "If you received it, the SMTP sender settings are working.");
    }

    private async Task<EmailSendResult> SendUsingTicketAsync(
        int ticketId,
        string emailType,
        CallEmailTemplateItem template,
        Dictionary<string, string> placeholders,
        int? createdByUserId,
        string successStatus = "Sent",
        IEnumerable<int>? employeeRecipientEmpIds = null)
    {
        var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
        if (ticket is null)
            return await SkipAndLogAsync(ticketId, emailType, "Ticket not found.", createdByUserId);

        var sender = await _repo.GetSmtpSenderForTicketAsync(ticket.BranchId, ticket.DeptId);
        if (sender is null || string.IsNullOrWhiteSpace(sender.SmtpServer))
            return await SkipAndLogAsync(ticketId, emailType,
                $"SMTP not configured (BranchId={ticket.BranchId}, DeptId={ticket.DeptId}).",
                createdByUserId);

        // Ticket lifecycle notifications are private to the ticket contact and
        // recipients. Do not fall back to branch, department, or global
        // notification recipients. The current assignee and any explicitly
        // passed employee ids are unioned so hand-offs (e.g. escalation with
        // reassignment) still notify the previous owner.
        var recipients = new List<string>();
        var employeeIds = (ticket.AssignedToEmpId.HasValue
                ? new[] { ticket.AssignedToEmpId.Value }.Concat(employeeRecipientEmpIds ?? Enumerable.Empty<int>())
                : (employeeRecipientEmpIds ?? Enumerable.Empty<int>()))
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        foreach (var empId in employeeIds)
        {
            try
            {
                var employeeEmail = await _repo.GetEmployeePrimaryEmailAsync(empId);
                if (!string.IsNullOrWhiteSpace(employeeEmail))
                {
                    try
                    {
                        var address = new System.Net.Mail.MailAddress(employeeEmail).Address;
                        if (!recipients.Contains(address, StringComparer.OrdinalIgnoreCase))
                            recipients.Add(address);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Employee email lookup failed for EmpId={EmpId}", empId);
            }
        }

        if (!string.IsNullOrWhiteSpace(ticket.ContactEmail))
        {
            foreach (var contactEmail in ParseRecipients(ticket.ContactEmail))
            {
                if (!recipients.Contains(contactEmail, StringComparer.OrdinalIgnoreCase))
                    recipients.Add(contactEmail);
            }
        }

        if (recipients.Count == 0)
            return await SkipAndLogAsync(ticketId, emailType, "No valid recipient email resolved.", createdByUserId);

        var basePlaceholders = BuildBasePlaceholders(ticket);
        foreach (var kvp in placeholders)
            basePlaceholders[kvp.Key] = kvp.Value;

        var subject = Render(template.Subject, basePlaceholders);
        var body = Render(template.Body, basePlaceholders);

        if (string.IsNullOrWhiteSpace(subject))
            subject = $"[IT Call Monitoring] Ticket {ticket.TicketCode ?? ticket.TicketId.ToString()}";
        if (string.IsNullOrWhiteSpace(body))
            body = $"Ticket {ticket.TicketCode ?? ticket.TicketId.ToString()}";

        try
        {
            await SendEmailAsync(sender, recipients, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ITCM email send failed (TicketId={TicketId}, EmailType={EmailType})", ticketId, emailType);
            await _repo.LogEmailAsync(ticketId, emailType, string.Join(", ", recipients), subject, "Failed", ex.Message, createdByUserId);
            return EmailSendResult.Failed(ex.Message);
        }

        try
        {
            await _repo.LogEmailAsync(ticketId, emailType, string.Join(", ", recipients), subject, successStatus, null, createdByUserId);
        }
        catch (Exception ex)
        {
            // The mail already went out: a log-write failure must not report
            // Failed (which would make reminders resend forever).
            _logger.LogWarning(ex, "ITCM email log write failed (TicketId={TicketId}, EmailType={EmailType})", ticketId, emailType);
        }
        return EmailSendResult.Sent(recipients.Count);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v?.Trim();
        }
        return null;
    }

    private static async Task SendEmailAsync(SmtpSenderConfig sender, IReadOnlyList<string> recipients, string subject, string body)
    {
        var configuredFromEmail = (sender.FromEmail ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configuredFromEmail) && !IsValidSingleEmail(configuredFromEmail))
            throw new InvalidOperationException("FromEmail is invalid. Correct the SMTP sender address or leave it blank to use the SMTP Username.");

        var fromEmail = GetFirstValidEmail(configuredFromEmail, sender.SmtpUsername);
        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("FromEmail is invalid.");

        using var message = new MailMessage();
        message.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(sender.FromName) ? "IT Call Monitoring" : sender.FromName);
        foreach (var to in recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
            message.To.Add(to);

        if (message.To.Count == 0)
            throw new InvalidOperationException("No valid recipients.");

        message.Subject = subject ?? string.Empty;
        message.Body = body ?? string.Empty;
        message.BodyEncoding = System.Text.Encoding.UTF8;
        message.SubjectEncoding = System.Text.Encoding.UTF8;
        message.IsBodyHtml = false;

        using var client = new SmtpClient(sender.SmtpServer, sender.SmtpPort <= 0 ? 587 : sender.SmtpPort);
        client.EnableSsl = sender.UseSsl;

        if (!string.IsNullOrWhiteSpace(sender.SmtpUsername))
        {
            string password;
            if (sender.SmtpPasswordEnc is { Length: > 0 })
            {
                // Fail fast with an explicit decrypt error instead of sending
                // an empty password (generic SMTP-auth failure, possible lockout).
                password = SecretProtector.UnprotectToString(sender.SmtpPasswordEnc)
                    ?? throw new InvalidOperationException("SMTP password decrypt failed. Re-save the SMTP settings to restore sending.");
            }
            else
            {
                password = string.Empty;
            }
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(sender.SmtpUsername, password);
        }
        else
        {
            client.UseDefaultCredentials = true;
        }

        await client.SendMailAsync(message);
    }

    private static string? GetFirstValidEmail(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var value = (candidate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            try
            {
                _ = new MailAddress(value);
                return value;
            }
            catch { }
        }
        return null;
    }

    private static bool IsValidSingleEmail(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<EmailSendResult> SkipAndLogAsync(
        int ticketId, string emailType, string reason, int? createdByUserId)
    {
        try
        {
            if (ticketId > 0)
                await _repo.LogEmailAsync(ticketId, emailType, string.Empty, null, "Skipped", reason, createdByUserId);
        }
        catch { }

        _logger.LogWarning("ITCM email skipped (TicketId={TicketId}, EmailType={EmailType}): {Reason}",
            ticketId, emailType, reason);
        return EmailSendResult.Skipped(reason);
    }

    private static Dictionary<string, string> BuildBasePlaceholders(CallTicketNotificationData ticket)
    {
        var nowUtc = DateTime.UtcNow;
        var idleDays = 0;
        if (ticket.LastContactAt.HasValue)
            idleDays = Math.Max(0, (int)Math.Floor((nowUtc - DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc)).TotalDays));

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TicketId"] = ticket.TicketId.ToString(),
            ["TicketCode"] = ticket.TicketCode ?? string.Empty,
            ["Company"] = ticket.Company ?? string.Empty,
            ["Department"] = ticket.Department ?? string.Empty,
            ["Branch"] = ticket.Branch ?? string.Empty,
            ["CallerName"] = ticket.CallerName ?? string.Empty,
            ["Issue"] = ticket.Issue ?? string.Empty,
            ["IssueType"] = ticket.IssueType ?? string.Empty,
            ["Priority"] = ticket.Priority ?? string.Empty,
            ["Status"] = ticket.Status ?? string.Empty,
            ["AssignedTo"] = ticket.AssignedTo ?? string.Empty,
            // Default so {{PreviousAssignee}} never renders literally on
            // non-reassignment mails (assignment path overrides when set).
            ["PreviousAssignee"] = string.Empty,
            ["CreatedAt"] = ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ["UpdatedAt"] = ticket.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
            ["LastContactAt"] = ticket.LastContactAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
            ["LastReminderSentAt"] = ticket.LastReminderSentAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
            ["IdleDays"] = idleDays.ToString(),
            ["SolvedAt"] = ticket.SolvedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
            ["ProvidedSolution"] = ticket.ProvidedSolution ?? string.Empty
        };
    }

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template) || values is null || values.Count == 0)
            return template ?? string.Empty;

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Success ? match.Groups["key"].Value : match.Groups["key2"].Value;
            return !string.IsNullOrWhiteSpace(key) && values.TryGetValue(key, out var val) ? (val ?? string.Empty) : match.Value;
        });
    }

    private static List<string> ParseRecipients(string? recipientText)
    {
        if (string.IsNullOrWhiteSpace(recipientText))
            return [];

        return recipientText
            .Split([';', ',', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p =>
            {
                try { return new MailAddress(p).Address?.Trim(); }
                catch { return null; }
            })
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }
}
