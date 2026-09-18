using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.SystemSettings
{
    /// <summary>
    /// Dialog for adding/editing email templates
    /// </summary>
    public sealed class EmailTemplateEditorDialog : Form
    {
        private readonly EmailTemplateDto _existing;
        private readonly List<SystemSmtpProfileDto> _smtpProfiles;

        private TextBox txtTemplateKey;
        private TextBox txtSubject;
        private TextBox txtBody;
        private ComboBox cmbSmtpProfile;
        private CheckBox chkIsHtml;
        private CheckBox chkIsActive;
        private Label lblPlaceholders;

        public EmailTemplateDto Template { get; private set; }

        public EmailTemplateEditorDialog(EmailTemplateDto existing, List<SystemSmtpProfileDto> smtpProfiles)
        {
            _existing = existing;
            _smtpProfiles = smtpProfiles ?? new List<SystemSmtpProfileDto>();
            InitializeComponent();
            LoadExisting();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = _existing == null ? "Add Email Template" : "Edit Email Template";
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.ClientSize = new Size(780, 680);
            this.Font = new Font("Segoe UI", 9.5F);
            this.MinimumSize = new Size(650, 580);
            this.Padding = new Padding(20);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                Padding = new Padding(0),
            };

            // Row 0: Template Key
            var rowTemplateKey = CreateFieldRow("Template Key:", out txtTemplateKey);
            txtTemplateKey.ReadOnly = _existing != null;
            mainLayout.Controls.Add(rowTemplateKey, 0, 0);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // Row 1: Subject
            var rowSubject = CreateFieldRow("Subject Template:", out txtSubject);
            mainLayout.Controls.Add(rowSubject, 0, 1);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // Row 2: Default SMTP Profile
            var rowSmtpProfile = CreateComboRow("Default SMTP Sender:", out cmbSmtpProfile);
            cmbSmtpProfile.DropDownStyle = ComboBoxStyle.DropDownList;
            PopulateSmtpProfileCombo();
            mainLayout.Controls.Add(rowSmtpProfile, 0, 2);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // Row 3: Body label
            var lblBody = new Label
            {
                Text = "Body Template:",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 2)
            };
            mainLayout.Controls.Add(lblBody, 0, 3);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            // Row 4: Body textbox
            txtBody = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.5F),
                WordWrap = true,
                AcceptsReturn = true
            };
            txtBody.KeyDown += TxtBody_KeyDown;
            mainLayout.Controls.Add(txtBody, 0, 4);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Row 5: Checkboxes
            var checkboxPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 8, 0, 0)
            };
            chkIsHtml = new CheckBox { Text = "HTML Format", AutoSize = true, Checked = true, Margin = new Padding(0, 0, 20, 0) };
            chkIsActive = new CheckBox { Text = "Active", AutoSize = true, Checked = true };
            checkboxPanel.Controls.Add(chkIsHtml);
            checkboxPanel.Controls.Add(chkIsActive);
            mainLayout.Controls.Add(checkboxPanel, 0, 5);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

            // Row 6: Placeholder help
            lblPlaceholders = new Label
            {
                Text = "Use placeholders like {PlaceholderName} or {{PlaceholderName}} in subject and body.\n" +
                       "Common placeholders: {EmployeeName}, {BranchName}, {Date}, {RequestId}, {SetCode}, etc.",
                AutoSize = true,
                ForeColor = Color.FromArgb(64, 64, 64),
                Margin = new Padding(0, 4, 0, 0),
                Font = new Font("Segoe UI", 8.5F)
            };
            mainLayout.Controls.Add(lblPlaceholders, 0, 6);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 7: Spacer
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));

            // Buttons panel
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

            this.Controls.Add(mainLayout);
            this.Controls.Add(buttonsPanel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private void PopulateSmtpProfileCombo()
        {
            cmbSmtpProfile.Items.Clear();
            cmbSmtpProfile.Items.Add(new SmtpProfileItem(null, "— None (use caller's default) —"));
            foreach (var p in _smtpProfiles)
                cmbSmtpProfile.Items.Add(new SmtpProfileItem(p.ProfileId, p.ProfileName));
            cmbSmtpProfile.SelectedIndex = 0;
        }

        private Panel CreateFieldRow(string labelText, out TextBox textBox)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Height = 40,
                Padding = new Padding(0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 10, 0) };
            textBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };

            panel.Controls.Add(label, 0, 0);
            panel.Controls.Add(textBox, 1, 0);
            return panel;
        }

        private Panel CreateComboRow(string labelText, out ComboBox comboBox)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Height = 40,
                Padding = new Padding(0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 10, 0) };
            comboBox = new ComboBox { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };

            panel.Controls.Add(label, 0, 0);
            panel.Controls.Add(comboBox, 1, 0);
            return panel;
        }

        private void LoadExisting()
        {
            if (_existing == null)
            {
                chkIsHtml.Checked = true;
                chkIsActive.Checked = true;
                return;
            }

            txtTemplateKey.Text = _existing.TemplateKey ?? string.Empty;
            txtSubject.Text = _existing.SubjectTemplate ?? string.Empty;
            txtBody.Text = _existing.BodyTemplate ?? string.Empty;
            chkIsHtml.Checked = _existing.IsHtml;
            chkIsActive.Checked = _existing.IsActive;

            // Select the saved SMTP profile in the combobox
            if (_existing.DefaultSmtpProfileId.HasValue)
            {
                for (int i = 0; i < cmbSmtpProfile.Items.Count; i++)
                {
                    if (((SmtpProfileItem)cmbSmtpProfile.Items[i]).ProfileId == _existing.DefaultSmtpProfileId)
                    {
                        cmbSmtpProfile.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void TxtBody_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = false;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Checks that all required placeholders for the current template key are present in the body.
        /// Returns true if validation passes, false (and shows a warning) if a token is missing.
        /// Templates with no defined requirements always pass.
        /// </summary>
        private bool ValidateTemplateTokens(string body)
        {
            // Required placeholders per template key.
            // Add entries here whenever a new system template is introduced.
            var requiredByKey = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "CARTRIDGE_EXCHANGE_FULFILLED", new[]
                    {
                        "{SetCode}", "{RequesterName}", "{DistributionMethod}", "{DistributionStatus}",
                        "{ReceivedBy}", "{ItemCount}", "{ModelRows}"
                    }
                },
                {
                    "CARTRIDGE_EXCHANGE_PARTIAL", new[]
                    {
                        "{SetCode}", "{RequesterName}", "{DistributionMethod}", "{DistributionStatus}",
                        "{ReceivedBy}", "{ItemCount}", "{ModelRows}"
                    }
                },
                {
                    "CARTRIDGE_EXCHANGE_UNFULFILLED", new[]
                    {
                        "{SetCode}", "{RequesterName}", "{DistributionMethod}", "{DistributionStatus}",
                        "{ReceivedBy}", "{ItemCount}", "{ModelRows}"
                    }
                },
            };

            var key = (txtTemplateKey.Text ?? string.Empty).Trim();

            if (!requiredByKey.TryGetValue(key, out var required))
                return true; // No requirements defined for this template key — allow save.

            foreach (var token in required)
            {
                if (body.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    MessageBox.Show(
                        $"The template is missing the required placeholder:\n\n  {token}\n\n" +
                        "Please restore it before saving.",
                        "Missing Placeholder",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    txtBody.Focus();
                    return false;
                }
            }

            return true;
        }

        private void SaveAndClose()
        {
            var templateKey = (txtTemplateKey.Text ?? string.Empty).Trim();
            var subject = (txtSubject.Text ?? string.Empty).Trim();
            var body = txtBody.Text ?? string.Empty;
            var isHtml = chkIsHtml.Checked;
            var isActive = chkIsActive.Checked;
            var selectedProfile = cmbSmtpProfile.SelectedItem as SmtpProfileItem;

            if (string.IsNullOrWhiteSpace(templateKey))
            {
                MessageBox.Show("Template Key is required.", "Email Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTemplateKey.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                MessageBox.Show("Subject Template is required.", "Email Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSubject.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                MessageBox.Show("Body Template is required.", "Email Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBody.Focus();
                return;
            }

            if (!ValidateTemplateTokens(body))
                return;

            Template = new EmailTemplateDto
            {
                TemplateId = _existing?.TemplateId ?? 0,
                TemplateKey = templateKey,
                SubjectTemplate = subject,
                BodyTemplate = body,
                IsHtml = isHtml,
                IsActive = isActive,
                DefaultSmtpProfileId = selectedProfile?.ProfileId,
                ModifiedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null,
                CreatedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Simple wrapper for ComboBox items so we can store ProfileId alongside the display name.
        /// </summary>
        private sealed class SmtpProfileItem
        {
            public int? ProfileId { get; }
            private readonly string _displayName;
            public SmtpProfileItem(int? profileId, string displayName) { ProfileId = profileId; _displayName = displayName; }
            public override string ToString() => _displayName;
        }
    }
}
