using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Controllers
{
    [RequireLogin]
    public class NotificationController : Controller
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService service)
        {
            _service = service;
        }

        private UserSessionModel? CurrentUser()
            => HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);

        // GET /Notification/UnreadCount — returns { count: N } for badge polling
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var user = CurrentUser();
            if (user == null) return Json(new { count = 0 });

            var count = await _service.GetUnreadCountAsync(user.UserId);
            return Json(new { count });
        }

        // GET /Notification/Panel — returns HTML partial for the dropdown
        [HttpGet]
        public async Task<IActionResult> Panel()
        {
            var user = CurrentUser();
            if (user == null) return Content(string.Empty);

            var notifications = await _service.GetNotificationsForUserAsync(user.UserId, limit: 50);
            return PartialView("_NotificationPanel", notifications);
        }

        // POST /Notification/MarkRead — marks a single notification read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int notificationId)
        {
            var user = CurrentUser();
            if (user == null) return Json(new { success = false });

            await _service.MarkAsReadAsync(notificationId, user.UserId);
            return Json(new { success = true });
        }

        // POST /Notification/MarkAllRead — marks all notifications read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = CurrentUser();
            if (user == null) return Json(new { success = false });

            await _service.MarkAllAsReadAsync(user.UserId);
            return Json(new { success = true });
        }

        // POST /Notification/ClearAll — deletes all notifications for the current user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            var user = CurrentUser();
            if (user == null) return Json(new { success = false });

            await _service.ClearAllAsync(user.UserId);
            return Json(new { success = true });
        }

        // GET /Notification/RecentUnread — returns JSON list of unread notifications for desktop alerts
        [HttpGet]
        public async Task<IActionResult> RecentUnread()
        {
            var user = CurrentUser();
            if (user == null) return Json(Array.Empty<object>());

            var notifications = await _service.GetNotificationsForUserAsync(user.UserId, 20);
            var result = notifications
                .Where(n => !n.IsRead)
                .Select(n => new
                {
                    n.NotificationId,
                    n.Title,
                    n.Message,
                    n.NotificationType,
                    n.ReferenceId
                });
            return Json(result);
        }

        // GET /Notification/Navigate?id=N — marks read then redirects to the referenced record
        [HttpGet]
        public async Task<IActionResult> Navigate(int id, string type, int? referenceId)
        {
            var user = CurrentUser();
            if (user != null)
                await _service.MarkAsReadAsync(id, user.UserId);

            // Authorization events: referenceId is an AuthorizationId → highlight auth row by numeric ID
            object? authRouteVals = referenceId.HasValue ? new { highlight = referenceId.Value } : null;

            // Request events: resolve to SetCode so MyRequests can highlight the exact row.
            // SetCode is the group key every MyRequests row already carries in its data-detail JSON.
            object? myRequestsRouteVals = null;
            if (referenceId.HasValue)
            {
                string? setCode = type switch
                {
                    // ApprovalId → most recent portal request SetCode for that employee
                    NotificationType.RequestApproved => await _service.GetRecentPortalSetCodeByApprovalIdAsync(referenceId.Value),
                    NotificationType.RequestRejected => await _service.GetRecentPortalSetCodeByApprovalIdAsync(referenceId.Value),

                    // AuthorizationId (stored by desktop app on fulfillment) → SetCode via CartridgeAuthorization
                    NotificationType.RequestFulfilled          => await _service.GetSetCodeByAuthorizationIdAsync(referenceId.Value),
                    NotificationType.RequestPartiallyFulfilled => await _service.GetSetCodeByAuthorizationIdAsync(referenceId.Value),
                    NotificationType.RequestUnfulfilled        => await _service.GetSetCodeByAuthorizationIdAsync(referenceId.Value),

                    // ReqId → SetCode directly
                    _ => await _service.GetSetCodeByReqIdAsync(referenceId.Value),
                };

                if (!string.IsNullOrWhiteSpace(setCode))
                    myRequestsRouteVals = new { setCode };
            }

            return type switch
            {
                // Authorization events → EmployeeStatus (highlight the specific auth row)
                NotificationType.AuthorizationApproved     => RedirectToAction("EmployeeStatus", "Authorization", authRouteVals),
                NotificationType.AuthorizationRejected     => RedirectToAction("EmployeeStatus", "Authorization", authRouteVals),

                // All request events → MyRequests (highlight via resolved SetCode)
                NotificationType.RequestApproved           => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestRejected           => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestCompleted          => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestCancelled          => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestStatusChanged      => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestFulfilled          => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestPartiallyFulfilled => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.RequestUnfulfilled        => RedirectToAction("MyRequests", "Request", myRequestsRouteVals),
                NotificationType.Test                      => RedirectToAction("Index", "NotificationSettings"),
                _                                          => RedirectToAction("Index", "Request"),
            };
        }
    }
}
