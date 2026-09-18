using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Repositories;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Persists guided-tour completion state per user.
    /// Keeps the DB in sync with the localStorage cache on the client.
    /// </summary>
    [RequireLogin]
    public class TourController : Controller
    {
        private readonly ITourRepository _tourRepo;

        public TourController(ITourRepository tourRepo)
        {
            _tourRepo = tourRepo;
        }

        /// <summary>
        /// GET /Tour/HasCompleted?tourKey=user-new-request
        /// Returns { completed: true|false }.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> HasCompleted(string tourKey)
        {
            var user = GetCurrentUser();
            if (user == null || string.IsNullOrWhiteSpace(tourKey))
                return Json(new { completed = false });

            var completed = await _tourRepo.HasCompletedAsync(user.UserId, tourKey);
            return Json(new { completed });
        }

        /// <summary>
        /// POST /Tour/MarkCompleted  (form body: tourKey=user-new-request)
        /// Records tour completion for the logged-in user.
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkCompleted([FromForm] string tourKey)
        {
            var user = GetCurrentUser();
            if (user == null || string.IsNullOrWhiteSpace(tourKey))
                return Json(new { success = false });

            await _tourRepo.MarkCompletedAsync(user.UserId, tourKey);
            return Json(new { success = true });
        }

        private UserSessionModel? GetCurrentUser() =>
            HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
    }
}
