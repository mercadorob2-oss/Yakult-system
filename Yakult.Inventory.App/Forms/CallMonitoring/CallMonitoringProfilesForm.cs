using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class CallMonitoringProfilesForm : Form
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly string _itDepartmentName;
        private readonly Action<int> _navigateToTicket;
        private readonly CallEmailNotificationService _email;

        // UI Controls
        private ComboBox cboEmployee;
        private DateTimePicker dtFrom;
        private DateTimePicker dtTo;
        private ComboBox cboQuickRange;
        private Button btnRefresh;
        private Button btnApplyFilters;
        private Button btnClearFilters;
        private Button btnAutoEscalationAssignee;
        private Button btnViewProfileCard;
        private Button btnToggleFilters;
        private Label lblStatus;

        private ComboBox cboDepartmentFilter;
        private ComboBox cboLocationModeFilter;
        private ComboBox cboPriorityFilter;
        private ComboBox cboStatusFilter;
        private TextBox txtSearch;

        // Metrics Labels
        private Label lblValOpen, lblValPending, lblValInProgress, lblValEscalated;
        private Label lblValHandledAssigned, lblValHandledSolved;

        // Metric cards
        private Panel cardOpen, cardPending, cardInProgress, cardEscalated;
        private Panel cardHandledAssignee, cardHandledSolver;

        // Grids
        private TabControl tabGrids;
        private DataGridView dgvOpen;
        private DataGridView dgvHandled;

        // Ticket Profile (right panel)
        private SplitContainer splitGridAndProfile;
        private Panel pnlTicketProfile;
        private Label lblProfileTicket;
        private Label lblProfileStatus;
        private Label lblProfilePriority;
        private Label lblProfileLocation;
        private Label lblProfileAssignedTo;
        private Label lblProfileCaller;
        private Label lblProfileUpdated;
        private Label lblProfileCreated;
        private Label lblProfileLastEmail;
        private Button btnProfileOpen;
        private Button btnProfileSummary;
        private int _profileSelectedTicketId;
        private int _profileSelectionVersion;
        private bool _suppressProfileSelectionChanged;
        private CancellationTokenSource _profileUpdateCts;
        private const int GridProfileMinWidth = 480;
        private const int GridProfileRightMinWidth = 320;
        private const int GridProfileDesiredRightWidth = 360;
        private CancellationTokenSource _applyFiltersCts;
        private int _applyFiltersVersion;

        private List<CallEmployeeProfileLookup> _employees = new List<CallEmployeeProfileLookup>();
        private List<LookupItem> _departments = new List<LookupItem>();

        private List<CallTechOpenTicketRow> _openTicketsAll = new List<CallTechOpenTicketRow>();
        private List<CallTechHandledTicketRow> _handledTicketsAll = new List<CallTechHandledTicketRow>();
        private List<CallTechOpenTicketRow> _openTicketsFiltered = new List<CallTechOpenTicketRow>();
        private List<CallTechHandledTicketRow> _handledTicketsFiltered = new List<CallTechHandledTicketRow>();

        private readonly Dictionary<int, (CallTicketListItem Ticket, DateTime FetchedUtc)> _ticketCache
            = new Dictionary<int, (CallTicketListItem, DateTime)>();

        private readonly Dictionary<int, (CallEmailLogItem Email, DateTime FetchedUtc)> _emailCache
            = new Dictionary<int, (CallEmailLogItem, DateTime)>();

        private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromSeconds(30);

        // Colors
        private readonly Color clrBackground = Color.FromArgb(243, 244, 246);
        private readonly Color clrCardWithBorder = Color.White;
        private readonly Color clrTextDark = Color.FromArgb(17, 24, 39);
        private readonly Color clrTextMuted = Color.FromArgb(107, 114, 128);
        private readonly Color clrPrimary = Color.FromArgb(59, 130, 246); // Blue
        private readonly Color clrAccentSolved = Color.FromArgb(16, 185, 129); // Green
        private readonly Color clrAccentEscalated = Color.FromArgb(239, 68, 68); // Red
        private readonly Color clrAccentPending = Color.FromArgb(245, 158, 11); // Orange

        private enum HandledRoleFilter
        {
            All,
            Assignee,
            Solver
        }

        private HandledRoleFilter _handledRoleFilter = HandledRoleFilter.All;

        public CallMonitoringProfilesForm(ICallMonitoringRepository repo, string itDepartmentName, Action<int> navigateToTicket = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _itDepartmentName = string.IsNullOrWhiteSpace(itDepartmentName) ? null : itDepartmentName.Trim();
            _navigateToTicket = navigateToTicket;
            _email = new CallEmailNotificationService(_repo);

            InitializeComponent();
            this.HandleCreated += (_, __) => BeginInvoke((Action)(() => EnsureGridProfileSplitLayout(force: true)));
            this.SizeChanged += (_, __) =>
            {
                if (this.Visible)
                    EnsureGridProfileSplitLayout(force: false);
            };
            this.Load += async (_, __) =>
            {
                await LoadEmployeesAsync();
                await LoadDepartmentsAsync();
                await RefreshAsync();
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.BackColor = clrBackground;
            this.Padding = new Padding(20);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Container for entire scrollable content if needed, but we rely on Dock logic
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = clrBackground
            };
            const float CollapsedFiltersHeight = 74F;
            const float ExpandedFiltersHeight = 112F;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, CollapsedFiltersHeight)); // Filters
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // Metrics
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Grid

            // 1. Filter Section (Top Card)
            var filterPanel = CreateCardPanel();
            filterPanel.Dock = DockStyle.Fill;
            filterPanel.Padding = new Padding(15, 8, 15, 8);
            
            // Build Filter Controls
            cboEmployee = new ComboBox
            {
                Width = 360,
                DropDownWidth = 560,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };
            dtFrom = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10F) };
            dtTo = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10F) };
            cboQuickRange = new ComboBox
            {
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };
            cboQuickRange.Items.AddRange(new object[] { "Last 30 days", "Today", "Last 7 days", "This month", "This year" });
            cboQuickRange.SelectedIndex = 0;
            cboQuickRange.SelectedIndexChanged += (_, __) => ApplyQuickRange();

            btnRefresh = new Button
            {
                Text = "Refresh Data",
                Width = 120,
                Height = 32,
                BackColor = clrPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (_, __) => await RefreshAsync();

            btnAutoEscalationAssignee = new Button
            {
                Text = "Escalation Assignee…",
                Width = 160,
                Height = 32,
                BackColor = Color.White,
                ForeColor = clrPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Visible = AppSession.IsAdmin || AppSession.IsDeveloper
            };
            btnAutoEscalationAssignee.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
            btnAutoEscalationAssignee.Click += async (_, __) => await OpenAutoEscalationAssigneeDialogAsync();

            btnViewProfileCard = new Button
            {
                Text = "View Profile Card",
                Width = 140,
                Height = 32,
                BackColor = Color.White,
                ForeColor = clrTextDark,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnViewProfileCard.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnViewProfileCard.Click += (_, __) => OpenProfileCard();

            btnToggleFilters = new Button
            {
                Text = "Filters ▾",
                Width = 90,
                Height = 32,
                BackColor = Color.White,
                ForeColor = clrTextMuted,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnToggleFilters.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);

            btnApplyFilters = new Button
            {
                Text = "Apply Filters",
                Width = 110,
                Height = 32,
                BackColor = Color.White,
                ForeColor = clrPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnApplyFilters.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
            btnApplyFilters.Click += (_, __) => ApplyFilters();

            btnClearFilters = new Button
            {
                Text = "Clear",
                Width = 80,
                Height = 32,
                BackColor = Color.White,
                ForeColor = clrTextMuted,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnClearFilters.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnClearFilters.Click += (_, __) => ClearFilters();

            lblStatus = new Label
            {
                Text = "Ready",
                AutoSize = true,
                ForeColor = clrTextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 7, 10, 7),
                BackColor = Color.FromArgb(249, 250, 251),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(10, 0, 0, 0)
            };

            cboDepartmentFilter = new ComboBox
            {
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };

            cboLocationModeFilter = new ComboBox
            {
                Width = 170,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };
            cboLocationModeFilter.Items.AddRange(new object[] { "All", "Department only", "Branch only" });
            cboLocationModeFilter.SelectedIndex = 0;
            cboLocationModeFilter.SelectedIndexChanged += (_, __) => UpdateDepartmentFilterEnabledState();

            cboPriorityFilter = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };
            cboPriorityFilter.Items.AddRange(new object[] { "All Priorities", "Critical", "High", "Medium", "Low" });
            cboPriorityFilter.SelectedIndex = 0;

            cboStatusFilter = new ComboBox
            {
                Width = 170,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat
            };
            cboStatusFilter.Items.AddRange(new object[] { "All Statuses", "Pending", "In Progress", "Escalated", "Forwarded to Repair", "Reopened" });
            cboStatusFilter.SelectedIndex = 0;

            txtSearch = new TextBox
            {
                Width = 240,
                Font = new Font("Segoe UI", 10F)
            };
            txtSearch.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    ApplyFilters();
                }
            };

            // Arrange filters into 2 stable rows (avoid messy wrapping).
            // Use Percent rows so row 2 never collapses to 0 height.
            var filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0),
                Margin = new Padding(0),
                AutoSize = false,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            TableLayoutPanel CreateFilterRow(FlowLayoutPanel left, FlowLayoutPanel right)
            {
                var row = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

                left.Dock = DockStyle.Fill;
                right.Dock = DockStyle.Right;

                row.Controls.Add(left, 0, 0);
                row.Controls.Add(right, 1, 0);
                return row;
            }

            FlowLayoutPanel MakeFlow()
            {
                return new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = true,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
            }

            var row1Left = MakeFlow();
            row1Left.Controls.Add(CreateFilterLabel("Employee"));
            row1Left.Controls.Add(cboEmployee);
            row1Left.Controls.Add(new Label { Width = 14 });
            row1Left.Controls.Add(CreateFilterLabel("From"));
            row1Left.Controls.Add(dtFrom);
            row1Left.Controls.Add(new Label { Width = 8 });
            row1Left.Controls.Add(CreateFilterLabel("To"));
            row1Left.Controls.Add(dtTo);
            row1Left.Controls.Add(new Label { Width = 8 });
            row1Left.Controls.Add(CreateFilterLabel("Quick"));
            row1Left.Controls.Add(cboQuickRange);

            var row1Right = MakeFlow();
            row1Right.WrapContents = false;
            row1Right.Controls.Add(btnRefresh);
            row1Right.Controls.Add(btnAutoEscalationAssignee);
            row1Right.Controls.Add(btnViewProfileCard);
            row1Right.Controls.Add(btnToggleFilters);
            row1Right.Controls.Add(lblStatus);

            var row2Left = MakeFlow();
            row2Left.Controls.Add(CreateFilterLabel("Dept"));
            row2Left.Controls.Add(cboDepartmentFilter);
            row2Left.Controls.Add(new Label { Width = 10 });
            row2Left.Controls.Add(CreateFilterLabel("Show"));
            row2Left.Controls.Add(cboLocationModeFilter);
            row2Left.Controls.Add(new Label { Width = 10 });
            row2Left.Controls.Add(CreateFilterLabel("Priority"));
            row2Left.Controls.Add(cboPriorityFilter);
            row2Left.Controls.Add(new Label { Width = 10 });
            row2Left.Controls.Add(CreateFilterLabel("Status"));
            row2Left.Controls.Add(cboStatusFilter);

            var row2Right = MakeFlow();
            row2Right.WrapContents = false;
            txtSearch.Width = 320;
            row2Right.Controls.Add(CreateFilterLabel("Search"));
            row2Right.Controls.Add(txtSearch);
            row2Right.Controls.Add(new Label { Width = 8 });
            row2Right.Controls.Add(btnApplyFilters);
            row2Right.Controls.Add(btnClearFilters);

            var row1 = CreateFilterRow(row1Left, row1Right);
            var row2 = CreateFilterRow(row2Left, row2Right);

            filterLayout.Controls.Add(row1, 0, 0);
            filterLayout.Controls.Add(row2, 0, 1);

            void SetFiltersExpanded(bool expanded)
            {
                try
                {
                    // Collapse/expand row 2 without leaving dead space.
                    row2.Visible = expanded;

                    if (expanded)
                    {
                        filterLayout.RowStyles[0].SizeType = SizeType.Percent;
                        filterLayout.RowStyles[0].Height = 50F;
                        filterLayout.RowStyles[1].SizeType = SizeType.Percent;
                        filterLayout.RowStyles[1].Height = 50F;

                        mainLayout.RowStyles[0].Height = ExpandedFiltersHeight;
                        btnToggleFilters.Text = "Filters ▴";
                        btnToggleFilters.ForeColor = clrPrimary;
                        btnToggleFilters.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
                    }
                    else
                    {
                        filterLayout.RowStyles[0].SizeType = SizeType.Percent;
                        filterLayout.RowStyles[0].Height = 100F;
                        filterLayout.RowStyles[1].SizeType = SizeType.Absolute;
                        filterLayout.RowStyles[1].Height = 0F;

                        mainLayout.RowStyles[0].Height = CollapsedFiltersHeight;
                        btnToggleFilters.Text = "Filters ▾";
                        btnToggleFilters.ForeColor = clrTextMuted;
                        btnToggleFilters.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
                    }

                    filterLayout.PerformLayout();
                    filterPanel.PerformLayout();
                }
                catch
                {
                }
            }

            var filtersExpanded = false;
            SetFiltersExpanded(filtersExpanded);
            btnToggleFilters.Click += (_, __) =>
            {
                filtersExpanded = !filtersExpanded;
                SetFiltersExpanded(filtersExpanded);
            };

            filterPanel.Controls.Add(filterLayout);
            mainLayout.Controls.Add(filterPanel, 0, 0);

            // 2. Metrics Section
            var metricsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Margin = new Padding(0, 10, 0, 10)
            };
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));

            // Create individual metric cards (clickable)
            cardOpen = CreateMetricCard("Open (Assigned - current)", out lblValOpen, clrPrimary);
            cardPending = CreateMetricCard("Pending", out lblValPending, clrAccentPending);
            cardInProgress = CreateMetricCard("In Progress", out lblValInProgress, clrPrimary);
            cardEscalated = CreateMetricCard("Escalated", out lblValEscalated, clrAccentEscalated);
            cardHandledAssignee = CreateMetricCard("Handled (Assignee)", out lblValHandledAssigned, clrAccentSolved);
            cardHandledSolver = CreateMetricCard("Handled (Solver)", out lblValHandledSolved, clrAccentSolved);

            metricsTable.Controls.Add(cardOpen, 0, 0);
            metricsTable.Controls.Add(cardPending, 1, 0);
            metricsTable.Controls.Add(cardInProgress, 2, 0);
            metricsTable.Controls.Add(cardEscalated, 3, 0);
            metricsTable.Controls.Add(cardHandledAssignee, 4, 0);
            metricsTable.Controls.Add(cardHandledSolver, 5, 0);

            MakeMetricCardClickable(cardOpen, () => ApplyCardFilter(openStatus: null, handledRole: null));
            MakeMetricCardClickable(cardPending, () => ApplyCardFilter(openStatus: "Pending", handledRole: null));
            MakeMetricCardClickable(cardInProgress, () => ApplyCardFilter(openStatus: "In Progress", handledRole: null));
            MakeMetricCardClickable(cardEscalated, () => ApplyCardFilter(openStatus: "Escalated", handledRole: null));
            MakeMetricCardClickable(cardHandledAssignee, () => ApplyCardFilter(openStatus: null, handledRole: HandledRoleFilter.Assignee));
            MakeMetricCardClickable(cardHandledSolver, () => ApplyCardFilter(openStatus: null, handledRole: HandledRoleFilter.Solver));

            mainLayout.Controls.Add(metricsTable, 0, 1);

            // 3. Grids Section
            var gridContainer = CreateCardPanel();
            gridContainer.Dock = DockStyle.Fill;
            gridContainer.Padding = new Padding(5);

            tabGrids = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F) }; // Larger font for tabs
            
            var tpOpen = new TabPage("Assigned (Open - current)") { BackColor = Color.White, Padding = new Padding(5) };
            dgvOpen = CreateStyledGrid();
            SetupOpenColumns(dgvOpen);
            dgvOpen.CellDoubleClick += HandleTicketGridDoubleClick;
            dgvOpen.ContextMenuStrip = BuildTicketContextMenu(isOpenGrid: true);

            var tpHandled = new TabPage("Handled (Solved/Resolved/Closed)") { BackColor = Color.White, Padding = new Padding(5) };
            dgvHandled = CreateStyledGrid();
            SetupHandledColumns(dgvHandled);
            dgvHandled.CellDoubleClick += HandleTicketGridDoubleClick;
            dgvHandled.ContextMenuStrip = BuildTicketContextMenu(isOpenGrid: false);

            tpOpen.Controls.Add(dgvOpen);
            tpHandled.Controls.Add(dgvHandled);
            
            tabGrids.TabPages.Add(tpOpen);
            tabGrids.TabPages.Add(tpHandled);

            dgvOpen.SelectionChanged += async (_, __) =>
            {
                if (_suppressProfileSelectionChanged)
                    return;
                await QueueTicketProfileUpdateFromGridAsync(dgvOpen);
            };
            dgvHandled.SelectionChanged += async (_, __) =>
            {
                if (_suppressProfileSelectionChanged)
                    return;
                await QueueTicketProfileUpdateFromGridAsync(dgvHandled);
            };
            tabGrids.SelectedIndexChanged += async (_, __) =>
            {
                var grid = tabGrids.SelectedIndex == 0 ? dgvOpen : dgvHandled;
                if (_suppressProfileSelectionChanged)
                    return;
                await QueueTicketProfileUpdateFromGridAsync(grid);
            };

            this.splitGridAndProfile = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                FixedPanel = FixedPanel.Panel2,
                Panel2MinSize = 0,
                Panel1MinSize = 0
            };

            this.splitGridAndProfile.Panel1.Padding = new Padding(0);
            this.splitGridAndProfile.Panel2.Padding = new Padding(10, 0, 0, 0);

            this.splitGridAndProfile.Panel1.Controls.Add(tabGrids);

            this.pnlTicketProfile = CreateCardPanel();
            this.pnlTicketProfile.Dock = DockStyle.Fill;
            this.pnlTicketProfile.Padding = new Padding(14, 12, 14, 12);

            Panel MakeProfileField(string headerText, out Label valueLabel)
            {
                var container = new Panel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(0, 2, 0, 6),
                    Margin = new Padding(0),
                    Tag = "profileField"
                };
                var header = new Label
                {
                    Text = headerText,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = clrTextMuted
                };
                valueLabel = new Label
                {
                    Text = "-",
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 20,
                    AutoEllipsis = false,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = clrTextDark,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Tag = "profileValue"
                };
                container.Controls.Add(valueLabel);
                container.Controls.Add(header);
                return container;
            }

            var profileStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var lblProfileHeader = new Label
            {
                Text = "TICKET PROFILE",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = clrTextMuted,
                Margin = new Padding(0, 0, 0, 8)
            };

            this.lblProfileTicket = new Label
            {
                Text = "Select a ticket",
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = clrTextDark,
                Margin = new Padding(0, 0, 0, 10)
            };

            profileStack.Controls.Add(lblProfileHeader);
            profileStack.Controls.Add(this.lblProfileTicket);
            profileStack.Controls.Add(MakeProfileField("STATUS", out this.lblProfileStatus));
            profileStack.Controls.Add(MakeProfileField("PRIORITY", out this.lblProfilePriority));
            profileStack.Controls.Add(MakeProfileField("LOCATION", out this.lblProfileLocation));
            profileStack.Controls.Add(MakeProfileField("ASSIGNED TO", out this.lblProfileAssignedTo));
            profileStack.Controls.Add(MakeProfileField("CALLER", out this.lblProfileCaller));
            profileStack.Controls.Add(MakeProfileField("UPDATED", out this.lblProfileUpdated));
            profileStack.Controls.Add(MakeProfileField("CREATED", out this.lblProfileCreated));
            profileStack.Controls.Add(MakeProfileField("LAST EMAIL", out this.lblProfileLastEmail));

            void AdjustProfileFieldWrapping()
            {
                if (this.pnlTicketProfile == null || this.pnlTicketProfile.IsDisposed)
                    return;

                // Prefer the stack width; it accounts for right-panel padding and scrollbars.
                var baseWidth = Math.Max(160, profileStack.ClientSize.Width - (profileStack.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
                var valueWidth = Math.Max(120, baseWidth);

                foreach (Control c in profileStack.Controls)
                {
                    if (c is Panel p && string.Equals(p.Tag as string, "profileField", StringComparison.Ordinal))
                    {
                        p.Width = baseWidth;
                        foreach (Control child in p.Controls)
                        {
                            if (child is Label lbl && string.Equals(lbl.Tag as string, "profileValue", StringComparison.Ordinal))
                            {
                                lbl.Width = valueWidth;
                                lbl.MaximumSize = new Size(valueWidth, 0);

                                // Force word-wrapped height calculation so the label doesn't clip.
                                // (WinForms Label tends to remain single-line + clipped unless AutoSize is false and Height is set.)
                                var text = string.IsNullOrWhiteSpace(lbl.Text) ? "-" : lbl.Text;
                                var measured = TextRenderer.MeasureText(
                                    text,
                                    lbl.Font,
                                    new Size(valueWidth, int.MaxValue),
                                    TextFormatFlags.WordBreak);
                                lbl.Height = Math.Max(20, measured.Height);
                            }
                        }
                    }
                }
            }

            profileStack.Layout += (_, __) => AdjustProfileFieldWrapping();
            this.pnlTicketProfile.SizeChanged += (_, __) => AdjustProfileFieldWrapping();
            // Text updates don't always trigger layout; force recalculation when values change.
            foreach (var lbl in new[] { this.lblProfileTicket, this.lblProfileStatus, this.lblProfilePriority, this.lblProfileLocation, this.lblProfileAssignedTo, this.lblProfileCaller, this.lblProfileUpdated, this.lblProfileCreated, this.lblProfileLastEmail })
            {
                if (lbl == null) continue;
                lbl.TextChanged += (_, __) => AdjustProfileFieldWrapping();
            }

            var pnlProfileActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Height = 44,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };

            this.btnProfileOpen = new Button
            {
                Text = "Profile",
                Width = 110,
                Height = 32,
                BackColor = clrPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled = false
            };
            this.btnProfileOpen.FlatAppearance.BorderSize = 0;
            this.btnProfileOpen.Click += (_, __) =>
            {
                if (_profileSelectedTicketId <= 0) return;
                if (_navigateToTicket != null) _navigateToTicket(_profileSelectedTicketId);
                else MessageBox.Show("Ticket navigation is not available from this view.", "Ticket Profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            this.btnProfileSummary = new Button
            {
                Text = "Summary",
                Width = 110,
                Height = 32,
                BackColor = Color.White,
                ForeColor = clrPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled = false
            };
            this.btnProfileSummary.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
            this.btnProfileSummary.Click += async (_, __) => await OpenSelectedTicketSummaryAsync();

            pnlProfileActions.Controls.Add(this.btnProfileOpen);
            pnlProfileActions.Controls.Add(this.btnProfileSummary);

            this.pnlTicketProfile.Controls.Add(profileStack);
            this.pnlTicketProfile.Controls.Add(pnlProfileActions);

            this.splitGridAndProfile.Panel2.Controls.Add(this.pnlTicketProfile);

            // Ensure initial widths are set so long values wrap instead of showing ellipses.
            AdjustProfileFieldWrapping();

            gridContainer.Controls.Add(this.splitGridAndProfile);
            mainLayout.Controls.Add(gridContainer, 0, 2);

            this.Controls.Add(mainLayout);

            // Init Dates
            dtTo.Value = DateTime.Today;
            dtFrom.Value = DateTime.Today.AddDays(-30);
            ApplyQuickRange();

            this.ResumeLayout(false);
        }

        private void OpenProfileCard()
        {
            if (cboEmployee.SelectedItem is CallEmployeeProfileLookup emp)
            {
                using (var dlg = new EmployeeProfileDetailsDialog(emp, _repo))
                {
                    dlg.ShowDialog(this);
                }
            }
            else
            {
                MessageBox.Show("Please select an employee first.", "Profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void EnsureGridProfileSplitLayout(bool force)
        {
            if (this.splitGridAndProfile == null || this.splitGridAndProfile.IsDisposed)
                return;

            if (this.splitGridAndProfile.Orientation != Orientation.Vertical)
                return;

            var total = this.splitGridAndProfile.Width;
            var available = total - this.splitGridAndProfile.SplitterWidth;
            if (available <= 0)
                return;

            var minLeft = GridProfileMinWidth;
            var minRight = GridProfileRightMinWidth;

            if (minLeft + minRight > available)
            {
                minLeft = Math.Max(0, available / 2);
                minRight = Math.Max(0, available - minLeft);
            }

            var maxLeft = Math.Max(0, available - minRight);
            minLeft = Math.Min(minLeft, maxLeft);

            var desiredLeft = available - GridProfileDesiredRightWidth;
            desiredLeft = Math.Max(minLeft, Math.Min(desiredLeft, maxLeft));

            var current = this.splitGridAndProfile.SplitterDistance;
            var outOfRange = current < minLeft || current > maxLeft;
            if (!force && !outOfRange
                && this.splitGridAndProfile.Panel1MinSize == minLeft
                && this.splitGridAndProfile.Panel2MinSize == minRight)
            {
                return;
            }

            try
            {
                var safe = outOfRange || force
                    ? desiredLeft
                    : Math.Max(minLeft, Math.Min(current, maxLeft));

                this.splitGridAndProfile.SplitterDistance = safe;
                this.splitGridAndProfile.Panel1MinSize = minLeft;
                this.splitGridAndProfile.Panel2MinSize = minRight;

                var finalMaxLeft = Math.Max(0, (this.splitGridAndProfile.Width - this.splitGridAndProfile.SplitterWidth) - this.splitGridAndProfile.Panel2MinSize);
                var finalMinLeft = Math.Min(this.splitGridAndProfile.Panel1MinSize, finalMaxLeft);
                var finalDistance = Math.Max(finalMinLeft, Math.Min(this.splitGridAndProfile.SplitterDistance, finalMaxLeft));
                this.splitGridAndProfile.SplitterDistance = finalDistance;
            }
            catch
            {
                // ignore layout exceptions
            }
        }

        private Panel CreateCardPanel()
        {
            var p = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(1) // Simulate border width
            };
            p.Paint += (s, e) =>
            {
                var rect = p.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (var pen = new Pen(Color.FromArgb(229, 231, 235))) // Light gray border
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };
            return p;
        }

        private Label CreateFilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = clrTextMuted,
                Padding = new Padding(0, 8, 0, 0)
            };
        }

        private Panel CreateMetricCard(string title, out Label valLabel, Color accentColor)
        {
            var p = CreateCardPanel(); // Base white card
            p.Margin = new Padding(5, 0, 5, 0); // Spacing between cards
            p.Dock = DockStyle.Fill;

            // Accent Bar
            var accent = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accentColor };
            
            // Content
            var lblTitle = new Label
            {
                Text = title.ToUpper(),
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = clrTextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 25,
                Padding = new Padding(10, 5, 0, 0)
            };

            valLabel = new Label
            {
                Text = "0",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = clrTextDark,
                TextAlign = ContentAlignment.MiddleCenter
            };

            p.Controls.Add(valLabel);
            p.Controls.Add(lblTitle);
            p.Controls.Add(accent);
            return p;
        }

        private static void MakeMetricCardClickable(Control card, Action onClick)
        {
            if (card == null || onClick == null)
                return;

            void wire(Control c)
            {
                c.Cursor = Cursors.Hand;
                c.Click += (_, __) => onClick();
                foreach (Control child in c.Controls)
                    wire(child);
            }

            wire(card);
        }

        private DataGridView CreateStyledGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(241, 245, 249),
            };

            try
            {
                typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dgv, true, null);
            }
            catch
            {
            }

            // Header Style
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 116, 139);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(14, 0, 10, 0);
            dgv.ColumnHeadersHeight = 42;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Row Style
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv.DefaultCellStyle.ForeColor = clrTextDark;
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
            dgv.DefaultCellStyle.SelectionForeColor = clrTextDark;
            dgv.DefaultCellStyle.Padding = new Padding(14, 6, 8, 6);
            dgv.RowTemplate.Height = 44;

            // Alternating row background
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = clrTextDark;

            // Draw a clean bottom border under the column headers
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(100, 116, 139);

            return dgv;
        }

        private void SetupOpenColumns(DataGridView grid)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TicketCode", HeaderText = "TICKET", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Location", HeaderText = "LOCATION", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Issue", HeaderText = "ISSUE", FillWeight = 34 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "STATUS", FillWeight = 11 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Priority", HeaderText = "PRIORITY", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Age", HeaderText = "AGE", FillWeight = 9 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sla", HeaderText = "SLA", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UpdatedAt", HeaderText = "UPDATED", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedAgo", HeaderText = "UPDATED (AGO)", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "CREATED", FillWeight = 12 });
            
            grid.CellFormatting += GridAttributesAdapter_CellFormatting;
        }

        private void SetupHandledColumns(DataGridView grid)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TicketCode", HeaderText = "TICKET", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Location", HeaderText = "LOCATION", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Issue", HeaderText = "ISSUE", FillWeight = 28 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "STATUS", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Priority", HeaderText = "PRIORITY", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AssignedToName", HeaderText = "ASSIGNED TO", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompletedBy", HeaderText = "COMPLETED BY", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompletedAtUtc", HeaderText = "COMPLETED", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CompletedAgo", HeaderText = "COMPLETED (AGO)", FillWeight = 12 });

            grid.CellFormatting += GridAttributesAdapter_CellFormatting;
        }

        private sealed class GridRowFormatCache
        {
            public long ComputedAtUtcTicks;
            public string Age;
            public string UpdatedAgo;
            public string Sla;
            public string CompletedAgo;
        }

        private static readonly long GridRowFormatCacheTtlTicks = TimeSpan.FromSeconds(30).Ticks;

        private static GridRowFormatCache TryGetRowFormatCache(DataGridViewRow row, DateTime nowUtc)
        {
            if (row == null)
                return null;

            var existing = row.Tag as GridRowFormatCache;
            if (existing != null)
            {
                if ((nowUtc.Ticks - existing.ComputedAtUtcTicks) <= GridRowFormatCacheTtlTicks)
                    return existing;

                // Stale cache: reset.
                existing.ComputedAtUtcTicks = nowUtc.Ticks;
                existing.Age = null;
                existing.UpdatedAgo = null;
                existing.Sla = null;
                existing.CompletedAgo = null;
                return existing;
            }

            // Don't clobber other Tag usages.
            if (row.Tag != null)
                return null;

            var cache = new GridRowFormatCache { ComputedAtUtcTicks = nowUtc.Ticks };
            row.Tag = cache;
            return cache;
        }

        // Shared Cell Formatting Logic
        private void GridAttributesAdapter_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var grid = (DataGridView)sender;
            var prop = grid.Columns[e.ColumnIndex].DataPropertyName;
            var name = grid.Columns[e.ColumnIndex].Name;
            var row = grid.Rows[e.RowIndex];
            var nowUtc = DateTime.UtcNow;
            var cache = TryGetRowFormatCache(row, nowUtc);

            if (name == "Age")
            {
                if (row.DataBoundItem is CallTechOpenTicketRow open)
                {
                    var v = cache?.Age;
                    if (v == null)
                    {
                        var age = nowUtc - open.CreatedAt;
                        if (age < TimeSpan.Zero) age = age.Duration();
                        v = age.TotalDays >= 1 ? $"{(int)Math.Floor(age.TotalDays)}d" : $"{Math.Max(1, (int)Math.Round(age.TotalHours))}h";
                        if (cache != null) cache.Age = v;
                    }

                    e.Value = v;
                    e.FormattingApplied = true;
                }
            }
            else if (name == "UpdatedAgo")
            {
                if (row.DataBoundItem is CallTechOpenTicketRow open)
                {
                    var v = cache?.UpdatedAgo;
                    if (v == null)
                    {
                        v = FormatAgo(open.UpdatedAt, nowUtc);
                        if (cache != null) cache.UpdatedAgo = v;
                    }

                    e.Value = v;
                    e.FormattingApplied = true;
                }
            }
            else if (name == "Sla")
            {
                if (row.DataBoundItem is CallTechOpenTicketRow open)
                {
                    var v = cache?.Sla;
                    if (v == null)
                    {
                        v = BuildSlaShortLabel(open.Priority, open.CreatedAt, open.Status, nowUtc);
                        if (cache != null) cache.Sla = v;
                    }

                    e.Value = v;
                    e.FormattingApplied = true;
                }
            }
            else if (name == "CompletedAgo")
            {
                if (row.DataBoundItem is CallTechHandledTicketRow handled && handled.CompletedAtUtc.HasValue)
                {
                    var v = cache?.CompletedAgo;
                    if (v == null)
                    {
                        v = FormatAgo(handled.CompletedAtUtc.Value, nowUtc);
                        if (cache != null) cache.CompletedAgo = v;
                    }

                    e.Value = v;
                    e.FormattingApplied = true;
                }
            }
            else if ((prop == "CreatedAt" || prop == "UpdatedAt") && e.Value is DateTime dt)
            {
                 // DB values come from SYSUTCDATETIME(); treat unspecified as UTC and convert for display.
                 var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                 e.Value = utc.ToLocalTime().ToString("g");
                 e.FormattingApplied = true;
            }
            else if (prop == "CompletedAtUtc" && e.Value is DateTime dtUtc)
            {
                 var utc = dtUtc.Kind == DateTimeKind.Utc ? dtUtc : DateTime.SpecifyKind(dtUtc, DateTimeKind.Utc);
                 e.Value = utc.ToLocalTime().ToString("g");
                 e.FormattingApplied = true;
            }
            else if (prop == "Priority" && e.Value != null)
            {
                var s = e.Value.ToString();
                if (s == "Critical" || s == "High") e.CellStyle.ForeColor = clrAccentEscalated;
                else if (s == "Medium") e.CellStyle.ForeColor = Color.FromArgb(245, 158, 11);
                else if (s == "Low") e.CellStyle.ForeColor = Color.FromArgb(100, 116, 139);
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                e.FormattingApplied = false;
            }
            else if (prop == "Status" && e.Value != null)
            {
                var s = e.Value.ToString().Trim();
                if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                    e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                else if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                    e.CellStyle.ForeColor = Color.FromArgb(20, 184, 166);
                else if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase))
                    e.CellStyle.ForeColor = clrAccentEscalated;
                else if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase))
                    e.CellStyle.ForeColor = Color.FromArgb(124, 58, 237);
                else if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    e.CellStyle.ForeColor = Color.FromArgb(37, 99, 235);
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                e.FormattingApplied = false;
            }
            else if (prop == "TicketCode" && e.Value != null)
            {
                e.CellStyle.ForeColor = Color.FromArgb(2, 132, 199);
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                e.FormattingApplied = false;
            }
        }

        private static string FormatAgo(DateTime utc)
        {
            return FormatAgo(utc, DateTime.UtcNow);
        }

        private static string FormatAgo(DateTime utc, DateTime nowUtc)
        {
            var diff = nowUtc - utc;
            if (diff < TimeSpan.Zero) diff = diff.Duration();

            if (diff.TotalDays >= 1) return $"{(int)Math.Floor(diff.TotalDays)}d ago";
            if (diff.TotalHours >= 1) return $"{(int)Math.Floor(diff.TotalHours)}h ago";
            return $"{Math.Max(1, (int)Math.Round(diff.TotalMinutes))}m ago";
        }

        private static string BuildSlaShortLabel(string priority, DateTime createdAtUtcUnspecified, string status)
        {
            return BuildSlaShortLabel(priority, createdAtUtcUnspecified, status, DateTime.UtcNow);
        }

        private static string BuildSlaShortLabel(string priority, DateTime createdAtUtcUnspecified, string status, DateTime nowUtc)
        {
            var isFinal = string.Equals((status ?? string.Empty).Trim(), "Solved", StringComparison.OrdinalIgnoreCase)
                          || string.Equals((status ?? string.Empty).Trim(), "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                          || string.Equals((status ?? string.Empty).Trim(), "Closed", StringComparison.OrdinalIgnoreCase);
            if (isFinal)
                return "Closed";

            var targetHours = GetResolutionSlaTargetHours(priority);
            if (targetHours <= 0)
                return "-";

            var createdUtc = createdAtUtcUnspecified.Kind == DateTimeKind.Utc
                ? createdAtUtcUnspecified
                : DateTime.SpecifyKind(createdAtUtcUnspecified, DateTimeKind.Utc);
            var dueUtc = createdUtc.AddHours(targetHours);
            var remaining = dueUtc - nowUtc;

            if (remaining <= TimeSpan.Zero)
            {
                var over = remaining.Duration();
                return over.TotalDays >= 1 ? $"Breached {over.Days}d" : $"Breached {Math.Max(1, (int)Math.Round(over.TotalHours))}h";
            }

            return remaining.TotalDays >= 1 ? $"Due {Math.Max(0, (int)Math.Floor(remaining.TotalDays))}d" : $"Due {Math.Max(1, (int)Math.Round(remaining.TotalHours))}h";
        }

        private async Task QueueTicketProfileUpdateFromGridAsync(DataGridView grid)
        {
            // SelectionChanged can fire rapidly while the user scrolls; debounce to avoid
            // launching a DB lookup per row.
            CancellationTokenSource cts = null;
            CancellationToken token = default(CancellationToken);

            try
            {
                cts = new CancellationTokenSource();
                token = cts.Token;

                var prev = Interlocked.Exchange(ref _profileUpdateCts, cts);
                if (prev != null)
                {
                    try { prev.Cancel(); } catch { }
                    try { prev.Dispose(); } catch { }
                }

                await Task.Delay(150, token);
                if (token.IsCancellationRequested)
                    return;

                if (_suppressProfileSelectionChanged)
                    return;

                await UpdateTicketProfileFromGridAsync(grid);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private async Task UpdateTicketProfileFromGridAsync(DataGridView grid)
        {
            if (this.pnlTicketProfile == null)
                return;

            if (grid == null)
            {
                SetTicketProfileEmpty("Select a ticket");
                return;
            }

            var row = grid.CurrentRow;
            var item = row?.DataBoundItem;
            if (item == null)
            {
                SetTicketProfileEmpty("Select a ticket");
                return;
            }

            var ticketId = 0;
            var ticketCode = string.Empty;
            var status = string.Empty;
            var priority = string.Empty;
            var location = string.Empty;
            DateTime? createdAt = null;
            DateTime? updatedAt = null;
            string assignedTo = "-";
            string caller = "-";

            if (item is CallTechOpenTicketRow open)
            {
                ticketId = open.TicketId;
                ticketCode = open.TicketCode ?? string.Empty;
                status = open.Status ?? string.Empty;
                priority = open.Priority ?? string.Empty;
                location = open.Location ?? string.Empty;
                createdAt = open.CreatedAt;
                updatedAt = open.UpdatedAt;
            }
            else if (item is CallTechHandledTicketRow handled)
            {
                ticketId = handled.TicketId;
                ticketCode = handled.TicketCode ?? string.Empty;
                status = handled.Status ?? string.Empty;
                priority = handled.Priority ?? string.Empty;
                location = handled.Location ?? string.Empty;
                assignedTo = string.IsNullOrWhiteSpace(handled.AssignedToName) ? "-" : handled.AssignedToName.Trim();

                if (handled.CompletedAtUtc.HasValue)
                {
                    updatedAt = DateTime.SpecifyKind(handled.CompletedAtUtc.Value, DateTimeKind.Utc);
                }
            }

            if (ticketId <= 0)
            {
                SetTicketProfileEmpty("Select a ticket");
                return;
            }

            _profileSelectedTicketId = ticketId;
            var version = Interlocked.Increment(ref _profileSelectionVersion);

            if (this.lblProfileTicket != null)
                this.lblProfileTicket.Text = string.IsNullOrWhiteSpace(ticketCode) ? $"Ticket #{ticketId}" : ticketCode.Trim();

            if (this.lblProfileStatus != null)
            {
                this.lblProfileStatus.Text = string.IsNullOrWhiteSpace(status) ? "-" : status.Trim();
                this.lblProfileStatus.ForeColor = GetStatusColor(status);
            }

            if (this.lblProfilePriority != null)
            {
                this.lblProfilePriority.Text = string.IsNullOrWhiteSpace(priority) ? "-" : priority.Trim();
                this.lblProfilePriority.ForeColor = GetPriorityColor(priority);
            }

            if (this.lblProfileLocation != null)
                this.lblProfileLocation.Text = string.IsNullOrWhiteSpace(location) ? "-" : location.Trim();

            if (this.lblProfileAssignedTo != null)
                this.lblProfileAssignedTo.Text = assignedTo;

            if (this.lblProfileCaller != null)
                this.lblProfileCaller.Text = caller;

            if (this.lblProfileUpdated != null)
                this.lblProfileUpdated.Text = updatedAt.HasValue ? ToLocalString(updatedAt.Value, "g") : "-";

            if (this.lblProfileCreated != null)
                this.lblProfileCreated.Text = createdAt.HasValue ? ToLocalString(createdAt.Value, "g") : "-";

            if (this.lblProfileLastEmail != null)
                this.lblProfileLastEmail.Text = "Loading...";

            if (this.btnProfileOpen != null) this.btnProfileOpen.Enabled = true;
            if (this.btnProfileSummary != null) this.btnProfileSummary.Enabled = true;

            try
            {
                var nowUtc = DateTime.UtcNow;

                CallTicketListItem full = null;
                CallEmailLogItem lastEmail = null;

                Task<CallTicketListItem> fullTask = null;
                Task<CallEmailLogItem> emailTask = null;

                if (_ticketCache.TryGetValue(ticketId, out var cachedTicket) && (nowUtc - cachedTicket.FetchedUtc) <= ProfileCacheTtl)
                    full = cachedTicket.Ticket;
                else
                    fullTask = _repo.GetTicketByIdAsync(ticketId);

                if (_emailCache.TryGetValue(ticketId, out var cachedEmail) && (nowUtc - cachedEmail.FetchedUtc) <= ProfileCacheTtl)
                    lastEmail = cachedEmail.Email;
                else
                    emailTask = _repo.GetLastEmailLogForTicketAsync(ticketId);

                if (fullTask != null && emailTask != null)
                    await Task.WhenAll(fullTask, emailTask);
                else if (fullTask != null)
                    full = await fullTask;
                else if (emailTask != null)
                    lastEmail = await emailTask;

                if (version != _profileSelectionVersion)
                    return;

                if (full == null && fullTask != null)
                    full = fullTask.Result;
                if (lastEmail == null && emailTask != null)
                    lastEmail = emailTask.Result;

                if (fullTask != null && full != null)
                    _ticketCache[ticketId] = (full, nowUtc);
                if (emailTask != null)
                    _emailCache[ticketId] = (lastEmail, nowUtc);

                if (full != null)
                {
                    if (this.lblProfileAssignedTo != null)
                        this.lblProfileAssignedTo.Text = string.IsNullOrWhiteSpace(full.ResponsiblePerson) ? "(Unassigned)" : full.ResponsiblePerson.Trim();

                    if (this.lblProfileCaller != null)
                        this.lblProfileCaller.Text = string.IsNullOrWhiteSpace(full.CallerName) ? "-" : full.CallerName.Trim();

                    if (this.lblProfileUpdated != null)
                        this.lblProfileUpdated.Text = ToLocalString(GetLastActivityUtc(full, out _), "g");

                    if (this.lblProfileCreated != null)
                        this.lblProfileCreated.Text = ToLocalString(full.CreatedAt, "g");
                }

                if (this.lblProfileLastEmail != null)
                {
                    if (lastEmail == null)
                    {
                        this.lblProfileLastEmail.Text = "None";
                    }
                    else
                    {
                        var when = ToLocalString(lastEmail.DateSent, "g");
                        var type = string.IsNullOrWhiteSpace(lastEmail.EmailType) ? "-" : lastEmail.EmailType.Trim();
                        this.lblProfileLastEmail.Text = $"{type} • {when}";
                    }
                }
            }
            catch
            {
                if (version != _profileSelectionVersion)
                    return;

                if (this.lblProfileLastEmail != null && this.lblProfileLastEmail.Text == "Loading...")
                    this.lblProfileLastEmail.Text = "-";
            }
        }

        private void SetTicketProfileEmpty(string title)
        {
            _profileSelectedTicketId = 0;
            Interlocked.Increment(ref _profileSelectionVersion);

            if (this.lblProfileTicket != null) this.lblProfileTicket.Text = title ?? "Select a ticket";
            if (this.lblProfileStatus != null) this.lblProfileStatus.Text = "-";
            if (this.lblProfilePriority != null) this.lblProfilePriority.Text = "-";
            if (this.lblProfileLocation != null) this.lblProfileLocation.Text = "-";
            if (this.lblProfileAssignedTo != null) this.lblProfileAssignedTo.Text = "-";
            if (this.lblProfileCaller != null) this.lblProfileCaller.Text = "-";
            if (this.lblProfileUpdated != null) this.lblProfileUpdated.Text = "-";
            if (this.lblProfileCreated != null) this.lblProfileCreated.Text = "-";
            if (this.lblProfileLastEmail != null) this.lblProfileLastEmail.Text = "-";
            if (this.btnProfileOpen != null) this.btnProfileOpen.Enabled = false;
            if (this.btnProfileSummary != null) this.btnProfileSummary.Enabled = false;
        }

        private async Task OpenSelectedTicketSummaryAsync()
        {
            var ticketId = _profileSelectedTicketId;
            if (ticketId <= 0)
                return;

            try
            {
                var ticket = await _repo.GetTicketByIdAsync(ticketId);
                if (ticket == null)
                {
                    MessageBox.Show("Unable to load ticket details for summary.", "Ticket Summary", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var settings = await _repo.GetEscalationSettingsAsync();
                var escalationOverride = await _repo.GetTicketEscalationOverrideAsync(ticketId);

                var slaLabel = BuildSlaShortLabel(ticket.Priority, ticket.CreatedAt, ticket.Status);
                var slaTooltip = BuildSlaTooltip(ticket);
                var escalationText = BuildEscalationLabel(ticket, settings, escalationOverride);

                var lastActivityUtc = GetLastActivityUtc(ticket, out var lastActivitySource);
                var idle = DateTime.UtcNow - lastActivityUtc;
                if (idle < TimeSpan.Zero) idle = idle.Duration();

                var lastActivityText = $"{ToLocalString(lastActivityUtc, "g")} ({lastActivitySource})";
                var idleText = FormatShortDuration(idle);

                using (var dlg = new TicketSummaryDialog(
                    ticket: ticket,
                    slaLabel: slaLabel,
                    slaTooltip: slaTooltip,
                    escalationText: escalationText,
                    lastActivityText: lastActivityText,
                    idleText: idleText))
                {
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ticket Summary", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildSlaTooltip(CallTicketListItem t)
        {
            if (t == null)
                return "SLA: -";

            var isFinal = IsFinalStatus(t.Status);
            var targetHours = GetResolutionSlaTargetHours(t.Priority);
            if (targetHours <= 0)
                return "SLA: -";

            var createdUtc = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc);
            var dueUtc = createdUtc.AddHours(targetHours);
            var remaining = dueUtc - DateTime.UtcNow;

            var dueLocal = dueUtc.ToLocalTime();
            if (isFinal)
                return $"SLA: (closed) • due {dueLocal:yyyy-MM-dd HH:mm}";

            if (remaining <= TimeSpan.Zero)
            {
                var overdue = remaining.Duration();
                return $"SLA: Breached by {FormatShortDuration(overdue)} • due {dueLocal:yyyy-MM-dd HH:mm}";
            }

            return $"SLA: Due in {FormatShortDuration(remaining)} • due {dueLocal:yyyy-MM-dd HH:mm}";
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
                var v = t.LastContactAt.Value;
                return v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc);
            }

            source = "UpdatedAt";
            var u = t.UpdatedAt;
            return u.Kind == DateTimeKind.Utc ? u : DateTime.SpecifyKind(u, DateTimeKind.Utc);
        }

        private static string FormatShortDuration(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                value = value.Duration();

            var days = value.Days;
            var hours = value.Hours;
            var minutes = value.Minutes;

            if (days > 0)
                return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
            if (hours > 0)
                return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
            return $"{Math.Max(1, (int)Math.Round(value.TotalMinutes))}m";
        }

        private static string ToLocalString(DateTime value, string format)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString(string.IsNullOrWhiteSpace(format) ? "g" : format);
        }

        private string BuildEscalationLabel(CallTicketListItem t, CallEscalationSettingsItem settings, CallTicketEscalationOverrideItem ovr)
        {
            if (t == null)
                return "-";

            var ageDays = Math.Max(0, t.TicketAgeDays);

            var supDays = ovr != null ? ovr.DaysToSupervisor : (settings?.DaysToSupervisor ?? 2);
            var mgrDays = ovr != null ? ovr.DaysToManager : (settings?.DaysToManager ?? 3);

            if (supDays < 1) supDays = 1;
            if (mgrDays < supDays) mgrDays = supDays;

            if (ageDays >= mgrDays)
            {
                var overBy = ageDays - mgrDays;
                return overBy > 0 ? $"Escalated: Manager • {overBy}d past threshold" : "Escalated: Manager";
            }

            if (ageDays >= supDays)
            {
                var mgrIn = mgrDays - ageDays;
                return mgrIn > 0 ? $"Escalated: Supervisor • manager in {mgrIn}d" : "Escalated: Supervisor";
            }

            var supInDays = supDays - ageDays;
            var mgrInDays = mgrDays - ageDays;
            var isNear = supInDays <= 1;
            var prefix = isNear ? "Warning" : "Next";
            var suffix = ovr != null ? " (override)" : string.Empty;
            return $"{prefix}: sup in {supInDays}d, mgr in {mgrInDays}d{suffix}";
        }

        private Color GetStatusColor(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return clrAccentPending;
            if (s.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return clrAccentEscalated;
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(124, 58, 237);
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return clrPrimary;
            if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(99, 102, 241);
            if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase)) return clrAccentSolved;
            if (s.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return clrAccentSolved;
            if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return clrAccentEscalated;
            return clrTextDark;
        }

        private Color GetPriorityColor(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return clrAccentEscalated;
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return clrAccentPending;
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return clrPrimary;
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return clrTextMuted;
            return clrTextDark;
        }

        private static int GetResolutionSlaTargetHours(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return 24;
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return 48;
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return 72;
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return 120;
            return 72;
        }

        private async Task LoadEmployeesAsync()
        {
            _employees = await _repo.GetItEmployeeProfilesAsync(_itDepartmentName);

            cboEmployee.BeginUpdate();
            cboEmployee.DataSource = null;
            cboEmployee.Items.Clear();

            var ordered = (_employees ?? new List<CallEmployeeProfileLookup>())
                .Where(x => x != null && x.EmpId > 0)
                .OrderBy(x => x.EmployeeName)
                .ToList();

            cboEmployee.DataSource = ordered;
            cboEmployee.EndUpdate();

            if (cboEmployee.Items.Count > 0)
                cboEmployee.SelectedIndex = 0;
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                _departments = await _repo.GetDepartmentsAsync();
            }
            catch
            {
                _departments = new List<LookupItem>();
            }

            var items = new List<LookupItem>
            {
                new LookupItem { Id = 0, Name = "All Departments" }
            };
            items.AddRange((_departments ?? new List<LookupItem>()).Where(d => d != null).OrderBy(d => d.Name));

            cboDepartmentFilter.BeginUpdate();
            cboDepartmentFilter.DataSource = null;
            cboDepartmentFilter.Items.Clear();
            cboDepartmentFilter.DisplayMember = "Name";
            cboDepartmentFilter.ValueMember = "Id";
            cboDepartmentFilter.DataSource = items;
            cboDepartmentFilter.EndUpdate();

            if (cboDepartmentFilter.Items.Count > 0)
                cboDepartmentFilter.SelectedIndex = 0;
        }

        private async Task RefreshAsync()
        {
            try
            {
                btnRefresh.Enabled = false;
                lblStatus.Text = "Loading...";

                if (!await _repo.CallSchemaExistsAsync())
                {
                    lblStatus.Text = "Call Monitoring schema not installed.";
                    dgvOpen.DataSource = null;
                    dgvHandled.DataSource = null;
                    return;
                }

                var emp = cboEmployee.SelectedItem as CallEmployeeProfileLookup;
                if (emp == null || emp.EmpId <= 0)
                {
                    lblStatus.Text = "Select an employee.";
                    dgvOpen.DataSource = null;
                    dgvHandled.DataSource = null;
                    return;
                }

                if (dtFrom.Value.Date > dtTo.Value.Date)
                {
                    MessageBox.Show("'From' date must be earlier than or equal to 'To' date.", "Call Monitoring Profiles",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "Invalid date range";
                    return;
                }

                var fromLocal = DateTime.SpecifyKind(dtFrom.Value.Date, DateTimeKind.Local);
                var toLocal = DateTime.SpecifyKind(dtTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
                var fromUtc = fromLocal.ToUniversalTime();
                var toUtc = toLocal.ToUniversalTime();

                var summaryTask = _repo.GetTechProfileSummaryAsync(emp.EmpId, fromUtc, toUtc);
                var openTicketsTask = _repo.GetOpenTicketsAssignedToEmployeeAsync(emp.EmpId, maxRows: 300);
                var handledTask = _repo.GetTechHandledTicketsAsync(emp.EmpId, fromUtc, toUtc, maxRows: 300);
                await Task.WhenAll(summaryTask, openTicketsTask, handledTask);

                var summary = summaryTask.Result;
                var openTickets = openTicketsTask.Result;
                var rows = handledTask.Result;

                // Update Metrics
                lblValOpen.Text = summary.AssignedOpenTotal.ToString();
                lblValPending.Text = summary.AssignedPending.ToString();
                lblValInProgress.Text = summary.AssignedInProgress.ToString();
                lblValEscalated.Text = summary.AssignedEscalated.ToString();
                lblValHandledAssigned.Text = summary.HandledAsAssigneeSolvedClosed.ToString();
                lblValHandledSolved.Text = summary.HandledAsSolverSolvedClosed.ToString();

                _openTicketsAll = openTickets ?? new List<CallTechOpenTicketRow>();
                _handledTicketsAll = rows ?? new List<CallTechHandledTicketRow>();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error";
                MessageBox.Show(ex.Message, "Call Monitoring Profiles", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void ApplyQuickRange()
        {
            try
            {
                var now = DateTime.Today;
                var choice = cboQuickRange?.SelectedItem?.ToString() ?? string.Empty;

                if (choice == "Today")
                {
                    dtFrom.Value = now;
                    dtTo.Value = now;
                }
                else if (choice == "Last 7 days")
                {
                    dtFrom.Value = now.AddDays(-6);
                    dtTo.Value = now;
                }
                else if (choice == "This month")
                {
                    dtFrom.Value = new DateTime(now.Year, now.Month, 1);
                    dtTo.Value = now;
                }
                else if (choice == "This year")
                {
                    dtFrom.Value = new DateTime(now.Year, 1, 1);
                    dtTo.Value = now;
                }
                else
                {
                    dtFrom.Value = now.AddDays(-30);
                    dtTo.Value = now;
                }
            }
            catch
            {
            }
        }

        private void ClearFilters()
        {
            try
            {
                cboDepartmentFilter.SelectedIndex = 0;
                cboLocationModeFilter.SelectedIndex = 0;
                cboPriorityFilter.SelectedIndex = 0;
                cboStatusFilter.SelectedIndex = 0;
                _handledRoleFilter = HandledRoleFilter.All;
                txtSearch.Text = string.Empty;
                ApplyFilters();
            }
            catch
            {
            }
        }

        private void ApplyCardFilter(string openStatus, HandledRoleFilter? handledRole)
        {
            // Prevent grid tab-switch + rebind from triggering multiple profile DB loads.
            _suppressProfileSelectionChanged = true;

            if (!string.IsNullOrWhiteSpace(openStatus))
            {
                tabGrids.SelectedIndex = 0;
                var idx = cboStatusFilter.Items.IndexOf(openStatus);
                cboStatusFilter.SelectedIndex = idx >= 0 ? idx : 0;
                _handledRoleFilter = HandledRoleFilter.All;
            }
            else if (handledRole.HasValue)
            {
                tabGrids.SelectedIndex = 1;
                cboStatusFilter.SelectedIndex = 0;
                _handledRoleFilter = handledRole.Value;
            }
            else
            {
                cboStatusFilter.SelectedIndex = 0;
                _handledRoleFilter = HandledRoleFilter.All;
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _ = ApplyFiltersAsync();
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesOpenQuery(CallTechOpenTicketRow t, string query)
        {
            return ContainsIgnoreCase(t.TicketCode, query)
                   || ContainsIgnoreCase(t.Issue, query)
                   || ContainsIgnoreCase(t.Department, query)
                   || ContainsIgnoreCase(t.Branch, query)
                   || ContainsIgnoreCase(t.Status, query)
                   || ContainsIgnoreCase(t.Priority, query);
        }

        private static bool MatchesHandledQuery(CallTechHandledTicketRow t, string query)
        {
            return ContainsIgnoreCase(t.TicketCode, query)
                   || ContainsIgnoreCase(t.Issue, query)
                   || ContainsIgnoreCase(t.Department, query)
                   || ContainsIgnoreCase(t.Branch, query)
                   || ContainsIgnoreCase(t.Status, query)
                   || ContainsIgnoreCase(t.Priority, query)
                   || ContainsIgnoreCase(t.AssignedToName, query)
                   || ContainsIgnoreCase(t.CompletedBy, query);
        }

        private async Task ApplyFiltersAsync()
        {
            // ApplyFilters can be triggered rapidly (metric cards, Enter key, Clear).
            // Run the filtering work off the UI thread and cancel any in-flight run.
            CancellationTokenSource cts = null;
            CancellationToken token = default(CancellationToken);
            var version = 0;

            try
            {
                UpdateDepartmentFilterEnabledState();

                var locationMode = cboLocationModeFilter?.SelectedIndex ?? 0; // 0=All, 1=Dept only, 2=Branch only

                var departmentName = (cboDepartmentFilter?.SelectedItem as LookupItem)?.Name;
                if (string.Equals(departmentName, "All Departments", StringComparison.OrdinalIgnoreCase))
                    departmentName = null;
                departmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName.Trim();

                if (locationMode == 2)
                    departmentName = null;

                var priority = cboPriorityFilter?.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(priority) && priority.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                    priority = null;
                priority = string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();

                var openStatus = cboStatusFilter?.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(openStatus) && openStatus.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                    openStatus = null;
                openStatus = string.IsNullOrWhiteSpace(openStatus) ? null : openStatus.Trim();

                var handledRole = _handledRoleFilter;

                var q = (txtSearch?.Text ?? string.Empty).Trim();
                var hasQuery = !string.IsNullOrWhiteSpace(q);
                var query = hasQuery ? q : null;

                var emp = cboEmployee?.SelectedItem as CallEmployeeProfileLookup;
                var empId = emp?.EmpId ?? 0;
                var empUserId = emp?.UserId;

                var openAll = _openTicketsAll ?? new List<CallTechOpenTicketRow>();
                var handledAll = _handledTicketsAll ?? new List<CallTechHandledTicketRow>();

                cts = new CancellationTokenSource();
                token = cts.Token;

                var prev = Interlocked.Exchange(ref _applyFiltersCts, cts);
                if (prev != null)
                {
                    try { prev.Cancel(); } catch { }
                    try { prev.Dispose(); } catch { }
                }

                version = Interlocked.Increment(ref _applyFiltersVersion);

                if (lblStatus != null)
                    lblStatus.Text = "Filtering...";

                UseWaitCursor = true;

                var result = await Task.Run(() =>
                {
                    var openFiltered = new List<CallTechOpenTicketRow>(Math.Min(256, openAll.Count));
                    for (var i = 0; i < openAll.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var t = openAll[i];
                        if (t == null) continue;

                        var hasDept = !string.IsNullOrWhiteSpace(t.Department);
                        var hasBranch = !string.IsNullOrWhiteSpace(t.Branch);
                        if (locationMode == 1 && !hasDept)
                            continue;
                        if (locationMode == 2 && (hasDept || !hasBranch))
                            continue;

                        if (!string.IsNullOrWhiteSpace(departmentName) &&
                            !string.Equals((t.Department ?? string.Empty).Trim(), departmentName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrWhiteSpace(priority) &&
                            !string.Equals((t.Priority ?? string.Empty).Trim(), priority, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrWhiteSpace(openStatus) &&
                            !string.Equals((t.Status ?? string.Empty).Trim(), openStatus, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (query != null && !MatchesOpenQuery(t, query))
                            continue;

                        openFiltered.Add(t);
                    }

                    var handledFiltered = new List<CallTechHandledTicketRow>(Math.Min(256, handledAll.Count));
                    for (var i = 0; i < handledAll.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var t = handledAll[i];
                        if (t == null) continue;

                        var hasDept = !string.IsNullOrWhiteSpace(t.Department);
                        var hasBranch = !string.IsNullOrWhiteSpace(t.Branch);
                        if (locationMode == 1 && !hasDept)
                            continue;
                        if (locationMode == 2 && (hasDept || !hasBranch))
                            continue;

                        if (!string.IsNullOrWhiteSpace(departmentName) &&
                            !string.Equals((t.Department ?? string.Empty).Trim(), departmentName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrWhiteSpace(priority) &&
                            !string.Equals((t.Priority ?? string.Empty).Trim(), priority, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (handledRole == HandledRoleFilter.Assignee && empId > 0 && t.AssignedToEmpId != empId)
                            continue;
                        if (handledRole == HandledRoleFilter.Solver && empUserId.HasValue && t.CompletedByUserId != empUserId.Value)
                            continue;
                        if (query != null && !MatchesHandledQuery(t, query))
                            continue;

                        handledFiltered.Add(t);
                    }

                    var parts = new List<string> { $"{openFiltered.Count} Open", $"{handledFiltered.Count} Handled" };
                    if (!string.IsNullOrWhiteSpace(openStatus)) parts.Add($"Status={openStatus}");
                    if (!string.IsNullOrWhiteSpace(departmentName)) parts.Add($"Dept={departmentName}");
                    if (locationMode == 1) parts.Add("Show=Dept");
                    else if (locationMode == 2) parts.Add("Show=Branch");
                    if (!string.IsNullOrWhiteSpace(priority)) parts.Add($"Priority={priority}");
                    if (handledRole != HandledRoleFilter.All) parts.Add(handledRole == HandledRoleFilter.Assignee ? "Handled=Assignee" : "Handled=Solver");
                    if (hasQuery) parts.Add($"Search=\"{q}\"");

                    return (Open: openFiltered, Handled: handledFiltered, StatusText: string.Join(" | ", parts));
                }, token).ConfigureAwait(true);

                if (token.IsCancellationRequested || version != _applyFiltersVersion)
                    return;

                _openTicketsFiltered = result.Open;
                _handledTicketsFiltered = result.Handled;

                _suppressProfileSelectionChanged = true;
                try
                {
                    BindActiveGrid();
                    SetTicketProfileEmpty("Select a ticket");
                }
                finally
                {
                    // Release suppression without auto-loading a profile row.
                    // Loading the right-panel profile can trigger extra DB calls and make tab/card switches feel laggy.
                    _suppressProfileSelectionChanged = false;
                }

                if (lblStatus != null)
                    lblStatus.Text = result.StatusText;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                if (version == _applyFiltersVersion)
                    UseWaitCursor = false;
            }
        }

        private void BindActiveGrid()
        {
            try
            {
                var prevSuppress = _suppressProfileSelectionChanged;
                _suppressProfileSelectionChanged = true;
                try
                {
                    if (dgvOpen != null)
                    {
                        var openSource = _openTicketsFiltered ?? new List<CallTechOpenTicketRow>();
                        if (!ReferenceEquals(dgvOpen.DataSource, openSource))
                        {
                            dgvOpen.SuspendLayout();
                            try
                            {
                                dgvOpen.DataSource = openSource;
                            }
                            finally
                            {
                                dgvOpen.ResumeLayout();
                            }
                        }

                        try
                        {
                            dgvOpen.ClearSelection();
                            dgvOpen.CurrentCell = null;
                        }
                        catch
                        {
                        }
                    }

                    if (dgvHandled != null)
                    {
                        var handledSource = _handledTicketsFiltered ?? new List<CallTechHandledTicketRow>();
                        if (!ReferenceEquals(dgvHandled.DataSource, handledSource))
                        {
                            dgvHandled.SuspendLayout();
                            try
                            {
                                dgvHandled.DataSource = handledSource;
                            }
                            finally
                            {
                                dgvHandled.ResumeLayout();
                            }
                        }

                        try
                        {
                            dgvHandled.ClearSelection();
                            dgvHandled.CurrentCell = null;
                        }
                        catch
                        {
                        }
                    }
                }
                finally
                {
                    _suppressProfileSelectionChanged = prevSuppress;
                }
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var cts = Interlocked.Exchange(ref _applyFiltersCts, null);
                if (cts != null)
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
                }

                var profileCts = Interlocked.Exchange(ref _profileUpdateCts, null);
                if (profileCts != null)
                {
                    try { profileCts.Cancel(); } catch { }
                    try { profileCts.Dispose(); } catch { }
                }
            }

            base.Dispose(disposing);
        }

        private async Task OpenAutoEscalationAssigneeDialogAsync()
        {
            try
            {
                if (!AppSession.IsLoggedIn)
                {
                    MessageBox.Show("You must be logged in.", "Call Monitoring Profiles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                if (userId == null)
                {
                    MessageBox.Show("No current user ID found.", "Call Monitoring Profiles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!await _repo.AutoEscalationAssigneeSettingsEnabledAsync())
                {
                    MessageBox.Show(
                        "Auto escalation assignee settings are not installed in this database yet.\r\nRun the DB script for CallAutoEscalationAssigneeSettings first.",
                        "Escalation Assignee",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var settings = await _repo.GetEscalationSettingsAsync();
                var supPos = (settings?.SupervisorPosition ?? "IT Supervisor").Trim();

                var candidates = (_employees ?? new List<CallEmployeeProfileLookup>())
                    .Where(x => x != null && x.EmpId > 0)
                    .ToList();

                var supervisors = candidates
                    .Where(x => string.Equals((x.Position ?? string.Empty).Trim(), supPos, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (supervisors.Count == 0)
                {
                    supervisors = candidates
                        .Where(x => (x.Position ?? string.Empty).IndexOf("supervisor", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                var options = new List<AutoEscalationAssigneeDialog.AssigneeOption>();
                foreach (var sup in supervisors.OrderBy(x => x.EmployeeName))
                {
                    options.Add(new AutoEscalationAssigneeDialog.AssigneeOption
                    {
                        EmpId = sup.EmpId,
                        Name = sup.EmployeeName,
                        Position = sup.Position,
                    });
                }

                var currentAssigneeEmpId = await _repo.GetAutoEscalationAssigneeEmpIdAsync();
                using (var dlg = new AutoEscalationAssigneeDialog(currentAssigneeEmpId, options))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    await _repo.SaveAutoEscalationAssigneeEmpIdAsync(dlg.SelectedEmpId, updatedByUserId: userId);

                    MessageBox.Show(
                        "Escalation assignee updated.",
                        "Escalation Assignee",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Escalation Assignee", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateDepartmentFilterEnabledState()
        {
            try
            {
                var branchOnly = cboLocationModeFilter != null && cboLocationModeFilter.SelectedIndex == 2;
                if (cboDepartmentFilter != null)
                {
                    cboDepartmentFilter.Enabled = !branchOnly;
                    if (branchOnly && cboDepartmentFilter.Items.Count > 0)
                        cboDepartmentFilter.SelectedIndex = 0;
                }
            }
            catch
            {
            }
        }

        private void HandleTicketGridDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var grid = sender as DataGridView;
            if (grid?.Rows == null || e.RowIndex >= grid.Rows.Count) return;

            var row = grid.Rows[e.RowIndex];
            var item = row?.DataBoundItem;

            int ticketId = 0;
            string ticketCode = null;
            if (item is CallTechOpenTicketRow open)
            {
                ticketId = open.TicketId;
                ticketCode = open.TicketCode;
            }
            else if (item is CallTechHandledTicketRow handled)
            {
                ticketId = handled.TicketId;
                ticketCode = handled.TicketCode;
            }

            if (_navigateToTicket != null && ticketId > 0)
            {
                _navigateToTicket(ticketId);
                return;
            }

            if (string.IsNullOrWhiteSpace(ticketCode))
                return;

            try
            {
                Clipboard.SetText(ticketCode);
                lblStatus.Text = $"Copied ticket: {ticketCode}";
            }
            catch
            {
                // ignore clipboard errors
            }
        }

        private ContextMenuStrip BuildTicketContextMenu(bool isOpenGrid)
        {
            var menu = new ContextMenuStrip();

            var miOpen = new ToolStripMenuItem("Open Ticket");
            miOpen.Click += (_, __) => OpenSelectedTicket(isOpenGrid);
            menu.Items.Add(miOpen);

            var miCopyCode = new ToolStripMenuItem("Copy Ticket Code");
            miCopyCode.Click += (_, __) => CopySelectedTicket(isOpenGrid, copyCode: true);
            menu.Items.Add(miCopyCode);

            var miCopyId = new ToolStripMenuItem("Copy Ticket ID");
            miCopyId.Click += (_, __) => CopySelectedTicket(isOpenGrid, copyCode: false);
            menu.Items.Add(miCopyId);

            if (isOpenGrid)
            {
                menu.Items.Add(new ToolStripSeparator());
                var miStatus = new ToolStripMenuItem("Set Status");
                miStatus.DropDownItems.Add(MakeStatusItem("Pending", isOpenGrid));
                miStatus.DropDownItems.Add(MakeStatusItem("In Progress", isOpenGrid));
                miStatus.DropDownItems.Add(MakeStatusItem("Escalated", isOpenGrid));
                miStatus.DropDownItems.Add(new ToolStripSeparator());
                miStatus.DropDownItems.Add(MakeStatusItem("Solved", isOpenGrid));
                miStatus.DropDownItems.Add(MakeStatusItem("Resolved (Temporary)", isOpenGrid));
                miStatus.DropDownItems.Add(MakeStatusItem("Closed", isOpenGrid));
                menu.Items.Add(miStatus);
            }
            else
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(MakeStatusItem("Reopened", isOpenGrid));
            }

            menu.Opening += (_, __) =>
            {
                var info = GetSelectedTicketInfo(isOpenGrid);
                miOpen.Enabled = info.TicketId > 0;
                miCopyCode.Enabled = !string.IsNullOrWhiteSpace(info.TicketCode);
                miCopyId.Enabled = info.TicketId > 0;
            };

            return menu;
        }

        private ToolStripMenuItem MakeStatusItem(string newStatus, bool isOpenGrid)
        {
            var mi = new ToolStripMenuItem(newStatus);
            mi.Click += async (_, __) => await SetSelectedTicketStatusAsync(isOpenGrid, newStatus);
            return mi;
        }

        private (int TicketId, string TicketCode, string Status) GetSelectedTicketInfo(bool isOpenGrid)
        {
            try
            {
                var grid = isOpenGrid ? dgvOpen : dgvHandled;
                var item = grid?.CurrentRow?.DataBoundItem;
                if (item is CallTechOpenTicketRow o) return (o.TicketId, o.TicketCode, o.Status);
                if (item is CallTechHandledTicketRow h) return (h.TicketId, h.TicketCode, h.Status);
            }
            catch
            {
            }

            return (0, null, null);
        }

        private void OpenSelectedTicket(bool isOpenGrid)
        {
            var info = GetSelectedTicketInfo(isOpenGrid);
            if (info.TicketId <= 0)
                return;

            if (_navigateToTicket != null)
            {
                _navigateToTicket(info.TicketId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(info.TicketCode))
            {
                try
                {
                    Clipboard.SetText(info.TicketCode);
                    lblStatus.Text = $"Copied ticket: {info.TicketCode}";
                }
                catch
                {
                }
            }
        }

        private void CopySelectedTicket(bool isOpenGrid, bool copyCode)
        {
            var info = GetSelectedTicketInfo(isOpenGrid);
            try
            {
                Clipboard.SetText(copyCode ? (info.TicketCode ?? string.Empty) : info.TicketId.ToString());
                lblStatus.Text = copyCode ? $"Copied: {info.TicketCode}" : $"Copied: {info.TicketId}";
            }
            catch
            {
            }
        }

        private async Task SetSelectedTicketStatusAsync(bool isOpenGrid, string newStatus)
        {
            var info = GetSelectedTicketInfo(isOpenGrid);
            if (info.TicketId <= 0 || string.IsNullOrWhiteSpace(newStatus))
                return;

            var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            if (changedByUserId == null)
            {
                MessageBox.Show("You must be logged in to change ticket status.", "Call Monitoring Profiles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var oldStatus = info.Status ?? string.Empty;
            if (string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
                return;

            // Final states require resolution details: route through Mark As
            // instead of setting them directly (keeps resolution history and
            // inventory movement intact).
            if (newStatus.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || newStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                || newStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "To mark a ticket as Solved/Resolved (Temporary)/Closed, use the 'Mark As...' button on the ticket.\n\n" +
                    "This ensures resolution details are captured properly.",
                    "Status Change",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var msg = $"Change status for {info.TicketCode ?? info.TicketId.ToString()}?\n\nFrom: {oldStatus}\nTo: {newStatus}";
            if (MessageBox.Show(msg, "Confirm Status Change", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await _repo.SetTicketStatusAsync(info.TicketId, newStatus, changedByUserId, note: null);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _email.NotifyStatusChangeAsync(info.TicketId, oldStatus, newStatus, string.Empty, changedByUserId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                        System.Diagnostics.Trace.TraceError(ex.ToString());
                    }
                });
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Status Change Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
