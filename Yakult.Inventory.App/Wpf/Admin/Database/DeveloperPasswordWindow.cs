using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.Admin.Database
{
    public sealed class DeveloperPasswordWindow : Window
    {
        private readonly IDeveloperConnectionAuthorizer _authorizer;
        private readonly TextBox _username;
        private readonly PasswordBox _password;
        private readonly TextBlock _status;
        private readonly Button _unlock;
        private bool _busy, _closed;
        public DeveloperPasswordWindow(IDeveloperConnectionAuthorizer authorizer)
        {
            _authorizer = authorizer;
            Title = "Developer verification"; Width = 430; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Brushes.White; FontFamily = new FontFamily("Segoe UI");
            var panel = new StackPanel { Margin = new Thickness(26) }; Content = panel;
            panel.Children.Add(new TextBlock { Text = "Unlock connection details", FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(32, 42, 60)) });
            panel.Children.Add(new TextBlock { Text = "An active developer account must approve viewing all connection values, including the SQL password.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 9, 0, 18), FontSize = 12 });
            panel.Children.Add(new TextBlock { Text = "Developer username", Margin = new Thickness(0, 0, 0, 6), FontSize = 12 });
            _username = new TextBox { Text = AppSession.IsDeveloper ? AppSession.CurrentUserName ?? "" : "", Padding = new Thickness(9, 7, 9, 7), FontSize = 13 }; panel.Children.Add(_username);
            panel.Children.Add(new TextBlock { Text = "Developer password", Margin = new Thickness(0, 14, 0, 6), FontSize = 12 });
            _password = new PasswordBox { Padding = new Thickness(9, 7, 9, 7), FontSize = 13 }; panel.Children.Add(_password);
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, FontSize = 11, Margin = new Thickness(0, 12, 0, 14) }; panel.Children.Add(_status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = DatabaseConnectionWindow.MakeButton("Cancel"); cancel.IsCancel = true; cancel.Click += (_, __) => Close(); buttons.Children.Add(cancel);
            _unlock = DatabaseConnectionWindow.MakeButton("Unlock", true); _unlock.IsDefault = true; _unlock.Click += async (_, __) => await UnlockAsync(); buttons.Children.Add(_unlock); panel.Children.Add(buttons);
            Closed += (_, __) => { _closed = true; _password.Clear(); };
            Loaded += (_, __) => { if (string.IsNullOrEmpty(_username.Text)) _username.Focus(); else _password.Focus(); };
        }
        private async Task UnlockAsync()
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(_username.Text) || string.IsNullOrWhiteSpace(_password.Password)) { _status.Text = "Enter the developer username and password."; return; }
            _busy = true; _unlock.IsEnabled = false; _username.IsEnabled = _password.IsEnabled = false; _status.Text = "Verifying developer…";
            var password = _password.Password; _password.Clear();
            try
            {
                var verified = await _authorizer.VerifyAsync(_username.Text, password);
                if (_closed) return;
                if (verified) { DialogResult = true; return; }
                _status.Text = "Developer verification failed. Check the account and password.";
            }
            catch { if (!_closed) _status.Text = "Developer verification is unavailable. Check the default database connection and try again."; }
            finally { password = null; _busy = false; if (!_closed) { _unlock.IsEnabled = true; _username.IsEnabled = _password.IsEnabled = true; _password.Focus(); } }
        }
    }
}
