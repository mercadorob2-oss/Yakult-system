using System;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Wpf.Notifications
{
    public partial class NotificationSettingsWindow : Window
    {
        private static readonly SolidColorBrush BrushBlue  = new SolidColorBrush(Color.FromRgb(78, 154, 252));
        private static readonly SolidColorBrush BrushGray  = new SolidColorBrush(Color.FromRgb(150, 150, 168));
        private static readonly SolidColorBrush BrushGreen = new SolidColorBrush(Color.FromRgb(30, 140, 60));
        private static readonly SolidColorBrush BrushRed   = new SolidColorBrush(Color.FromRgb(213, 0, 50));

        private readonly UserNotificationSettingRepository _settingRepo;
        private readonly NotificationRepository            _notifRepo;

        /// <summary>Raised immediately after the user saves, so the caller can refresh its UI.</summary>
        public event Action SettingsSaved;

        public NotificationSettingsWindow()
        {
            InitializeComponent();
            _settingRepo = new UserNotificationSettingRepository();
            _notifRepo   = new NotificationRepository();

            // Subscribe AFTER InitializeComponent so all named elements exist
            toggleSwitch.Checked   += Toggle_Changed;
            toggleSwitch.Unchecked += Toggle_Changed;

            bool enabled = !AppSession.IsLoggedIn || AppSession.NotificationsEnabled;
            toggleSwitch.IsChecked = enabled;
            UpdateToggleLabel(enabled);
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateToggleLabel(toggleSwitch.IsChecked == true);
        }

        private void UpdateToggleLabel(bool enabled)
        {
            lblToggleState.Text       = enabled ? "Active" : "Silent";
            lblToggleState.Foreground = enabled ? BrushBlue : BrushGray;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.IsLoggedIn) return;

            bool enabled = toggleSwitch.IsChecked == true;
            _settingRepo.Upsert(AppSession.CurrentUserId, enabled);
            AppSession.NotificationsEnabled = enabled;

            SetStatus(enabled ? "✓  Notifications active." : "✓  Notifications silenced.", isError: false);

            // Notify the caller (RequesterPortalForm) to refresh the bell immediately
            SettingsSaved?.Invoke();
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.IsLoggedIn) return;

            if (toggleSwitch.IsChecked != true)
            {
                SetStatus("ℹ  Enable notifications first, then test.", isError: false);
                return;
            }

            _notifRepo.Create(new NotificationCreateDto
            {
                UserId           = AppSession.CurrentUserId,
                Title            = "Test Notification",
                Message          = "This is a test notification. Your notifications are working correctly.",
                NotificationType = NotificationType.Test,
                ReferenceId      = null
            });

            ToastNotificationService.Instance.ShowInfo("Test Notification", "Your notifications are working correctly.");
            SetStatus("✓  Test sent — check your notification bell.", isError: false);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SetStatus(string text, bool isError)
        {
            lblStatus.Text       = text;
            lblStatus.Foreground = isError ? BrushRed : BrushGreen;
        }
    }
}
