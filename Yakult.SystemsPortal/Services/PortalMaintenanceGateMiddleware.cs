using Microsoft.AspNetCore.Http;
using Yakult.SystemsPortal.Repositories;

namespace Yakult.SystemsPortal.Services;

/// <summary>
/// Enforces a whole-site full-page notice when the existing Portal Notice is
/// active and set to the Maintenance level. Developer accounts keep access to
/// Admin so the notice can be disabled without a server-side intervention.
/// </summary>
public sealed class PortalMaintenanceGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PortalMaintenanceGateMiddleware> _logger;

    public PortalMaintenanceGateMiddleware(
        RequestDelegate next,
        ILogger<PortalMaintenanceGateMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IPortalCardRepository portalCardRepository)
    {
        if (IsBypassRequest(context))
        {
            await _next(context);
            return;
        }

        try
        {
            var notice = await portalCardRepository.GetNoticeSettingsAsync();
            if (!notice.IsMaintenanceMode || !notice.IsActive(DateTime.UtcNow))
            {
                await _next(context);
                return;
            }

            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";

            if (IsBrowserNavigation(context.Request))
            {
                context.Response.Redirect("/Maintenance");
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status503ServiceUnavailable,
                message = string.IsNullOrWhiteSpace(notice.Message)
                    ? "The Yakult Systems Portal is temporarily under maintenance."
                    : notice.Message
            });
        }
        catch (Exception ex)
        {
            // A transient settings/database failure must not turn the entire portal
            // into a maintenance outage. Existing page-level fallbacks still apply.
            _logger.LogWarning(ex, "Maintenance gate could not read the portal notice; allowing the request.");
            await _next(context);
        }
    }

    private static bool IsBypassRequest(HttpContext context)
    {
        if (context.User.HasClaim("IsDeveloper", "true"))
            return true;

        var path = context.Request.Path;
        return path.StartsWithSegments("/Maintenance", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/Home/Login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/Home/Logout", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/Home/Error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrowserNavigation(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        return request.Headers.Accept.Any(value =>
            value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
    }
}
