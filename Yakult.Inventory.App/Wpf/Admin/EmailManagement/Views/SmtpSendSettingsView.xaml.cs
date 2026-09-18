using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    public partial class SmtpSendSettingsView : UserControl
    {
        private SmtpSendSettingsViewModel _vm;

        public SmtpSendSettingsView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await RefreshAsync();
        }

        public void Bind(SmtpSendSettingsViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            if (_vm == null) return;
            try
            {
                await _vm.LoadAsync();
                RefreshGlobalToggleUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading SMTP settings:\n\n{ex.Message}", "SMTP Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        // ── Global toggle ────────────────────────────────────────────────────────

        private void RefreshGlobalToggleUi()
        {
            if (_vm.SmtpEnabled)
            {
                LblGlobalStatus.Text = "ON — Outgoing email is active";
                LblGlobalStatus.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                BtnToggleGlobal.Content = "Turn OFF";
                BtnToggleGlobal.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                GlobalToggleCard.Background = new SolidColorBrush(Color.FromRgb(240, 255, 244));
            }
            else
            {
                LblGlobalStatus.Text = "OFF — All outgoing email is suppressed";
                LblGlobalStatus.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
                BtnToggleGlobal.Content = "Turn ON";
                BtnToggleGlobal.Background = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                GlobalToggleCard.Background = new SolidColorBrush(Colors.White);
            }
        }

        private async void BtnToggleGlobal_Click(object sender, RoutedEventArgs e)
        {
            bool newValue = !_vm.SmtpEnabled;
            string action = newValue ? "ENABLE" : "DISABLE";

            var confirm = MessageBox.Show(
                $"Are you sure you want to {action} global SMTP sending?\n\n" +
                (newValue
                    ? "Outgoing emails will be dispatched normally."
                    : "ALL outgoing emails will be suppressed system-wide.\nEmail log entries will still be recorded."),
                "Confirm SMTP Change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            BtnToggleGlobal.IsEnabled = false;
            try
            {
                int userId = Yakult.Inventory.App.Session.AppSession.CurrentUserId;
                await _vm.SetGlobalSmtpEnabledAsync(newValue, userId);
                RefreshGlobalToggleUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save global SMTP setting:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnToggleGlobal.IsEnabled = true;
            }
        }

        // ── Apply profile to templates ───────────────────────────────────────────

        private async void BtnApplySome_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Profiles.Count == 0)
            {
                MessageBox.Show("No SMTP profiles are available. Add a profile first.", "Apply Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? preselectedId = _vm.SelectedGlobalProfile?.ProfileId ?? _vm.Profiles.First().ProfileId;

            var dlg = new ApplyProfileToTemplatesDialog(_vm.Templates.ToList(), _vm.Profiles.ToList(), preselectedId)
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() != true) return;

            BtnApplySome.IsEnabled = false;
            try
            {
                await _vm.ApplyProfileToTemplatesAsync(dlg.SelectedProfile, dlg.SelectedTemplates);
                GridTemplates.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply profile:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnApplySome.IsEnabled = true;
            }
        }

        // ── Turn off some ────────────────────────────────────────────────────────

        private async void BtnTurnOffSome_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TurnOffTemplatesDialog(_vm.Templates.ToList())
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() != true) return;

            BtnTurnOffSome.IsEnabled = false;
            try
            {
                await _vm.TurnOffTemplatesAsync(dlg.SelectedTemplateIds);
                GridTemplates.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to turn off templates:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnTurnOffSome.IsEnabled = true;
            }
        }

        // ── Per-row profile change ───────────────────────────────────────────────

        private async void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is ComboBox combo)) return;
            if (!(combo.DataContext is EmailTemplateDto template)) return;
            if (!(combo.SelectedItem is SystemSmtpProfileDto profile)) return;

            // Ignore the initial selection made while the combo is being generated.
            if (e.RemovedItems.Count == 0) return;

            int? newProfileId = profile.ProfileId == SmtpSendSettingsViewModel.NoneProfileSentinelId
                ? (int?)null
                : profile.ProfileId;
            string newProfileName = newProfileId.HasValue ? profile.ProfileName : null;

            if (newProfileId == template.DefaultSmtpProfileId) return;

            combo.IsEnabled = false;
            try
            {
                await _vm.SetTemplateProfileAsync(template, newProfileId, newProfileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to update profile for '{template.TemplateKey}':\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                combo.IsEnabled = true;
                GridTemplates.Items.Refresh();
            }
        }

        // ── Per-row SMTP Enabled toggle ──────────────────────────────────────────

        private async void ActiveCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb)) return;
            if (!(cb.DataContext is EmailTemplateDto template)) return;

            bool newActive = cb.IsChecked == true;
            cb.IsEnabled = false;
            try
            {
                await _vm.SetTemplateActiveAsync(template, newActive);
            }
            catch (Exception ex)
            {
                template.IsActive = !newActive;
                GridTemplates.Items.Refresh();
                MessageBox.Show(
                    $"Failed to update template '{template.TemplateKey}':\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                cb.IsEnabled = true;
            }
        }
    }
}
