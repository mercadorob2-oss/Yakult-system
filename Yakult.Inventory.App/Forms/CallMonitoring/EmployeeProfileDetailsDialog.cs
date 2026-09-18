using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public class EmployeeProfileDetailsDialog : Form
    {
        private readonly CallEmployeeProfileLookup _employee;
        private readonly ICallMonitoringRepository _repo;

        // UI Components
        private Panel pnlLeft;
        private Panel pnlRight;
        private PictureBox picAvatar;
        private Label lblName;
        private Label lblTitle;
        private Label lblQuote;
        private Label lblAutoEscalationSupervisorBadge;
        private Label lblEmailValue;
        private TextBox txtEmail;
        private Button btnSaveEmail;
        private Label lblEmailStatus;
        private FlowLayoutPanel pnlInfo;
        private TableLayoutPanel rowDefaultSupervisor;
        private Label lblDefaultSupervisorValue;
        private LinkLabel lnkDefaultSupervisorView;

        private ComboBox cboRange;
        private Button btnRefresh;
        private Button btnReplacementItems;
        private Label lblPeriodHint;
        private Label lblRefreshed;
        private ToolTip toolTip;

        // Stats Labels
        private Label lblStatSolved;
        private Label lblStatAvgTime;
        private Label lblStatSla;

        // Workload Labels
        private Label lblStatPending;
        private Label lblStatInProgress;
        private Label lblStatEscalated;
        private Label lblStatSolvedAsAssignee;
        private Label lblStatOpenTotal;

        // Recent Activity
        private FlowLayoutPanel pnlRecentActivity;

        private DateTime _lastFromUtc;
        private DateTime _lastToUtc;
        private List<CallTechOpenTicketRow> _cachedOpenTickets;
        private List<CallTechHandledTicketRow> _cachedHandledTickets;
        private bool _isRefreshing;

        public EmployeeProfileDetailsDialog(CallEmployeeProfileLookup employee, ICallMonitoringRepository repo)
        {
            _employee = employee ?? throw new ArgumentNullException(nameof(employee));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            InitializeComponent();
            this.Shown += async (_, __) =>
            {
                await LoadEmployeeEmailAsync();
                await RefreshAsync();
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Size = new Size(1280, 820);
            this.MinimumSize = new Size(1100, 720);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 247, 250); // Light gray background
            this.Text = "Employee Profile";

            toolTip = new ToolTip();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.White
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var divider = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 230, 230) };

            root.Controls.Add(pnlLeft, 0, 0);
            root.Controls.Add(divider, 1, 0);
            root.Controls.Add(pnlRight, 2, 0);

            // --- LEFT PANEL (Bio / Personal) ---
            var leftScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(20, 30, 20, 20)
            };
            pnlLeft.Controls.Add(leftScroll);

            var tlpLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            tlpLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftScroll.Controls.Add(tlpLeft);

            int leftRow = 0;
            void AddLeftRow(Control control)
            {
                tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tlpLeft.Controls.Add(control, 0, leftRow++);
            }

            // Avatar (Circle)
            picAvatar = new PictureBox
            {
                Size = new Size(120, 120),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.FromArgb(220, 220, 220),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 15)
            };
            // Create a circular region for the avatar
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, picAvatar.Width, picAvatar.Height);
            picAvatar.Region = new Region(path);
            
            // Draw a placeholder initial if no image
            picAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                e.Graphics.Clear(ModernUiHelper.ColorPrimary); // Blue background
                var initials = GetInitials(_employee?.EmployeeName);
                using (var brush = new SolidBrush(Color.White))
                using (var font = new Font("Segoe UI", 32, FontStyle.Bold))
                {
                    var size = e.Graphics.MeasureString(initials, font);
                    e.Graphics.DrawString(initials, font, brush, (picAvatar.Width - size.Width) / 2, (picAvatar.Height - size.Height) / 2);
                }
            };

            lblName = new Label
            {
                Text = _employee.EmployeeName,
                AutoSize = true,
                MaximumSize = new Size(220, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 5)
            };

            lblTitle = new Label
            {
                Text = _employee.Position ?? "Employee",
                AutoSize = true,
                MaximumSize = new Size(220, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60), // Alizarin Red
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 15)
            };

            lblAutoEscalationSupervisorBadge = ModernUiHelper.CreateBadge("Auto Escalation Supervisor", Color.FromArgb(46, 204, 113));
            lblAutoEscalationSupervisorBadge.Anchor = AnchorStyles.None;
            lblAutoEscalationSupervisorBadge.Visible = false;
            lblAutoEscalationSupervisorBadge.Margin = new Padding(0, 0, 0, 12);
            toolTip.SetToolTip(lblAutoEscalationSupervisorBadge, "This employee is the selected default supervisor for auto escalations.");

            lblQuote = new Label
            {
                Text = "\"Providing excellent IT support to keep Yakult running smoothly.\"",
                AutoSize = true,
                MaximumSize = new Size(220, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 30)
            };

            // Bio / Info Block
            pnlInfo = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 20)
            };

            pnlInfo.Controls.Add(CreateMetaRow("Dept", "IT Department")); // Placeholder for now or fetch via Repo
            pnlInfo.Controls.Add(CreateMetaRow("Location", "Head Office"));
            pnlInfo.Controls.Add(CreateEmailEditorRow());
            pnlInfo.Controls.Add(CreateMetaRow("Status", "Active", Color.SeaGreen));

            rowDefaultSupervisor = CreateMetaRowWithLink("Supervisor", "-", "View", () => { });
            rowDefaultSupervisor.Visible = false;
            pnlInfo.Controls.Add(rowDefaultSupervisor);

            AddLeftRow(picAvatar);
            AddLeftRow(lblName);
            AddLeftRow(lblTitle);
            AddLeftRow(lblAutoEscalationSupervisorBadge);
            AddLeftRow(lblQuote);
            AddLeftRow(pnlInfo);

            // --- RIGHT PANEL (Stats / Graphs) ---
            var rightScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(28, 24, 28, 24)
            };
            pnlRight.Controls.Add(rightScroll);

            var rightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            rightStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightScroll.Controls.Add(rightStack);

            int rightRow = 0;
            void AddRightRow(Control control)
            {
                rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                rightStack.Controls.Add(control, 0, rightRow++);
            }

            cboRange = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Width = 160,
                Margin = new Padding(0, 0, 8, 0)
            };
            cboRange.Items.Add(new RangePreset("Today", RangePresetKind.Today));
            cboRange.Items.Add(new RangePreset("Last 7 days", RangePresetKind.Last7Days));
            cboRange.Items.Add(new RangePreset("Last 30 days", RangePresetKind.Last30Days));
            cboRange.Items.Add(new RangePreset("Last 90 days", RangePresetKind.Last90Days));
            cboRange.Items.Add(new RangePreset("All time", RangePresetKind.AllTime));
            cboRange.SelectedIndex = 2; // default: last 30 days
            cboRange.SelectedIndexChanged += async (_, __) => await RefreshAsync();

            btnRefresh = ModernUiHelper.CreateSecondaryButton("Refresh");
            btnRefresh.Margin = new Padding(0);
            btnRefresh.Click += async (_, __) => await RefreshAsync(force: true);

            btnReplacementItems = ModernUiHelper.CreateSecondaryButton("Replacement Items");
            btnReplacementItems.Margin = new Padding(0, 0, 0, 0);
            btnReplacementItems.Click += (_, __) =>
            {
                var fromUtc = _lastFromUtc == default ? GetSelectedRangeUtc().FromUtc : _lastFromUtc;
                var toUtc = _lastToUtc == default ? GetSelectedRangeUtc().ToUtc : _lastToUtc;

                using (var dlg = new EmployeeReplacementItemsDialog(_employee, _repo, fromUtc, toUtc))
                {
                    dlg.ShowDialog(this);
                }
            };

            var lblStatsHeader = new Label
            {
                Text = "Performance Stats",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0)
            };

            var headerRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 6)
            };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var headerRight = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0)
            };
            headerRight.Controls.Add(cboRange);
            headerRight.Controls.Add(btnRefresh);
            headerRight.Controls.Add(btnReplacementItems);

            headerRow.Controls.Add(lblStatsHeader, 0, 0);
            headerRow.Controls.Add(headerRight, 1, 0);

            lblPeriodHint = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 2)
            };

            lblRefreshed = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            // Top Row Stats (KPI Cards)
            var tlpStats = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20)
            };
            tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));

            var card1 = CreateStatCard(
                "Tickets Solved",
                "0",
                out lblStatSolved,
                onClickAsync: async () => await ShowHandledTicketsAsync(HandledTicketFilter.SolvedAsSolver),
                tooltipText: "Click to view solved tickets in this period.");
            var card2 = CreateStatCard(
                "Avg Resolution",
                "-",
                out lblStatAvgTime,
                onClickAsync: async () => await ShowHandledTicketsAsync(HandledTicketFilter.SolvedAsAssignee),
                tooltipText: "Click to view handled tickets (as assignee) in this period.");
            var card3 = CreateStatCard(
                "SLA Compliance",
                "-",
                out lblStatSla,
                onClickAsync: async () => await ShowHandledTicketsAsync(HandledTicketFilter.SolvedAsAssignee),
                tooltipText: "Click to view handled tickets (as assignee) in this period.");

            tlpStats.Controls.Add(card1, 0, 0);
            tlpStats.Controls.Add(card2, 1, 0);
            tlpStats.Controls.Add(card3, 2, 0);

            // Current Workload Section
            var lblWorkloadHeader = new Label
            {
                Text = "Current Workload",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 10)
            };

            var tlpWorkload = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20)
            };
            tlpWorkload.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpWorkload.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpWorkload.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));

            tlpWorkload.Controls.Add(
                CreateStatCard(
                    "Pending",
                    "0",
                    out lblStatPending,
                    onClickAsync: async () => await ShowOpenTicketsAsync(OpenTicketFilter.Pending),
                    tooltipText: "Click to view pending tickets."),
                0,
                0);
            tlpWorkload.Controls.Add(
                CreateStatCard(
                    "In Progress",
                    "0",
                    out lblStatInProgress,
                    onClickAsync: async () => await ShowOpenTicketsAsync(OpenTicketFilter.InProgress),
                    tooltipText: "Click to view in progress tickets."),
                1,
                0);
            tlpWorkload.Controls.Add(
                CreateStatCard(
                    "Escalated",
                    "0",
                    out lblStatEscalated,
                    onClickAsync: async () => await ShowOpenTicketsAsync(OpenTicketFilter.Escalated),
                    tooltipText: "Click to view escalated tickets."),
                2,
                0);

            // Resolution History Section
            var lblResHeader = new Label
            {
                Text = "Resolution Activity",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 10)
            };

            var tlpResolution = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20)
            };
            tlpResolution.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpResolution.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            tlpResolution.Controls.Clear();
            tlpResolution.Controls.Add(
                CreateStatCard(
                    "Open Tickets",
                    "0",
                    out lblStatOpenTotal,
                    onClickAsync: async () => await ShowOpenTicketsAsync(OpenTicketFilter.All),
                    tooltipText: "Click to view assigned open tickets."),
                0,
                0);
            tlpResolution.Controls.Add(
                CreateStatCard(
                    "Solved (As Assignee)",
                    "0",
                    out lblStatSolvedAsAssignee,
                    onClickAsync: async () => await ShowHandledTicketsAsync(HandledTicketFilter.SolvedAsAssignee),
                    tooltipText: "Click to view handled tickets (as assignee) in this period."),
                1,
                0);

            AddRightRow(headerRow);
            AddRightRow(lblPeriodHint);
            AddRightRow(lblRefreshed);
            AddRightRow(tlpStats);
            AddRightRow(lblWorkloadHeader);
            AddRightRow(tlpWorkload);
            AddRightRow(lblResHeader);
            AddRightRow(tlpResolution);

            // Recent Activity Section
            var lblRecentHeader = new Label
            {
                Text = "Recent Activity",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 10)
            };

            pnlRecentActivity = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20),
                Padding = new Padding(0),
                Height = 220
            };
            pnlRecentActivity.SizeChanged += (_, __) => SyncRecentActivityRowWidths();

            AddRightRow(lblRecentHeader);
            AddRightRow(pnlRecentActivity);

            this.Controls.Add(root);
            this.ResumeLayout(false);
        }

        private async Task RefreshAsync(bool force = false)
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            try
            {
                btnRefresh.Enabled = false;
                cboRange.Enabled = false;
                lblRefreshed.Text = "Refreshing...";

                var range = GetSelectedRangeUtc();
                _lastFromUtc = range.FromUtc;
                _lastToUtc = range.ToUtc;
                _cachedHandledTickets = null;

                lblPeriodHint.Text = BuildPeriodHint(range.FromUtc, range.ToUtc);

                var summaryTask = _repo.GetTechProfileSummaryAsync(_employee.EmpId, range.FromUtc, range.ToUtc);
                var samplesTask = _repo.GetTechResolutionSamplesAsync(_employee.EmpId, range.FromUtc, range.ToUtc);
                var openTicketsTask = _repo.GetOpenTicketsAssignedToEmployeeAsync(_employee.EmpId, 200);
                await Task.WhenAll(summaryTask, samplesTask, openTicketsTask);

                var summary = summaryTask.Result;

                lblStatSolved.Text = summary.HandledAsSolverSolvedClosed.ToString();
                lblStatPending.Text = summary.AssignedPending.ToString();
                lblStatInProgress.Text = summary.AssignedInProgress.ToString();
                lblStatEscalated.Text = summary.AssignedEscalated.ToString();
                lblStatOpenTotal.Text = summary.AssignedOpenTotal.ToString();
                lblStatSolvedAsAssignee.Text = summary.HandledAsAssigneeSolvedClosed.ToString();

                var samples = samplesTask.Result;
                UpdateResolutionAndSla(samples);

                _cachedOpenTickets = openTicketsTask.Result;
                PopulateRecentActivity((_cachedOpenTickets ?? new List<CallTechOpenTicketRow>())
                    .OrderByDescending(t => t.UpdatedAt)
                    .Take(5)
                    .ToList());

                lblTitle.Text = string.IsNullOrWhiteSpace(_employee.Position) ? "IT Support Specialist" : _employee.Position;
                lblRefreshed.Text = $"Last refreshed: {AppTime.ToLocalString(AppTime.UtcNow, "g")}";

                await UpdateAutoEscalationSupervisorUiAsync();
            }
            catch
            {
                lblRefreshed.Text = "Refresh failed.";
            }
            finally
            {
                cboRange.Enabled = true;
                btnRefresh.Enabled = true;
                _isRefreshing = false;
            }
        }

        private async Task UpdateAutoEscalationSupervisorUiAsync()
        {
            try
            {
                lblAutoEscalationSupervisorBadge.Visible = false;
                rowDefaultSupervisor.Visible = false;

                if (!await _repo.AutoEscalationAssigneeSettingsEnabledAsync())
                    return;

                var supervisorEmpId = await _repo.GetAutoEscalationAssigneeEmpIdAsync();
                if (!supervisorEmpId.HasValue || supervisorEmpId.Value <= 0)
                    return;

                var isThisEmployee = supervisorEmpId.Value == _employee.EmpId;
                lblAutoEscalationSupervisorBadge.Visible = isThisEmployee;

                var supervisor = isThisEmployee
                    ? _employee
                    : await _repo.GetEmployeeProfileLookupByEmpIdAsync(supervisorEmpId.Value);

                lblDefaultSupervisorValue.Text = supervisor?.EmployeeName ?? $"EmpId {supervisorEmpId.Value}";
                lnkDefaultSupervisorView.Enabled = supervisor != null && !isThisEmployee;
                lnkDefaultSupervisorView.Visible = !isThisEmployee;
                lnkDefaultSupervisorView.Tag = supervisor;
                rowDefaultSupervisor.Visible = true;
            }
            catch
            {
                // ignore
            }
        }

        private void UpdateResolutionAndSla(List<CallMonitoringRepository.CallTechResolutionSampleRow> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                lblStatAvgTime.Text = "-";
                lblStatSla.Text = "-";
                toolTip.SetToolTip(lblStatAvgTime, "No completed tickets in the selected period.");
                toolTip.SetToolTip(lblStatSla, "No completed tickets in the selected period.");
                return;
            }

            var durations = new List<double>(samples.Count);
            int compliant = 0;
            int total = 0;

            foreach (var s in samples)
            {
                var createdUtc = AppTime.AssumeUtc(s.CreatedAt);
                var completedUtc = AppTime.AssumeUtc(s.CompletedAtUtc);
                var delta = completedUtc - createdUtc;
                if (delta < TimeSpan.Zero)
                    continue;

                total++;
                durations.Add(delta.TotalHours);

                var target = CallMonitoringSla.GetResolutionSlaTarget(s.Priority, s.IssueType);
                if (delta.TotalHours <= Math.Max(1, target.TargetHours))
                    compliant++;
            }

            if (total == 0)
            {
                lblStatAvgTime.Text = "-";
                lblStatSla.Text = "-";
                toolTip.SetToolTip(lblStatAvgTime, "No completed tickets in the selected period.");
                toolTip.SetToolTip(lblStatSla, "No completed tickets in the selected period.");
                return;
            }

            var avgHours = durations.Count == 0 ? 0 : durations.Average();
            lblStatAvgTime.Text = avgHours < 0.01 ? "0h" : $"{avgHours:0.0}h";

            var pct = (int)Math.Round((double)compliant * 100.0 / total);
            lblStatSla.Text = $"{pct}%";

            toolTip.SetToolTip(lblStatAvgTime, $"{total} completed tickets in period.");
            toolTip.SetToolTip(lblStatSla, $"{compliant}/{total} completed within SLA target.");
        }

        private void PopulateRecentActivity(List<CallTechOpenTicketRow> tickets)
        {
            pnlRecentActivity.Controls.Clear();
            if (tickets == null || tickets.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No active tickets assigned.",
                    AutoSize = true,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                    Margin = new Padding(5, 5, 5, 2)
                };

                var lnk = new LinkLabel
                {
                    Text = "View handled tickets (selected period)",
                    AutoSize = true,
                    LinkColor = ModernUiHelper.ColorPrimary,
                    ActiveLinkColor = ModernUiHelper.ColorPrimary,
                    VisitedLinkColor = ModernUiHelper.ColorPrimary,
                    Margin = new Padding(5, 2, 5, 5)
                };
                lnk.Click += async (_, __) => await ShowHandledTicketsAsync(HandledTicketFilter.SolvedAsAssignee);

                pnlRecentActivity.Controls.Add(lblEmpty);
                pnlRecentActivity.Controls.Add(lnk);
                SyncRecentActivityRowWidths();
                return;
            }

            pnlRecentActivity.Controls.Add(CreateRecentActivityHeaderRow());
            foreach (var ticket in tickets)
            {
                pnlRecentActivity.Controls.Add(CreateTicketRow(ticket));
            }

            SyncRecentActivityRowWidths();
        }

        private void SyncRecentActivityRowWidths()
        {
            if (pnlRecentActivity == null)
                return;

            var width = pnlRecentActivity.ClientSize.Width;
            if (width <= 0)
                return;

            foreach (Control c in pnlRecentActivity.Controls)
            {
                try
                {
                    c.Width = Math.Max(0, width - c.Margin.Horizontal);
                }
                catch
                {
                    // ignore sizing errors
                }
            }
        }

        private Control CreateRecentActivityHeaderRow()
        {
            var row = new Panel
            {
                Height = 28,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(10, 0, 10, 0)
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));

            Label MakeHeader(string text, ContentAlignment align)
            {
                return new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    TextAlign = align,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(120, 130, 140)
                };
            }

            tlp.Controls.Add(MakeHeader("Ticket", ContentAlignment.MiddleLeft), 0, 0);
            tlp.Controls.Add(MakeHeader("Issue", ContentAlignment.MiddleLeft), 1, 0);
            tlp.Controls.Add(MakeHeader("Status", ContentAlignment.MiddleCenter), 2, 0);
            tlp.Controls.Add(MakeHeader("Priority", ContentAlignment.MiddleRight), 3, 0);

            row.Controls.Add(tlp);
            row.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(238, 242, 247)))
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            };

            return row;
        }

        private async Task ShowOpenTicketsAsync(OpenTicketFilter filter)
        {
            if (_cachedOpenTickets == null)
                _cachedOpenTickets = await _repo.GetOpenTicketsAssignedToEmployeeAsync(_employee.EmpId, 300);

            IEnumerable<CallTechOpenTicketRow> rows = _cachedOpenTickets;
            if (filter == OpenTicketFilter.Pending) rows = rows.Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            else if (filter == OpenTicketFilter.InProgress) rows = rows.Where(r => string.Equals(r.Status, "In Progress", StringComparison.OrdinalIgnoreCase));
            else if (filter == OpenTicketFilter.Escalated) rows = rows.Where(r => string.Equals(r.Status, "Escalated", StringComparison.OrdinalIgnoreCase));

            var list = rows.Select(MapOpenRowToListItem).ToList();
            await ShowTicketPickerAsync(list, "Assigned tickets");
        }

        private async Task ShowHandledTicketsAsync(HandledTicketFilter filter)
        {
            if (_cachedHandledTickets == null)
            {
                var fromUtc = _lastFromUtc == default ? GetSelectedRangeUtc().FromUtc : _lastFromUtc;
                var toUtc = _lastToUtc == default ? GetSelectedRangeUtc().ToUtc : _lastToUtc;
                _cachedHandledTickets = await _repo.GetTechHandledTicketsAsync(_employee.EmpId, fromUtc, toUtc, maxRows: 5000);
            }

            IEnumerable<CallTechHandledTicketRow> rows = _cachedHandledTickets;
            if (filter == HandledTicketFilter.SolvedAsAssignee)
                rows = rows.Where(r => r.AssignedToEmpId == _employee.EmpId);
            else if (filter == HandledTicketFilter.SolvedAsSolver)
                rows = _employee.UserId.HasValue ? rows.Where(r => r.CompletedByUserId == _employee.UserId) : Enumerable.Empty<CallTechHandledTicketRow>();

            var list = rows.Select(MapHandledRowToListItem).ToList();
            await ShowTicketPickerAsync(list, "Handled tickets");
        }

        private async Task ShowTicketPickerAsync(List<CallTicketListItem> tickets, string title)
        {
            if (tickets == null) tickets = new List<CallTicketListItem>();
            using (var dlg = new TicketPickerDialog(tickets))
            {
                dlg.Text = title;
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedTicketId > 0)
                {
                    OpenTicketDetails(dlg.SelectedTicketId);
                }
            }
            await Task.CompletedTask;
        }

        private void OpenTicketDetails(int ticketId)
        {
            if (ticketId <= 0)
                return;

            using (var dlg = new TicketDetailsDialog(_repo, ticketId))
            {
                dlg.ShowDialog(this);
            }

            _ = RefreshAsync(force: true);
        }

        private static CallTicketListItem MapOpenRowToListItem(CallTechOpenTicketRow row)
        {
            return new CallTicketListItem
            {
                TicketId = row.TicketId,
                TicketCode = row.TicketCode,
                Issue = row.Issue,
                Status = row.Status,
                Priority = row.Priority,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                CallerName = row.Location
            };
        }

        private static CallTicketListItem MapHandledRowToListItem(CallTechHandledTicketRow row)
        {
            return new CallTicketListItem
            {
                TicketId = row.TicketId,
                TicketCode = row.TicketCode,
                Issue = row.Issue,
                Status = row.Status,
                Priority = row.Priority,
                CreatedAt = row.CreatedAt,
                CallerName = row.Location
            };
        }

        private (DateTime FromUtc, DateTime ToUtc) GetSelectedRangeUtc()
        {
            var nowUtc = AppTime.UtcNow;
            var preset = cboRange?.SelectedItem as RangePreset;
            if (preset == null)
                return (nowUtc.AddDays(-30), nowUtc);

            switch (preset.Kind)
            {
                case RangePresetKind.Today:
                    return (nowUtc.Date, nowUtc);
                case RangePresetKind.Last7Days:
                    return (nowUtc.AddDays(-7), nowUtc);
                case RangePresetKind.Last30Days:
                    return (nowUtc.AddDays(-30), nowUtc);
                case RangePresetKind.Last90Days:
                    return (nowUtc.AddDays(-90), nowUtc);
                case RangePresetKind.AllTime:
                    return (new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), nowUtc);
                default:
                    return (nowUtc.AddDays(-30), nowUtc);
            }
        }

        private string BuildPeriodHint(DateTime fromUtc, DateTime toUtc)
        {
            var preset = cboRange?.SelectedItem as RangePreset;
            if (preset != null && preset.Kind == RangePresetKind.AllTime)
                return "Period: All time";

            return $"Period: {AppTime.ToLocalString(fromUtc, "yyyy-MM-dd")} → {AppTime.ToLocalString(toUtc, "yyyy-MM-dd")}";
        }

        private Control CreateEmailEditorRow()
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 6)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.Controls.Add(new Label
            {
                Text = "Email:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 2, 12, 2)
            }, 0, 0);

            var valuePanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            lblEmailValue = new Label
            {
                Text = "Loading...",
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                MaximumSize = new Size(220, 0),
                Margin = new Padding(0, 2, 0, 2)
            };
            valuePanel.Controls.Add(lblEmailValue);

            if (AppSession.IsAdmin || AppSession.IsDeveloper)
            {
                txtEmail = new TextBox { Width = 220, Margin = new Padding(0, 2, 0, 2) };
                btnSaveEmail = ModernUiHelper.CreateSecondaryButton("Save Email");
                btnSaveEmail.Margin = new Padding(0, 2, 0, 2);
                btnSaveEmail.Click += async (_, __) => await SaveEmployeeEmailAsync();
                lblEmailStatus = new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(220, 0),
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8.5F),
                    Margin = new Padding(0, 0, 0, 2)
                };
                valuePanel.Controls.Add(txtEmail);
                valuePanel.Controls.Add(btnSaveEmail);
                valuePanel.Controls.Add(lblEmailStatus);
            }
            row.Controls.Add(valuePanel, 1, 0);
            return row;
        }

        private async Task LoadEmployeeEmailAsync()
        {
            try
            {
                var email = await _repo.GetEmployeeEmailBindingAsync(_employee.EmpId);
                var display = string.IsNullOrWhiteSpace(email) ? "(No email configured)" : email.Trim();
                if (lblEmailValue != null) lblEmailValue.Text = display;
                if (txtEmail != null) txtEmail.Text = email ?? string.Empty;
                if (lblEmailStatus != null) lblEmailStatus.Text = string.Empty;
            }
            catch (Exception ex)
            {
                if (lblEmailValue != null) lblEmailValue.Text = "(Unable to load email)";
                if (lblEmailStatus != null) lblEmailStatus.Text = ex.Message;
            }
        }

        private async Task SaveEmployeeEmailAsync()
        {
            if (!(AppSession.IsAdmin || AppSession.IsDeveloper) || txtEmail == null)
                return;

            var value = (txtEmail.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                try { _ = new MailAddress(value); }
                catch
                {
                    lblEmailStatus.Text = "Enter a valid email address.";
                    return;
                }
            }
            else if (MessageBox.Show(this, "Clear this employee's primary email address?", "Confirm email removal", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                btnSaveEmail.Enabled = false;
                await _repo.SaveEmployeeEmailBindingAsync(_employee.EmpId, value, AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null);
                await LoadEmployeeEmailAsync();
                lblEmailStatus.Text = string.IsNullOrWhiteSpace(value) ? "Primary email cleared." : "Email saved.";
            }
            catch (Exception ex)
            {
                lblEmailStatus.Text = ex.Message;
            }
            finally
            {
                btnSaveEmail.Enabled = true;
            }
        }

        private Control CreateMetaRow(string label, string value, Color? valueColor = null)
        {
            var p = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 6)
            };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            var l = new Label
            {
                Text = label + ":",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 2, 12, 2)
            };
            var v = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = valueColor ?? Color.Black,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            };
            p.Controls.Add(l, 0, 0);
            p.Controls.Add(v, 1, 0);
            return p;
        }

        private TableLayoutPanel CreateMetaRowWithLink(string label, string value, string linkText, Action onClick)
        {
            var p = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 6)
            };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var l = new Label
            {
                Text = label + ":",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 2, 12, 2)
            };

            lblDefaultSupervisorValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Black,
                AutoSize = true,
                Margin = new Padding(0, 2, 8, 2)
            };

            lnkDefaultSupervisorView = new LinkLabel
            {
                Text = linkText,
                AutoSize = true,
                LinkColor = ModernUiHelper.ColorPrimary,
                ActiveLinkColor = ModernUiHelper.ColorPrimary,
                VisitedLinkColor = ModernUiHelper.ColorPrimary,
                Margin = new Padding(0, 2, 0, 2)
            };
            lnkDefaultSupervisorView.Click += (_, __) =>
            {
                if (lnkDefaultSupervisorView.Tag is CallEmployeeProfileLookup emp && emp.EmpId > 0)
                {
                    using (var dlg = new EmployeeProfileDetailsDialog(emp, _repo))
                    {
                        dlg.ShowDialog(this);
                    }
                }
                else
                {
                    onClick?.Invoke();
                }
            };

            p.Controls.Add(l, 0, 0);
            p.Controls.Add(lblDefaultSupervisorValue, 1, 0);
            p.Controls.Add(lnkDefaultSupervisorView, 2, 0);
            return p;
        }

        private Panel CreateStatCard(string title, string value, out Label lblVal, Func<Task> onClickAsync = null, string tooltipText = null)
        {
            var p = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(10),
                Margin = new Padding(6),
                MinimumSize = new Size(140, 90)
            };

            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                toolTip.SetToolTip(p, tooltipText);
            }
            
            var t = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                toolTip.SetToolTip(t, tooltipText);
            }
            
            lblVal = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorPrimary,
                TextAlign = ContentAlignment.MiddleCenter
            };

            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                toolTip.SetToolTip(lblVal, tooltipText);
            }

            p.Controls.Add(lblVal);
            p.Controls.Add(t);

            if (onClickAsync != null)
            {
                WireClick(p, async () =>
                {
                    try { await onClickAsync(); }
                    catch { }
                });
            }
            return p;
        }

        private Control CreateTicketRow(CallTechOpenTicketRow ticket)
        {
            var row = new Panel
            {
                Height = 44,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(10, 8, 10, 8)
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Ticket | Issue | Status | Priority
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));

            var lblId = new Label
            {
                Text = (ticket.TicketCode ?? string.Empty).Trim(),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorPrimary,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblIssue = new Label
            {
                Text = (ticket.Issue ?? string.Empty).Trim(),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            var lblStatus = ModernUiHelper.CreateBadge(ticket.Status, GetStatusColor(ticket.Status));
            lblStatus.AutoSize = false;
            lblStatus.Dock = DockStyle.None;
            lblStatus.Width = 100;
            lblStatus.Height = 22;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.Anchor = AnchorStyles.None;
            lblStatus.Margin = new Padding(0);

            var statusHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            statusHost.Controls.Add(lblStatus);
            statusHost.Resize += (_, __) =>
            {
                lblStatus.Left = Math.Max(0, (statusHost.ClientSize.Width - lblStatus.Width) / 2);
                lblStatus.Top = Math.Max(0, (statusHost.ClientSize.Height - lblStatus.Height) / 2);
            };

            var lblPriority = new Label
            {
                Text = (ticket.Priority ?? string.Empty).Trim(),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = GetPriorityColor(ticket.Priority),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };

            tlp.Controls.Add(lblId, 0, 0);
            tlp.Controls.Add(lblIssue, 1, 0);
            tlp.Controls.Add(statusHost, 2, 0);
            tlp.Controls.Add(lblPriority, 3, 0);

            row.Controls.Add(tlp);

            // Hover state (improves scan-ability)
            var normalBack = row.BackColor;
            var hoverBack = Color.FromArgb(248, 250, 252);
            void SetHover(bool hovering)
            {
                try { row.BackColor = hovering ? hoverBack : normalBack; }
                catch { }
            }
            row.MouseEnter += (_, __) => SetHover(true);
            row.MouseLeave += (_, __) => SetHover(false);

            // Click-through
            WireClick(row, () => OpenTicketDetails(ticket.TicketId));

            // Bottom border
            row.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(238, 242, 247)))
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            };

            return row;
        }

        private static void WireClick(Control root, Action onClick)
        {
            if (root == null || onClick == null)
                return;

            root.Cursor = Cursors.Hand;
            root.Click += (_, __) => onClick();
            foreach (Control child in root.Controls)
            {
                WireClick(child, onClick);
            }
        }

        private Color GetStatusColor(string status)
        {
            switch (status?.ToLower())
            {
                case "new": return Color.FromArgb(52, 152, 219);
                case "pending": return Color.FromArgb(241, 196, 15);
                case "in progress": return Color.FromArgb(46, 204, 113);
                case "escalated": return Color.FromArgb(231, 76, 60);
                default: return Color.Gray;
            }
        }

        private Color GetPriorityColor(string priority)
        {
            switch (priority?.ToLower())
            {
                case "critical": return Color.FromArgb(192, 57, 43);
                case "high": return Color.FromArgb(230, 126, 34);
                case "medium": return Color.FromArgb(241, 196, 15);
                default: return Color.Gray;
            }
        }



        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var parts = name.Trim().Split(' ');
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpper();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpper();
        }

        private enum OpenTicketFilter { All, Pending, InProgress, Escalated }
        private enum HandledTicketFilter { SolvedAsAssignee, SolvedAsSolver }
        private enum RangePresetKind { Today, Last7Days, Last30Days, Last90Days, AllTime }

        private sealed class RangePreset
        {
            public RangePreset(string name, RangePresetKind kind)
            {
                Name = name;
                Kind = kind;
            }

            public string Name { get; }
            public RangePresetKind Kind { get; }
            public override string ToString() => Name;
        }
    }
}
