using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.SystemSettings
{
    /// <summary>
    /// Dialog for sending test emails manually
    /// </summary>
    public sealed class SendTestEmailDialog : Form
    {
        private readonly List<SystemSmtpProfileDto> _profiles;
        private readonly List<EmailTemplateDto> _templates;
        private readonly SystemEmailNotificationService _emailService;

        private ComboBox cboProfile;
        private ComboBox cboTemplate;
        private TextBox txtRecipients;
        private TextBox txtPlaceholders;
        private Button btnSend;
        private Label lblStatus;

        public SendTestEmailDialog(
            List<SystemSmtpProfileDto> profiles,
            List<EmailTemplateDto> templates,
            SystemEmailNotificationService emailService)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _templates = templates ?? throw new ArgumentNullException(nameof(templates));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));

            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "Send Test Email";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(600, 520);
            this.Font = new Font("Segoe UI", 9.5F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(16),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Row 0: Profile
            cboProfile = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = "ProfileName",
                ValueMember = "ProfileId"
            };
            layout.Controls.Add(CreateLabel("SMTP Profile:"), 0, 0);
            layout.Controls.Add(cboProfile, 1, 0);

            // Row 1: Template
            cboTemplate = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = "TemplateKey",
                ValueMember = "TemplateId"
            };
            layout.Controls.Add(CreateLabel("Template:"), 0, 1);
            layout.Controls.Add(cboTemplate, 1, 1);

            // Row 2: Recipients
            var lblRecipients = new Label
            {
                Text = "Recipients:",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            txtRecipients = new TextBox
            {
                Multiline = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                ScrollBars = ScrollBars.Vertical,
                Height = 70
            };
            var recipientHint = new Label
            {
                Text = "(Comma or semicolon separated)",
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F)
            };

            var recipientPanel = new Panel { Dock = DockStyle.Fill };
            txtRecipients.Location = new Point(0, 0);
            txtRecipients.Width = 420;
            recipientHint.Location = new Point(0, 72);
            recipientPanel.Controls.Add(txtRecipients);
            recipientPanel.Controls.Add(recipientHint);

            layout.Controls.Add(lblRecipients, 0, 2);
            layout.Controls.Add(recipientPanel, 1, 2);

            // Row 3: Placeholders
            var lblPlaceholders = new Label
            {
                Text = "Placeholders:",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            txtPlaceholders = new TextBox
            {
                Multiline = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                Height = 110
            };
            var placeholderHint = new Label
            {
                Text = "(One per line: PlaceholderName=Value)",
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F)
            };

            var placeholderPanel = new Panel { Dock = DockStyle.Fill };
            txtPlaceholders.Location = new Point(0, 0);
            txtPlaceholders.Width = 420;
            placeholderHint.Location = new Point(0, 112);
            placeholderPanel.Controls.Add(txtPlaceholders);
            placeholderPanel.Controls.Add(placeholderHint);

            layout.Controls.Add(lblPlaceholders, 0, 3);
            layout.Controls.Add(placeholderPanel, 1, 3);

            // Row 4: Send button
            btnSend = new Button
            {
                Text = "Send Email",
                Width = 120,
                Height = 34,
                Margin = new Padding(0, 0, 12, 0)
            };
            btnSend.Click += BtnSend_Click;

            lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                ForeColor = Color.Green,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 8, 0, 0)
            };

            var sendPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };
            sendPanel.Controls.Add(btnSend);
            sendPanel.Controls.Add(lblStatus);

            layout.SetColumnSpan(sendPanel, 2);
            layout.Controls.Add(sendPanel, 0, 4);

            // Bottom buttons
            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 54,
                Padding = new Padding(12, 10, 12, 10)
            };

            var btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32
            };

            buttonsPanel.Controls.Add(btnClose);

            this.Controls.Add(layout);
            this.Controls.Add(buttonsPanel);

            this.CancelButton = btnClose;

            this.ResumeLayout(false);
        }

        private void LoadData()
        {
            // Load profiles
            cboProfile.DataSource = _profiles;
            if (_profiles.Count > 0)
                cboProfile.SelectedIndex = 0;

            // Load templates
            cboTemplate.DataSource = _templates;
            if (_templates.Count > 0)
                cboTemplate.SelectedIndex = 0;

            // Set example placeholders
            txtPlaceholders.Text = "EmployeeName=John Doe\r\n" +
                                   "BranchName=Manila Branch\r\n" +
                                   "Date=" + DateTime.Now.ToString("yyyy-MM-dd") + "\r\n" +
                                   "RequestId=12345\r\n" +
                                   "SetCode=SET-001";
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            var profile = cboProfile.SelectedItem as SystemSmtpProfileDto;
            var template = cboTemplate.SelectedItem as EmailTemplateDto;
            var recipients = (txtRecipients.Text ?? string.Empty).Trim();
            var placeholderText = txtPlaceholders.Text ?? string.Empty;

            if (profile == null)
            {
                MessageBox.Show("Please select an SMTP profile.", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (template == null)
            {
                MessageBox.Show("Please select a template.", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(recipients))
            {
                MessageBox.Show("Please enter at least one recipient email.", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipients.Focus();
                return;
            }

            // Parse placeholders
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = placeholderText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                        placeholders[key] = value;
                }
            }

            // Parse recipient emails
            var recipientList = recipients
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            if (recipientList.Count == 0)
            {
                MessageBox.Show("No valid recipient emails found.", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSend.Enabled = false;
                lblStatus.Text = "Sending...";
                lblStatus.ForeColor = Color.Orange;
                Application.DoEvents();

                var result = await _emailService.SendEmailAsync(
                    templateKey: template.TemplateKey,
                    placeholders: placeholders,
                    profileId: profile.ProfileId,
                    recipientEmails: recipientList,
                    entityType: "TestEmail",
                    sentByUserId: AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null);

                if (result.SentSuccessfully)
                {
                    lblStatus.Text = $"Sent to {result.RecipientCount} recipient(s)!";
                    lblStatus.ForeColor = Color.Green;
                    MessageBox.Show($"Test email sent successfully to {result.RecipientCount} recipient(s)!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (result.WasSkipped)
                {
                    lblStatus.Text = "Skipped";
                    lblStatus.ForeColor = Color.Orange;
                    MessageBox.Show($"Email was skipped: {result.Message}", "Skipped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    lblStatus.Text = "Failed";
                    lblStatus.ForeColor = Color.Red;
                    MessageBox.Show($"Failed to send email: {result.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Error sending email: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSend.Enabled = true;
            }
        }

        private static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
    }
}
