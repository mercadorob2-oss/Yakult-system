using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Inventory.RequestPortal.Controllers;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Filters
{
    /// <summary>
    /// Action filter that requires user to be logged in.
    /// Redirects to login page if not authenticated.
    /// </summary>
    public class RequireLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var currentUser = session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);

            if (currentUser == null || !currentUser.IsLoggedIn)
            {
                // Get the current URL for redirect after login
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

                context.Result = new RedirectToActionResult(
                    "Login",
                    "Account",
                    new { returnUrl });
            }

            base.OnActionExecuting(context);
        }
    }
}
