using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Orchestrates the cartridge-request supervisor approval workflow:
    ///   • Check/create approval on employee login
    ///   • Send SMTP notification to supervisor
    ///   • Save signature and approve
    ///   • Reject with reason
    ///   • Notify employee of decision
    ///
    /// Signature file storage:
    ///   Files are written to {AppBase}\Signatures\Approvals\{approvalId}_{timestamp}.png
    ///   The relative path is stored in CartridgeApprovalSignature.SignaturePath.
    /// </summary>
    public class CartridgeApprovalService
    {
        private readonly CartridgeApprovalRepository _repo;
        private readonly EmailRepository             _emailRepo;
        private readonly SmtpService                 _smtp;

        // Template keys (must match seeds in migration script)
        private const string TplRequest  = "CARTRIDGE_APPROVAL_REQUEST";
        private const string TplGranted  = "CARTRIDGE_APPROVAL_GRANTED";
        private const string TplRejected = "CARTRIDGE_APPROVAL_REJECTED";

        public CartridgeApprovalService(
            CartridgeApprovalRepository repo,
            EmailRepository             emailRepo,
            SmtpService                 smtp)
        {
            _repo      = repo      ?? throw new ArgumentNullException(nameof(repo));
            _emailRepo = emailRepo ?? throw new ArgumentNullException(nameof(emailRepo));
            _smtp      = smtp      ?? throw new ArgumentNullException(nameof(smtp));
        }

        // ── Login gate ───────────────────────────────────────────────────────

        /// <summary>
        /// Call this immediately after a successful employee login.
        /// Returns the current approval status so the caller can redirect accordingly.
        ///
        /// Status returns:
        ///   "Approved" – employee may proceed into the request portal
        ///   "Pending"  – waiting on supervisor; show pending page
        ///   "Created"  – new pending row created, email sent to supervisor
        ///   "Rejected" – supervisor declined; show rejection message
        /// </summary>
        public async Task<CartridgeApprovalDto> HandleLoginCheckAsync(
            int empId, string ipAddress = null)
        {
            var approval = await _repo.CheckOrCreateAsync(empId, ipAddress);

            if (approval == null)
                return null;

            // If a new record was created, send the supervisor notification
            if (approval.Status == "Created")
            {
                await SendSupervisorNotificationAsync(approval);
                approval.Status = "Pending";   // normalise for caller
            }

            return approval;
        }

        // ── Approve ──────────────────────────────────────────────────────────

        /// <summary>
        /// Saves the e-signature image, then approves the request.
        /// After approval, sends an email to the employee (if email is known).
        /// </summary>
        public async Task<CartridgeApprovalDto> ApproveAsync(
            ApprovalSignatureInput input,
            string employeeEmail,
            string employeeName,
            string expiresDateDisplay)
        {
            // 1. Persist the signature file to disk
            string signaturePath = null;
            if (input.SignatureData != null && input.SignatureData.Length > 0)
                signaturePath = SaveSignatureFile(input.ApprovalId, input.SignatureData, input.MimeType);

            // 2. Update DB (SP handles the transaction)
            var approved = await _repo.ApproveAsync(input, signaturePath);
            if (approved == null) return null;

            // 3. Notify employee
            if (!string.IsNullOrWhiteSpace(employeeEmail))
            {
                try
                {
                    var tpl = await _emailRepo.GetEmailTemplateByKeyAsync(TplGranted);
                    if (tpl != null && tpl.IsActive)
                    {
                        var body = tpl.BodyTemplate
                            .Replace("{{EMPLOYEE_NAME}}",    employeeName ?? "")
                            .Replace("{{SUPERVISOR_NAME}}", "your supervisor")
                            .Replace("{{APPROVED_DATE}}",   DateTime.Now.ToString("MMMM dd, yyyy"))
                            .Replace("{{EXPIRES_DATE}}",    expiresDateDisplay ?? "");

                        var subject = tpl.SubjectTemplate;

                        await _smtp.SendEmailAsync(employeeEmail, subject, body, tpl.IsHtml);
                    }
                }
                catch
                {
                    // Don't fail the approval if the notification email fails
                }
            }

            return approved;
        }

        // ── Reject ───────────────────────────────────────────────────────────

        /// <summary>
        /// Rejects the approval and optionally notifies the employee.
        /// </summary>
        public async Task<CartridgeApprovalDto> RejectAsync(
            int approvalId, Guid token, int supervisorUserId,
            string supervisorName, string notes, string ipAddress,
            string employeeEmail, string employeeName)
        {
            var rejected = await _repo.RejectAsync(
                approvalId, token, supervisorUserId, notes, ipAddress);

            if (rejected == null) return null;

            // Notify employee
            if (!string.IsNullOrWhiteSpace(employeeEmail))
            {
                try
                {
                    var tpl = await _emailRepo.GetEmailTemplateByKeyAsync(TplRejected);
                    if (tpl != null && tpl.IsActive)
                    {
                        var body = tpl.BodyTemplate
                            .Replace("{{EMPLOYEE_NAME}}",    employeeName ?? "")
                            .Replace("{{SUPERVISOR_NAME}}", supervisorName ?? "")
                            .Replace("{{REJECTION_NOTES}}",  string.IsNullOrWhiteSpace(notes)
                                                                ? "No reason provided."
                                                                : notes);

                        await _smtp.SendEmailAsync(
                            employeeEmail, tpl.SubjectTemplate, body, tpl.IsHtml);
                    }
                }
                catch
                {
                    // Swallow — don't block rejection because email failed
                }
            }

            return rejected;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task SendSupervisorNotificationAsync(CartridgeApprovalDto approval)
        {
            if (string.IsNullOrWhiteSpace(approval.SupervisorEmail))
                return;

            try
            {
                var tpl = await _emailRepo.GetEmailTemplateByKeyAsync(TplRequest);
                if (tpl == null || !tpl.IsActive)
                    return;

                var body = tpl.BodyTemplate
                    .Replace("{{SUPERVISOR_NAME}}",   approval.SupervisorName   ?? "Supervisor")
                    .Replace("{{EMPLOYEE_NAME}}",     approval.EmployeeName     ?? "")
                    .Replace("{{EMPLOYEE_POSITION}}", approval.EmployeePosition ?? "")
                    .Replace("{{COMPANY_NAME}}",      approval.CompanyName      ?? "")
                    .Replace("{{BRANCH_NAME}}",       approval.BranchName       ?? "")
                    .Replace("{{DEPARTMENT_NAME}}",   approval.DepartmentName   ?? "")
                    .Replace("{{REQUESTED_DATE}}",    approval.RequestedAt.ToString("MMMM dd, yyyy  h:mm tt"));

                var subject = tpl.SubjectTemplate
                    .Replace("{{EMPLOYEE_NAME}}", approval.EmployeeName ?? "");

                bool sent = await _smtp.SendEmailAsync(
                    approval.SupervisorEmail, subject, body, tpl.IsHtml);

                if (sent)
                {
                    int logId = await _emailRepo.LogEmailAsync(new SystemEmailLogDto
                    {
                        TemplateKey  = TplRequest,
                        Recipients   = approval.SupervisorEmail,
                        Subject      = subject,
                        Status       = "Sent",
                        EntityType   = "CartridgeApproval",
                        EntityId     = approval.ApprovalId,
                        SentDate     = DateTime.UtcNow
                    });

                    await _repo.MarkEmailSentAsync(approval.ApprovalId, logId > 0 ? logId : (int?)null);
                }
            }
            catch
            {
                // Non-fatal — approval row exists; email can be retried
            }
        }

        /// <summary>
        /// Writes the signature bytes to disk and returns the relative path.
        /// </summary>
        private static string SaveSignatureFile(int approvalId, byte[] data, string mimeType)
        {
            string ext = (mimeType ?? "").Contains("jpeg") ? ".jpg" : ".png";
            string fileName = $"approval_{approvalId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";

            string relativeDir  = Path.Combine("Signatures", "Approvals");
            string absoluteDir  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            string absolutePath = Path.Combine(absoluteDir, fileName);
            File.WriteAllBytes(absolutePath, data);

            return Path.Combine(relativeDir, fileName);   // stored in DB
        }
    }
}
