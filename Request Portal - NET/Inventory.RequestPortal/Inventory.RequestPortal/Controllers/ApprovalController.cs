using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Supervisor Approval page — visible only to users whose employee position
    /// is one of the 10 approved supervisor / coordinator / manager titles.
    /// </summary>
    [RequireLogin]
    public class ApprovalController : Controller
    {
        private static readonly HashSet<string> ApproverPositions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ACCOUNT COORDINATOR",
                "ACTING ACCOUNT COORDINATOR",
                "ASST. COORDINATOR",
                "LADY COORDINATOR",
                "ACTING JR. ASST. MANAGER",
                "ASST. MANAGER",
                "JR. ASST. MANAGER",
                "SUPERVISOR",
                "MANAGER",
                "COORDINATOR"
            };

        private readonly ICartridgeApprovalWebRepository       _approvalRepo;
        private readonly ICartridgeAuthorizationWebRepository  _authRepo;
        private readonly INotificationService                  _notifications;
        private readonly ILogger<ApprovalController>           _logger;
        private readonly IWebHostEnvironment                   _environment;

        public ApprovalController(
            ICartridgeApprovalWebRepository      approvalRepo,
            ICartridgeAuthorizationWebRepository authRepo,
            INotificationService                 notifications,
            ILogger<ApprovalController>          logger,
            IWebHostEnvironment                  environment)
        {
            _approvalRepo  = approvalRepo;
            _authRepo      = authRepo;
            _notifications = notifications;
            _logger        = logger;
            _environment   = environment;
        }

        private UserSessionModel? GetCurrentUser()
            => HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);

        private bool IsApprover(UserSessionModel? user)
        {
            if (user == null) return false;
            // IT / Developer: unconditional approver access
            if (user.IsDeveloper || user.LevelRank >= 999) return true;
            // Rank-based: Coordinator (2), Supervisor (3), Manager (4) can approve
            if (user.LevelRank > 1) return true;
            // Fallback: position-string check for users not yet assigned a level
            return !string.IsNullOrWhiteSpace(user.EmployeePosition)
                   && ApproverPositions.Contains(user.EmployeePosition.Trim());
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = GetCurrentUser();
            if (!IsApprover(currentUser))
                return RedirectToAction("Index", "Request");

            ViewBag.CurrentUserName = currentUser!.UserName;
            ViewBag.IsLoggedIn      = currentUser.IsLoggedIn;
            ViewBag.IsApprover      = true;
            ViewBag.IsDeveloper     = currentUser.IsDeveloper;

            List<CartridgeAuthorizationViewModel> pendingAuths   = new();
            List<CartridgeAuthorizationViewModel> approvedAuths  = new();
            if (currentUser.DepartmentId.HasValue)
            {
                pendingAuths  = await _authRepo.GetPendingForApproverAsync(currentUser.CompanyId, currentUser.BranchId, currentUser.DepartmentId, currentUser.EmployeeId);
                approvedAuths = await _authRepo.GetApprovedByDepartmentAsync(currentUser.DepartmentId.Value);
            }

            var vm = new ApprovalQueueViewModel
            {
                PendingAuthorizations  = pendingAuths,
                ApprovedAuthorizations = approvedAuths,
            };

            if (TempData["SuccessMessage"] != null) ViewBag.SuccessMessage = TempData["SuccessMessage"];
            if (TempData["ErrorMessage"]   != null) ViewBag.ErrorMessage   = TempData["ErrorMessage"];

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(ApproveRequestModel model)
        {
            var currentUser = GetCurrentUser();
            if (!IsApprover(currentUser))
                return RedirectToAction("Index", "Request");

            try
            {
                byte[]? signatureBytes = null;
                string  signatureType  = "Canvas";
                string? mimeType       = "image/png";
                string? fileName       = null;

                // File upload takes precedence over canvas
                if (model.SignatureFile != null && model.SignatureFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await model.SignatureFile.CopyToAsync(ms);
                    signatureBytes = ms.ToArray();
                    signatureType  = "Upload";
                    mimeType       = model.SignatureFile.ContentType;
                    fileName       = model.SignatureFile.FileName;
                }
                else if (!string.IsNullOrWhiteSpace(model.SignatureBase64))
                {
                    // Strip data URI prefix if present (data:image/png;base64,...)
                    var b64 = model.SignatureBase64;
                    var comma = b64.IndexOf(',');
                    if (comma >= 0) b64 = b64[(comma + 1)..];
                    signatureBytes = Convert.FromBase64String(b64);
                    signatureType  = "Canvas";
                    mimeType       = "image/png";
                }

                if (signatureBytes == null || signatureBytes.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please provide a signature before approving.";
                    return RedirectToAction("Index");
                }

                string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                await _approvalRepo.ApproveAsync(
                    model.ApprovalId,
                    model.ApprovalToken,
                    currentUser!.UserId,
                    signatureBytes,
                    signatureType,
                    mimeType,
                    fileName,
                    model.Notes,
                    ipAddress);

                _logger.LogInformation("User {UserId} approved ApprovalId {ApprovalId}",
                    currentUser.UserId, model.ApprovalId);

                _ = _notifications.NotifyApprovalStatusChangedAsync(
                    model.ApprovalId,
                    NotificationType.RequestApproved,
                    "Request Approved",
                    "Your request has been approved by your supervisor.");

                TempData["SuccessMessage"] = "Request approved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving ApprovalId {ApprovalId}", model.ApprovalId);
                TempData["ErrorMessage"] = ex.ToUserMessage(_environment, "Approval failed");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(RejectRequestModel model)
        {
            var currentUser = GetCurrentUser();
            if (!IsApprover(currentUser))
                return RedirectToAction("Index", "Request");

            if (string.IsNullOrWhiteSpace(model.Notes))
            {
                TempData["ErrorMessage"] = "Please enter a reason for the rejection.";
                return RedirectToAction("Index");
            }

            try
            {
                string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                await _approvalRepo.RejectAsync(
                    model.ApprovalId,
                    model.ApprovalToken,
                    currentUser!.UserId,
                    model.Notes,
                    ipAddress);

                _logger.LogInformation("User {UserId} rejected ApprovalId {ApprovalId}",
                    currentUser.UserId, model.ApprovalId);

                _ = _notifications.NotifyApprovalStatusChangedAsync(
                    model.ApprovalId,
                    NotificationType.RequestRejected,
                    "Request Rejected",
                    $"Your request has been rejected. Reason: {model.Notes?.Trim()}");

                TempData["SuccessMessage"] = "Request rejected. The employee has been notified.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting ApprovalId {ApprovalId}", model.ApprovalId);
                TempData["ErrorMessage"] = ex.ToUserMessage(_environment, "Rejection failed");
            }

            return RedirectToAction("Index");
        }
    }
}
