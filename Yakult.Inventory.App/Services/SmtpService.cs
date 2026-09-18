using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Lightweight SMTP service that loads active profile, maps to SmtpSenderConfig, sends via SmtpClient
    /// </summary>
    public class SmtpService : ISmtpService
    {
        private readonly EmailRepository _emailRepository;

        public SmtpService(EmailRepository emailRepository)
        {
            _emailRepository = emailRepository ?? throw new ArgumentNullException(nameof(emailRepository));
        }

        /// <summary>
        /// Sends an email using the active SMTP profile
        /// </summary>
        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            try
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.SendEmailAsync START. ThreadId={Thread.CurrentThread.ManagedThreadId} To='{to ?? ""}' SubjectPresent={!string.IsNullOrWhiteSpace(subject)} IsHtml={isHtml}");

                // Validate inputs
                if (string.IsNullOrWhiteSpace(to))
                {
                    Logger.LogWarning("SmtpService.SendEmailAsync: Recipient email address is required");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(subject))
                {
                    Logger.LogWarning("SmtpService.SendEmailAsync: Email subject is required");
                    return false;
                }

                // Load active SMTP profile
                Debug.WriteLine($"[SMTP DEBUG] SmtpService: loading active SMTP profile. ThreadId={Thread.CurrentThread.ManagedThreadId}");
                var profile = await LoadActiveSmtpProfileAsync();
                if (profile == null)
                {
                    Logger.LogWarning("SmtpService.SendEmailAsync: No active SMTP profile found");
                    return false;
                }

                Debug.WriteLine($"[SMTP DEBUG] SmtpService: active SMTP profile loaded. ThreadId={Thread.CurrentThread.ManagedThreadId} ProfileId={profile.ProfileId} ProfileName='{(profile.ProfileName ?? "")}' Host='{(profile.SmtpServer ?? "")}' Port={profile.SmtpPort} UseSsl={profile.UseSsl} UsernamePresent={!string.IsNullOrWhiteSpace(profile.SmtpUsername)}");

                // Map to SmtpSenderConfig
                var config = MapToSmtpSenderConfig(profile);

                // Send email
                await SendEmailInternalAsync(config, to, subject, body, isHtml);

                Logger.LogInfo($"Email sent successfully to {to} with subject: {subject}");
                return true;
            }
            catch (CryptographicException ex)
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.SendEmailAsync CryptographicException (DPAPI/ProtectedData failure). ThreadId={Thread.CurrentThread.ManagedThreadId} To='{to ?? ""}'");
                Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                Logger.LogError($"SmtpService.SendEmailAsync CryptographicException for recipient '{to}'", ex);
                return false;
            }
            catch (SmtpException ex)
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.SendEmailAsync SmtpException. ThreadId={Thread.CurrentThread.ManagedThreadId} To='{to ?? ""}' StatusCode={ex.StatusCode}");
                Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                Logger.LogError($"SmtpService.SendEmailAsync SmtpException for recipient '{to}'", ex);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.SendEmailAsync Exception. ThreadId={Thread.CurrentThread.ManagedThreadId} To='{to ?? ""}'");
                Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                Logger.LogError($"SmtpService.SendEmailAsync failed for recipient '{to}'", ex);
                return false;
            }
        }

        /// <summary>
        /// Loads the first active SMTP profile from the database
        /// </summary>
        private async Task<SystemSmtpProfileDto> LoadActiveSmtpProfileAsync()
        {
            try
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.LoadActiveSmtpProfileAsync START. ThreadId={Thread.CurrentThread.ManagedThreadId}");
                var profiles = await _emailRepository.GetSmtpProfilesAsync(activeOnly: true);
                return profiles?.FirstOrDefault();
            }
            catch (CryptographicException ex)
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.LoadActiveSmtpProfileAsync CryptographicException. ThreadId={Thread.CurrentThread.ManagedThreadId}");
                Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                Logger.LogError("SmtpService.LoadActiveSmtpProfileAsync CryptographicException", ex);
                return null;
            }
            catch (SmtpException ex)
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.LoadActiveSmtpProfileAsync SmtpException. ThreadId={Thread.CurrentThread.ManagedThreadId} StatusCode={ex.StatusCode}");
                Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                Logger.LogError("SmtpService.LoadActiveSmtpProfileAsync SmtpException", ex);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SMTP DEBUG] SmtpService.LoadActiveSmtpProfileAsync Exception. ThreadId={Thread.CurrentThread.ManagedThreadId}");
                Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                Logger.LogError("SmtpService.LoadActiveSmtpProfileAsync failed", ex);
                return null;
            }
        }

        /// <summary>
        /// Maps SystemSmtpProfileDto to SmtpSenderConfig
        /// </summary>
        private SmtpSenderConfig MapToSmtpSenderConfig(SystemSmtpProfileDto profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            return new SmtpSenderConfig
            {
                SmtpServer = profile.SmtpServer,
                SmtpPort = profile.SmtpPort <= 0 ? 587 : profile.SmtpPort,
                UseSsl = profile.UseSsl,
                SmtpUsername = profile.SmtpUsername,
                SmtpPasswordEnc = profile.SmtpPasswordEnc,
                FromEmail = profile.FromEmailAddress ?? profile.SmtpUsername,
                FromName = profile.FromDisplayName ?? "YIMS"
            };
        }

        /// <summary>
        /// Sends email using System.Net.Mail.SmtpClient
        /// </summary>
        private async Task SendEmailInternalAsync(SmtpSenderConfig config, string to, string subject, string body, bool isHtml)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.SmtpServer))
                throw new InvalidOperationException("SMTP server is not configured");

            if (string.IsNullOrWhiteSpace(config.FromEmail))
                throw new InvalidOperationException("From email address is not configured");

            // Create mail message
            using (var message = new MailMessage())
            {
                message.From = new MailAddress(config.FromEmail, config.FromName ?? config.FromEmail);
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = isHtml;
                message.BodyEncoding = Encoding.UTF8;
                message.SubjectEncoding = Encoding.UTF8;

                // Create and configure SMTP client
                using (var smtpClient = new SmtpClient())
                {
                    smtpClient.Host = config.SmtpServer;
                    smtpClient.Port = config.SmtpPort;
                    smtpClient.EnableSsl = config.UseSsl;
                    smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtpClient.UseDefaultCredentials = false;

                    Debug.WriteLine($"[SMTP DEBUG] SmtpService SMTP client configured. ThreadId={Thread.CurrentThread.ManagedThreadId} Host='{(config.SmtpServer ?? "")}' Port={config.SmtpPort} UseSsl={config.UseSsl} UsernamePresent={!string.IsNullOrWhiteSpace(config.SmtpUsername)}");

                    // Set credentials if username and password are provided
                    if (!string.IsNullOrWhiteSpace(config.SmtpUsername) && config.SmtpPasswordEnc != null)
                    {
                        Debug.WriteLine($"[SMTP DEBUG] SmtpService attempting SMTP password decryption. ThreadId={Thread.CurrentThread.ManagedThreadId} EncryptedBytesLength={(config.SmtpPasswordEnc != null ? config.SmtpPasswordEnc.Length : 0)}");
                        var password = SecretProtector.UnprotectToString(config.SmtpPasswordEnc);
                        Debug.WriteLine($"[SMTP DEBUG] SmtpService SMTP password decryption completed. ThreadId={Thread.CurrentThread.ManagedThreadId} DecryptionResult={(string.IsNullOrWhiteSpace(password) ? "EMPTY_OR_FAILED" : "OK")}");
                        if (!string.IsNullOrWhiteSpace(password))
                        {
                            smtpClient.Credentials = new NetworkCredential(config.SmtpUsername, password);
                        }
                        else
                        {
                            Logger.LogWarning("SmtpService: Failed to decrypt SMTP password");
                        }
                    }

                    // Send email
                    try
                    {
                        await smtpClient.SendMailAsync(message);
                        Debug.WriteLine($"[SMTP DEBUG] SmtpService SMTP SendMailAsync SUCCESS. ThreadId={Thread.CurrentThread.ManagedThreadId} Host='{(config.SmtpServer ?? "")}' Port={config.SmtpPort} UseSsl={config.UseSsl}");
                    }
                    catch (SmtpException ex)
                    {
                        Debug.WriteLine($"[SMTP DEBUG] SmtpService SMTP SendMailAsync SmtpException. ThreadId={Thread.CurrentThread.ManagedThreadId} Host='{(config.SmtpServer ?? "")}' Port={config.SmtpPort} UseSsl={config.UseSsl} StatusCode={ex.StatusCode}");
                        Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                        Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                        Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SMTP DEBUG] SmtpService SMTP SendMailAsync Exception. ThreadId={Thread.CurrentThread.ManagedThreadId} Host='{(config.SmtpServer ?? "")}' Port={config.SmtpPort} UseSsl={config.UseSsl}");
                        Debug.WriteLine($"[SMTP DEBUG] ExceptionType={ex.GetType().FullName}");
                        Debug.WriteLine($"[SMTP DEBUG] Message={ex.Message}");
                        Debug.WriteLine($"[SMTP DEBUG] StackTrace={ex.StackTrace}");
                        throw;
                    }
                }
            }
        }
    }
}
