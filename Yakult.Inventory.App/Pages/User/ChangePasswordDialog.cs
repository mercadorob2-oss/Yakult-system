using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.User
{
    public partial class ChangePasswordDialog : Form
    {
        private readonly UserRepository _userRepo;

        private Panel headerPanel;
        private Label lblTitle;
        private TextBox txtCurrentPassword;
        private TextBox txtNewPassword;
        private TextBox txtConfirmPassword;
        private CheckBox chkShowPassword;
        private Button btnChange;
        private Button btnCancel;

        private Label lblInfo;

        public ChangePasswordDialog(UserRepository userRepo)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Text = "Update Account Password";
            Size = new Size(640, 580);
            MinimumSize = new Size(640, 580);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(248, 249, 250);
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            this.KeyDown += ChangePasswordDialog_KeyDown;

            var themeGreen      = Color.FromArgb(78, 154, 252);
            var themeGreenHover = Color.FromArgb(58, 142, 246);
            var outlineHoverBack = Color.FromArgb(235, 245, 255);

            // ── Footer (buttons) — docked Bottom so it is always visible ──────
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = Color.White,
                Padding = new Padding(0, 12, 20, 12)
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 38,
                BackColor = Color.White,
                ForeColor = themeGreen,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
                TabIndex = 6,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = themeGreen;
            btnCancel.FlatAppearance.MouseOverBackColor = outlineHoverBack;
            btnCancel.Location = new Point(footerPanel.Width - 90 - 12, 13);

            btnChange = new Button
            {
                Text = "Update Password",
                Width = 150,
                Height = 38,
                BackColor = themeGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                TabIndex = 5,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnChange.FlatAppearance.BorderSize = 0;
            btnChange.FlatAppearance.MouseOverBackColor = themeGreenHover;
            btnChange.Location = new Point(footerPanel.Width - 90 - 12 - 150 - 8, 13);
            btnChange.Click += BtnChange_Click;

            footerPanel.Controls.Add(btnCancel);
            footerPanel.Controls.Add(btnChange);

            // Reposition buttons when footer resizes
            footerPanel.Resize += (_, __) =>
            {
                btnCancel.Location  = new Point(footerPanel.Width - 90 - 12, 13);
                btnChange.Location  = new Point(footerPanel.Width - 90 - 12 - 150 - 8, 13);
            };

            // ── Header — docked Top ────────────────────────────────────────────
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            lblTitle = new Label
            {
                Text = "Change Password",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = themeGreen,
                AutoSize = false,
                Width = 400,
                Height = 48,
                TextAlign = ContentAlignment.BottomLeft,
                Location = new Point(24, 10)
            };
            headerPanel.Controls.Add(lblTitle);

            // ── Content area ───────────────────────────────────────────────────
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(24, 12, 24, 0)
            };

            // Spacer after header
            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // Input grid
            var inputGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++)
                inputGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var lblCurrent = CreateFieldLabel("Current Password:");
            txtCurrentPassword = CreatePasswordField();
            txtCurrentPassword.TabIndex = 1;

            var lblNew = CreateFieldLabel("New Password:");
            txtNewPassword = CreatePasswordField();
            txtNewPassword.TabIndex = 2;

            var lblConfirm = CreateFieldLabel("Confirm New Password:");
            txtConfirmPassword = CreatePasswordField();
            txtConfirmPassword.TabIndex = 3;

            chkShowPassword = new CheckBox
            {
                Text = "Show Passwords",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Cursor = Cursors.Hand,
                TabIndex = 4,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 10, 0, 0)
            };
            chkShowPassword.CheckedChanged += ChkShowPassword_CheckedChanged;

            inputGrid.Controls.Add(lblCurrent, 0, 0);
            inputGrid.Controls.Add(WrapInBorder(txtCurrentPassword), 1, 0);
            inputGrid.Controls.Add(lblNew, 0, 1);
            inputGrid.Controls.Add(WrapInBorder(txtNewPassword), 1, 1);
            inputGrid.Controls.Add(lblConfirm, 0, 2);
            inputGrid.Controls.Add(WrapInBorder(txtConfirmPassword), 1, 2);
            inputGrid.Controls.Add(chkShowPassword, 1, 3);

            // Spacer before requirements
            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // Requirements label
            lblInfo = new Label
            {
                Text = "Security Rules:  • Minimum 8 characters" + Environment.NewLine +
                       "• Must include: 1 uppercase  • 1 number  • 1 special character",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 110, 135),
                AutoSize = false,
                Height = 75,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 4, 0, 18)
            };

            // Controls are added in reverse order for Dock=Top stacking
            contentPanel.Controls.Add(lblInfo);
            contentPanel.Controls.Add(spacer2);
            contentPanel.Controls.Add(inputGrid);
            contentPanel.Controls.Add(spacer1);

            // Add in reverse dock order: Fill last, so Bottom and Top claim space first
            Controls.Add(contentPanel);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            CancelButton = btnCancel;
            AcceptButton = btnChange;

            this.FormClosed += (_, __) => ClearFields();

            txtCurrentPassword.TextChanged += (_, __) => UpdateButtonState();
            txtNewPassword.TextChanged += (_, __) => UpdateButtonState();
            txtConfirmPassword.TextChanged += (_, __) => UpdateButtonState();

            ResumeLayout(false);
            PerformLayout();
        }

        private static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = false,
                Margin = new Padding(0, 0, 12, 0)
            };
        }

        private static TextBox CreatePasswordField()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                PasswordChar = '●',
                Font = new Font("Segoe UI", 11F),
                AutoSize = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Multiline = false
            };
        }

        private static Panel WrapInBorder(TextBox txt)
        {
            var normalBorder = Color.FromArgb(180, 180, 180);
            var focusBorder  = Color.FromArgb(78, 154, 252);

            var wrapper = new Panel
            {
                Anchor    = AnchorStyles.Left | AnchorStyles.Right,
                Height    = 32,
                Margin    = new Padding(0, 10, 0, 10),
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

        private void ChangePasswordDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void ClearFields()
        {
            txtCurrentPassword?.Clear();
            txtNewPassword?.Clear();
            txtConfirmPassword?.Clear();
            if (chkShowPassword != null)
                chkShowPassword.Checked = false;
            UpdateButtonState();
        }

        private static bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));
            return hasUpper && hasDigit && hasSpecial;
        }

        private void UpdateButtonState()
        {
            string currentPassword = txtCurrentPassword.Text;
            string newPassword = txtNewPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            bool ok = !string.IsNullOrWhiteSpace(currentPassword)
                      && IsPasswordStrong(newPassword)
                      && string.Equals(newPassword, confirmPassword, StringComparison.Ordinal)
                      && !string.Equals(currentPassword, newPassword, StringComparison.Ordinal);

            btnChange.Enabled = ok;
        }

        private void ChkShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            char passwordChar = chkShowPassword.Checked ? '\0' : '●';
            txtCurrentPassword.PasswordChar = passwordChar;
            txtNewPassword.PasswordChar = passwordChar;
            txtConfirmPassword.PasswordChar = passwordChar;
        }

        private async void BtnChange_Click(object sender, EventArgs e)
        {
            // Validation
            string currentPassword = txtCurrentPassword.Text;
            string newPassword = txtNewPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                MessageBox.Show("Please enter your current password.", "Current Password Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCurrentPassword.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Please enter a new password.", "New Password Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (!IsPasswordStrong(newPassword))
            {
                MessageBox.Show("New password does not meet the password rules.", "Weak Password",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("New passwords do not match.", "Password Mismatch",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Focus();
                return;
            }

            if (currentPassword == newPassword)
            {
                MessageBox.Show("New password must be different from current password.", "Same Password",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            try
            {
                // Disable button to prevent double-click
                btnChange.Enabled = false;
                btnChange.Text = "Updating...";

                int currentUserId = AppSession.CurrentUserId;

                if (currentUserId <= 0)
                {
                    MessageBox.Show("Unable to determine current user. Please log in again.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                bool success = await _userRepo.ChangePasswordAsync(currentUserId, currentPassword, newPassword);

                if (success)
                {
                    MessageBox.Show(
                        "Your password has been changed successfully!\n\nPlease remember your new password.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "Current password is incorrect. Please try again.",
                        "Incorrect Password",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    txtCurrentPassword.Clear();
                    txtCurrentPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnChange.Text = "Update Password";
                UpdateButtonState();
            }
        }
    }
}
