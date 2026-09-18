using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Inventory.RequestPortal.Controllers;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Filters
{
    /// <summary>
    /// Action filter that requires the logged-in user to be a Developer account or an
    /// Information Technology department employee (UserSessionModel.IsITDepartment).
    /// Gates the Consumable Management portal — Department-based, not Role-based (see
    /// IsITDepartment for why this differs from IsITStaff/RequireLoginAttribute's cousin).
    /// Redirects to the Portal Hub if the requirement isn't met.
    /// </summary>
    public class RequireITDepartmentAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var currentUser = session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);

            if (currentUser == null || !currentUser.IsLoggedIn)
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

                context.Result = new RedirectToActionResult(
                    "Login",
                    "Account",
                    new { returnUrl });
            }
            else if (!currentUser.IsITDepartment)
            {
                context.Result = new RedirectToActionResult("Index", "PortalHub", null);
            }

            base.OnActionExecuting(context);
        }
    }
}
