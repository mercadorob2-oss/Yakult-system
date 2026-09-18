using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Landing page for the Consumable Management section (IT department employees only).
    /// Phase 1 only covers the fulfillment loop — links into RequestFulfillment and
    /// CartridgeFulfillment. Master data, batch ops, printing/QR, and notifications
    /// are out of scope for this pass; see the desktop Consumable Management Portal.
    /// </summary>
    [RequireITDepartment]
    public class ConsumableManagementController : Controller
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
