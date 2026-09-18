using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yakult.ITCM.Scheduler
{
    internal sealed class StandaloneItcmRuntime
    {
        private const string GlobalBackgroundLockName = "Yakult.Inventory.App|CallMonitoring|BackgroundJobs";
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("Yakult.Inventory.App|SecretProtector|v1");
        private static readonly Regex PlaceholderRx = new Regex(@"\{\{\s*(?<k>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<k2>[A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

        private readonly string _connectionString;

        public StandaloneItcmRuntime(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ItcmRunSummary> RunAsync(int maxTickets, int? triggeredByUserId)
        {
            var summary = new ItcmRunSummary
            {
                LastRunStartUtc = DateTime.UtcNow,
                LastLockAcquired = null
            };

            using (var lockConnection = new SqlConnection(_connectionString))
            {
                await lockConnection.OpenAsync();
                summary.ConnectionVerified = true;
                summary.DatabaseName = lockConnection.Database;
                summary.TotalTicketCount = await GetTicketCountAsync(lockConnection, false);
                summary.OpenTicketCount = await GetTicketCountAsync(lockConnection, true);

                var acquired = await TryAcquireAppLockAsync(lockConnection, GlobalBackgroundLockName, 0);
                summary.LastLockAcquired = acquired;
                if (!acquired)
                {
                    summary.LastRunEndUtc = DateTime.UtcNow;
                    return summary;
                }

                try
                {
                    var reminderResult = await ProcessRemindersAsync(maxTickets, triggeredByUserId);
                    summary.ReminderCandidates = reminderResult.Candidates;
                    summary.RemindersSent = reminderResult.Successes;
                    summary.ReminderSkippedOrFailed = Math.Max(0, reminderResult.Candidates - reminderResult.Successes);

                    var escalationResult = await ProcessEscalationsAsync(maxTickets, triggeredByUserId);
                    summary.EscalationCandidates = escalationResult.Candidates;
                    summary.EscalationsApplied = escalationResult.Successes;
                    summary.EscalationSkippedOrFailed = Math.Max(0, escalationResult.Candidates - escalationResult.Successes);
                }
                catch (Exception ex)
                {
                    summary.LastError = ex.Message;
                    throw;
                }
                finally
                {
                    await ReleaseAppLockAsync(lockConnection, GlobalBackgroundLockName);
                    summary.LastRunEndUtc = DateTime.UtcNow;
                }
            }

            return summary;
        }

        private async Task<int?> GetTicketCountAsync(SqlConnection con, bool openOnly)
        {
            if (!await TableExistsAsync(con, "dbo.CallTicket"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = openOnly
                    ? @"
SELECT COUNT(1)
FROM dbo.CallTicket
WHERE Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed');"
                    : @"
SELECT COUNT(1)
FROM dbo.CallTicket;";

                var value = await cmd.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
            }
        }

        private async Task<BatchResult> ProcessRemindersAsync(int maxTickets, int? triggeredByUserId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                var rules = await GetNotificationRulesAsync(con);
                if (rules == null || !rules.NotifyOnReminder)
                    return BatchResult.Empty;

                var reminderDays = rules.ReminderDays;
                if (reminderDays < 1) reminderDays = 1;
                if (reminderDays > 60) reminderDays = 60;

                var ticketIds = await GetTicketIdsNeedingReminderAsync(con, reminderDays, maxTickets);
                if (ticketIds.Count == 0)
                    return BatchResult.Empty;

                var successes = 0;
                foreach (var ticketId in ticketIds)
                {
                    try
                    {
                        var sent = await NotifyReminderAsync(con, ticketId, rules, triggeredByUserId);
                        if (!sent)
                            continue;

                        await MarkReminderSentAsync(con, ticketId);
                        successes++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[{DateTime.UtcNow:u}] Reminder failed for ticket {ticketId}: {ex.Message}");
                    }
                }

                return new BatchResult { Candidates = ticketIds.Count, Successes = successes };
            }
        }

        private async Task<BatchResult> ProcessEscalationsAsync(int maxTickets, int? triggeredByUserId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                var ticketIds = await GetTicketIdsNeedingAutoEscalationAsync(con, maxTickets);
                if (ticketIds.Count == 0)
                    return BatchResult.Empty;

                int? autoAssigneeEmpId = null;
                try
                {
                    if (await EmployeeAssignmentEnabledAsync(con))
                        autoAssigneeEmpId = await GetAutoEscalationAssigneeEmpIdAsync(con);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:u}] Could not resolve auto-assignee: {ex.Message}");
                }

                var successes = 0;
                foreach (var ticketId in ticketIds)
                {
                    try
                    {
                        var ticket = await GetTicketNotificationDataAsync(con, ticketId);
                        if (ticket == null)
                            continue;

                        var oldStatus = (ticket.Status ?? string.Empty).Trim();
                        if (await ShouldSkipTicketAsync(con, ticketId, oldStatus))
                            continue;

                        var note = BuildAutoEscalationNote(ticket);
                        await SetTicketStatusAsync(con, ticketId, "Escalated", triggeredByUserId, note);

                        if (autoAssigneeEmpId.HasValue && autoAssigneeEmpId.Value > 0)
                        {
                            try
                            {
                                await AssignTicketEmployeeAsync(con, ticketId, autoAssigneeEmpId.Value, triggeredByUserId);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[{DateTime.UtcNow:u}] Auto-assign failed for ticket {ticketId}: {ex.Message}");
                            }
                        }

                        try
                        {
                            await NotifyStatusChangeAsync(con, ticketId, oldStatus, "Escalated", note, triggeredByUserId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.UtcNow:u}] Status-change notification failed for ticket {ticketId}: {ex.Message}");
                        }

                        successes++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[{DateTime.UtcNow:u}] Escalation processing failed for ticket {ticketId}: {ex.Message}");
                    }
                }

                return new BatchResult { Candidates = ticketIds.Count, Successes = successes };
            }
        }

        private async Task<bool> NotifyReminderAsync(SqlConnection con, int ticketId, NotificationRules rules, int? userId)
        {
            var template = await GetEmailTemplateByTypeAsync(con, "Reminder");
            if (template == null || !template.IsActive)
            {
                await LogEmailAsync(con, ticketId, "Reminder", null, null, "Skipped", "Reminder template missing or inactive.", userId);
                return false;
            }

            return await SendUsingTicketAsync(con, ticketId, "Reminder", rules, template, false, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), userId, "Sent");
        }

        private async Task<bool> NotifyStatusChangeAsync(SqlConnection con, int ticketId, string oldStatus, string newStatus, string note, int? userId)
        {
            var rules = await GetNotificationRulesAsync(con);
            if (rules == null)
            {
                await LogEmailAsync(con, ticketId, "StatusUpdate", null, null, "Skipped", "Notification rules are not configured.", userId);
                return false;
            }

            var isEscalated = string.Equals(NormalizeStatus(newStatus), "Escalated", StringComparison.OrdinalIgnoreCase);
            var templateType = isEscalated ? "Escalation" : "StatusUpdate";

            if (isEscalated && !rules.NotifyOnEscalation)
            {
                await LogEmailAsync(con, ticketId, "Escalation", null, null, "Skipped", "Escalation notifications are disabled.", userId);
                return false;
            }

            if (!isEscalated && !rules.NotifyOnStatusChange)
            {
                await LogEmailAsync(con, ticketId, "StatusUpdate", null, null, "Skipped", "Status change notifications are disabled.", userId);
                return false;
            }

            var template = await GetEmailTemplateByTypeAsync(con, templateType);
            if (template == null || !template.IsActive)
            {
                await LogEmailAsync(con, ticketId, templateType, null, null, "Skipped", templateType + " template missing or inactive.", userId);
                return false;
            }

            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            placeholders["OldStatus"] = oldStatus ?? string.Empty;
            placeholders["NewStatus"] = newStatus ?? string.Empty;
            placeholders["Note"] = note ?? string.Empty;

            return await SendUsingTicketAsync(con, ticketId, templateType, rules, template, isEscalated, placeholders, userId, "Sent");
        }

        private async Task<bool> SendUsingTicketAsync(SqlConnection con, int ticketId, string emailType, NotificationRules rules, EmailTemplate template, bool preferEscalationRecipients, Dictionary<string, string> placeholders, int? userId, string successStatus)
        {
            var ticket = await GetTicketNotificationDataAsync(con, ticketId);
            if (ticket == null)
            {
                await LogEmailAsync(con, ticketId, emailType, null, null, "Skipped", "Ticket not found.", userId);
                return false;
            }

            var sender = await GetSmtpSenderForTicketAsync(con, ticket.BranchId, ticket.DeptId);
            if (sender == null || string.IsNullOrWhiteSpace(sender.SmtpServer))
            {
                await LogEmailAsync(con, ticketId, emailType, null, null, "Skipped", "SMTP is not configured.", userId);
                return false;
            }

            var recipients = await ResolveRecipientsAsync(con, ticket.DeptId, ticket.BranchId, rules, preferEscalationRecipients);
            if (recipients.Count == 0)
            {
                await LogEmailAsync(con, ticketId, emailType, null, null, "Skipped", "No recipients resolved for this ticket.", userId);
                return false;
            }

            var basePlaceholders = BuildBasePlaceholders(ticket);
            if (placeholders != null)
            {
                foreach (var kvp in placeholders)
                    basePlaceholders[kvp.Key] = kvp.Value ?? string.Empty;
            }

            var subject = Render(template.Subject ?? string.Empty, basePlaceholders);
            var body = Render(template.Body ?? string.Empty, basePlaceholders);

            if (string.IsNullOrWhiteSpace(subject))
                subject = "[IT Call Monitoring] Ticket " + (ticket.TicketCode ?? ticket.TicketId.ToString());

            if (string.IsNullOrWhiteSpace(body))
                body = "Ticket " + (ticket.TicketCode ?? ticket.TicketId.ToString());

            try
            {
                await SendEmailAsync(sender, recipients, subject, body);
                await LogEmailAsync(con, ticketId, emailType, string.Join(", ", recipients), subject, successStatus ?? "Sent", null, userId);
                return true;
            }
            catch (Exception ex)
            {
                await LogEmailAsync(con, ticketId, emailType, string.Join(", ", recipients), subject, "Failed", ex.Message, userId);
                return false;
            }
        }

        private async Task<List<string>> ResolveRecipientsAsync(SqlConnection con, int? deptId, int? branchId, NotificationRules rules, bool preferEscalationRecipients)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            BranchNotificationRecipient branchRecipient = null;
            DepartmentNotificationRecipient deptRecipient = null;
            string branchEmail = null;

            if (branchId.HasValue && branchId.Value > 0)
            {
                branchRecipient = await GetBranchNotificationRecipientAsync(con, branchId.Value);
                if ((branchRecipient == null || (string.IsNullOrWhiteSpace(branchRecipient.RecipientEmails) && string.IsNullOrWhiteSpace(branchRecipient.EscalationEmails))))
                    branchEmail = await GetBranchEmailAsync(con, branchId.Value);
            }

            if (deptId.HasValue && deptId.Value > 0)
                deptRecipient = await GetDepartmentNotificationRecipientAsync(con, deptId.Value);

            if (preferEscalationRecipients)
            {
                AddEmails(recipients, branchRecipient == null ? null : branchRecipient.EscalationEmails);
                AddEmails(recipients, branchRecipient == null ? null : branchRecipient.RecipientEmails);
                AddEmails(recipients, branchEmail);
                AddEmails(recipients, deptRecipient == null ? null : deptRecipient.EscalationEmails);
                AddEmails(recipients, deptRecipient == null ? null : deptRecipient.RecipientEmails);
                AddEmails(recipients, rules == null ? null : rules.EscalationEmail);
                AddEmails(recipients, rules == null ? null : rules.GroupEmail);
            }
            else
            {
                AddEmails(recipients, branchRecipient == null ? null : branchRecipient.RecipientEmails);
                AddEmails(recipients, branchEmail);
                AddEmails(recipients, deptRecipient == null ? null : deptRecipient.RecipientEmails);
                AddEmails(recipients, rules == null ? null : rules.GroupEmail);
            }

            return recipients.ToList();
        }

        private static void AddEmails(HashSet<string> recipients, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var parts = raw.Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var value = (part ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                try
                {
                    var address = new MailAddress(value).Address;
                    if (!string.IsNullOrWhiteSpace(address))
                        recipients.Add(address);
                }
                catch
                {
                }
            }
        }

        private async Task SendEmailAsync(SmtpSenderConfig sender, IEnumerable<string> recipients, string subject, string body)
        {
            using (var message = new MailMessage())
            {
                // Mirrors the server resolver: explicit FromEmail must be valid,
                // otherwise fall back to the SMTP username (validated too).
                var fromEmail = GetFirstValidEmail(
                    (sender.FromEmail ?? string.Empty).Trim(),
                    (sender.SmtpUsername ?? string.Empty).Trim());
                if (string.IsNullOrWhiteSpace(fromEmail))
                    throw new InvalidOperationException("FromEmail is invalid. Correct the SMTP sender address or leave it blank to use the SMTP Username.");

                message.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(sender.FromName) ? "IT Call Monitoring" : sender.FromName);
                foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
                    message.To.Add(new MailAddress(recipient));

                if (message.To.Count == 0)
                    throw new InvalidOperationException("No valid recipients resolved.");

                message.Subject = subject ?? string.Empty;
                message.Body = body ?? string.Empty;
                message.IsBodyHtml = false;
                message.BodyEncoding = Encoding.UTF8;
                message.SubjectEncoding = Encoding.UTF8;

                using (var smtp = new SmtpClient(sender.SmtpServer, sender.SmtpPort > 0 ? sender.SmtpPort : 587))
                {
                    smtp.EnableSsl = sender.UseSsl;
                    if (!string.IsNullOrWhiteSpace(sender.SmtpUsername))
                    {
                        smtp.UseDefaultCredentials = false;
                        string password;
                        if (sender.SmtpPasswordEnc != null && sender.SmtpPasswordEnc.Length > 0)
                        {
                            password = Decrypt(sender.SmtpPasswordEnc)
                                ?? throw new InvalidOperationException("SMTP password decrypt failed. Re-save the SMTP settings to restore sending.");
                        }
                        else
                        {
                            password = string.Empty;
                        }
                        smtp.Credentials = new NetworkCredential(sender.SmtpUsername, password);
                    }
                    else
                    {
                        smtp.UseDefaultCredentials = true;
                    }

                    await smtp.SendMailAsync(message);
                }
            }
        }

        private static string GetFirstValidEmail(params string[] candidates)
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

        private static string Decrypt(byte[] encrypted)
        {
            if (encrypted == null || encrypted.Length == 0)
                return null;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, DpapiEntropy, DataProtectionScope.LocalMachine));
            }
            catch
            {
            }

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, DpapiEntropy, DataProtectionScope.CurrentUser));
            }
            catch
            {
                return null;
            }
        }

        private async Task<NotificationRules> GetNotificationRulesAsync(SqlConnection con)
        {
            if (!await TableExistsAsync(con, "dbo.CallNotificationRules"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP 1
    RulesId,
    NotifyOnNewTicket,
    NotifyOnStatusChange,
    NotifyOnEscalation,
    NotifyOnReminder,
    GroupEmail,
    EscalationEmail,
    ReminderDays
FROM dbo.CallNotificationRules
ORDER BY RulesId DESC;";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new NotificationRules
                    {
                        RulesId = GetInt(reader, "RulesId"),
                        NotifyOnNewTicket = GetBool(reader, "NotifyOnNewTicket"),
                        NotifyOnStatusChange = GetBool(reader, "NotifyOnStatusChange"),
                        NotifyOnEscalation = GetBool(reader, "NotifyOnEscalation"),
                        NotifyOnReminder = GetBool(reader, "NotifyOnReminder"),
                        GroupEmail = GetString(reader, "GroupEmail"),
                        EscalationEmail = GetString(reader, "EscalationEmail"),
                        ReminderDays = GetInt(reader, "ReminderDays")
                    };
                }
            }
        }

        private async Task<EmailTemplate> GetEmailTemplateByTypeAsync(SqlConnection con, string templateType)
        {
            if (!await TableExistsAsync(con, "dbo.CallEmailTemplate"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP 1
    TemplateId,
    TemplateType,
    Subject,
    Body,
    IsActive
FROM dbo.CallEmailTemplate
WHERE TemplateType = @TemplateType
ORDER BY TemplateId DESC;";
                cmd.Parameters.Add("@TemplateType", SqlDbType.NVarChar).Value = templateType;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new EmailTemplate
                    {
                        TemplateId = GetInt(reader, "TemplateId"),
                        TemplateType = GetString(reader, "TemplateType"),
                        Subject = GetString(reader, "Subject"),
                        Body = GetString(reader, "Body"),
                        IsActive = GetBool(reader, "IsActive")
                    };
                }
            }
        }

        private async Task<TicketNotificationData> GetTicketNotificationDataAsync(SqlConnection con, int ticketId)
        {
            if (!await TableExistsAsync(con, "dbo.CallTicket"))
                return null;

            var hasView = await ObjectExistsAsync(con, "dbo.vw_Call_TicketList", null);
            var sql = hasView
                ? @"
SELECT TOP 1
    TicketId,
    TicketCode,
    ComId,
    Company,
    DeptId,
    Department,
    BranchId,
    Branch,
    CallerName,
    Issue,
    ProvidedSolution,
    IssueType,
    Priority,
    Status,
    AssignedToEmpId,
    AssignedTo,
    CreatedAt,
    UpdatedAt,
    LastContactAt,
    LastReminderSentAt,
    SolvedAt
FROM dbo.vw_Call_TicketList
WHERE TicketId = @TicketId;"
                : @"
SELECT TOP 1
    TicketId,
    TicketCode,
    CAST(NULL AS int) AS ComId,
    CAST('' AS nvarchar(255)) AS Company,
    DeptId,
    CAST('' AS nvarchar(255)) AS Department,
    BranchId,
    CAST('' AS nvarchar(255)) AS Branch,
    CallerName,
    Issue,
    ProvidedSolution,
    IssueType,
    Priority,
    Status,
    AssignedToEmpId,
    CAST(NULL AS nvarchar(255)) AS AssignedTo,
    CreatedAt,
    UpdatedAt,
    LastContactAt,
    LastReminderSentAt,
    SolvedAt
FROM dbo.CallTicket
WHERE TicketId = @TicketId;";

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new TicketNotificationData
                    {
                        TicketId = GetInt(reader, "TicketId"),
                        TicketCode = GetString(reader, "TicketCode"),
                        ComId = GetNullableInt(reader, "ComId"),
                        Company = GetString(reader, "Company"),
                        DeptId = GetNullableInt(reader, "DeptId"),
                        Department = GetString(reader, "Department"),
                        BranchId = GetNullableInt(reader, "BranchId"),
                        Branch = GetString(reader, "Branch"),
                        CallerName = GetString(reader, "CallerName"),
                        Issue = GetString(reader, "Issue"),
                        ProvidedSolution = GetString(reader, "ProvidedSolution"),
                        IssueType = GetString(reader, "IssueType"),
                        Priority = GetString(reader, "Priority"),
                        Status = GetString(reader, "Status"),
                        AssignedToEmpId = GetNullableInt(reader, "AssignedToEmpId"),
                        AssignedTo = GetString(reader, "AssignedTo"),
                        CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                        UpdatedAt = GetDateTime(reader, "UpdatedAt") ?? DateTime.UtcNow,
                        LastContactAt = GetDateTime(reader, "LastContactAt"),
                        LastReminderSentAt = GetDateTime(reader, "LastReminderSentAt"),
                        SolvedAt = GetDateTime(reader, "SolvedAt")
                    };
                }
            }
        }

        private async Task<List<int>> GetTicketIdsNeedingReminderAsync(SqlConnection con, int reminderDays, int maxRows)
        {
            if (!await TableExistsAsync(con, "dbo.CallTicket"))
                return new List<int>();

            var hasLastContactAt = await ColumnExistsAsync(con, "dbo.CallTicket", "LastContactAt");
            var hasLastReminderSentAt = await ColumnExistsAsync(con, "dbo.CallTicket", "LastReminderSentAt");
            if (!hasLastContactAt || !hasLastReminderSentAt)
                return new List<int>();

            var tempGate = await TempGateAsync(con);

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP (@MaxRows) t.TicketId
FROM dbo.CallTicket t
WHERE t.Status NOT IN ('Solved', 'Closed')
  " + tempGate + @"
  AND t.LastContactAt IS NOT NULL
  AND DATEDIFF(DAY, t.LastContactAt, SYSUTCDATETIME()) >= @ReminderDays
  AND (
        t.LastReminderSentAt IS NULL
     OR DATEDIFF(DAY, t.LastReminderSentAt, SYSUTCDATETIME()) >= @ReminderDays
  )
ORDER BY t.UpdatedAt ASC, t.TicketId ASC;";
                cmd.Parameters.Add("@MaxRows", SqlDbType.Int).Value = Math.Max(1, Math.Min(maxRows, 500));
                cmd.Parameters.Add("@ReminderDays", SqlDbType.Int).Value = Math.Max(1, Math.Min(reminderDays, 60));
                return await ReadIntListAsync(cmd);
            }
        }

        private async Task MarkReminderSentAsync(SqlConnection con, int ticketId)
        {
            if (!await ColumnExistsAsync(con, "dbo.CallTicket", "LastReminderSentAt"))
                return;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "UPDATE dbo.CallTicket SET LastReminderSentAt = SYSUTCDATETIME() WHERE TicketId = @TicketId;";
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<List<int>> GetTicketIdsNeedingAutoEscalationAsync(SqlConnection con, int maxRows)
        {
            if (!await TableExistsAsync(con, "dbo.CallTicket") || !await TableExistsAsync(con, "dbo.CallEscalationSettings"))
                return new List<int>();

            var overrideExists = await TableExistsAsync(con, "dbo.CallTicketEscalationOverride");
            var hasLastContactAt = await ColumnExistsAsync(con, "dbo.CallTicket", "LastContactAt");
            var lastActivityExpr = hasLastContactAt ? "COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt)" : "COALESCE(t.UpdatedAt, t.CreatedAt)";
            var thresholdExpr = overrideExists ? "ISNULL(o.DaysToSupervisor, s.DaysToSupervisor)" : "s.DaysToSupervisor";
            var tempGate = await TempGateAsync(con);
            // Portal invariant (mirrors the server): unassigned Portal
            // submissions stay in triage until owned. Fail closed.
            var hasTicketSource = await ColumnExistsAsync(con, "dbo.CallTicket", "TicketSource");
            var hasAssignedToEmpId = await ColumnExistsAsync(con, "dbo.CallTicket", "AssignedToEmpId");
            if (!hasTicketSource || !hasAssignedToEmpId)
                return new List<int>();

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = string.Format(@"
SELECT TOP (@MaxRows) t.TicketId
FROM dbo.CallTicket t
CROSS JOIN (SELECT TOP 1 DaysToSupervisor FROM dbo.CallEscalationSettings ORDER BY SettingsId DESC) s
{0}
WHERE t.Status NOT IN ('Solved', 'Closed', 'Escalated', 'Forwarded to Repair')
  {3}
  AND NOT (UPPER(LTRIM(RTRIM(t.TicketSource))) = 'PORTAL' AND t.AssignedToEmpId IS NULL)
  AND {1} IS NOT NULL
  AND {1} > 0
  AND DATEDIFF(DAY, {2}, SYSUTCDATETIME()) >= {1}
ORDER BY {2} ASC, t.TicketId ASC;",
                    overrideExists ? "LEFT JOIN dbo.CallTicketEscalationOverride o ON o.TicketId = t.TicketId" : string.Empty,
                    thresholdExpr,
                    lastActivityExpr,
                    tempGate);
                cmd.Parameters.Add("@MaxRows", SqlDbType.Int).Value = Math.Max(1, Math.Min(maxRows, 500));
                return await ReadIntListAsync(cmd);
            }
        }

        private async Task<bool> EmployeeAssignmentEnabledAsync(SqlConnection con)
        {
            return await ColumnExistsAsync(con, "dbo.CallTicket", "AssignedToEmpId");
        }

        private async Task<int?> GetAutoEscalationAssigneeEmpIdAsync(SqlConnection con)
        {
            if (!await TableExistsAsync(con, "dbo.CallAutoEscalationAssigneeSettings"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP 1 AssigneeEmpId
FROM dbo.CallAutoEscalationAssigneeSettings
ORDER BY SettingsId DESC;";
                var value = await cmd.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
            }
        }

        private async Task SetTicketStatusAsync(SqlConnection con, int ticketId, string newStatus, int? changedByUserId, string note)
        {
            using (var cmd = new SqlCommand("dbo.sp_Call_SetTicketStatus", con))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                cmd.Parameters.Add("@NewStatus", SqlDbType.NVarChar, 100).Value = (object)newStatus ?? DBNull.Value;
                cmd.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = (object)changedByUserId ?? DBNull.Value;
                cmd.Parameters.Add("@Note", SqlDbType.NVarChar, -1).Value = (object)note ?? DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task AssignTicketEmployeeAsync(SqlConnection con, int ticketId, int assignedToEmpId, int? changedByUserId)
        {
            using (var cmd = new SqlCommand("dbo.sp_Call_AssignTicket", con))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                cmd.Parameters.Add("@AssignedToEmpId", SqlDbType.Int).Value = assignedToEmpId;
                cmd.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = (object)changedByUserId ?? DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<SmtpSenderConfig> GetSmtpSenderForTicketAsync(SqlConnection con, int? branchId, int? deptId)
        {
            if (branchId.HasValue && branchId.Value > 0 && await TableExistsAsync(con, "dbo.CallSmtpProfile") && await TableExistsAsync(con, "dbo.CallBranchSmtpProfileLink"))
            {
                var branchProfile = await QuerySingleSmtpAsync(con, @"
SELECT TOP 1
    p.SmtpServer,
    p.SmtpPort,
    p.UseSsl,
    p.SmtpUsername,
    p.SmtpPasswordEnc,
    COALESCE(NULLIF(p.FromName,''), NULLIF(p.ProfileName,'')) AS FromName,
    p.FromEmail
FROM dbo.CallBranchSmtpProfileLink l
INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
WHERE l.BranchId = @Id
  AND l.IsActive = 1
  AND p.IsActive = 1
ORDER BY p.UpdatedAt DESC, p.ProfileId DESC;", branchId.Value);
                if (branchProfile != null && !string.IsNullOrWhiteSpace(branchProfile.SmtpServer))
                    return branchProfile;
            }

            if (branchId.HasValue && branchId.Value > 0 && await TableExistsAsync(con, "dbo.CallBranchSmtpProfile"))
            {
                var branchLegacy = await QuerySingleSmtpAsync(con, @"
SELECT TOP 1
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail
FROM dbo.CallBranchSmtpProfile
WHERE BranchId = @Id
ORDER BY UpdatedAt DESC, ProfileId DESC;", branchId.Value);
                if (branchLegacy != null && !string.IsNullOrWhiteSpace(branchLegacy.SmtpServer))
                    return branchLegacy;
            }

            if (deptId.HasValue && deptId.Value > 0 && await TableExistsAsync(con, "dbo.CallSmtpProfile") && await TableExistsAsync(con, "dbo.CallDepartmentSmtpProfileLink"))
            {
                var deptProfile = await QuerySingleSmtpAsync(con, @"
SELECT TOP 1
    p.SmtpServer,
    p.SmtpPort,
    p.UseSsl,
    p.SmtpUsername,
    p.SmtpPasswordEnc,
    COALESCE(NULLIF(p.FromName,''), NULLIF(p.ProfileName,'')) AS FromName,
    p.FromEmail
FROM dbo.CallDepartmentSmtpProfileLink l
INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
WHERE l.DeptId = @Id
  AND l.IsActive = 1
  AND p.IsActive = 1
ORDER BY p.UpdatedAt DESC, p.ProfileId DESC;", deptId.Value);
                if (deptProfile != null && !string.IsNullOrWhiteSpace(deptProfile.SmtpServer))
                    return deptProfile;
            }

            if (deptId.HasValue && deptId.Value > 0 && await TableExistsAsync(con, "dbo.CallDepartmentSmtpProfile"))
            {
                var deptLegacy = await QuerySingleSmtpAsync(con, @"
SELECT TOP 1
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail
FROM dbo.CallDepartmentSmtpProfile
WHERE DeptId = @Id
ORDER BY UpdatedAt DESC, ProfileId DESC;", deptId.Value);
                if (deptLegacy != null && !string.IsNullOrWhiteSpace(deptLegacy.SmtpServer))
                    return deptLegacy;
            }

            if (await TableExistsAsync(con, "dbo.CallEmailSettings"))
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT TOP 1
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail
FROM dbo.CallEmailSettings
ORDER BY SettingsId DESC;";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return MapSmtpSender(reader);
                    }
                }
            }

            return null;
        }

        private async Task<SmtpSenderConfig> QuerySingleSmtpAsync(SqlConnection con, string sql, int id)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return MapSmtpSender(reader);
                }
            }
        }

        private static SmtpSenderConfig MapSmtpSender(IDataRecord reader)
        {
            return new SmtpSenderConfig
            {
                SmtpServer = GetString(reader, "SmtpServer"),
                SmtpPort = GetNullableInt(reader, "SmtpPort") ?? 587,
                UseSsl = GetBool(reader, "UseSsl"),
                SmtpUsername = GetString(reader, "SmtpUsername"),
                SmtpPasswordEnc = GetBytes(reader, "SmtpPasswordEnc"),
                FromName = GetString(reader, "FromName"),
                FromEmail = GetString(reader, "FromEmail")
            };
        }

        private async Task<BranchNotificationRecipient> GetBranchNotificationRecipientAsync(SqlConnection con, int branchId)
        {
            if (!await TableExistsAsync(con, "dbo.CallBranchNotificationRecipient"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP 1
    RecipientEmails,
    EscalationEmails,
    IsActive
FROM dbo.CallBranchNotificationRecipient
WHERE BranchId = @BranchId
  AND IsActive = 1
ORDER BY UpdatedAt DESC;";
                cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new BranchNotificationRecipient
                    {
                        RecipientEmails = GetString(reader, "RecipientEmails"),
                        EscalationEmails = GetString(reader, "EscalationEmails"),
                        IsActive = GetBool(reader, "IsActive")
                    };
                }
            }
        }

        private async Task<DepartmentNotificationRecipient> GetDepartmentNotificationRecipientAsync(SqlConnection con, int deptId)
        {
            if (!await TableExistsAsync(con, "dbo.CallDepartmentNotificationRecipient"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP 1
    RecipientEmails,
    EscalationEmails,
    IsActive
FROM dbo.CallDepartmentNotificationRecipient
WHERE DeptId = @DeptId
  AND IsActive = 1
ORDER BY UpdatedAt DESC;";
                cmd.Parameters.Add("@DeptId", SqlDbType.Int).Value = deptId;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new DepartmentNotificationRecipient
                    {
                        RecipientEmails = GetString(reader, "RecipientEmails"),
                        EscalationEmails = GetString(reader, "EscalationEmails"),
                        IsActive = GetBool(reader, "IsActive")
                    };
                }
            }
        }

        private async Task<string> GetBranchEmailAsync(SqlConnection con, int branchId)
        {
            if (!await TableExistsAsync(con, "dbo.Branch") || !await TableExistsAsync(con, "dbo.EmailAddress") || !await ColumnExistsAsync(con, "dbo.Branch", "EmailId"))
                return null;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP 1 e.EmailAddress
FROM dbo.Branch b
INNER JOIN dbo.EmailAddress e ON b.EmailId = e.EmailId
WHERE b.BranchId = @BranchId
  AND ISNULL(b.Active, 1) = 1
  AND ISNULL(e.IsActive, 1) = 1;";
                cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                var value = await cmd.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        private async Task LogEmailAsync(SqlConnection con, int? ticketId, string emailType, string recipient, string subject, string status, string errorMessage, int? createdByUserId)
        {
            if (!await TableExistsAsync(con, "dbo.CallEmailLog"))
                return;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
INSERT dbo.CallEmailLog
(
    TicketId,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    CreatedByUserId
)
VALUES
(
    @TicketId,
    @EmailType,
    @Recipient,
    @Subject,
    @Status,
    @ErrorMessage,
    @CreatedByUserId
);";
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = (object)ticketId ?? DBNull.Value;
                cmd.Parameters.Add("@EmailType", SqlDbType.NVarChar).Value = (emailType ?? string.Empty).Trim();
                cmd.Parameters.Add("@Recipient", SqlDbType.NVarChar).Value = (object)((recipient ?? string.Empty).Trim()) ?? DBNull.Value;
                cmd.Parameters.Add("@Subject", SqlDbType.NVarChar).Value = (object)subject ?? DBNull.Value;
                cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = (status ?? string.Empty).Trim();
                cmd.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar).Value = (object)errorMessage ?? DBNull.Value;
                cmd.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = (object)createdByUserId ?? DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static Dictionary<string, string> BuildBasePlaceholders(TicketNotificationData ticket)
        {
            var now = DateTime.UtcNow;
            var lastContact = ticket.LastContactAt.HasValue ? DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc) : (DateTime?)null;
            var idleDays = lastContact.HasValue ? Math.Max(0, (int)Math.Floor((now - lastContact.Value).TotalDays)) : 0;
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            placeholders["TicketId"] = ticket.TicketId.ToString();
            placeholders["TicketCode"] = ticket.TicketCode ?? string.Empty;
            placeholders["Company"] = ticket.Company ?? string.Empty;
            placeholders["Department"] = ticket.Department ?? string.Empty;
            placeholders["Branch"] = ticket.Branch ?? string.Empty;
            placeholders["CallerName"] = ticket.CallerName ?? string.Empty;
            placeholders["Issue"] = ticket.Issue ?? string.Empty;
            placeholders["ProvidedSolution"] = ticket.ProvidedSolution ?? string.Empty;
            placeholders["IssueType"] = ticket.IssueType ?? string.Empty;
            placeholders["Priority"] = ticket.Priority ?? string.Empty;
            placeholders["Status"] = ticket.Status ?? string.Empty;
            placeholders["AssignedTo"] = ticket.AssignedTo ?? string.Empty;
            placeholders["CreatedAt"] = ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            placeholders["UpdatedAt"] = ticket.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
            placeholders["LastContactAt"] = ticket.LastContactAt.HasValue ? ticket.LastContactAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;
            placeholders["LastReminderSentAt"] = ticket.LastReminderSentAt.HasValue ? ticket.LastReminderSentAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;
            placeholders["SolvedAt"] = ticket.SolvedAt.HasValue ? ticket.SolvedAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;
            placeholders["IdleDays"] = idleDays.ToString();
            return placeholders;
        }

        private static string Render(string template, Dictionary<string, string> placeholders)
        {
            if (string.IsNullOrEmpty(template) || placeholders == null || placeholders.Count == 0)
                return template ?? string.Empty;

            return PlaceholderRx.Replace(template, match =>
            {
                var key = match.Groups["k"].Success ? match.Groups["k"].Value : match.Groups["k2"].Value;
                string value;
                return placeholders.TryGetValue(key, out value) ? (value ?? string.Empty) : match.Value;
            });
        }

        private static string NormalizeStatus(string status)
        {
            return (status ?? string.Empty).Trim();
        }

        private static bool ShouldSkipAutoEscalation(string currentStatus)
        {
            var current = NormalizeStatus(currentStatus);
            return current.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase);
        }

        // Replacement-aware temporaries: temp WITH issued parts stays parked;
        // Service-Only temporaries remain watchable. Shared predicate text for
        // the candidate queries above; per-ticket version below.
        private static async Task<string> TempGateAsync(SqlConnection con)
        {
            if (!await TableExistsAsync(con, "dbo.CallTicketHistory"))
                return "AND t.Status <> 'Resolved (Temporary)'";

            return @"AND (
    t.Status <> 'Resolved (Temporary)'
    OR NOT EXISTS (
        SELECT 1 FROM dbo.CallTicketHistory h
        WHERE h.TicketId = t.TicketId
          AND h.FieldName = 'ReplacementNewItemId'
          AND TRY_CONVERT(int, h.NewValue) > 0
    )
  )";
        }

        private static async Task<bool> HasReplacementAllocationAsync(SqlConnection con, int ticketId)
        {
            if (!await TableExistsAsync(con, "dbo.CallTicketHistory"))
                return false;

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.CallTicketHistory h
    WHERE h.TicketId = @TicketId
      AND h.FieldName = 'ReplacementNewItemId'
      AND TRY_CONVERT(int, h.NewValue) > 0
) THEN 1 ELSE 0 END AS bit);";
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                var result = await cmd.ExecuteScalarAsync();
                return result is bool b && b;
            }
        }

        private static async Task<bool> ShouldSkipTicketAsync(SqlConnection con, int ticketId, string currentStatus)
        {
            var current = NormalizeStatus(currentStatus);
            if (current.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!current.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                return await HasReplacementAllocationAsync(con, ticketId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:u}] Replacement allocation check failed for ticket {ticketId}: {ex.Message}");
                return true;
            }
        }

        private static string BuildAutoEscalationNote(TicketNotificationData ticket)
        {
            var lastContactUtc = ticket.LastContactAt.HasValue
                ? DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc)
                : DateTime.SpecifyKind(ticket.UpdatedAt, DateTimeKind.Utc);

            var idleDays = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - lastContactUtc).TotalDays));
            return idleDays > 0
                ? "Auto-escalated due to no updates for " + idleDays + " day(s)."
                : "Auto-escalated due to inactivity.";
        }

        private static async Task<bool> TryAcquireAppLockAsync(SqlConnection connection, string resourceName, int timeoutMs)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
DECLARE @res int;
EXEC @res = sp_getapplock
    @Resource = @Resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = @TimeoutMs;
SELECT @res;";
                cmd.Parameters.AddWithValue("@Resource", resourceName);
                cmd.Parameters.AddWithValue("@TimeoutMs", timeoutMs);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) >= 0;
            }
        }

        private static async Task ReleaseAppLockAsync(SqlConnection connection, string resourceName)
        {
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';";
                    cmd.Parameters.AddWithValue("@Resource", resourceName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
            }
        }

        private static async Task<bool> TableExistsAsync(SqlConnection con, string tableName)
        {
            return await ObjectExistsAsync(con, tableName, "U");
        }

        private static async Task<bool> ColumnExistsAsync(SqlConnection con, string tableName, string columnName)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.objects o ON o.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
    WHERE (s.name + '.' + o.name) = @TableName
      AND c.name = @ColumnName
) THEN 1 ELSE 0 END;";
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", columnName);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
            }
        }

        private static async Task<bool> ObjectExistsAsync(SqlConnection con, string objectName, string type)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = type == null
                    ? "SELECT CASE WHEN OBJECT_ID(@ObjectName) IS NOT NULL THEN 1 ELSE 0 END;"
                    : "SELECT CASE WHEN OBJECT_ID(@ObjectName, @ObjectType) IS NOT NULL THEN 1 ELSE 0 END;";
                cmd.Parameters.AddWithValue("@ObjectName", objectName);
                if (type != null)
                    cmd.Parameters.AddWithValue("@ObjectType", type);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
            }
        }

        private static async Task<List<int>> ReadIntListAsync(SqlCommand cmd)
        {
            var values = new List<int>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        values.Add(Convert.ToInt32(reader.GetValue(0)));
                }
            }
            return values;
        }

        private static int GetInt(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? 0 : Convert.ToInt32(record.GetValue(ordinal));
        }

        private static int? GetNullableInt(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(record.GetValue(ordinal));
        }

        private static string GetString(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? null : Convert.ToString(record.GetValue(ordinal));
        }

        private static bool GetBool(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return !record.IsDBNull(ordinal) && Convert.ToBoolean(record.GetValue(ordinal));
        }

        private static DateTime? GetDateTime(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? (DateTime?)null : Convert.ToDateTime(record.GetValue(ordinal));
        }

        private static byte[] GetBytes(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? null : (byte[])record.GetValue(ordinal);
        }

        private sealed class BatchResult
        {
            public static readonly BatchResult Empty = new BatchResult();
            public int Candidates { get; set; }
            public int Successes { get; set; }
        }

        private sealed class NotificationRules
        {
            public int RulesId { get; set; }
            public bool NotifyOnNewTicket { get; set; }
            public bool NotifyOnStatusChange { get; set; }
            public bool NotifyOnEscalation { get; set; }
            public bool NotifyOnReminder { get; set; }
            public string GroupEmail { get; set; }
            public string EscalationEmail { get; set; }
            public int ReminderDays { get; set; }
        }

        private sealed class EmailTemplate
        {
            public int TemplateId { get; set; }
            public string TemplateType { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public bool IsActive { get; set; }
        }

        private sealed class TicketNotificationData
        {
            public int TicketId { get; set; }
            public string TicketCode { get; set; }
            public int? ComId { get; set; }
            public string Company { get; set; }
            public int? DeptId { get; set; }
            public string Department { get; set; }
            public int? BranchId { get; set; }
            public string Branch { get; set; }
            public string CallerName { get; set; }
            public string Issue { get; set; }
            public string ProvidedSolution { get; set; }
            public string IssueType { get; set; }
            public string Priority { get; set; }
            public string Status { get; set; }
            public int? AssignedToEmpId { get; set; }
            public string AssignedTo { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public DateTime? LastContactAt { get; set; }
            public DateTime? LastReminderSentAt { get; set; }
            public DateTime? SolvedAt { get; set; }
        }

        private sealed class BranchNotificationRecipient
        {
            public string RecipientEmails { get; set; }
            public string EscalationEmails { get; set; }
            public bool IsActive { get; set; }
        }

        private sealed class DepartmentNotificationRecipient
        {
            public string RecipientEmails { get; set; }
            public string EscalationEmails { get; set; }
            public bool IsActive { get; set; }
        }

        private sealed class SmtpSenderConfig
        {
            public string SmtpServer { get; set; }
            public int SmtpPort { get; set; }
            public bool UseSsl { get; set; }
            public string SmtpUsername { get; set; }
            public byte[] SmtpPasswordEnc { get; set; }
            public string FromName { get; set; }
            public string FromEmail { get; set; }
        }
    }

    internal sealed class ItcmRunSummary
    {
        public DateTime? LastRunStartUtc { get; set; }
        public DateTime? LastRunEndUtc { get; set; }
        public bool ConnectionVerified { get; set; }
        public string DatabaseName { get; set; }
        public int? TotalTicketCount { get; set; }
        public int? OpenTicketCount { get; set; }
        public bool? LastLockAcquired { get; set; }
        public int ReminderCandidates { get; set; }
        public int RemindersSent { get; set; }
        public int ReminderSkippedOrFailed { get; set; }
        public int EscalationCandidates { get; set; }
        public int EscalationsApplied { get; set; }
        public int EscalationSkippedOrFailed { get; set; }
        public string LastError { get; set; }
    }
}
