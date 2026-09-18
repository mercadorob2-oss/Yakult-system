using System.Security.Claims;

namespace Yakult.ITCM.Server.Services;

public sealed class ItcmAuditService
{
    private readonly ILogger<ItcmAuditService> _logger;

    public ItcmAuditService(ILogger<ItcmAuditService> logger)
    {
        _logger = logger;
    }

    public void Record(HttpContext? context, string action, bool succeeded, string? detail = null)
    {
        var principal = context?.User;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var username = principal?.Identity?.Name ?? "anonymous";
        var remoteIp = context?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation(
            "ITCM_AUDIT Action={Action} Outcome={Outcome} UserId={UserId} User={User} RemoteIp={RemoteIp} Detail={Detail}",
            action,
            succeeded ? "Success" : "Failure",
            userId,
            username,
            remoteIp,
            detail ?? string.Empty);
    }

    public void RecordLogin(string username, bool succeeded, string detail)
    {
        _logger.LogInformation(
            "ITCM_AUDIT Action=Login Outcome={Outcome} User={User} Detail={Detail}",
            succeeded ? "Success" : "Failure",
            username,
            detail);
    }
}
