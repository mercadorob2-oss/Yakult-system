using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
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

        private sealed class AlertOption
        {
            public string Label { get; set; }
            public string Kind { get; set; }
            public string Description { get; set; }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
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

            if (!row.Online)
            {
                MessageBox.Show("Alerts can only be sent to an online device.", "Device offline",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowAlertComposer(row);
        }

        private void ShowAlertComposer(ConnectedDeviceRow row)
        {
            var dialog = new Window
            {
                Title = "Send device alert", Owner = Window.GetWindow(this), Width = 560, Height = 700,
                MinWidth = 480, MinHeight = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White, FontFamily = new FontFamily("Segoe UI"), ResizeMode = ResizeMode.CanResize
            };
            var root = new DockPanel { Margin = new Thickness(24) };
            dialog.Content = root;
            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var status = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            footer.Children.Add(status);
            var footerButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            footerButtons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 8, 0) });
            var send = new Button { Content = "Queue alert", Padding = new Thickness(18, 8, 18, 8), Style = (Style)FindResource("ActionButton"), Height = 36 };
            footerButtons.Children.Add(send);
            footer.Children.Add(footerButtons);

            var form = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            form.Children.Add(new TextBlock { Text = "Send an alert", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("YakultPrimaryBrush") });
            form.Children.Add(new TextBlock { Text = "To: " + row.MachineName + " • " + row.UserName, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 16) });
            form.Children.Add(new TextBlock { Text = "Alert type", FontWeight = FontWeights.SemiBold });

            var options = new List<AlertOption>
            {
                new AlertOption { Label = "STOP with sound", Kind = Repositories.AdminAlertKinds.StopSound, Description = "Warning dialog with alarm sound." },
                new AlertOption { Label = "STOP silent", Kind = Repositories.AdminAlertKinds.StopSilent, Description = "Warning dialog without sound." },
                new AlertOption { Label = "Fake BSOD", Kind = Repositories.AdminAlertKinds.Bsod, Description = "Fullscreen blue screen imitation; Esc or click dismisses it." },
                new AlertOption { Label = "Maintenance notice", Kind = Repositories.AdminAlertKinds.Maintenance, Description = "A quiet information dialog." },
                new AlertOption { Label = "Screen lock", Kind = Repositories.AdminAlertKinds.ScreenLock, Description = "Existing fullscreen lock alert." },
                new AlertOption { Label = "Full-screen custom image", Kind = Repositories.AdminAlertKinds.CustomImage, Description = "Shows your image fullscreen. Esc, click, or the close button dismisses it." }
            };
            var choice = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedIndex = 0, Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 6) };
            form.Children.Add(choice);
            var description = new TextBlock { Foreground = (Brush)FindResource("YakultTextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
            form.Children.Add(description);
            form.Children.Add(new TextBlock { Text = "Title (optional)", FontWeight = FontWeights.SemiBold });
            var title = new TextBox { Text = "STOP", MaxLength = 100, Padding = new Thickness(9), Margin = new Thickness(0, 5, 0, 12) };
            form.Children.Add(title);
            form.Children.Add(new TextBlock { Text = "Message (optional)", FontWeight = FontWeights.SemiBold });
            var message = new TextBox { MaxLength = 500, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 105, Padding = new Thickness(9), Margin = new Thickness(0, 5, 0, 12) };
            form.Children.Add(message);

            byte[] imageData = null;
            var imagePanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 2, 0, 12) };
            var chooseImage = new Button { Content = "Choose PNG or JPEG…", Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left };
            var imageName = new TextBlock { Text = "No image selected (8 MB maximum).", Foreground = (Brush)FindResource("YakultTextMutedBrush"), Margin = new Thickness(0, 7, 0, 7), TextWrapping = TextWrapping.Wrap };
            var imagePreview = new Image { Height = 220, Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
            imagePanel.Children.Add(chooseImage);
            imagePanel.Children.Add(imageName);
            imagePanel.Children.Add(new Border { BorderBrush = (Brush)FindResource("YakultBorderBrush"), BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = imagePreview });
            form.Children.Add(imagePanel);
            form.Children.Add(new TextBlock
            {
                Text = "Alerts are sent to the selected signed-in device and expire after 10 minutes. Full-screen image alerts close automatically after two minutes.",
                FontSize = 11, Foreground = (Brush)FindResource("YakultTextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
            });

            choice.SelectionChanged += (_, __) =>
            {
                var selected = choice.SelectedItem as AlertOption;
                if (selected == null) return;
                description.Text = selected.Description;
                imagePanel.Visibility = selected.Kind == Repositories.AdminAlertKinds.CustomImage ? Visibility.Visible : Visibility.Collapsed;
                if (selected.Kind == Repositories.AdminAlertKinds.Maintenance)
                {
                    title.Text = "System Under Maintenance";
                    message.Text = "This workstation has been flagged for scheduled maintenance by IT. Please save your work.";
                }
                else if (selected.Kind == Repositories.AdminAlertKinds.CustomImage)
                {
                    title.Text = "Custom image popup";
                    message.Text = string.Empty;
                }
                else
                {
                    title.Text = "STOP";
                    message.Text = string.Empty;
                }
            };

            chooseImage.Click += (_, __) =>
            {
                var picker = new OpenFileDialog { Title = "Choose an image for the fullscreen popup", Filter = "PNG or JPEG images|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
                if (picker.ShowDialog(dialog) != true) return;
                try
                {
                    var info = new FileInfo(picker.FileName);
                    if (info.Length < 1 || info.Length > 8 * 1024 * 1024) throw new InvalidDataException("Choose an image no larger than 8 MB.");
                    var bytes = File.ReadAllBytes(picker.FileName);
                    BitmapImage bitmap;
                    using (var stream = new MemoryStream(bytes))
                    {
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 640;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    imageData = bytes;
                    imagePreview.Source = bitmap;
                    imagePreview.Visibility = Visibility.Visible;
                    imageName.Text = Path.GetFileName(picker.FileName) + " • " + (info.Length / 1024.0).ToString("0") + " KB";
                    status.Text = string.Empty;
                }
                catch (Exception ex)
                {
                    imageData = null;
                    imagePreview.Source = null;
                    imagePreview.Visibility = Visibility.Collapsed;
                    imageName.Text = "No image selected (8 MB maximum).";
                    status.Text = "That image could not be opened: " + ex.Message;
                }
            };

            send.Click += async (_, __) =>
            {
                var selected = choice.SelectedItem as AlertOption;
                if (selected == null) { status.Text = "Choose an alert type."; return; }
                if (selected.Kind == Repositories.AdminAlertKinds.CustomImage && imageData == null)
                {
                    status.Text = "Choose a PNG or JPEG image before sending.";
                    return;
                }
                var confirm = MessageBox.Show("Send " + selected.Label + " to " + row.MachineName + " (" + row.UserName + ")?",
                    "Confirm alert", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.OK) return;
                send.IsEnabled = false;
                send.Content = "Queuing…";
                try
                {
                    await _vm.SendAlertAsync(row, selected.Kind, title.Text, message.Text, imageData);
                    dialog.DialogResult = true;
                }
                catch (Exception ex)
                {
                    status.Text = ex.Message;
                    send.IsEnabled = true;
                    send.Content = "Queue alert";
                }
            };

            description.Text = options[0].Description;
            choice.SelectedIndex = 0;
            dialog.ShowDialog();
        }
    }
}
