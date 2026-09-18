using System;
using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.RequestPortal
{
    public partial class NotificationBellControl : UserControl
    {
        // ── Dependency property — bind or set from WinForms host ──────────────
        public static readonly DependencyProperty UnreadCountProperty =
            DependencyProperty.Register(
                nameof(UnreadCount),
                typeof(int),
                typeof(NotificationBellControl),
                new PropertyMetadata(0, OnUnreadCountChanged));

        public int UnreadCount
        {
            get => (int)GetValue(UnreadCountProperty);
            set => SetValue(UnreadCountProperty, value);
        }

        private static void OnUnreadCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl  = (NotificationBellControl)d;
            int count = (int)e.NewValue;

            ctrl.BadgeBorder.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ctrl.BadgeText.Text         = count > 99 ? "99+" : count.ToString();
        }

        // ── IsSilent — shows a diagonal line through the bell when true ──────
        public static readonly DependencyProperty IsSilentProperty =
            DependencyProperty.Register(
                nameof(IsSilent),
                typeof(bool),
                typeof(NotificationBellControl),
                new PropertyMetadata(false, OnIsSilentChanged));

        public bool IsSilent
        {
            get => (bool)GetValue(IsSilentProperty);
            set => SetValue(IsSilentProperty, value);
        }

        private static void OnIsSilentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (NotificationBellControl)d;
            ctrl.SilentLine.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Event raised when the user clicks the bell ────────────────────────
        public event EventHandler BellClicked;

        public NotificationBellControl()
        {
            InitializeComponent();
        }

        private void OnBellClicked(object sender, RoutedEventArgs e)
        {
            BellClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
