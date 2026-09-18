using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Landing hub shown after login. Every logged-in user (except approvers, who keep
    /// going straight to their Authorization queue) lands here and picks a section:
    /// "Request Portal" (everyone) or "Consumable Management" (IT staff only).
    /// </summary>
    [RequireLogin]
    public class PortalHubController : Controller
    {
        private UserSessionModel? GetCurrentUser()
        {
            return HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
        }

        [HttpGet]
        public IActionResult Index()
        {
            var currentUser = GetCurrentUser();

            ViewBag.CurrentUserName = currentUser?.UserName;
            ViewBag.IsLoggedIn      = currentUser?.IsLoggedIn ?? false;
            ViewBag.IsITDepartment  = currentUser?.IsITDepartment ?? false;

            return View();
        }
    }
}
