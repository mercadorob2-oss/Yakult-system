using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Services;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Web port of the desktop "Send Notifications" page under Cartridge Management
    /// (Wpf/SendNotifications/ViewModels/SendNotificationsViewModel.cs +
    /// Wpf/SendNotifications/Views/SendNotificationsView.xaml). Same two-pane shape as
    /// CartridgeExchangeController: a persistent filtered list on the left, a detail panel on the
    /// right that (re)loads via AJAX when a row is selected, with Preview/Send/Save Receiver
    /// actions scoped to that one Set.
    /// </summary>
    [RequireITDepartment]
    public class SendNotificationsController : Controller
    {
        private readonly ISendNotificationsRepository _repo;
        private readonly ICartridgeExchangeRepository _cartridgeExchangeRepo;
        private readonly ICartridgeEmailService _emailService;
        private readonly IActivityLogRepository _activityLog;

        public SendNotificationsController(
            ISendNotificationsRepository repo,
            ICartridgeExchangeRepository cartridgeExchangeRepo,
            ICartridgeEmailService emailService,
            IActivityLogRepository activityLog)
        {
            _repo = repo;
            _cartridgeExchangeRepo = cartridgeExchangeRepo;
            _emailService = emailService;
            _activityLog = activityLog;
        }

        private UserSessionModel? GetCurrentUser()
        {
            return HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
        }

        private void SetCommonViewBag(UserSessionModel? user)
        {
            ViewBag.CurrentUserName = user?.UserName;
            ViewBag.IsLoggedIn = user?.IsLoggedIn ?? false;
            ViewBag.IsITDepartment = user?.IsITDepartment ?? false;
        }

        private static List<FulfilledSetNotificationDto> ApplyFilters(
            List<FulfilledSetNotificationDto> rows, string? search, string status, DateTime? from, DateTime? to)
        {
            IEnumerable<FulfilledSetNotificationDto> query = rows;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(r =>
                    r.SetCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.RequesterName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.CompanyName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.BranchDept.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
                query = query.Where(r => r.FriendlyStatus == status);

            if (from.HasValue)
                query = query.Where(r => r.CreatedAt == DateTime.MinValue || r.CreatedAt.Date >= from.Value.Date);

            if (to.HasValue)
                query = query.Where(r => r.CreatedAt == DateTime.MinValue || r.CreatedAt.Date <= to.Value.Date);

            return query.ToList();
        }

        private async Task<SendNotificationDetailViewModel?> BuildDetailAsync(int setId)
        {
            var rows = await _repo.GetFulfilledSetsForNotificationAsync();
            var row = rows.FirstOrDefault(r => r.SetId == setId);
            if (row == null) return null;

            var receivers = new List<EmployeeOptionDto>();
            if (row.IsPickup)
            {
                receivers.Add(new EmployeeOptionDto { EmpId = 0, Name = "— Select receiver —" });
                receivers.AddRange(await _repo.GetActiveEmployeesForReceiverAsync());
            }

            return new SendNotificationDetailViewModel
            {
                Row = row,
                ReceiverOptions = receivers
            };
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string status = "All", DateTime? from = null, DateTime? to = null, int? id = null)
        {
            var allRows = await _repo.GetFulfilledSetsForNotificationAsync();
            var filtered = ApplyFilters(allRows, search, status, from, to);

            var today = DateTime.Today;
            var vm = new SendNotificationsWorkspaceViewModel
            {
                Rows = filtered,
                SearchText = search,
                StatusFilter = string.IsNullOrWhiteSpace(status) ? "All" : status,
                DateFrom = from,
                DateTo = to,
                PendingCount = allRows.Count(r => r.SetStatus == "Pending"),
                FulfilledCount = allRows.Count(r => r.SetStatus == "Dispatched"),
                UpdatedTodayCount = allRows.Count(r => r.CreatedAt.Date == today),
                TotalCount = allRows.Count,
                SelectedSetId = id
            };

            if (id.HasValue)
                vm.SelectedDetail = await BuildDetailAsync(id.Value);

            SetCommonViewBag(GetCurrentUser());
            return View(vm);
        }

        /// <summary>AJAX endpoint backing the grid row click — returns just the detail panel markup.</summary>
        [HttpGet]
        public async Task<IActionResult> Panel(int id)
        {
            var detail = await BuildDetailAsync(id);
            SetCommonViewBag(GetCurrentUser());
            return PartialView("_DetailPanel", detail);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveReceiver(int setId, int empId)
        {
            if (empId <= 0)
                return Json(new { success = false, message = "Please select a receiver before saving." });

            var rows = await _repo.GetFulfilledSetsForNotificationAsync();
            var row = rows.FirstOrDefault(r => r.SetId == setId);
            if (row == null)
                return Json(new { success = false, message = "Set not found." });

            var currentUser = GetCurrentUser();
            int userId = currentUser?.UserId ?? 0;

            try
            {
                if (row.SubmissionSessionId.HasValue)
                    await _repo.UpdateReceivedByForSessionAsync(row.SubmissionSessionId.Value, empId, userId);
                else
                    await _repo.UpdateReceivedByForRequestAsync(row.ReqId, empId, userId);

                var receivers = await _repo.GetActiveEmployeesForReceiverAsync();
                var chosen = receivers.FirstOrDefault(e => e.EmpId == empId);

                _activityLog.LogActivity(userId, "Update", "Set", setId,
                    $"[Portal] Updated Received By for Set {row.SetCode} to {chosen?.Name ?? empId.ToString()}.");

                return Json(new { success = true, receivedByName = chosen?.Name ?? "" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Could not save receiver: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Preview(int setId, string? notes)
        {
            var rows = await _repo.GetFulfilledSetsForNotificationAsync();
            var row = rows.FirstOrDefault(r => r.SetId == setId);
            if (row == null) return NotFound();

            var detail = await _cartridgeExchangeRepo.GetFulfilledCartridgeDetailAsync(setId);
            var (_, body) = _emailService.BuildFulfillmentNotificationEmail(row, detail, notes);
            return Content(body, "text/html");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int setId, string? notes)
        {
            var rows = await _repo.GetFulfilledSetsForNotificationAsync();
            var row = rows.FirstOrDefault(r => r.SetId == setId);
            if (row == null)
                return Json(new { success = false, message = "Set not found." });

            if (row.EmpId <= 0)
                return Json(new { success = false, message = "This set has no linked requester employee to notify." });

            var currentUser = GetCurrentUser();
            int userId = currentUser?.UserId ?? 0;

            try
            {
                var detail = await _cartridgeExchangeRepo.GetFulfilledCartridgeDetailAsync(setId);
                var (subject, body) = _emailService.BuildFulfillmentNotificationEmail(row, detail, notes);

                string? error = await _emailService.SendRenderedEmailAsync(row.EmpId, subject, body);
                if (error != null)
                    return Json(new { success = false, message = error });

                _activityLog.LogActivity(userId, "Update", "Set", setId,
                    $"[Portal] Sent fulfillment notification email for Set {row.SetCode}.");

                return Json(new { success = true, message = "Notification sent successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Send failed: {ex.Message}" });
            }
        }
    }
}
