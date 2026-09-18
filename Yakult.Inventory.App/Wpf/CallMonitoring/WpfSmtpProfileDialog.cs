using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Native WPF editor for a reusable SMTP sender profile
    /// (dbo.CallSmtpProfile) with optional department / branch routing links.
    /// Blank password keeps the saved one. Links are set-only: picking
    /// "(No change)" leaves existing routing untouched (there is no
    /// unlink API — disable the profile instead to stop using it).
    /// </summary>
    public sealed class WpfSmtpProfileDialog : Window
    {
        private readonly ICallMonitoringRepository _repository;
        private readonly int? _currentUserId;
        private readonly CallSmtpProfileItem _existing;

        private readonly TextBox _txtName;
        private readonly TextBox _txtServer;
        private readonly TextBox _txtPort;
        private readonly CheckBox _chkSsl;
        private readonly TextBox _txtUsername;
        private readonly PasswordBox _txtPassword;
        private readonly TextBox _txtFromName;
        private readonly TextBox _txtFromEmail;
        private readonly CheckBox _chkActive;
        private readonly ComboBox _cboDept;
        private readonly ComboBox _cboBranch;

        public bool Saved { get; private set; }

        public WpfSmtpProfileDialog(
            ICallMonitoringRepository repository,
            IList<LookupItem> departments,
            IList<LookupItem> branches,
            CallSmtpProfileItem existingOrNull,
            int? currentUserId)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _currentUserId = currentUserId;
            _existing = existingOrNull;

            var isNew = _existing == null || _existing.ProfileId <= 0;
            Title = isNew ? "New SMTP Profile" : "Edit SMTP Profile";
            Width = 620;
            Height = 640;
            MinWidth = 520;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            FontFamily = new FontFamily("Segoe UI");

            var root = new DockPanel { LastChildFill = true };
            Content = root;

            var header = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var headerStack = new StackPanel();
            header.Child = headerStack;
            headerStack.Children.Add(new TextBlock
            {
                Text = (string)Title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Sender identity for department and branch routing. Leave password blank to keep the saved one.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            var footer = new DockPanel { Background = Brushes.White, LastChildFill = false, Height = 60 };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var saveBtn = new Button
            {
                Content = "Save",
                MinWidth = 110,
                Height = 34,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            saveBtn.Click += async (_, __) => await SaveAsync(saveBtn);
            var cancelBtn = new Button
            {
                Content = "Cancel",
                MinWidth = 100,
                Height = 34,
                Margin = new Thickness(0, 0, 16, 0),
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                IsCancel = true
            };
            var footerActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            footerActions.Children.Add(saveBtn);
            footerActions.Children.Add(cancelBtn);
            DockPanel.SetDock(footerActions, Dock.Right);
            footer.Children.Add(footerActions);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(24, 16, 24, 16) };
            root.Children.Add(scroll);
            var form = new Grid();
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scroll.Content = form;

            var row = 0;
            _txtName = AddRow(form, row++, "Profile name", new TextBox());
            _txtServer = AddRow(form, row++, "SMTP server", new TextBox());
            _txtPort = AddRow(form, row++, "Port", new TextBox());
            _chkSsl = new CheckBox { Content = "Use SSL", VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
            AddControl(form, row++, "Security", _chkSsl);
            _txtUsername = AddRow(form, row++, "Username", new TextBox());
            _txtPassword = new PasswordBox { MinHeight = 30, Padding = new Thickness(8, 6, 8, 6) };
            AddControl(form, row++, "Password", _txtPassword);
            _txtFromName = AddRow(form, row++, "From name", new TextBox());
            _txtFromEmail = AddRow(form, row++, "From email", new TextBox());
            _chkActive = new CheckBox { Content = "Active", VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
            AddControl(form, row++, "Status", _chkActive);
            _cboDept = new ComboBox { MinHeight = 30, Margin = new Thickness(0, 8, 0, 8) };
            FillLinkCombo(_cboDept, departments);
            AddControl(form, row++, "Link department", _cboDept);
            _cboBranch = new ComboBox { MinHeight = 30, Margin = new Thickness(0, 8, 0, 8) };
            FillLinkCombo(_cboBranch, branches);
            AddControl(form, row++, "Link branch", _cboBranch);

            if (!isNew)
            {
                _txtName.Text = _existing.ProfileName ?? string.Empty;
                _txtServer.Text = _existing.SmtpServer ?? string.Empty;
                _txtPort.Text = _existing.SmtpPort > 0 ? _existing.SmtpPort.ToString() : "587";
                _chkSsl.IsChecked = _existing.UseSsl;
                _txtUsername.Text = _existing.SmtpUsername ?? string.Empty;
                _txtFromName.Text = _existing.FromName ?? string.Empty;
                _txtFromEmail.Text = _existing.FromEmail ?? string.Empty;
                _chkActive.IsChecked = _existing.IsActive;
            }
            else
            {
                _txtPort.Text = "587";
                _chkSsl.IsChecked = true;
                _chkActive.IsChecked = true;
            }
        }

        private static TextBox AddRow(Grid form, int row, string label, TextBox box)
        {
            box.MinHeight = 30;
            box.Padding = new Thickness(8, 6, 8, 6);
            box.VerticalContentAlignment = VerticalAlignment.Center;
            AddControl(form, row, label, box);
            return box;
        }

        private static void AddControl(Grid form, int row, string label, FrameworkElement control)
        {
            while (form.RowDefinitions.Count <= row)
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 8, 12, 8)
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            form.Children.Add(labelBlock);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            form.Children.Add(control);
        }

        private static void FillLinkCombo(ComboBox combo, IList<LookupItem> targets)
        {
            combo.Items.Add(new LookupItem { Id = 0, Name = "(No change)", DisplayName = "(No change)" });
            foreach (var t in (targets ?? new List<LookupItem>()).OrderBy(x => x.DisplayName ?? x.Name))
                combo.Items.Add(t);
            combo.SelectedIndex = 0;
        }

        private static int SelectedLinkId(ComboBox combo)
        {
            return (combo?.SelectedItem as LookupItem)?.Id ?? 0;
        }

        private async Task SaveAsync(Button saveBtn)
        {
            var name = (_txtName.Text ?? string.Empty).Trim();
            var server = (_txtServer.Text ?? string.Empty).Trim();
            var username = (_txtUsername.Text ?? string.Empty).Trim();
            var fromName = (_txtFromName.Text ?? string.Empty).Trim();
            var fromEmail = (_txtFromEmail.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Profile name is required.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show("SMTP server is required.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse((_txtPort.Text ?? string.Empty).Trim(), out var port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Port must be a valid number (1-65535).", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!string.IsNullOrWhiteSpace(fromEmail) && !EmailAddressValidator.TryNormalize(fromEmail, out fromEmail))
            {
                MessageBox.Show("From email must be a valid email address.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var typedPassword = _txtPassword.Password ?? string.Empty;
            byte[] passwordEnc = null;
            if (!string.IsNullOrWhiteSpace(typedPassword))
                passwordEnc = SecretProtector.ProtectString(typedPassword);
            else if (_existing?.SmtpPasswordEnc != null && _existing.SmtpPasswordEnc.Length > 0)
                passwordEnc = _existing.SmtpPasswordEnc;

            saveBtn.IsEnabled = false;
            try
            {
                var profileId = await _repository.UpsertSmtpProfileAsync(new CallSmtpProfileItem
                {
                    ProfileId = _existing?.ProfileId ?? 0,
                    ProfileName = name,
                    SmtpServer = server,
                    SmtpPort = port,
                    UseSsl = _chkSsl.IsChecked == true,
                    SmtpUsername = username,
                    SmtpPasswordEnc = passwordEnc,
                    FromName = fromName,
                    FromEmail = fromEmail,
                    IsActive = _chkActive.IsChecked != false,
                    UpdatedByUserId = _currentUserId
                });

                var deptId = SelectedLinkId(_cboDept);
                if (deptId > 0)
                    await _repository.UpsertDepartmentSmtpProfileLinkAsync(deptId, profileId, _currentUserId);

                var branchId = SelectedLinkId(_cboBranch);
                if (branchId > 0)
                    await _repository.UpsertBranchSmtpProfileLinkAsync(branchId, profileId, _currentUserId);

                Saved = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                saveBtn.IsEnabled = true;
            }
        }
    }
}
