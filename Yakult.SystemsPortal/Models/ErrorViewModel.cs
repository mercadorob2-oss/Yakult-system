namespace Yakult.SystemsPortal.Models;

public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

    /// <summary>
    /// HTTP error status from the controller. A missing or invalid code defaults to 500.
    /// </summary>
    public int? StatusCode { get; init; }

    public int EffectiveStatusCode => StatusCode is >= 400 and <= 599 ? StatusCode.Value : 500;

    public string Title => EffectiveStatusCode switch
    {
        400 => "We couldn't open this request",
        404 => "Page not found",
        403 => "Access denied",
        401 => "Please sign in to continue",
        429 => "Let's take a short pause",
        502 or 503 => "The portal is temporarily unavailable",
        504 => "This is taking longer than expected",
        _   => "Something went wrong"
    };

    public string Message => EffectiveStatusCode switch
    {
        400 => "The portal couldn't understand the request. The link or information submitted may be incomplete.",
        404 => "We couldn't find the page you're looking for. The link may have changed, or the page may no longer be available.",
        403 => "Your account doesn't have permission to open this page. If you need access for your work, contact IT support.",
        401 => "You need to be signed in to open this page. Your session may have expired.",
        429 => "The portal has received too many requests. Please wait a little before continuing.",
        502 or 503 => "We're having trouble connecting to a service the portal needs. Please try again in a few minutes.",
        504 => "A service didn't respond in time, so we couldn't complete your request.",
        _ => "We couldn't complete your request. Please try again later, or contact IT support if the problem continues."
    };

    public string RecoveryHint => EffectiveStatusCode switch
    {
        400 or 404 => "Check the web address for a typo, or start again from the portal home page.",
        401 => "Sign in with your Yakult portal account, then open the page again.",
        403 => "You can return to the portal to browse the services available to you.",
        429 => "Wait a few moments before opening the page again.",
        _ => "If you were submitting a form, check whether it was saved before sending it again."
    };
}
