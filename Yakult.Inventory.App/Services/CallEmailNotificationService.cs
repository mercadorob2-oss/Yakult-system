using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    public sealed class CallEmailNotificationService
    {
        private const string GlobalBackgroundLockName = "Yakult.Inventory.App|CallMonitoring|BackgroundJobs";

        private readonly ICallMonitoringRepository _repo;

        public sealed class BackgroundJobStatus
        {
            public DateTime? LastRunStartUtc { get; set; }
            public DateTime? LastRunEndUtc { get; set; }
            public DateTime? NextRunUtc { get; set; }
            public bool? LastLockAcquired { get; set; }
            public int ReminderCandidates { get; set; }
            public int RemindersSent { get; set; }
            public int ReminderSkippedOrFailed { get; set; }
            public int EscalationCandidates { get; set; }
            public int EscalationsApplied { get; set; }
            public int EscalationSkippedOrFailed { get; set; }
            public string LastError { get; set; }
        }

        public sealed class RecipientResolutionTrace
        {
            public bool PreferEscalationRecipients { get; set; }

            public string BranchEmail { get; set; }
            public string BranchRecipients { get; set; }
            public string BranchEscalationRecipients { get; set; }

            public string DepartmentRecipients { get; set; }
            public string DepartmentEscalationRecipients { get; set; }

            public string RulesGroupEmail { get; set; }
            public string RulesEscalationEmail { get; set; }

            public string SelectedBaseRecipients { get; set; }
            public string SelectedGlobalRecipients { get; set; }

            public List<string> FinalRecipients { get; set; } = new List<string>();
        }

        public sealed class SmtpTestResult
        {
            public int TicketId { get; set; }
            public string EmailType { get; set; }
            public string TestToEmail { get; set; }

            public CallMonitoringRepository.SmtpSenderResolution SenderResolution { get; set; }
            public RecipientResolutionTrace RecipientResolution { get; set; }

            public bool Sent { get; set; }
            public string Error { get; set; }
        }

        private static readonly object JobStatusGate = new object();
        private static readonly BackgroundJobStatus JobStatusState = new BackgroundJobStatus();

        public static BackgroundJobStatus GetBackgroundJobStatusSnapshot()
        {
            lock (JobStatusGate)
            {
                return new BackgroundJobStatus
                {
                    LastRunStartUtc = JobStatusState.LastRunStartUtc,
                    LastRunEndUtc = JobStatusState.LastRunEndUtc,
                    NextRunUtc = JobStatusState.NextRunUtc,
                    LastLockAcquired = JobStatusState.LastLockAcquired,
                    ReminderCandidates = JobStatusState.ReminderCandidates,
                    RemindersSent = JobStatusState.RemindersSent,
                    ReminderSkippedOrFailed = JobStatusState.ReminderSkippedOrFailed,
                    EscalationCandidates = JobStatusState.EscalationCandidates,
                    EscalationsApplied = JobStatusState.EscalationsApplied,
                    EscalationSkippedOrFailed = JobStatusState.EscalationSkippedOrFailed,
                    LastError = JobStatusState.LastError
                };
            }
        }

        public static void SetNextBackgroundRunUtc(DateTime? nextRunUtc)
        {
            lock (JobStatusGate)
            {
                JobStatusState.NextRunUtc = nextRunUtc;
            }
        }

        public CallEmailNotificationService(ICallMonitoringRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        /// <summary>
        /// Runs reminder + auto-escalation processing under a global SQL application lock so only one client runs at a time.
        /// This prevents duplicate background sends when multiple Admin/Developer clients are open.
        /// </summary>
        public async Task ProcessRemindersAndAutoEscalationsWithGlobalLockAsync(int maxTickets = 20, int? triggeredByUserId = null)
        {
            lock (JobStatusGate)
            {
                JobStatusState.LastRunStartUtc = DateTime.UtcNow;
                JobStatusState.LastRunEndUtc = null;
                JobStatusState.LastError = null;
                JobStatusState.LastLockAcquired = null;
                JobStatusState.ReminderCandidates = 0;
                JobStatusState.RemindersSent = 0;
                JobStatusState.ReminderSkippedOrFailed = 0;
                JobStatusState.EscalationCandidates = 0;
                JobStatusState.EscalationsApplied = 0;
                JobStatusState.EscalationSkippedOrFailed = 0;
            }

            var cs = DatabaseConfig.GetConnectionStringOrNull();
            if (string.IsNullOrWhiteSpace(cs))
            {
                Logger.LogWarning("ITCM background jobs skipped: database connection is not configured.");
                lock (JobStatusGate)
                {
                    JobStatusState.LastError = "Database connection is not configured.";
                    JobStatusState.LastRunEndUtc = DateTime.UtcNow;
                    JobStatusState.LastLockAcquired = false;
                }
                return;
            }

            using (var lockConnection = new SqlConnection(cs))
            {
                await lockConnection.OpenAsync();

                var acquired = await TryAcquireAppLockAsync(lockConnection, GlobalBackgroundLockName, timeoutMs: 0);
                lock (JobStatusGate) { JobStatusState.LastLockAcquired = acquired; }
                if (!acquired)
                {
                    lock (JobStatusGate) { JobStatusState.LastRunEndUtc = DateTime.UtcNow; }
                    return;
                }

                try
                {
                    var reminders = await ProcessRemindersWithCountsAsync(maxTickets: maxTickets, triggeredByUserId: triggeredByUserId);
                    var escalations = await ProcessAutoEscalationsWithCountsAsync(maxTickets: maxTickets, triggeredByUserId: triggeredByUserId);

                    lock (JobStatusGate)
                    {
                        JobStatusState.ReminderCandidates = reminders.candidates;
                        JobStatusState.RemindersSent = reminders.sent;
                        JobStatusState.ReminderSkippedOrFailed = Math.Max(0, reminders.candidates - reminders.sent);

                        JobStatusState.EscalationCandidates = escalations.candidates;
                        JobStatusState.EscalationsApplied = escalations.applied;
                        JobStatusState.EscalationSkippedOrFailed = Math.Max(0, escalations.candidates - escalations.applied);
                    }
                }
                catch (Exception ex)
                {
                    lock (JobStatusGate) { JobStatusState.LastError = ex.Message; }
                    throw;
                }
                finally
                {
                    await ReleaseAppLockAsync(lockConnection, GlobalBackgroundLockName);
                    lock (JobStatusGate) { JobStatusState.LastRunEndUtc = DateTime.UtcNow; }
                }
            }
        }

        private async Task<(int candidates, int sent)> ProcessRemindersWithCountsAsync(int maxTickets, int? triggeredByUserId)
        {
            var rules = await _repo.GetNotificationRulesAsync();
            if (rules == null || !rules.NotifyOnReminder)
                return (0, 0);

            var reminderDays = rules.ReminderDays;
            if (reminderDays < 1) reminderDays = 1;
            if (reminderDays > 60) reminderDays = 60;

            var ticketIds = await _repo.GetTicketIdsNeedingReminderAsync(reminderDays, maxTickets);
            if (ticketIds == null || ticketIds.Count == 0)
                return (0, 0);

            var sentCount = 0;
            foreach (var ticketId in ticketIds)
            {
                sentCount += await TryProcessReminderAsync(ticketId, triggeredByUserId);
            }

            return (ticketIds.Count, sentCount);
        }

        private async Task<(int candidates, int applied)> ProcessAutoEscalationsWithCountsAsync(int maxTickets, int? triggeredByUserId)
        {
            if (maxTickets < 1) maxTickets = 1;
            if (maxTickets > 500) maxTickets = 500;

            var ticketIds = await _repo.GetTicketIdsNeedingAutoEscalationAsync(maxTickets);
            if (ticketIds == null || ticketIds.Count == 0)
                return (0, 0);

            var autoAssigneeEmpId = await TryGetAutoEscalationAssigneeEmpIdAsync();

            var escalatedCount = 0;
            foreach (var ticketId in ticketIds)
            {
                escalatedCount += await TryProcessAutoEscalationAsync(ticketId, autoAssigneeEmpId, triggeredByUserId);
            }

            return (ticketIds.Count, escalatedCount);
        }

        private async Task<int> TryProcessReminderAsync(int ticketId, int? triggeredByUserId)
        {
            try
            {
                var result = await NotifyReminderAsync(ticketId, triggeredByUserId);
                if (!result.SentSuccessfully)
                    return 0;

                await _repo.MarkReminderSentAsync(ticketId);
                return 1;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ITCM reminder processing failed (TicketId={ticketId}, Action=Reminder).", ex);
                return 0;
            }
        }

        private async Task<int?> TryGetAutoEscalationAssigneeEmpIdAsync()
        {
            try
            {
                if (await _repo.EmployeeAssignmentEnabledAsync())
                    return await _repo.GetAutoEscalationAssigneeEmpIdAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ITCM auto-escalation assignee lookup failed. {ex.Message}");
            }

            return null;
        }

        private async Task<int> TryProcessAutoEscalationAsync(int ticketId, int? autoAssigneeEmpId, int? triggeredByUserId)
        {
            try
            {
                var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
                if (ticket == null)
                    return 0;

                var oldStatus = ticket.Status ?? string.Empty;
                if (await ShouldSkipTicketAsync(ticketId, oldStatus))
                    return 0;

                var oldAssigneeEmpId = ticket.AssignedToEmpId;
                var note = BuildAutoEscalationNote(ticket);
                await _repo.SetTicketStatusAsync(ticketId, "Escalated", triggeredByUserId, note);
                await TryAssignEscalatedTicketAsync(ticketId, autoAssigneeEmpId, triggeredByUserId);

                var employeeRecipients = new List<int>();
                if (oldAssigneeEmpId.HasValue && oldAssigneeEmpId.Value > 0)
                    employeeRecipients.Add(oldAssigneeEmpId.Value);
                if (autoAssigneeEmpId.HasValue && autoAssigneeEmpId.Value > 0)
                    employeeRecipients.Add(autoAssigneeEmpId.Value);

                _ = NotifyStatusChangeAsync(ticketId, oldStatus, "Escalated", note, triggeredByUserId, employeeRecipients);
                return 1;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ITCM auto-escalation processing failed (TicketId={ticketId}, Action=Escalation).", ex);
                return 0;
            }
        }

        private async Task<bool> ShouldSkipTicketAsync(int ticketId, string currentStatus)
        {
            var current = (currentStatus ?? string.Empty).Trim();
            if (current.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!current.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                return false;

            // Replacement-backed temporaries stay parked; Service-Only
            // temporaries remain watchable. Fail parked when unknown.
            try
            {
                var preview = await _repo.GetTemporaryReplacementReturnPreviewAsync(ticketId);
                return preview.NewItemId > 0;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ITCM replacement allocation check failed (TicketId={ticketId}). {ex.Message}");
                return true;
            }
        }

        private static bool ShouldSkipAutoEscalation(string currentStatus)
        {
            var current = (currentStatus ?? string.Empty).Trim();
            return current.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildAutoEscalationNote(CallTicketNotificationData ticket)
        {
            var lastContactUtc = ticket.LastContactAt.HasValue
                ? DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc)
                : DateTime.SpecifyKind(ticket.UpdatedAt, DateTimeKind.Utc);

            var idleDays = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - lastContactUtc).TotalDays));
            return idleDays > 0
                ? $"Auto-escalated due to no updates for {idleDays} day(s)."
                : "Auto-escalated due to inactivity.";
        }

        private async Task TryAssignEscalatedTicketAsync(int ticketId, int? autoAssigneeEmpId, int? triggeredByUserId)
        {
            if (!autoAssigneeEmpId.HasValue || autoAssigneeEmpId.Value <= 0)
                return;

            try
            {
                await _repo.AssignTicketEmployeeAsync(ticketId, autoAssigneeEmpId.Value, triggeredByUserId);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ITCM auto-escalation assignment failed (TicketId={ticketId}, AssigneeEmpId={autoAssigneeEmpId.Value}).", ex);
            }
        }

        private static async Task<bool> TryAcquireAppLockAsync(SqlConnection connection, string resourceName, int timeoutMs)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("Resource name is required.", nameof(resourceName));

            if (timeoutMs < 0) timeoutMs = 0;

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

                var resultObj = await cmd.ExecuteScalarAsync();
                var result = resultObj is int i ? i : Convert.ToInt32(resultObj);

                // 0 = acquired, 1 = already held by this session, >0 granted.
                return result >= 0;
            }
        }

        private static async Task ReleaseAppLockAsync(SqlConnection connection, string resourceName)
        {
            if (connection == null)
                return;

            if (string.IsNullOrWhiteSpace(resourceName))
                return;

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';";
                    cmd.Parameters.AddWithValue("@Resource", resourceName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                // Best-effort; lock will be released when the connection is closed.
            }
        }

        public async Task<EmailSendResult> NotifyReminderAsync(int ticketId, int? triggeredByUserId)
        {
            var rules = await _repo.GetNotificationRulesAsync();
            if (rules == null || !rules.NotifyOnReminder)
                return await SkipAndLogAsync(
                    ticketId,
                    "Reminder",
                    BuildActionableMessage(
                        problem: "Reminder notifications are disabled.",
                        fix: "Enable 'Notify On Reminder' in IT Call Monitoring → Email Notification → Setup → Notification Rules."),
                    triggeredByUserId);

            var template = await _repo.GetEmailTemplateByTypeAsync("Reminder");
            if (template == null || !template.IsActive)
                return await SkipAndLogAsync(
                    ticketId,
                    "Reminder",
                    BuildActionableMessage(
                        problem: "Reminder template is missing or inactive.",
                        fix: "Activate the 'Reminder' template in IT Call Monitoring → Email Notification → Templates."),
                    triggeredByUserId);

            return await SendUsingTicketAsync(
                ticketId: ticketId,
                emailType: "Reminder",
                rules: rules,
                template: template,
                preferEscalationRecipients: false,
                placeholders: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                createdByUserId: triggeredByUserId);
        }

        public async Task<int> ProcessRemindersAsync(int maxTickets = 20, int? triggeredByUserId = null)
        {
            var r = await ProcessRemindersWithCountsAsync(maxTickets, triggeredByUserId);
            return r.sent;
        }

        public async Task<int> ProcessAutoEscalationsAsync(int maxTickets = 20, int? triggeredByUserId = null)
        {
            var r = await ProcessAutoEscalationsWithCountsAsync(maxTickets, triggeredByUserId);
            return r.applied;
        }

        public async Task<SmtpTestResult> RunSmtpTestAsync(int ticketId, string emailType, string testToEmail)
        {
            var result = new SmtpTestResult
            {
                TicketId = ticketId,
                EmailType = (emailType ?? string.Empty).Trim(),
                TestToEmail = (testToEmail ?? string.Empty).Trim()
            };

            if (ticketId <= 0)
            {
                result.Error = "TicketId is required.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(result.TestToEmail))
            {
                result.Error = "Test recipient email is required.";
                return result;
            }

            try { _ = new MailAddress(result.TestToEmail); }
            catch
            {
                result.Error = "Test recipient email is invalid.";
                return result;
            }

            var templateType = NormalizeEmailTypeForTemplate(emailType);
            if (string.IsNullOrWhiteSpace(templateType))
                templateType = "Reminder";

            var preferEscalationRecipients = string.Equals(templateType, "Escalation", StringComparison.OrdinalIgnoreCase);

            var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
            if (ticket == null)
            {
                result.Error = "Ticket not found.";
                return result;
            }

            var rules = await _repo.GetNotificationRulesAsync();
            result.RecipientResolution = await ResolveRecipientsWithTraceAsync(ticket.DeptId, ticket.BranchId, rules, preferEscalationRecipients);
            result.SenderResolution = await _repo.GetSmtpSenderResolutionForTicketAsync(ticket.BranchId, ticket.DeptId);

            if (result.SenderResolution?.Sender == null || string.IsNullOrWhiteSpace(result.SenderResolution.Sender.SmtpServer))
            {
                result.Error = "SMTP is not configured for this ticket (no sender resolved).";
                return result;
            }

            var subject = $"[ITCM SMTP Test] {templateType} • Ticket #{ticketId}";
            var body = new StringBuilder()
                .AppendLine("This is a one-time SMTP test email sent from IT Call Monitoring Diagnostics.")
                .AppendLine()
                .AppendLine($"TicketId: {ticketId}")
                .AppendLine($"EmailType: {templateType}")
                .AppendLine($"SenderSource: {result.SenderResolution.Source}")
                .AppendLine($"Profile: {(result.SenderResolution.ProfileId.HasValue ? result.SenderResolution.ProfileId.Value.ToString() : "")} {(result.SenderResolution.ProfileName ?? "")}".Trim())
                .AppendLine()
                .AppendLine("Resolved recipients (normal pipeline):")
                .AppendLine(string.Join(", ", (result.RecipientResolution?.FinalRecipients ?? new List<string>())))
                .AppendLine()
                .AppendLine("Note: This test was sent to the override address only to avoid spamming real recipients.")
                .ToString();

            try
            {
                await SendEmailAsync(result.SenderResolution.Sender, new[] { result.TestToEmail }, subject, body);
                result.Sent = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Sent = false;
                result.Error = ex.Message;
                return result;
            }
        }

        public async Task<EmailSendResult> NotifyNewTicketAsync(int ticketId, int? triggeredByUserId)
        {
            var rules = await _repo.GetNotificationRulesAsync();
            if (rules == null || !rules.NotifyOnNewTicket)
                return await SkipAndLogAsync(
                    ticketId,
                    "NewTicket",
                    BuildActionableMessage(
                        problem: "New ticket notifications are disabled.",
                        fix: "Enable 'Notify On New Ticket' in IT Call Monitoring → Email Notification → Setup → Notification Rules."),
                    triggeredByUserId);

            var template = await _repo.GetEmailTemplateByTypeAsync("NewTicket");
            if (template == null || !template.IsActive)
                return await SkipAndLogAsync(
                    ticketId,
                    "NewTicket",
                    BuildActionableMessage(
                        problem: "NewTicket template is missing or inactive.",
                        fix: "Activate the 'NewTicket' template in IT Call Monitoring → Email Notification → Templates."),
                    triggeredByUserId);

            return await SendUsingTicketAsync(
                ticketId: ticketId,
                emailType: "NewTicket",
                rules: rules,
                template: template,
                preferEscalationRecipients: false,
                placeholders: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                createdByUserId: triggeredByUserId);
        }

        public async Task<EmailSendResult> NotifyStatusChangeAsync(
            int ticketId,
            string oldStatus,
            string newStatus,
            string note,
            int? triggeredByUserId,
            IEnumerable<int> employeeRecipientEmpIds = null)
        {
            var rules = await _repo.GetNotificationRulesAsync();
            if (rules == null)
                return await SkipAndLogAsync(
                    ticketId,
                    "StatusUpdate",
                    BuildActionableMessage(
                        problem: "Notification rules are not configured.",
                        fix: "Configure Notification Rules in IT Call Monitoring → Email Notification → Setup."),
                    triggeredByUserId);

            var isEscalated = string.Equals(NormalizeStatus(newStatus), "Escalated", StringComparison.OrdinalIgnoreCase);
            var templateType = isEscalated ? "Escalation" : "StatusUpdate";

            if (isEscalated)
            {
                if (!rules.NotifyOnEscalation)
                    return await SkipAndLogAsync(
                        ticketId,
                        "Escalation",
                        BuildActionableMessage(
                            problem: "Escalation notifications are disabled.",
                            fix: "Enable 'Notify On Escalation' in IT Call Monitoring → Email Notification → Setup → Notification Rules."),
                        triggeredByUserId);
            }
            else
            {
                if (!rules.NotifyOnStatusChange)
                    return await SkipAndLogAsync(
                        ticketId,
                        "StatusUpdate",
                        BuildActionableMessage(
                            problem: "Status change notifications are disabled.",
                            fix: "Enable 'Notify On Status Change' in IT Call Monitoring → Email Notification → Setup → Notification Rules."),
                        triggeredByUserId);
            }

            var template = await _repo.GetEmailTemplateByTypeAsync(templateType);
            if (template == null || !template.IsActive)
                return await SkipAndLogAsync(
                    ticketId,
                    templateType,
                    BuildActionableMessage(
                        problem: $"{templateType} template is missing or inactive.",
                        fix: $"Activate the '{templateType}' template in IT Call Monitoring → Email Notification → Templates."),
                    triggeredByUserId);

            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OldStatus"] = oldStatus ?? string.Empty,
                ["NewStatus"] = newStatus ?? string.Empty,
                ["Note"] = note ?? string.Empty
            };

            return await SendUsingTicketAsync(
                ticketId: ticketId,
                emailType: templateType,
                rules: rules,

                template: template,
                preferEscalationRecipients: isEscalated,
                placeholders: placeholders,
                createdByUserId: triggeredByUserId,
                employeeRecipientEmpIds: employeeRecipientEmpIds);
        }

        public async Task<EmailSendResult> AssignTicketAndNotifyAsync(int ticketId, int? assignedToEmpId, int? triggeredByUserId)
        {
            // Capture the previous assignee before the stored procedure changes the ticket so
            // initial assignments and true reassignments select different templates.
            var previousTicket = await _repo.GetTicketNotificationDataAsync(ticketId);
            var previousEmpId = previousTicket?.AssignedToEmpId;
            var previousAssignee = previousTicket?.AssignedTo;

            await _repo.AssignTicketEmployeeAsync(ticketId, assignedToEmpId, triggeredByUserId);

            if (!assignedToEmpId.HasValue || assignedToEmpId.Value <= 0 || previousEmpId == assignedToEmpId)
                return EmailSendResult.Skipped("Assignment did not change to a new IT employee.");

            return await NotifyAssignmentAsync(
                ticketId,
                assignedToEmpId.Value,
                triggeredByUserId,
                previousEmpId,
                previousAssignee);
        }

        public async Task<EmailSendResult> NotifyAssignmentAsync(
            int ticketId,
            int assignedToEmpId,
            int? triggeredByUserId,
            int? previousAssignedToEmpId = null,
            string previousAssignee = null)
        {
            var isReassignment = previousAssignedToEmpId.HasValue
                                 && previousAssignedToEmpId.Value > 0
                                 && previousAssignedToEmpId.Value != assignedToEmpId;
            var emailType = isReassignment ? "Reassignment" : "Assignment";

            if (assignedToEmpId <= 0)
                return await SkipAndLogAsync(ticketId, emailType, "No IT employee was assigned.", triggeredByUserId);

            var template = await _repo.GetEmailTemplateByTypeAsync(emailType);
            if (template == null || !template.IsActive)
                return await SkipAndLogAsync(ticketId, emailType, $"{emailType} template is missing or inactive.", triggeredByUserId);

            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (isReassignment)
            {
                placeholders["PreviousAssignee"] = string.IsNullOrWhiteSpace(previousAssignee)
                    ? $"Employee #{previousAssignedToEmpId.Value}"
                    : previousAssignee.Trim();
            }

            return await SendUsingTicketAsync(
                ticketId: ticketId,
                emailType: emailType,
                rules: null,
                template: template,
                preferEscalationRecipients: false,
                placeholders: placeholders,
                createdByUserId: triggeredByUserId,
                employeeRecipientEmpIds: new[] { assignedToEmpId },
                includeConfiguredRecipients: false);
        }



        public async Task<EmailSendResult> ResendEmailAsync(int ticketId, string emailType, int? triggeredByUserId)
        {
            var templateType = NormalizeEmailTypeForTemplate(emailType);
            if (string.IsNullOrWhiteSpace(templateType))
                return await SkipAndLogAsync(
                    ticketId,
                    emailType ?? string.Empty,
                    BuildActionableMessage(
                        problem: "Unknown email type.",
                        fix: "Resend supports: NewTicket, Assignment, Reassignment, StatusUpdate, Escalation, and Reminder. Refresh the Email Log and try again."),
                    triggeredByUserId);

            var rules = await _repo.GetNotificationRulesAsync();
            if (rules == null)
                return await SkipAndLogAsync(
                    ticketId,
                    templateType,
                    BuildActionableMessage(
                        problem: "Notification rules are not configured.",
                        fix: "Configure Notification Rules in IT Call Monitoring → Email Notification → Setup."),
                    triggeredByUserId);

            var template = await _repo.GetEmailTemplateByTypeAsync(templateType);
            if (template == null || !template.IsActive)
                return await SkipAndLogAsync(
                    ticketId,
                    templateType,
                    BuildActionableMessage(
                        problem: $"{templateType} template is missing or inactive.",
                        fix: $"Activate the '{templateType}' template in IT Call Monitoring → Email Notification → Templates."),
                    triggeredByUserId);

            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var preferEscalationRecipients = templateType.Equals("Escalation", StringComparison.OrdinalIgnoreCase);

            if (templateType.Equals("StatusUpdate", StringComparison.OrdinalIgnoreCase) || templateType.Equals("Escalation", StringComparison.OrdinalIgnoreCase))
            {
                var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
                var currentStatus = (ticket?.Status ?? string.Empty).Trim();

                placeholders["OldStatus"] = string.Empty;
                placeholders["NewStatus"] = templateType.Equals("Escalation", StringComparison.OrdinalIgnoreCase) ? "Escalated" : currentStatus;
                placeholders["Note"] = "Resent from Email Log";
            }
            else if (templateType.Equals("Reassignment", StringComparison.OrdinalIgnoreCase))
            {
                // The email log does not persist the previous-assignee snapshot.
                // Render a clear fallback rather than leaving the template token visible.
                placeholders["PreviousAssignee"] = "Not available for resend";
            }

            return await SendUsingTicketAsync(
                ticketId: ticketId,
                emailType: templateType,
                rules: rules,
                template: template,
                preferEscalationRecipients: preferEscalationRecipients,
                placeholders: placeholders,
                createdByUserId: triggeredByUserId,
                successStatus: "Resent");
        }

        private async Task<EmailSendResult> SendUsingTicketAsync(
            int ticketId,
            string emailType,
            CallNotificationRulesItem rules,
            CallEmailTemplateItem template,
            bool preferEscalationRecipients,
            Dictionary<string, string> placeholders,
            int? createdByUserId,
            string successStatus = "Sent",
            IEnumerable<int> employeeRecipientEmpIds = null,
            bool includeConfiguredRecipients = true)
        {
            if (ticketId <= 0)
                return await SkipAndLogAsync(
                    ticketId,
                    emailType,
                    BuildActionableMessage(
                        problem: "Invalid ticket.",
                        fix: "Refresh the ticket list and try again."),
                    createdByUserId);

            var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
            if (ticket == null)
                return await SkipAndLogAsync(
                    ticketId,
                    emailType,
                    BuildActionableMessage(
                        problem: "Ticket not found.",
                        fix: "The ticket may have been deleted or the database is out of sync. Refresh and try again.",
                        info: $"TicketId={ticketId}"),
                    createdByUserId);

            var sender = await _repo.GetSmtpSenderForTicketAsync(ticket.BranchId, ticket.DeptId);
            if (sender == null || string.IsNullOrWhiteSpace(sender.SmtpServer))
                return await SkipAndLogAsync(
                    ticketId,
                    emailType,
                    BuildActionableMessage(
                        problem: "SMTP is not configured.",
                        fix: "Set SMTP settings in IT Call Monitoring → Email Notification → Setup, or assign a Department SMTP profile (Dept SMTP + Logs tab).",
                        info: $"BranchId={ticket.BranchId?.ToString() ?? ""}, DeptId={ticket.DeptId?.ToString() ?? ""}"),
                    createdByUserId);

            // Ticket lifecycle notifications are private to the ticket contact and current assignee.
            // Do not fall back to branch, department, or global notification recipients.
            var recipients = new List<string>();

            var employeeIds = (ticket.AssignedToEmpId.HasValue
                    ? new[] { ticket.AssignedToEmpId.Value }
                    : (employeeRecipientEmpIds ?? Enumerable.Empty<int>()))
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            recipients.AddRange(await ResolveEmployeeRecipientsAsync(employeeIds));
            if (!string.IsNullOrWhiteSpace(ticket.ContactEmail))
            {
                try
                {
                    recipients.Add(new MailAddress(ticket.ContactEmail.Trim()).Address);
                }
                catch
                {
                    Logger.LogWarning($"ITCM ticket contact email ignored because it is invalid (TicketId={ticketId}).");
                }
            }

            recipients = recipients
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();            if (recipients.Count == 0)
                return await SkipAndLogAsync(
                    ticketId,
                    emailType,
                    BuildActionableMessage(
                        problem: "No ticket contact or assigned IT employee email was resolved.",
                        fix: "Save a valid contact email on the ticket and assign an IT employee with an active primary email.",
                        info: $"TicketId={ticketId}, BranchId={ticket.BranchId?.ToString() ?? ""}, DeptId={ticket.DeptId?.ToString() ?? ""}"),
                    createdByUserId);

            var basePlaceholders = BuildBasePlaceholders(ticket);
            foreach (var kvp in placeholders ?? new Dictionary<string, string>())
                basePlaceholders[kvp.Key] = kvp.Value ?? string.Empty;

            var subject = Render(template.Subject ?? string.Empty, basePlaceholders);
            var body = Render(template.Body ?? string.Empty, basePlaceholders);

            if (string.IsNullOrWhiteSpace(subject))
                subject = $"[IT Call Monitoring] Ticket {ticket.TicketCode ?? ticket.TicketId.ToString()}";

            if (string.IsNullOrWhiteSpace(body))
                body = $"Ticket {ticket.TicketCode ?? ticket.TicketId.ToString()}";

            try
            {
                await SendEmailAsync(sender, recipients, subject, body);
                await _repo.LogEmailAsync(ticketId, emailType, string.Join(", ", recipients), subject, successStatus ?? "Sent", null, createdByUserId);
                return EmailSendResult.Sent(recipients.Count);
            }
            catch (Exception ex)
            {
                var failure = BuildSendFailureMessage(ex, sender, recipients, ticketId, emailType);
                Logger.LogError($"ITCM email send failed (TicketId={ticketId}, EmailType={emailType}).", ex);
                await _repo.LogEmailAsync(ticketId, emailType, string.Join(", ", recipients), subject, "Failed", failure, createdByUserId);
                return EmailSendResult.Failed(failure);
            }
        }

        public async Task<RecipientResolutionTrace> GetRecipientResolutionTraceForTicketAsync(int ticketId, string emailType)
        {
            var templateType = NormalizeEmailTypeForTemplate(emailType);
            var preferEscalationRecipients = string.Equals(templateType, "Escalation", StringComparison.OrdinalIgnoreCase);

            var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
            if (ticket == null)
                return new RecipientResolutionTrace
                {
                    PreferEscalationRecipients = preferEscalationRecipients,
                    FinalRecipients = new List<string>()
                };

            var rules = await _repo.GetNotificationRulesAsync();
            return await ResolveRecipientsWithTraceAsync(ticket.DeptId, ticket.BranchId, rules, preferEscalationRecipients);
        }

        private async Task<List<string>> ResolveRecipientsAsync(int? deptId, int? branchId, CallNotificationRulesItem rules, bool preferEscalationRecipients)
        {
            var trace = await ResolveRecipientsWithTraceAsync(deptId, branchId, rules, preferEscalationRecipients);
            return trace?.FinalRecipients ?? new List<string>();
        }

        private async Task<RecipientResolutionTrace> ResolveRecipientsWithTraceAsync(int? deptId, int? branchId, CallNotificationRulesItem rules, bool preferEscalationRecipients)
        {
            var trace = new RecipientResolutionTrace
            {
                PreferEscalationRecipients = preferEscalationRecipients,
                RulesGroupEmail = rules?.GroupEmail,
                RulesEscalationEmail = rules?.EscalationEmail
            };

            try
            {
                if (branchId.HasValue && branchId.Value > 0)
                {
                    if (await _repo.BranchNotificationRecipientsSchemaExistsAsync())
                    {
                        var r = await _repo.GetBranchNotificationRecipientAsync(branchId.Value);
                        if (r != null && r.IsActive)
                        {
                            trace.BranchRecipients = r.RecipientEmails;
                            trace.BranchEscalationRecipients = r.EscalationEmails;
                        }
                    }

                    // Fallback: if no per-branch override is configured, use the branch's own email (Branch.EmailId).
                    if (string.IsNullOrWhiteSpace(trace.BranchRecipients) && string.IsNullOrWhiteSpace(trace.BranchEscalationRecipients))
                        trace.BranchEmail = await _repo.GetBranchEmailAsync(branchId.Value);
                }

                if (deptId.HasValue && deptId.Value > 0 && await _repo.DepartmentNotificationRecipientsSchemaExistsAsync())
                {
                    var r = await _repo.GetDepartmentNotificationRecipientAsync(deptId.Value);
                    if (r != null && r.IsActive)
                    {
                        trace.DepartmentRecipients = r.RecipientEmails;
                        trace.DepartmentEscalationRecipients = r.EscalationEmails;
                    }
                }
            }
            catch
            {
                // Best-effort; fallback to global rules.
            }

            if (preferEscalationRecipients)
            {
                // Escalation: send to branch escalation/notify (if configured) else branch email,
                // else department escalation/notify; also include global escalation email (IT manager).
                trace.SelectedBaseRecipients = FirstNonEmpty(
                    trace.BranchEscalationRecipients,
                    trace.BranchRecipients,
                    trace.BranchEmail,

                    trace.DepartmentEscalationRecipients,
                    trace.DepartmentRecipients);

                trace.SelectedGlobalRecipients = FirstNonEmpty(trace.RulesEscalationEmail, trace.RulesGroupEmail);

                var combined = new List<string>();
                combined.AddRange(ParseRecipients(trace.SelectedBaseRecipients));
                combined.AddRange(ParseRecipients(trace.SelectedGlobalRecipients));
                trace.FinalRecipients = combined
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                trace.SelectedBaseRecipients = FirstNonEmpty(trace.BranchRecipients, trace.BranchEmail, trace.DepartmentRecipients, trace.RulesGroupEmail);
                trace.SelectedGlobalRecipients = null;
                trace.FinalRecipients = ParseRecipients(trace.SelectedBaseRecipients);
            }

            return trace;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values ?? Array.Empty<string>())
            {
                var t = (v ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    return t;
            }

            return null;
        }

        private async Task<List<string>> ResolveEmployeeRecipientsAsync(IEnumerable<int> employeeIds)
        {
            var recipients = new List<string>();
            foreach (var empId in (employeeIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct())
            {
                try
                {
                    var email = await _repo.GetEmployeeEmailBindingAsync(empId);
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    try
                    {
                        var address = new MailAddress(email.Trim()).Address;
                        if (!string.IsNullOrWhiteSpace(address) && !recipients.Contains(address, StringComparer.OrdinalIgnoreCase))
                            recipients.Add(address);
                    }
                    catch
                    {
                        Logger.LogWarning($"ITCM employee email ignored because it is invalid (EmpId={empId}).");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"ITCM employee email lookup failed (EmpId={empId}). {ex.Message}");
                }
            }

            return recipients;
        }

        private static async Task SendEmailAsync(SmtpSenderConfig sender, IReadOnlyList<string> recipients, string subject, string body)
        {
            var configuredFromEmail = (sender?.FromEmail ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configuredFromEmail) && !EmailAddressValidator.IsValid(configuredFromEmail))
                throw new InvalidOperationException("FromEmail is invalid. Correct the SMTP sender address (for example, robpogi54@gmail.com) or leave it blank to use the SMTP Username.");

            var fromEmail = GetFirstValidEmail(configuredFromEmail, sender?.SmtpUsername);
            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("FromEmail is invalid. Set a valid FromEmail (or leave it blank so it uses the SMTP Username).");

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(sender.FromName) ? "IT Call Monitoring" : sender.FromName);
                foreach (var to in recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
                    message.To.Add(to);

                if (message.To.Count == 0)
                    throw new InvalidOperationException("No valid recipients.");

                message.Subject = subject ?? string.Empty;
                message.Body = body ?? string.Empty;
                message.BodyEncoding = Encoding.UTF8;
                message.SubjectEncoding = Encoding.UTF8;
                message.IsBodyHtml = false;

                using (var client = new SmtpClient(sender.SmtpServer, sender.SmtpPort <= 0 ? 587 : sender.SmtpPort))
                {
                    client.EnableSsl = sender.UseSsl;

                    if (!string.IsNullOrWhiteSpace(sender.SmtpUsername))
                    {
                        var password = SecretProtector.UnprotectToString(sender.SmtpPasswordEnc) ?? string.Empty;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(sender.SmtpUsername, password);
                    }
                    else
                    {
                        client.UseDefaultCredentials = true;
                    }

                    await client.SendMailAsync(message);
                }

            }
        }

        private static string GetFirstValidEmail(params string[] candidates)
        {
            foreach (var candidate in candidates ?? Array.Empty<string>())
            {
                var value = (candidate ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                try
                {
                    _ = new MailAddress(value);
                    return value;
                }
                catch
                {
                    // ignore invalid
                }
            }

            return null;
        }

        private async Task<EmailSendResult> SkipAndLogAsync(int ticketId, string emailType, string reason, int? createdByUserId)
        {
            var safeReason = TrimToMax((reason ?? string.Empty).Trim(), 1900);

            try
            {
                if (ticketId > 0)
                    await _repo.LogEmailAsync(ticketId, emailType, string.Empty, null, "Skipped", safeReason, createdByUserId);
            }
            catch
            {
                // Ignore log failures
            }

            Logger.LogWarning($"ITCM email skipped (TicketId={ticketId}, EmailType={emailType}): {safeReason}");
            return EmailSendResult.Skipped(safeReason);
        }

        private static string BuildActionableMessage(string problem, string fix, string info = null)
        {
            var sb = new StringBuilder();

            problem = (problem ?? string.Empty).Trim();
            fix = (fix ?? string.Empty).Trim();
            info = (info ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(problem))
                sb.Append("Problem: ").Append(problem);

            if (!string.IsNullOrWhiteSpace(fix))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("Fix: ").Append(fix);
            }

            if (!string.IsNullOrWhiteSpace(info))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("Info: ").Append(info);
            }

            return TrimToMax(sb.ToString().Trim(), 1900);
        }

        private static string BuildSendFailureMessage(Exception ex, SmtpSenderConfig sender, IReadOnlyList<string> recipients, int ticketId, string emailType)
        {
            var baseEx = ex?.GetBaseException() ?? ex;
            var msg = (baseEx?.Message ?? "Unknown send error.").Trim();

            string fix;
            if (baseEx is SmtpException smtpEx)
            {
                if (msg.IndexOf("MustIssueStartTlsFirst", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("STARTTLS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fix = "Enable SSL/TLS (Use SSL = true) and use the correct SMTP port (commonly 587). If your server expects authentication, also set SMTP Username/Password.";
                }
                else if (msg.IndexOf("Authentication Required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         msg.IndexOf("not authenticated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         msg.IndexOf("5.7.0", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fix = "Set SMTP Username/Password (or configure your SMTP relay to allow this app to send without authentication). Also confirm SSL/TLS and port settings.";
                }
                else
                {
                    fix = "Check SMTP server/port/SSL, credentials, and network/firewall access. If authentication fails, verify the SMTP username/password.";
                }
                msg = $"SMTP error ({smtpEx.StatusCode}): {msg}";
            }
            else if (msg.IndexOf("FromEmail", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fix = "Set a valid FromEmail in Email Notification → Setup (or leave it blank to use SMTP Username as FromEmail).";
            }
            else if (msg.IndexOf("No valid recipients", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fix = "Configure recipients in Notification Rules (GroupEmail/EscalationEmail) and/or per-Branch/Department recipients.";
            }
            else if (msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fix = "Check network connectivity to the SMTP server and confirm the SMTP port is reachable.";
            }
            else
            {
                fix = "Review Email Notification settings (SMTP + Templates + Notification Rules) and retry. See Diagnostics for last error.";
            }

            var auth = string.IsNullOrWhiteSpace(sender?.SmtpUsername) ? "DefaultCredentials" : "Username";
            var recSummary = recipients == null ? string.Empty : string.Join(", ", recipients.Take(5));
            if (recipients != null && recipients.Count > 5) recSummary += ", …";

            var port = sender?.SmtpPort <= 0 ? 587 : sender.SmtpPort;
            var info = $"TicketId={ticketId}, EmailType={emailType}, SMTP={sender?.SmtpServer}:{port} SSL={(sender?.UseSsl ?? false)}, Auth={auth}, Recipients=[{recSummary}]";
            return BuildActionableMessage(problem: msg, fix: fix, info: info);
        }

        private static string TrimToMax(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (max < 10) max = 10;
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        internal static Dictionary<string, string> BuildBasePlaceholders(CallTicketNotificationData ticket)
        {
            var nowUtc = DateTime.UtcNow;
            var idleDays = 0;
            if (ticket.LastContactAt.HasValue)
            {
                var lastContactUtc = DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc);
                idleDays = Math.Max(0, (int)Math.Floor((nowUtc - lastContactUtc).TotalDays));
            }

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
                ["PreviousAssignee"] = string.Empty,
                ["CreatedAt"] = ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                ["UpdatedAt"] = ticket.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
                ["LastContactAt"] = ticket.LastContactAt.HasValue ? ticket.LastContactAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty,
                ["LastReminderSentAt"] = ticket.LastReminderSentAt.HasValue ? ticket.LastReminderSentAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty,
                ["IdleDays"] = idleDays.ToString(),
                ["SolvedAt"] = ticket.SolvedAt.HasValue ? ticket.SolvedAt.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty,
                ["ProvidedSolution"] = ticket.ProvidedSolution ?? string.Empty
            };
        }

        private static string NormalizeStatus(string status)
        {
            return (status ?? string.Empty).Trim();
        }

        private static string NormalizeEmailTypeForTemplate(string emailType)
        {
            var t = (emailType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
                return null;

            if (t.Equals("New Ticket", StringComparison.OrdinalIgnoreCase)) return "NewTicket";
            if (t.Equals("Status Update", StringComparison.OrdinalIgnoreCase)) return "StatusUpdate";

            if (t.Equals("NewTicket", StringComparison.OrdinalIgnoreCase)) return "NewTicket";
            if (t.Equals("StatusUpdate", StringComparison.OrdinalIgnoreCase)) return "StatusUpdate";
            if (t.Equals("Escalation", StringComparison.OrdinalIgnoreCase)) return "Escalation";
            if (t.Equals("Reminder", StringComparison.OrdinalIgnoreCase)) return "Reminder";

            return t;
        }

        private static List<string> ParseRecipients(string recipientText)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(recipientText))
                return result;

            var parts = recipientText
                .Split(new[] { ';', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            foreach (var part in parts)
            {
                try
                {
                    var addr = new MailAddress(part);
                    var normalized = addr.Address?.Trim();
                    if (!string.IsNullOrWhiteSpace(normalized) &&
                        !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(normalized);
                    }
                }
                catch
                {
                    // ignore invalid addresses
                }
            }

            return result;
        }

        private static readonly Regex PlaceholderRegex = new Regex(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<key2>[A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

        public static string Render(string template, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            if (values == null || values.Count == 0)
                return template;

            return PlaceholderRegex.Replace(
                template,
                match =>
                {
                    var key = match.Groups["key"].Success ? match.Groups["key"].Value : match.Groups["key2"].Value;
                    if (string.IsNullOrWhiteSpace(key))
                        return match.Value;

                    return values.TryGetValue(key, out var val) ? (val ?? string.Empty) : match.Value;
                });
        }

        public sealed class EmailSendResult
        {
            private EmailSendResult(bool sent, bool skipped, int recipients, string message)
            {
                SentSuccessfully = sent;
                WasSkipped = skipped;
                RecipientCount = recipients;
                Message = message;
            }

            public bool SentSuccessfully { get; }
            public bool WasSkipped { get; }
            public int RecipientCount { get; }
            public string Message { get; }

            public static EmailSendResult Sent(int recipients) => new EmailSendResult(true, false, recipients, null);
            public static EmailSendResult Skipped(string reason) => new EmailSendResult(false, true, 0, reason);
            public static EmailSendResult Failed(string error) => new EmailSendResult(false, false, 0, error);
        }
    }

}
