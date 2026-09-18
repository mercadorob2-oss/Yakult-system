using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

public interface IDemoModeService
{
    bool IsAvailable { get; }
    bool CanUse(HttpContext httpContext);
    bool IsEnabled(HttpContext httpContext);
    void SetEnabled(HttpContext httpContext, bool enabled);
}

public sealed class DemoModeService : IDemoModeService
{
    private const string SessionKey = "Yakult.SystemsPortal.DemoMode";
    private readonly PortalOptions _options;

    public DemoModeService(IOptions<PortalOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAvailable => _options.EnableDemoMode;

    public bool CanUse(HttpContext httpContext)
    {
        if (!IsAvailable || httpContext.User.Identity?.IsAuthenticated != true)
            return false;

        var user = httpContext.User;
        if (user.HasClaim("IsDeveloper", "true") || user.IsInRole("Administrator"))
            return true;

        var username = user.Identity?.Name;
        return !string.IsNullOrWhiteSpace(username)
            && _options.DemoModeAllowedUsernames.Contains(username, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEnabled(HttpContext httpContext) =>
        CanUse(httpContext)
        && httpContext.Session.GetString(SessionKey) == "true";

    public void SetEnabled(HttpContext httpContext, bool enabled)
    {
        if (!enabled || !CanUse(httpContext))
        {
            httpContext.Session.Remove(SessionKey);
            return;
        }

        httpContext.Session.SetString(SessionKey, "true");
    }
}
