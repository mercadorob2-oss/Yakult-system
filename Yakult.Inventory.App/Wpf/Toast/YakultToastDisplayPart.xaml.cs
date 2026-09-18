using System.Windows.Media;
using ToastNotifications.Core;

namespace Yakult.Inventory.App.Wpf.Toast
{
    /// <summary>
    /// Real WPF-designed toast card: white background, black/dark-slate text, rounded corners,
    /// drop shadow, and a colored icon circle per severity — replacing both the earlier
    /// ToastNotifications.Messages default template (a flat, edge-to-edge blue block with no
    /// spacing between stacked toasts) and, before that, WindowsToastService's native balloon.
    /// Colors match the CallMonitoring feature's existing ToastNotification.cs palette so the
    /// app's various toast surfaces read as one visual language.
    ///
    /// UseLayoutRounding/SnapsToDevicePixels/TextOptions in the XAML root mitigate the blur that
    /// otherwise shows under this app's process-wide RenderOptions.ProcessRenderMode =
    /// SoftwareOnly (see Program.cs — required for MediaElement stability, not something this
    /// control can or should override); they don't need hardware rendering to take effect.
    /// </summary>
    public partial class YakultToastDisplayPart : NotificationDisplayPart
    {
        public YakultToastDisplayPart(YakultToastNotification notification)
        {
            InitializeComponent();
            Bind(notification);

            TitleBlock.Text   = notification.Title;
            MessageBlock.Text = notification.Message;

            ApplyKindStyle(notification.Kind);

            CloseButton.Click += (s, e) => Notification.Close();
        }

        private void ApplyKindStyle(YakultToastKind kind)
        {
            Color bg, fg;
            string glyph;

            switch (kind)
            {
                case YakultToastKind.Success:
                    bg = Color.FromRgb(0xEC, 0xFD, 0xF5); fg = Color.FromRgb(0x10, 0xB9, 0x81); glyph = "✓";
                    break;
                case YakultToastKind.Warning:
                    bg = Color.FromRgb(0xFF, 0xFB, 0xEB); fg = Color.FromRgb(0xF5, 0x9E, 0x0B); glyph = "!";
                    break;
                case YakultToastKind.Error:
                    bg = Color.FromRgb(0xFE, 0xF2, 0xF2); fg = Color.FromRgb(0xEF, 0x44, 0x44); glyph = "✕";
                    break;
                default: // Info
                    bg = Color.FromRgb(0xEF, 0xF6, 0xFF); fg = Color.FromRgb(0x3B, 0x82, 0xF6); glyph = "ℹ";
                    break;
            }

            IconCircle.Fill    = new SolidColorBrush(bg);
            IconGlyph.Text     = glyph;
            IconGlyph.Foreground = new SolidColorBrush(fg);
        }
    }
}
