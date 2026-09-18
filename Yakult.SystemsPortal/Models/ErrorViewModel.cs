namespace Yakult.SystemsPortal.Models;

public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

    /// <summary>
    /// HTTP status code, when surfaced via <c>UseStatusCodePagesWithReExecute</c>.
    /// Null when reached through the unhandled-exception pipeline.
    /// </summary>
    public int? StatusCode { get; init; }

    public string Title => StatusCode switch
    {
        404 => "Page not found",
        403 => "Access denied",
        401 => "Authentication required",
        500 => "Server error",
        _   => "Something went wrong"
    };

    public string Message => StatusCode switch
    {
        404 => "The page you requested could not be located.",
        403 => "You do not have permission to view this resource.",
        401 => "Please sign in to continue.",
        500 => "The portal could not complete your request.",
        _   => "The portal could not complete your request."
    };
}
