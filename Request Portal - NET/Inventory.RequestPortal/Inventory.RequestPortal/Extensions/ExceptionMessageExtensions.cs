namespace Inventory.RequestPortal.Extensions
{
    /// <summary>
    /// Standardizes how caught exceptions are surfaced to end users: detailed message in
    /// Development (to help diagnose issues locally), generic message in every other
    /// environment (so a production stack trace / SQL error text is never shown to a user).
    /// Mirrors the pattern already used correctly in AccountController.Login — this exists
    /// so every other controller's catch block follows the same rule instead of leaking
    /// ex.Message directly.
    /// </summary>
    public static class ExceptionMessageExtensions
    {
        public static string ToUserMessage(this Exception ex, IWebHostEnvironment environment, string genericMessage)
            => environment.IsDevelopment() ? $"{genericMessage}: {ex.Message}" : genericMessage;
    }
}
