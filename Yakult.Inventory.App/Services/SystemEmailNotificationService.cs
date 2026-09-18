using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Service for sending system email notifications for Inventory, Cartridge, and Request Portal
    /// </summary>
    public sealed class SystemEmailNotificationService
    {
        private readonly EmailRepository _emailRepo;
        private readonly SystemSettingRepository _settingRepo;

        public SystemEmailNotificationService(EmailRepository emailRepo)
        {
            _emailRepo = emailRepo ?? throw new ArgumentNullException(nameof(emailRepo));
            _settingRepo = new SystemSettingRepository();
        }

        /// <summary>
        /// Sends an email using a template and placeholder values
        /// </summary>
        public async Task<EmailSendResult> SendEmailAsync(
            string templateKey,
            Dictionary<string, string> placeholders,
            int profileId,
            List<string> recipientEmails = null,
            int? empId = null,
            int? branchId = null,
            string entityType = null,
            int? entityId = null,
            int? sentByUserId = null,
            List<string> attachmentFilePaths = null,
            Dictionary<string, string> inlineImageFilePathsByContentId = null)
        {
            // Check global SMTP toggle first
            var smtpEnabled = await _settingRepo.GetSmtpEnabledAsync();
            if (!smtpEnabled)
                return await SkipAndLogAsync(templateKey, "SMTP sending is globally disabled.", profileId, sentByUserId, entityType, entityId);

            // Get template
            var template = await _emailRepo.GetEmailTemplateByKeyAsync(templateKey);
            if (template == null || !template.IsActive)
                return await SkipAndLogAsync(templateKey, $"Template '{templateKey}' is missing or inactive.", profileId, sentByUserId, entityType, entityId);

            // Get SMTP profile
            var profile = await _emailRepo.GetSmtpProfileByIdAsync(profileId);
            if (profile == null || !profile.IsActive)
                return await SkipAndLogAsync(templateKey, "SMTP profile is missing or inactive.", profileId, sentByUserId, entityType, entityId);

            // Resolve recipients
            var recipients = await ResolveRecipientsAsync(recipientEmails, empId, branchId);
            if (recipients.Count == 0)
                return await SkipAndLogAsync(templateKey, "No valid recipients found.", profileId, sentByUserId, entityType, entityId);

            // Render template
            var subject = Render(template.SubjectTemplate ?? string.Empty, placeholders);
            var body = Render(template.BodyTemplate ?? string.Empty, placeholders);

            if (string.IsNullOrWhiteSpace(subject))
                subject = "[YIMS Notification]";

            if (string.IsNullOrWhiteSpace(body))
                body = "Notification from Yakult Inventory Monitoring System";

            // Send email
            try
            {
                await SendEmailInternalAsync(profile, recipients, subject, body, template.IsHtml, attachmentFilePaths, inlineImageFilePathsByContentId);

                await _emailRepo.LogEmailAsync(new SystemEmailLogDto
                {
                    TemplateKey = templateKey,
                    Recipients = string.Join(", ", recipients),
                    Subject = subject,
                    Status = "Sent",
                    ProfileId = profileId,
                    EntityType = entityType,
                    EntityId = entityId,
                    SentDate = DateTime.Now,
                    SentByUserId = sentByUserId
                });

                Logger.LogInfo($"SystemEmailNotificationService: Sent template '{templateKey}' to '{string.Join(", ", recipients)}'. EntityType={entityType ?? ""}, EntityId={(entityId.HasValue ? entityId.Value.ToString() : "")}.");

                return EmailSendResult.Sent(recipients.Count);
            }
            catch (Exception ex)
            {
                await _emailRepo.LogEmailAsync(new SystemEmailLogDto
                {
                    TemplateKey = templateKey,
                    Recipients = string.Join(", ", recipients),
                    Subject = subject,
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    ProfileId = profileId,
                    EntityType = entityType,
                    EntityId = entityId,
                    SentDate = DateTime.Now,
                    SentByUserId = sentByUserId
                });

                Logger.LogError($"SystemEmailNotificationService: Failed template '{templateKey}' to '{string.Join(", ", recipients)}'. EntityType={entityType ?? ""}, EntityId={(entityId.HasValue ? entityId.Value.ToString() : "")}", ex);

                return EmailSendResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Sends a pre-rendered email (subject and body already generated).
        /// Used when the user edits the email preview before sending — the stored template is not modified.
        /// </summary>
        public async Task<EmailSendResult> SendRenderedEmailAsync(
            string subject,
            string body,
            int profileId,
            List<string> recipientEmails = null,
            int? empId = null,
            int? branchId = null,
            string entityType = null,
            int? entityId = null,
            int? sentByUserId = null)
        {
            var smtpEnabled = await _settingRepo.GetSmtpEnabledAsync();
            if (!smtpEnabled)
                return await SkipAndLogAsync("(preview-send)", "SMTP sending is globally disabled.", profileId, sentByUserId, entityType, entityId);

            var profile = await _emailRepo.GetSmtpProfileByIdAsync(profileId);
            if (profile == null || !profile.IsActive)
                return await SkipAndLogAsync("(preview-send)", "SMTP profile is missing or inactive.", profileId, sentByUserId, entityType, entityId);

            var recipients = await ResolveRecipientsAsync(recipientEmails, empId, branchId);
            if (recipients.Count == 0)
                return await SkipAndLogAsync("(preview-send)", "No valid recipients found.", profileId, sentByUserId, entityType, entityId);

            try
            {
                await SendEmailInternalAsync(profile, recipients, subject, body, isHtml: true,
                    attachmentFilePaths: null, inlineImageFilePathsByContentId: null);

                await _emailRepo.LogEmailAsync(new SystemEmailLogDto
                {
                    TemplateKey  = "(edited-preview)",
                    Recipients   = string.Join(", ", recipients),
                    Subject      = subject,
                    Status       = "Sent",
                    ProfileId    = profileId,
                    EntityType   = entityType,
                    EntityId     = entityId,
                    SentDate     = DateTime.Now,
                    SentByUserId = sentByUserId
                });

                Logger.LogInfo($"SystemEmailNotificationService.SendRenderedEmailAsync: sent to '{string.Join(", ", recipients)}'.");
                return EmailSendResult.Sent(recipients.Count);
            }
            catch (Exception ex)
            {
                await _emailRepo.LogEmailAsync(new SystemEmailLogDto
                {
                    TemplateKey  = "(edited-preview)",
                    Recipients   = string.Join(", ", recipients),
                    Subject      = subject,
                    Status       = "Failed",
                    ErrorMessage = ex.Message,
                    ProfileId    = profileId,
                    EntityType   = entityType,
                    EntityId     = entityId,
                    SentDate     = DateTime.Now,
                    SentByUserId = sentByUserId
                });

                Logger.LogError("SystemEmailNotificationService.SendRenderedEmailAsync failed", ex);
                return EmailSendResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Resolves recipient email addresses using fallback logic:
        /// 1. Use provided email addresses if any
        /// 2. Else use employee primary email
        /// 3. Else use branch email
        /// 4. Else use system default (if configured)
        /// </summary>
        private async Task<List<string>> ResolveRecipientsAsync(
            List<string> recipientEmails = null,
            int? empId = null,
            int? branchId = null)
        {
            var result = new List<string>();

            // Use provided emails first
            if (recipientEmails != null && recipientEmails.Any())
            {
                foreach (var email in recipientEmails)
                {
                    var validated = ParseRecipients(email);
                    result.AddRange(validated.Where(v => !result.Contains(v, StringComparer.OrdinalIgnoreCase)));
                }
            }

            // Resolve employee emails using parent/child hierarchy:
            //   child  = personal email (IsPrimary=1, highest priority)
            //   parent = department email (fallback; both sent when both exist)
            if (result.Count == 0 && empId.HasValue)
            {
                var empEmails = await _emailRepo.GetEmployeeEmailRecipientsAsync(empId.Value);
                foreach (var e in empEmails)
                    if (!result.Contains(e, StringComparer.OrdinalIgnoreCase))
                        result.Add(e);
            }

            // Final fallback to branch email (only if no employee-level address resolved)
            if (result.Count == 0 && branchId.HasValue)
            {
                var branchEmail = await _emailRepo.GetBranchEmailAsync(branchId.Value);
                if (!string.IsNullOrWhiteSpace(branchEmail))
                    result.Add(branchEmail);
            }

            // TODO: Add system default email fallback if configured

            return result;
        }

        /// <summary>
        /// Sends an email using the specified SMTP profile
        /// </summary>
        private static async Task SendEmailInternalAsync(
            SystemSmtpProfileDto profile,
            IReadOnlyList<string> recipients,
            string subject,
            string body,
            bool isHtml,
            IReadOnlyList<string> attachmentFilePaths,
            IReadOnlyDictionary<string, string> inlineImageFilePathsByContentId)
        {
            // Get from email address
            var fromEmail = profile.FromEmailAddress;
            if (string.IsNullOrWhiteSpace(fromEmail))
                fromEmail = profile.SmtpUsername;

            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("From email is not configured in the SMTP profile.");

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(profile.FromDisplayName) ? "YIMS" : profile.FromDisplayName);

                foreach (var to in recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
                    message.To.Add(to);

                if (message.To.Count == 0)
                    throw new InvalidOperationException("No valid recipients.");

                message.Subject = subject ?? string.Empty;
                message.Body = body ?? string.Empty;
                message.BodyEncoding = Encoding.UTF8;
                message.SubjectEncoding = Encoding.UTF8;
                message.IsBodyHtml = isHtml;

                if (isHtml && inlineImageFilePathsByContentId != null && inlineImageFilePathsByContentId.Count > 0)
                {
                    var htmlView = AlternateView.CreateAlternateViewFromString(message.Body, Encoding.UTF8, MediaTypeNames.Text.Html);

                    foreach (var kvp in inlineImageFilePathsByContentId)
                    {
                        var contentId = kvp.Key;
                        var filePath = kvp.Value;

                        if (string.IsNullOrWhiteSpace(contentId) || string.IsNullOrWhiteSpace(filePath))
                            continue;

                        try
                        {
                            if (!File.Exists(filePath))
                                continue;

                            var resource = new LinkedResource(filePath)
                            {
                                ContentId = contentId,
                                TransferEncoding = TransferEncoding.Base64
                            };
                            var ext = (Path.GetExtension(filePath) ?? string.Empty).ToLowerInvariant();
                            var mediaType = "application/octet-stream";
                            if (ext == ".png") mediaType = "image/png";
                            else if (ext == ".jpg" || ext == ".jpeg") mediaType = "image/jpeg";
                            else if (ext == ".gif") mediaType = "image/gif";
                            resource.ContentType = new ContentType(mediaType);

                            htmlView.LinkedResources.Add(resource);
                        }
                        catch
                        {
                        }
                    }

                    message.AlternateViews.Add(htmlView);
                }

                if (attachmentFilePaths != null && attachmentFilePaths.Count > 0)
                {
                    foreach (var filePath in attachmentFilePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        try
                        {
                            if (File.Exists(filePath))
                                message.Attachments.Add(new Attachment(filePath));
                        }
                        catch
                        {
                        }
                    }
                }

                using (var client = new SmtpClient(profile.SmtpServer, profile.SmtpPort <= 0 ? 587 : profile.SmtpPort))
                {
                    client.EnableSsl = profile.UseSsl;

                    client.UseDefaultCredentials = false;

                    if (!string.IsNullOrWhiteSpace(profile.SmtpUsername))
                    {
                        // A username with no stored password is a valid setup for an internal
                        // relay that accepts anonymous senders — send without credentials.
                        // Only treat a null result as an error when bytes are actually present
                        // (i.e. decryption genuinely failed), since UnprotectToString returns
                        // null for both "no password" and "could not decrypt".
                        bool hasStoredPassword = profile.SmtpPasswordEnc != null && profile.SmtpPasswordEnc.Length > 0;
                        var password = hasStoredPassword
                            ? SecretProtector.UnprotectToString(profile.SmtpPasswordEnc)
                            : null;

                        if (hasStoredPassword && password == null)
                            throw new InvalidOperationException(
                                "Failed to decrypt the SMTP profile password. Open the SMTP profile, re-enter the password, and save it again.");

                        if (!string.IsNullOrEmpty(password))
                            client.Credentials = new NetworkCredential(profile.SmtpUsername, password);
                    }

                    await client.SendMailAsync(message);
                }
            }
        }

        /// <summary>
        /// Logs a skipped email send
        /// </summary>
        private async Task<EmailSendResult> SkipAndLogAsync(
            string templateKey,
            string reason,
            int? profileId,
            int? sentByUserId,
            string entityType,
            int? entityId)
        {
            Logger.LogWarning($"SystemEmailNotificationService: Skipped template '{templateKey}'. Reason='{reason}'. ProfileId={(profileId.HasValue ? profileId.Value.ToString() : "")}, EntityType={entityType ?? ""}, EntityId={(entityId.HasValue ? entityId.Value.ToString() : "")}");

            try
            {
                await _emailRepo.LogEmailAsync(new SystemEmailLogDto
                {
                    TemplateKey = templateKey,
                    Status = "Skipped",
                    ErrorMessage = reason,
                    ProfileId = profileId,
                    EntityType = entityType,
                    EntityId = entityId,
                    SentDate = DateTime.Now,
                    SentByUserId = sentByUserId
                });
            }
            catch
            {
                // Ignore log failures
            }

            return EmailSendResult.Skipped(reason);
        }

        /// <summary>
        /// Parses a semicolon or comma-separated list of email addresses
        /// </summary>
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

        /// <summary>
        /// Placeholder regex for template rendering
        /// </summary>
        private static readonly Regex PlaceholderRegex = new Regex(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<key2>[A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

        /// <summary>
        /// Renders a template by replacing placeholders with values
        /// Supports both {PlaceholderName} and {{PlaceholderName}} syntax
        /// </summary>
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

        /// <summary>
        /// Result of an email send operation
        /// </summary>
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
