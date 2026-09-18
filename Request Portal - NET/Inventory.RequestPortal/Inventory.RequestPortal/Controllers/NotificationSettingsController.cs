using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Filters;

namespace Inventory.RequestPortal.Controllers
{
    [RequireLogin]
    public class NotificationSettingsController : Controller
    {
        private readonly IUserNotificationSettingRepository _settingRepo;
        private readonly INotificationRepository            _notifRepo;
        private readonly ILogger<NotificationSettingsController> _logger;

        public NotificationSettingsController(
            IUserNotificationSettingRepository settingRepo,
            INotificationRepository notifRepo,
            ILogger<NotificationSettingsController> logger)
        {
            _settingRepo = settingRepo;
            _notifRepo   = notifRepo;
            _logger      = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = CurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            bool enabled = await _settingRepo.GetNotificationsEnabledAsync(user.UserId);
            ViewBag.NotificationsEnabled = enabled;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(bool notificationsEnabled)
        {
            var user = CurrentUser();
            if (user == null) return Json(new { success = false });

            await _settingRepo.UpsertAsync(user.UserId, notificationsEnabled);

            // Update session so the bell reflects the change immediately.
            user.NotificationsEnabled = notificationsEnabled;
            HttpContext.Session.SetObject(AccountController.SessionKeyUser, user);

            _logger.LogInformation("[NotificationSettings] UserId={UserId} set NotificationsEnabled={Enabled}",
                user.UserId, notificationsEnabled);

            return Json(new { success = true, notificationsEnabled });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTest()
        {
            var user = CurrentUser();
            if (user == null) return Json(new { success = false, message = "Not logged in." });

            if (!user.NotificationsEnabled)
                return Json(new { success = false, message = "Enable notifications first, then test." });

            await _notifRepo.CreateAsync(new NotificationCreateDto
            {
                UserId           = user.UserId,
                Title            = "Test Notification",
                Message          = "This is a test notification. Your notifications are working correctly.",
                NotificationType = NotificationType.Test,
                ReferenceId      = null,
            });

            _logger.LogInformation("[NotificationSettings] Test notification created for UserId={UserId}", user.UserId);

            return Json(new { success = true, message = "Test notification sent. Check your notification bell." });
        }

        private UserSessionModel? CurrentUser()
            => HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
    }
}
