using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class AutoEscalationAssigneeDialog : Form
    {
        public sealed class AssigneeOption
        {
            public int EmpId { get; set; }
            public string Name { get; set; }
            public string Position { get; set; }

            public override string ToString()
            {
                var pos = string.IsNullOrWhiteSpace(Position) ? string.Empty : $" • {Position.Trim()}";
                return $"{(Name ?? string.Empty).Trim()}{pos}";
            }
        }

        private readonly int? _currentEmpId;
        private readonly List<AssigneeOption> _options;

        private ComboBox cboSupervisor;
        private Label lblCurrentValue;
        private Button btnSave;
        private Button btnCancel;
        private ErrorProvider _error;

        public int? SelectedEmpId { get; private set; }

        public AutoEscalationAssigneeDialog(int? currentAssigneeEmpId, IEnumerable<AssigneeOption> supervisors)
        {
            _currentEmpId = currentAssigneeEmpId.HasValue && currentAssigneeEmpId.Value > 0 ? currentAssigneeEmpId : null;
            _options = (supervisors ?? Enumerable.Empty<AssigneeOption>())
                .Where(x => x != null && x.EmpId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Name)
                .ToList();

            InitializeComponent();
            BindOptions();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Auto-Escalation Assignee";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.ClientSize = new Size(760, 320);

            _error = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18),
                BackColor = this.BackColor
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
            var lblTitle = new Label
            {
                Text = "Auto Escalation Assignee",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(8, 8)
            };
            var lblSub = new Label
            {
                Text = "Select a single Supervisor who will be assigned when auto-escalation triggers.",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location = new Point(10, 44)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = Color.White
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            void AddRow(string label, Control valueControl)
            {
                var row = tbl.RowCount;
                tbl.RowCount++;
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var lbl = new Label
                {
                    Text = label,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 100, 100),
                    Margin = new Padding(0, 10, 10, 10),
                    Dock = DockStyle.Top
                };

                valueControl.Margin = new Padding(0, 8, 0, 8);
                valueControl.Dock = DockStyle.Top;

                tbl.Controls.Add(lbl, 0, row);
                tbl.Controls.Add(valueControl, 1, row);
            }

            lblCurrentValue = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                ForeColor = Color.FromArgb(44, 62, 80),
                Text = "-"
            };

            cboSupervisor = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 520,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };

            var note = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                ForeColor = Color.FromArgb(120, 120, 120),
                Text = "This affects app-driven auto escalation only (background). Manual status changes won't auto-assign."
            };

            AddRow("Current Assignee", lblCurrentValue);
            AddRow("Select Supervisor", cboSupervisor);
            AddRow(string.Empty, note);

            body.Controls.Add(tbl);

            var buttons = new Panel { Dock = DockStyle.Fill, BackColor = this.BackColor };
            btnSave = new Button
            {
                Text = "Save",
                Size = new Size(110, 34),
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (_, __) => OnSave();

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 34),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(107, 114, 128),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnCancel.Click += (_, __) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            buttons.Controls.Add(btnSave);
            buttons.Controls.Add(btnCancel);
            buttons.Resize += (_, __) =>
            {
                btnCancel.Left = buttons.ClientSize.Width - btnCancel.Width;
                btnCancel.Top = (buttons.ClientSize.Height - btnCancel.Height) / 2;

                btnSave.Left = btnCancel.Left - 10 - btnSave.Width;
                btnSave.Top = btnCancel.Top;
            };

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;

            main.Controls.Add(header, 0, 0);
            main.Controls.Add(body, 0, 1);
            main.Controls.Add(buttons, 0, 2);

            this.Controls.Add(main);
            this.ResumeLayout(false);
        }

        private void BindOptions()
        {
            cboSupervisor.BeginUpdate();
            try
            {
                cboSupervisor.DataSource = null;
                cboSupervisor.Items.Clear();

                var items = new List<AssigneeOption>();
                items.AddRange(_options);
                cboSupervisor.DataSource = items;
            }
            finally
            {
                cboSupervisor.EndUpdate();
            }

            if (_currentEmpId.HasValue)
            {
                var idx = -1;
                for (var i = 0; i < cboSupervisor.Items.Count; i++)
                {
                    if (cboSupervisor.Items[i] is AssigneeOption opt && opt.EmpId == _currentEmpId.Value)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx >= 0)
                    cboSupervisor.SelectedIndex = idx;
            }

            var current = _options.FirstOrDefault(o => _currentEmpId.HasValue && o.EmpId == _currentEmpId.Value);
            lblCurrentValue.Text = current != null ? current.ToString() : "-";
        }

        private void OnSave()
        {
            _error.SetError(cboSupervisor, string.Empty);

            if (!(cboSupervisor.SelectedItem is AssigneeOption opt) || opt.EmpId <= 0)
            {
                _error.SetError(cboSupervisor, "Select a supervisor to assign escalated tickets to.");
                return;
            }

            SelectedEmpId = opt.EmpId;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
