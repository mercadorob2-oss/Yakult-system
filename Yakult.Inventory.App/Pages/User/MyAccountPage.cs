using System;
using System.Drawing;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.User
{
    public sealed class MyAccountPage : UserControl
    {
        private readonly string _connectionString;
        private UserRepository _userRepo;

        private System.Windows.Forms.Panel _header;
        private Label _title;

        private MaterialCard _card;
        private TableLayoutPanel _table;

        private TextBox _txtUsername;
        private TextBox _txtEmail;
        private TextBox _txtEmployee;
        private TextBox _txtRoles;
        private TextBox _txtStatus;

        private Label _lblEmailError;

        private HopeButton _btnSave;
        private HopeButton _btnCancel;
        private HopeButton _btnChangePassword;

        private string _originalEmail;

        public MyAccountPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            this.AutoScaleMode = AutoScaleMode.Font;
            _ = LoadProfileAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            this.AutoScaleMode = AutoScaleMode.Font;

            _userRepo = new UserRepository(_connectionString);

            var themeGreen = Color.FromArgb(78, 154, 252);
            var themeGreenHover = Color.FromArgb(55, 130, 230);
            var outlineHoverBack = Color.FromArgb(235, 244, 255);

            // 1. Header Section
            _header = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = Color.White,
                Padding = new Padding(24, 18, 24, 10)
            };

            _title = new Label
            {
                AutoSize = true,
                Text = "My Account",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = themeGreen,
                Location = new Point(24, 18)
            };

            _header.Controls.Add(_title);

            // 2. Main Content Container (Scrollable)
            var mainContainer = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(24, 16, 24, 16),
                AutoScroll = true
            };

            // 3. Profile Card (Personal Information)
            _card = new MaterialCard
            {
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(24, 18, 24, 18),
                Margin = new Padding(0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // Form Fields Table
            _table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 7,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 14)
            };

            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Section title
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Username
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Email
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Email error
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Employee
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Roles
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Status

            var userInfoLabel = new Label
            {
                Text = "Personal Information",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 8)
            };
            _table.Controls.Add(userInfoLabel, 1, 0);
            _table.SetColumnSpan(userInfoLabel, 2);

            var userIcon = new Label
            {
                Text = "👤",
                Font = new Font("Segoe UI", 18F, FontStyle.Regular),
                ForeColor = themeGreen,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 0, 0)
            };
            _table.Controls.Add(userIcon, 0, 1);

            _txtUsername = CreateReadOnlyTextBox();
            _txtEmployee = CreateReadOnlyTextBox();
            _txtRoles = CreateReadOnlyTextBox();
            _txtStatus = CreateReadOnlyTextBox();

            _txtEmail = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F),
                AutoSize = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                TabIndex = 1
            };
            _txtEmail.TextChanged += (s, e) => ValidateEmailInline();

            _lblEmailError = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(231, 76, 60),
                Text = string.Empty,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0),
                Visible = false
            };

            AddRow(1, "Username", _txtUsername);
            AddRow(2, "Email Address", WrapInBorder(_txtEmail));
            _table.Controls.Add(_lblEmailError, 2, 3);
            AddRow(4, "Employee Name", _txtEmployee);
            AddRow(5, "User Roles", _txtRoles);
            AddRow(6, "Account Status", _txtStatus);

            // Footer Buttons Panel
            var buttonPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 0)
            };

            var buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            _btnSave = new HopeButton
            {
                Text = "Save Changes",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Size = new Size(155, 44),
                Margin = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                TabIndex = 2,
                Enabled = false
            };
            UiFactory.ConfigurePillHopeButton(_btnSave, themeGreen, themeGreenHover);
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = new HopeButton
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                Size = new Size(100, 44),
                Margin = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                TabIndex = 3
            };
            UiFactory.ConfigureOutlineHopeButton(_btnCancel, themeGreen, outlineHoverBack);
            _btnCancel.Click += async (s, e) => await ReloadAsync();

            _btnChangePassword = new HopeButton
            {
                Text = "Change Password",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                Size = new Size(170, 44),
                Margin = new Padding(0),
                Cursor = Cursors.Hand,
                TabIndex = 4
            };
            UiFactory.ConfigureOutlineHopeButton(_btnChangePassword, themeGreen, outlineHoverBack);
            _btnChangePassword.Click += (s, e) => OpenChangePasswordDialog();

            buttonFlow.Controls.Add(_btnSave);
            buttonFlow.Controls.Add(_btnCancel);
            buttonFlow.Controls.Add(_btnChangePassword);
            buttonPanel.Controls.Add(buttonFlow);

            // Add sections to card
            _card.Controls.Add(buttonPanel);
            _card.Controls.Add(_table);

            mainContainer.Controls.Add(_card);

            Controls.Add(mainContainer);
            Controls.Add(_header);

            // Ensure z-order is correct for clickability and visibility
            _header.BringToFront();
            mainContainer.BringToFront();
            _card.BringToFront();
            _table.BringToFront();
            buttonPanel.BringToFront();
            buttonFlow.BringToFront();
            _btnSave.BringToFront();
            _btnCancel.BringToFront();
            _btnChangePassword.BringToFront();

            ResumeLayout(true);
        }

        private static System.Windows.Forms.Panel WrapInBorder(TextBox txt)
        {
            var normalBorder = Color.FromArgb(180, 180, 180);
            var focusBorder  = Color.FromArgb(78, 154, 252);

            var wrapper = new System.Windows.Forms.Panel
            {
                Height    = 32,
                Margin    = new Padding(0, 6, 0, 6),
                BackColor = Color.White,
                Padding   = new Padding(4, 1, 4, 1)
            };

            wrapper.Paint += (_, e) =>
            {
                using var pen = new Pen(txt.Focused ? focusBorder : normalBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, wrapper.Width - 1, wrapper.Height - 1);
            };

            txt.Enter += (_, __) => wrapper.Invalidate();
            txt.Leave += (_, __) => wrapper.Invalidate();

            wrapper.Controls.Add(txt);
            return wrapper;
        }

        private static TextBox CreateReadOnlyTextBox()
        {
            return new TextBox
            {
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                AutoSize = false,
                Height = 32,
                Margin = new Padding(0, 6, 0, 6),
                Multiline = false,
                TabStop = false,
                ForeColor = Color.FromArgb(40, 40, 40)
            };
        }

        private void AddRow(int rowIndex, string label, Control control)
        {
            var lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 12, 0),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            if (control.Margin == Padding.Empty)
                control.Margin = new Padding(0, 6, 0, 6);

            _table.Controls.Add(lbl, 1, rowIndex);
            _table.Controls.Add(control, 2, rowIndex);
        }

        private async Task LoadProfileAsync()
        {
            try
            {
                if (!AppSession.IsLoggedIn)
                    return;

                var account = await _userRepo.GetUserAccountByUserIdAsync(AppSession.CurrentUserId);
                if (account == null)
                    return;

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
                ValidateEmailInline();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load account details: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReloadAsync()
        {
            await LoadProfileAsync();
        }

        private void ValidateEmailInline()
        {
            string email = _txtEmail.Text?.Trim() ?? string.Empty;

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
            string email = _txtEmail.Text?.Trim() ?? string.Empty;

            if (email.Length == 0 || !IsValidEmail(email))
            {
                ValidateEmailInline();
                _txtEmail.Focus();
                return;
            }

            try
            {
                _btnSave.Enabled = false;

                bool updated = await _userRepo.UpdateUserEmailAsync(AppSession.CurrentUserId, email);
                if (updated)
                {
                    AppSession.CurrentEmail = email;
                    _originalEmail = email;
                    ValidateEmailInline();

                    MessageBox.Show("Your account details have been updated.", "Saved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No changes were saved.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ValidateEmailInline();
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
