using ToastNotifications.Core;

namespace Yakult.Inventory.App.Wpf.Toast
{
    public enum YakultToastKind
    {
        Success,
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// The Request Portal / home dashboard's toast model. Deliberately bypasses the
    /// ToastNotifications.Messages package (SuccessMessage/InformationMessage/etc.) entirely —
    /// their built-in DisplayPart XAML pulls a StaticResource theme dictionary that has to be
    /// merged into Application.Resources by hand in this WinForms-hosted app (fragile — see the
    /// "Cannot find resource named 'InformationIcon'" crash this replaced), and its default
    /// template only takes a single Message string, not a separate Title. Implementing
    /// NotificationBase directly means DisplayPart (YakultToastDisplayPart) is a normal
    /// self-contained WPF UserControl with its own local resources — no external dictionary
    /// dependency, and full control over the design (see design notes on that class).
    /// </summary>
    public sealed class YakultToastNotification : NotificationBase
    {
        public string        Title { get; }
        public YakultToastKind Kind { get; }

        // Must return the SAME instance on every access. The display pipeline (ItemsControl
        // layout passes, DataTemplate re-evaluation, etc.) reads DisplayPart more than once —
        // an uncached "=> new YakultToastDisplayPart(this)" getter silently constructed a
        // different UserControl each time, so the close button wired in one instance's
        // constructor could end up on a control that was never the one actually rendered,
        // leaving the visible toast's X unresponsive.
        private NotificationDisplayPart _displayPart;
        public override NotificationDisplayPart DisplayPart =>
            _displayPart ?? (_displayPart = new YakultToastDisplayPart(this));

        public YakultToastNotification(string title, string message, YakultToastKind kind, MessageOptions options)
            : base(message, options)
        {
            Title = title;
            Kind  = kind;
        }
    }
}
