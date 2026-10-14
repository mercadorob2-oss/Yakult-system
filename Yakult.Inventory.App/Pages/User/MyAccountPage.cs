using System;
using System.Drawing;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.User
{
    public sealed class MyAccountPage : UserControl
    {
        private readonly string _connectionString;
        private UserRepository _userRepo;
        private TextBox _txtUsername;
        private TextBox _txtEmail;
        private TextBox _txtEmployee;
        private TextBox _txtRoles;
        private TextBox _txtStatus;
        private Label _lblEmailError;
        private Label _profileMessage;
        private Button _btnSave;
        private Button _btnCancel;
        private Button _btnChangePassword;
        private string _originalEmail;
        private bool _profileLoaded;
        private bool _saving;

        private static readonly Color Navy = ColorTranslator.FromHtml("#182235");
        private static readonly Color Ink = ColorTranslator.FromHtml("#202635");
        private static readonly Color Muted = ColorTranslator.FromHtml("#677081");
        private static readonly Color Accent = ColorTranslator.FromHtml("#B92335");
        private static readonly Color Line = ColorTranslator.FromHtml("#E5E6E8");

        public MyAccountPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            _ = LoadProfileAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 10F);
            BackColor = ColorTranslator.FromHtml("#F6F5F2");
            AutoScaleMode = AutoScaleMode.Font;
            _userRepo = new UserRepository(_connectionString);

            var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Navy, Padding = new Padding(28, 18, 28, 18) };
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Navy };
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            heading.Controls.Add(Caption("My account", 21, Color.White, true), 0, 0);
            heading.Controls.Add(Caption("Manage your contact details and account security.", 10, ColorTranslator.FromHtml("#B8C3D4")), 0, 1);
            header.Controls.Add(heading);

            var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
            var columns = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            columns.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            columns.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var profile = Card("Profile & contact", "Keep your email address up to date.");
            profile.Margin = new Padding(0, 0, 10, 0);
            _txtUsername = ReadOnlyField("Username");
            _txtEmployee = ReadOnlyField("Employee name");
            _txtEmail = new TextBox { Font = new Font("Segoe UI", 11F), BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Ink, Enabled = false, TabIndex = 0, AccessibleName = "Email address" };
            AddField(profile, "Username", _txtUsername);
            AddField(profile, "Employee name", _txtEmployee);
            AddField(profile, "Email address", _txtEmail);
            _lblEmailError = Caption("", 9, Accent);
            _lblEmailError.Visible = false;
            AddToCard(profile, _lblEmailError);
            _profileMessage = Caption("Loading your account…", 9, Muted);
            _profileMessage.Margin = new Padding(0, 4, 0, 12);
            AddToCard(profile, _profileMessage);

            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
            _btnSave = AccountButton("Save changes", true);
            _btnSave.Enabled = false;
            _btnSave.TabIndex = 1;
            _btnSave.Click += async (_, __) => await SaveAsync();
            _btnCancel = AccountButton("Discard changes", false);
            _btnCancel.Enabled = false;
            _btnCancel.TabIndex = 2;
            _btnCancel.Click += (_, __) => { _txtEmail.Text = _originalEmail ?? ""; ValidateEmailInline(); };
            actions.Controls.Add(_btnSave);
            actions.Controls.Add(_btnCancel);
            AddToCard(profile, actions);

            var aside = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, Margin = new Padding(10, 0, 0, 0) };
            var access = Card("Account access", "Your access is managed by your administrator.");
            _txtRoles = ReadOnlyField("Assigned roles");
            _txtStatus = ReadOnlyField("Account status");
            AddField(access, "Assigned roles", _txtRoles);
            AddField(access, "Account status", _txtStatus);
            aside.Controls.Add(access, 0, 0);
            var security = Card("Security", "Update your password using your current password.");
            security.Margin = new Padding(0, 18, 0, 0);
            _btnChangePassword = AccountButton("Change password", false);
            _btnChangePassword.Enabled = false;
            _btnChangePassword.TabIndex = 3;
            _btnChangePassword.Click += (_, __) => OpenChangePasswordDialog();
            AddToCard(security, _btnChangePassword);
            aside.Controls.Add(security, 0, 1);
            columns.Controls.Add(profile, 0, 0);
            columns.Controls.Add(aside, 1, 0);
            content.Controls.Add(columns);
            content.SizeChanged += (_, __) =>
            {
                bool narrow = content.ClientSize.Width < 760;
                columns.ColumnStyles[0].Width = narrow ? 100 : 58;
                columns.ColumnStyles[1].Width = narrow ? 0 : 42;
                columns.SetCellPosition(aside, new TableLayoutPanelCellPosition(narrow ? 0 : 1, narrow ? 1 : 0));
                profile.Margin = narrow ? Padding.Empty : new Padding(0, 0, 10, 0);
                aside.Margin = narrow ? new Padding(0, 18, 0, 0) : new Padding(10, 0, 0, 0);
            };
            Controls.Add(content);
            Controls.Add(header);
            _txtEmail.TextChanged += (_, __) => ValidateEmailInline();
            ResumeLayout(true);
        }

        private static Label Caption(string text, float size, Color color, bool bold = false)
        {
            return new Label { Text = text, AutoSize = true, Dock = DockStyle.Top, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = Padding.Empty, UseMnemonic = false };
        }

        private static TableLayoutPanel Card(string title, string description)
        {
            var card = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, Padding = new Padding(20), Margin = Padding.Empty, BackColor = Color.White };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Paint += (_, e) => { using (var pen = new Pen(Line)) e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1); };
            var heading = Caption(title, 13, Ink, true);
            heading.Margin = new Padding(0, 0, 0, 7);
            AddToCard(card, heading);
            var subtitle = Caption(description, 9, Muted);
            subtitle.Margin = new Padding(0, 0, 0, 18);
            AddToCard(card, subtitle);
            return card;
        }

        private static void AddToCard(TableLayoutPanel card, Control control)
        {
            int row = card.RowCount++;
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(control, 0, row);
        }

        private static void AddField(TableLayoutPanel card, string label, TextBox field)
        {
            var caption = Caption(label, 9, Muted, true);
            caption.Margin = new Padding(0, 0, 0, 6);
            AddToCard(card, caption);
            var wrapper = new Panel { Dock = DockStyle.Top, Height = 39, Padding = new Padding(10, 8, 10, 7), Margin = new Padding(0, 0, 0, 16), BackColor = field.ReadOnly ? ColorTranslator.FromHtml("#F5F6F8") : Color.White };
            field.BackColor = wrapper.BackColor;
            wrapper.Controls.Add(field);
            wrapper.Paint += (_, e) => { using (var pen = new Pen(field.Focused ? Accent : Line)) e.Graphics.DrawRectangle(pen, 0, 0, wrapper.Width - 1, wrapper.Height - 1); };
            field.Enter += (_, __) => wrapper.Invalidate();
            field.Leave += (_, __) => wrapper.Invalidate();
            AddToCard(card, wrapper);
        }

        private static TextBox ReadOnlyField(string name)
        {
            return new TextBox { ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10F), ForeColor = Ink, Text = "—", TabStop = false, AccessibleName = name };
        }

        private static Button AccountButton(string text, bool primary)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(132, 40), Padding = new Padding(10, 6, 10, 6), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, primary ? FontStyle.Bold : FontStyle.Regular), BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, UseVisualStyleBackColor = false, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0) };
            button.FlatAppearance.BorderColor = primary ? Accent : Line;
            button.FlatAppearance.MouseOverBackColor = primary ? ColorTranslator.FromHtml("#9E1E2E") : ColorTranslator.FromHtml("#FCF0F1");
            button.EnabledChanged += (_, __) =>
            {
                button.BackColor = button.Enabled ? (primary ? Accent : Color.White) : ColorTranslator.FromHtml("#F0F1F3");
                button.FlatAppearance.BorderColor = button.Enabled && primary ? Accent : Line;
                button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
            };
            return button;
        }

        private async Task LoadProfileAsync()
        {
            try
            {
                if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
                {
                    _profileMessage.Text = "Sign in to view your account details.";
                    return;
                }

                var account = await _userRepo.GetUserAccountByUserIdAsync(AppSession.CurrentUserId);
                if (IsDisposed || Disposing) return;
                if (account == null)
                {
                    _profileMessage.Text = "Account details are unavailable. Please contact IT support.";
                    return;
                }

                _txtUsername.Text = account.Name;
                _txtEmail.Text = account.EmailAddress;
                _txtEmployee.Text = string.IsNullOrWhiteSpace(account.EmployeeName) ? "Not linked" : account.EmployeeName;

                var rolesText = (account.Roles != null && account.Roles.Count > 0)
                    ? string.Join(", ", account.Roles)
                    : "Requester";
                if (account.IsDeveloper && !rolesText.Split(',').Any(r => r.Trim().Equals("Developer", StringComparison.OrdinalIgnoreCase)))
                    rolesText = rolesText.Length == 0 ? "Developer" : rolesText + ", Developer";

                _txtRoles.Text = rolesText;
                _txtStatus.Text = account.IsActive ? "Active" : "Disabled";

                _originalEmail = account.EmailAddress;
                _profileLoaded = true;
                _txtEmail.Enabled = true;
                _btnChangePassword.Enabled = true;
                _profileMessage.Text = "Your email is used for account notifications.";
                ValidateEmailInline();
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                Logger.LogError("[MyAccount] Failed to load account details.", ex);
                _profileMessage.Text = "Unable to load your account. Close this page and try again.";
            }
        }

        private void ValidateEmailInline()
        {
            if (!_profileLoaded || _saving)
            {
                _btnSave.Enabled = false;
                _btnCancel.Enabled = false;
                return;
            }
            string email = _txtEmail.Text?.Trim() ?? string.Empty;
            _btnCancel.Enabled = !string.Equals(email, _originalEmail ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            if (email.Length == 0)
            {
                _lblEmailError.Text = string.Empty;
                _lblEmailError.Visible = false;
                _btnSave.Enabled = false;
                return;
            }

            if (!IsValidEmail(email))
            {
                _lblEmailError.Text = "Invalid email format.";
                _lblEmailError.Visible = true;
                _btnSave.Enabled = false;
                return;
            }

            _lblEmailError.Text = string.Empty;
            _lblEmailError.Visible = false;
            _btnSave.Enabled = !string.Equals(email, _originalEmail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task SaveAsync()
        {
            if (!_profileLoaded || _saving) return;
            string email = _txtEmail.Text?.Trim() ?? string.Empty;

            if (email.Length == 0 || !IsValidEmail(email))
            {
                ValidateEmailInline();
                _txtEmail.Focus();
                return;
            }

            try
            {
                _saving = true;
                _btnSave.Enabled = false;
                _btnSave.Text = "Saving…";
                _btnCancel.Enabled = false;
                _txtEmail.Enabled = false;

                bool updated = await _userRepo.UpdateUserEmailAsync(AppSession.CurrentUserId, email);
                if (IsDisposed || Disposing) return;
                if (updated)
                {
                    AppSession.CurrentEmail = email;
                    _originalEmail = email;
                    ValidateEmailInline();

                    _profileMessage.Text = "Your email address has been updated.";
                }
                else
                {
                    _profileMessage.Text = "No changes were saved. Please try again.";
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                Logger.LogError("[MyAccount] Failed to save account details.", ex);
                _profileMessage.Text = "Unable to save your email. Please try again.";
            }
            finally
            {
                _saving = false;
                if (!IsDisposed && !Disposing)
                {
                    _btnSave.Text = "Save changes";
                    _txtEmail.Enabled = _profileLoaded;
                    ValidateEmailInline();
                }
            }
        }

        private void OpenChangePasswordDialog()
        {
            try
            {
                using (var dlg = new ChangePasswordDialog(_userRepo))
                {
                    dlg.ShowDialog(FindForm());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Change Password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
