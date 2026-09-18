using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Pages.Admin.AccountManagement
{
    public partial class ResetPasswordDialog : Form
    {
        private readonly UserRepository _userRepo;
        private readonly int? _preSelectedUserId;
        private List<UserDto> _allUsers;

        private Panel headerPanel;
        private Label lblTitle;
        private ComboBox cboUsers;
        private TextBox txtNewPassword;
        private TextBox txtConfirmPassword;
        private CheckBox chkShowPassword;
        private Button btnReset;
        private Button btnCancel;

        public ResetPasswordDialog(UserRepository userRepo)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            InitializeComponent();
            LoadUsers();
        }

        public ResetPasswordDialog(UserRepository userRepo, int preSelectedUserId)
        {
            _userRepo          = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            _preSelectedUserId = preSelectedUserId;
            InitializeComponent();
            Text     = "Manual Password Reset";
            lblTitle.Text = "Manual Password Reset";
            LoadUsers();
        }

        private void InitializeComponent()
        {
            Text = "Reset User Password";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            // Header Panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0, 120, 215),
                Padding = new Padding(20, 10, 20, 10)
            };

            lblTitle = new Label
            {
                Text = "Reset User Password",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            headerPanel.Controls.Add(lblTitle);

            // User Selection
            var lblUser = new Label
            {
                Text = "Select User:",
                Location = new Point(30, 90),
                Width = 120,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            cboUsers = new ComboBox
            {
                Location = new Point(160, 88),
                Width = 380,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };

            // New Password
            var lblNewPassword = new Label
            {
                Text = "New Password:",
                Location = new Point(30, 140),
                Width = 120,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            txtNewPassword = new TextBox
            {
                Location = new Point(160, 138),
                Width = 380,
                PasswordChar = '*',
                Font = new Font("Segoe UI", 9F)
            };

            // Confirm Password
            var lblConfirm = new Label
            {
                Text = "Confirm Password:",
                Location = new Point(30, 180),
                Width = 120,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            txtConfirmPassword = new TextBox
            {
                Location = new Point(160, 178),
                Width = 380,
                PasswordChar = '*',
                Font = new Font("Segoe UI", 9F)
            };

            // Show Password Checkbox
            chkShowPassword = new CheckBox
            {
                Text = "Show Password",
                Location = new Point(160, 215),
                Width = 150,
                Font = new Font("Segoe UI", 9F)
            };
            chkShowPassword.CheckedChanged += ChkShowPassword_CheckedChanged;

            // Info Label
            var lblInfo = new Label
            {
                Text = "⚠ Admin function: Reset password for selected user.\nPassword must be at least 6 characters.",
                Location = new Point(30, 255),
                Width = 510,
                Height = 40,
                ForeColor = Color.DarkOrange,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            // Buttons
            btnReset = new Button
            {
                Text = "Reset Password",
                Location = new Point(350, 310),
                Width = 130,
                Height = 35,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += BtnReset_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(490, 310),
                Width = 80,
                Height = 35,
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            // Add controls to form
            Controls.AddRange(new Control[] {
                headerPanel,
                lblUser, cboUsers,
                lblNewPassword, txtNewPassword,
                lblConfirm, txtConfirmPassword,
                chkShowPassword,
                lblInfo,
                btnReset, btnCancel
            });

            CancelButton = btnCancel;
        }

        private async void LoadUsers()
        {
            try
            {
                _allUsers = await _userRepo.GetAllUsersAsync();

                cboUsers.DisplayMember = "DisplayText";
                cboUsers.ValueMember = "UserId";
                cboUsers.DataSource = _allUsers.Select(u => new
                {
                    u.UserId,
                    DisplayText = $"{u.Name} ({u.EmailAddress}){(u.IsDeveloper ? " [Developer]" : "")}"
                }).ToList();

                if (_preSelectedUserId.HasValue)
                {
                    // Pre-select the specified user and lock the dropdown
                    for (int i = 0; i < cboUsers.Items.Count; i++)
                    {
                        dynamic item = cboUsers.Items[i];
                        if ((int)item.UserId == _preSelectedUserId.Value)
                        {
                            cboUsers.SelectedIndex = i;
                            break;
                        }
                    }
                    cboUsers.Enabled = false;
                }
                else if (cboUsers.Items.Count > 0)
                {
                    cboUsers.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ChkShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            char passwordChar = chkShowPassword.Checked ? '\0' : '*';
            txtNewPassword.PasswordChar = passwordChar;
            txtConfirmPassword.PasswordChar = passwordChar;
        }

        private async void BtnReset_Click(object sender, EventArgs e)
        {
            // Validation
            if (cboUsers.SelectedValue == null)
            {
                MessageBox.Show("Please select a user.", "User Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newPassword = txtNewPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Please enter a new password.", "Password Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPassword.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters long.", "Weak Password",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("Passwords do not match.", "Password Mismatch",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Focus();
                return;
            }

            int userId = (int)cboUsers.SelectedValue;
            var selectedUser = _allUsers.FirstOrDefault(u => u.UserId == userId);

            // Confirm action
            var confirmResult = MessageBox.Show(
                $"Are you sure you want to reset the password for:\n\n" +
                $"User: {selectedUser?.Name}\n" +
                $"Email: {selectedUser?.EmailAddress}\n\n" +
                $"This action cannot be undone.",
                "Confirm Password Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                // Disable button to prevent double-click
                btnReset.Enabled = false;
                btnReset.Text = "Resetting...";

                bool success = await _userRepo.ResetPasswordAsync(userId, newPassword);

                if (success)
                {
                    ActivityLogger.Log(ActivityLogger.Actions.ResetPassword,
                        "User", userId, $"Password manually reset for '{selectedUser?.Name}'");

                    MessageBox.Show(
                        $"Password for '{selectedUser?.Name}' has been reset successfully!",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to reset password. User not found.", "Reset Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnReset.Enabled = true;
                btnReset.Text = "Reset Password";
            }
        }
    }
}
