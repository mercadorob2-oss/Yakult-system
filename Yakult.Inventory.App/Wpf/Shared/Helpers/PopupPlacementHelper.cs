using System.Windows;
using System.Windows.Controls.Primitives;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>
    /// Attached behavior for "Advanced Filters" popups that have grown tall enough to run past
    /// the bottom of the screen into the taskbar. WPF's own Placement="Bottom" auto-flip is not
    /// reliable for AllowsTransparency="True" popups, so this measures the popup's actual content
    /// on Opened and, if it would extend past SystemParameters.WorkArea (which already excludes
    /// the taskbar), shifts VerticalOffset negative to pull the whole popup upward until its
    /// bottom edge lands exactly on the work-area boundary - overlapping the toggle button/toolbar
    /// row above it if that's what it takes. Covering the taskbar is worse than covering a button.
    /// </summary>
    public static class PopupPlacementHelper
    {
        public static readonly DependencyProperty ClampToWorkAreaProperty =
            DependencyProperty.RegisterAttached(
                "ClampToWorkArea",
                typeof(bool),
                typeof(PopupPlacementHelper),
                new PropertyMetadata(false, OnClampToWorkAreaChanged));

        public static void SetClampToWorkArea(DependencyObject element, bool value) =>
            element.SetValue(ClampToWorkAreaProperty, value);

        public static bool GetClampToWorkArea(DependencyObject element) =>
            (bool)element.GetValue(ClampToWorkAreaProperty);

        private static void OnClampToWorkAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Popup popup && e.NewValue is bool enabled && enabled)
                popup.Opened += Popup_Opened;
        }

        private static void Popup_Opened(object sender, System.EventArgs e)
        {
            var popup = (Popup)sender;
            if (!(popup.PlacementTarget is FrameworkElement target) || popup.Child == null) return;

            // Re-measure so DesiredSize reflects the ScrollViewer's actual capped height, not an
            // earlier/zero measurement from before the popup was shown.
            popup.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double popupHeight = popup.Child.DesiredSize.Height;

            Point targetBottomOnScreen = target.PointToScreen(new Point(0, target.ActualHeight));
            double workAreaBottom = SystemParameters.WorkArea.Bottom;

            double baseOffset = popup.VerticalOffset >= 0 ? 0 : popup.VerticalOffset; // don't compound repeated opens
            double projectedBottom = targetBottomOnScreen.Y + baseOffset + popupHeight;

            popup.VerticalOffset = projectedBottom > workAreaBottom
                ? baseOffset - (projectedBottom - workAreaBottom)
                : baseOffset;
        }
    }
}
