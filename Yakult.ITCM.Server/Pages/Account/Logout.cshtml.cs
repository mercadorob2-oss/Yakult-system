using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Yakult.ITCM.Server.Security;
using Yakult.ITCM.Server.Services;

namespace Yakult.ITCM.Server.Pages.Account;

[AutoValidateAntiforgeryToken]
public sealed class LogoutModel : PageModel
{
    private readonly ItcmAuditService _audit;

    public LogoutModel(ItcmAuditService audit)
    {
        _audit = audit;
    }

    public IActionResult OnGet() => RedirectToPage("/Account/Login");

    public async Task<IActionResult> OnPostAsync()
    {
        _audit.Record(HttpContext, "Logout", true);
        await HttpContext.SignOutAsync(ItcmAuthDefaults.Scheme);
        return RedirectToPage("/Account/Login");
    }
}
