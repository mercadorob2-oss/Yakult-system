using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfSmtpTestDialog : Window
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly TextBox _ticketIdBox;
        private readonly ComboBox _typeCombo;
        private readonly TextBox _toBox;
        private readonly Button _runButton;
        private readonly TextBox _outputBox;

        public WpfSmtpTestDialog(ICallMonitoringRepository repo, int? defaultTicketId)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Title = "Run SMTP Test";
            Width = 840;
            Height = 560;
            MinWidth = 720;
            MinHeight = 480;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = BrushFromRgb(245, 247, 250);

            var root = new DockPanel();
            Content = root;

            var header = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(20, 16, 20, 14)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            header.Children.Add(new TextBlock
            {
                Text = "SMTP Test",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            header.Children.Add(new TextBlock
            {
                Text = "Sends one email to the override address while still resolving the normal SMTP profile and recipient path.",
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = BrushFromRgb(100, 116, 139)
            });

            var footer = new DockPanel { Background = Brushes.White, Height = 62, LastChildFill = false };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var closeButton = CreateButton("Close", BrushFromRgb(71, 85, 105));
            closeButton.Margin = new Thickness(8, 12, 18, 12);
            closeButton.Click += (_, __) => Close();
            DockPanel.SetDock(closeButton, Dock.Right);
            footer.Children.Add(closeButton);

            _runButton = CreateButton("Send Test Email", BrushFromRgb(22, 163, 74));
            _runButton.Margin = new Thickness(8, 12, 0, 12);
            _runButton.Click += async (_, __) => await RunAsync();
            DockPanel.SetDock(_runButton, Dock.Right);
            footer.Children.Add(_runButton);

            var body = new Grid { Margin = new Thickness(20) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(body);

            var form = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            form.ColumnDefinitions.Add(new ColumnDefinition());
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _ticketIdBox = CreateTextBox();
            _ticketIdBox.Text = defaultTicketId.HasValue && defaultTicketId.Value > 0 ? defaultTicketId.Value.ToString() : "0";
            _typeCombo = CreateCombo();
            _typeCombo.ItemsSource = new[] { "Reminder", "Escalation", "StatusUpdate", "NewTicket" };
            _typeCombo.SelectedIndex = 0;
            _toBox = CreateTextBox();

            AddField(form, 0, "TicketId", _ticketIdBox);
            AddField(form, 1, "Email Type", _typeCombo);
            AddField(form, 2, "Test To", _toBox);

            Grid.SetRow(form, 0);
            body.Children.Add(form);

            _outputBox = CreateTextBox();
            _outputBox.FontFamily = new FontFamily("Consolas");
            _outputBox.FontSize = 12;
            _outputBox.AcceptsReturn = true;
            _outputBox.TextWrapping = TextWrapping.NoWrap;
            _outputBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _outputBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _outputBox.IsReadOnly = true;
            Grid.SetRow(_outputBox, 1);
            body.Children.Add(_outputBox);
        }

        private async Task RunAsync()
        {
            _runButton.IsEnabled = false;
            try
            {
                int.TryParse((_ticketIdBox.Text ?? string.Empty).Trim(), out var ticketId);
                var type = (_typeCombo.SelectedItem ?? "Reminder").ToString();
                var to = (_toBox.Text ?? string.Empty).Trim();

                var svc = new CallEmailNotificationService(_repo);
                var res = await svc.RunSmtpTestAsync(ticketId, type, to);

                var sb = new StringBuilder();
                sb.AppendLine("SMTP Test Result");
                sb.AppendLine("Time (local): " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine("TicketId: " + res.TicketId);
                sb.AppendLine("EmailType: " + res.EmailType);
                sb.AppendLine("TestTo: " + res.TestToEmail);
                sb.AppendLine();

                if (res.SenderResolution != null)
                {
                    var sender = res.SenderResolution.Sender;
                    var auth = sender != null && !string.IsNullOrWhiteSpace(sender.SmtpUsername) ? "Username+Password" : "DefaultCredentials";
                    sb.AppendLine("SMTP source: " + res.SenderResolution.Source);
                    sb.AppendLine(("Profile: " + (res.SenderResolution.ProfileId.HasValue ? res.SenderResolution.ProfileId.Value.ToString() : "") + " " + res.SenderResolution.ProfileName).Trim());
                    if (sender != null)
                    {
                        sb.AppendLine("Host: " + sender.SmtpServer);
                        sb.AppendLine("Port: " + sender.SmtpPort);
                        sb.AppendLine("SSL: " + sender.UseSsl);
                        sb.AppendLine("Auth: " + auth);
                        sb.AppendLine("User: " + (string.IsNullOrWhiteSpace(sender.SmtpUsername) ? "(none)" : "(set)"));
                        sb.AppendLine("From: " + (GetFirstValidEmail(sender.FromEmail, sender.SmtpUsername) ?? "(invalid)"));
                    }
                    sb.AppendLine();
                }

                if (res.RecipientResolution != null)
                {
                    sb.AppendLine("Resolved recipients (normal pipeline):");
                    sb.AppendLine("Final: " + string.Join(", ", res.RecipientResolution.FinalRecipients ?? new List<string>()));
                    sb.AppendLine("GroupEmail: " + (res.RecipientResolution.RulesGroupEmail ?? ""));
                    sb.AppendLine("Dept recipients: " + (res.RecipientResolution.DepartmentRecipients ?? ""));
                    sb.AppendLine("Branch recipients: " + (res.RecipientResolution.BranchRecipients ?? ""));
                    sb.AppendLine();
                }

                sb.AppendLine(res.Sent ? "Result: SENT OK" : "Result: FAILED\r\n" + (res.Error ?? ""));
                _outputBox.Text = sb.ToString().TrimEnd();

                if (!res.Sent && !string.IsNullOrWhiteSpace(res.Error))
                    MessageBox.Show(this, res.Error, "SMTP Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _outputBox.Text = ex.ToString();
                MessageBox.Show(this, ex.Message, "SMTP Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _runButton.IsEnabled = true;
            }
        }

        private static string GetFirstValidEmail(string a, string b)
        {
            foreach (var candidate in new[] { a, b })
            {
                var value = (candidate ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                try { _ = new MailAddress(value); return value; } catch { }
            }
            return null;
        }

        private static void AddField(Grid grid, int row, string label, FrameworkElement control)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, row == 0 ? 0 : 10, 12, 0)
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            control.Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 0);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White
            };
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White
            };
        }

        private static Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                MinWidth = 112,
                Padding = new Thickness(14, 8, 14, 8),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
