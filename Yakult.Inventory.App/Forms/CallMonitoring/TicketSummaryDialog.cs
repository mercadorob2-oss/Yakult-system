using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class TicketSummaryDialog : Form
    {
        private readonly CallTicketListItem _ticket;
        private readonly string _slaLabel;
        private readonly string _slaTooltip;
        private readonly string _escalationText;
        private readonly string _lastActivityText;
        private readonly string _idleText;

        public TicketSummaryDialog(
            CallTicketListItem ticket,
            string slaLabel,
            string slaTooltip,
            string escalationText,
            string lastActivityText,
            string idleText)
        {
            _ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
            _slaLabel = slaLabel ?? string.Empty;
            _slaTooltip = slaTooltip ?? string.Empty;
            _escalationText = escalationText ?? string.Empty;
            _lastActivityText = lastActivityText ?? string.Empty;
            _idleText = idleText ?? string.Empty;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Ticket Summary";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            this.ClientSize = new Size(920, 560);

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18),
                BackColor = this.BackColor
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F)); // header
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // content
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F)); // buttons

            // Header
            var pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
            var lblTitle = new Label
            {
                Text = "Ticket Summary",
                AutoSize = true,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(8, 8)
            };
            var subtitle = $"{(_ticket.TicketCode ?? _ticket.TicketId.ToString())} • {(_ticket.Status ?? "-")} • {(_ticket.Priority ?? "-")}";
            var lblSub = new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location = new Point(10, 40)
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);

            // Content (scrollable)
            var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = Color.White
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
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
                    Margin = new Padding(0, 8, 10, 8),
                    Dock = DockStyle.Top
                };

                valueControl.Margin = new Padding(0, 8, 0, 8);
                valueControl.Dock = DockStyle.Top;

                tbl.Controls.Add(lbl, 0, row);
                tbl.Controls.Add(valueControl, 1, row);
            }

            Label Val(string text) => new Label
            {
                AutoSize = true,
                MaximumSize = new Size(680, 0),
                Text = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim(),
                ForeColor = Color.FromArgb(44, 62, 80)
            };

            TextBox ValMultiline(string text, int minHeight = 60)
            {
                var tb = new TextBox
                {
                    ReadOnly = true,
                    Multiline = true,
                    BorderStyle = BorderStyle.FixedSingle,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(44, 62, 80),
                    Text = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim()
                };

                tb.MinimumSize = new Size(680, minHeight);
                tb.Height = minHeight;
                return tb;
            }

            AddRow("Ticket", Val(_ticket.TicketCode ?? _ticket.TicketId.ToString()));
            AddRow("Status", Val(_ticket.Status));
            AddRow("Priority", Val(_ticket.Priority));
            AddRow("Assigned To", Val(string.IsNullOrWhiteSpace(_ticket.ResponsiblePerson) ? "Unassigned" : _ticket.ResponsiblePerson));
            AddRow("Company", Val(_ticket.Company));
            AddRow("Department", Val(_ticket.Department));
            AddRow("Branch", Val(_ticket.Branch));
            AddRow("Caller", Val(_ticket.CallerName));
            AddRow("Issue Type", Val(_ticket.IssueType));
            AddRow("Issue", ValMultiline(_ticket.Issue, minHeight: 90));

            AddRow("Created", Val(AppTime.ToLocalString(_ticket.CreatedAt, "g")));
            AddRow("Updated", Val(AppTime.ToLocalString(_ticket.UpdatedAt, "g")));
            AddRow("Last Contact", Val(_ticket.LastContactAt.HasValue ? AppTime.ToLocalString(_ticket.LastContactAt.Value, "g") : "-"));
            AddRow("Age (days)", Val(_ticket.TicketAgeDays.ToString()));
            AddRow("Last Activity", Val(_lastActivityText));
            AddRow("Idle", Val(_idleText));

            var slaValue = Val(string.IsNullOrWhiteSpace(_slaLabel) ? "-" : _slaLabel);
            if (!string.IsNullOrWhiteSpace(_slaTooltip))
                new ToolTip { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 200, ShowAlways = true }.SetToolTip(slaValue, _slaTooltip);
            AddRow("SLA", slaValue);

            var esc = Val(string.IsNullOrWhiteSpace(_escalationText) ? "-" : _escalationText);
            AddRow("Escalation", esc);

            scroll.Controls.Add(tbl);
            pnlBody.Controls.Add(scroll);

            // Buttons
            var pnlButtons = new Panel { Dock = DockStyle.Fill, BackColor = this.BackColor };
            var btnClose = new Button
            {
                Text = "Close",
                Size = new Size(110, 34),
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(this.ClientSize.Width - 18 - 110, 8)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => this.Close();
            this.AcceptButton = btnClose;
            pnlButtons.Controls.Add(btnClose);
            pnlButtons.Resize += (_, __) =>
            {
                btnClose.Left = pnlButtons.ClientSize.Width - btnClose.Width;
                btnClose.Top = (pnlButtons.ClientSize.Height - btnClose.Height) / 2;
            };

            main.Controls.Add(pnlHeader, 0, 0);
            main.Controls.Add(pnlBody, 0, 1);
            main.Controls.Add(pnlButtons, 0, 2);

            this.Controls.Add(main);

            this.ResumeLayout(false);
        }
    }
}

