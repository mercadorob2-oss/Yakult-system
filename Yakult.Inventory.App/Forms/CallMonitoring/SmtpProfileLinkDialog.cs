using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    /// <summary>
    /// Recipient-only editor for a department or branch (subclassed as
    /// DepartmentSmtpProfileLinkDialog / BranchSmtpProfileLinkDialog for the
    /// target noun). Cleanup 2026-09-04: branches and departments are
    /// recipients-only — the per-target SMTP profile sections were removed.
    /// The single sender lives in Setup (dbo.CallEmailSettings).
    /// </summary>
    public class SmtpProfileLinkDialog : Form
    {
        private readonly string _targetNoun;
        private readonly List<LookupItem> _targets;

        private ComboBox cboTarget;
        private TextBox txtRecipientEmails;
        private TextBox txtEscalationEmails;

        public int SelectedTargetId { get; private set; }
        public string RecipientEmails { get; private set; }
        public string EscalationEmails { get; private set; }

        public SmtpProfileLinkDialog(
            string targetNoun,
            List<LookupItem> targets,
            int? preselectedTargetId,
            string prefilledRecipientEmails,
            string prefilledEscalationEmails)
        {
            _targetNoun = string.IsNullOrWhiteSpace(targetNoun) ? "Target" : targetNoun.Trim();
            _targets = targets ?? new List<LookupItem>();
            RecipientEmails = prefilledRecipientEmails ?? string.Empty;
            EscalationEmails = prefilledEscalationEmails ?? string.Empty;

            InitializeComponent();
            BindData(preselectedTargetId);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = $"{_targetNoun} Recipients";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(580, 380);
            this.BackColor = Color.White;
            this.Font = ModernUiHelper.FontNormal;

            var mainPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(24),
                AutoScroll = true
            };

            // Header
            mainPanel.Controls.Add(ModernUiHelper.CreateHeaderLabel($"{_targetNoun} Recipients"));
            mainPanel.Controls.Add(new Label { Height = 10 });

            cboTarget = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 28, Width = 450, Font = ModernUiHelper.FontNormal };
            AddLabeledControl(mainPanel, $"Select {_targetNoun}", cboTarget);

            // Notification Recipients
            mainPanel.Controls.Add(new Label { Height = 10 });
            mainPanel.Controls.Add(ModernUiHelper.CreateSectionHeader("Notifications"));

            txtRecipientEmails = ModernUiHelper.CreateTextBox();
            AddLabeledControl(mainPanel, "Notify To (emails separated by ;)", txtRecipientEmails);

            txtEscalationEmails = ModernUiHelper.CreateTextBox();
            AddLabeledControl(mainPanel, "Escalate To (emails separated by ;)", txtEscalationEmails);

            // Footer
            var buttonsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ModernUiHelper.ColorBackground,
                Padding = new Padding(0, 12, 24, 12)
            };
            buttonsPanel.Paint += (s, e) => { using (var pen = new Pen(Color.FromArgb(220, 220, 220))) e.Graphics.DrawLine(pen, 0, 0, buttonsPanel.Width, 0); };

            var btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel");
            btnCancel.DialogResult = DialogResult.Cancel;

            var btnOk = ModernUiHelper.CreatePrimaryButton("Save");
            btnOk.Click += (_, __) => SaveAndClose();

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false };
            btnFlow.Controls.Add(btnOk);
            btnFlow.Controls.Add(btnCancel);
            buttonsPanel.Controls.Add(btnFlow);

            this.Controls.Add(mainPanel);
            this.Controls.Add(buttonsPanel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private void AddLabeledControl(Panel parent, string labelText, Control control)
        {
            parent.Controls.Add(ModernUiHelper.CreateLabel(labelText));
            control.Width = 450;
            parent.Controls.Add(control);
            parent.Controls.Add(new Label { Height = 8 });
        }

        private void BindData(int? preselectedTargetId)
        {
            cboTarget.DisplayMember = "Name";
            cboTarget.ValueMember = "Id";
            cboTarget.DataSource = _targets;

            if (preselectedTargetId.HasValue)
                cboTarget.SelectedValue = preselectedTargetId.Value;

            txtRecipientEmails.Text = RecipientEmails;
            txtEscalationEmails.Text = EscalationEmails;
        }

        private void SaveAndClose()
        {
            if (!(cboTarget.SelectedValue is int targetId))
            {
                MessageBox.Show($"{_targetNoun} is required.", $"{_targetNoun} Recipients", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedTargetId = targetId;
            RecipientEmails = (txtRecipientEmails.Text ?? string.Empty).Trim();
            EscalationEmails = (txtEscalationEmails.Text ?? string.Empty).Trim();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
