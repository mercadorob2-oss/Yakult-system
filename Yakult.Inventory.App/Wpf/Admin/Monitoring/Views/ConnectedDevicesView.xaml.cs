using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.Admin.Monitoring.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.Monitoring.Views
{
    /// <summary>
    /// Admin Portal > Connected Devices (WPF). Live presence grid with a
    /// per-device message composer and alert menu; auto-refreshes every 15 seconds.
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

        private void MessageButton_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).DataContext as ConnectedDeviceRow;
            if (row == null) return;
            if (!(Session.AppSession.IsDeveloper || Session.AppSession.IsSuperAdmin || Session.AppSession.IsAdmin))
            {
                MessageBox.Show("Admin privileges are required to send device messages.", "Send notification",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new Window
            {
                Title = "Send device notification", Owner = Window.GetWindow(this),
                Width = 580, Height = 670, MinWidth = 460, MinHeight = 540,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.White, FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };
            var root = new DockPanel { Margin = new Thickness(24) };
            dialog.Content = root;
            var footer = new StackPanel { Margin = new Thickness(0,16,0,0) };
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick, Margin = new Thickness(0,0,0,8) };
            footer.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(18,8,18,8), Margin = new Thickness(0,0,8,0) };
            var send = new Button { Content = "Send to device", Padding = new Thickness(18,8,18,8), Style = (Style)FindResource("ActionButton"), Height = 36 };
            buttons.Children.Add(cancel); buttons.Children.Add(send); footer.Children.Add(buttons);
            var form = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            form.Children.Add(new TextBlock { Text = "Send a notification", FontSize = 22, FontWeight = FontWeights.Bold });
            form.Children.Add(new TextBlock { Text = "To: " + row.MachineName + " • " + row.UserName, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,18) });
            form.Children.Add(new TextBlock { Text = "Title", FontWeight = FontWeights.SemiBold });
            var title = new TextBox { Text = "System update available", MaxLength = 100, Padding = new Thickness(10), Margin = new Thickness(0,6,0,14) };
            form.Children.Add(title);
            form.Children.Add(new TextBlock { Text = "Message", FontWeight = FontWeights.SemiBold });
            var message = new TextBox
            {
                Text = "Hello, good day!\r\n\r\nThere is an update available for the Yakult system. Please save your work and restart the application when convenient.\r\n\r\nThank you!",
                MaxLength = 500, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 140,
                Padding = new Thickness(10), Margin = new Thickness(0,6,0,4)
            };
            form.Children.Add(message);
            var count = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, FontSize = 11 };
            form.Children.Add(count);
            form.Children.Add(new TextBlock { Text = "Recipient preview", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,14,0,8) });
            var previewTitle = new TextBlock { FontSize = 17, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            var previewMessage = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,10,0,0) };
            var preview = new StackPanel(); preview.Children.Add(previewTitle); preview.Children.Add(previewMessage);
            form.Children.Add(new Border { Child = preview, Padding = new Thickness(16), Background = System.Windows.Media.Brushes.AliceBlue, CornerRadius = new CornerRadius(8) });
            form.Children.Add(new TextBlock { Text = "Appears as a quiet notice in the open desktop app. Queued notices expire after 10 minutes. Updated clients display the custom title; older clients may show a maintenance heading.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,0) });
            Action updatePreview = () =>
            {
                previewTitle.Text = title.Text; previewMessage.Text = message.Text;
                count.Text = message.Text.Length + " / 500 characters";
                send.IsEnabled = !string.IsNullOrWhiteSpace(title.Text) && !string.IsNullOrWhiteSpace(message.Text);
            };
            title.TextChanged += (_,__) => updatePreview();
            message.TextChanged += (_,__) => updatePreview();
            updatePreview();
            var sending = false;
            dialog.Closing += (_, args) => { if (sending) args.Cancel = true; };
            send.Click += async (_,__) =>
            {
                if (sending) return;
                sending = true; send.IsEnabled = false; cancel.IsEnabled = false;
                title.IsEnabled = false; message.IsEnabled = false; send.Content = "Queuing…";
                try
                {
                    await _vm.SendMessageAsync(row, title.Text, message.Text);
                    sending = false; dialog.DialogResult = true;
                }
                catch (Exception ex) { status.Text = ex.Message; }
                finally
                {
                    sending = false; cancel.IsEnabled = true; title.IsEnabled = true; message.IsEnabled = true;
                    send.Content = "Send to device"; updatePreview();
                }
            };
            dialog.ShowDialog();
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
