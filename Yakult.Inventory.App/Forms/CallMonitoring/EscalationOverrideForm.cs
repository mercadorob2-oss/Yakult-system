using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class EscalationOverrideForm : Form
    {
        private readonly int _defaultSupDays;
        private readonly int _defaultMgrDays;
        private readonly bool _allowClear;
        private readonly string _headerText;

        private readonly ErrorProvider _errorProvider = new ErrorProvider();
        private readonly ToolTip _toolTip = new ToolTip();

        private Label lblDefaults;
        private Label lblPreview;
        private Label lblReasonCount;
        private NumericUpDown numSupDays;
        private NumericUpDown numMgrDays;
        private TextBox txtReason;
        private Button btnSave;
        private Button btnClear;
        private Button btnCancel;

        public bool ClearRequested { get; private set; }
        public int DaysToSupervisor { get; private set; }
        public int DaysToManager { get; private set; }
        public string Reason { get; private set; }

        public EscalationOverrideForm(
            CallEscalationSettingsItem settings,
            CallTicketEscalationOverrideItem existingOverride,
            bool allowClear = true,
            string headerText = null,
            string windowTitle = null)
        {
            _defaultSupDays = Math.Max(1, settings?.DaysToSupervisor ?? 2);
            _defaultMgrDays = Math.Max(_defaultSupDays, settings?.DaysToManager ?? 3);
            _allowClear = allowClear;
            _headerText = string.IsNullOrWhiteSpace(headerText) ? "Override Escalation" : headerText.Trim();

            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(windowTitle))
                this.Text = windowTitle.Trim();

            var sup = existingOverride?.DaysToSupervisor ?? _defaultSupDays;
            var mgr = existingOverride?.DaysToManager ?? _defaultMgrDays;
            this.numSupDays.Value = Math.Max(this.numSupDays.Minimum, Math.Min(this.numSupDays.Maximum, sup));
            this.numMgrDays.Value = Math.Max(this.numMgrDays.Minimum, Math.Min(this.numMgrDays.Maximum, mgr));
            this.txtReason.Text = existingOverride?.Reason ?? string.Empty;

            UpdateUiState();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = ModernUiHelper.FontNormal;
            this.BackColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.Text = "Escalation Override";
            this.ClientSize = new Size(560, 520);

            _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;
            _errorProvider.ContainerControl = this;

            _toolTip.AutoPopDelay = 10000;
            _toolTip.InitialDelay = 300;
            _toolTip.ReshowDelay = 200;
            _toolTip.ShowAlways = true;

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                AutoScroll = true,
                BackColor = Color.White
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 8,
                BackColor = Color.White
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Header
            content.Controls.Add(ModernUiHelper.CreateHeaderLabel(_headerText));

            // Helper text (what this does + audit behavior)
            var lblHelp = new Label
            {
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Margin = new Padding(0, 6, 0, 0),
                Text = "Increase escalation time for this ticket. A reason is required and will be saved in the ticket history."
            };
            content.Controls.Add(lblHelp);

            // Info Banner (defaults)
            var infoPanel = new Panel
            {
                BackColor = Color.FromArgb(240, 248, 255), // AliceBlue
                Padding = new Padding(12),
                Margin = new Padding(0, 14, 0, 0),
                Dock = DockStyle.Top,
                AutoSize = true
            };
            this.lblDefaults = new Label
            {
                AutoSize = true,
                Text =
                    $"System defaults: Supervisor {_defaultSupDays} day(s), Manager {_defaultMgrDays} day(s)",
                ForeColor = Color.FromArgb(60, 90, 120),
                Font = new Font("Segoe UI", 8.5F)
            };
            infoPanel.Controls.Add(this.lblDefaults);
            content.Controls.Add(infoPanel);

            // 1. Settings
            content.Controls.Add(ModernUiHelper.CreateSectionHeader("Escalation Timing"));

            numSupDays = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = _defaultSupDays,
                Maximum = 30,
                Height = 28,
                Font = ModernUiHelper.FontNormal
            };
            _toolTip.SetToolTip(numSupDays, $"Minimum is {_defaultSupDays} (system default). Max 30 days.");

            numMgrDays = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = _defaultMgrDays,
                Maximum = 30,
                Height = 28,
                Font = ModernUiHelper.FontNormal
            };
            _toolTip.SetToolTip(numMgrDays, $"Minimum is {_defaultMgrDays} (system default). Must be ≥ Supervisor days. Max 30 days.");

            numSupDays.ValueChanged += (_, __) =>
            {
                if (numMgrDays.Value < numSupDays.Value)
                    numMgrDays.Value = numSupDays.Value;
                UpdateUiState();
            };
            numMgrDays.ValueChanged += (_, __) => UpdateUiState();

            var timingGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 0),
                Padding = new Padding(0, 4, 0, 0)
            };
            timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));

            AddGridRow(timingGrid, "Escalate to Supervisor after (days)", numSupDays);
            AddGridRow(timingGrid, "Escalate to Manager after (days)", numMgrDays);
            content.Controls.Add(timingGrid);

            this.lblPreview = new Label
            {
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Margin = new Padding(0, 8, 0, 0)
            };
            content.Controls.Add(this.lblPreview);

            // 2. Reason
            content.Controls.Add(ModernUiHelper.CreateSectionHeader("Justification"));

            this.txtReason = ModernUiHelper.CreateTextBox();
            this.txtReason.Multiline = true;
            this.txtReason.ScrollBars = ScrollBars.Vertical;
            this.txtReason.Height = 96;
            this.txtReason.MaxLength = 400; // matches dbo.sp_Call_SetTicketEscalationOverride @Reason NVARCHAR(400)
            this.txtReason.TextChanged += (_, __) => UpdateUiState();
            _toolTip.SetToolTip(this.txtReason, "Required. This will be stored in the ticket history.");

            var lblReason = ModernUiHelper.CreateLabel("Reason for Override (Required)");
            lblReason.Margin = new Padding(0, 0, 0, 3);
            content.Controls.Add(lblReason);
            content.Controls.Add(this.txtReason);

            var reasonFooter = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22,
                Margin = new Padding(0, 6, 0, 0)
            };

            var lblReasonHint = new Label
            {
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Text = "Tip: include what changed (e.g., vendor delay, part ordering, schedule).",
                Dock = DockStyle.Left
            };

            this.lblReasonCount = new Label
            {
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight
            };

            reasonFooter.Controls.Add(this.lblReasonCount);
            reasonFooter.Controls.Add(lblReasonHint);
            content.Controls.Add(reasonFooter);


            // Footer
            var buttonsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ModernUiHelper.ColorBackground,
                Padding = new Padding(24, 12, 24, 12)
            };
            buttonsPanel.Paint += (s, e) => { using (var pen = new Pen(Color.FromArgb(220, 220, 220))) e.Graphics.DrawLine(pen, 0, 0, buttonsPanel.Width, 0); };

            this.btnSave = ModernUiHelper.CreatePrimaryButton("Save");
            this.btnSave.Click += (_, __) => SaveAndClose(clear: false);

            this.btnClear = new Button
            {
                Text = "Clear Override",
                AutoSize = true,
                Height = 36,
                BackColor = Color.White,
                ForeColor = ModernUiHelper.ColorDanger,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Left
            };
            this.btnClear.FlatAppearance.BorderSize = 1;
            this.btnClear.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            this.btnClear.Click += (_, __) => SaveAndClose(clear: true);
            this.btnClear.Visible = _allowClear;

            this.btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel");
            this.btnCancel.DialogResult = DialogResult.Cancel;

            var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false };
            rightFlow.Controls.Add(this.btnSave);
            rightFlow.Controls.Add(this.btnCancel);

            buttonsPanel.Controls.Add(this.btnClear);
            buttonsPanel.Controls.Add(rightFlow);

            if (!_allowClear && this.btnClear != null)
            {
                try
                {
                    buttonsPanel.Controls.Remove(this.btnClear);
                }
                catch
                {
                }
            }

            contentHost.Controls.Add(content);
            this.Controls.Add(contentHost);
            this.Controls.Add(buttonsPanel);

            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private static void AddGridRow(TableLayoutPanel grid, string labelText, Control control)
        {
            if (grid == null)
                return;

            var rowIndex = grid.RowCount;
            grid.RowCount = rowIndex + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = ModernUiHelper.CreateLabel(labelText);
            lbl.Margin = new Padding(0, 8, 10, 0);
            lbl.Dock = DockStyle.Fill;

            control.Margin = new Padding(0, 6, 0, 0);

            grid.Controls.Add(lbl, 0, rowIndex);
            grid.Controls.Add(control, 1, rowIndex);
        }

        private void SaveAndClose(bool clear)
        {
            var reason = (this.txtReason.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                UpdateUiState();
                this.txtReason.Focus();
                return;
            }

            var sup = (int)this.numSupDays.Value;
            var mgr = (int)this.numMgrDays.Value;
            if (mgr < sup)
            {
                UpdateUiState();
                this.numMgrDays.Focus();
                return;
            }

            if (clear)
            {
                var msg = "This will remove the escalation override for this ticket and revert to system defaults.\n\nContinue?";
                if (MessageBox.Show(msg, "Clear Override", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            this.ClearRequested = clear;
            this.DaysToSupervisor = sup;
            this.DaysToManager = mgr;
            this.Reason = reason;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void UpdateUiState()
        {
            var reason = (this.txtReason?.Text ?? string.Empty).Trim();
            var reasonOk = !string.IsNullOrWhiteSpace(reason);

            var sup = (int)(this.numSupDays?.Value ?? _defaultSupDays);
            var mgr = (int)(this.numMgrDays?.Value ?? _defaultMgrDays);
            var daysOk = mgr >= sup;

            if (this.lblPreview != null)
            {
                var supDelta = sup - _defaultSupDays;
                var mgrDelta = mgr - _defaultMgrDays;
                this.lblPreview.Text = $"Preview: Supervisor {_defaultSupDays} → {sup} day(s) ({(supDelta >= 0 ? "+" : string.Empty)}{supDelta}), " +
                                       $"Manager {_defaultMgrDays} → {mgr} day(s) ({(mgrDelta >= 0 ? "+" : string.Empty)}{mgrDelta})";
            }

            if (this.lblReasonCount != null)
            {
                var len = (this.txtReason?.Text ?? string.Empty).Length;
                this.lblReasonCount.Text = $"{Math.Min(len, 400)}/400";
            }

            if (this.btnSave != null)
                this.btnSave.Enabled = reasonOk && daysOk;

            if (this.btnClear != null)
                this.btnClear.Enabled = reasonOk;

            try
            {
                _errorProvider.SetError(this.txtReason, reasonOk ? string.Empty : "Reason is required.");
                _errorProvider.SetError(this.numMgrDays, daysOk ? string.Empty : "Manager days must be ≥ Supervisor days.");
            }
            catch
            {
            }
        }
    }
}
