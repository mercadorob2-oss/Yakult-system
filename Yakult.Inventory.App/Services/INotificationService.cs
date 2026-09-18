namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Single entry point for showing an in-app notification popup. Production code (the
    /// Request Portal's notification poller, the notification testers) calls this interface
    /// rather than instantiating a toast implementation directly, so the popup mechanism stays
    /// swappable and every caller produces exactly one visible notification.
    /// </summary>
    public interface INotificationService
    {
        void ShowSuccess(string title, string message);
        void ShowInfo(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message);
    }
}
