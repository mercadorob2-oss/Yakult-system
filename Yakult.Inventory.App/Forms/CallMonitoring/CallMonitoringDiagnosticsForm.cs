using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class CallMonitoringDiagnosticsForm : Form
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly Action<Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget, int?, int?, string> _openEmailDeepLink;

        private Button _btnRefresh;
        private Button _btnCopy;
        private Button _btnExport;
        private Button _btnOpenLogs;
        private Button _btnRunSmtpTest;
        private Button _btnOpenDashboard;
        private Button _btnConnect;
        private Label _lblWebServerValue;
        private Label _lblHeartbeatLastStartedValue;
        private Label _lblHeartbeatLastFinishedValue;
        private Label _lblHeartbeatLastSuccessValue;
        private Label _lblHeartbeatMachineValue;
        private Label _lblHeartbeatVersionValue;

        private Label _lblHealthBadge;
        private Label _lblLastRefreshedValue;
        private Label _lblDbValue;
        private Label _lblServerValue;
        private Label _lblSchedulerTaskStateValue;
        private Label _lblSchedulerEnabledValue;
        private Label _lblSchedulerStatusValue;
        private Label _lblSchedulerLastActivityValue;
        private Label _lblSchedulerSignalsValue;
        private Label _lblSchemaValue;
        private Label _lblCountsValue;
        private Label _lblLastEscalationValue;
        private Label _lblLastEmailFailedValue;
        private TextBox _txtLastEmailFailedDetails;
        private Label _lblLastEmailSkippedValue;
        private TextBox _txtLastEmailSkippedDetails;
        private Label _lblLastReminderEmailValue;
        private Label _lblLastEscalationEmailValue;

        private LinkLabel _lnkEmailSettings;
        private LinkLabel _lnkNotificationRules;
        private LinkLabel _lnkTemplates;
        private LinkLabel _lnkDeptRecipients;
        private LinkLabel _lnkBranchRecipients;
        private LinkLabel _lnkEmailLog;

        private Label _lblJobLastStartValue;
        private Label _lblJobLastEndValue;
        private Label _lblJobNextRunValue;
        private Label _lblJobLockProbeValue;
        private Label _lblJobCountsValue;

        private Label _lblOpenValue;
        private Label _lblPendingValue;
        private Label _lblCriticalValue;
        private Label _lblTodayValue;

        private ListView _lvSchema;

        private CallMonitoringRepository.ItcmDiagnosticsSnapshot _lastSnapshot;
        private int? _lastResolvedTicketId;
        private int? _lastResolvedDeptId;
        private int? _lastResolvedBranchId;

        private readonly HttpClient _itcmPingClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private bool _lastPingConnected;
        private string _lastPingText = "-";
        private string _lastPingUrl = string.Empty;
        private DateTime _lastPingAtUtc = DateTime.MinValue;
        private string _lastPingSummary = "Web Server: -";

        public CallMonitoringDiagnosticsForm(
            ICallMonitoringRepository repo,
            Action<Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget, int?, int?, string> openEmailDeepLink = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _openEmailDeepLink = openEmailDeepLink;
            InitializeComponent();
            this.Load += async (_, __) => await RefreshAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Diagnostics";
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(240, 242, 245);
            this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(16);

            var pnlRoot = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = this.BackColor
            };

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.White,
                Padding = new Padding(16, 12, 16, 12)
            };
            pnlTop.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(225, 230, 235)))
                    e.Graphics.DrawLine(pen, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "Diagnostics",
                AutoSize = true,
                Font = new Font("Segoe UI", 15.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Margin = new Padding(0, 0, 0, 0)
            };

            var lblSubtitle = new Label
            {
                Text = "IT Call Monitoring • Health & email pipeline status",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(120, 130, 140),
                Margin = new Padding(0, 1, 0, 0)
            };

            _btnRefresh = CreateActionButton("Refresh");
            _btnRefresh.BackColor = Color.FromArgb(52, 152, 219);
            _btnRefresh.ForeColor = Color.White;
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Click += async (_, __) => await RefreshAsync();

            _btnCopy = CreateActionButton("Copy");
            _btnCopy.Click += (_, __) => TryCopyToClipboard(BuildDiagnosticsText(_lastSnapshot) + PingSummaryLine());

            _btnExport = CreateActionButton("Export");
            _btnExport.Click += (_, __) => TryExportDiagnostics();

            _btnOpenLogs = CreateActionButton("Open Logs");
            _btnOpenLogs.Click += (_, __) => TryOpenLogFolder();

            _btnRunSmtpTest = CreateActionButton("Run SMTP Test");
            _btnRunSmtpTest.Width = 140;
            _btnRunSmtpTest.Click += async (_, __) => await OpenSmtpTestDialogAsync();

            var flpActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            flpActions.Controls.Add(_btnRefresh);
            flpActions.Controls.Add(_btnRunSmtpTest);
            flpActions.Controls.Add(_btnCopy);
            flpActions.Controls.Add(_btnExport);
            flpActions.Controls.Add(_btnOpenLogs);

            var toolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 500,
                ReshowDelay = 200,
                ShowAlways = true
            };
            toolTip.SetToolTip(_btnRefresh, "Reload diagnostics from the database.");
            toolTip.SetToolTip(_btnCopy, "Copy a text snapshot (safe to paste into support tickets).");
            toolTip.SetToolTip(_btnExport, "Save the diagnostics text snapshot to a .txt file.");
            toolTip.SetToolTip(_btnOpenLogs, "Open the local app log folder.");

            _lblHealthBadge = CreatePillLabel("Loading…", backColor: Color.FromArgb(236, 240, 241), foreColor: Color.FromArgb(44, 62, 80));
            _lblHealthBadge.Margin = new Padding(0, 8, 0, 0);

            _lblLastRefreshedValue = new Label
            {
                Text = "Last refreshed: -",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(120, 130, 140),
                Margin = new Padding(0, 2, 0, 0)
            };

            var flpTitle = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(8, 0, 0, 0)
            };
            flpTitle.Controls.Add(lblTitle);
            flpTitle.Controls.Add(lblSubtitle);
            flpTitle.Controls.Add(_lblHealthBadge);
            flpTitle.Controls.Add(_lblLastRefreshedValue);

            pnlTop.Controls.Add(flpActions);
            pnlTop.Controls.Add(flpTitle);

            var pnlMainScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = this.BackColor,
                Padding = new Padding(0, 12, 0, 0)
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlp.MinimumSize = new Size(0, 600);

            var cardSummary = CreateCard("System Summary", accent: Color.FromArgb(52, 152, 219), out var pnlSummaryBody);
            cardSummary.Dock = DockStyle.Fill;
            cardSummary.Margin = new Padding(0);

            var tlpSummaryRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            tlpSummaryRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 246F));
            tlpSummaryRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblServerValue = CreateValueLabel("-");
            _lblDbValue = CreateValueLabel("-");
            _lblSchedulerTaskStateValue = CreateValueLabel("-");
            _lblSchedulerEnabledValue = CreateValueLabel("-");
            _lblSchedulerStatusValue = CreateValueLabel("-");
            _lblSchedulerLastActivityValue = CreateValueLabel("-");
            _lblSchedulerSignalsValue = CreateValueLabel("-");
            _lblSchemaValue = CreateValueLabel("-");
            _lblCountsValue = CreateValueLabel("-");
            _lblLastEscalationValue = CreateValueLabel("-");

            var tlpSummaryMeta = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            tlpSummaryMeta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            tlpSummaryMeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < tlpSummaryMeta.RowCount; i++)
                tlpSummaryMeta.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Server:"), 0, 0);
            tlpSummaryMeta.Controls.Add(_lblServerValue, 1, 0);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Database:"), 0, 1);
            tlpSummaryMeta.Controls.Add(_lblDbValue, 1, 1);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Task Scheduler:"), 0, 2);
            tlpSummaryMeta.Controls.Add(_lblSchedulerTaskStateValue, 1, 2);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Scheduler Enabled:"), 0, 3);
            tlpSummaryMeta.Controls.Add(_lblSchedulerEnabledValue, 1, 3);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Processing:"), 0, 4);
            tlpSummaryMeta.Controls.Add(_lblSchedulerStatusValue, 1, 4);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Last activity:"), 0, 5);
            tlpSummaryMeta.Controls.Add(_lblSchedulerLastActivityValue, 1, 5);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Signals (24h):"), 0, 6);
            tlpSummaryMeta.Controls.Add(_lblSchedulerSignalsValue, 1, 6);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Schema:"), 0, 7);
            tlpSummaryMeta.Controls.Add(_lblSchemaValue, 1, 7);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Avg resolve:"), 0, 8);
            tlpSummaryMeta.Controls.Add(_lblCountsValue, 1, 8);
            tlpSummaryMeta.Controls.Add(CreateKeyLabel("Last escalation:"), 0, 9);
            tlpSummaryMeta.Controls.Add(_lblLastEscalationValue, 1, 9);

            var lblHint = new Label
            {
                Text = "Tip: Copy and paste this into support tickets for faster triage.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(120, 130, 140),
                Margin = new Padding(0, 4, 0, 0)
            };

            var pnlMeta = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            lblHint.Dock = DockStyle.Bottom;
            pnlMeta.Controls.Add(tlpSummaryMeta);
            pnlMeta.Controls.Add(lblHint);

            var tlpTiles = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            tlpTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            tlpTiles.Controls.Add(CreateMetricTile("Open", Color.FromArgb(41, 128, 185), out _lblOpenValue), 0, 0);
            tlpTiles.Controls.Add(CreateMetricTile("Pending", Color.FromArgb(230, 126, 34), out _lblPendingValue), 1, 0);
            tlpTiles.Controls.Add(CreateMetricTile("Critical", Color.FromArgb(192, 57, 43), out _lblCriticalValue), 2, 0);
            tlpTiles.Controls.Add(CreateMetricTile("Today", Color.FromArgb(39, 174, 96), out _lblTodayValue), 3, 0);

            tlpSummaryRoot.Controls.Add(pnlMeta, 0, 0);
            tlpSummaryRoot.Controls.Add(tlpTiles, 0, 1);

            pnlSummaryBody.Controls.Add(tlpSummaryRoot);

            var cardEmail = CreateCard("Email / Background Signals", accent: Color.FromArgb(46, 204, 113), out var pnlEmailBody);
            cardEmail.Dock = DockStyle.Fill;
            cardEmail.Margin = new Padding(0);
            pnlEmailBody.AutoScroll = true;

            var tlpEmail = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 10,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            tlpEmail.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));

            _lblLastEmailFailedValue = CreateValueLabel("-");
            _txtLastEmailFailedDetails = CreateMultilineBox();
            _lblLastEmailSkippedValue = CreateValueLabel("-");
            _txtLastEmailSkippedDetails = CreateMultilineBox();
            _lblLastReminderEmailValue = CreateValueLabel("-");
            _lblLastEscalationEmailValue = CreateValueLabel("-");
            _lblLastReminderEmailValue.ForeColor = Color.FromArgb(120, 130, 140);
            _lblLastEscalationEmailValue.ForeColor = Color.FromArgb(120, 130, 140);

            tlpEmail.Controls.Add(CreateKeyLabel("Last FAILED email:"), 0, 0);
            tlpEmail.Controls.Add(WrapWithPanel(_lblLastEmailFailedValue, _txtLastEmailFailedDetails), 0, 1);
            tlpEmail.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Fill }, 0, 2);
            tlpEmail.Controls.Add(CreateKeyLabel("Last SKIPPED email:"), 0, 3);
            tlpEmail.Controls.Add(WrapWithPanel(_lblLastEmailSkippedValue, _txtLastEmailSkippedDetails), 0, 4);
            tlpEmail.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Fill }, 0, 5);
            tlpEmail.Controls.Add(CreateKeyLabel("Last reminder email:"), 0, 6);
            tlpEmail.Controls.Add(_lblLastReminderEmailValue, 0, 7);
            tlpEmail.Controls.Add(CreateKeyLabel("Last escalation email:"), 0, 8);
            tlpEmail.Controls.Add(_lblLastEscalationEmailValue, 0, 9);

            var pnlDeepLinks = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 6)
            };

            var flpLinks = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _lnkEmailSettings = CreateDeepLink("Email Settings", () => OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.SetupSmtp));
            _lnkNotificationRules = CreateDeepLink("Notification Rules", () => OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.SetupNotificationRules));
            _lnkTemplates = CreateDeepLink("Templates", () => OpenTemplatesDeepLink());
            _lnkDeptRecipients = CreateDeepLink("Dept Recipients", () =>
            {
                var deptId = _lastResolvedDeptId;
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.DepartmentRecipients, deptId: deptId);
            });
            _lnkBranchRecipients = CreateDeepLink("Branch Recipients", () =>
            {
                var branchId = _lastResolvedBranchId;
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.BranchRecipients, branchId: branchId);
            });
            _lnkEmailLog = CreateDeepLink("Email Log", () =>
            {
                var ticketId = _lastResolvedTicketId;
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.EmailLog, emailLogSearch: ticketId.HasValue ? ticketId.Value.ToString() : null);
            });

            flpLinks.Controls.Add(_lnkEmailSettings);
            flpLinks.Controls.Add(_lnkNotificationRules);
            flpLinks.Controls.Add(_lnkTemplates);
            flpLinks.Controls.Add(_lnkDeptRecipients);
            flpLinks.Controls.Add(_lnkBranchRecipients);
            flpLinks.Controls.Add(_lnkEmailLog);

            var lblLinks = CreateKeyLabel("Quick links:");
            lblLinks.Margin = new Padding(0, 0, 0, 4);
            pnlDeepLinks.Controls.Add(flpLinks);
            pnlDeepLinks.Controls.Add(lblLinks);
            lblLinks.Dock = DockStyle.Top;

            var pnlJob = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 0)
            };

            var lblJobTitle = CreateKeyLabel("Job status:");
            lblJobTitle.Margin = new Padding(0, 0, 0, 6);

            var tlpJob = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlpJob.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            tlpJob.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < tlpJob.RowCount; i++)
                tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));

            _lblJobLastStartValue = CreateValueLabel("-");
            _lblJobLastEndValue = CreateValueLabel("-");
            _lblJobNextRunValue = CreateValueLabel("-");
            _lblJobLockProbeValue = CreateValueLabel("-");
            _lblJobCountsValue = CreateValueLabel("-");

            tlpJob.Controls.Add(CreateKeyLabel("Last run start:"), 0, 0);
            tlpJob.Controls.Add(_lblJobLastStartValue, 1, 0);
            tlpJob.Controls.Add(CreateKeyLabel("Last run end:"), 0, 1);
            tlpJob.Controls.Add(_lblJobLastEndValue, 1, 1);
            tlpJob.Controls.Add(CreateKeyLabel("Next run:"), 0, 2);
            tlpJob.Controls.Add(_lblJobNextRunValue, 1, 2);
            tlpJob.Controls.Add(CreateKeyLabel("Lock (probe):"), 0, 3);
            tlpJob.Controls.Add(_lblJobLockProbeValue, 1, 3);
            tlpJob.Controls.Add(CreateKeyLabel("Counts:"), 0, 4);
            tlpJob.Controls.Add(_lblJobCountsValue, 1, 4);

            pnlJob.Controls.Add(tlpJob);
            pnlJob.Controls.Add(lblJobTitle);
            lblJobTitle.Dock = DockStyle.Top;

            // DockTop stacking: add job first (bottom), links next, then email rows last (top).
            pnlEmailBody.Controls.Add(pnlJob);
            pnlEmailBody.Controls.Add(pnlDeepLinks);
            pnlEmailBody.Controls.Add(tlpEmail);

            var cardSchema = CreateCard("Schema Checks", accent: Color.FromArgb(155, 89, 182), out var pnlSchemaBody);
            cardSchema.Dock = DockStyle.Fill;
            cardSchema.Margin = new Padding(0);
            cardSchema.MinimumSize = new Size(0, 220);

            _lvSchema = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None
            };
            typeof(ListView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_lvSchema, true, null);
            _lvSchema.Columns.Add("Area", 100);
            _lvSchema.Columns.Add("Object", 260);
            _lvSchema.Columns.Add("Status", 90);
            _lvSchema.Columns.Add("Details", 420);
            _lvSchema.DoubleClick += (_, __) =>
            {
                try
                {
                    var item = _lvSchema.SelectedItems.Count > 0 ? _lvSchema.SelectedItems[0] : null;
                    var obj = item?.SubItems.Count > 1 ? item.SubItems[1].Text : null;
                    if (!string.IsNullOrWhiteSpace(obj))
                        TryCopyToClipboard(obj);
                }
                catch
                {
                }
            };

            pnlSchemaBody.Controls.Add(_lvSchema);

            var cardSchedulerStatus = CreateCard("Scheduler Status", accent: Color.FromArgb(230, 126, 34), out var pnlSchedulerStatusBody);
            cardSchedulerStatus.Dock = DockStyle.Fill;
            cardSchedulerStatus.Margin = new Padding(0);
            cardSchedulerStatus.MinimumSize = new Size(0, 220);

            _btnConnect = CreateActionButton("Connect");
            _btnConnect.BackColor = Color.FromArgb(39, 174, 96);
            _btnConnect.ForeColor = Color.White;
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.Click += async (_, __) => await ConnectToItcmServerAsync(manual: true);

            _btnOpenDashboard = CreateActionButton("Open Web Dashboard");
            _btnOpenDashboard.BackColor = Color.FromArgb(41, 98, 255);
            _btnOpenDashboard.ForeColor = Color.White;
            _btnOpenDashboard.FlatAppearance.BorderSize = 0;
            _btnOpenDashboard.Click += (_, __) => OpenWebDashboard();

            toolTip.SetToolTip(_btnConnect, "Test the connection to the ITCM web server.");

            var flpServerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(0)
            };
            flpServerActions.Controls.Add(_btnConnect);
            flpServerActions.Controls.Add(_btnOpenDashboard);

            _lblHeartbeatLastStartedValue = CreateValueLabel("-");
            _lblHeartbeatLastFinishedValue = CreateValueLabel("-");
            _lblHeartbeatLastSuccessValue = CreateValueLabel("-");
            _lblHeartbeatMachineValue = CreateValueLabel("-");
            _lblHeartbeatVersionValue = CreateValueLabel("-");
            _lblWebServerValue = CreateValueLabel("-");

            var tlpHeartbeat = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 6,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlpHeartbeat.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tlpHeartbeat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 6; i++)
                tlpHeartbeat.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpHeartbeat.Controls.Add(CreateKeyLabel("Last started:"), 0, 0);
            tlpHeartbeat.Controls.Add(_lblHeartbeatLastStartedValue, 1, 0);
            tlpHeartbeat.Controls.Add(CreateKeyLabel("Last finished:"), 0, 1);
            tlpHeartbeat.Controls.Add(_lblHeartbeatLastFinishedValue, 1, 1);
            tlpHeartbeat.Controls.Add(CreateKeyLabel("Last success:"), 0, 2);
            tlpHeartbeat.Controls.Add(_lblHeartbeatLastSuccessValue, 1, 2);
            tlpHeartbeat.Controls.Add(CreateKeyLabel("Machine:"), 0, 3);
            tlpHeartbeat.Controls.Add(_lblHeartbeatMachineValue, 1, 3);
            tlpHeartbeat.Controls.Add(CreateKeyLabel("Version:"), 0, 4);
            tlpHeartbeat.Controls.Add(_lblHeartbeatVersionValue, 1, 4);
            tlpHeartbeat.Controls.Add(CreateKeyLabel("Web Server:"), 0, 5);
            tlpHeartbeat.Controls.Add(_lblWebServerValue, 1, 5);

            var lblSchedulerStatusHint = new Label
            {
                Text = "The ITCM Scheduler runs as a centralized web service. Use the dashboard to view run history, settings, and logs.",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(120, 130, 140),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 0, 0)
            };

            pnlSchedulerStatusBody.Controls.Add(lblSchedulerStatusHint);
            pnlSchedulerStatusBody.Controls.Add(tlpHeartbeat);
            pnlSchedulerStatusBody.Controls.Add(flpServerActions);

            var pnlLeft = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
            var pnlRight = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            
            AddTabbedCardsToContainer(pnlLeft, cardSummary, "System Summary", cardSchema, "Schema Checks");
            AddTabbedCardsToContainer(pnlRight, cardEmail, "Email / Background", cardSchedulerStatus, "Scheduler Status");
            
            tlp.Controls.Add(pnlLeft, 0, 0);
            tlp.Controls.Add(pnlRight, 1, 0);

            pnlMainScroll.Controls.Add(tlp);
            pnlRoot.Controls.Add(pnlMainScroll);
            pnlRoot.Controls.Add(pnlTop);

            this.Controls.Add(pnlRoot);
            this.ResumeLayout(false);
        }

        private static void AddTabbedCardsToContainer(Panel container, Panel card1, string tab1Text, Panel card2, string tab2Text)
        {
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 0, 4)
            };

            var btnTab1 = new Button
            {
                Text = tab1Text,
                Height = 32,
                Width = 160,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnTab1.FlatAppearance.BorderSize = 0;
            
            var btnTab2 = new Button
            {
                Text = tab2Text,
                Height = 32,
                Width = 160,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnTab2.FlatAppearance.BorderSize = 0;

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            header.Controls.Add(btnTab1);
            header.Controls.Add(btnTab2);
            container.Controls.Add(body);
            container.Controls.Add(header);

            card1.Dock = DockStyle.Fill;
            card1.Margin = new Padding(0);
            card2.Dock = DockStyle.Fill;
            card2.Margin = new Padding(0);
            
            body.Controls.Add(card2);
            body.Controls.Add(card1);

            Action<bool> setActiveTab = (showTab1) =>
            {
                if (showTab1)
                {
                    card1.Visible = true;
                    card1.BringToFront();
                    card2.Visible = false;
                    btnTab1.BackColor = Color.FromArgb(52, 152, 219);
                    btnTab1.ForeColor = Color.White;
                    btnTab2.BackColor = Color.FromArgb(232, 236, 240);
                    btnTab2.ForeColor = Color.FromArgb(120, 130, 140);
                }
                else
                {
                    card2.Visible = true;
                    card2.BringToFront();
                    card1.Visible = false;
                    btnTab2.BackColor = Color.FromArgb(52, 152, 219);
                    btnTab2.ForeColor = Color.White;
                    btnTab1.BackColor = Color.FromArgb(232, 236, 240);
                    btnTab1.ForeColor = Color.FromArgb(120, 130, 140);
                }
            };

            btnTab1.Click += (s, e) => setActiveTab(true);
            btnTab2.Click += (s, e) => setActiveTab(false);

            setActiveTab(true);
        }

        private static Panel CreateCard(string title, Color accent, out Panel body)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(16, 14, 16, 16)
            };

            card.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(225, 230, 235)))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.Transparent
            };

            var bar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = accent
            };

            var lbl = new Label
            {
                Text = title ?? string.Empty,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            header.Controls.Add(lbl);
            header.Controls.Add(bar);

            body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 0)
            };

            card.Controls.Add(body);
            card.Controls.Add(header);
            return card;
        }

        private static Button CreateActionButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Height = 32,
                Width = 110,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(44, 62, 80),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(220, 226, 232);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        private static Button CreateIconActionButton(string icon)
        {
            var button = CreateActionButton(string.Empty);
            button.Width = 36;
            button.Margin = new Padding(12, 0, 0, 0);
            button.Image = CallMonitoringIcons.RenderIconBitmap(icon, 16, Color.FromArgb(120, 130, 140));
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Text = string.Empty;
            return button;
        }

        private static Label CreatePillLabel(string text, Color backColor, Color foreColor)
        {
            return new Label
            {
                Text = text ?? string.Empty,
                AutoSize = true,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0),
                MaximumSize = new Size(600, 0)
            };
        }

        private static Panel CreateMetricTile(string title, Color accent, out Label valueLabel)
        {
            var tile = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(6),
                Padding = new Padding(10)
            };
            tile.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(232, 236, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, tile.Width - 1, tile.Height - 1);
            };

            var bar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 6,
                BackColor = accent
            };

            var lblTitle = new Label
            {
                Text = (title ?? string.Empty).ToUpperInvariant(),
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 8.0F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 140)
            };

            valueLabel = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                TextAlign = ContentAlignment.MiddleLeft
            };

            tile.Controls.Add(valueLabel);
            tile.Controls.Add(lblTitle);
            tile.Controls.Add(bar);
            return tile;
        }

        private static Label CreateKeyLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.0F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 140),
                Margin = new Padding(0, 6, 0, 0)
            };
        }

        private static Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoEllipsis = true,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                Margin = new Padding(0, 6, 0, 0)
            };
        }

        private static TextBox CreateMultilineBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Consolas", 9F),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
        }

        private static Panel WrapWithPanel(Label headerValue, TextBox detailsBox)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0) };
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlp.Controls.Add(headerValue, 0, 0);
            tlp.Controls.Add(detailsBox, 0, 1);
            p.Controls.Add(tlp);
            return p;
        }

        private async Task RefreshAsync()
        {
            if (_btnRefresh != null) _btnRefresh.Enabled = false;
            try
            {
                var snap = await _repo.GetDiagnosticsSnapshotAsync();
                _lastSnapshot = snap;
                ApplySnapshotToUi(snap);

                try
                {
                    await ApplyEnhancedSignalsAsync(snap);
                }
                catch
                {
                    // Best-effort; base diagnostics should still show.
                }
                try
                {
                    await ConnectToItcmServerAsync(manual: false);
                }
                catch
                {
                    // Best-effort; a down web server must not break DB diagnostics.
                }
                if (_lblLastRefreshedValue != null)
                    _lblLastRefreshedValue.Text = "Last refreshed: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                Logger.LogError("ITCM Diagnostics refresh failed.", ex);
                MessageBox.Show(
                    "Diagnostics failed to load.\n\n" + ex.Message,
                    "Diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                if (_btnRefresh != null) _btnRefresh.Enabled = true;
            }
        }

        private void ApplySnapshotToUi(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            if (snap == null)
                return;

            _lblServerValue.Text = string.IsNullOrWhiteSpace(snap.ServerName) ? "-" : snap.ServerName;
            _lblDbValue.Text = string.IsNullOrWhiteSpace(snap.DatabaseName) ? "-" : snap.DatabaseName;

            if (_lblSchedulerTaskStateValue != null)
            {
                var taskStateText = snap.SchedulerTaskState;
                if (snap.SchedulerTaskEnabled == true && snap.SchedulerTaskNextRunLocal.HasValue)
                    taskStateText = $"{taskStateText} • next {snap.SchedulerTaskNextRunLocal.Value:yyyy-MM-dd HH:mm:ss}";
                else if (snap.SchedulerTaskEnabled == false)
                    taskStateText = string.IsNullOrWhiteSpace(taskStateText) ? "DISABLED" : taskStateText;

                _lblSchedulerTaskStateValue.Text = string.IsNullOrWhiteSpace(taskStateText) ? "UNKNOWN" : taskStateText;

                if (snap.SchedulerTaskEnabled == true)
                    _lblSchedulerTaskStateValue.ForeColor = Color.FromArgb(39, 174, 96);
                else if (snap.SchedulerTaskEnabled == false)
                    _lblSchedulerTaskStateValue.ForeColor = Color.FromArgb(192, 57, 43);
                else
                    _lblSchedulerTaskStateValue.ForeColor = Color.FromArgb(120, 130, 140);
            }

            if (_lblSchedulerEnabledValue != null)
            {
                if (snap.SchedulerTaskEnabled == true)
                {
                    _lblSchedulerEnabledValue.Text = "YES";
                    _lblSchedulerEnabledValue.ForeColor = Color.FromArgb(39, 174, 96);
                }
                else if (snap.SchedulerTaskEnabled == false)
                {
                    _lblSchedulerEnabledValue.Text = "NO";
                    _lblSchedulerEnabledValue.ForeColor = Color.FromArgb(192, 57, 43);
                }
                else
                {
                    _lblSchedulerEnabledValue.Text = "UNKNOWN";
                    _lblSchedulerEnabledValue.ForeColor = Color.FromArgb(120, 130, 140);
                }
            }

            if (_lblSchedulerStatusValue != null)
            {
                if (snap.SchedulerIsRunning == true)
                {
                    _lblSchedulerStatusValue.Text = "RUNNING";
                    _lblSchedulerStatusValue.ForeColor = Color.FromArgb(39, 174, 96);
                }
                else if (snap.SchedulerIsRunning == false)
                {
                    _lblSchedulerStatusValue.Text = "IDLE";
                    _lblSchedulerStatusValue.ForeColor = Color.FromArgb(230, 126, 34);
                }
                else
                {
                    _lblSchedulerStatusValue.Text = "UNKNOWN";
                    _lblSchedulerStatusValue.ForeColor = Color.FromArgb(120, 130, 140);
                }
            }

            if (_lblSchedulerLastActivityValue != null)
                _lblSchedulerLastActivityValue.Text = snap.SchedulerLastActivityUtc.HasValue
                    ? ToLocalDisplay(snap.SchedulerLastActivityUtc.Value)
                    : "(none)";

            if (_lblSchedulerSignalsValue != null)
                _lblSchedulerSignalsValue.Text = snap.SchedulerSignalCountLast24Hours.ToString();

            // Heartbeat
            var hb = snap.SchedulerHeartbeat;
            if (_lblHeartbeatLastStartedValue != null)
            {
                if (hb != null && hb.TableExists)
                {
                    _lblHeartbeatLastStartedValue.Text = hb.LastStartedUtc.HasValue ? ToLocalDisplay(hb.LastStartedUtc.Value) : "(none)";
                    _lblHeartbeatLastFinishedValue.Text = hb.LastFinishedUtc.HasValue ? ToLocalDisplay(hb.LastFinishedUtc.Value) : "(none)";
                    _lblHeartbeatLastSuccessValue.Text = hb.LastSuccessUtc.HasValue ? ToLocalDisplay(hb.LastSuccessUtc.Value) : "(none)";
                    _lblHeartbeatMachineValue.Text = string.IsNullOrWhiteSpace(hb.MachineName) ? "-" : hb.MachineName;
                    _lblHeartbeatVersionValue.Text = string.IsNullOrWhiteSpace(hb.Version) ? "-" : hb.Version;
                }
                else
                {
                    _lblHeartbeatLastStartedValue.Text = hb == null ? "Heartbeat table not found" : "No heartbeat rows yet";
                    _lblHeartbeatLastFinishedValue.Text = "-";
                    _lblHeartbeatLastSuccessValue.Text = "-";
                    _lblHeartbeatMachineValue.Text = "-";
                    _lblHeartbeatVersionValue.Text = "-";
                }
            }

            var coreOk = snap.SchemaChecks.Any(c => c.Area == "Core" && c.ObjectName == "dbo.CallTicket" && c.Exists);
            var emailOk = snap.SchemaChecks.Any(c => c.Area == "Email" && c.ObjectName == "dbo.CallEmailSettings" && c.Exists)
                          && snap.SchemaChecks.Any(c => c.Area == "Email" && c.ObjectName == "dbo.CallEmailTemplate" && c.Exists);

            _lblSchemaValue.Text = coreOk
                ? (emailOk ? "OK (Core + Email)" : "OK (Core) / Email partial")
                : "Missing core schema";

            _lblSchemaValue.ForeColor = coreOk ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);

            if (_lblHealthBadge != null)
            {
                var lastFailedUtc = snap.LastEmailFailed?.DateSent;
                var hasRecentFailure = lastFailedUtc.HasValue
                                       && (DateTime.UtcNow - DateTime.SpecifyKind(lastFailedUtc.Value, DateTimeKind.Utc)).TotalHours <= 24;
                var heartbeatAlive = hb?.LastSuccessUtc.HasValue == true && (DateTime.UtcNow - hb.LastSuccessUtc.Value).TotalMinutes < 10;

                if (!coreOk)
                {
                    _lblHealthBadge.Text = "Schema missing";
                    _lblHealthBadge.BackColor = Color.FromArgb(255, 235, 238);
                    _lblHealthBadge.ForeColor = Color.FromArgb(192, 57, 43);
                }
                else if (snap.SchedulerTaskEnabled == false)
                {
                    _lblHealthBadge.Text = "Scheduler disabled";
                    _lblHealthBadge.BackColor = Color.FromArgb(255, 235, 238);
                    _lblHealthBadge.ForeColor = Color.FromArgb(192, 57, 43);
                }
                else if (!emailOk)
                {
                    _lblHealthBadge.Text = "Email partial";
                    _lblHealthBadge.BackColor = Color.FromArgb(255, 243, 224);
                    _lblHealthBadge.ForeColor = Color.FromArgb(194, 100, 0);
                }
                else if (hasRecentFailure)
                {
                    _lblHealthBadge.Text = "Email failing (last 24h)";
                    _lblHealthBadge.BackColor = Color.FromArgb(255, 243, 224);
                    _lblHealthBadge.ForeColor = Color.FromArgb(194, 100, 0);
                }
                else if (heartbeatAlive)
                {
                    _lblHealthBadge.Text = "Healthy • Scheduler running (heartbeat OK)";
                    _lblHealthBadge.BackColor = Color.FromArgb(232, 245, 233);
                    _lblHealthBadge.ForeColor = Color.FromArgb(39, 174, 96);
                }
                else if (snap.SchedulerIsRunning == true)
                {
                    _lblHealthBadge.Text = "Healthy • Scheduler running";
                    _lblHealthBadge.BackColor = Color.FromArgb(232, 245, 233);
                    _lblHealthBadge.ForeColor = Color.FromArgb(39, 174, 96);
                }
                else if (snap.SchedulerTaskEnabled == true)
                {
                    _lblHealthBadge.Text = "Healthy • Scheduler ready";
                    _lblHealthBadge.BackColor = Color.FromArgb(232, 245, 233);
                    _lblHealthBadge.ForeColor = Color.FromArgb(39, 174, 96);
                }
                else
                {
                    _lblHealthBadge.Text = "Healthy • Scheduler idle";
                    _lblHealthBadge.BackColor = Color.FromArgb(232, 245, 233);
                    _lblHealthBadge.ForeColor = Color.FromArgb(39, 174, 96);
                }
            }

            var open = snap.DashboardMetrics?.OpenTickets ?? 0;
            var critical = snap.DashboardMetrics?.CriticalTickets ?? 0;
            var today = snap.DashboardMetrics?.TodaysVolume ?? 0;
            var pending = snap.PendingTickets;

            if (_lblOpenValue != null) _lblOpenValue.Text = open.ToString();
            if (_lblPendingValue != null) _lblPendingValue.Text = pending.ToString();
            if (_lblCriticalValue != null) _lblCriticalValue.Text = critical.ToString();
            if (_lblTodayValue != null) _lblTodayValue.Text = today.ToString();

            _lblCountsValue.Text = snap.DashboardMetrics?.AvgResolutionMinutes.HasValue == true
                ? $"{snap.DashboardMetrics.AvgResolutionMinutes.Value} min"
                : "-";

            _lblLastEscalationValue.Text = snap.LastAutoEscalationUtc.HasValue
                ? ToLocalDisplay(snap.LastAutoEscalationUtc.Value) + " (Auto)"
                : "-";

            ApplyEmailSection(
                label: _lblLastEmailFailedValue,
                details: _txtLastEmailFailedDetails,
                log: snap.LastEmailFailed,
                emptyMessage: "(none)");

            ApplyEmailSection(
                label: _lblLastEmailSkippedValue,
                details: _txtLastEmailSkippedDetails,
                log: snap.LastEmailSkipped,
                emptyMessage: "(none)");

            if (_lblLastReminderEmailValue != null)
                _lblLastReminderEmailValue.Text = snap.LastReminderEmail != null
                    ? $"{ToLocalDisplay(snap.LastReminderEmail.DateSent)} • {snap.LastReminderEmail.Status}"
                    : "(none)";

            if (_lblLastEscalationEmailValue != null)
                _lblLastEscalationEmailValue.Text = snap.LastEscalationEmail != null
                    ? $"{ToLocalDisplay(snap.LastEscalationEmail.DateSent)} • {snap.LastEscalationEmail.Status}"
                    : "(none)";

            _lvSchema.BeginUpdate();
            _lvSchema.Items.Clear();
            _lvSchema.Groups.Clear();
            foreach (var check in snap.SchemaChecks.OrderBy(c => c.Area).ThenBy(c => c.ObjectName))
            {
                var statusText = check.Exists ? "OK" : "MISSING";

                var groupKey = (check.Area ?? "Other").Trim();
                var group = _lvSchema.Groups.Cast<ListViewGroup>()
                    .FirstOrDefault(g => string.Equals(g.Header, groupKey, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    group = new ListViewGroup(groupKey, HorizontalAlignment.Left);
                    _lvSchema.Groups.Add(group);
                }

                var item = new ListViewItem(new[]
                {
                    check.Area ?? string.Empty,
                    check.ObjectName ?? string.Empty,
                    statusText,
                    (check.Detail ?? string.Empty).Trim()
                })
                { Group = group };

                item.ForeColor = check.Exists ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
                item.BackColor = check.Exists ? Color.White : Color.FromArgb(255, 245, 245);
                _lvSchema.Items.Add(item);
            }
            _lvSchema.EndUpdate();
        }

        private void OpenWebDashboard()
        {
            try
            {
                var url = Core.AppConfig.ItcmServerUrl;
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open ITCM Server dashboard.\n\n" + ex.Message, "Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ConnectToItcmServerAsync(bool manual)
        {
            // Already connected and fresh (5 min): just show Connected, no network call.
            if (_lastPingConnected && (DateTime.UtcNow - _lastPingAtUtc) < TimeSpan.FromMinutes(5))
            {
                ApplyPingToUi();
                return;
            }

            if (_btnConnect != null) _btnConnect.Enabled = false;
            try
            {
                var url = (Core.AppConfig.ItcmServerUrl ?? string.Empty).Trim().TrimEnd('/');
                if (string.IsNullOrWhiteSpace(url))
                {
                    CachePing(false, "Not configured", url);
                    return;
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    using (var response = await _itcmPingClient.GetAsync(url + "/api/itcm/ping"))
                    {
                        sw.Stop();
                        if (!response.IsSuccessStatusCode)
                        {
                            CachePing(false,
                                (int)response.StatusCode == 404
                                    ? "Server outdated (old build)"
                                    : "HTTP " + (int)response.StatusCode,
                                url);
                            return;
                        }

                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        var version = ((string)json["version"] ?? string.Empty).Trim();
                        var sched = json["scheduler"];
                        var running = (bool?)sched?["running"];
                        var paused = (bool?)sched?["paused"];
                        var enabled = (bool?)sched?["enabled"];
                        string state;
                        if (running == true) state = "RUNNING";
                        else if (paused == true) state = "PAUSED";
                        else if (enabled == false) state = "DISABLED";
                        else state = "READY";
                        CachePing(true,
                            "Connected (" + sw.ElapsedMilliseconds + "ms)"
                                + (string.IsNullOrEmpty(version) ? string.Empty : " • v" + version)
                                + " • " + state,
                            url);
                    }
                }
                catch (TaskCanceledException)
                {
                    sw.Stop();
                    CachePing(false, "Timed out after 5s", url);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var msg = (ex.GetBaseException().Message ?? ex.Message ?? "unreachable").Trim();
                    if (msg.Length > 90) msg = msg.Substring(0, 90) + "...";
                    CachePing(false, "Unreachable (" + msg + ")", url);
                }
            }
            finally
            {
                if (_btnConnect != null) _btnConnect.Enabled = true;
            }
        }

        private void CachePing(bool connected, string text, string url)
        {
            _lastPingConnected = connected;
            _lastPingText = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
            _lastPingUrl = (url ?? string.Empty).Trim();
            _lastPingAtUtc = DateTime.UtcNow;
            _lastPingSummary = "Web Server: " + _lastPingText
                + (string.IsNullOrWhiteSpace(_lastPingUrl) ? string.Empty : " • " + _lastPingUrl);
            ApplyPingToUi();
        }

        private void ApplyPingToUi()
        {
            if (_btnConnect != null && !_btnConnect.IsDisposed)
                _btnConnect.Text = _lastPingConnected ? "Connected" : "Connect";
            if (_lblWebServerValue == null) return;
            _lblWebServerValue.Text = _lastPingText;
            _lblWebServerValue.ForeColor = _lastPingConnected
                ? Color.FromArgb(39, 174, 96)
                : Color.FromArgb(192, 57, 43);
        }

        private string PingSummaryLine()
        {
            return Environment.NewLine + (_lastPingSummary ?? "Web Server: -") + Environment.NewLine;
        }

        private static void ApplyEmailSection(Label label, TextBox details, Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, string emptyMessage)
        {
            if (label == null || details == null)
                return;

            if (log == null)
            {
                label.Text = emptyMessage;
                label.ForeColor = Color.FromArgb(120, 130, 140);
                details.Text = string.Empty;
                return;
            }

            var dateSent = ToLocalDisplay(log.DateSent);
            var ticket = log.TicketId.HasValue && log.TicketId.Value > 0 ? $"Ticket #{log.TicketId.Value}" : "No Ticket";
            label.Text = $"{dateSent} • {log.EmailType} • {ticket}";

            if (string.Equals(log.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                label.ForeColor = Color.FromArgb(192, 57, 43);
            else if (string.Equals(log.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
                label.ForeColor = Color.FromArgb(230, 126, 34);
            else
                label.ForeColor = Color.FromArgb(44, 62, 80);

            details.Text = FormatActionableText(log.ErrorMessage);
        }

        private static string FormatActionableText(string message)
        {
            var text = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Expected format from service: "Problem: ...\nFix: ...\nInfo: ..."
            try
            {
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Select(l => (l ?? string.Empty).Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                var problem = lines.FirstOrDefault(l => l.StartsWith("Problem:", StringComparison.OrdinalIgnoreCase));
                var fix = lines.FirstOrDefault(l => l.StartsWith("Fix:", StringComparison.OrdinalIgnoreCase));
                var info = lines.FirstOrDefault(l => l.StartsWith("Info:", StringComparison.OrdinalIgnoreCase));

                if (problem != null || fix != null || info != null)
                {
                    var sb = new StringBuilder();
                    if (problem != null) sb.AppendLine(problem);
                    if (fix != null)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.AppendLine(fix);
                    }
                    if (info != null)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.AppendLine(info);
                    }
                    return sb.ToString().Trim();
                }
            }
            catch
            {
            }

            return text;
        }

        private static string ToLocalDisplay(DateTime utcFromDb)
        {
            var utc = utcFromDb.Kind == DateTimeKind.Utc ? utcFromDb : DateTime.SpecifyKind(utcFromDb, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static LinkLabel CreateDeepLink(string text, Action onClick)
        {
            var lnk = new LinkLabel
            {
                Text = text ?? string.Empty,
                AutoSize = true,
                LinkColor = Color.FromArgb(52, 152, 219),
                ActiveLinkColor = Color.FromArgb(41, 128, 185),
                VisitedLinkColor = Color.FromArgb(52, 152, 219),
                Margin = new Padding(0, 0, 12, 0)
            };

            lnk.LinkClicked += (_, __) =>
            {
                try { onClick?.Invoke(); }
                catch { }
            };

            return lnk;
        }

        private void OpenEmailDeepLink(
            Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget target,
            int? deptId = null,
            int? branchId = null,
            string emailLogSearch = null)
        {
            if (_openEmailDeepLink == null)
            {
                MessageBox.Show("Email Notifications screen is not available in this view.", "Deep Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _openEmailDeepLink(target, deptId, branchId, emailLogSearch);
        }

        private void OpenTemplatesDeepLink()
        {
            var type = (_lastSnapshot?.LastEmailFailed?.EmailType ?? string.Empty).Trim();
            if (type.Equals("Escalation", StringComparison.OrdinalIgnoreCase))
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.TemplatesEscalation);
            else if (type.Equals("StatusUpdate", StringComparison.OrdinalIgnoreCase))
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.TemplatesStatusUpdate);
            else if (type.Equals("NewTicket", StringComparison.OrdinalIgnoreCase))
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.TemplatesNewTicket);
            else
                OpenEmailDeepLink(Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.TemplatesReminder);
        }

        private async Task ApplyEnhancedSignalsAsync(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            var status = CallEmailNotificationService.GetBackgroundJobStatusSnapshot();

            if (_lblJobLastStartValue != null)
                _lblJobLastStartValue.Text = status.LastRunStartUtc.HasValue ? status.LastRunStartUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "(none)";
            if (_lblJobLastEndValue != null)
                _lblJobLastEndValue.Text = status.LastRunEndUtc.HasValue ? status.LastRunEndUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "(none)";
            if (_lblJobNextRunValue != null)
                _lblJobNextRunValue.Text = status.NextRunUtc.HasValue ? status.NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "(unknown)";

            if (_lblJobLockProbeValue != null)
            {
                var p = snap?.BackgroundLockProbe;
                if (p?.IsFree == true)
                    _lblJobLockProbeValue.Text = p.ResultCode.HasValue ? $"Free (code {p.ResultCode.Value})" : "Free";
                else if (p?.IsFree == false)
                    _lblJobLockProbeValue.Text = p.ResultCode.HasValue ? $"Blocked (code {p.ResultCode.Value})" : "Blocked (another client running)";
                else
                    _lblJobLockProbeValue.Text = "Unknown";
            }

            if (_lblJobCountsValue != null)
            {
                _lblJobCountsValue.Text =
                    $"Reminders: {status.RemindersSent}/{status.ReminderCandidates} (skipped/failed {status.ReminderSkippedOrFailed}) • " +
                    $"Escalations: {status.EscalationsApplied}/{status.EscalationCandidates} (skipped/failed {status.EscalationSkippedOrFailed})";
            }

            // Enrich Last FAILED email with actual resolution traces (SMTP profile + recipients).
            await EnrichEmailDetailsAsync(snap?.LastEmailFailed, _txtLastEmailFailedDetails, isPrimary: true);
            await EnrichEmailDetailsAsync(snap?.LastEmailSkipped, _txtLastEmailSkippedDetails, isPrimary: false);
        }

        private async Task EnrichEmailDetailsAsync(Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, TextBox detailsBox, bool isPrimary)
        {
            if (log == null || detailsBox == null)
                return;

            if (!log.TicketId.HasValue || log.TicketId.Value <= 0)
                return;

            var ticketId = log.TicketId.Value;
            var baseText = (detailsBox.Text ?? string.Empty).Trim();

            var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
            _lastResolvedTicketId = ticketId;
            _lastResolvedDeptId = ticket?.DeptId;
            _lastResolvedBranchId = ticket?.BranchId;

            var senderResolution = await _repo.GetSmtpSenderResolutionForTicketAsync(ticket?.BranchId, ticket?.DeptId);
            var email = new CallEmailNotificationService(_repo);
            var recipientTrace = await email.GetRecipientResolutionTraceForTicketAsync(ticketId, log.EmailType);

            if (senderResolution == null && recipientTrace == null)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("Resolution trace:");

            if (senderResolution != null)
            {
                var sender = senderResolution.Sender;
                var authMode = sender != null && !string.IsNullOrWhiteSpace(sender.SmtpUsername) ? "Username+Password" : "DefaultCredentials";
                var from = sender == null ? "" : $"{(sender.FromEmail ?? "").Trim()} {(string.IsNullOrWhiteSpace(sender.FromName) ? "" : $"({sender.FromName.Trim()})")}".Trim();

                sb.AppendLine($"- SMTP profile: {senderResolution.Source}" +
                              $"{(senderResolution.ProfileId.HasValue ? $" (ProfileId={senderResolution.ProfileId.Value})" : "")}" +
                              $"{(!string.IsNullOrWhiteSpace(senderResolution.ProfileName) ? $" • {senderResolution.ProfileName}" : "")}");

                if (sender != null)
                {
                    var userLabel = string.IsNullOrWhiteSpace(sender.SmtpUsername) ? "(none)" : "(set)";
                    sb.AppendLine($"- SMTP host: {sender.SmtpServer}:{sender.SmtpPort} • SSL={sender.UseSsl} • Auth={authMode} • User={userLabel}");
                    if (!string.IsNullOrWhiteSpace(from)) sb.AppendLine($"- From: {from}");
                }
            }

            if (recipientTrace != null)
            {
                sb.AppendLine($"- Recipients resolved: {recipientTrace.FinalRecipients.Count}");
                if (recipientTrace.FinalRecipients.Count > 0)
                    sb.AppendLine("  " + string.Join(", ", recipientTrace.FinalRecipients));

                sb.AppendLine($"- GroupEmail: {(recipientTrace.RulesGroupEmail ?? "").Trim()}");
                sb.AppendLine($"- Dept recipients: {(recipientTrace.DepartmentRecipients ?? "").Trim()}");
                sb.AppendLine($"- Branch recipients: {(recipientTrace.BranchRecipients ?? "").Trim()}");
            }

            sb.AppendLine();
            detailsBox.Text = sb.ToString().TrimEnd() + (string.IsNullOrWhiteSpace(baseText) ? "" : ("\r\n\r\n" + baseText));

            // Update the Templates link to point at the relevant template tab for the last failed email type.
            if (isPrimary && _lnkTemplates != null)
            {
                var normalized = (log.EmailType ?? string.Empty).Trim();
                if (normalized.Equals("Escalation", StringComparison.OrdinalIgnoreCase))
                    _lnkTemplates.Text = "Templates (Escalation)";
                else if (normalized.Equals("StatusUpdate", StringComparison.OrdinalIgnoreCase))
                    _lnkTemplates.Text = "Templates (Status Update)";
                else if (normalized.Equals("NewTicket", StringComparison.OrdinalIgnoreCase))
                    _lnkTemplates.Text = "Templates (New Ticket)";
                else
                    _lnkTemplates.Text = "Templates (Reminder)";
            }
        }

        private async Task OpenSmtpTestDialogAsync()
        {
            try
            {
                var defaultTicketId = _lastSnapshot?.LastEmailFailed?.TicketId ?? _lastSnapshot?.LastEmailSkipped?.TicketId;
                using (var dlg = new SmtpTestDialog(_repo, defaultTicketId))
                {
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SMTP Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            await Task.CompletedTask;
        }

        private sealed class SmtpTestDialog : Form
        {
            private readonly ICallMonitoringRepository _repo;
            private readonly NumericUpDown _numTicketId;
            private readonly ComboBox _cboType;
            private readonly TextBox _txtTo;
            private readonly Button _btnRun;
            private readonly TextBox _txtOut;

            public SmtpTestDialog(ICallMonitoringRepository repo, int? defaultTicketId)
            {
                _repo = repo ?? throw new ArgumentNullException(nameof(repo));

                this.Text = "Run SMTP Test";
                this.StartPosition = FormStartPosition.CenterParent;
                this.FormBorderStyle = FormBorderStyle.None;
                this.MinimizeBox = false;
                this.MaximizeBox = false;
                this.Width = 820;
                this.Height = 520;
                this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);
                this.BackColor = Color.White;

                var pnlBorder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
                pnlBorder.Paint += (_, e) => { using (var p = new Pen(Color.FromArgb(52, 152, 219))) e.Graphics.DrawRectangle(p, 0, 0, pnlBorder.Width - 1, pnlBorder.Height - 1); };

                var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(44, 62, 80) };
                var lblHeader = new Label { Text = "Run SMTP Test", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 11F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
                var btnClose = new Button { Text = "✕", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Transparent, Cursor = Cursors.Hand };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (_, __) => this.Close();
                pnlHeader.Controls.Add(lblHeader);
                pnlHeader.Controls.Add(btnClose);

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 5,
                    Padding = new Padding(12)
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

                _numTicketId = new NumericUpDown { Minimum = 0, Maximum = 999999999, Dock = DockStyle.Left, Width = 160 };
                if (defaultTicketId.HasValue && defaultTicketId.Value > 0) _numTicketId.Value = defaultTicketId.Value;

                _cboType = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
                _cboType.Items.AddRange(new object[] { "Reminder", "Escalation", "StatusUpdate", "NewTicket" });
                _cboType.SelectedIndex = 0;

                _txtTo = new TextBox { Dock = DockStyle.Fill };
                _txtTo.TextChanged += (_, __) => 
                {
                    var text = _txtTo.Text.Trim();
                    bool isValid = System.Text.RegularExpressions.Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                    _btnRun.Enabled = isValid;
                    _txtTo.BackColor = isValid || text.Length == 0 ? Color.White : Color.FromArgb(255, 235, 238);
                };

                _btnRun = new Button { Text = "Send Test Email", Dock = DockStyle.Left, Width = 140, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White, Cursor = Cursors.Hand };
                _btnRun.FlatAppearance.BorderSize = 0;
                _btnRun.Enabled = false;
                _btnRun.Click += async (_, __) => await RunAsync();

                var lblHint = new Label
                {
                    Text = "Sends a single email to the override address only (still resolves the exact SMTP profile + recipients path).",
                    AutoSize = true,
                    ForeColor = Color.FromArgb(120, 130, 140),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                _txtOut = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9F)
                };

                root.Controls.Add(new Label { Text = "TicketId", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                root.Controls.Add(_numTicketId, 1, 0);
                root.Controls.Add(new Label { Text = "Email Type", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
                root.Controls.Add(_cboType, 1, 1);
                root.Controls.Add(new Label { Text = "Test To (override)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
                root.Controls.Add(_txtTo, 1, 2);

                var pnlRun = new Panel { Dock = DockStyle.Fill };
                pnlRun.Controls.Add(_btnRun);
                pnlRun.Controls.Add(lblHint);
                _btnRun.Location = new Point(0, 6);
                lblHint.Location = new Point(_btnRun.Right + 12, 10);
                lblHint.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

                root.Controls.Add(pnlRun, 1, 3);
                root.SetColumnSpan(pnlRun, 1);

                root.Controls.Add(_txtOut, 0, 4);
                root.SetColumnSpan(_txtOut, 2);

                pnlBorder.Controls.Add(root);
                pnlBorder.Controls.Add(pnlHeader);
                this.Controls.Add(pnlBorder);
            }

            private async Task RunAsync()
            {
                _btnRun.Enabled = false;
                try
                {
                    var ticketId = (int)_numTicketId.Value;
                    var type = (_cboType.SelectedItem ?? "Reminder").ToString();
                    var to = (_txtTo.Text ?? string.Empty).Trim();

                    var svc = new CallEmailNotificationService(_repo);
                    var res = await svc.RunSmtpTestAsync(ticketId, type, to);

                    var sb = new StringBuilder();
                    sb.AppendLine("SMTP Test Result");
                    sb.AppendLine("Time (local): " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine();
                    sb.AppendLine($"TicketId: {res.TicketId}");
                    sb.AppendLine($"EmailType: {res.EmailType}");
                    sb.AppendLine($"TestTo: {res.TestToEmail}");
                    sb.AppendLine();

                    if (res.SenderResolution != null)
                    {
                        var s = res.SenderResolution.Sender;
                        var auth = s != null && !string.IsNullOrWhiteSpace(s.SmtpUsername) ? "Username+Password" : "DefaultCredentials";
                        string GetFirstValidEmail(string a, string b)
                        {
                            foreach (var candidate in new[] { a, b })
                            {
                                var v = (candidate ?? string.Empty).Trim();
                                if (string.IsNullOrWhiteSpace(v)) continue;
                                try { _ = new System.Net.Mail.MailAddress(v); return v; } catch { }
                            }
                            return null;
                        }

                        sb.AppendLine($"SMTP source: {res.SenderResolution.Source}");
                        sb.AppendLine($"Profile: {(res.SenderResolution.ProfileId.HasValue ? res.SenderResolution.ProfileId.Value.ToString() : "")} {res.SenderResolution.ProfileName}".Trim());
                        if (s != null)
                        {
                            sb.AppendLine($"Host: {s.SmtpServer}");
                            sb.AppendLine($"Port: {s.SmtpPort}");
                            sb.AppendLine($"SSL: {s.UseSsl}");
                            sb.AppendLine($"Auth: {auth}");
	                            sb.AppendLine($"User: {(string.IsNullOrWhiteSpace(s.SmtpUsername) ? "(none)" : "(set)")}");
	                            var effectiveFrom = GetFirstValidEmail(s.FromEmail, s.SmtpUsername);
	                            sb.AppendLine($"From: {(effectiveFrom ?? "(invalid)")}");
	                        }
                        sb.AppendLine();
                    }

                    if (res.RecipientResolution != null)
                    {
                        sb.AppendLine("Resolved recipients (normal pipeline):");
                        sb.AppendLine("Final: " + string.Join(", ", res.RecipientResolution.FinalRecipients ?? new System.Collections.Generic.List<string>()));
                        sb.AppendLine("GroupEmail: " + (res.RecipientResolution.RulesGroupEmail ?? ""));
                        sb.AppendLine("Dept recipients: " + (res.RecipientResolution.DepartmentRecipients ?? ""));
                        sb.AppendLine("Branch recipients: " + (res.RecipientResolution.BranchRecipients ?? ""));
                        sb.AppendLine();
                    }

                    if (res.Sent)
                        sb.AppendLine("Result: SENT OK");
                    else
                        sb.AppendLine("Result: FAILED\n" + (res.Error ?? ""));

                    _txtOut.Text = sb.ToString().TrimEnd();

                    if (!res.Sent && !string.IsNullOrWhiteSpace(res.Error))
                        MessageBox.Show(res.Error, "SMTP Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    _txtOut.Text = ex.ToString();
                    MessageBox.Show(ex.Message, "SMTP Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _btnRun.Enabled = true;
                }
            }
        }

        private void TryOpenLogFolder()
        {
            try
            {
                var logPath = Logger.GetTodayLogPath();
                var folder = Path.GetDirectoryName(logPath);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    MessageBox.Show("Log folder not found.", "Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open log folder.", ex);
            }
        }

        private static void TryCopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text ?? string.Empty);
            }
            catch
            {
            }
        }

        private void TryExportDiagnostics()
        {
            try
            {
                var text = BuildDiagnosticsText(_lastSnapshot) + PingSummaryLine();
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                    sfd.FileName = $"ITCM_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, text);
                        MessageBox.Show("Diagnostics exported successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export diagnostics.\n\n{ex.Message}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildDiagnosticsText(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            if (snap == null)
                return "Diagnostics: (no data)";

            var sb = new StringBuilder();
            sb.AppendLine("IT Call Monitoring - Diagnostics");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Server: " + (snap.ServerName ?? string.Empty));
            sb.AppendLine("Database: " + (snap.DatabaseName ?? string.Empty));
            sb.AppendLine("Task Scheduler: " + (!string.IsNullOrWhiteSpace(snap.SchedulerTaskState) ? snap.SchedulerTaskState : "UNKNOWN"));
            sb.AppendLine("Task enabled: " + (snap.SchedulerTaskEnabled.HasValue ? (snap.SchedulerTaskEnabled.Value ? "YES" : "NO") : "UNKNOWN"));
            sb.AppendLine("Task next run (local): " + (snap.SchedulerTaskNextRunLocal.HasValue ? snap.SchedulerTaskNextRunLocal.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-"));
            sb.AppendLine("Task last result: " + (!string.IsNullOrWhiteSpace(snap.SchedulerTaskLastResult) ? snap.SchedulerTaskLastResult : "-"));
            sb.AppendLine("Scheduler processing: " + (snap.SchedulerIsRunning.HasValue ? (snap.SchedulerIsRunning.Value ? "RUNNING" : "IDLE") : "UNKNOWN"));
            sb.AppendLine("Scheduler last activity (local): " + (snap.SchedulerLastActivityUtc.HasValue ? ToLocalDisplay(snap.SchedulerLastActivityUtc.Value) : "-"));
            sb.AppendLine("Scheduler signals (24h): " + snap.SchedulerSignalCountLast24Hours);
            sb.AppendLine();

            var open = snap.DashboardMetrics?.OpenTickets ?? 0;
            var critical = snap.DashboardMetrics?.CriticalTickets ?? 0;
            var today = snap.DashboardMetrics?.TodaysVolume ?? 0;
            sb.AppendLine($"Counts: Open={open}, Pending={snap.PendingTickets}, Critical={critical}, Today={today}");

            if (snap.LastAutoEscalationUtc.HasValue)
                sb.AppendLine("Last auto-escalation (local): " + ToLocalDisplay(snap.LastAutoEscalationUtc.Value));
            else
                sb.AppendLine("Last auto-escalation (local): -");

            sb.AppendLine();
            sb.AppendLine("Last FAILED email:");
            sb.AppendLine(FormatEmailLogLine(snap.LastEmailFailed));
            sb.AppendLine();
            sb.AppendLine("Last SKIPPED email:");
            sb.AppendLine(FormatEmailLogLine(snap.LastEmailSkipped));
            sb.AppendLine();
            sb.AppendLine("Last Reminder email:");
            sb.AppendLine(FormatEmailLogLine(snap.LastReminderEmail));
            sb.AppendLine();
            sb.AppendLine("Last Escalation email:");
            sb.AppendLine(FormatEmailLogLine(snap.LastEscalationEmail));
            sb.AppendLine();

            sb.AppendLine("Schema checks:");
            foreach (var c in snap.SchemaChecks.OrderBy(x => x.Area).ThenBy(x => x.ObjectName))
                sb.AppendLine($"- [{c.Area}] {(c.Exists ? "OK" : "MISSING")} {c.ObjectName} {(!string.IsNullOrWhiteSpace(c.Detail) ? ("| " + c.Detail) : string.Empty)}");

            return sb.ToString();
        }

        private static string FormatEmailLogLine(Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log)
        {
            if (log == null)
                return "(none)";

            var date = ToLocalDisplay(log.DateSent);
            var ticket = log.TicketId.HasValue && log.TicketId.Value > 0 ? $"Ticket #{log.TicketId.Value}" : "No Ticket";
            var err = (log.ErrorMessage ?? string.Empty).Trim();
            if (err.Length > 800) err = err.Substring(0, 800) + "…";
            return $"{date} | {log.Status} | {log.EmailType} | {ticket}\n{err}";
        }
    }
}
