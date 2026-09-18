using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Controllers
{
    [RequireLogin]
    public class AuthorizationController : Controller
    {
        private readonly ICartridgeAuthorizationWebRepository _authRepo;
        private readonly INotificationService _notifications;
        private readonly ILogger<AuthorizationController> _logger;
        private readonly IWebHostEnvironment _environment;

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

        public AuthorizationController(
            ICartridgeAuthorizationWebRepository authRepo,
            INotificationService notifications,
            ILogger<AuthorizationController> logger,
            IWebHostEnvironment environment)
        {
            _authRepo      = authRepo;
            _notifications = notifications;
            _logger        = logger;
            _environment   = environment;
        }

        private UserSessionModel? GetCurrentUser() =>
            HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);

        private bool IsApprover(UserSessionModel? user) =>
            user != null
            && !string.IsNullOrWhiteSpace(user.EmployeePosition)
            && ApproverPositions.Contains(user.EmployeePosition.Trim());

        private bool CanSign(UserSessionModel? user) =>
            (user?.IsDeveloper ?? false) || IsApprover(user);

        private void SetCommonViewBag(UserSessionModel? user)
        {
            ViewBag.CurrentUserName = user?.UserName;
            ViewBag.IsLoggedIn      = user?.IsLoggedIn ?? false;
            ViewBag.IsApprover      = IsApprover(user);
            ViewBag.IsDeveloper     = user?.IsDeveloper ?? false;
            ViewBag.IsITStaff       = user?.IsITStaff ?? false;
            ViewBag.IsITDepartment  = user?.IsITDepartment ?? false;
        }

        // ── Approver Landing ─────────────────────────────────────────────────

        /// <summary>
        /// Landing page for approvers (coordinators/managers/supervisors).
        /// Shows two options: Authorize Cartridge Requests and Submit a Request.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Landing()
        {
            var user = GetCurrentUser();

            if (user?.IsDeveloper == true)
                return RedirectToAction("DevQueue");

            if (!IsApprover(user))
                return RedirectToAction("Index", "Request");

            SetCommonViewBag(user);

            // Count pending authorizations for badge.
            int pendingCount = 0;
            if (user!.DepartmentId.HasValue)
            {
                var pending = await _authRepo.GetPendingByDepartmentAsync(user.DepartmentId.Value);
                pendingCount = pending.Count;
            }

            ViewBag.PendingCount = pendingCount;
            ViewBag.EmployeePosition = user.EmployeePosition;

            return View();
        }

        // ── Employee Status ──────────────────────────────────────────────────

        /// <summary>
        /// Renders the page shell instantly. Table data is loaded via AJAX by EmployeeStatusTable.
        /// </summary>
        [HttpGet]
        public IActionResult EmployeeStatus()
        {
            var user = GetCurrentUser();
            SetCommonViewBag(user);
            return View();
        }

        /// <summary>
        /// AJAX endpoint: returns the authorization history table as a partial view.
        /// Called by EmployeeStatus.cshtml on DOMContentLoaded.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EmployeeStatusTable()
        {
            var user = GetCurrentUser();
            var history = new List<CartridgeAuthorizationViewModel>();
            if (user?.EmployeeId.HasValue == true)
                history = await _authRepo.GetHistoryByEmployeeAsync(user.EmployeeId.Value);
            return PartialView("_EmployeeStatusContent", history);
        }

        // ── Supervisor Queue ─────────────────────────────────────────────────

        /// <summary>
        /// Shows all pending authorizations for the supervisor's department.
        /// Accessible only to users with an approver position.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SupervisorQueue(int? selectedId = null)
        {
            var user = GetCurrentUser();

            if (user?.IsDeveloper == true)
                return RedirectToAction("DevQueue", new { selectedId });

            if (!IsApprover(user))
                return RedirectToAction("Index", "Request");

            SetCommonViewBag(user);

            var pending = new List<CartridgeAuthorizationViewModel>();

            if (user!.DepartmentId.HasValue)
            {
                pending = await _authRepo.GetPendingByDepartmentAsync(user.DepartmentId.Value);
            }

            ViewBag.SelectedAuthId = selectedId;
            return View(new SupervisorAuthorizationQueueViewModel { Pending = pending });
        }

        // ── Developer Queue ──────────────────────────────────────────────────

        /// <summary>
        /// Shows all pending authorizations across all departments.
        /// Accessible only to developer accounts (IsDeveloper = true).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DevQueue(int? selectedId = null)
        {
            var user = GetCurrentUser();

            if (user?.IsDeveloper != true)
                return RedirectToAction("Index", "Request");

            SetCommonViewBag(user);

            var pending = await _authRepo.GetAllPendingAsync();
            ViewBag.SelectedAuthId = selectedId;
            return View("SupervisorQueue", new SupervisorAuthorizationQueueViewModel { Pending = pending });
        }

        // ── Authorization Details ────────────────────────────────────────────

        /// <summary>
        /// Shows a specific authorization so the supervisor can approve or reject it.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(int id, bool selfSign = false)
        {
            var user = GetCurrentUser();

            if (!CanSign(user))
                return RedirectToAction("Index", "Request");

            var auth = await _authRepo.GetByIdAsync(id);

            if (auth == null)
                return NotFound();

            // Supervisors may only act on records in their own department; developers bypass this
            if (user!.IsDeveloper != true && auth.DepartmentId != user!.DepartmentId)
                return Forbid();

            SetCommonViewBag(user);
            ViewBag.SignerFullName   = user!.IsDeveloper ? "Information Technology Department" : (user.EmployeeName ?? string.Empty);
            ViewBag.SignerPosition   = user.IsDeveloper ? string.Empty : (user.EmployeePosition ?? string.Empty);
            ViewBag.SignerCompany    = user.IsDeveloper ? string.Empty : (user.CompanyName ?? string.Empty);
            ViewBag.SignerBranch     = user.IsDeveloper ? string.Empty : (user.BranchName ?? string.Empty);
            ViewBag.SignerDepartment = user.IsDeveloper ? string.Empty : (user.DepartmentName ?? string.Empty);
            ViewBag.IsSelfSign       = selfSign;
            return View(new AuthorizationDetailsViewModel { Authorization = auth });
        }

        // ── Tour: demo Details page (no real DB record needed) ──────────────
        /// <summary>
        /// Renders the Authorization Details view with hardcoded demo data so the
        /// guided tour can walk through the self-sign flow without requiring an
        /// actual submission. Only accessible to approvers.
        /// </summary>
        [HttpGet]
        public IActionResult TourDetails()
        {
            var user = GetCurrentUser();
            if (!CanSign(user))
                return RedirectToAction("Index", "Request");

            SetCommonViewBag(user);
            ViewBag.SignerFullName   = user!.EmployeeName   ?? "Your Name";
            ViewBag.SignerPosition   = user.EmployeePosition ?? "Your Position";
            ViewBag.SignerCompany    = user.CompanyName      ?? "Your Company";
            ViewBag.SignerBranch     = user.BranchName       ?? "Your Branch";
            ViewBag.SignerDepartment = user.DepartmentName   ?? "Your Department";
            ViewBag.IsSelfSign       = true;

            var demo = new CartridgeAuthorizationViewModel
            {
                AuthorizationId  = 0,
                EmployeeName     = user.EmployeeName   ?? "Your Name",
                DepartmentName   = user.DepartmentName ?? "Your Department",
                BranchName       = user.BranchName     ?? "Your Branch",
                CompanyName      = user.CompanyName    ?? "Your Company",
                Status           = "Pending",
                CreatedDate      = DateTime.Now,
                RequestedModels  = "[{\"model\":\"CE285A (HP 85A)\",\"qty\":2,\"good\":1,\"damaged\":0}]",
                FulfillmentMethod = "Pickup",
                ReceivedByName   = user.EmployeeName   ?? "Your Name",
            };

            return View("Details", new AuthorizationDetailsViewModel { Authorization = demo });
        }

        // ── Pending Status (JSON polling endpoint) ───────────────────────────

        /// <summary>
        /// Returns the current pending authorizations for the approver's department as JSON.
        /// Used by the queue page to poll for real-time status updates every 10 seconds.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PendingStatus()
        {
            var user = GetCurrentUser();
            if (!CanSign(user)) return Forbid();

            var pending = user!.IsDeveloper
                ? await _authRepo.GetAllPendingAsync()
                : user.DepartmentId.HasValue
                    ? await _authRepo.GetPendingByDepartmentAsync(user.DepartmentId.Value)
                    : new List<CartridgeAuthorizationViewModel>();

            var result = pending.Select(a => new
            {
                authorizationId = a.AuthorizationId,
                approvalLevel   = a.ApprovalLevel,
                tooltip         = a.ApprovalLevelTooltip,
            });

            return Json(result);
        }

        // ── Details Panel (partial for split-pane queue) ─────────────────────

        /// <summary>
        /// Returns the authorization detail as a partial view (no layout) for the split-pane queue.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetailsPanel(int id)
        {
            var user = GetCurrentUser();
            if (!CanSign(user)) return Forbid();

            var auth = await _authRepo.GetByIdAsync(id);
            if (auth == null) return NotFound();
            if (user!.IsDeveloper != true && auth.DepartmentId != user!.DepartmentId) return Forbid();

            SetCommonViewBag(user);
            ViewBag.SignerFullName   = user!.IsDeveloper ? "Information Technology Department" : (user.EmployeeName ?? string.Empty);
            ViewBag.SignerPosition   = user.IsDeveloper ? string.Empty : (user.EmployeePosition ?? string.Empty);
            ViewBag.SignerCompany    = user.IsDeveloper ? string.Empty : (user.CompanyName ?? string.Empty);
            ViewBag.SignerBranch     = user.IsDeveloper ? string.Empty : (user.BranchName ?? string.Empty);
            ViewBag.SignerDepartment = user.IsDeveloper ? string.Empty : (user.DepartmentName ?? string.Empty);
            return PartialView("_DetailsPanel", new AuthorizationDetailsViewModel { Authorization = auth });
        }

        // ── Approve ──────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(ApproveSignatureInputModel input)
        {
            var user = GetCurrentUser();

            if (!CanSign(user))
                return RedirectToAction("Index", "Request");

            // Server-side validation
            if (string.IsNullOrWhiteSpace(input.SignatureData))
            {
                TempData["ErrorMessage"] = "A signature is required to approve this authorization.";
                return RedirectToAction("Details", new { id = input.AuthorizationId, selfSign = input.IsSelfSign });
            }

            if (string.IsNullOrWhiteSpace(input.RequestedModels))
            {
                TempData["ErrorMessage"] = "Please specify the requested cartridge models.";
                return RedirectToAction("Details", new { id = input.AuthorizationId, selfSign = input.IsSelfSign });
            }

            try
            {
                var sig = new ApproveSignatureData
                {
                    SignatureData    = input.SignatureData,
                    SignatureSource  = input.SignatureSource ?? "draw",
                    SignatureFileName = input.SignatureFileName,
                    RequestedModels  = input.RequestedModels,
                    // Capture signer's context from session — never trust posted values
                    SignerPosition   = user!.EmployeePosition ?? string.Empty,
                    SignerCompany    = user.CompanyName       ?? string.Empty,
                    SignerBranch     = user.BranchName        ?? string.Empty,
                };

                bool succeeded = await _authRepo.ApproveAsync(input.AuthorizationId, user.UserId, sig);

                if (succeeded)
                {
                    _logger.LogInformation("User {UserId} approved authorization #{AuthId} with {Source} signature",
                        user.UserId, input.AuthorizationId, sig.SignatureSource);

                    _ = _notifications.NotifyAuthorizationStatusChangedAsync(
                        input.AuthorizationId,
                        NotificationType.AuthorizationApproved,
                        "Authorization Approved",
                        "Your cartridge authorization request has been approved and signed.");

                    TempData["SuccessMessage"] = "Authorization approved and signed successfully.";
                    if (input.IsSelfSign)
                        return RedirectToAction("MyRequests", "Request");
                    return user!.IsDeveloper
                        ? RedirectToAction("DevQueue")
                        : RedirectToAction("SupervisorQueue");
                }
                else
                {
                    TempData["ErrorMessage"] =
                        "This authorization was already signed. No changes were made.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving authorization #{AuthId}", input.AuthorizationId);
                TempData["ErrorMessage"] = ex.ToUserMessage(_environment, "An error occurred");
            }

            return input.IsSelfSign
                ? RedirectToAction("Details", new { id = input.AuthorizationId, selfSign = true })
                : RedirectToAction("SupervisorQueue");
        }

        // ── History ──────────────────────────────────────────────────────────

        private const int HistoryPageSize = 10;

        [HttpGet]
        public async Task<IActionResult> History(string? search, string? status, int page = 1)
        {
            var user = GetCurrentUser();
            SetCommonViewBag(user);
            ViewBag.EmployeePosition = user?.EmployeePosition;

            var pageItems = new List<CartridgeAuthorizationViewModel>();
            int totalCount = 0;
            if (user?.EmployeeId.HasValue == true)
            {
                (pageItems, totalCount) = await _authRepo.GetHistoryByEmployeePagedAsync(
                    user.EmployeeId.Value, page, HistoryPageSize, search, status);
                ViewBag.StatusOptions = await _authRepo.GetDistinctHistoryStatusesByEmployeeAsync(user.EmployeeId.Value);
            }
            else
            {
                ViewBag.StatusOptions = new List<string>();
            }

            if (IsApprover(user) && user!.DepartmentId.HasValue)
            {
                var deptApproved = await _authRepo.GetApprovedByDepartmentAsync(user.DepartmentId.Value);
                ViewBag.SignedApprovals = deptApproved
                    .Where(a => a.SignedBySupervisorId.HasValue && a.SignedBySupervisorId == user.UserId)
                    .ToList();
            }

            int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)HistoryPageSize);
            ViewBag.SearchText   = search ?? string.Empty;
            ViewBag.StatusFilter = status ?? string.Empty;
            ViewBag.CurrentPage  = Math.Max(1, Math.Min(page, totalPages));
            ViewBag.TotalPages   = totalPages;
            ViewBag.TotalCount   = totalCount;

            return View(pageItems);
        }

        /// <summary>
        /// Filters an already-loaded history list in memory (GetHistoryByEmployeeAsync loads
        /// the full result set either way) — search matches signed-by, department, branch,
        /// and company; status is an exact match against the raw Status value.
        /// </summary>
        private static List<CartridgeAuthorizationViewModel> FilterAuthorizationHistory(
            List<CartridgeAuthorizationViewModel> history, string? search, string? status)
        {
            IEnumerable<CartridgeAuthorizationViewModel> filtered = history;

            if (!string.IsNullOrWhiteSpace(status))
                filtered = filtered.Where(a => string.Equals(a.Status, status, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim();
                filtered = filtered.Where(a =>
                    Contains(a.SignedByName, q) || Contains(a.DepartmentName, q) ||
                    Contains(a.BranchName, q) || Contains(a.CompanyName, q));
            }

            return filtered.ToList();
        }

        private static bool Contains(string? haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Downloads the same employee authorization history shown on the History page as a CSV,
        /// with the same search/status filter applied so the export matches the current view.
        /// Deliberately excludes the signed-by-me department approvals ViewBag block — that's a
        /// separate, secondary list on the page, not part of "my authorization history".
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportHistoryCsv(string? search, string? status)
        {
            var user = GetCurrentUser();

            var fullHistory = new List<CartridgeAuthorizationViewModel>();
            if (user?.EmployeeId.HasValue == true)
                fullHistory = await _authRepo.GetHistoryByEmployeeAsync(user.EmployeeId.Value);

            var history = FilterAuthorizationHistory(fullHistory, search, status);

            var headers = new[]
            {
                "Authorization Id", "Date Requested", "Status", "Approval Level",
                "Signed By", "Signed Date", "Department", "Branch", "Company"
            };
            var rows = history.Select(a => new object?[]
            {
                a.AuthorizationId, a.CreatedDate.ToString("yyyy-MM-dd HH:mm"), a.Status, a.ApprovalLevel,
                a.SignedByName, a.SignedDate?.ToString("yyyy-MM-dd HH:mm"), a.DepartmentName, a.BranchName, a.CompanyName
            });

            var csv = CsvExportExtensions.ToCsvBytes(headers, rows);
            return File(csv, "text/csv", $"authorization-history-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        // ── Employee View (read-only detail for non-approvers) ───────────────

        /// <summary>
        /// Returns a read-only partial view of an authorization for the employee who owns it.
        /// Used by the row-click detail modal on EmployeeStatus and History pages.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EmployeeView(int id)
        {
            var user = GetCurrentUser();
            var auth = await _authRepo.GetByIdAsync(id);

            if (auth == null) return NotFound();
            if (auth.EmployeeId != user?.EmployeeId) return Forbid();

            return PartialView("_EmployeeAuthDetail", auth);
        }

        // ── Reject ───────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int authorizationId)
        {
            var user = GetCurrentUser();

            if (!CanSign(user))
                return RedirectToAction("Index", "Request");

            try
            {
                await _authRepo.RejectAsync(authorizationId);

                _logger.LogInformation("User {UserId} rejected authorization #{AuthId}",
                    user!.UserId, authorizationId);

                _ = _notifications.NotifyAuthorizationStatusChangedAsync(
                    authorizationId,
                    NotificationType.AuthorizationRejected,
                    "Authorization Rejected",
                    "Your cartridge authorization request has been rejected. Please contact your supervisor for details.");

                TempData["SuccessMessage"] = "Authorization rejected.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting authorization #{AuthId}", authorizationId);
                TempData["ErrorMessage"] = ex.ToUserMessage(_environment, "An error occurred");
            }

            return user!.IsDeveloper
                ? RedirectToAction("DevQueue")
                : RedirectToAction("SupervisorQueue");
        }
    }
}
