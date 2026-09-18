using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.SystemSettings
{
    /// <summary>
    /// Dialog for adding/editing System SMTP profiles
    /// </summary>
    public sealed class SystemSmtpProfileDialog : Form
    {
        private readonly SystemSmtpProfileDto _existing;
        private readonly EmailRepository _emailRepo;
        private List<EmailAddressDto> _emailAddresses;

        // True when _existing has password bytes that could NOT be decrypted on this machine.
        // In that state a blank password box means "remove the unusable password", not "keep it".
        private bool _existingPasswordUnreadable;

        private TextBox txtProfileName;
        private TextBox txtSmtpServer;
        private TextBox txtPort;
        private CheckBox chkUseSsl;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private ComboBox cboFromEmail;
        private CheckBox chkIsActive;

        public SystemSmtpProfileDto Profile { get; private set; }

        public SystemSmtpProfileDialog(SystemSmtpProfileDto existing, EmailRepository emailRepo)
        {
            _existing = existing;
            _emailRepo = emailRepo ?? throw new ArgumentNullException(nameof(emailRepo));
            InitializeComponent();
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = _existing == null ? "Add SMTP Profile" : "Edit SMTP Profile";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(600, 450);
            this.Font = new Font("Segoe UI", 9.5F);
            this.Padding = new Padding(20);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (var i = 0; i < 8; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            txtProfileName = CreateTextBox();
            txtSmtpServer = CreateTextBox();
            txtPort = CreateTextBox();
            chkUseSsl = new CheckBox
            {
                Text = "Use SSL/TLS",
                AutoSize = true,
                Checked = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 10, 0, 0)
            };
            txtUsername = CreateTextBox();
            txtPassword = CreateTextBox(password: true);
            cboFromEmail = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = "DisplayText",
                ValueMember = "EmailId",
                Margin = new Padding(0, 10, 0, 0)
            };
            chkIsActive = new CheckBox
            {
                Text = "Active",
                AutoSize = true,
                Checked = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 10, 0, 0)
            };

            layout.Controls.Add(CreateLabel("Profile Name:"), 0, 0);
            layout.Controls.Add(txtProfileName, 1, 0);

            layout.Controls.Add(CreateLabel("SMTP Server:"), 0, 1);
            layout.Controls.Add(txtSmtpServer, 1, 1);

            layout.Controls.Add(CreateLabel("Port:"), 0, 2);
            layout.Controls.Add(txtPort, 1, 2);

            layout.Controls.Add(CreateLabel("SSL/TLS:"), 0, 3);
            layout.Controls.Add(chkUseSsl, 1, 3);

            layout.Controls.Add(CreateLabel("Username:"), 0, 4);
            layout.Controls.Add(txtUsername, 1, 4);

            layout.Controls.Add(CreateLabel("Password:"), 0, 5);
            layout.Controls.Add(txtPassword, 1, 5);

            layout.Controls.Add(CreateLabel("From Email:"), 0, 6);
            layout.Controls.Add(cboFromEmail, 1, 6);

            layout.Controls.Add(CreateLabel("Status:"), 0, 7);
            layout.Controls.Add(chkIsActive, 1, 7);

            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 54,
                Padding = new Padding(0, 10, 0, 10),
                AutoSize = false
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Margin = new Padding(0, 0, 0, 0)
            };

            var btnOk = new Button
            {
                Text = "Save",
                Width = 90,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnOk.Click += (_, __) => SaveAndClose();

            buttonsPanel.Controls.Add(btnCancel);
            buttonsPanel.Controls.Add(btnOk);

            this.Controls.Add(layout);
            this.Controls.Add(buttonsPanel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Load email addresses
                _emailAddresses = await _emailRepo.GetEmailAddressesAsync(activeOnly: true);

                // Populate combo
                cboFromEmail.Items.Clear();
                foreach (var email in _emailAddresses)
                {
                    cboFromEmail.Items.Add(new
                    {
                        EmailId = email.EmailId,
                        DisplayText = string.IsNullOrWhiteSpace(email.DisplayName)
                            ? email.EmailAddress
                            : $"{email.DisplayName} <{email.EmailAddress}>"
                    });
                }

                // Load existing profile
                LoadExisting();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading email addresses: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadExisting()
        {
            if (_existing == null)
            {
                txtPort.Text = "587";
                chkUseSsl.Checked = true;
                chkIsActive.Checked = true;
                return;
            }

            txtProfileName.Text = _existing.ProfileName ?? string.Empty;
            txtSmtpServer.Text = _existing.SmtpServer ?? string.Empty;
            txtPort.Text = _existing.SmtpPort > 0 ? _existing.SmtpPort.ToString() : "587";
            chkUseSsl.Checked = _existing.UseSsl;
            txtUsername.Text = _existing.SmtpUsername ?? string.Empty;
            chkIsActive.Checked = _existing.IsActive;

            // Select from email
            if (_existing.FromEmailId > 0)
            {
                for (int i = 0; i < cboFromEmail.Items.Count; i++)
                {
                    dynamic item = cboFromEmail.Items[i];
                    if (item.EmailId == _existing.FromEmailId)
                    {
                        cboFromEmail.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Decrypt password — decryption can fail if the profile was saved by a different
            // Windows user account (DataProtectionScope.CurrentUser). If it fails, the field
            // is left blank and the user must re-enter the password to re-encrypt it.
            var password = SecretProtector.UnprotectToString(_existing.SmtpPasswordEnc);
            txtPassword.Text = password ?? string.Empty;

            if (_existing.SmtpPasswordEnc != null && _existing.SmtpPasswordEnc.Length > 0 && password == null)
            {
                _existingPasswordUnreadable = true;
                txtPassword.BackColor = System.Drawing.Color.FromArgb(255, 230, 230);
                new ToolTip().SetToolTip(txtPassword,
                    "The stored password could not be decrypted on this machine. " +
                    "Type a new password, or leave this blank and Save to clear it " +
                    "(use blank for a relay that needs no authentication).");
            }
        }

        private void SaveAndClose()
        {
            var profileName = (txtProfileName.Text ?? string.Empty).Trim();
            var server = (txtSmtpServer.Text ?? string.Empty).Trim();
            var portText = (txtPort.Text ?? string.Empty).Trim();
            var username = (txtUsername.Text ?? string.Empty).Trim();
            var passwordText = txtPassword.Text ?? string.Empty;
            var useSsl = chkUseSsl.Checked;
            var isActive = chkIsActive.Checked;

            // Validation
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Profile Name is required.", "SMTP Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProfileName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show("SMTP Server is required.", "SMTP Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSmtpServer.Focus();
                return;
            }

            if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Port must be a valid number (1-65535).", "SMTP Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPort.Focus();
                return;
            }

            if (cboFromEmail.SelectedItem == null)
            {
                MessageBox.Show("From Email is required.", "SMTP Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboFromEmail.Focus();
                return;
            }

            dynamic selectedEmail = cboFromEmail.SelectedItem;
            int fromEmailId = selectedEmail.EmailId;

            // Encrypt password.
            //  - A new value typed in the box is (re-)encrypted.
            //  - A blank box normally means "keep the existing password" — but only when that
            //    existing blob is actually usable. If it couldn't be decrypted (pink field),
            //    a blank box means "remove it", so the profile can be used with no auth.
            byte[] passwordEnc = null;
            if (!string.IsNullOrEmpty(passwordText))
                passwordEnc = SecretProtector.ProtectString(passwordText);
            else if (!_existingPasswordUnreadable
                     && _existing?.SmtpPasswordEnc != null && _existing.SmtpPasswordEnc.Length > 0)
                passwordEnc = _existing.SmtpPasswordEnc;

            Profile = new SystemSmtpProfileDto
            {
                ProfileId = _existing?.ProfileId ?? 0,
                ProfileName = profileName,
                SmtpServer = server,
                SmtpPort = port,
                UseSsl = useSsl,
                SmtpUsername = username,
                SmtpPasswordEnc = passwordEnc,
                FromEmailId = fromEmailId,
                IsActive = isActive,
                ModifiedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null,
                CreatedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 12, 10, 0)
            };
        }

        private static TextBox CreateTextBox(bool password = false)
        {
            return new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                UseSystemPasswordChar = password,
                Margin = new Padding(0, 10, 0, 0)
            };
        }
    }
}
