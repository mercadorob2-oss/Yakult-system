using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Data;

namespace Yakult.Inventory.App.Pages.User
{
    public partial class RegisterPage : Form
    {
        private readonly UserRepository _userRepo;

        private bool _passwordVisible = false;
        private bool _confirmPasswordVisible = false;
        private bool _isRegistering = false;

        public RegisterPage(UserRepository userRepo)
        {
            InitializeComponent();
            _userRepo = userRepo;

            // Mask password fields
            PassRegField.PasswordChar = '*';
            ConfPassRegField.PasswordChar = '*';

            // 👁 Eye icon setup (Password)
            lblTogglePassword.Font = new Font("Segoe MDL2 Assets", 14F);
            lblTogglePassword.Text = "\uE890"; // eye open
            lblTogglePassword.Cursor = Cursors.Hand;
            lblTogglePassword.ForeColor = Color.Gray;

            // 👁 Eye icon setup (Confirm Password)
            lblToggleConfirmPassword.Font = new Font("Segoe MDL2 Assets", 14F);
            lblToggleConfirmPassword.Text = "\uE890"; // eye open
            lblToggleConfirmPassword.Cursor = Cursors.Hand;
            lblToggleConfirmPassword.ForeColor = Color.Gray;

            // Click events
            lblTogglePassword.Click += lblTogglePassword_Click;
            lblToggleConfirmPassword.Click += lblToggleConfirmPassword_Click;

            // Hover events (shared)
            lblTogglePassword.MouseEnter += EyeIcon_MouseEnter;
            lblTogglePassword.MouseLeave += EyeIcon_MouseLeave;
            lblToggleConfirmPassword.MouseEnter += EyeIcon_MouseEnter;
            lblToggleConfirmPassword.MouseLeave += EyeIcon_MouseLeave;

            // Center eye icons
            CenterEyeIcons();

            // Password match check
            PassRegField.TextChanged += CheckPasswordMatch;
            ConfPassRegField.TextChanged += CheckPasswordMatch;

            // Enter key support
            NameRegField.KeyDown += NameRegField_KeyDown;
            EmailAddRegField.KeyDown += EmailAddRegField_KeyDown;
            PassRegField.KeyDown += PassRegField_KeyDown;
            ConfPassRegField.KeyDown += ConfPassRegField_KeyDown;
        }

        /* -------------------- Centering -------------------- */

        private void CenterEyeIcons()
        {
            // Password (outside, right)
            lblTogglePassword.Left =
                PassRegField.Right + 8;

            lblTogglePassword.Top =
                PassRegField.Top +
                (PassRegField.Height - lblTogglePassword.Height) / 2;

            // Confirm Password (outside, right)
            lblToggleConfirmPassword.Left =
                ConfPassRegField.Right + 8;

            lblToggleConfirmPassword.Top =
                ConfPassRegField.Top +
                (ConfPassRegField.Height - lblToggleConfirmPassword.Height) / 2;
        }


        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterEyeIcons();
        }

        /* -------------------- Hover Effects -------------------- */

        private void EyeIcon_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Label lbl)
            {
                lbl.ForeColor = Color.FromArgb(78, 154, 252); // Yakult green
            }
        }

        private void EyeIcon_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Label lbl)
            {
                lbl.ForeColor = Color.Gray;
            }
        }

        /* -------------------- Key Navigation -------------------- */

        private void NameRegField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                EmailAddRegField.Focus();
            }
        }

        private void EmailAddRegField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                PassRegField.Focus();
            }
        }

        private void PassRegField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ConfPassRegField.Focus();
            }
        }

        private void ConfPassRegField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                RegBtn_Click(sender, e);
            }
        }

        /* -------------------- Password Match -------------------- */

        private void CheckPasswordMatch(object sender, EventArgs e)
        {
            string password = PassRegField.Text;
            string confirmPassword = ConfPassRegField.Text;

            if (string.IsNullOrEmpty(confirmPassword))
            {
                lblPasswordMatch.Text = "";
                return;
            }

            if (password == confirmPassword)
            {
                lblPasswordMatch.Text = "✓ Passwords match";
                lblPasswordMatch.ForeColor = Color.Green;
            }
            else
            {
                lblPasswordMatch.Text = "✗ Passwords don't match";
                lblPasswordMatch.ForeColor = Color.Red;
            }
        }

        /* -------------------- Toggle Password Visibility -------------------- */

        private void lblTogglePassword_Click(object sender, EventArgs e)
        {
            _passwordVisible = !_passwordVisible;

            if (_passwordVisible)
            {
                PassRegField.PasswordChar = '\0';
                lblTogglePassword.Text = "\uE890"; // eye closed
            }
            else
            {
                PassRegField.PasswordChar = '*';
                lblTogglePassword.Text = "\uE890"; // eye open
            }
        }

        private void lblToggleConfirmPassword_Click(object sender, EventArgs e)
        {
            _confirmPasswordVisible = !_confirmPasswordVisible;

            if (_confirmPasswordVisible)
            {
                ConfPassRegField.PasswordChar = '\0';
                lblToggleConfirmPassword.Text = "\uE890"; // eye closed
            }
            else
            {
                ConfPassRegField.PasswordChar = '*';
                lblToggleConfirmPassword.Text = "\uE890"; // eye open
            }
        }

        /* -------------------- Register Logic -------------------- */

        private async void RegBtn_Click(object sender, EventArgs e)
        {
            if (_isRegistering) return;

            string name = NameRegField.Text.Trim();
            string email = string.IsNullOrWhiteSpace(EmailAddRegField.Text) ? null : EmailAddRegField.Text.Trim();
            string password = PassRegField.Text.Trim();
            string confirm = ConfPassRegField.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirm))
            {
                MessageBox.Show(
                    "Please fill in all required fields.",
                    "Missing Information",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show(
                    "Passwords do not match.",
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _isRegistering = true;
            RegBtn.Enabled = false;
            NameRegField.Enabled = false;
            EmailAddRegField.Enabled = false;
            PassRegField.Enabled = false;
            ConfPassRegField.Enabled = false;

            try
            {
                bool userExists = await _userRepo.IsUserExistsAsync(name, email);
                if (userExists)
                {
                    MessageBox.Show(
                        "The name or email address is already registered.",
                        "Duplicate Account",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                int newUserId =
                    await _userRepo.CreateUser_PlainVarbinaryAsync(name, email, password);

                if (newUserId > 0)
                {
                    MessageBox.Show(
                        "Registration successful! Please login.",
                        "Registration Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        "Registration failed. Please try again.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred during registration:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isRegistering = false;
                RegBtn.Enabled = true;
                NameRegField.Enabled = true;
                EmailAddRegField.Enabled = true;
                PassRegField.Enabled = true;
                ConfPassRegField.Enabled = true;
            }
        }

        private void LoginBtnRedirect_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
