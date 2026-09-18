using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Yakult.ITCM.Server.Security;
using Yakult.ITCM.Server.Services;

namespace Yakult.ITCM.Server.Pages.Account;

[AutoValidateAntiforgeryToken]
public sealed class LoginModel : PageModel
{
    private readonly IItcmAuthenticationService _authenticationService;
    private readonly ItcmAuditService _audit;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IItcmAuthenticationService authenticationService,
        ItcmAuditService audit,
        ILogger<LoginModel> logger)
    {
        _authenticationService = authenticationService;
        _audit = audit;
        _logger = logger;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var result = await _authenticationService.AuthenticateAsync(
                Input.Username,
                Input.Password,
                cancellationToken);

            if (!result.Success || result.User is null)
            {
                _audit.RecordLogin(Input.Username, false, "invalid-credentials");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return Page();
            }

            var isAdmin = result.IsAdministrator;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.UserId.ToString()),
                new(ClaimTypes.Name, result.User.Name),
                new(ClaimTypes.Email, result.User.Email ?? string.Empty),
                new("ItcmAdministrator", isAdmin ? "true" : "false")
            };

            if (isAdmin)
                claims.Add(new("IsDeveloper", "true"));

            foreach (var role in result.User.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, ItcmAuthDefaults.Scheme);
            var principal = new ClaimsPrincipal(identity);

            // Session cookie: no Remember Me and no persistent browser storage.
            await HttpContext.SignInAsync(
                ItcmAuthDefaults.Scheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _audit.RecordLogin(Input.Username, true, isAdmin ? "administrator-authenticated" : "viewer-authenticated");

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return RedirectToPage("/Index");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ITCM login request failed for username {Username}", Input.Username);
            _audit.RecordLogin(Input.Username, false, "authentication-service-unavailable");
            ModelState.AddModelError(string.Empty, "Unable to sign in right now. Please try again later.");
            return Page();
        }
    }

    public sealed class LoginInput
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
