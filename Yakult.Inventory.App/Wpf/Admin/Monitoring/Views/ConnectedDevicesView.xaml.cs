using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.Admin.Monitoring.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.Monitoring.Views
{
    /// <summary>
    /// Admin Portal > Connected Devices (WPF). Live presence grid with a
    /// per-row STOP prank button; auto-refreshes every 15 seconds.
    /// </summary>
    public partial class ConnectedDevicesView : UserControl
    {
        private readonly ConnectedDevicesViewModel _vm;
        private readonly DispatcherTimer _refreshTimer;
        private bool _loadedOnce;

        public ConnectedDevicesView()
        {
            InitializeComponent();
            _vm = new ConnectedDevicesViewModel();
            DataContext = _vm;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _refreshTimer.Tick += async (_, __) =>
            {
                try { await _vm.LoadAsync(); }
                catch { }
            };

            Loaded += OnLoaded;
            Unloaded += (_, __) =>
            {
                try { _refreshTimer.Stop(); } catch { }
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loadedOnce) return;
            _loadedOnce = true;
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load connected devices: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            TxtSearch.Focus();
            _refreshTimer.Start();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _vm.StatusMessage = string.Empty;
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load connected devices: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is ConnectedDeviceRow row)) return;

            if (!(Yakult.Inventory.App.Session.AppSession.IsDeveloper
                || Yakult.Inventory.App.Session.AppSession.IsSuperAdmin
                || Yakult.Inventory.App.Session.AppSession.IsAdmin))
            {
                MessageBox.Show("Access denied. Admin privileges required to send alerts.",
                    "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var menu = new ContextMenu();
            AddTemplateItem(menu, "STOP with sound", "Detailed warning dialog + alarm", Repositories.AdminAlertKinds.StopSound, row);
            AddTemplateItem(menu, "STOP silent", "Detailed warning dialog, no sound", Repositories.AdminAlertKinds.StopSilent, row);
            AddTemplateItem(menu, "Fake BSOD", "Fullscreen blue screen prank (Esc dismisses)", Repositories.AdminAlertKinds.Bsod, row);
            AddTemplateItem(menu, "Maintenance notice", "Quiet system-maintenance dialog", Repositories.AdminAlertKinds.Maintenance, row);
            AddTemplateItem(menu, "Screen lock", "Ransomware-style fullscreen lock, unlock with password 1234", Repositories.AdminAlertKinds.ScreenLock, row);
            menu.PlacementTarget = (Button)sender;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void AddTemplateItem(ContextMenu menu, string header, string description, string kind, ConnectedDeviceRow row)
        {
            var item = new MenuItem { Header = header, ToolTip = description };
            item.Click += async (_, __) =>
            {
                var confirm = MessageBox.Show(
                    "Send " + header + " to " + row.MachineName + " (" + row.UserName + ")?",
                    "Send Alert", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.OK) return;
                try
                {
                    await _vm.SendStopAsync(row, kind);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not queue alert: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            menu.Items.Add(item);
        }
    }
}
