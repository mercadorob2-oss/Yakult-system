using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class CallMonitoringDisplayModeControl : UserControl
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly Action<int> _openTicket;

        private Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblClock;
        private Label lblLastRefresh;
        private Button btnRefresh;
        private Button btnExportExcel;
        private Button btnExportPdf;
        private FlowLayoutPanel flpExportButtons;

        private TableLayoutPanel tblSummary;
        private Label lblOpenCount;
        private Label lblOverdueCount;
        private Label lblEscalatedCount;
        private Label lblUnassignedCount;

        private FlowLayoutPanel flpInfo;
        private Label lblAutoRefreshInfo;
        private Label lblOverdueRule;
        private Label lblActionHint;
        private Label lblViewSummary;

        private TabControl tabViews;
        private TableLayoutPanel tblBoard;
        private Label lblPendingHeader;
        private Label lblInProgressHeader;
        private Label lblEscalatedHeader;
        private Label lblOverdueHeader;
        private FlowLayoutPanel flpPending;
        private FlowLayoutPanel flpInProgress;
        private FlowLayoutPanel flpEscalated;
        private FlowLayoutPanel flpOverdue;
        private Panel pnlTableHost;
        private Label lblTableEmpty;
        private DataGridView dgvTable;

        private readonly Timer _refreshTimer;
        private readonly Timer _clockTimer;
        private bool _isLoading;
        private DateTime? _lastRefreshLocal;
        private int _overdueDays = 3;
        private int _lastTicketCount;
        private int _lastOverdueCount;
        private int _lastEscalatedCount;
        private int _lastUnassignedCount;
        private List<CallTicketListItem> _currentTickets;

        public CallMonitoringDisplayModeControl(ICallMonitoringRepository repo, Action<int> openTicket)
        {
            if (repo == null) throw new ArgumentNullException("repo");
            _repo = repo;
            _openTicket = openTicket;
            InitializeComponent();
            _refreshTimer = new Timer { Interval = 30000 };
            _refreshTimer.Tick += async (_, __) => await LoadDataAsync();
            _clockTimer = new Timer { Interval = 1000 };
            _clockTimer.Tick += (_, __) => lblClock.Text = DateTime.Now.ToString("hh:mm:ss tt");
            this.VisibleChanged += async (_, __) =>
            {
                if (!this.Visible || this.IsDisposed)
                {
                    StopTimers();
                    return;
                }
                StartTimers();
                lblClock.Text = DateTime.Now.ToString("hh:mm:ss tt");
                await LoadDataAsync();
            };
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            BackColor = Color.FromArgb(239, 243, 247);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(18, 18, 18, 14), BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            pnlHeader = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 20, 24, 16), Margin = new Padding(0, 0, 0, 12) };
            pnlHeader.Paint += PaintHeader;
            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));

            var headerText = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            lblTitle = new Label { AutoSize = true, Font = new Font("Segoe UI", 22F, FontStyle.Bold), ForeColor = Color.White, Text = "Open Tickets Display" };
            lblSubtitle = new Label { AutoSize = true, Font = new Font("Segoe UI", 11F), ForeColor = Color.FromArgb(213, 232, 250), Text = "Live ITCM wallboard for active tickets and immediate action" };
            headerText.Controls.Add(lblTitle);
            headerText.Controls.Add(lblSubtitle);
            lblSubtitle.Location = new Point(2, 48);

            var headerRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            lblClock = new Label { Dock = DockStyle.Top, Height = 42, Font = new Font("Segoe UI", 22F, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleRight, Text = "--:--:--" };
            lblLastRefresh = new Label { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(205, 226, 246), TextAlign = ContentAlignment.MiddleRight, Text = "Refreshing..." };
            btnRefresh = new Button { Anchor = AnchorStyles.Top | AnchorStyles.Right, Size = new Size(148, 36), Location = new Point(188, 62), Text = "Refresh Now", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 160, 148), ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (_, __) => await LoadDataAsync(true);

            headerRight.Controls.Add(btnRefresh);
            headerRight.Controls.Add(lblLastRefresh);
            headerRight.Controls.Add(lblClock);

            headerLayout.Controls.Add(headerText, 0, 0);
            headerLayout.Controls.Add(headerRight, 1, 0);
            pnlHeader.Controls.Add(headerLayout);

            tblSummary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 0, 0, 10), BackColor = Color.Transparent };
            for (var i = 0; i < 4; i++) tblSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tblSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            lblOpenCount = SummaryValue(); lblOverdueCount = SummaryValue(); lblEscalatedCount = SummaryValue(); lblUnassignedCount = SummaryValue();
            tblSummary.Controls.Add(SummaryCard("Open Tickets", "All active work on screen", lblOpenCount, Color.FromArgb(52, 152, 219), Color.FromArgb(240, 248, 255)), 0, 0);
            tblSummary.Controls.Add(SummaryCard("Overdue", "Needs immediate attention", lblOverdueCount, Color.FromArgb(231, 76, 60), Color.FromArgb(255, 244, 244)), 1, 0);
            tblSummary.Controls.Add(SummaryCard("Escalated", "Already pushed for action", lblEscalatedCount, Color.FromArgb(230, 126, 34), Color.FromArgb(255, 248, 240)), 2, 0);
            tblSummary.Controls.Add(SummaryCard("Unassigned", "Owner still missing", lblUnassignedCount, Color.FromArgb(108, 117, 125), Color.FromArgb(245, 247, 249)), 3, 0);

            flpExportButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, 4, 0, 4), BackColor = Color.Transparent, Height = 34 };

            btnExportExcel = new Button { Size = new Size(110, 28), Text = "Export Excel", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4, 2, 0, 2) };
            btnExportExcel.FlatAppearance.BorderSize = 0;
            btnExportExcel.Click += (_, __) => ExportToExcel();

            btnExportPdf = new Button { Size = new Size(110, 28), Text = "Export PDF", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(211, 47, 47), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0, 2, 4, 2) };
            btnExportPdf.FlatAppearance.BorderSize = 0;
            btnExportPdf.Click += (_, __) => ExportToPdf();

            flpExportButtons.Controls.Add(btnExportExcel);
            flpExportButtons.Controls.Add(btnExportPdf);

            flpInfo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 10), BackColor = Color.Transparent };
            lblAutoRefreshInfo = InfoChip("Auto refresh every 30s", Color.FromArgb(227, 242, 253), Color.FromArgb(30, 87, 153));
            lblOverdueRule = InfoChip("Overdue rule: 3d idle", Color.FromArgb(255, 243, 224), Color.FromArgb(166, 95, 26));
            lblActionHint = InfoChip("Double-click any card to open ticket details", Color.FromArgb(237, 247, 237), Color.FromArgb(39, 94, 54));
            lblViewSummary = new Label { AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(73, 80, 87), Margin = new Padding(6, 0, 0, 0), Text = "Board | waiting for ticket data..." };
            flpInfo.Controls.Add(lblAutoRefreshInfo);
            flpInfo.Controls.Add(lblOverdueRule);
            flpInfo.Controls.Add(lblActionHint);
            flpInfo.Controls.Add(lblViewSummary);

            tblBoard = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent };
            for (var i = 0; i < 4; i++) tblBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tblBoard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            lblPendingHeader = ColumnHeader(); lblInProgressHeader = ColumnHeader(); lblEscalatedHeader = ColumnHeader(); lblOverdueHeader = ColumnHeader();
            flpPending = ColumnFlow(Color.FromArgb(248, 251, 254)); flpInProgress = ColumnFlow(Color.FromArgb(249, 247, 252)); flpEscalated = ColumnFlow(Color.FromArgb(255, 249, 242)); flpOverdue = ColumnFlow(Color.FromArgb(255, 247, 247));
            HookColumn(flpPending); HookColumn(flpInProgress); HookColumn(flpEscalated); HookColumn(flpOverdue);
            tblBoard.Controls.Add(ColumnPanel(lblPendingHeader, flpPending, Color.FromArgb(52, 152, 219), Color.FromArgb(248, 251, 254)), 0, 0);
            tblBoard.Controls.Add(ColumnPanel(lblInProgressHeader, flpInProgress, Color.FromArgb(155, 89, 182), Color.FromArgb(249, 247, 252)), 1, 0);
            tblBoard.Controls.Add(ColumnPanel(lblEscalatedHeader, flpEscalated, Color.FromArgb(230, 126, 34), Color.FromArgb(255, 249, 242)), 2, 0);
            tblBoard.Controls.Add(ColumnPanel(lblOverdueHeader, flpOverdue, Color.FromArgb(231, 76, 60), Color.FromArgb(255, 247, 247)), 3, 0);

            dgvTable = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(229, 233, 238),
                ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable
            };
            dgvTable.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(18, 52, 86);
            dgvTable.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvTable.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvTable.ColumnHeadersHeight = 42;
            dgvTable.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvTable.DefaultCellStyle.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular);
            dgvTable.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvTable.DefaultCellStyle.SelectionBackColor = Color.FromArgb(228, 239, 250);
            dgvTable.DefaultCellStyle.SelectionForeColor = Color.FromArgb(27, 38, 49);
            dgvTable.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvTable.RowTemplate.Height = 34;
            dgvTable.CellDoubleClick += (_, e) =>
            {
                if (_openTicket == null || e.RowIndex < 0 || e.RowIndex >= dgvTable.Rows.Count)
                    return;

                var ticket = dgvTable.Rows[e.RowIndex].Tag as CallTicketListItem;
                if (ticket != null)
                    _openTicket(ticket.TicketId);
            };
            ConfigureTableColumns();

            tabViews = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                ItemSize = new Size(140, 34),
                SizeMode = TabSizeMode.Fixed
            };

            var tabBoard = new TabPage("Board View") { BackColor = Color.FromArgb(239, 243, 247) };
            var tabTable = new TabPage("Table View") { BackColor = Color.FromArgb(239, 243, 247) };
            tabBoard.Controls.Add(tblBoard);

            pnlTableHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 0), BackColor = Color.Transparent };
            lblTableEmpty = new Label
            {
                Dock = DockStyle.Fill,
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 117, 125),
                BackColor = Color.White,
                Text = "No tickets to show in table view."
            };
            pnlTableHost.Controls.Add(dgvTable);
            pnlTableHost.Controls.Add(lblTableEmpty);
            tabTable.Controls.Add(pnlTableHost);

            tabViews.TabPages.Add(tabBoard);
            tabViews.TabPages.Add(tabTable);
            tabViews.SelectedIndexChanged += (_, __) =>
            {
                UpdateViewSummary();
                UpdateActionHint();
            };

            root.Controls.Add(pnlHeader, 0, 0);
            root.Controls.Add(tblSummary, 0, 1);
            root.Controls.Add(flpExportButtons, 0, 2);
            root.Controls.Add(flpInfo, 0, 3);
            root.Controls.Add(tabViews, 0, 4);
            Controls.Add(root);
            ResumeLayout(false);
        }

        public async Task LoadDataAsync(bool force = false)
        {
            if (_isLoading && !force) return;
            _isLoading = true;
            btnRefresh.Enabled = false;
            lblLastRefresh.Text = "Refreshing...";
            try
            {
                _overdueDays = await _repo.GetOverdueDaysAsync(3);
                var tickets = await _repo.GetTicketsAsync("All", null, 250) ?? new List<CallTicketListItem>();
                _currentTickets = tickets;
                RenderTickets(tickets);
                _lastRefreshLocal = DateTime.Now;
                lblLastRefresh.Text = string.Format("Last refresh {0}  |  {1} active tickets", _lastRefreshLocal.Value.ToString("MMM dd, yyyy hh:mm:ss tt"), tickets.Count);
                lblOverdueRule.Text = string.Format("Overdue rule: {0}d idle", Math.Max(1, _overdueDays));
            }
            catch (Exception ex)
            {
                ShowLoadError(ex.Message);
            }
            finally
            {
                _isLoading = false;
                btnRefresh.Enabled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopTimers();
                if (_refreshTimer != null) _refreshTimer.Dispose();
                if (_clockTimer != null) _clockTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void StartTimers() { if (!_refreshTimer.Enabled) _refreshTimer.Start(); if (!_clockTimer.Enabled) _clockTimer.Start(); }
        private void StopTimers() { if (_refreshTimer.Enabled) _refreshTimer.Stop(); if (_clockTimer.Enabled) _clockTimer.Stop(); }

        private void RenderTickets(List<CallTicketListItem> tickets)
        {
            var pending = new List<CallTicketListItem>();
            var progress = new List<CallTicketListItem>();
            var escalated = new List<CallTicketListItem>();
            var overdue = new List<CallTicketListItem>();
            var orderedTableRows = new List<CallTicketListItem>();

            foreach (var t in tickets.Where(t => t != null))
            {
                if (IsOverdue(t)) { overdue.Add(t); continue; }
                var s = NormalizeStatus(t.Status);
                if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) escalated.Add(t);
                else if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) progress.Add(t);
                else pending.Add(t);
            }

            SetMetric(lblOpenCount, tickets.Count, Color.FromArgb(22, 52, 86));
            SetMetric(lblOverdueCount, overdue.Count, overdue.Count > 0 ? Color.FromArgb(192, 57, 43) : Color.FromArgb(22, 52, 86));
            SetMetric(lblEscalatedCount, escalated.Count, escalated.Count > 0 ? Color.FromArgb(211, 84, 0) : Color.FromArgb(22, 52, 86));
            SetMetric(lblUnassignedCount, tickets.Count(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson)), Color.FromArgb(73, 80, 87));
            _lastTicketCount = tickets.Count;
            _lastOverdueCount = overdue.Count;
            _lastEscalatedCount = escalated.Count;
            _lastUnassignedCount = tickets.Count(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson));

            BindColumn(flpPending, lblPendingHeader, "Pending", pending);
            BindColumn(flpInProgress, lblInProgressHeader, "In Progress", progress);
            BindColumn(flpEscalated, lblEscalatedHeader, "Escalated", escalated);
            BindColumn(flpOverdue, lblOverdueHeader, "Overdue", overdue);

            orderedTableRows.AddRange(overdue.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            orderedTableRows.AddRange(escalated.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            orderedTableRows.AddRange(progress.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            orderedTableRows.AddRange(pending.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            BindTable(orderedTableRows);
            UpdateViewSummary(_lastTicketCount, _lastOverdueCount, _lastEscalatedCount, _lastUnassignedCount);
        }

        private void BindColumn(FlowLayoutPanel host, Label header, string title, List<CallTicketListItem> tickets)
        {
            host.SuspendLayout();
            host.Controls.Clear();
            var ordered = tickets.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt).ToList();
            header.Text = string.Format("{0} ({1})", title, ordered.Count);
            if (ordered.Count == 0) host.Controls.Add(EmptyCard(EmptyText(title)));
            else foreach (var t in ordered) host.Controls.Add(TicketCard(t));
            host.ResumeLayout();
            ResizeColumn(host);
        }

        private Control TicketCard(CallTicketListItem ticket)
        {
            var accent = Accent(ticket);
            var card = new Panel { Width = 300, Height = 226, BackColor = Color.White, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(16, 14, 14, 14), Cursor = _openTicket != null ? Cursors.Hand : Cursors.Default, Tag = ticket };
            card.Paint += (_, e) =>
            {
                using (var b = new SolidBrush(accent)) e.Graphics.FillRectangle(b, 0, 0, 7, card.Height);
                using (var p = new Pen(Color.FromArgb(225, 230, 236))) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            var top = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.Transparent };
            var code = new Label { Dock = DockStyle.Left, Width = 148, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(22, 52, 86), TextAlign = ContentAlignment.MiddleLeft, Text = ticket.TicketCode ?? string.Format("#{0}", ticket.TicketId) };
            var priority = Badge(Safe(ticket.Priority, "Unspecified").ToUpperInvariant(), accent, Color.White, new Size(102, 24));
            priority.Dock = DockStyle.Right;
            top.Controls.Add(priority);
            top.Controls.Add(code);

            var issue = new Label { Dock = DockStyle.Top, Height = 50, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(44, 62, 80), Text = Safe(ticket.Issue, "(No issue summary)"), AutoEllipsis = true };
            var context = new Label { Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 9.25F), ForeColor = Color.FromArgb(93, 109, 126), Text = string.Format("{0} | {1}", Safe(ticket.Branch, "-"), Safe(ticket.Department, "-")), AutoEllipsis = true };
            var owner = new Label { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.25F), ForeColor = Color.FromArgb(73, 80, 87), Text = string.Format("Owner: {0}", Safe(ticket.ResponsiblePerson, "Unassigned")), AutoEllipsis = true };

            var metrics = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
            metrics.Controls.Add(Chip("Age " + ShortDuration(TimeSpan.FromDays(Math.Max(0, ticket.TicketAgeDays))), Color.FromArgb(235, 245, 255), Color.FromArgb(30, 87, 153)));
            metrics.Controls.Add(Chip("Idle " + ShortDuration(TimeSpan.FromDays(Math.Max(0, ticket.IdleDays))), Color.FromArgb(255, 244, 230), Color.FromArgb(166, 95, 26)));
            if (!string.IsNullOrWhiteSpace(ticket.CallerName)) metrics.Controls.Add(Chip(FitChipLabelText("Caller", ticket.CallerName, 18), Color.FromArgb(240, 247, 240), Color.FromArgb(39, 94, 54)));

            card.Controls.Add(metrics);
            card.Controls.Add(owner);
            card.Controls.Add(context);
            card.Controls.Add(issue);
            card.Controls.Add(top);
            WireClick(card); WireClick(top); WireClick(code); WireClick(priority); WireClick(issue); WireClick(context); WireClick(owner); WireClick(metrics);
            return card;
        }

        private Control EmptyCard(string text)
        {
            var panel = new Panel { Width = 300, Height = 118, BackColor = Color.White, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(16) };
            panel.Paint += (_, e) => { using (var p = new Pen(Color.FromArgb(226, 232, 240))) e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1); };
            panel.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(108, 117, 125), Text = text });
            return panel;
        }

        private void WireClick(Control control)
        {
            control.DoubleClick += (_, __) =>
            {
                if (_openTicket == null) return;
                var source = control;
                while (source != null && !(source.Tag is CallTicketListItem)) source = source.Parent;
                var ticket = source != null ? source.Tag as CallTicketListItem : null;
                if (ticket != null) _openTicket(ticket.TicketId);
            };
        }

        private static Control SummaryCard(string title, string caption, Label value, Color accent, Color surface)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = surface, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(16, 14, 16, 12) };
            panel.Paint += (_, e) =>
            {
                using (var b = new SolidBrush(accent)) e.Graphics.FillRectangle(b, 0, 0, panel.Width, 5);
                using (var p = new Pen(Color.FromArgb(224, 229, 235))) e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            panel.Controls.Add(value);
            panel.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 22, Font = new Font("Segoe UI", 8.75F), ForeColor = Color.FromArgb(108, 117, 125), Text = caption });
            panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(73, 80, 87), Text = title });
            value.Dock = DockStyle.Fill;
            return panel;
        }

        private static Label SummaryValue() => new Label { Text = "0", Font = new Font("Segoe UI", 24F, FontStyle.Bold), ForeColor = Color.FromArgb(22, 52, 86), TextAlign = ContentAlignment.MiddleLeft };
        private static Label InfoChip(string text, Color back, Color fore) => new Label { AutoSize = true, Text = text, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 10, 0) };
        private static Label ColumnHeader() => new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0) };
        private static FlowLayoutPanel ColumnFlow(Color surface) => new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = surface, Padding = new Padding(10), Margin = new Padding(0) };

        private static Control ColumnPanel(Label header, FlowLayoutPanel flow, Color accent, Color surface)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0), BackColor = Color.White };
            panel.Paint += (_, e) => { using (var p = new Pen(Color.FromArgb(221, 227, 234))) e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1); };
            var top = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = accent };
            top.Controls.Add(header);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = surface };
            body.Controls.Add(flow);
            panel.Controls.Add(body);
            panel.Controls.Add(top);
            return panel;
        }

        private void ShowLoadError(string message)
        {
            _lastTicketCount = 0;
            _lastOverdueCount = 0;
            _lastEscalatedCount = 0;
            _lastUnassignedCount = 0;
            lblOpenCount.Text = lblOverdueCount.Text = lblEscalatedCount.Text = lblUnassignedCount.Text = "-";
            BindError(flpPending, lblPendingHeader, "Pending", message);
            BindError(flpInProgress, lblInProgressHeader, "In Progress", message);
            BindError(flpEscalated, lblEscalatedHeader, "Escalated", message);
            BindError(flpOverdue, lblOverdueHeader, "Overdue", message);
            dgvTable.Rows.Clear();
            if (lblTableEmpty != null)
            {
                lblTableEmpty.Visible = true;
                lblTableEmpty.Text = "Unable to load tickets.\r\n" + Safe(message, "Refresh again to retry.");
            }
            lblLastRefresh.Text = "Refresh failed: " + message;
            UpdateViewSummary();
        }

        private void BindError(FlowLayoutPanel host, Label header, string title, string message)
        {
            header.Text = title;
            host.Controls.Clear();
            host.Controls.Add(ErrorCard(message));
            ResizeColumn(host);
        }

        private Control ErrorCard(string message)
        {
            var panel = new Panel { Width = 300, Height = 118, BackColor = Color.FromArgb(255, 244, 244), Margin = new Padding(0, 0, 0, 12), Padding = new Padding(16) };
            panel.Paint += (_, e) => { using (var p = new Pen(Color.FromArgb(241, 196, 196))) e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1); };
            panel.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(192, 57, 43), Text = Safe(message, "Unable to load tickets.") });
            return panel;
        }

        private void ConfigureTableColumns()
        {
            dgvTable.Columns.Clear();
            dgvTable.Columns.Add(TextColumn("colCode", "Ticket", 90));
            dgvTable.Columns.Add(TextColumn("colIssue", "Issue", 240));
            dgvTable.Columns.Add(TextColumn("colStatus", "Status", 95));
            dgvTable.Columns.Add(TextColumn("colPriority", "Priority", 85));
            dgvTable.Columns.Add(TextColumn("colBranch", "Branch", 130));
            dgvTable.Columns.Add(TextColumn("colDepartment", "Department", 130));
            dgvTable.Columns.Add(TextColumn("colOwner", "Assigned To", 140));
            dgvTable.Columns.Add(TextColumn("colAge", "Age", 70));
            dgvTable.Columns.Add(TextColumn("colIdle", "Idle", 70));
            dgvTable.Columns.Add(TextColumn("colLastActivity", "Last Activity", 135));
        }

        private DataGridViewTextBoxColumn TextColumn(string name, string header, int minWidth)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                MinimumWidth = minWidth,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = minWidth
            };
        }

        private void BindTable(List<CallTicketListItem> rows)
        {
            dgvTable.Rows.Clear();

            foreach (var ticket in rows)
            {
                var status = IsOverdue(ticket) ? "Overdue" : NormalizeStatus(ticket.Status);
                var rowIndex = dgvTable.Rows.Add(
                    ticket.TicketCode ?? string.Format("#{0}", ticket.TicketId),
                    Safe(ticket.Issue, "(No issue summary)"),
                    status,
                    Safe(ticket.Priority, "-"),
                    Safe(ticket.Branch, "-"),
                    Safe(ticket.Department, "-"),
                    Safe(ticket.ResponsiblePerson, "Unassigned"),
                    ShortDuration(TimeSpan.FromDays(Math.Max(0, ticket.TicketAgeDays))),
                    ShortDuration(TimeSpan.FromDays(Math.Max(0, ticket.IdleDays))),
                    ToLocalDisplay(GetLastActivityUtc(ticket)));

                var gridRow = dgvTable.Rows[rowIndex];
                gridRow.Tag = ticket;
                gridRow.DefaultCellStyle.BackColor = rowIndex % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);
                gridRow.Cells["colStatus"].Style.ForeColor = status.Equals("Overdue", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(192, 57, 43)
                    : AccentForStatus(status);
                gridRow.Cells["colStatus"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                gridRow.Cells["colPriority"].Style.ForeColor = Accent(ticket);
                gridRow.Cells["colPriority"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }

            if (lblTableEmpty != null)
            {
                lblTableEmpty.Visible = rows == null || rows.Count == 0;
                lblTableEmpty.Text = "No open tickets to show in table view.";
                lblTableEmpty.BringToFront();
            }
        }

        private void HookColumn(FlowLayoutPanel host) { host.SizeChanged += (_, __) => ResizeColumn(host); }

        private void ResizeColumn(FlowLayoutPanel host)
        {
            if (host == null || host.IsDisposed) return;
            var width = Math.Max(220, host.ClientSize.Width - host.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
            foreach (Control child in host.Controls) child.Width = width;
        }

        private bool IsOverdue(CallTicketListItem ticket)
        {
            if (ticket == null) return false;
            var lastActivity = GetLastActivityUtc(ticket);
            var idle = DateTime.UtcNow - lastActivity;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            return idle >= TimeSpan.FromDays(Math.Max(1, _overdueDays));
        }

        private static DateTime GetLastActivityUtc(CallTicketListItem ticket)
        {
            if (ticket == null) return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            if (ticket.LastContactAt.HasValue)
                return ticket.LastContactAt.Value.Kind == DateTimeKind.Utc ? ticket.LastContactAt.Value : DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Local).ToUniversalTime();
            var updated = ticket.UpdatedAt;
            if (updated.Kind == DateTimeKind.Utc) return updated;
            if (updated.Kind == DateTimeKind.Unspecified) updated = DateTime.SpecifyKind(updated, DateTimeKind.Local);
            return updated.ToUniversalTime();
        }

        private static string NormalizeStatus(string status)
        {
            var value = (status ?? string.Empty).Trim();
            if (value.Equals("Waiting on Vendor", StringComparison.OrdinalIgnoreCase) || value.Equals("Waiting on Department", StringComparison.OrdinalIgnoreCase)) return "In Progress";
            return string.IsNullOrWhiteSpace(value) ? "Pending" : value;
        }

        private static string ShortDuration(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = value.Duration();
            var d = value.Days; var h = value.Hours; var m = value.Minutes;
            if (d > 0) return h > 0 ? string.Format("{0}d {1}h", d, h) : string.Format("{0}d", d);
            if (h > 0) return m > 0 ? string.Format("{0}h {1}m", h, m) : string.Format("{0}h", h);
            return string.Format("{0}m", Math.Max(1, m));
        }

        private static int GetPriorityRank(CallTicketListItem ticket)
        {
            var p = (ticket != null ? ticket.Priority : null) ?? string.Empty;
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return 4;
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return 3;
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return 2;
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        private void UpdateViewSummary(int totalTickets = -1, int overdueCount = -1, int escalatedCount = -1, int unassignedCount = -1)
        {
            if (lblViewSummary == null)
                return;

            var mode = tabViews != null && tabViews.SelectedIndex == 1 ? "Table" : "Board";
            if (totalTickets < 0)
            {
                totalTickets = _lastTicketCount;
                overdueCount = _lastOverdueCount;
                escalatedCount = _lastEscalatedCount;
                unassignedCount = _lastUnassignedCount;
            }

            lblViewSummary.Text = string.Format(
                "{0} | {1} open | {2} overdue | {3} escalated | {4} unassigned",
                mode,
                totalTickets,
                overdueCount,
                escalatedCount,
                unassignedCount);
        }

        private void UpdateActionHint()
        {
            if (lblActionHint == null)
                return;

            lblActionHint.Text = tabViews != null && tabViews.SelectedIndex == 1
                ? "Double-click any row to open ticket details"
                : "Double-click any card to open ticket details";
        }

        private static string FitChipText(string prefix, string value, int maxLength)
        {
            var text = Safe(value, string.Empty);
            if (text.Length > maxLength)
                text = text.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd() + "…";

            return string.IsNullOrWhiteSpace(text) ? prefix : prefix + " " + text;
        }

        private static string FitChipLabelText(string prefix, string value, int maxLength)
        {
            var text = Safe(value, string.Empty);
            if (text.Length > maxLength)
                text = text.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";

            return string.IsNullOrWhiteSpace(text) ? prefix : prefix + " " + text;
        }

        private static Color Accent(CallTicketListItem ticket)
        {
            if (ticket == null) return Color.FromArgb(52, 73, 94);
            var p = (ticket.Priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(192, 57, 43);
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(230, 126, 34);
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(52, 152, 219);
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(46, 204, 113);
            return Color.FromArgb(108, 117, 125);
        }

        private static Color AccentForStatus(string status)
        {
            var s = NormalizeStatus(status);
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(211, 84, 0);
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(142, 68, 173);
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(41, 128, 185);
            return Color.FromArgb(73, 80, 87);
        }

        private static void SetMetric(Label label, int value, Color color) { label.Text = value.ToString(); label.ForeColor = color; }
        private static Label Badge(string text, Color back, Color fore, Size size) => new Label { AutoSize = false, Size = size, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 8.75F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Text = text };
        private static Label Chip(string text, Color back, Color fore) => new Label { AutoSize = true, Text = text, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Padding = new Padding(9, 4, 9, 4), Margin = new Padding(0, 0, 8, 8) };

        private static string EmptyText(string title)
        {
            if (title.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "Nothing is waiting in Pending.";
            if (title.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return "No active work is marked In Progress.";
            if (title.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return "No escalated tickets right now.";
            if (title.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return "No overdue tickets at the moment.";
            return "No tickets in this column.";
        }

        private static string Safe(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string ToLocalDisplay(DateTime utc)
        {
            var local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return local == DateTime.MinValue ? "-" : local.ToString("MMM dd HH:mm");
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            var rect = pnlHeader.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(18, 52, 86), Color.FromArgb(27, 79, 114), LinearGradientMode.Horizontal)) e.Graphics.FillRectangle(brush, rect);
            using (var overlay = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) e.Graphics.FillEllipse(overlay, rect.Width - 220, -60, 260, 180);
            using (var pen = new Pen(Color.FromArgb(18, 52, 86))) e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        }

        private void ExportToExcel()
        {
            if (_currentTickets == null || _currentTickets.Count == 0)
            {
                MessageBox.Show("No ticket data to export. Please refresh the data first.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.FileName = string.Format("ITCM_Tickets_{0:yyyyMMdd_HHmmss}", DateTime.Now);
                saveDialog.Title = "Export Tickets to CSV";

                if (saveDialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    ExportToCsv(saveDialog.FileName);
                    MessageBox.Show("Export completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToCsv(string filePath)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Ticket Code,Issue,Status,Priority,Branch,Department,Assigned To,Caller,Age Days,Idle Days,Created At,Last Activity");

            foreach (var ticket in _currentTickets)
            {
                var status = IsOverdue(ticket) ? "Overdue" : NormalizeStatus(ticket.Status);
                var line = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",{8},{9},\"{10:yyyy-MM-dd HH:mm:ss}\",\"{11}\"",
                    EscapeCsv(ticket.TicketCode ?? "#" + ticket.TicketId),
                    EscapeCsv(ticket.Issue),
                    EscapeCsv(status),
                    EscapeCsv(ticket.Priority),
                    EscapeCsv(ticket.Branch ?? "-"),
                    EscapeCsv(ticket.Department ?? "-"),
                    EscapeCsv(ticket.ResponsiblePerson ?? "Unassigned"),
                    EscapeCsv(ticket.CallerName ?? "-"),
                    ticket.TicketAgeDays,
                    ticket.IdleDays,
                    ticket.CreatedAt.ToLocalTime(),
                    ToLocalDisplay(GetLastActivityUtc(ticket)));
                csv.AppendLine(line);
            }

            System.IO.File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }

        private void ExportToPdf()
        {
            if (_currentTickets == null || _currentTickets.Count == 0)
            {
                MessageBox.Show("No ticket data to export. Please refresh the data first.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PDF files (*.pdf)|*.pdf";
                saveDialog.FileName = string.Format("ITCM_Tickets_{0:yyyyMMdd_HHmmss}", DateTime.Now);
                saveDialog.Title = "Export Tickets to PDF";

                if (saveDialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    ExportToPdfFile(saveDialog.FileName);
                    MessageBox.Show("PDF export completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("PDF export failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToPdfFile(string filePath)
        {
            try
            {
                // Try to use iTextSharp if available
                ExportUsingITextSharp(filePath);
            }
            catch (Exception)
            {
                // Fallback to simple report
                ExportSimplePdf(filePath);
            }
        }

        private void ExportUsingITextSharp(string filePath)
        {
            // Dynamic loading to avoid hard dependency
            var itextAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("iText"));

            if (itextAssembly == null)
                throw new Exception("iTextSharp not available");

            // Use reflection to call iTextSharp methods
            var documentType = itextAssembly.GetType("iTextSharp.text.Document");
            var pdfWriterType = itextAssembly.GetType("iTextSharp.text.pdf.PdfWriter");
            var paragraphType = itextAssembly.GetType("iTextSharp.text.Paragraph");
            var phraseType = itextAssembly.GetType("iTextSharp.text.Phrase");
            var chunkType = itextAssembly.GetType("iTextSharp.text.Chunk");
            var fontType = itextAssembly.GetType("iTextSharp.text.Font");
            var baseColorType = itextAssembly.GetType("iTextSharp.text.BaseColor");
            var pdfPTableType = itextAssembly.GetType("iTextSharp.text.pdf.PdfPTable");
            var pdfPCellType = itextAssembly.GetType("iTextSharp.text.pdf.PdfPCell");
            var elementType = itextAssembly.GetType("iTextSharp.text.Element");
            var rectangleType = itextAssembly.GetType("iTextSharp.text.Rectangle");

            var document = Activator.CreateInstance(documentType);
            var getInstance = pdfWriterType.GetMethod("GetInstance", new[] { documentType, typeof(System.IO.Stream) });

            using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                getInstance.Invoke(null, new object[] { document, fs });
                var openMethod = documentType.GetMethod("Open");
                openMethod.Invoke(document, null);

                // Title
                var titleFont = Activator.CreateInstance(fontType, new object[] { fontType.GetProperty("HELVETICA").GetValue(null), 18f, 1 });
                var title = Activator.CreateInstance(paragraphType, new object[] { "IT Call Monitoring - Ticket Report", titleFont });
                var alignmentProperty = paragraphType.GetProperty("Alignment");
                alignmentProperty.SetValue(title, 1); // CENTER = 1
                var addMethod = documentType.GetMethod("Add", new[] { typeof(object) });
                addMethod.Invoke(document, new[] { title });

                // Date
                var dateFont = Activator.CreateInstance(fontType, new object[] { fontType.GetProperty("HELVETICA").GetValue(null), 10f, 0 });
                var dateText = Activator.CreateInstance(paragraphType, new object[] { string.Format("Generated: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now), dateFont });
                alignmentProperty.SetValue(dateText, 1);
                addMethod.Invoke(document, new[] { dateText });

                // Empty line
                addMethod.Invoke(document, new[] { Activator.CreateInstance(paragraphType, " ") });

                // Table
                var table = Activator.CreateInstance(pdfPTableType, new object[] { 6 });
                var widthPercentageProperty = pdfPTableType.GetProperty("WidthPercentage");
                widthPercentageProperty.SetValue(table, 100f);

                // Headers
                string[] headers = { "Ticket", "Status", "Priority", "Branch/Dept", "Assigned To", "Age/Idle" };
                var headerFont = Activator.CreateInstance(fontType, new object[] { fontType.GetProperty("HELVETICA").GetValue(null), 10f, 1, baseColorType.GetProperty("WHITE").GetValue(null) });

                foreach (var header in headers)
                {
                    var cell = Activator.CreateInstance(pdfPCellType, new object[] { Activator.CreateInstance(phraseType, new object[] { header, headerFont }) });
                    var backgroundColorProperty = pdfPCellType.GetProperty("BackgroundColor");
                    backgroundColorProperty.SetValue(cell, Activator.CreateInstance(baseColorType, new object[] { 18, 52, 86 }));
                    var horizontalAlignmentProperty = pdfPCellType.GetProperty("HorizontalAlignment");
                    horizontalAlignmentProperty.SetValue(cell, 1); // CENTER
                    var addCellMethod = pdfPTableType.GetMethod("AddCell", new[] { pdfPCellType });
                    addCellMethod.Invoke(table, new[] { cell });
                }

                // Data rows
                var dataFont = Activator.CreateInstance(fontType, new object[] { fontType.GetProperty("HELVETICA").GetValue(null), 9f });
                foreach (var ticket in _currentTickets)
                {
                    var status = IsOverdue(ticket) ? "Overdue" : NormalizeStatus(ticket.Status);
                    string[] rowData = {
                        ticket.TicketCode ?? "#" + ticket.TicketId,
                        status,
                        ticket.Priority ?? "-",
                        (ticket.Branch ?? "-") + " / " + (ticket.Department ?? "-"),
                        ticket.ResponsiblePerson ?? "Unassigned",
                        ticket.TicketAgeDays + "d / " + ticket.IdleDays + "d"
                    };

                    foreach (var data in rowData)
                    {
                        var cell = Activator.CreateInstance(pdfPCellType, new object[] { Activator.CreateInstance(phraseType, new object[] { data, dataFont }) });
                        var addCellMethod = pdfPTableType.GetMethod("AddCell", new[] { pdfPCellType });
                        addCellMethod.Invoke(table, new[] { cell });
                    }
                }

                addMethod.Invoke(document, new[] { table });

                // Summary
                addMethod.Invoke(document, new[] { Activator.CreateInstance(paragraphType, " ") });
                var summaryFont = Activator.CreateInstance(fontType, new object[] { fontType.GetProperty("HELVETICA").GetValue(null), 10f, 1 });
                var summary = Activator.CreateInstance(paragraphType, new object[] {
                    string.Format("Total: {0} | Overdue: {1} | Escalated: {2} | Unassigned: {3}",
                        _currentTickets.Count,
                        _currentTickets.Count(t => IsOverdue(t)),
                        _currentTickets.Count(t => NormalizeStatus(t.Status).Equals("Escalated", StringComparison.OrdinalIgnoreCase)),
                        _currentTickets.Count(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson))),
                    summaryFont
                });
                addMethod.Invoke(document, new[] { summary });

                var closeMethod = documentType.GetMethod("Close");
                closeMethod.Invoke(document, null);
            }
        }

        private void ExportSimplePdf(string filePath)
        {
            // Fallback: Generate an HTML-based report and convert or save as HTML
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>ITCM Ticket Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #123456; text-align: center; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            html.AppendLine("th { background-color: #123456; color: white; padding: 8px; text-align: left; }");
            html.AppendLine("td { padding: 6px; border-bottom: 1px solid #ddd; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            html.AppendLine(".critical { color: #c0392b; font-weight: bold; }");
            html.AppendLine(".high { color: #e67e22; font-weight: bold; }");
            html.AppendLine(".overdue { color: #c0392b; }");
            html.AppendLine(".summary { margin-top: 20px; font-weight: bold; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            html.AppendLine("<h1>IT Call Monitoring - Ticket Report</h1>");
            html.AppendLine(string.Format("<p style='text-align:center;'>Generated: {0:yyyy-MM-dd HH:mm:ss}</p>", DateTime.Now));
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Ticket</th><th>Issue</th><th>Status</th><th>Priority</th><th>Branch</th><th>Department</th><th>Assigned To</th><th>Age</th><th>Idle</th></tr>");

            foreach (var ticket in _currentTickets)
            {
                var status = IsOverdue(ticket) ? "Overdue" : NormalizeStatus(ticket.Status);
                var priorityClass = "";
                if (ticket.Priority != null)
                {
                    if (ticket.Priority.Equals("Critical", StringComparison.OrdinalIgnoreCase)) priorityClass = "critical";
                    else if (ticket.Priority.Equals("High", StringComparison.OrdinalIgnoreCase)) priorityClass = "high";
                }
                var statusClass = status.Equals("Overdue", StringComparison.OrdinalIgnoreCase) ? "overdue" : "";

                html.AppendLine(string.Format(
                    "<tr><td>{0}</td><td>{1}</td><td class='{2}'>{3}</td><td class='{4}'>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td><td>{9}d</td><td>{10}d</td></tr>",
                    HtmlEncode(ticket.TicketCode ?? "#" + ticket.TicketId),
                    HtmlEncode(ticket.Issue ?? "-"),
                    statusClass,
                    HtmlEncode(status),
                    priorityClass,
                    HtmlEncode(ticket.Priority ?? "-"),
                    HtmlEncode(ticket.Branch ?? "-"),
                    HtmlEncode(ticket.Department ?? "-"),
                    HtmlEncode(ticket.ResponsiblePerson ?? "Unassigned"),
                    ticket.TicketAgeDays,
                    ticket.IdleDays));
            }

            html.AppendLine("</table>");
            html.AppendLine("<div class='summary'>");
            html.AppendLine(string.Format("Total: {0} | Overdue: {1} | Escalated: {2} | Unassigned: {3}",
                _currentTickets.Count,
                _currentTickets.Count(t => IsOverdue(t)),
                _currentTickets.Count(t => NormalizeStatus(t.Status).Equals("Escalated", StringComparison.OrdinalIgnoreCase)),
                _currentTickets.Count(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson))));
            html.AppendLine("</div>");
            html.AppendLine("</body></html>");

            // Save as HTML - user can print to PDF from browser
            var htmlPath = System.IO.Path.ChangeExtension(filePath, ".html");
            System.IO.File.WriteAllText(htmlPath, html.ToString(), System.Text.Encoding.UTF8);

            // Try to open the HTML file
            try { System.Diagnostics.Process.Start(htmlPath); } catch { }

            MessageBox.Show("PDF library not found. Exported as HTML instead (open in browser and print to PDF).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

    }
}
