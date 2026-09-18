using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public partial class TicketManagementControl : UserControl
    {
        private const int EM_SETCUEBANNER = 0x1501;
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly ICallMonitoringRepository _callRepo;
        private readonly CallEmailNotificationService _callEmail;
        private readonly ErrorProvider _newTicketErrorProvider = new ErrorProvider();
        private readonly System.Windows.Forms.Timer _historySearchDebounce;
        private readonly System.Windows.Forms.Timer _ticketSelectionDebounce;
        private System.Windows.Forms.Timer _callerSearchDebounceTimer;

        // UI Controls - Left Panel
        private SplitContainer splitMain;
        private Panel grpNewTicket;
        private ComboBox cboCompany;
        private ComboBox cboCallerName;
        private Button btnQuickAddCaller;
        private ComboBox cboDepartment;
        private ComboBox cboBranch;
        private ComboBox cboAssignedTo;
        private TextBox txtTechnicalProblem;
        private TextBox txtProvidedSolution;
        private Button btnCreateTicket;
        private Button btnClearForm;
        private Button btnSetNewTicketEscalation;

        // Backdate controls for backlog ticket creation
        private CheckBox chkBackdate;
        private DateTimePicker dtpBackdateDate;
        private DateTimePicker dtpBackdateTime;
        private LinkLabel lnkClearNewTicketEscalation;
        private Label lblNewTicketEscalationSummary;

        // UI Controls - Left Panel (Pending Details)
        private Panel grpPendingCaseDetails;
        private ComboBox cboStatus;
        private ComboBox cboPriority;
        private ComboBox cboReassignTo;
        private Button btnReassignTicket;
        private Button btnAssignToMe;
        private TabControl tabTicketDetails;
        private TextBox txtCaseProblem;
        private TextBox txtSolutionsLog;
        private TextBox txtAttemptedSolutions;
        private TextBox txtTabHistory;
        private Button btnUpdateNotes;
        private Button btnMarkAs;
        private Button btnReturnTempItem;
        private Button btnSyncSet;
        private Button btnEscalationOverride;
        private Button btnReopen;

        // UI Controls - Right Panel
        private TableLayoutPanel tlpRight;
        private Panel grpPendingTickets;
        private TextBox txtSearch;
        private ComboBox cboFilter;
        private ComboBox cboBranchFilter;
        private ComboBox cboAssigneeFilter;
        private Button btnApplyFilter;
        private FlowLayoutPanel pnlStatusLegend;
        private DataGridView dgvPendingTickets;
        private Button btnPendingNext;
        private Label lblPendingPage;
        private Button btnPendingPrev;

        private Panel grpSolvedTickets;
        private DataGridView dgvSolvedTickets;
        private Button btnSolvedNext;
        private Label lblSolvedPage;
        private Button btnSolvedPrev;

        private Panel grpTicketSummary;
        private Label lblSummaryId;
        private Label lblSummaryAge;
        private Label lblSummarySla;
        private Label lblSummaryEscalation;
        private Label lblProfileStatus;
        private Label lblProfilePriority;
        private Label lblProfileAssignee;
        private Label lblProfileCaller;
        private Label lblProfileLocation;
        private Button btnProfileDetails;
        private Button btnProfileSummary;
        private Button btnProfileDelete;

        private Panel grpEscalation;
        private Label lblNextEscalation;
        private Label lblEmailInfo;
        private Label lblLastEmail;

        // State
        private int _overdueDays = 3;
        private CallTicketListItem _selectedTicket;
        private int _pendingPageIndex = 1;
        private const int PendingPageSize = 7; // Per request: 7 rows per page in Pending Tickets
        private bool _pendingHasNext = false;
        private int _pendingTotalCount = 0;
        private int _solvedPageIndex = 1;
        private const int SolvedPageSize = 4; // Decreased for compact container
        private bool _solvedHasNext = false;
        private int _solvedTotalCount = 0;
        private bool _lookupLoadErrorShown = false;
        private DateTime _lastLookupLoadErrorShownUtc = DateTime.MinValue;
        private CallEscalationSettingsItem _escalationSettings;
        private bool _escalationOverridesInstalled;
        private int _selectionVersion = 0;
        private int _ticketsRefreshVersion = 0;
        private bool _suppressNewTicketValidation;
        private bool _newTicketShowValidationHints;
        private bool _splitInitialized;
        private bool _userMovedSplitter;
        private bool _suppressStatusDropdownUpdate;
        private bool _branchEventsWired;
        private bool _suppressBranchFilterRefresh;
        private bool _suppressTicketSelectionChanged;
        private bool _loadingNewTicketLookups;
        private bool _newTicketOrgLockedFromEmployeeSearch;
        private ToolTip _toolTip;
        private int? _newTicketEscDaysToSupervisor;
        private int? _newTicketEscDaysToManager;
        private string _newTicketEscReason;

        // Notes/History data
        private List<CallTicketNoteItem> _notesCacheSelected = new List<CallTicketNoteItem>();
        private DataGridView dgvHistory;
        private TextBox txtHistorySearch;
        private List<CallTicketHistoryItem> _historyCacheSelected = new List<CallTicketHistoryItem>();
        private int _pendingSelectedTicketId;

        // Direct-pick dropdown source. Single catalog lives in TicketWorkflow;
        // "Forwarded to Repair" stays out (forward flow only, needs a linked
        // Repair ticket). "Closed" is pickable here, matching WPF and the proc.
        private static readonly string[] WorkflowStatuses = TicketWorkflow.DirectPickStatuses;

        private static void SetTextBoxCueBanner(TextBox textBox, string cueText)
        {
            if (textBox == null || string.IsNullOrWhiteSpace(cueText))
                return;

            void Apply()
            {
                try
                {
                    if (!textBox.IsHandleCreated)
                        return;

                    // wParam = 0 => show only when not focused (Win32 convention)
                    SendMessage(textBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, cueText);
                }
                catch
                {
                    // Ignore if not supported (older OS/themes) or handle issues.
                }
            }

            if (textBox.IsHandleCreated)
            {
                Apply();
                return;
            }

            EventHandler handler = null;
            handler = (_, __) =>
            {
                try { textBox.HandleCreated -= handler; } catch { }
                Apply();
            };
            textBox.HandleCreated += handler;
        }

        public TicketManagementControl(ICallMonitoringRepository repo, CallEmailNotificationService emailService)
        {
            _callRepo = repo ?? throw new ArgumentNullException(nameof(repo));
            _callEmail = emailService ?? throw new ArgumentNullException(nameof(emailService));

            _historySearchDebounce = new System.Windows.Forms.Timer { Interval = 200 };
            _historySearchDebounce.Tick += (_, __) =>
            {
                try
                {
                    _historySearchDebounce.Stop();
                    ApplyHistoryFilterAndRender();
                }
                catch
                {
                }
            };

            _ticketSelectionDebounce = new System.Windows.Forms.Timer { Interval = 120 };
            _ticketSelectionDebounce.Tick += (_, __) =>
            {
                try
                {
                    _ticketSelectionDebounce.Stop();
                    var ticketId = _pendingSelectedTicketId;
                    if (ticketId > 0)
                        _ = LoadSelectedTicketDetailsAsync(ticketId, ++_selectionVersion);
                }
                catch
                {
                }
            };
            
            InitializeComponent();

            InitializeEmployeeSearch();

            if (AppSession.IsReadOnly)
                ApplyReadOnlyMode();

            // Keyboard shortcuts
            this.KeyDown += TicketManagementControl_KeyDown;

            this._newTicketErrorProvider.ContainerControl = this;
            this._newTicketErrorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

            this.HandleCreated += (_, __) =>
            {
                BeginInvoke((Action)(() => EnsureSplitLayout(force: true)));
            };

            this.VisibleChanged += (_, __) =>
            {
                if (this.Visible)
                    BeginInvoke((Action)(() => EnsureSplitLayout(force: true)));
            };

            this.SizeChanged += (_, __) =>
            {
                if (this.Visible)
                    EnsureSplitLayout(force: false);
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { this._newTicketErrorProvider?.Dispose(); } catch { }
                try { _historySearchDebounce?.Stop(); } catch { }
                try { _historySearchDebounce?.Dispose(); } catch { }
                try { _ticketSelectionDebounce?.Stop(); } catch { }
                try { _ticketSelectionDebounce?.Dispose(); } catch { }
                try { _callerSearchDebounceTimer?.Stop(); } catch { }
                try { _callerSearchDebounceTimer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void ApplyReadOnlyMode()
        {
            // Hide the entire new-ticket creation panel
            if (grpNewTicket != null)
                grpNewTicket.Visible = false;

            // Hide all write-action buttons on the case/ticket detail panel
            Button[] writeButtons =
            {
                btnCreateTicket, btnUpdateNotes, btnMarkAs,
                btnReturnTempItem, btnSyncSet, btnEscalationOverride,
                btnReopen, btnAssignToMe, btnReassignTicket,
                btnProfileDelete, btnQuickAddCaller, btnSetNewTicketEscalation
            };
            foreach (var btn in writeButtons)
                if (btn != null) btn.Visible = false;
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(236, 240, 241);

            // MAIN SPLIT CONTAINER
            this.splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 8,
                SplitterDistance = 0,
                TabStop = false
            };
            this.splitMain.Panel1.Padding = new Padding(10, 10, 5, 10);
            this.splitMain.Panel2.Padding = new Padding(5, 10, 10, 10);
            this.splitMain.Panel1MinSize = 0;
            this.splitMain.Panel2MinSize = 0;
            this.splitMain.SplitterMoving += (_, __) => this._userMovedSplitter = true;

            SetupLeftPanel();
            SetupRightPanel();

            this.Controls.Add(this.splitMain);
        }

        private void EnsureSplitLayout(bool force)
        {
            if (this.splitMain == null || this.splitMain.IsDisposed)
                return;

            if (!force && this._userMovedSplitter)
            {
                // Respect user's preference unless the split collapsed due to layout timing.
                if (this.splitMain.SplitterDistance >= this.splitMain.Panel1MinSize)
                    return;
            }

            var total = this.splitMain.Width;
            var available = total - this.splitMain.SplitterWidth;
            if (available <= 0)
                return;

            // Dynamic mins to avoid a collapsed left panel on first show across different window sizes.
            var minLeft = Math.Max(180, Math.Min(520, (int)(available * 0.30)));
            var minRight = Math.Max(260, Math.Min(720, (int)(available * 0.35)));

            // If the container is too small, shrink mins so they don't exceed available width.
            if (minLeft + minRight > available)
            {
                minLeft = Math.Max(0, available / 3);
                minRight = Math.Max(0, available - minLeft);
            }

            var maxLeft = Math.Max(0, available - minRight);
            minLeft = Math.Min(minLeft, maxLeft);

            var desired = Math.Max(minLeft, (int)(available * 0.45)); // "middle-ish" by default
            desired = Math.Min(desired, maxLeft);

            var current = this.splitMain.SplitterDistance;
            var clamped = Math.Max(minLeft, Math.Min(current, maxLeft));
            var needsFix = current < minLeft || current > maxLeft;

            if (force || needsFix || !this._splitInitialized)
            {
                try
                {
                    // Ensure SplitterDistance is within the *new* bounds before applying min sizes,
                    // otherwise SplitContainer can throw when Panel*MinSize is increased.
                    var safe = needsFix ? desired : clamped;
                    this.splitMain.SplitterDistance = safe;

                    this.splitMain.Panel1MinSize = minLeft;
                    this.splitMain.Panel2MinSize = minRight;

                    var finalMaxLeft = Math.Max(0, (this.splitMain.Width - this.splitMain.SplitterWidth) - this.splitMain.Panel2MinSize);
                    var finalMinLeft = Math.Min(this.splitMain.Panel1MinSize, finalMaxLeft);
                    var finalDesired = Math.Max(finalMinLeft, Math.Min(safe, finalMaxLeft));
                    this.splitMain.SplitterDistance = finalDesired;
                    this._splitInitialized = true;
                }
                catch
                {
                    // ignore layout exceptions
                }
            }
        }

        private void SetupLeftPanel()
        {
            // A. NEW TICKET GROUP (Top)
            this.grpNewTicket = CreateGroupCard("NEW TICKET");
            this.grpNewTicket.Dock = DockStyle.Top;
            this.grpNewTicket.Height = 560;

            var tlpNewTicket = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(5)
            };
            // Wider label column to accommodate "Initial Troubleshooting / Notes (optional)".
            tlpNewTicket.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpNewTicket.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            tlpNewTicket.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            this.cboCompany = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.cboCallerName = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.None,
                AutoCompleteSource = AutoCompleteSource.None
            };
            this.btnQuickAddCaller = new Button
            {
                Text = "+",
                Width = 34,
                Height = 32,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(6, 0, 0, 0),
                TabStop = false
            };
            this.btnQuickAddCaller.FlatAppearance.BorderSize = 0;
            this.btnQuickAddCaller.Click += async (_, __) => await QuickAddCallerEmployeeAsync();
            try { EnsureToolTip().SetToolTip(this.btnQuickAddCaller, "Quick add employee"); } catch { }
            this.cboDepartment = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.cboBranch = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.cboAssignedTo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

            // Backdate controls
            this.chkBackdate = new CheckBox
            {
                Text = "Backdate from logbook",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0),
                Checked = false
            };
            this.chkBackdate.CheckedChanged += (s, e) =>
            {
                var enabled = this.chkBackdate.Checked;
                this.dtpBackdateDate.Enabled = enabled;
                this.dtpBackdateTime.Enabled = enabled;
            };

            this.dtpBackdateDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Width = 130,
                Margin = new Padding(0),
                Enabled = false,
                Value = DateTime.Today
            };
            this.dtpBackdateTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Width = 100,
                Margin = new Padding(6, 0, 0, 0),
                Enabled = false,
                Value = DateTime.Today
            };

            this.txtTechnicalProblem = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };
            this.txtProvidedSolution = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };

            this.lblNewTicketEscalationSummary = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(90, 100, 110)
            };

            this.btnSetNewTicketEscalation = CreateButton("Set escalation", Color.FromArgb(230, 126, 34), CallMonitoringIcons.Escalated);
            this.btnSetNewTicketEscalation.Click += btnSetNewTicketEscalation_Click;

            this.lnkClearNewTicketEscalation = new LinkLabel
            {
                Text = "Clear",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                LinkColor = Color.FromArgb(230, 126, 34),
                ActiveLinkColor = Color.FromArgb(211, 84, 0),
                VisitedLinkColor = Color.FromArgb(230, 126, 34),
                Margin = new Padding(8, 8, 0, 0)
            };
            this.lnkClearNewTicketEscalation.LinkClicked += (_, __) => ClearNewTicketEscalation();

            var pnlTicketButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            this.btnCreateTicket = CreateButton("Create Ticket", Color.FromArgb(52, 152, 219), CallMonitoringIcons.NewTicket);
            this.btnClearForm = CreateButton("Clear Form", Color.FromArgb(149, 165, 166), CallMonitoringIcons.Refresh);
            this.btnCreateTicket.Click += btnCreateTicket_Click;
            this.btnClearForm.Click += btnClearForm_Click;
            pnlTicketButtons.Controls.Add(this.btnCreateTicket);
            pnlTicketButtons.Controls.Add(this.btnClearForm);

            // New Ticket required-fields validation
            this.cboCompany.SelectedIndexChanged += (_, __) => UpdateNewTicketValidationState();
            this.cboDepartment.SelectedIndexChanged += (_, __) => UpdateNewTicketValidationState();
            this.cboBranch.SelectedIndexChanged += (_, __) => UpdateNewTicketValidationState();
            this.cboCallerName.TextChanged += (_, __) => UpdateNewTicketValidationState();
            this.cboCallerName.SelectedIndexChanged += (_, __) => UpdateNewTicketValidationState();
            this.txtTechnicalProblem.TextChanged += (_, __) => UpdateNewTicketValidationState();

            tlpNewTicket.Controls.Add(MakeLabel("Company"), 0, 0); tlpNewTicket.Controls.Add(this.cboCompany, 1, 0);
            var callerHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            callerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            callerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));
            callerHost.Controls.Add(this.cboCallerName, 0, 0);
            callerHost.Controls.Add(this.btnQuickAddCaller, 1, 0);
            tlpNewTicket.Controls.Add(MakeLabel("Caller Name"), 0, 1); tlpNewTicket.Controls.Add(callerHost, 1, 1);
            tlpNewTicket.Controls.Add(MakeLabel("Department"), 0, 2); tlpNewTicket.Controls.Add(this.cboDepartment, 1, 2);
            tlpNewTicket.Controls.Add(MakeLabel("Branch"), 0, 3); tlpNewTicket.Controls.Add(this.cboBranch, 1, 3);
            tlpNewTicket.Controls.Add(MakeLabel("Assigned To"), 0, 4); tlpNewTicket.Controls.Add(this.cboAssignedTo, 1, 4);

            var pnlBackdate = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
            pnlBackdate.Controls.Add(this.chkBackdate);
            pnlBackdate.Controls.Add(this.dtpBackdateDate);
            pnlBackdate.Controls.Add(this.dtpBackdateTime);
            tlpNewTicket.Controls.Add(MakeLabel("Backdate"), 0, 5); tlpNewTicket.Controls.Add(pnlBackdate, 1, 5);

            var pnlEscalation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0) };
            pnlEscalation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlEscalation.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlEscalation.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlEscalation.Controls.Add(this.lblNewTicketEscalationSummary, 0, 0);
            pnlEscalation.Controls.Add(this.btnSetNewTicketEscalation, 1, 0);
            pnlEscalation.Controls.Add(this.lnkClearNewTicketEscalation, 2, 0);

            tlpNewTicket.Controls.Add(MakeLabel("Escalation"), 0, 6); tlpNewTicket.Controls.Add(pnlEscalation, 1, 6);
            tlpNewTicket.Controls.Add(MakeLabel("Technical Problem"), 0, 7); tlpNewTicket.Controls.Add(this.txtTechnicalProblem, 1, 7);
            tlpNewTicket.Controls.Add(MakeLabel("Initial Troubleshooting / Notes (optional)"), 0, 8); tlpNewTicket.Controls.Add(this.txtProvidedSolution, 1, 8);
            tlpNewTicket.Controls.Add(pnlTicketButtons, 1, 9);

            this.grpNewTicket.Controls.Add(tlpNewTicket);
            UpdateNewTicketValidationState();
            UpdateNewTicketEscalationUi();

            // B. PENDING CASE DETAILS
            this.grpPendingCaseDetails = CreateGroupCard("PENDING CASE DETAILS");

            var tlpCase = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(5)
            };
            tlpCase.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            tlpCase.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpCase.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpCase.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpCase.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            tlpCase.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpCase.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            this.cboStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.cboStatus.Items.AddRange(WorkflowStatuses.Cast<object>().ToArray());
            this.cboPriority = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.cboPriority.Items.AddRange(new object[] { "Low", "Medium", "High", "Critical" });

            this.cboReassignTo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            this.btnAssignToMe = CreateButton("Assign to me", Color.FromArgb(52, 152, 219), CallMonitoringIcons.Assign);
            this.btnAssignToMe.Click += btnAssignToMe_Click;
            this.btnReassignTicket = CreateButton("Reassign", Color.FromArgb(155, 89, 182), CallMonitoringIcons.User);
            this.btnReassignTicket.Click += btnReassignTicket_Click;

            this.tabTicketDetails = new TabControl { Dock = DockStyle.Fill };
            var tpProb = new TabPage("Problem");
            this.txtCaseProblem = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, Text = "Select a ticket..." };
            tpProb.Controls.Add(this.txtCaseProblem);

            var tpSol = new TabPage("Solutions");
            var splitSolutions = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6
            };
            SplitContainerUtil.BindSafeSplitterDistance(splitSolutions, () => 180);
            this.txtSolutionsLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Text = "Select a ticket to view notes..."
            };

            this.txtAttemptedSolutions = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle
            };
            splitSolutions.Panel1.Padding = new Padding(3);
            splitSolutions.Panel2.Padding = new Padding(3);
            splitSolutions.Panel1.Controls.Add(this.txtSolutionsLog);
            splitSolutions.Panel2.Controls.Add(this.txtAttemptedSolutions);
            tpSol.Controls.Add(splitSolutions);

            var tpHist = new TabPage("History");
            this.txtTabHistory = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, Text = "History..." };

            var pnlHistoryTimeline = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            pnlHistoryTimeline.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            pnlHistoryTimeline.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var tlpHistorySearch = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            tlpHistorySearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            tlpHistorySearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpHistorySearch.Controls.Add(MakeLabel("Search"), 0, 0);
            this.txtHistorySearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            tlpHistorySearch.Controls.Add(this.txtHistorySearch, 1, 0);

            this.dgvHistory = new DataGridView();
            ConfigureTimelineGrid(this.dgvHistory);
            EnsureHistoryTimelineColumns();
            this.txtHistorySearch.TextChanged += (_, __) => RequestHistorySearchFilterDebounced();

            pnlHistoryTimeline.Controls.Add(tlpHistorySearch, 0, 0);
            pnlHistoryTimeline.Controls.Add(this.dgvHistory, 0, 1);
            tpHist.Controls.Add(pnlHistoryTimeline);

            this.tabTicketDetails.TabPages.Add(tpProb);
            this.tabTicketDetails.TabPages.Add(tpSol);
            this.tabTicketDetails.TabPages.Add(tpHist);

            var pnlCaseButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            this.btnUpdateNotes = CreateButton("Update", Color.FromArgb(52, 152, 219), CallMonitoringIcons.Save);
            this.btnMarkAs = CreateButton("Mark As...", Color.FromArgb(52, 73, 94), CallMonitoringIcons.Edit);
            this.btnReturnTempItem = CreateButton("Return Temp Item", Color.FromArgb(124, 58, 237), CallMonitoringIcons.Refresh);
            this.btnReturnTempItem.Visible = false;
            this.btnSyncSet = CreateButton("Sync Set", Color.FromArgb(46, 204, 113), CallMonitoringIcons.Refresh);
            this.btnEscalationOverride = CreateButton("Escalation Override", Color.FromArgb(230, 126, 34), CallMonitoringIcons.Alert);
            this.btnReopen = CreateButton("Reopen", Color.FromArgb(231, 76, 60), CallMonitoringIcons.Reopened);
            
            // Events
            this.btnUpdateNotes.Click += btnUpdateNotes_Click;
            this.btnMarkAs.Click += btnMarkAs_Click;
            this.btnReturnTempItem.Click += btnReturnTempItem_Click;
            this.btnSyncSet.Click += btnSyncSet_Click;
            this.btnEscalationOverride.Click += btnEscalationOverride_Click;
            this.btnReopen.Click += btnReopen_Click;

            pnlCaseButtons.Controls.Add(this.btnUpdateNotes);
            pnlCaseButtons.Controls.Add(this.btnMarkAs);
            pnlCaseButtons.Controls.Add(this.btnReturnTempItem);
            pnlCaseButtons.Controls.Add(this.btnSyncSet);
            pnlCaseButtons.Controls.Add(this.btnEscalationOverride);
            pnlCaseButtons.Controls.Add(this.btnReopen);

            tlpCase.Controls.Add(MakeLabel("Status"), 0, 0); tlpCase.Controls.Add(this.cboStatus, 1, 0);
            tlpCase.Controls.Add(MakeLabel("Priority"), 0, 1); tlpCase.Controls.Add(this.cboPriority, 1, 1);
            tlpCase.Controls.Add(MakeLabel("Assigned To"), 0, 2);
            var tlpAssign = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            tlpAssign.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpAssign.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlpAssign.Controls.Add(this.cboReassignTo, 0, 0);

            var pnlAssignButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };
            pnlAssignButtons.Controls.Add(this.btnAssignToMe);
            pnlAssignButtons.Controls.Add(this.btnReassignTicket);
            tlpAssign.Controls.Add(pnlAssignButtons, 1, 0);
            tlpCase.Controls.Add(tlpAssign, 1, 2);
            tlpCase.Controls.Add(this.tabTicketDetails, 0, 3); tlpCase.SetColumnSpan(this.tabTicketDetails, 2);
            tlpCase.Controls.Add(pnlCaseButtons, 0, 4); tlpCase.SetColumnSpan(pnlCaseButtons, 2);

            this.grpPendingCaseDetails.Controls.Add(tlpCase);

            this.splitMain.Panel1.Controls.Add(this.grpPendingCaseDetails);
            this.splitMain.Panel1.Controls.Add(this.grpNewTicket);
            this.grpNewTicket.BringToFront();
            this.grpPendingCaseDetails.BringToFront();
            
            // Fix order to match main form behavior
            this.splitMain.Panel1.Controls.Clear();
            this.splitMain.Panel1.Controls.Add(this.grpPendingCaseDetails);
            this.splitMain.Panel1.Controls.Add(this.grpNewTicket);

            // Ensure action buttons start disabled until a ticket is selected.
            UpdateTicketActionStates();
        }

        private void SetupRightPanel()
        {
            this.tlpRight = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(0)
            };
            this.tlpRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            // Split remaining space so Pending Tickets doesn't grow excessively (reduces empty area under the grid).
            // The last row is an intentional spacer (no control is added there).
            this.tlpRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Pending Tickets (Takes dominant space)
            this.tlpRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // Recently Resolved Tickets (Reduced back to compact size)
            this.tlpRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // Summary
            this.tlpRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F)); // Profile (selection details + actions)
            this.tlpRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));   // Reserved
            this.tlpRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));   // Spacer

            // 0. PENDING TICKETS
            this.grpPendingTickets = CreateGroupCard("PENDING TICKETS");
            // Compact Layout:
            // Row 0: Search[1] | Assignee[1] | Branch[1] | Filters[1]
            // Row 1: Legend (Auto)
            // Row 2: Grid (Percent)
            // Row 3: Pager (Auto)
            var tlpPending = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
            tlpPending.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Search
            tlpPending.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Assignee
            tlpPending.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Branch
            tlpPending.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Filter+Btn
            
            tlpPending.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F)); // Filters
            tlpPending.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Legend
            tlpPending.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Grid
            tlpPending.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Pager 

            this.txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.White,
                ForeColor = Color.Black
            };
            SetTextBoxCueBanner(this.txtSearch, "Search...");

            this.cboFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.White,
                ForeColor = Color.Black
            };
            this.cboFilter.Items.AddRange(new object[] { "All", "Pending", "In Progress", "Escalated", "Overdue", "Solved", "Reopened" });
            if (this.cboFilter.Items.Count > 0) this.cboFilter.SelectedIndex = 0;

            this.cboBranchFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = Color.White, ForeColor = Color.Black };
            this.cboAssigneeFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), BackColor = Color.White, ForeColor = Color.Black };

            // Ensure dropdowns don't look "blank" before LoadDataAsync() populates real values.
            PopulateAssigneeFilterCombo(this.cboAssigneeFilter, Enumerable.Empty<LookupItem>());
            PopulateBranchCombo(this.cboBranchFilter, Enumerable.Empty<LookupItem>(), "(All Branches)");
            this.btnApplyFilter = CreateButton("Filter", Color.FromArgb(52, 152, 219), CallMonitoringIcons.Filter);
            this.btnApplyFilter.Click += (s, e) =>
            {
                ResetTicketPaging();
                _ = SafeRefreshTicketGridsAsync();
            };

            this.pnlStatusLegend = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true };
            // Compact badges (removed text to save space? No, just keep them but ensure wrapping is efficient)
            AddStatusBadge("Pending", Color.FromArgb(52, 152, 219));
            AddStatusBadge("In Progress", Color.FromArgb(155, 89, 182));
            AddStatusBadge("Escalated", Color.FromArgb(243, 156, 18));
            AddStatusBadge("Overdue", Color.FromArgb(231, 76, 60));
            AddStatusBadge("Solved", Color.FromArgb(46, 204, 113));
            AddQuickFilterButtons();

            this.dgvPendingTickets = new DataGridView();
            ConfigureTicketsGrid(this.dgvPendingTickets);
            this.dgvPendingTickets.ScrollBars = ScrollBars.None;
            this.dgvPendingTickets.MultiSelect = false;
            this.dgvPendingTickets.SelectionChanged += PendingTickets_SelectionChanged;
            this.dgvPendingTickets.CellDoubleClick += TicketGrid_CellDoubleClick;

            var pnlPendingPager = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true
            };

            this.btnPendingNext = CreateButton("Next", Color.FromArgb(52, 152, 219), CallMonitoringIcons.Next);
            this.btnPendingNext.Width = 80;
            this.btnPendingNext.Height = 26;
             this.btnPendingNext.Click += async (s, e) => {
                 if (_pendingHasNext) { _pendingPageIndex++; await SafeRefreshTicketGridsAsync(); }
             };

            this.lblPendingPage = new Label
            {
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(60, 60, 60),
                Padding = new Padding(8, 6, 8, 0),
                Text = "Page 1 of 1 (0)"
            };

            this.btnPendingPrev = CreateButton("Prev", Color.FromArgb(149, 165, 166), CallMonitoringIcons.Previous);
            this.btnPendingPrev.Width = 80;
            this.btnPendingPrev.Height = 26;
             this.btnPendingPrev.Click += async (s, e) => {
                 if (_pendingPageIndex > 1) { _pendingPageIndex--; await SafeRefreshTicketGridsAsync(); }
             };

            pnlPendingPager.Controls.Add(this.btnPendingNext);
            pnlPendingPager.Controls.Add(this.lblPendingPage);
            pnlPendingPager.Controls.Add(this.btnPendingPrev);

            // Row 0 Controls: Search | Assignee | Branch | Filter+Btn
            tlpPending.Controls.Add(this.txtSearch, 0, 0);
            
            // Assignee (Label removed, using placeholder text or just the combo)
            tlpPending.Controls.Add(this.cboAssigneeFilter, 1, 0);
            
            // Branch (Label removed)
            tlpPending.Controls.Add(this.cboBranchFilter, 2, 0);

            // Filter Cluster (Combo + Button)
            var pnlFilterBtn = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            pnlFilterBtn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlFilterBtn.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlFilterBtn.Controls.Add(this.cboFilter, 0, 0);
            pnlFilterBtn.Controls.Add(this.btnApplyFilter, 1, 0);
            tlpPending.Controls.Add(pnlFilterBtn, 3, 0);

            // Row 1: Legend
            tlpPending.Controls.Add(this.pnlStatusLegend, 0, 1); 
            tlpPending.SetColumnSpan(this.pnlStatusLegend, 4);
            
            // Row 2: Grid
            tlpPending.Controls.Add(this.dgvPendingTickets, 0, 2); 
            tlpPending.SetColumnSpan(this.dgvPendingTickets, 4);

            // Row 3: Pager
            tlpPending.Controls.Add(pnlPendingPager, 0, 3); 
            tlpPending.SetColumnSpan(pnlPendingPager, 4);
            
            this.grpPendingTickets.Controls.Add(tlpPending);

            // 1. RESOLVED TICKETS
            this.grpSolvedTickets = CreateGroupCard("RECENTLY RESOLVED TICKETS");
            this.dgvSolvedTickets = new DataGridView();
            ConfigureTicketsGrid(this.dgvSolvedTickets);
            this.dgvSolvedTickets.ScrollBars = ScrollBars.None;
            this.dgvSolvedTickets.SelectionChanged += SolvedTickets_SelectionChanged;
            this.dgvSolvedTickets.CellDoubleClick += TicketGrid_CellDoubleClick;

            var pnlSolvedPager = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true
            };
            this.btnSolvedNext = CreateButton("Next", Color.FromArgb(52, 152, 219), CallMonitoringIcons.Next);
            this.btnSolvedNext.Width = 80;
            this.btnSolvedNext.Height = 26;
            this.btnSolvedNext.Click += async (s, e) =>
            {
                if (_solvedHasNext)
                {
                    _solvedPageIndex++;
                    await SafeRefreshTicketGridsAsync();
                }
            };
            this.lblSolvedPage = new Label { AutoSize = true, Text = "Page 1 of 1 (0)", Padding = new Padding(8, 6, 8, 0) };
            this.btnSolvedPrev = CreateButton("Prev", Color.FromArgb(149, 165, 166), CallMonitoringIcons.Previous);
            this.btnSolvedPrev.Width = 80;
            this.btnSolvedPrev.Height = 26;
            this.btnSolvedPrev.Click += async (s, e) =>
            {
                if (_solvedPageIndex > 1)
                {
                    _solvedPageIndex--;
                    await SafeRefreshTicketGridsAsync();
                }
            };
            pnlSolvedPager.Controls.Add(this.btnSolvedNext);
            pnlSolvedPager.Controls.Add(this.lblSolvedPage);
            pnlSolvedPager.Controls.Add(this.btnSolvedPrev);
            
            // Dock layout: Add Pager FIRST (Index 1 when Grid is added) so Dock=Bottom reserves space.
            this.grpSolvedTickets.Controls.Add(pnlSolvedPager);
            this.grpSolvedTickets.Controls.Add(this.dgvSolvedTickets);

            // 2. SUMMARY
            this.grpTicketSummary = CreateGroupCard("TICKET SUMMARY");

            var tlpSummary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(8, 2, 8, 0)
            };
            tlpSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // Slightly smaller ID
            tlpSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F)); // Age
            tlpSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // SLA
            tlpSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Esc
            tlpSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            tlpSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label MakeSummaryHeader(string text) => new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft, // Bottom align to sit on data
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 160, 160)
            };

            this.lblSummaryId = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft, // Top align data
                Font = new Font("Segoe UI", 12F, FontStyle.Bold), // Larger ID
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(44, 62, 80)
            };
            this.lblSummaryAge = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80)
            };
            this.lblSummarySla = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), // Slightly smaller to fit "Breached..."
                ForeColor = Yakult.Inventory.App.Helpers.ModernUiHelper.ColorPrimary
            };
            this.lblSummaryEscalation = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Yakult.Inventory.App.Helpers.ModernUiHelper.ColorDanger,
                AutoEllipsis = true
            };

            tlpSummary.Controls.Add(MakeSummaryHeader("TICKET"), 0, 0);
            tlpSummary.Controls.Add(MakeSummaryHeader("AGE"), 1, 0);
            tlpSummary.Controls.Add(MakeSummaryHeader("SLA"), 2, 0);
            tlpSummary.Controls.Add(MakeSummaryHeader("ESCALATION"), 3, 0); // Spelled out

            tlpSummary.Controls.Add(this.lblSummaryId, 0, 1);
            tlpSummary.Controls.Add(this.lblSummaryAge, 1, 1);
            tlpSummary.Controls.Add(this.lblSummarySla, 2, 1);
            tlpSummary.Controls.Add(this.lblSummaryEscalation, 3, 1);

            this.grpTicketSummary.Controls.Add(tlpSummary);

            // Make summary clickable (read-only dialog)
            WireTicketSummaryClick(this.grpTicketSummary);
            WireTicketSummaryClick(tlpSummary);
            WireTicketSummaryClick(this.lblSummaryId);
            WireTicketSummaryClick(this.lblSummaryAge);
            WireTicketSummaryClick(this.lblSummarySla);
            WireTicketSummaryClick(this.lblSummaryEscalation);

            // 3. PROFILE (details + actions)
            this.grpEscalation = CreateGroupCard("TICKET PROFILE");

            // Keep container height fixed (tlpRight row is Absolute).
            // To avoid clipping at higher DPI/scaling, put fields inside an AutoScroll panel.
            var pnlProfileRoot = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var pnlProfileScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var tlpProfileFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0)
            };
            tlpProfileFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpProfileFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpProfileFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Row 0: Status/Priority
            tlpProfileFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Row 1: Assigned/Caller
            tlpProfileFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Row 2: Location/Activity
            tlpProfileFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Row 3: Escalation/Last Email

            Label MakeProfileValueLabel()
            {
                var font = new Font("Segoe UI", 9F, FontStyle.Bold);
                return new Label
                {
                    Text = "-",
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = font,
                    ForeColor = Color.FromArgb(44, 62, 80),
                    AutoSize = false,
                    Height = 18,
                    AutoEllipsis = false,
                    Margin = new Padding(0, 2, 0, 0)
                };
            }

            Panel MakeProfileFieldExisting(string headerText, Label valueLabel)
            {
                var container = new Panel
                {
                    // Important: keep profile fields "tight" vertically and prevent them from
                    // stretching when the parent container has extra height (DPI/layout quirks).
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(0, 0, 0, 2),
                    Margin = new Padding(0)
                };
                var header = new Label
                {
                    Text = headerText,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(160, 160, 160),
                    Margin = new Padding(0)
                };

                container.Controls.Add(valueLabel);
                container.Controls.Add(header);
                return container;
            }

            Panel MakeProfileField(string headerText, out Label valueLabel)
            {
                valueLabel = MakeProfileValueLabel();
                return MakeProfileFieldExisting(headerText, valueLabel);
            }

            tlpProfileFields.Controls.Add(MakeProfileField("STATUS", out this.lblProfileStatus), 0, 0);
            tlpProfileFields.Controls.Add(MakeProfileField("PRIORITY", out this.lblProfilePriority), 1, 0);
            tlpProfileFields.Controls.Add(MakeProfileField("ASSIGNED TO", out this.lblProfileAssignee), 0, 1);
            tlpProfileFields.Controls.Add(MakeProfileField("CALLER", out this.lblProfileCaller), 1, 1);
            tlpProfileFields.Controls.Add(MakeProfileField("LOCATION", out this.lblProfileLocation), 0, 2);

            // Keep these existing members, but render them using the same compact profile field layout.
            this.lblEmailInfo = MakeProfileValueLabel();
            tlpProfileFields.Controls.Add(MakeProfileFieldExisting("ACTIVITY", this.lblEmailInfo), 1, 2);

            this.lblNextEscalation = MakeProfileValueLabel();
            tlpProfileFields.Controls.Add(MakeProfileFieldExisting("ESCALATION", this.lblNextEscalation), 0, 3);

            this.lblLastEmail = MakeProfileValueLabel();
            tlpProfileFields.Controls.Add(MakeProfileFieldExisting("LAST EMAIL", this.lblLastEmail), 1, 3);

            void AdjustProfileFieldWrapping()
            {
                try
                {
                    var widths = tlpProfileFields.GetColumnWidths();
                    var colWidth = widths != null && widths.Length > 0 ? widths[0] : Math.Max(1, tlpProfileFields.ClientSize.Width / 2);
                    var max = Math.Max(120, colWidth - 10);
                    var minHeight = 18;

                    var labels = new[]
                    {
                        this.lblProfileStatus,
                        this.lblProfilePriority,
                        this.lblProfileAssignee,
                        this.lblProfileCaller,
                        this.lblProfileLocation,
                        this.lblEmailInfo,
                        this.lblNextEscalation,
                        this.lblLastEmail
                    };

                    foreach (var lbl in labels)
                    {
                        if (lbl == null) continue;
                        // Allow wrapping to use vertical whitespace instead of truncating with "...".
                        lbl.MaximumSize = new Size(max, 0);
                        lbl.Width = max;

                        var text = string.IsNullOrWhiteSpace(lbl.Text) ? "-" : lbl.Text;
                        var measured = TextRenderer.MeasureText(
                            text,
                            lbl.Font,
                            new Size(max, int.MaxValue),
                            TextFormatFlags.WordBreak);
                        lbl.Height = Math.Max(minHeight, measured.Height);
                    }
                }
                catch
                {
                    // Best-effort only; wrapping is a UI enhancement.
                }
            }

            tlpProfileFields.Layout += (_, __) => AdjustProfileFieldWrapping();
            // Text updates don't always trigger layout; force recalculation when values change.
            foreach (var lbl in new[] { this.lblProfileStatus, this.lblProfilePriority, this.lblProfileAssignee, this.lblProfileCaller, this.lblProfileLocation, this.lblEmailInfo, this.lblNextEscalation, this.lblLastEmail })
            {
                if (lbl == null) continue;
                lbl.TextChanged += (_, __) => AdjustProfileFieldWrapping();
            }

            var pnlProfileActions = new FlowLayoutPanel
            {
                // Same idea as fields: don't stretch vertically; keep aligned.
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(8, 4, 8, 0)
            };

            this.btnProfileDetails = CreateButton("Profile", Color.FromArgb(52, 152, 219), CallMonitoringIcons.View);
            this.btnProfileDetails.Width = 110;
            this.btnProfileDetails.Height = 26;
            this.btnProfileDetails.Enabled = false;
            this.btnProfileDetails.Click += (_, __) =>
            {
                if (this._selectedTicket == null || this._selectedTicket.TicketId <= 0) return;
                OpenTicketDetailsDialog(this._selectedTicket.TicketId);
            };

            this.btnProfileSummary = CreateButton("Summary", Color.FromArgb(149, 165, 166), CallMonitoringIcons.Note);
            this.btnProfileSummary.Width = 90;
            this.btnProfileSummary.Height = 26;
            this.btnProfileSummary.Enabled = false;
            this.btnProfileSummary.Click += (_, __) => OpenTicketSummaryDialog();

            this.btnProfileDelete = CreateButton("Delete", Color.FromArgb(231, 76, 60), CallMonitoringIcons.Delete);
            this.btnProfileDelete.Width = 90;
            this.btnProfileDelete.Height = 26;
            this.btnProfileDelete.Enabled = false;
            this.btnProfileDelete.Click += async (_, __) => await DeleteSelectedTicketAsync();

            pnlProfileActions.Controls.Add(this.btnProfileDetails);
            pnlProfileActions.Controls.Add(this.btnProfileSummary);
            pnlProfileActions.Controls.Add(this.btnProfileDelete);

            pnlProfileScroll.Controls.Add(tlpProfileFields);

            // Dock order matters: actions at bottom-ish, fields scroll above.
            pnlProfileRoot.Controls.Add(pnlProfileScroll);
            pnlProfileRoot.Controls.Add(pnlProfileActions);

            this.grpEscalation.Controls.Add(pnlProfileRoot);

            // Ensure initial widths are set so long values wrap instead of showing ellipses.
            AdjustProfileFieldWrapping();

            this.tlpRight.Controls.Add(this.grpPendingTickets, 0, 0);
            this.tlpRight.Controls.Add(this.grpSolvedTickets, 0, 1);
            this.tlpRight.Controls.Add(this.grpTicketSummary, 0, 2);
            this.tlpRight.Controls.Add(this.grpEscalation, 0, 3);

            this.splitMain.Panel2.Controls.Add(this.tlpRight);
        }

        // --- HELPERS ---

        private void WireTicketSummaryClick(Control control)
        {
            if (control == null)
                return;

            try
            {
                control.Cursor = Cursors.Hand;
                control.Click += (_, __) => OpenTicketSummaryDialog();
            }
            catch
            {
            }
        }

        private Button CreateButton(string text, Color backColor, string icon = null)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 32,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                AutoSize = true,
                UseCompatibleTextRendering = true,
                Padding = new Padding(6, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            if (!string.IsNullOrEmpty(icon))
            {
                btn.Image = RenderIconBitmap(icon, 14, Color.White);
                btn.ImageAlign = ContentAlignment.MiddleLeft;
                btn.TextAlign = ContentAlignment.MiddleRight;
                btn.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            return btn;
        }

        private static Bitmap RenderIconBitmap(string icon, int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (var font = new Font("Segoe MDL2 Assets", size - 2f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(icon, font, brush, new RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(44, 62, 80)
            };
        }

        private Panel CreateGroupCard(string title)
        {
            var card = Yakult.Inventory.App.Helpers.ModernUiHelper.CreateCard();
            card.Dock = DockStyle.Fill;
            // Reduce default chrome so tables/grids have more vertical room.
            card.Padding = new Padding(12);
            card.Margin = new Padding(6);

            // Use a dedicated layout with an auto-sized header row and a fill content row
            // to prevent overlap and accidental clipping.
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = card.BackColor,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 4, 0, 6),
                Margin = new Padding(0)
            };
            var lbl = Yakult.Inventory.App.Helpers.ModernUiHelper.CreateSectionHeader(title);
            lbl.AutoSize = true;
            lbl.Margin = new Padding(0);
            // Override helper’s generous vertical padding to reclaim space.
            lbl.Padding = new Padding(0, 6, 0, 4);
            header.Controls.Add(lbl);

            var content = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(content, 0, 1);
            card.Controls.Add(layout);

            var suppress = false;
            card.ControlAdded += (_, e) =>
            {
                if (suppress) return;
                if (e.Control == null) return;
                if (e.Control == layout || e.Control == header || e.Control == content) return;
                if (!ReferenceEquals(e.Control.Parent, card)) return;

                try
                {
                    suppress = true;
                    card.Controls.Remove(e.Control);
                    content.Controls.Add(e.Control);
                }
                finally
                {
                    try { header.BringToFront(); } catch { }
                    suppress = false;
                }
            };

            return card;
        }

        private void AddStatusBadge(string status, Color color)
        {
            var lbl = new Label
            {
                Text = status,
                BackColor = color,
                ForeColor = Color.White,
                AutoSize = true,
                Padding = new Padding(5),
                Margin = new Padding(3),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            this.pnlStatusLegend.Controls.Add(lbl);
        }

        private void ConfigureTicketsGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 35;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 240, 241);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;

            // Zebra striping for better readability
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            
            // Row hover effect
            grid.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count)
                {
                    var row = grid.Rows[e.RowIndex];
                    if (!row.Selected)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(235, 245, 251);
                    }
                }
            };
            grid.CellMouseLeave += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count)
                {
                    var row = grid.Rows[e.RowIndex];
                    if (!row.Selected)
                    {
                        row.DefaultCellStyle.BackColor = (e.RowIndex % 2 == 1) 
                            ? Color.FromArgb(248, 249, 250) 
                            : Color.White;
                    }
                }
            };

            try
            {
                typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(grid, true, null);
            }
            catch
            {
            }
        }

        private void ConfigureTimelineGrid(DataGridView grid)
        {
            ConfigureTicketsGrid(grid);
            grid.MultiSelect = false;
            grid.ScrollBars = ScrollBars.Vertical;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        }

        private void EnsureHistoryTimelineColumns()
        {
            if (this.dgvHistory == null) return;
            if (this.dgvHistory.Columns.Count > 0) return;

            this.dgvHistory.Columns.Add("colHistWhen", "When");
            this.dgvHistory.Columns.Add("colHistField", "Field");
            this.dgvHistory.Columns.Add("colHistOld", "Old");
            this.dgvHistory.Columns.Add("colHistNew", "New");
            this.dgvHistory.Columns.Add("colHistBy", "By");
            this.dgvHistory.Columns.Add("colHistNote", "Note");

            this.dgvHistory.Columns["colHistWhen"].FillWeight = 16;
            this.dgvHistory.Columns["colHistField"].FillWeight = 16;
            this.dgvHistory.Columns["colHistOld"].FillWeight = 17;
            this.dgvHistory.Columns["colHistNew"].FillWeight = 17;
            this.dgvHistory.Columns["colHistBy"].FillWeight = 14;
            this.dgvHistory.Columns["colHistNote"].FillWeight = 20;

            this.dgvHistory.Columns["colHistOld"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            this.dgvHistory.Columns["colHistNew"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            this.dgvHistory.Columns["colHistNote"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            this.dgvHistory.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        }

        private void RenderSolutionNotes()
        {
            if (this.txtSolutionsLog == null) return;

            var solutionNotes = (this._notesCacheSelected ?? new List<CallTicketNoteItem>())
                .Where(n => string.Equals((n.NoteType ?? string.Empty).Trim(), "Solution", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.CreatedAt ?? DateTime.MinValue)
                .ToList();

            var sbNotes = new StringBuilder();

            if (solutionNotes.Count == 0) sbNotes.AppendLine("No notes yet.");
            else foreach (var n in solutionNotes) AppendNoteBlock(sbNotes, n, showType: false);

            try
            {
                this.txtSolutionsLog.Text = sbNotes.ToString().TrimEnd();
                this.txtSolutionsLog.SelectionStart = 0;
                this.txtSolutionsLog.SelectionLength = 0;
            }
            catch
            {
            }
        }

        private static void AppendNoteBlock(StringBuilder sb, CallTicketNoteItem n, bool showType)
        {
            if (sb == null || n == null) return;

            var text = (n.NoteText ?? string.Empty).Trim();
            if (text.Length == 0)
                return;

            var when = n.CreatedAt.HasValue ? ToLocalString(n.CreatedAt.Value, "g") : "-";
            var by = string.IsNullOrWhiteSpace(n.CreatedByName) ? "-" : n.CreatedByName.Trim();
            var type = string.IsNullOrWhiteSpace(n.NoteType) ? "-" : n.NoteType.Trim();

            sb.Append(when).Append(" - ").Append(by);
            if (showType) sb.Append("  [").Append(type).Append(']');
            sb.AppendLine();
            sb.AppendLine(text);
            sb.AppendLine();
        }

        private void ApplyHistoryFilterAndRender()
        {
            if (this.dgvHistory == null) return;
            EnsureHistoryTimelineColumns();

            var query = (this.txtHistorySearch?.Text ?? string.Empty).Trim();
            var q = query.Length == 0 ? null : query;

            var list = this._historyCacheSelected ?? new List<CallTicketHistoryItem>();
            IEnumerable<CallTicketHistoryItem> filtered = list;
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(h =>
                    ContainsIgnoreCase(h.FieldName, q) ||
                    ContainsIgnoreCase(h.OldValue, q) ||
                    ContainsIgnoreCase(h.NewValue, q) ||
                    ContainsIgnoreCase(h.ChangedByName, q) ||
                    ContainsIgnoreCase(h.Note, q));
            }

            var ordered = filtered
                .OrderByDescending(h => h.ChangedAt ?? DateTime.MinValue)
                .ToList();

            this.dgvHistory.SuspendLayout();
            try
            {
                this.dgvHistory.Rows.Clear();
                foreach (var h in ordered)
                {
                    var when = h.ChangedAt.HasValue ? ToLocalString(h.ChangedAt.Value, "g") : "-";
                    var field = string.IsNullOrWhiteSpace(h.FieldName) ? "-" : h.FieldName;
                    var oldV = h.OldValue ?? string.Empty;
                    var newV = h.NewValue ?? string.Empty;
                    var by = string.IsNullOrWhiteSpace(h.ChangedByName) ? "-" : h.ChangedByName;
                    var note = h.Note ?? string.Empty;

                    var idx = this.dgvHistory.Rows.Add(when, field, oldV, newV, by, note);
                    this.dgvHistory.Rows[idx].Tag = h;
                }
            }
            finally
            {
                this.dgvHistory.ResumeLayout();
            }
        }
        
        // --- DATA BINDING & EVENTS ---

        public async Task LoadDataAsync()
        {
            this._suppressNewTicketValidation = true;
            try
            {
                if (!await _callRepo.CallSchemaExistsAsync())
                {
                    // Schema not ready
                    return;
                }

                // 1. Employees/Departments
                var companiesTask = _callRepo.GetCompaniesAsync();
                var deptsTask = _callRepo.GetDepartmentsAsync();
                await Task.WhenAll(companiesTask, deptsTask);
                var companies = companiesTask.Result;
                var depts = deptsTask.Result;
                var itDeptName = GuessItDepartmentName(depts);
                var itEmployees = await _callRepo.GetCallAssignmentCandidatesByDepartmentNameAsync(itDeptName);

                // 2. Escalation Settings
                var escSettingsTask = _callRepo.GetEscalationSettingsAsync();
                var escalationOverrideEnabledTask = _callRepo.EscalationOverridesEnabledAsync();
                await Task.WhenAll(escSettingsTask, escalationOverrideEnabledTask);

                _escalationSettings = escSettingsTask.Result;

                // Enable/disable per-ticket escalation overrides in the UI based on schema presence.
                var escalationOverrideEnabled = escalationOverrideEnabledTask.Result;
                _escalationOverridesInstalled = escalationOverrideEnabled;
                if (this.btnSetNewTicketEscalation != null)
                    this.btnSetNewTicketEscalation.Enabled = escalationOverrideEnabled;
                UpdateNewTicketEscalationUi();

                // 3. Populate Combos
                // New Ticket
                PopulateLookupCombo(this.cboCompany, companies);
                PopulateLookupCombo(this.cboDepartment, depts);
                PopulateEmployeeCombo(this.cboAssignedTo, itEmployees);
                // Default to unassigned to avoid auto-assigning the first IT employee.
                if (this.cboAssignedTo.Items.Count > 0) this.cboAssignedTo.SelectedIndex = 0;

                if (!_branchEventsWired)
                {
                    _branchEventsWired = true;
this.cboCompany.SelectedIndexChanged += async (_, __) =>
                      {
                          if (_loadingNewTicketLookups) return;
                          if (_newTicketOrgLockedFromEmployeeSearch)
                          {
                              UnlockNewTicketOrgFromEmployeeSearch();
                              return;
                          }
                          try
                          {
                              await ReloadNewTicketDepartmentsAsync();
                              await ReloadNewTicketBranchesAsync(preserveSelection: false);
                              await ReloadNewTicketCallerEmployeesAsync(preserveSelection: false);
                          }
                          catch (Exception ex)
                          {
                              ShowFriendlyError(
                                  "Call Monitoring",
                                  "We couldn't reload the company, branch, and caller lists right now.",
                                  ex,
                                  "ReloadNewTicketLookups");
                          }
                      };
                       this.cboDepartment.SelectedIndexChanged += async (_, __) =>
                       {
                           if (_loadingNewTicketLookups) return;
                           if (_newTicketOrgLockedFromEmployeeSearch)
                           {
                               UnlockNewTicketOrgFromEmployeeSearch();
                               return;
                           }
                           try
                           {
                               // Department is an attribute of the ticket/caller, not a strict filter for branches.
                               // In production, a single Branch can contain multiple departments; filtering branches by
                               // Branch.DeptId causes valid combinations (e.g., Alabang Center + Engineering) to disappear.
                               // Keep the branch list stable and only re-validate required fields.
                               await ReloadNewTicketCallerEmployeesAsync(preserveSelection: false);
                               UpdateNewTicketValidationState();
                           }
                           catch (Exception ex)
                           {
                               ShowFriendlyError(
                                   "Call Monitoring",
                                   "We couldn't refresh the caller and validation state right now.",
                                   ex,
                                   "UpdateNewTicketValidation");
                           }
                       };
                    this.cboBranch.SelectedIndexChanged += async (_, __) =>
                    {
                        if (_loadingNewTicketLookups) return;
                        if (_newTicketOrgLockedFromEmployeeSearch)
                        {
                            UnlockNewTicketOrgFromEmployeeSearch();
                            return;
                        }
                        try
                        {
                            await ReloadNewTicketCallerEmployeesAsync(preserveSelection: false);
                            UpdateNewTicketValidationState();
                        }
                        catch
                        {
                            UpdateNewTicketValidationState();
                        }
                    };
                    if (this.cboBranchFilter != null)
                        this.cboBranchFilter.SelectedIndexChanged += (_, __) =>
                        {
                            if (_suppressBranchFilterRefresh)
                                return;

                            ResetTicketPaging();
                            _ = SafeRefreshTicketGridsAsync();
                        };
                }

                // Pending Details
                PopulateEmployeeCombo(this.cboReassignTo, itEmployees);
                 
                // Filter
                PopulateAssigneeFilterCombo(this.cboAssigneeFilter, itEmployees);
                await ReloadBranchFilterAsync();

                 try
                 {
                     _loadingNewTicketLookups = true;
                     await ReloadNewTicketDepartmentsAsync();
                     await ReloadNewTicketBranchesAsync(preserveSelection: false);
                     await ReloadNewTicketCallerEmployeesAsync(preserveSelection: false);
                 }
                 finally
                 {
                     _loadingNewTicketLookups = false;
                 }
 
                 // 4. Load Grids
                 await SafeRefreshTicketGridsAsync();
            }
            catch (Exception ex)
            {
                ShowLookupLoadError(ex);
            }
            finally
            {
                this._suppressNewTicketValidation = false;
                UpdateNewTicketValidationState();
            }
        }

        private Task RefreshTicketGridsLatestAsync()
        {
            var version = Interlocked.Increment(ref _ticketsRefreshVersion);
            return RefreshTicketGridsAsync(version);
        }

        private async Task RefreshTicketGridsAsync(int version)
        {
            if (version != _ticketsRefreshVersion)
                return;

            if (!await _callRepo.CallSchemaExistsAsync())
                return;

            if (version != _ticketsRefreshVersion)
                return;

            var statusFilter = this.cboFilter?.SelectedItem?.ToString() ?? "All";
            var search = this.txtSearch?.Text ?? string.Empty;
            var assignee = this.cboAssigneeFilter?.SelectedItem as LookupItem;
            var branchFilter = this.cboBranchFilter?.SelectedItem as LookupItem;
            int? branchIdFilter = (branchFilter != null && branchFilter.Id > 0) ? (int?)branchFilter.Id : null;
            var preferredTicketId = _selectedTicket?.TicketId;

            string assigneeName = null;
            var unassignedOnly = false;
            if (assignee != null && assignee.Id != -1)
            {
                if (assignee.Id == 0) unassignedOnly = true;
                else assigneeName = assignee.Name?.Trim();
            }

            // Pending
            List<CallTicketListItem> pendingPage = null;
            while (true)
            {
                var pending = await _callRepo.GetPendingTicketsPageResultAsync(
                    statusFilter,
                    search,
                    assigneeName,
                    companyId: null,
                    branchId: branchIdFilter,
                    unassignedOnly: unassignedOnly,
                    overdueDays: _overdueDays,
                    pageIndex: _pendingPageIndex,
                    pageSize: PendingPageSize);

                if (version != _ticketsRefreshVersion)
                    return;

                if (pending.Items.Count == 0 && _pendingPageIndex > 1)
                {
                    _pendingPageIndex--;
                    continue;
                }

                _pendingHasNext = pending.HasNext;
                _pendingTotalCount = pending.TotalCount;
                pendingPage = pending.Items ?? new List<CallTicketListItem>();
                break;
            }

            if (version != _ticketsRefreshVersion)
                return;

            // Solved
            List<CallTicketListItem> solvedPage = null;
            while (true)
            {
                var resolved = await _callRepo.GetRecentlyResolvedTicketsPageResultAsync(
                    search,
                    assigneeName,
                    companyId: null,
                    branchId: branchIdFilter,
                    unassignedOnly: unassignedOnly,
                    pageIndex: _solvedPageIndex,
                    pageSize: SolvedPageSize);

                if (version != _ticketsRefreshVersion)
                    return;

                if (resolved.Items.Count == 0 && _solvedPageIndex > 1)
                {
                    _solvedPageIndex--;
                    continue;
                }

                _solvedHasNext = resolved.HasNext;
                _solvedTotalCount = resolved.TotalCount;
                solvedPage = resolved.Items ?? new List<CallTicketListItem>();
                break;
            }

            if (version != _ticketsRefreshVersion)
                return;

            _suppressTicketSelectionChanged = true;
            try
            {
                BindPendingGrid(pendingPage ?? new List<CallTicketListItem>());
                BindSolvedGrid(solvedPage ?? new List<CallTicketListItem>());
            }
            finally
            {
                _suppressTicketSelectionChanged = false;
            }

            UpdatePendingPager();
            UpdateSolvedPager();

            if (version != _ticketsRefreshVersion)
                return;

            RestoreTicketSelectionAfterRefresh(preferredTicketId);
        }

        private async Task SafeRefreshTicketGridsAsync()
        {
            try
            {
                await RefreshTicketGridsLatestAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                System.Diagnostics.Trace.TraceError(ex.ToString());

                try
                {
                    ShowFriendlyError(
                        "Call Monitoring",
                        "We couldn't refresh the ticket lists right now. Please try again.",
                        ex,
                        "RefreshTicketGrids");
                }
                catch
                {
                }
            }
        }

        private void BindPendingGrid(System.Collections.Generic.List<CallTicketListItem> tickets)
        {
            SetControlRedraw(this.dgvPendingTickets, false);
            this.dgvPendingTickets.SuspendLayout();
            try
            {
                EnsurePendingColumns();
                this.dgvPendingTickets.Rows.Clear();
                foreach (var t in tickets)
                {
                    var isOverdue = IsTicketOverdue(t);
                    var normalizedStatus = NormalizeLegacyTicketStatus(t.Status);
                    var displayStatus = isOverdue ? "Overdue" : normalizedStatus;

                    var lastActivityUtc = GetLastActivityUtc(t, out var lastActivitySource);
                    var idle = DateTime.UtcNow - lastActivityUtc;
                    if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
                    var idleLabel = FormatShortDuration(idle);

                    var sla = BuildSlaCell(t);

                    var rowIndex = this.dgvPendingTickets.Rows.Add();
                    var row = this.dgvPendingTickets.Rows[rowIndex];
                    row.Tag = t;

                    row.Cells["colId"].Value = t.TicketCode ?? t.TicketId.ToString();
                    row.Cells["colIssue"].Value = t.Issue;
                    row.Cells["colDepartment"].Value = t.Department ?? "-";
                    row.Cells["colBranch"].Value = t.Branch ?? "-";
                    row.Cells["colResponsible"].Value = string.IsNullOrWhiteSpace(t.ResponsiblePerson) ? "Unassigned" : t.ResponsiblePerson;
                    row.Cells["colAge"].Value = t.TicketAgeDays.ToString();
                    row.Cells["colIdle"].Value = idleLabel;
                    row.Cells["colSla"].Value = sla.Label;
                    row.Cells["colLastContact"].Value = t.LastContactAt.HasValue ? ToLocalString(t.LastContactAt.Value, "g") : "-";
                    row.Cells["colStatus"].Value = displayStatus;

                    var idleCell = row.Cells["colIdle"];
                    idleCell.ToolTipText = $"Last activity ({lastActivitySource}): {ToLocalString(lastActivityUtc, "g")}";
                    if (idle >= TimeSpan.FromDays(Math.Max(1, _overdueDays)))
                        idleCell.Style.ForeColor = Color.FromArgb(231, 76, 60);
                    else if (idle >= TimeSpan.FromHours(24))
                        idleCell.Style.ForeColor = Color.FromArgb(230, 126, 34);

                    var slaCell = row.Cells["colSla"];
                    slaCell.ToolTipText = sla.Tooltip;
                    if (sla.State == SlaState.Breached)
                        slaCell.Style.ForeColor = Color.FromArgb(231, 76, 60);
                    else if (sla.State == SlaState.AtRisk)
                        slaCell.Style.ForeColor = Color.FromArgb(230, 126, 34);
                    else if (sla.State == SlaState.Ok)
                        slaCell.Style.ForeColor = Color.FromArgb(46, 204, 113);
                    
                    var statusCell = row.Cells["colStatus"];
                    if (isOverdue) statusCell.Style.ForeColor = Color.FromArgb(231, 76, 60);
                    else if (normalizedStatus == "Escalated") statusCell.Style.ForeColor = Color.FromArgb(230, 126, 34);
                    else if (normalizedStatus == "In Progress") statusCell.Style.ForeColor = Color.FromArgb(155, 89, 182);
                    else if (normalizedStatus == "Pending") statusCell.Style.ForeColor = Color.FromArgb(52, 152, 219);
                    else if (normalizedStatus == "Resolved (Temporary)") statusCell.Style.ForeColor = Color.FromArgb(99, 102, 241);
                    else if (normalizedStatus == "Solved") statusCell.Style.ForeColor = Color.FromArgb(46, 204, 113);
                    else if (normalizedStatus == "Reopened") statusCell.Style.ForeColor = Color.FromArgb(231, 76, 60);
                }
            }
            finally
            {
                ClearGridSelection(this.dgvPendingTickets);
                this.dgvPendingTickets.ResumeLayout();
                SetControlRedraw(this.dgvPendingTickets, true);
            }
        }

        private void BindSolvedGrid(System.Collections.Generic.List<CallTicketListItem> tickets)
        {
            SetControlRedraw(this.dgvSolvedTickets, false);
            this.dgvSolvedTickets.SuspendLayout();
            try
            {
                EnsureSolvedColumns();
                this.dgvSolvedTickets.Rows.Clear();
                foreach (var t in tickets)
                {
                    var normalizedStatus = NormalizeLegacyTicketStatus(t.Status);
                    var rowIndex = this.dgvSolvedTickets.Rows.Add(
                        t.TicketCode ?? t.TicketId.ToString(),
                        t.Issue,
                        t.Department ?? "-",
                        t.Branch ?? "-",
                        normalizedStatus,
                        t.ResponsiblePerson ?? "-",
                        t.SolvedAt.HasValue ? ToLocalString(t.SolvedAt.Value, "g") : ToLocalString(t.UpdatedAt, "g")
                    );
                    
                    var row = this.dgvSolvedTickets.Rows[rowIndex];
                    row.Tag = t;

                    var statusCell = row.Cells["colSolvedStatus"];
                    if (normalizedStatus == "Resolved (Temporary)") statusCell.Style.ForeColor = Color.FromArgb(99, 102, 241);
                    else if (normalizedStatus == "Solved") statusCell.Style.ForeColor = Color.FromArgb(46, 204, 113);
                }
            }
            finally
            {
                ClearGridSelection(this.dgvSolvedTickets);
                this.dgvSolvedTickets.ResumeLayout();
                SetControlRedraw(this.dgvSolvedTickets, true);
            }
        }

        private void EnsurePendingColumns()
        {
            if (this.dgvPendingTickets.Columns.Count == 0)
            {
                this.dgvPendingTickets.Columns.Add("colId", "ID");
                this.dgvPendingTickets.Columns.Add("colIssue", "Issue");
                this.dgvPendingTickets.Columns.Add("colDepartment", "Department");
                this.dgvPendingTickets.Columns.Add("colBranch", "Branch");
                this.dgvPendingTickets.Columns.Add("colResponsible", "Responsible");
                this.dgvPendingTickets.Columns.Add("colAge", "Age (Days)");
                this.dgvPendingTickets.Columns.Add("colIdle", "Idle");
                this.dgvPendingTickets.Columns.Add("colSla", "SLA");
                this.dgvPendingTickets.Columns.Add("colLastContact", "Last Contact");
                this.dgvPendingTickets.Columns.Add("colStatus", "Status");
                return;
            }

            // Backwards-compatible: add new columns if an older layout is already present.
            if (!this.dgvPendingTickets.Columns.Contains("colIdle"))
            {
                var insertAt = this.dgvPendingTickets.Columns.Contains("colAge")
                    ? this.dgvPendingTickets.Columns["colAge"].Index + 1
                    : (this.dgvPendingTickets.Columns.Contains("colLastContact") ? this.dgvPendingTickets.Columns["colLastContact"].Index : this.dgvPendingTickets.Columns.Count);
                this.dgvPendingTickets.Columns.Insert(Math.Min(insertAt, this.dgvPendingTickets.Columns.Count),
                    new DataGridViewTextBoxColumn { Name = "colIdle", HeaderText = "Idle" });
            }
            if (!this.dgvPendingTickets.Columns.Contains("colSla"))
            {
                var insertAt = this.dgvPendingTickets.Columns.Contains("colIdle")
                    ? this.dgvPendingTickets.Columns["colIdle"].Index + 1
                    : (this.dgvPendingTickets.Columns.Contains("colLastContact") ? this.dgvPendingTickets.Columns["colLastContact"].Index : this.dgvPendingTickets.Columns.Count);
                this.dgvPendingTickets.Columns.Insert(Math.Min(insertAt, this.dgvPendingTickets.Columns.Count),
                    new DataGridViewTextBoxColumn { Name = "colSla", HeaderText = "SLA" });
            }
        }

        private void EnsureSolvedColumns()
        {
            if (this.dgvSolvedTickets.Columns.Count > 0) return;
            this.dgvSolvedTickets.Columns.Add("colSolvedId", "ID");
            this.dgvSolvedTickets.Columns.Add("colSolvedIssue", "Issue");
            this.dgvSolvedTickets.Columns.Add("colSolvedDept", "Department");
            this.dgvSolvedTickets.Columns.Add("colSolvedBranch", "Branch");
            this.dgvSolvedTickets.Columns.Add("colSolvedStatus", "Status");
            this.dgvSolvedTickets.Columns.Add("colSolvedBy", "Resolved By");
            this.dgvSolvedTickets.Columns.Add("colSolvedDate", "Resolved On");
        }

        private bool IsTicketOverdue(CallTicketListItem ticket)
        {
            if (ticket == null || IsFinalStatus(ticket.Status)) return false;
            if (_overdueDays <= 0) return false;

            var lastActivityUtc = GetLastActivityUtc(ticket, out _);
            var idle = DateTime.UtcNow - lastActivityUtc;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            return idle >= TimeSpan.FromDays(_overdueDays);
        }

        private static bool IsFinalStatus(string status)
        {
            var s = (status ?? string.Empty).Trim();
            return s.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("Closed", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime GetLastActivityUtc(CallTicketListItem t, out string source)
        {
            if (t == null)
            {
                source = "-";
                return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            }

            if (t.LastContactAt.HasValue)
            {
                source = "LastContactAt";
                return t.LastContactAt.Value.Kind == DateTimeKind.Utc
                    ? t.LastContactAt.Value
                    : DateTime.SpecifyKind(t.LastContactAt.Value, DateTimeKind.Utc);
            }

            source = "UpdatedAt";
            return t.UpdatedAt.Kind == DateTimeKind.Utc
                ? t.UpdatedAt
                : DateTime.SpecifyKind(t.UpdatedAt, DateTimeKind.Utc);
        }

        private enum SlaState
        {
            None = 0,
            Closed = 1,
            Ok = 2,
            AtRisk = 3,
            Breached = 4
        }

        private static (string Label, SlaState State, string Tooltip) BuildSlaCell(CallTicketListItem t)
        {
            var tooltip = BuildSlaLabel(t);
            if (t == null)
                return ("-", SlaState.None, tooltip);

            var target = GetResolutionSlaTarget(t.Priority, t.IssueType);
            if (target.TargetHours <= 0)
                return ("-", SlaState.None, tooltip);

            if (IsFinalStatus(t.Status))
                return ("Closed", SlaState.Closed, tooltip);

            var createdUtc = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc);
            var dueUtc = createdUtc.AddHours(target.TargetHours);
            var remaining = dueUtc - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                var overdue = remaining.Duration();
                return ($"Breached {FormatShortDuration(overdue)}", SlaState.Breached, tooltip);
            }

            var warn = TimeSpan.FromHours(Math.Max(1, target.WarningHours));
            if (remaining <= warn)
                return ($"Due {FormatShortDuration(remaining)}", SlaState.AtRisk, tooltip);

            return ($"Due {FormatShortDuration(remaining)}", SlaState.Ok, tooltip);
        }

        private static int CalculateTotalPages(int totalCount, int pageSize)
        {
            if (pageSize < 1) pageSize = 1;
            if (totalCount < 0) totalCount = 0;
            return Math.Max(1, (totalCount + pageSize - 1) / pageSize);
        }

        private void UpdatePendingPager()
        {
            var totalPages = CalculateTotalPages(_pendingTotalCount, PendingPageSize);
            var pageIndex = Math.Max(1, _pendingPageIndex);
            if (pageIndex > totalPages) pageIndex = totalPages;

            if (this.lblPendingPage != null) this.lblPendingPage.Text = $"Page {pageIndex} of {totalPages} ({Math.Max(0, _pendingTotalCount)})";
            if (this.btnPendingPrev != null) this.btnPendingPrev.Enabled = _pendingPageIndex > 1;
            if (this.btnPendingNext != null) this.btnPendingNext.Enabled = _pendingHasNext;
        }

        private void UpdateSolvedPager()
        {
            var totalPages = CalculateTotalPages(_solvedTotalCount, SolvedPageSize);
            var pageIndex = Math.Max(1, _solvedPageIndex);
            if (pageIndex > totalPages) pageIndex = totalPages;

            if (this.lblSolvedPage != null) this.lblSolvedPage.Text = $"Page {pageIndex} of {totalPages} ({Math.Max(0, _solvedTotalCount)})";
            if (this.btnSolvedPrev != null) this.btnSolvedPrev.Enabled = _solvedPageIndex > 1;
            if (this.btnSolvedNext != null) this.btnSolvedNext.Enabled = _solvedHasNext;
        }

        private void PopulateLookupCombo(ComboBox combo, System.Collections.Generic.IEnumerable<LookupItem> items)
        {
             if (combo == null) return;
             combo.BeginUpdate();
             combo.Items.Clear();
             if (items != null)
             {
                 foreach (var item in items.Where(i => i != null).OrderBy(i => i.Name))
                     combo.Items.Add(item);
             }
             combo.EndUpdate();
             if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void PopulateEmployeeCombo(ComboBox combo, System.Collections.Generic.IEnumerable<LookupItem> employees)
        {
            if (combo == null) return;
            var unique = (employees ?? Enumerable.Empty<LookupItem>()).Where(x => x?.Id > 0).GroupBy(x => x.Id).Select(g => g.First()).OrderBy(x => x.Name).ToList();
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(new LookupItem { Id = 0, Name = "(Unassigned)" });
            foreach (var emp in unique) combo.Items.Add(emp);
            combo.EndUpdate();
            combo.SelectedIndex = 0;
        }

        private void PopulateAssigneeFilterCombo(ComboBox combo, System.Collections.Generic.IEnumerable<LookupItem> employees)
        {
            if (combo == null) return;
            var unique = (employees ?? Enumerable.Empty<LookupItem>()).Where(x => x?.Id > 0).GroupBy(x => x.Id).Select(g => g.First()).OrderBy(x => x.Name).ToList();
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(new LookupItem { Id = -1, Name = "(All)" });
            combo.Items.Add(new LookupItem { Id = 0, Name = "(Unassigned)" });
            foreach (var emp in unique) combo.Items.Add(emp);
            combo.EndUpdate();
            combo.SelectedIndex = 0;
        }

        private static void PopulateBranchCombo(ComboBox combo, IEnumerable<LookupItem> branches, string firstLabel)
        {
            if (combo == null) return;

            var unique = (branches ?? Enumerable.Empty<LookupItem>())
                .Where(x => x?.Id > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();

            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(new LookupItem { Id = 0, Name = firstLabel });
            foreach (var b in unique) combo.Items.Add(b);
            combo.EndUpdate();
            combo.SelectedIndex = 0;
        }

        private static void PopulateDepartmentCombo(ComboBox combo, IEnumerable<LookupItem> departments, string firstLabel)
        {
            if (combo == null) return;

            var unique = (departments ?? Enumerable.Empty<LookupItem>())
                .Where(x => x?.Id > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();

            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(new LookupItem { Id = 0, Name = firstLabel });
            foreach (var d in unique) combo.Items.Add(d);
            combo.EndUpdate();
            combo.SelectedIndex = 0;
        }

        private static void PopulateCallerCombo(ComboBox combo, IEnumerable<LookupItem> employees, string firstLabel)
        {
            if (combo == null) return;

            var unique = (employees ?? Enumerable.Empty<LookupItem>())
                .Where(x => x?.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();

            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(new LookupItem { Id = 0, Name = firstLabel });
            foreach (var e in unique) combo.Items.Add(e);
            combo.EndUpdate();
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private LookupItem TryResolveCallerEmployee()
        {
            if (this.cboCallerName == null)
                return null;

            var selected = this.cboCallerName.SelectedItem as LookupItem;
            if (selected != null && selected.Id > 0)
                return selected;

            var text = (this.cboCallerName.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            foreach (var item in this.cboCallerName.Items.OfType<LookupItem>())
            {
                if (item == null || item.Id <= 0)
                    continue;

                var name = (item.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                {
                    try { this.cboCallerName.SelectedItem = item; } catch { }
                    return item;
                }
            }

            return null;
        }

        private async Task ReloadNewTicketCallerEmployeesAsync(bool preserveSelection = true)
        {
            if (this.cboCallerName == null)
                return;

            try
            {
                var prev = this.cboCallerName.SelectedItem as LookupItem;
                var prevId = (preserveSelection && prev != null && prev.Id > 0) ? prev.Id : 0;

                var comId = (this.cboCompany.SelectedItem as LookupItem)?.Id;
                if (comId.HasValue && comId.Value <= 0) comId = null;

                var dept = this.cboDepartment.SelectedItem as LookupItem;
                var deptId = dept != null && dept.Id > 0 ? (int?)dept.Id : null;

                var branch = this.cboBranch.SelectedItem as LookupItem;
                var branchId = branch != null && branch.Id > 0 ? (int?)branch.Id : null;

                if ((!deptId.HasValue || deptId.Value <= 0) && (!branchId.HasValue || branchId.Value <= 0))
                {
                    PopulateCallerCombo(this.cboCallerName, Enumerable.Empty<LookupItem>(), "(Select Department or Branch)");
                    return;
                }

                var employees = await _callRepo.GetEmployeesByDeptAndBranchAsync(comId, deptId, branchId);
                PopulateCallerCombo(this.cboCallerName, employees, "(Select Employee)");

                if (prevId > 0)
                    SelectLookupItemById(this.cboCallerName, prevId);
            }
            catch
            {
                PopulateCallerCombo(this.cboCallerName, Enumerable.Empty<LookupItem>(), "(Select Employee)");
            }
        }

        private async Task QuickAddCallerEmployeeAsync()
        {
            if (this.btnQuickAddCaller == null || this.btnQuickAddCaller.IsDisposed)
                return;

            if (AppSession.CurrentUserId <= 0)
            {
                MessageBox.Show("Must be logged in.", "Add Employee", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new Yakult.Inventory.App.Pages.Employee.QuickAddEmployeeDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.NewEmployeeId.HasValue)
                    return;

                var newEmpId = dlg.NewEmployeeId.Value;

                CallEmployeeOrgInfo org = null;
                try
                {
                    org = await _callRepo.GetEmployeeOrgInfoByEmpIdAsync(newEmpId);
                }
                catch
                {
                    org = null;
                }

                try
                {
                    _loadingNewTicketLookups = true;

                    if (org?.ComId.HasValue == true && org.ComId.Value > 0)
                        SelectLookupItemById(this.cboCompany, org.ComId.Value);

                    await ReloadNewTicketDepartmentsAsync();
                    if (org?.DeptId.HasValue == true && org.DeptId.Value > 0)
                        SelectLookupItemById(this.cboDepartment, org.DeptId.Value);

                    await ReloadNewTicketBranchesAsync(preserveSelection: false);
                    if (org?.BranchId.HasValue == true && org.BranchId.Value > 0)
                        SelectLookupItemById(this.cboBranch, org.BranchId.Value);

                    await ReloadNewTicketCallerEmployeesAsync(preserveSelection: false);
                    SelectLookupItemById(this.cboCallerName, newEmpId);

                    UpdateNewTicketValidationState();
                }
                finally
                {
                    _loadingNewTicketLookups = false;
                }

                if ((this.cboCallerName.SelectedItem as LookupItem)?.Id != newEmpId)
                {
                    MessageBox.Show(
                        "Employee added.\n\nIf you don't see them in Caller Name, check that Company / Department / Branch match the employee you just created.",
                        "Add Employee",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        private void InitializeEmployeeSearch()
        {
            this._callerSearchDebounceTimer = new System.Windows.Forms.Timer { Interval = 200 };
            this._callerSearchDebounceTimer.Tick += (_, __) =>
            {
                this._callerSearchDebounceTimer.Stop();
                try { SearchCallerEmployeeSync(); } catch { }
            };

            this.cboCallerName.TextChanged += cboCallerName_TextChanged;
            this.cboCallerName.SelectedIndexChanged += cboCallerName_SelectedIndexChanged;
        }

        private void cboCallerName_TextChanged(object sender, EventArgs e)
        {
            if (this._callerSearchDebounceTimer == null)
                return;

            var text = this.cboCallerName.Text ?? string.Empty;
            if (text.Length < 2)
            {
                try { this._callerSearchDebounceTimer.Stop(); } catch { }
                if (_newTicketOrgLockedFromEmployeeSearch)
                    UnlockNewTicketOrgFromEmployeeSearch();
                return;
            }

            this._callerSearchDebounceTimer.Stop();
            this._callerSearchDebounceTimer.Start();
        }

        private void cboCallerName_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingNewTicketLookups)
                return;

            var selected = this.cboCallerName.SelectedItem as LookupItem;
            if (selected == null || selected.Id <= 0)
                return;

            _ = HandleCallerEmployeeSelectedAsync(selected.Id);
        }

        private void SearchCallerEmployeeSync()
        {
            if (this.cboCallerName == null || this.cboCallerName.IsDisposed)
                return;

            var text = this.cboCallerName.Text?.Trim() ?? string.Empty;
            if (text.Length < 2)
                return;

            try
            {
                var employees = _callRepo.SearchEmployeesByNameAsync(text).GetAwaiter().GetResult();

                if (this.cboCallerName.IsDisposed)
                    return;

                if (this.cboCallerName.InvokeRequired)
                {
                    this.cboCallerName.Invoke(new Action(() => PopulateCallerSearchResults(employees)));
                }
                else
                {
                    PopulateCallerSearchResults(employees);
                }
            }
            catch
            {
            }
        }

        private void PopulateCallerSearchResults(List<LookupItem> employees)
        {
            if (this.cboCallerName == null || this.cboCallerName.IsDisposed)
                return;

            this.cboCallerName.BeginUpdate();
            try
            {
                this.cboCallerName.Items.Clear();
                this.cboCallerName.Items.Add(new LookupItem { Id = 0, Name = "(Type to search)" });
                foreach (var emp in employees)
                    this.cboCallerName.Items.Add(emp);

                if (this.cboCallerName.Items.Count > 1)
                {
                    this.cboCallerName.SelectedIndex = 0;
                    this.cboCallerName.DroppedDown = true;
                }
            }
            finally
            {
                this.cboCallerName.EndUpdate();
            }
        }

        private async Task HandleCallerEmployeeSelectedAsync(int empId)
        {
            if (empId <= 0)
                return;

            try
            {
                _loadingNewTicketLookups = true;

                var org = await _callRepo.GetEmployeeOrgInfoByEmpIdAsync(empId);

                if (org?.ComId.HasValue == true && org.ComId.Value > 0)
                    SelectLookupItemById(this.cboCompany, org.ComId.Value);

                await ReloadNewTicketDepartmentsAsync();
                if (org?.DeptId.HasValue == true && org.DeptId.Value > 0)
                    SelectLookupItemById(this.cboDepartment, org.DeptId.Value);

                await ReloadNewTicketBranchesAsync(preserveSelection: false);
                if (org?.BranchId.HasValue == true && org.BranchId.Value > 0)
                    SelectLookupItemById(this.cboBranch, org.BranchId.Value);

                _newTicketOrgLockedFromEmployeeSearch = true;
                SetNewTicketOrgDropdownsEnabled(false);

                SelectLookupItemById(this.cboCallerName, empId);

                UpdateNewTicketValidationState();
            }
            catch { }
            finally
            {
                _loadingNewTicketLookups = false;
            }
        }

        private void UnlockNewTicketOrgFromEmployeeSearch()
        {
            if (!_newTicketOrgLockedFromEmployeeSearch)
                return;

            _newTicketOrgLockedFromEmployeeSearch = false;
            SetNewTicketOrgDropdownsEnabled(true);

            if (this.cboCallerName != null && !this.cboCallerName.IsDisposed)
            {
                this.cboCallerName.Items.Clear();
                this.cboCallerName.Text = string.Empty;
                this.cboCallerName.SelectedIndex = -1;
            }

            UpdateNewTicketValidationState();
        }

        private void SetNewTicketOrgDropdownsEnabled(bool enabled)
        {
            if (this.cboCompany != null && !this.cboCompany.IsDisposed)
                this.cboCompany.Enabled = enabled;
            if (this.cboDepartment != null && !this.cboDepartment.IsDisposed)
                this.cboDepartment.Enabled = enabled;
            if (this.cboBranch != null && !this.cboBranch.IsDisposed)
                this.cboBranch.Enabled = enabled;
        }

        private async Task ReloadNewTicketBranchesAsync(bool preserveSelection = true)
        {
            if (this.cboBranch == null)
                return;

            try
            {
                var prev = this.cboBranch.SelectedItem as LookupItem;
                var prevId = (preserveSelection && prev != null && prev.Id > 0) ? prev.Id : 0;

                var comId = (this.cboCompany.SelectedItem as LookupItem)?.Id;
                if (comId.HasValue && comId.Value <= 0) comId = null;

                // IMPORTANT: Do not filter branches by department. A branch may have multiple departments.
                var branches = await _callRepo.GetBranchesAsync(comId, null);
                PopulateBranchCombo(this.cboBranch, branches, "(None)");

                if (prevId > 0)
                    SelectLookupItemById(this.cboBranch, prevId);
            }
            catch
            {
                PopulateBranchCombo(this.cboBranch, Enumerable.Empty<LookupItem>(), "(None)");
            }
        }

        private async Task ReloadNewTicketDepartmentsAsync()
        {
            if (this.cboDepartment == null)
                return;

            try
            {
                var prev = this.cboDepartment.SelectedItem as LookupItem;
                var prevId = prev != null && prev.Id > 0 ? prev.Id : 0;

                var comId = (this.cboCompany.SelectedItem as LookupItem)?.Id;
                if (comId.HasValue && comId.Value <= 0) comId = null;

                var departments = await _callRepo.GetDepartmentsAsync(comId);
                PopulateDepartmentCombo(this.cboDepartment, departments, "N/A");

                if (prevId > 0)
                    SelectLookupItemById(this.cboDepartment, prevId);
            }
            catch
            {
                // Keep whatever is currently loaded; avoid blocking ticket creation.
            }
        }

        private async Task ReloadBranchFilterAsync()
        {
            if (this.cboBranchFilter == null)
                return;

            try
            {
                var prev = this.cboBranchFilter.SelectedItem as LookupItem;
                var prevId = prev != null && prev.Id > 0 ? prev.Id : 0;

                var branches = await _callRepo.GetBranchesAsync(null, null);
                _suppressBranchFilterRefresh = true;
                try
                {
                    PopulateBranchCombo(this.cboBranchFilter, branches, "(All Branches)");

                    if (prevId > 0)
                        SelectLookupItemById(this.cboBranchFilter, prevId);
                }
                finally
                {
                    _suppressBranchFilterRefresh = false;
                }
            }
            catch
            {
                _suppressBranchFilterRefresh = true;
                try
                {
                    PopulateBranchCombo(this.cboBranchFilter, Enumerable.Empty<LookupItem>(), "(All Branches)");
                }
                finally
                {
                    _suppressBranchFilterRefresh = false;
                }
            }
        }

        private static void ClearGridSelection(DataGridView grid)
        {
            if (grid == null)
                return;

            try
            {
                grid.ClearSelection();
                grid.CurrentCell = null;
            }
            catch
            {
            }
        }

        private static void SetControlRedraw(Control control, bool enabled)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated)
                return;

            try
            {
                SendMessage(control.Handle, WM_SETREDRAW, enabled ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero);
                if (enabled)
                {
                    control.Invalidate(true);
                    control.Update();
                }
            }
            catch
            {
            }
        }

        private void RequestSelectedTicketDetailsDebounced(int ticketId)
        {
            if (ticketId <= 0 || this.IsDisposed)
                return;

            _pendingSelectedTicketId = ticketId;

            try
            {
                _ticketSelectionDebounce.Stop();
                _ticketSelectionDebounce.Start();
            }
            catch
            {
                _ = LoadSelectedTicketDetailsAsync(ticketId, ++_selectionVersion);
            }
        }

        private bool TrySelectFirstRow(DataGridView grid)
        {
            if (grid == null || grid.Rows.Count == 0)
                return false;

            SelectGridRow(grid, grid.Rows[0]);
            return true;
        }

        private void SelectGridRow(DataGridView grid, DataGridViewRow row)
        {
            if (grid == null || row == null || row.Index < 0)
                return;

            try
            {
                grid.ClearSelection();
                row.Selected = true;
                if (row.Cells.Count > 0)
                    grid.CurrentCell = row.Cells[0];
            }
            catch
            {
            }
        }

        private void RestoreTicketSelectionAfterRefresh(int? preferredTicketId)
        {
            if (preferredTicketId.HasValue && preferredTicketId.Value > 0)
            {
                if (TrySelectTicketInPendingGrid(preferredTicketId.Value))
                    return;

                if (TrySelectTicketInSolvedGrid(preferredTicketId.Value))
                    return;
            }

            if (TrySelectFirstRow(this.dgvPendingTickets))
                return;

            if (TrySelectFirstRow(this.dgvSolvedTickets))
                return;

            ClearSelectedTicketDetails();
        }

        private void ClearSelectedTicketDetails()
        {
            try { _ticketSelectionDebounce.Stop(); } catch { }
            _pendingSelectedTicketId = 0;
            Interlocked.Increment(ref _selectionVersion);
            this._selectedTicket = null;
            this._notesCacheSelected = new List<CallTicketNoteItem>();
            this._historyCacheSelected = new List<CallTicketHistoryItem>();

            if (this.txtCaseProblem != null) this.txtCaseProblem.Clear();
            if (this.txtAttemptedSolutions != null) this.txtAttemptedSolutions.Clear();
            if (this.lblSummaryId != null) this.lblSummaryId.Text = "-";
            if (this.lblSummaryAge != null) this.lblSummaryAge.Text = "-";
            if (this.lblSummarySla != null) this.lblSummarySla.Text = "-";
            if (this.lblSummaryEscalation != null) this.lblSummaryEscalation.Text = "-";

            UpdateTicketProfile(null);
            RenderSolutionNotes();
            ApplyHistoryFilterAndRender();
            UpdateTicketActionStates();
        }

        private void AutoSelectFirstPending()
        {
            if (this.dgvPendingTickets.Rows.Count > 0)
            {
                this.dgvPendingTickets.Rows[0].Selected = true;
                // Will trigger SelectionChanged
            }
        }

        private bool TrySelectTicketInPendingGrid(int ticketId)
        {
            foreach (DataGridViewRow row in this.dgvPendingTickets.Rows)
            {
                if (row.Tag is CallTicketListItem t && t.TicketId == ticketId)
                {
                    SelectGridRow(this.dgvPendingTickets, row);
                    return true;
                }
            }

            return false;
        }
        
        private bool TrySelectTicketInSolvedGrid(int ticketId)
        {
            foreach (DataGridViewRow row in this.dgvSolvedTickets.Rows)
            {
               if (row.Tag is CallTicketListItem t && t.TicketId == ticketId)
               {
                   SelectGridRow(this.dgvSolvedTickets, row);
                   return true;
               }
            }

            return false;
        }
        
        private static void SelectLookupItemById(ComboBox combo, int id)
        {
            if (combo == null) return;
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is LookupItem item && item.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static string GuessItDepartmentName(IEnumerable<LookupItem> departments)
        {
            if (departments == null)
                return null;

            LookupItem best = null;
            var bestScore = -1;

            foreach (var d in departments.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                var name = (d.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                var upper = name.ToUpperInvariant();

                var score = 0;
                if (upper == "IT") score = 100;
                else if (upper == "I.T.") score = 95;
                else if (upper == "IT DEPARTMENT") score = 90;
                else if (upper.Contains("INFORMATION") && upper.Contains("TECH")) score = 85;
                else if (upper.StartsWith("IT")) score = 80;
                else if (upper.Contains(" I.T.")) score = 70;
                else if (upper.Contains(" IT ")) score = 60;
                else if (upper.EndsWith(" IT")) score = 55;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }

            return bestScore > 0 ? (best?.Name ?? string.Empty).Trim() : null;
        }

        // --- EVENT HANDLERS ---

        private async void btnCreateTicket_Click(object sender, EventArgs e)
        {
            try
            {
                if (!await _callRepo.CallSchemaExistsAsync()) return;
                if (!ValidateNewTicketInputs(showErrors: true))
                {
                    this._newTicketShowValidationHints = true;
                    MessageBox.Show(
                        "Company, a Department or Branch, Caller Name, and Technical Problem are required.",
                        "Validation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
                var comId = (this.cboCompany.SelectedItem as LookupItem)?.Id;
                var deptId = (this.cboDepartment.SelectedItem as LookupItem)?.Id;
                var branchId = (this.cboBranch.SelectedItem as LookupItem)?.Id;
                int? assignedToEmpId = (this.cboAssignedTo.SelectedItem as LookupItem)?.Id;

                // UI placeholder ids use 0; stored procedures expect NULL when not applicable.
                if (comId.HasValue && comId.Value <= 0) comId = null;
                if (deptId.HasValue && deptId.Value <= 0) deptId = null;
                if (branchId.HasValue && branchId.Value <= 0) branchId = null;
                if (assignedToEmpId.HasValue && assignedToEmpId.Value <= 0) assignedToEmpId = null;

                var callerItem = TryResolveCallerEmployee();
                var caller = (callerItem?.Name ?? string.Empty).Trim();
                var issue = (this.txtTechnicalProblem.Text ?? string.Empty).Trim();
                var solution = (this.txtProvidedSolution.Text ?? string.Empty).Trim();

                if (!await _callRepo.CanStoreTicketContactEmailAsync())
                {
                    MessageBox.Show(
                        "Ticket contact email storage is not installed. Apply the ContactEmail migration before creating tickets.",
                        "Ticket Contact Storage Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var ticketContact = await _callRepo.ResolveTicketContactEmailAsync(
                    callerItem?.Id,
                    comId,
                    deptId,
                    branchId);
                var requiresCallerEmail = callerItem != null
                    && !string.Equals(ticketContact?.Source, "Employee personal email", StringComparison.Ordinal);

                var createdByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;

                // --- Resolve backdate (if enabled) ---
                DateTime? backlogUtc = null;
                string backdateDisplay = null;
                if (this.chkBackdate.Checked)
                {
                    var dateLocal = this.dtpBackdateDate.Value.Date;
                    var timeLocal = this.dtpBackdateTime.Value.TimeOfDay;
                    var local = dateLocal + timeLocal;
                    if (local > DateTime.Now.AddMinutes(1))
                    {
                        MessageBox.Show("Backdate cannot be in the future.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    backlogUtc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
                    backdateDisplay = local.ToString("yyyy-MM-dd HH:mm");
                }
                // ---

                var companyText = (this.cboCompany?.SelectedItem as LookupItem)?.Name ?? this.cboCompany?.Text ?? "-";
                var departmentText = (this.cboDepartment?.SelectedItem as LookupItem)?.Name ?? this.cboDepartment?.Text ?? "-";
                var branchText = (this.cboBranch?.SelectedItem as LookupItem)?.Name ?? this.cboBranch?.Text ?? "-";
                var assignedText = (this.cboAssignedTo?.SelectedItem as LookupItem)?.Name ?? this.cboAssignedTo?.Text ?? "(Unassigned)";
                var notesText = string.IsNullOrWhiteSpace(solution) ? "(None)" : solution;
                var escalationText = GetNewTicketEscalationSummaryForConfirm();
                var escalationReason = TryGetNewTicketCustomEscalation(out _, out _, out var reason) ? reason : null;
                var suggestedPriority = ConfirmTicketCreationDialog.SuggestPriority(issue, out var priorityReason);
                var recentWarning = await BuildRecentOpenTicketWarningAsync(caller);
                ConfirmTicketCreationDialog.CreationAction createAction;
                string selectedPriority;
                string selectedTicketContactEmail;
                bool shouldLinkTicketContactEmailToCaller;

                using (var dlg = new ConfirmTicketCreationDialog(new ConfirmTicketCreationDialog.TicketCreationModel
                {
                    Company = companyText,
                    Department = departmentText,
                    Branch = branchText,
                    Caller = caller,
                    AssignedTo = assignedText,
                    TicketContactEmail = ticketContact?.Email,
                    TicketContactEmailSource = ticketContact?.Source,
                    RequiresCallerEmail = requiresCallerEmail,
                    CanLinkTicketContactEmailToCaller = requiresCallerEmail && callerItem != null,
                    RequiresTemporaryTicketContactEmail = !requiresCallerEmail && (ticketContact == null || !ticketContact.HasEmail),
                    Priority = "Medium",
                    SuggestedPriority = suggestedPriority,
                    PrioritySuggestionReason = priorityReason,
                    RecentOpenTicketWarning = recentWarning,
                    EscalationSummary = escalationText,
                    EscalationReason = escalationReason,
                    Issue = issue,
                    InitialNotes = notesText,
                    BackdateDisplay = backdateDisplay
                }))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;
                    createAction = dlg.SelectedAction;
                    selectedPriority = dlg.SelectedPriority;
                    selectedTicketContactEmail = dlg.SelectedTicketContactEmail;
                    shouldLinkTicketContactEmailToCaller = dlg.ShouldLinkTicketContactEmailToCaller;
                }

                var created = await _callRepo.CreateTicketAsync(comId, deptId, branchId, caller, issue, string.IsNullOrWhiteSpace(solution) ? null : solution, null, selectedPriority, assignedToEmpId, createdByUserId, backlogUtc);
                if (created == null || created.TicketId <= 0)
                    throw new InvalidOperationException("Ticket creation did not return a valid ticket.");

                try
                {
                    await _callRepo.SetTicketContactEmailAsync(created.TicketId, selectedTicketContactEmail);
                }
                catch (Exception ex)
                {
                    var ticketLabel = string.IsNullOrWhiteSpace(created.TicketCode)
                        ? $"#{created.TicketId}"
                        : created.TicketCode.Trim();
                    throw new InvalidOperationException(
                        $"Ticket {ticketLabel} was created, but its required ticket contact email could not be saved. No lifecycle notification was sent.",
                        ex);
                }

                if (shouldLinkTicketContactEmailToCaller)
                {
                    if (callerItem == null || callerItem.Id <= 0)
                        throw new InvalidOperationException("The ticket contact was saved, but no caller employee profile was selected to link the email.");

                    try
                    {
                        await _callRepo.SaveEmployeeEmailBindingAsync(
                            callerItem.Id,
                            selectedTicketContactEmail,
                            createdByUserId);
                    }
                    catch (Exception ex)
                    {
                        var ticketLabel = string.IsNullOrWhiteSpace(created.TicketCode)
                            ? $"#{created.TicketId}"
                            : created.TicketCode.Trim();
                        throw new InvalidOperationException(
                            $"Ticket {ticketLabel} was created and its contact email was saved, but the caller's employee email could not be linked. No lifecycle notification was sent.",
                            ex);
                    }
                }

                if (created != null && created.TicketId > 0
                    && TryGetNewTicketCustomEscalation(out var customSup, out var customMgr, out var customReason))
                {
                    try
                    {
                        if (!await _callRepo.EscalationOverridesEnabledAsync())
                        {
                            MessageBox.Show(
                                "Ticket created, but escalation overrides are not installed in this database yet.\n\nRun the CallTicketEscalationOverride DB script to enable per-ticket escalation timing.",
                                "Set Escalation",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            await _callRepo.SetTicketEscalationOverrideAsync(
                                created.TicketId,
                                (int?)customSup,
                                (int?)customMgr,
                                customReason,
                                createdByUserId,
                                clear: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Ticket created, but failed to set escalation timing.\n\n" + ex.Message,
                            "Set Escalation Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                if (created != null && created.TicketId > 0)
                {
                    // Suppress "new ticket" email for backlog entries; keep assignment emails.
                    if (!backlogUtc.HasValue)
                    {
                        RunNotification(async () => await _callEmail.NotifyNewTicketAsync(created.TicketId, createdByUserId));
                    }
                }

                ResetTicketPaging();
                await SafeRefreshTicketGridsAsync();
                if (created != null && created.TicketId > 0 && createAction == ConfirmTicketCreationDialog.CreationAction.CreateAndOpen)
                {
                    TrySelectTicketInPendingGrid(created.TicketId);
                    OpenTicketDetailsDialog(created.TicketId);
                }
                if (this.cboCallerName != null && this.cboCallerName.Items.Count > 0) this.cboCallerName.SelectedIndex = 0;
                this.txtTechnicalProblem.Clear();
                this.txtProvidedSolution.Clear();
                ClearNewTicketEscalation();
                this._newTicketShowValidationHints = false;
                ClearNewTicketErrors();
                UpdateNewTicketValidationState();

                if (created != null && created.TicketId > 0 && createAction != ConfirmTicketCreationDialog.CreationAction.CreateAndOpen)
                {
                    var ticketLabel = !string.IsNullOrWhiteSpace(created.TicketCode) ? created.TicketCode.Trim() : $"Ticket #{created.TicketId}";
                    var assignedLabel = string.IsNullOrWhiteSpace(created.ResponsiblePerson) ? "(Unassigned)" : created.ResponsiblePerson.Trim();
                    MessageBox.Show(
                        "Ticket created successfully.\n\n" +
                        $"Ticket:      {ticketLabel}\n" +
                        $"Assigned To: {assignedLabel}\n\n" +
                        (createAction == ConfirmTicketCreationDialog.CreationAction.CreateAnother
                            ? "The form is ready for another ticket."
                            : "It should now appear in the Pending Tickets list."),
                        "Ticket Created",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Create Ticket Failed", "We couldn't create the ticket right now. Please try again.", ex, "CreateTicket");
            }
        }

        private async Task<string> BuildRecentOpenTicketWarningAsync(string caller)
        {
            if (string.IsNullOrWhiteSpace(caller))
                return null;

            try
            {
                var trimmedCaller = caller.Trim();
                var rows = await _callRepo.GetPendingTicketsPageAsync(
                    "All",
                    trimmedCaller,
                    null,
                    null,
                    null,
                    false,
                    _overdueDays,
                    1,
                    5);

                rows = (rows ?? new List<CallTicketListItem>())
                    .Where(t => t != null
                        && !string.IsNullOrWhiteSpace(t.CallerName)
                        && t.CallerName.IndexOf(trimmedCaller, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(3)
                    .ToList();

                if (rows.Count == 0)
                    return null;

                var sb = new StringBuilder();
                sb.AppendLine($"Caller has {rows.Count} recent/open matching ticket(s). Review before creating a duplicate:");
                foreach (var t in rows)
                    sb.AppendLine("- " + (t.TicketCode ?? t.TicketId.ToString()) + ": " + (t.Issue ?? "-"));
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void btnClearForm_Click(object sender, EventArgs e)
        {
            if (_newTicketOrgLockedFromEmployeeSearch)
                UnlockNewTicketOrgFromEmployeeSearch();

            if (this.cboCallerName != null && this.cboCallerName.Items.Count > 0) this.cboCallerName.SelectedIndex = 0;
            this.txtTechnicalProblem.Clear();
            this.txtProvidedSolution.Clear();
            ClearNewTicketEscalation();
            this._newTicketShowValidationHints = false;
            ClearNewTicketErrors();
            UpdateNewTicketValidationState();

            // Reset backdate controls
            this.chkBackdate.Checked = false;
            this.dtpBackdateDate.Value = DateTime.Today;
            this.dtpBackdateTime.Value = DateTime.Today;
            this.dtpBackdateDate.Enabled = false;
            this.dtpBackdateTime.Enabled = false;
        }

        private async void btnSetNewTicketEscalation_Click(object sender, EventArgs e)
        {
            try
            {
                if (!await _callRepo.CallSchemaExistsAsync())
                    return;

                if (!await _callRepo.EscalationOverridesEnabledAsync())
                {
                    MessageBox.Show(
                        "Escalation overrides are not installed in this database yet.\n\nRun the CallTicketEscalationOverride DB script to enable per-ticket escalation timing.",
                        "Set Escalation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var settings = _escalationSettings ?? await _callRepo.GetEscalationSettingsAsync();

                CallTicketEscalationOverrideItem existing = null;
                if (_newTicketEscDaysToSupervisor.HasValue && _newTicketEscDaysToManager.HasValue)
                {
                    existing = new CallTicketEscalationOverrideItem
                    {
                        TicketId = 0,
                        DaysToSupervisor = _newTicketEscDaysToSupervisor.Value,
                        DaysToManager = _newTicketEscDaysToManager.Value,
                        Reason = _newTicketEscReason ?? string.Empty
                    };
                }

                using (var dlg = new EscalationOverrideForm(settings, existing, allowClear: false, headerText: "Set Escalation", windowTitle: "Set Escalation"))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    _newTicketEscDaysToSupervisor = dlg.DaysToSupervisor;
                    _newTicketEscDaysToManager = dlg.DaysToManager;
                    _newTicketEscReason = dlg.Reason;

                    if (!TryGetNewTicketCustomEscalation(out _, out _, out _))
                    {
                        // If user selected the system default values, treat it as "no custom escalation".
                        ClearNewTicketEscalation();
                        return;
                    }

                    UpdateNewTicketEscalationUi();
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Set Escalation Failed", "We couldn't save the escalation settings right now. Please try again.", ex, "SetNewTicketEscalation");
            }
        }

        private void ClearNewTicketEscalation()
        {
            _newTicketEscDaysToSupervisor = null;
            _newTicketEscDaysToManager = null;
            _newTicketEscReason = null;
            UpdateNewTicketEscalationUi();
        }

        private void UpdateNewTicketEscalationUi()
        {
            if (this.lblNewTicketEscalationSummary == null || this.btnSetNewTicketEscalation == null || this.lnkClearNewTicketEscalation == null)
                return;

            var defSup = Math.Max(1, _escalationSettings?.DaysToSupervisor ?? 2);
            var defMgr = Math.Max(defSup, _escalationSettings?.DaysToManager ?? 3);

            var hasCustom = _newTicketEscDaysToSupervisor.HasValue && _newTicketEscDaysToManager.HasValue;
            if (hasCustom)
            {
                this.lblNewTicketEscalationSummary.Text = $"Custom: Supervisor {_newTicketEscDaysToSupervisor.Value} day(s), Manager {_newTicketEscDaysToManager.Value} day(s)";
                this.btnSetNewTicketEscalation.Text = "Edit escalation";
                this.lnkClearNewTicketEscalation.Visible = true;
            }
            else
            {
                this.lblNewTicketEscalationSummary.Text = $"Default: Supervisor {defSup} day(s), Manager {defMgr} day(s)";
                this.btnSetNewTicketEscalation.Text = "Set escalation";
                this.lnkClearNewTicketEscalation.Visible = false;
            }

            try
            {
                var tip = hasCustom
                    ? $"Will create this ticket with custom escalation timing.\n\nReason: {_newTicketEscReason ?? string.Empty}"
                    : "Uses system default escalation timing.";
                EnsureToolTip().SetToolTip(this.lblNewTicketEscalationSummary, tip);
                EnsureToolTip().SetToolTip(this.btnSetNewTicketEscalation, "Configure escalation timing for the new ticket.");
            }
            catch
            {
            }
        }

        private string GetNewTicketEscalationSummaryForConfirm()
        {
            var defSup = Math.Max(1, _escalationSettings?.DaysToSupervisor ?? 2);
            var defMgr = Math.Max(defSup, _escalationSettings?.DaysToManager ?? 3);

            if (TryGetNewTicketCustomEscalation(out var sup, out var mgr, out _))
                return $"Custom (Sup {sup}d, Mgr {mgr}d)";

            return $"Default (Sup {defSup}d, Mgr {defMgr}d)";
        }

        private bool TryGetNewTicketCustomEscalation(out int supDays, out int mgrDays, out string reason)
        {
            supDays = 0;
            mgrDays = 0;
            reason = null;

            if (!_newTicketEscDaysToSupervisor.HasValue || !_newTicketEscDaysToManager.HasValue)
                return false;

            var defSup = Math.Max(1, _escalationSettings?.DaysToSupervisor ?? 2);
            var defMgr = Math.Max(defSup, _escalationSettings?.DaysToManager ?? 3);

            var sup = _newTicketEscDaysToSupervisor.Value;
            var mgr = _newTicketEscDaysToManager.Value;
            if (sup == defSup && mgr == defMgr)
                return false;

            supDays = sup;
            mgrDays = mgr;
            reason = _newTicketEscReason;
            return true;
        }

        private void UpdateNewTicketValidationState()
        {
            if (this._suppressNewTicketValidation)
                return;

            var ok = ValidateNewTicketInputs(showErrors: this._newTicketShowValidationHints);
            if (this.btnCreateTicket != null)
                this.btnCreateTicket.Enabled = ok;
        }

        private bool ValidateNewTicketInputs(bool showErrors)
        {
            if (this._newTicketErrorProvider == null)
                return true;

            var ok = true;
            var companyId = (this.cboCompany?.SelectedItem as LookupItem)?.Id ?? 0;
            var deptId = (this.cboDepartment?.SelectedItem as LookupItem)?.Id ?? 0;
            var branchId = (this.cboBranch?.SelectedItem as LookupItem)?.Id ?? 0;
            var hasOrganizationScope = deptId > 0 || branchId > 0;

            if (showErrors)
            {
                ok &= SetNewTicketError(this.cboCompany, companyId <= 0 ? "Company is required." : null);
                var organizationError = hasOrganizationScope ? null : "Select a department or branch.";
                ok &= SetNewTicketError(this.cboDepartment, organizationError);
                ok &= SetNewTicketError(this.cboBranch, organizationError);
            }

            var callerItem = TryResolveCallerEmployee();
            if (showErrors)
                ok &= SetNewTicketError(this.cboCallerName, callerItem == null ? "Caller Name is required." : null);

            var issue = (this.txtTechnicalProblem?.Text ?? string.Empty).Trim();
            if (showErrors)
                ok &= SetNewTicketError(this.txtTechnicalProblem, string.IsNullOrWhiteSpace(issue) ? "Technical Problem is required." : null);

            if (companyId <= 0) ok = false;
            if (!hasOrganizationScope) ok = false;
            if (callerItem == null) ok = false;
            if (string.IsNullOrWhiteSpace(issue)) ok = false;

            return ok;
        }

        private bool SetNewTicketError(Control control, string message)
        {
            if (this._newTicketErrorProvider == null || control == null)
                return string.IsNullOrWhiteSpace(message);

            try
            {
                this._newTicketErrorProvider.SetError(control, message ?? string.Empty);
            }
            catch
            {
            }

            return string.IsNullOrWhiteSpace(message);
        }

        private void ClearNewTicketErrors()
        {
            try { this._newTicketErrorProvider.SetError(this.cboCompany, string.Empty); } catch { }
            try { this._newTicketErrorProvider.SetError(this.cboDepartment, string.Empty); } catch { }
            try { this._newTicketErrorProvider.SetError(this.cboBranch, string.Empty); } catch { }
            try { this._newTicketErrorProvider.SetError(this.cboCallerName, string.Empty); } catch { }
            try { this._newTicketErrorProvider.SetError(this.txtTechnicalProblem, string.Empty); } catch { }
        }

        private async void btnAssignToMe_Click(object sender, EventArgs e)
        {
            try
            {
                if (!await _callRepo.EmployeeAssignmentEnabledAsync())
                {
                    MessageBox.Show("Employee assignment is not enabled in this database.", "Assign to me", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!Yakult.Inventory.App.Session.AppSession.IsLoggedIn)
                {
                    MessageBox.Show("Must be logged in.", "Assign to me", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var me = FindBestEmployeeMatchForCurrentUser();
                if (me == null || me.Id <= 0)
                {
                    MessageBox.Show(
                        "Your account is not linked to an IT employee in the 'Assigned To' list.\n\n" +
                        "Please select your name manually in the dropdown and click Reassign.",
                        "Assign to me",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                SelectLookupItemById(this.cboReassignTo, me.Id);
                btnReassignTicket_Click(sender, e);
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Assign to me", "We couldn't assign the ticket to your account right now. Please try again.", ex, "AssignToMe");
            }
        }

        private LookupItem FindBestEmployeeMatchForCurrentUser()
        {
            if (this.cboReassignTo == null)
                return null;

            // Prefer explicit employee id linkage (most reliable).
            if (AppSession.CurrentEmployeeId.HasValue && AppSession.CurrentEmployeeId.Value > 0)
            {
                var empId = AppSession.CurrentEmployeeId.Value;
                foreach (var obj in this.cboReassignTo.Items)
                {
                    if (obj is LookupItem item && item.Id == empId)
                        return item;
                }
            }

            var currentName = (AppSession.CurrentEmployeeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(currentName))
                currentName = (Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(currentName))
                return null;

            LookupItem best = null;
            var normalizedCurrent = NormalizeName(currentName);

            foreach (var obj in this.cboReassignTo.Items)
            {
                var item = obj as LookupItem;
                if (item == null || item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name))
                    continue;

                var normalizedItem = NormalizeName(item.Name);
                if (normalizedItem == normalizedCurrent)
                    return item;

                if (best == null && (!string.IsNullOrWhiteSpace(normalizedItem) && normalizedItem.Contains(normalizedCurrent)))
                    best = item;
            }

            if (best != null)
                return best;

            // Fallback: loose contains either direction
            var upperCurrent = currentName.ToUpperInvariant();
            foreach (var obj in this.cboReassignTo.Items)
            {
                var item = obj as LookupItem;
                if (item == null || item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name))
                    continue;

                var upperItem = item.Name.ToUpperInvariant();
                if (upperItem.Contains(upperCurrent) || upperCurrent.Contains(upperItem))
                    return item;
            }

            return null;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var s = value.Trim().ToUpperInvariant();
            while (s.Contains("  "))
                s = s.Replace("  ", " ");
            return s;
        }

        private static int? NormalizeEmployeeId(int? empId)
        {
            if (!empId.HasValue || empId.Value <= 0)
                return null;
            return empId.Value;
        }

        private static string GetTicketLabel(CallTicketListItem t)
        {
            if (t == null)
                return "this ticket";

            if (!string.IsNullOrWhiteSpace(t.TicketCode))
                return t.TicketCode.Trim();

            return t.TicketId > 0 ? $"Ticket #{t.TicketId}" : "this ticket";
        }

        private static string GetAssigneeLabel(CallTicketListItem t)
        {
            var name = (t?.ResponsiblePerson ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(name) ? "(Unassigned)" : name;
        }

        private async void btnReassignTicket_Click(object sender, EventArgs e)
        {
              try
              {
                  if (!await _callRepo.EmployeeAssignmentEnabledAsync()) { MessageBox.Show("Employee assignment not enabled.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                  
                  var emp = this.cboReassignTo?.SelectedItem as LookupItem;
                  var assignedToEmpId = emp != null && emp.Id > 0 ? (int?)emp.Id : null;
                  assignedToEmpId = NormalizeEmployeeId(assignedToEmpId);

                  var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                  if (!AppSession.IsLoggedIn || changedByUserId == null)
                  {
                      MessageBox.Show("Must be logged in.", "Reassign", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                      return;
                  }

                  var selected = GetSelectedPendingTickets();
                  if (selected.Count == 0)
                  {
                      if (this._selectedTicket == null) return;
                      selected.Add(this._selectedTicket);
                  }

                  var isAssignToMe = ReferenceEquals(sender, this.btnAssignToMe);
                  var who = emp != null ? emp.Name : "(Unassigned)";

                  // Skip tickets that are already in the desired assignment state.
                  var toChange = selected
                      .Where(t => t != null)
                      .Where(t => NormalizeEmployeeId(t.AssignedToEmpId) != assignedToEmpId)
                      .ToList();

                  if (toChange.Count == 0)
                  {
                      var title = isAssignToMe ? "Assign to me" : "Reassign";
                      if (selected.Count == 1)
                      {
                          var currentWho = GetAssigneeLabel(selected[0]);
                          MessageBox.Show($"{GetTicketLabel(selected[0])} is already assigned to '{currentWho}'.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                      }
                      else
                      {
                          MessageBox.Show($"No changes were made.\n\nAll {selected.Count} selected tickets are already assigned to '{who}'.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                      }
                      return;
                  }

                  var alreadyOk = selected.Count - toChange.Count;
                  var verb = isAssignToMe ? "Assign" : "Reassign";
                  var titleConfirm = isAssignToMe ? "Confirm assignment" : "Confirm reassignment";
                  var icon = assignedToEmpId.HasValue ? MessageBoxIcon.Question : MessageBoxIcon.Warning;
                  var targetLabel = assignedToEmpId.HasValue ? who : "(Unassigned)";

                  if (toChange.Count > 1)
                  {
                      var groups = toChange
                          .GroupBy(GetAssigneeLabel)
                          .OrderByDescending(g => g.Count())
                          .ThenBy(g => g.Key)
                          .Take(5)
                          .Select(g => $"{g.Count(),2} from {g.Key}")
                          .ToList();

                      var groupText = groups.Count > 0 ? ("\n\nCurrent assignments:\n" + string.Join("\n", groups)) : string.Empty;
                      var note = alreadyOk > 0 ? $"\n\nNote: {alreadyOk} selected ticket(s) are already assigned to '{targetLabel}' and will be skipped." : string.Empty;

                      if (MessageBox.Show(
                              $"{verb} {toChange.Count} ticket(s) to '{targetLabel}'?\n\nThis updates the ticket assignment immediately.{groupText}{note}\n\nContinue?",
                              titleConfirm,
                              MessageBoxButtons.YesNo,
                              icon,
                              MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                          return;
                  }
                  else
                  {
                      var t = toChange[0];
                      var ticketText = GetTicketLabel(t);
                      var currentWho = GetAssigneeLabel(t);

                      var message =
                          $"{verb} {ticketText}\n\n" +
                          $"From: {currentWho}\n" +
                          $"To:   {targetLabel}\n\n" +
                          "This updates the ticket assignment immediately.\n\n" +
                          "Continue?";

                      if (MessageBox.Show(
                              message,
                              titleConfirm,
                              MessageBoxButtons.YesNo,
                              icon,
                              MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                          return;
                  }

                  foreach (var t in toChange.Where(x => x != null))
                  {
                      await _callEmail.AssignTicketAndNotifyAsync(t.TicketId, assignedToEmpId, changedByUserId);
                  }

                  await SafeRefreshTicketGridsAsync();
                  TrySelectTicketInPendingGrid((toChange.FirstOrDefault() ?? selected[0]).TicketId);
               }
               catch (Exception ex) { ShowFriendlyError("Reassign Failed", "We couldn't reassign the selected ticket(s) right now. Please try again.", ex, "ReassignTicket"); }
          }
        
        private async void btnReturnTempItem_Click(object sender, EventArgs e)
        {
             if (this._selectedTicket == null) return;
             try
             {
                 var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                 if (changedByUserId == null) return;

                 var ticketId = this._selectedTicket.TicketId;
                 var preview = await _callRepo.GetTemporaryReplacementReturnPreviewAsync(ticketId);
                 if (preview.NewItemId <= 0 || preview.Quantity <= 0)
                 {
                     // Service-Only temporary: no parts were ever issued, so offer
                     // a one-click close-out instead of a dead end.
                     if (MessageBox.Show("No temporary parts were issued for this ticket (Service Only).\n\nMark it as Solved instead?", "No Temp Replacement", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                     {
                         var oldStatus = this._selectedTicket.Status;
                         await _callRepo.SetTicketStatusAsync(ticketId, "Solved", changedByUserId, "Temporary service confirmed permanent.");
                         RunNotification(async () => await _callEmail.NotifyStatusChangeAsync(ticketId, oldStatus, "Solved", "Temporary service confirmed permanent.", changedByUserId));
                         await SafeRefreshTicketGridsAsync();
                         TrySelectTicketInSolvedGrid(ticketId);
                     }
                     return;
                 }
                 if (preview.AlreadyReturned) { MessageBox.Show("Already returned.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                  if (MessageBox.Show($"Return {preview.NewItemText} (Qty: {preview.Quantity})?", "Return Temp Item", MessageBoxButtons.YesNo) == DialogResult.Yes)
                  {
                      await _callRepo.ReturnTemporaryReplacementAsync(ticketId, changedByUserId);
                      await SafeRefreshTicketGridsAsync();
                      TrySelectTicketInSolvedGrid(ticketId);
                  }
             }
             catch (Exception ex) { ShowFriendlyError("Return Temp Item", "We couldn't return the temporary item right now. Please try again.", ex, "ReturnTempItem"); }
         }

        private async void btnSyncSet_Click(object sender, EventArgs e)
        {
            if (this._selectedTicket == null)
            {
                MessageBox.Show("Select a ticket first.", "Sync Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!await _callRepo.CallSchemaExistsAsync()) return;

                var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                if (changedByUserId == null) return;

                 var ticketId = this._selectedTicket.TicketId;
 
                var sync = await _callRepo.SyncSetToLatestReplacementWithResultAsync(ticketId, changedByUserId);
 
                var setLabel = (sync != null && sync.SetId.HasValue && sync.SetId.Value > 0)
                    ? (!string.IsNullOrWhiteSpace(sync.SetCode) ? $"{sync.SetCode} (SetId {sync.SetId.Value})" : $"SetId {sync.SetId.Value}")
                    : "N/A";

                var outcome = (sync?.Outcome ?? "Unknown").Trim();
                var detailsText =
                    $"Outcome: {outcome}\n" +
                    $"Set: {setLabel}\n" +
                    $"Requests updated: {sync?.UpdatedRequests ?? 0}\n" +
                    $"SetItems updated: {sync?.UpdatedSetItems ?? 0}\n\n" +
                    "Open the Set Details and click Refresh to confirm the serial number.";

                MessageBox.Show(
                    detailsText,
                    "Sync Set",
                    MessageBoxButtons.OK,
                    outcome.Equals("Swapped", StringComparison.OrdinalIgnoreCase) ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
 
                await SafeRefreshTicketGridsAsync();
                TrySelectTicketInPendingGrid(ticketId);
            }
            catch (Exception ex)
            {
                // Service-Only tickets have no replacement swap to sync - explain,
                // don't alarm.
                if (ex is InvalidOperationException && (ex.Message ?? string.Empty).IndexOf("No replacement history", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MessageBox.Show("This ticket has no replacement history (Service Only), so there is no set to sync.", "Sync Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ShowFriendlyError("Sync Set Failed", "We couldn't sync the set details right now. Please try again.", ex, "SyncSet");
            }
        }

        private void RequestHistorySearchFilterDebounced()
        {
            if (this.IsDisposed)
                return;

            try
            {
                _historySearchDebounce.Stop();
                _historySearchDebounce.Start();
            }
            catch
            {
                ApplyHistoryFilterAndRender();
            }
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async void btnMarkAs_Click(object sender, EventArgs e)
        {
             if (this._selectedTicket == null)
             {
                 MessageBox.Show("Select a ticket first.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Information);
                 return;
             }
             try
             {
                 var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                 if (changedByUserId == null) return;
                 
                 var ticketId = this._selectedTicket.TicketId;
                 var oldStatus = this._selectedTicket.Status;

                 var itemRepo = new ItemRepository();
                 var outHardware = await itemRepo.GetHardwareOutItemsLookupAsync();
                 var stockHardware = await itemRepo.GetHardwareStockItemsLookupAsync();
                 var categoryRepo = new CategoryRepository();
                 var categories = (await categoryRepo.GetAllAsync()).Where(c => c.Active).OrderBy(c => c.Name).ToList();
                 var conditionRepo = new ItemConditionRepository();
                 var conditions = conditionRepo.GetAll().OrderBy(c => c.ConditionId).ToList();

                 using (var dlg = new MarkAsResolutionDialog(outHardware, stockHardware, categories, conditions))
                 {
                     if (dlg.ShowDialog(this) != DialogResult.OK) return;

                     string markAsStatus = "Solved";
                     if (dlg.ResolutionType == "Replacement")
                     {
                          int oldItemId = dlg.UseUnlistedOldItem ? 
                              itemRepo.AddItem(new Yakult.Inventory.App.Pages.ItemDto 
                              { 
                                  Name = dlg.UnlistedOldItemName,
                                  Description = dlg.UnlistedOldItemDescription,
                                  ModelNumber = dlg.UnlistedOldItemModelNumber,
                                  SerialNumber = dlg.UnlistedOldItemSerialNumber,
                                  UnitOfMeasure = dlg.UnlistedOldItemUnitOfMeasure,
                                  CreatedByUserId = changedByUserId.Value,
                                  CreatedByName = AppSession.CurrentUserName,
                                  Active = true,
                                  DateCreated = DateTime.Now,
                                  ItemType = "Hardware",
                                  CategoryId = dlg.UnlistedOldItemCategoryId,
                                  Category = dlg.UnlistedOldItemCategoryName,
                                  StockOnHand = 0,
                                  ConditionId = dlg.OldItemConditionId,
                                  Remarks = dlg.OldItemConditionRemarks,
                                  AffectsInventory = true,
                                  IsTrackedAsset = false
                              })
                              : (dlg.OldItemId ?? 0);
                              
                           await _callRepo.ApplyTicketReplacementAsync(
                               ticketId,
                               oldItemId,
                               dlg.NewItemId ?? 0,
                               dlg.Quantity,
                               dlg.Remarks,
                               changedByUserId,
                               dlg.OldItemConditionId,
                               dlg.OldItemConditionRemarks,
                               dlg.OldItemRepairAction);
                           markAsStatus = dlg.IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved";
                     }
                      else
                      {
                           await _callRepo.LogTicketResolutionAsync(ticketId, "Service Only", dlg.Remarks, changedByUserId);
                           markAsStatus = dlg.IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved";
                      }
                      
                      var resolutionLabel = dlg.ResolutionType + (dlg.IsTemporaryReplacement ? " (Temporary)" : string.Empty);
                      var note = string.IsNullOrWhiteSpace(dlg.Remarks) ? $"Marked as: {resolutionLabel}" : $"Marked as: {resolutionLabel} | {dlg.Remarks}";
                     await _callRepo.SetTicketStatusAsync(ticketId, markAsStatus, changedByUserId, note);
                     RunNotification(async () => await _callEmail.NotifyStatusChangeAsync(ticketId, oldStatus, markAsStatus, dlg.Remarks, changedByUserId));
                 }
                 await SafeRefreshTicketGridsAsync();
                 // Mark As always lands Solved / Resolved (Temporary), which live
                 // in the solved grid - reselect there, not in pending.
                 TrySelectTicketInSolvedGrid(ticketId);
             }
             catch (Exception ex) { ShowFriendlyError("Mark As Failed", "We couldn't update the ticket status right now. Please try again.", ex, "MarkAs"); }
        }

        private async void btnEscalationOverride_Click(object sender, EventArgs e)
        {
             if (this._selectedTicket == null) return;
             try
             {
                 if (!await _callRepo.EscalationOverridesEnabledAsync()) return;
                 var ticketId = this._selectedTicket.TicketId;
                 var existing = await _callRepo.GetTicketEscalationOverrideAsync(ticketId);
                 var settings = _escalationSettings ?? await _callRepo.GetEscalationSettingsAsync();
                 
                  using (var dlg = new EscalationOverrideForm(settings, existing))
                  {
                      if (dlg.ShowDialog(this) != DialogResult.OK) return;
                      var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                      await _callRepo.SetTicketEscalationOverrideAsync(ticketId, dlg.ClearRequested ? null : (int?)dlg.DaysToSupervisor, dlg.ClearRequested ? null : (int?)dlg.DaysToManager, dlg.Reason, changedByUserId, dlg.ClearRequested);
                      
                      var updated = await _callRepo.GetTicketEscalationOverrideAsync(ticketId);
                      if (this.lblNextEscalation != null)
                      {
                          var full = BuildEscalationLabel(this._selectedTicket, settings, updated);
                          this.lblNextEscalation.Text = full;
                          EnsureToolTip().SetToolTip(this.lblNextEscalation, full);
                      }
                      
                      _ = LoadSelectedTicketDetailsAsync(ticketId, ++_selectionVersion);
                  }
              }
             catch(Exception ex) { ShowFriendlyError("Ticket Workflow", "We couldn't complete that ticket action right now. Please try again.", ex, "TicketWorkflowAction"); }
        }
        
        // --- Selection & Helpers ---

        private void ResetTicketPaging()
        {
            _pendingPageIndex = 1;
            _solvedPageIndex = 1;
        }
        
        private void PendingTickets_SelectionChanged(object sender, EventArgs e)
        {
             if (_suppressTicketSelectionChanged)
                 return;

             // SelectedRows can be empty depending on selection behavior; fall back to CurrentRow/CurrentCell.
             DataGridViewRow row = null;

             if (this.dgvPendingTickets.CurrentRow != null)
                 row = this.dgvPendingTickets.CurrentRow;
             else if (this.dgvPendingTickets.CurrentCell != null)
                 row = this.dgvPendingTickets.CurrentCell.OwningRow;

             if (row == null && this.dgvPendingTickets.SelectedRows.Count > 0)
                 row = this.dgvPendingTickets.SelectedRows[0];

             if (row == null)
                 return;

             ApplySelectionToDetails(row);
             this.dgvSolvedTickets.ClearSelection();
        }
        
        private void SolvedTickets_SelectionChanged(object sender, EventArgs e)
        {
             if (_suppressTicketSelectionChanged)
                 return;

             DataGridViewRow row = null;

             if (this.dgvSolvedTickets.CurrentRow != null)
                 row = this.dgvSolvedTickets.CurrentRow;
             else if (this.dgvSolvedTickets.CurrentCell != null)
                 row = this.dgvSolvedTickets.CurrentCell.OwningRow;

             if (row == null && this.dgvSolvedTickets.SelectedRows.Count > 0)
                 row = this.dgvSolvedTickets.SelectedRows[0];

             if (row == null)
                 return;

             ApplySelectionToDetails(row);
             this.dgvPendingTickets.ClearSelection();
        }
        
        private void TicketGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
             if (e.RowIndex < 0) return;
             var grid = sender as DataGridView;
             if (grid?.Rows[e.RowIndex].Tag is CallTicketListItem t)
             {
                 OpenTicketDetailsDialog(t.TicketId);
             }
        }
        
        private void ApplySelectionToDetails(DataGridViewRow row)
        {
             if (row == null || !(row.Tag is CallTicketListItem t)) return;
             this._selectedTicket = t;
             
             // Update UI details
             ConfigureStatusDropdownForTicket(t.Status);
             if (this.cboPriority != null && this.cboPriority.Items.Contains(t.Priority)) this.cboPriority.SelectedItem = t.Priority;
             
             // SelectLookupItemById(this.cboReassignTo, t.AssignedToEmpId ?? 0); // Not available in list item
             if (this.cboReassignTo != null)
             {
                 int index = this.cboReassignTo.FindStringExact(t.ResponsiblePerson);
                 if (index >= 0) this.cboReassignTo.SelectedIndex = index;
             }

             this.txtCaseProblem.Text = $"[{t.TicketCode ?? t.TicketId.ToString()}] {t.Issue} (by {t.CallerName})";
             this.txtAttemptedSolutions.Clear();
             
              // Update Summary
              this.lblSummaryId.Text = t.TicketCode ?? t.TicketId.ToString(); // Removed prefix
              this.lblSummaryAge.Text = $"{t.TicketAgeDays} Days";

              var sla = BuildSlaCell(t);
              this.lblSummarySla.Text = sla.Label;
              this.lblSummarySla.AutoEllipsis = true;
              this.lblSummarySla.ForeColor = sla.State == SlaState.Breached
                  ? Color.FromArgb(231, 76, 60)
                  : (sla.State == SlaState.AtRisk ? Color.FromArgb(230, 126, 34)
                      : (sla.State == SlaState.Ok ? Color.FromArgb(46, 204, 113) : Yakult.Inventory.App.Helpers.ModernUiHelper.ColorTextSecondary));

              var escalationFull = BuildEscalationLabel(t, _escalationSettings, null);
              this.lblSummaryEscalation.Text = BuildEscalationShortLabel(escalationFull);
              this.lblSummaryEscalation.AutoEllipsis = true;

              var tt = EnsureToolTip();
              tt.SetToolTip(this.lblSummarySla, sla.Tooltip);
              tt.SetToolTip(this.lblSummaryEscalation, escalationFull);

              UpdateTicketProfile(t);
              
              UpdateTicketActionStates();
              
              // Debounce DB-backed detail loading so fast row changes don't feel jittery.
              RequestSelectedTicketDetailsDebounced(t.TicketId);
        }

        private void UpdateTicketProfile(CallTicketListItem t)
        {
            try
            {
                if (t == null)
                {
                    if (this.lblProfileStatus != null) this.lblProfileStatus.Text = "-";
                    if (this.lblProfilePriority != null) this.lblProfilePriority.Text = "-";
                    if (this.lblProfileAssignee != null) this.lblProfileAssignee.Text = "-";
                    if (this.lblProfileCaller != null) this.lblProfileCaller.Text = "-";
                    if (this.lblProfileLocation != null) this.lblProfileLocation.Text = "-";
                    if (this.lblEmailInfo != null) this.lblEmailInfo.Text = "-";
                    if (this.lblNextEscalation != null) this.lblNextEscalation.Text = "-";
                    if (this.lblLastEmail != null) this.lblLastEmail.Text = "-";
                    if (this.btnProfileDetails != null) this.btnProfileDetails.Enabled = false;
                    if (this.btnProfileSummary != null) this.btnProfileSummary.Enabled = false;
                    if (this.btnProfileDelete != null) this.btnProfileDelete.Enabled = false;
                    return;
                }

                var isOverdue = IsTicketOverdue(t);
                var status = isOverdue ? "Overdue" : NormalizeLegacyTicketStatus(t.Status);
                var priority = string.IsNullOrWhiteSpace(t.Priority) ? "-" : t.Priority.Trim();

                if (this.lblProfileStatus != null)
                {
                    this.lblProfileStatus.Text = status;
                    this.lblProfileStatus.ForeColor = GetStatusColor(status, isOverdue);
                }

                if (this.lblProfilePriority != null)
                {
                    this.lblProfilePriority.Text = priority;
                    this.lblProfilePriority.ForeColor = GetPriorityColor(priority);
                }

                if (this.lblProfileAssignee != null)
                    this.lblProfileAssignee.Text = string.IsNullOrWhiteSpace(t.ResponsiblePerson) ? "(Unassigned)" : t.ResponsiblePerson.Trim();

                if (this.lblProfileCaller != null)
                    this.lblProfileCaller.Text = string.IsNullOrWhiteSpace(t.CallerName) ? "-" : t.CallerName.Trim();

                if (this.lblProfileLocation != null)
                {
                    var dept = string.IsNullOrWhiteSpace(t.Department) ? "-" : t.Department.Trim();
                    var branch = string.IsNullOrWhiteSpace(t.Branch) ? "-" : t.Branch.Trim();
                    this.lblProfileLocation.Text = $"{dept} • {branch}";
                }

                var lastActivityUtc = GetLastActivityUtc(t, out var lastActivitySource);
                var idle = DateTime.UtcNow - lastActivityUtc;
                if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
                var idleLabel = FormatShortDuration(idle);

                if (this.lblEmailInfo != null)
                    this.lblEmailInfo.Text = $"{ToLocalString(lastActivityUtc, "g")} • {idleLabel} idle";

                var esc = BuildEscalationLabel(t, _escalationSettings, null);
                if (this.lblNextEscalation != null)
                    this.lblNextEscalation.Text = esc;

                if (this.lblLastEmail != null)
                    this.lblLastEmail.Text = "Loading...";

                var tt = EnsureToolTip();
                if (this.lblEmailInfo != null)
                    tt.SetToolTip(this.lblEmailInfo, $"Last activity ({lastActivitySource}): {ToLocalString(lastActivityUtc, "g")}\nIdle: {idleLabel}");
                if (this.lblNextEscalation != null)
                    tt.SetToolTip(this.lblNextEscalation, esc);
                if (this.lblLastEmail != null)
                    tt.SetToolTip(this.lblLastEmail, "Loading last email log...");

                if (this.btnProfileDetails != null) this.btnProfileDetails.Enabled = true;
                if (this.btnProfileSummary != null) this.btnProfileSummary.Enabled = true;

                // Hard delete is restricted to Admin/Developer.
                if (this.btnProfileDelete != null)
                    this.btnProfileDelete.Enabled = AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);
            }
            catch
            {
                // Never block ticket selection for profile rendering issues.
            }
        }

        private async Task DeleteSelectedTicketAsync()
        {
            var t = this._selectedTicket;
            if (t == null || t.TicketId <= 0)
                return;

            if (!AppSession.IsLoggedIn || !(AppSession.IsAdmin || AppSession.IsDeveloper))
            {
                MessageBox.Show("You do not have permission to delete tickets.", "Delete Ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            if (changedByUserId == null)
                return;

            var ticketLabel = string.IsNullOrWhiteSpace(t.TicketCode) ? t.TicketId.ToString() : t.TicketCode.Trim();

            if (!ConfirmHardDelete(ticketLabel))
                return;

            try
            {
                if (this.btnProfileDelete != null) this.btnProfileDelete.Enabled = false;

                var deleted = await _callRepo.DeleteTicketAsync(t.TicketId, changedByUserId);
                if (!deleted)
                {
                    MessageBox.Show("Ticket was not deleted (it may have already been removed).", "Delete Ticket", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Clear selection so refresh can auto-select a valid ticket.
                this._selectedTicket = null;
                UpdateTicketProfile(null);
                UpdateTicketActionStates();

                await SafeRefreshTicketGridsAsync();
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Delete Ticket Failed", "We couldn't delete the ticket right now. Please try again.", ex, "DeleteTicket");
            }
            finally
            {
                // Re-enable based on current selection + role.
                if (this.btnProfileDelete != null)
                    this.btnProfileDelete.Enabled = this._selectedTicket != null && AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);
            }
        }

        private bool ConfirmHardDelete(string ticketLabel)
        {
            try
            {
                var msg =
                    "This will permanently delete the ticket and its related data (notes/history/email logs if installed).\n\n" +
                    $"Ticket: {ticketLabel}\n\n" +
                    "This cannot be undone.\n\nProceed?";

                if (MessageBox.Show(msg, "Hard Delete Ticket", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return false;

                using (var dlg = new Form())
                {
                    dlg.Text = "Confirm Delete";
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MinimizeBox = false;
                    dlg.MaximizeBox = false;
                    dlg.ShowInTaskbar = false;
                    dlg.ClientSize = new Size(520, 160);

                    var lbl = new Label
                    {
                        AutoSize = false,
                        Dock = DockStyle.Top,
                        Height = 70,
                        Padding = new Padding(12, 12, 12, 0),
                        Text =
                            $"Type the ticket code/ID to confirm deletion:\n\n{ticketLabel}",
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
                    };

                    var txt = new TextBox
                    {
                        Dock = DockStyle.Top,
                        Margin = new Padding(12),
                        Font = new Font("Segoe UI", 10F, FontStyle.Regular)
                    };

                    var pnlButtons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Bottom,
                        FlowDirection = FlowDirection.RightToLeft,
                        Height = 48,
                        Padding = new Padding(12, 8, 12, 8),
                        WrapContents = false
                    };

                    var btnOk = new Button { Text = "Delete", Width = 90, Height = 28, DialogResult = DialogResult.OK };
                    var btnCancel = new Button { Text = "Cancel", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
                    pnlButtons.Controls.Add(btnOk);
                    pnlButtons.Controls.Add(btnCancel);

                    dlg.Controls.Add(pnlButtons);
                    dlg.Controls.Add(txt);
                    dlg.Controls.Add(lbl);

                    dlg.AcceptButton = btnOk;
                    dlg.CancelButton = btnCancel;

                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return false;

                    var typed = (txt.Text ?? string.Empty).Trim();
                    return string.Equals(typed, (ticketLabel ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ToLocalString(DateTime value, string format)
        {
            return AppTime.ToLocalString(value, string.IsNullOrWhiteSpace(format) ? "g" : format);
        }

        private void OpenTicketDetailsDialog(int ticketId)
        {
             using (var dlg = new TicketDetailsDialog(_callRepo, ticketId))
             {
                  dlg.ShowDialog(this);
                  HandleTicketDetailsAction(ticketId, dlg.RequestedAction);
                  _ = SafeRefreshTicketGridsAsync();
             }
        }

        private void HandleTicketDetailsAction(int ticketId, TicketDetailsDialog.TicketDetailAction action)
        {
            if (action == TicketDetailsDialog.TicketDetailAction.None)
                return;

            TrySelectTicketInPendingGrid(ticketId);

            if (action == TicketDetailsDialog.TicketDetailAction.OpenEmailLog)
            {
                using (var form = new EmailLogShortcutDialog(_callRepo, ticketId, this._selectedTicket?.TicketCode))
                {
                    form.ShowDialog(this);
                }
                return;
            }

            if (this._selectedTicket == null || this._selectedTicket.TicketId != ticketId)
            {
                MessageBox.Show("The ticket action is available from the ticket list after the ticket is selected.", "Ticket Action", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (action == TicketDetailsDialog.TicketDetailAction.Reassign)
            {
                this.cboReassignTo?.Focus();
                return;
            }

            if (action == TicketDetailsDialog.TicketDetailAction.MarkAs)
            {
                btnMarkAs_Click(this.btnMarkAs, EventArgs.Empty);
                return;
            }

            if (action == TicketDetailsDialog.TicketDetailAction.Reopen)
            {
                btnReopen_Click(this.btnReopen, EventArgs.Empty);
            }
        }

        private void OpenTicketSummaryDialog()
        {
            if (this._selectedTicket == null || this._selectedTicket.TicketId <= 0)
                return;

            try
            {
                var t = this._selectedTicket;

                var sla = BuildSlaCell(t);
                var escalation = (this.lblNextEscalation?.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(escalation))
                    escalation = BuildEscalationLabel(t, _escalationSettings, null);

                var lastActivityUtc = GetLastActivityUtc(t, out var lastActivitySource);
                var idle = DateTime.UtcNow - lastActivityUtc;
                if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;

                var lastActivityText = $"{ToLocalString(lastActivityUtc, "g")} ({lastActivitySource})";
                var idleText = FormatShortDuration(idle);

                using (var dlg = new TicketSummaryDialog(
                    ticket: t,
                    slaLabel: sla.Label,
                    slaTooltip: sla.Tooltip,
                    escalationText: escalation,
                    lastActivityText: lastActivityText,
                    idleText: idleText))
                {
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Ticket Summary", "We couldn't open the ticket summary right now. Please try again.", ex, "OpenTicketSummary");
            }
        }

        private void ShowFriendlyError(string title, string userMessage, Exception ex, string context)
        {
            try
            {
                Logger.LogError($"[TicketManagementControl] {context}", ex);
            }
            catch
            {
            }

            MessageBox.Show(
                userMessage,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void ShowLookupLoadError(Exception ex)
        {
            try
            {
                Logger.LogError("[TicketManagementControl] LoadLookups", ex);
            }
            catch
            {
            }

            var nowUtc = DateTime.UtcNow;
            var shouldShow = !_lookupLoadErrorShown || (nowUtc - _lastLookupLoadErrorShownUtc) >= TimeSpan.FromSeconds(30);
            _lookupLoadErrorShown = true;
            _lastLookupLoadErrorShownUtc = nowUtc;

            if (!shouldShow)
                return;

            MessageBox.Show(
                "We couldn't load some call monitoring data right now. You can try again in a moment.",
                "Call Monitoring",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void ShowTicketDetailLoadWarning(Exception ex, int ticketId)
        {
            try
            {
                Logger.LogError($"[TicketManagementControl] LoadSelectedTicketDetailsAsync failed for TicketId={ticketId}", ex);
            }
            catch
            {
            }

            try
            {
                if (this.IsDisposed)
                    return;

                Action apply = () =>
                {
                    if (this.IsDisposed)
                        return;

                    var warningText = "Details unavailable";
                    var warningTip = "Some ticket details could not be loaded right now. You can continue working and try selecting the ticket again.";

                    if (this.lblLastEmail != null)
                    {
                        this.lblLastEmail.Text = warningText;
                        this.lblLastEmail.ForeColor = Color.FromArgb(211, 84, 0);
                        EnsureToolTip().SetToolTip(this.lblLastEmail, warningTip);
                    }

                    if (this.lblEmailInfo != null)
                    {
                        EnsureToolTip().SetToolTip(this.lblEmailInfo, warningTip);
                    }
                };

                if (this.InvokeRequired) this.BeginInvoke(apply);
                else apply();
            }
            catch
            {
            }
        }
         
         private static void RunNotification(Func<Task> action)
         {
              if (action != null)
                  _ = Task.Run(async () =>
                  {
                      try
                      {
                          await action();
                      }
                      catch (Exception ex)
                      {
                          Logger.LogError("[TicketManagementControl] Background notification failed.", ex);
                      }
                  });
         }

        private List<CallTicketListItem> GetSelectedPendingTickets()
        {
            var result = new List<CallTicketListItem>();
            try
            {
                if (this.dgvPendingTickets == null)
                    return result;

                foreach (DataGridViewRow row in this.dgvPendingTickets.SelectedRows)
                {
                    if (row?.Tag is CallTicketListItem t && t.TicketId > 0)
                        result.Add(t);
                }

                // Stable order (top-to-bottom selection)
                result = result
                    .GroupBy(t => t.TicketId)
                    .Select(g => g.First())
                    .OrderBy(t => t.CreatedAt)
                    .ToList();
            }
            catch
            {
            }

            return result;
        }

        private void AddQuickFilterButtons()
        {
            if (this.pnlStatusLegend == null)
                return;

            var spacer = new Label
            {
                Text = " | ",
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Padding = new Padding(6, 6, 6, 0)
            };
            this.pnlStatusLegend.Controls.Add(spacer);

            this.pnlStatusLegend.Controls.Add(Yakult.Inventory.App.Helpers.ModernUiHelper.CreateFilterChip("All", this.cboFilter.SelectedItem?.ToString() == "All", () =>
            {
                this.cboFilter.SelectedItem = "All";
                SelectLookupItemById(this.cboAssigneeFilter, -1);
                ResetTicketPaging();
                _ = SafeRefreshTicketGridsAsync();
            }));

            this.pnlStatusLegend.Controls.Add(Yakult.Inventory.App.Helpers.ModernUiHelper.CreateFilterChip("Mine", false, () =>
            {
                if (!AppSession.CurrentEmployeeId.HasValue || AppSession.CurrentEmployeeId.Value <= 0)
                {
                    MessageBox.Show("Your account is not linked to an employee (EmpId).", "My Tickets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                this.cboFilter.SelectedItem = "All";
                SelectLookupItemById(this.cboAssigneeFilter, AppSession.CurrentEmployeeId.Value);
                ResetTicketPaging();
                _ = SafeRefreshTicketGridsAsync();
            }));

            this.pnlStatusLegend.Controls.Add(Yakult.Inventory.App.Helpers.ModernUiHelper.CreateFilterChip("Unassigned", false, () =>
            {
                this.cboFilter.SelectedItem = "All";
                SelectLookupItemById(this.cboAssigneeFilter, 0);
                ResetTicketPaging();
                _ = SafeRefreshTicketGridsAsync();
            }));

            this.pnlStatusLegend.Controls.Add(Yakult.Inventory.App.Helpers.ModernUiHelper.CreateFilterChip("Overdue", this.cboFilter.SelectedItem?.ToString() == "Overdue", () =>
            {
                this.cboFilter.SelectedItem = "Overdue";
                ResetTicketPaging();
                _ = SafeRefreshTicketGridsAsync();
            }));

            this.pnlStatusLegend.Controls.Add(Yakult.Inventory.App.Helpers.ModernUiHelper.CreateFilterChip("Escalated", this.cboFilter.SelectedItem?.ToString() == "Escalated", () =>
            {
                this.cboFilter.SelectedItem = "Escalated";
                ResetTicketPaging();
                _ = SafeRefreshTicketGridsAsync();
            }));

        }
        
        public async Task SelectTicketAsync(int ticketId)
        {
             // Reuse Navigate logic..
              this.txtSearch.Text = "";
              this.cboFilter.SelectedItem = "All";
              ResetTicketPaging();
              await SafeRefreshTicketGridsAsync();
              TrySelectTicketInPendingGrid(ticketId);
              // If not found, try solved
              if (this._selectedTicket == null || this._selectedTicket.TicketId != ticketId)
              {
                  this.cboFilter.SelectedItem = "Solved";
                  ResetTicketPaging();
                  await SafeRefreshTicketGridsAsync();
                  TrySelectTicketInSolvedGrid(ticketId);
              }
         }

        /// <summary>
        /// Keyboard shortcuts for power users
        /// </summary>
        private void TicketManagementControl_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+N - New Ticket (focus to first field)
            if (e.Control && e.KeyCode == Keys.N)
            {
                e.Handled = true;
                this.cboCompany.Focus();
                ToastNotification.ShowInfo("New Ticket", "Enter details for the new ticket");
                return;
            }

            // Ctrl+F - Focus search
            if (e.Control && e.KeyCode == Keys.F)
            {
                e.Handled = true;
                this.txtSearch.Focus();
                this.txtSearch.SelectAll();
                return;
            }

            // Ctrl+Enter - Create ticket (when focused in new ticket area)
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                if (this.btnCreateTicket.Enabled && 
                    (this.cboCompany.Focused || this.cboCallerName.Focused || this.txtTechnicalProblem.Focused))
                {
                    e.Handled = true;
                    btnCreateTicket_Click(this.btnCreateTicket, EventArgs.Empty);
                    return;
                }
            }

            // F5 - Refresh
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                _ = SafeRefreshTicketGridsAsync();
                ToastNotification.ShowSuccess("Refreshed", "Ticket data updated");
                return;
            }

            // Esc - Clear selection or close details
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                this.dgvPendingTickets.ClearSelection();
                this.dgvSolvedTickets.ClearSelection();
                return;
            }

            // Ctrl+1/2/3/4/5/6 - Quick filter by status
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6)
            {
                e.Handled = true;
                string[] filters = { "All", "Pending", "In Progress", "Escalated", "Solved", "Overdue" };
                int index = e.KeyCode - Keys.D1;
                if (index < filters.Length)
                {
                    this.cboFilter.SelectedItem = filters[index];
                    ResetTicketPaging();
                    _ = SafeRefreshTicketGridsAsync();
                    ToastNotification.ShowInfo("Filter Applied", $"Showing {filters[index]} tickets");
                }
                return;
            }
        }
    }
}
