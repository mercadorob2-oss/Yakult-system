using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Update;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public class DashboardHome : UserControl
    {
        // Controls
        private TableLayoutPanel tlpMain;
        private FlowLayoutPanel pnlKpiContainer;
        private Panel pnlChartsContainer;
        private GroupBox grpAttention;
        private DataGridView dgvAttention;
        private Label lblLastRefreshed;
        private Button btnRefresh;

        // Attention filters
        private ComboBox cmbAttentionView;
        private ComboBox cmbAttentionPriority;
        private TextBox txtAttentionSearch;
        private CheckBox chkMyWork; // NEW: My Work Toggle

        // Attention pagination
        private const int AttentionPageSize = 5;
        private int _attentionPageIndex = 1;
        private Button btnAttentionPrev;
        private Button btnAttentionNext;
        private Label lblAttentionPage;

        // KPI Cards
        private Panel cardOpen;
        private Panel cardCritical;
        private Panel cardResolution;
        private Panel cardVolume;
        private Panel cardMobileUpdates;
        private Label lblOpenValue;
        private Label lblCriticalValue;
        private Label lblResolutionValue;
        private Label lblVolumeValue;
        private Label lblMobileUpdatesValue;
        private readonly List<Panel> _kpiCards = new List<Panel>();

        // Charts
        private Chart chartVolume;
        private Chart chartIssueTypes;
        private Button btnTogglePieChart;
        private Button btnAnalyticsRange;
        private ContextMenuStrip cmsAnalyticsRange;
        private Label lblLeftChartTitle;
        private Label lblRightChartTitle;
        private bool _showResolutionPieChart = true;
        private Panel pnlPieDataTable;
        private TableLayoutPanel tlpPieDataTable;
        private int _analyticsRangeDays = 7;

        // Chart containers to be accessed later
        private Panel pnlChartLeft;
        private Panel pnlChartRight;

        // Repository reference for drill-down
        private ICallMonitoringRepository _repo;

        // Cache for attention list so we can filter without hitting DB repeatedly
        private List<CallTicketListItem> _attentionCache = new List<CallTicketListItem>();
        private int _attentionOverdueDays = 3;
        private bool _isRefreshing;

        public DashboardHome()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InitializeCharts();
            // SetupDummyData(); // REMOVED
        }

        private void InitializeCharts()
        {
            // Initialize Volume Chart
            this.chartVolume = new Chart { Dock = DockStyle.Fill };
            ConfigureLineChart(this.chartVolume);
            this.pnlChartLeft.Controls.Add(this.chartVolume);

            // Initialize Resolution Types Chart
            this.chartIssueTypes = new Chart { Dock = DockStyle.Fill };
            ConfigureDonutChart(this.chartIssueTypes);
            this.chartIssueTypes.MouseClick += ChartIssueTypes_MouseClick;
            this.pnlChartRight.Controls.Add(this.chartIssueTypes);

            this.pnlPieDataTable = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 0)
            };

            this.tlpPieDataTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                BackColor = Color.Transparent
            };
            this.tlpPieDataTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18F));
            this.tlpPieDataTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tlpPieDataTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            this.pnlPieDataTable.Controls.Add(this.tlpPieDataTable);
            this.pnlChartRight.Controls.Add(this.pnlPieDataTable);
            this.pnlPieDataTable.BringToFront();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.BackColor = Color.FromArgb(240, 242, 245); // Soft Gray Background
            this.Size = new Size(1100, 700);
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(16);

            // 1. Main Layout
            this.tlpMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            this.tlpMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F)); // KPI Cards + toolbar
            this.tlpMain.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // Charts
            this.tlpMain.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // Grid

            // 2. KPI Section
            var pnlKpiSection = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0) };

            var pnlKpiToolbar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
            this.lblLastRefreshed = new Label
            {
                Text = "Last refreshed: —",
                AutoSize = true,
                ForeColor = Color.FromArgb(127, 140, 141),
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 6)
            };
            this.btnRefresh = new Button
            {
                Text = "Refresh",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(44, 62, 80),
                Font = new Font("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            this.btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(220, 224, 228);
            this.btnRefresh.FlatAppearance.BorderSize = 1;
            this.btnRefresh.Location = new Point(pnlKpiToolbar.Width - 90, 2);
            pnlKpiToolbar.SizeChanged += (s, e) =>
            {
                this.btnRefresh.Location = new Point(pnlKpiToolbar.Width - this.btnRefresh.Width, 2);
            };
            this.btnRefresh.Click += async (s, e) => await RefreshDashboardAsync();

            pnlKpiToolbar.Controls.Add(this.lblLastRefreshed);
            pnlKpiToolbar.Controls.Add(this.btnRefresh);

            this.pnlKpiContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 0, 0, 10)
            };
            
            // Create Cards (Modern Colors + Click Actions)
            this.cardOpen = CreateKpiCard("Open Tickets", out this.lblOpenValue, "-", "Active now", 
                Color.FromArgb(255, 255, 255), Color.FromArgb(52, 152, 219), "📂",
                async () => await ShowDrillDown("Open Tickets", () => _repo.GetOpenTicketsListAsync()));
                
            this.cardCritical = CreateKpiCard("Critical", out this.lblCriticalValue, "-", "Requires attention", 
                Color.FromArgb(255, 255, 255), Color.FromArgb(231, 76, 60), "🚨",
                async () => await ShowDrillDown("Critical Tickets", () => _repo.GetCriticalTicketsListAsync()));

            this.cardResolution = CreateKpiCard("Avg Resolution", out this.lblResolutionValue, "-", "Last 30 days", 
                Color.FromArgb(255, 255, 255), Color.FromArgb(46, 204, 113), "⏱",
                async () => await ShowDrillDown("Example Resolved", () => _repo.GetRecentlySolvedAsync())); // Approximate

            this.cardVolume = CreateKpiCard("Today's Volume", out this.lblVolumeValue, "-", "Tickets today", 
                Color.FromArgb(255, 255, 255), Color.FromArgb(155, 89, 182), "📊",
                async () => await ShowDrillDown("Ticket Volume Today", () => _repo.GetTodaysTicketsListAsync()));

            this.cardMobileUpdates = CreateKpiCard("Mobile Updates", out this.lblMobileUpdatesValue, "-", "Needs processing",
                Color.FromArgb(255, 255, 255), Color.FromArgb(255, 152, 0), "📱",
                () => OpenMobileUpdates());

            this.pnlKpiContainer.Controls.Add(this.cardOpen);
            this.pnlKpiContainer.Controls.Add(this.cardCritical);
            this.pnlKpiContainer.Controls.Add(this.cardResolution);
            this.pnlKpiContainer.Controls.Add(this.cardVolume);
            this.pnlKpiContainer.Controls.Add(this.cardMobileUpdates);

            pnlKpiSection.Controls.Add(this.pnlKpiContainer);
            pnlKpiSection.Controls.Add(pnlKpiToolbar);

            // 3. Charts Section (Containers ONLY)
            this.pnlChartsContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
            var splitCharts = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 16,
                BackColor = Color.Transparent
            };
            // Prevent split container focus rectangles
            splitCharts.TabStop = false;
            // Keep the line chart larger than the donut chart (70/30 split).
            SplitContainerUtil.BindSafeSplitterDistance(splitCharts, () => (int)(splitCharts.Width * 0.70));

            this.btnAnalyticsRange = new Button
            {
                Text = "Last 7 Days",
                Width = 110,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(52, 152, 219),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            this.btnAnalyticsRange.FlatAppearance.BorderColor = Color.FromArgb(236, 240, 241);
            this.btnAnalyticsRange.FlatAppearance.BorderSize = 1;
            this.btnAnalyticsRange.Click += (s, e) =>
            {
                EnsureAnalyticsRangeMenu();
                this.cmsAnalyticsRange?.Show(this.btnAnalyticsRange, new Point(0, this.btnAnalyticsRange.Height));
            };

            // Create container panels
            this.pnlChartLeft = CreateContentPanel("Ticket Volume (Last 7 Days)", out this.lblLeftChartTitle, headerRight: this.btnAnalyticsRange);

            this.btnTogglePieChart = new Button
            {
                Text = "Active Tickets",
                Width = 120,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(52, 152, 219),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            this.btnTogglePieChart.FlatAppearance.BorderColor = Color.FromArgb(236, 240, 241);
            this.btnTogglePieChart.FlatAppearance.BorderSize = 1;
            this.btnTogglePieChart.Click += async (_, __) =>
            {
                this._showResolutionPieChart = !this._showResolutionPieChart;
                UpdateAnalyticsTitles();
                await RefreshPieChartAsync();
            };

            this.pnlChartRight = CreateContentPanel("Resolutions (Last 30 Days)", out this.lblRightChartTitle, headerRight: this.btnTogglePieChart);
            
            splitCharts.Panel1.Controls.Add(this.pnlChartLeft);
            splitCharts.Panel2.Controls.Add(this.pnlChartRight);
            this.pnlChartsContainer.Controls.Add(splitCharts);

            // 4. Attention Grid (Bottom)
            this.grpAttention = new GroupBox
            {
                Text = "", // Hide default groupbox text for custom header
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent,
                Padding = new Padding(0, 5, 0, 0)
            };
            
            // Container for grid to add white bg and padding
            var pnlGridContainer = CreateContentPanel("Requires Attention (High Priority / Unassigned)");

            var pnlGridTools = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 6)
            };

            var lblFilter = new Label
            {
                Text = "Filter:",
                AutoSize = true,
                ForeColor = Color.FromArgb(127, 140, 141),
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 10)
            };

            this.btnAttentionPrev = new Button
            {
                Text = "<",
                Width = 30,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(44, 62, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            this.btnAttentionPrev.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            this.btnAttentionPrev.Click += (s, e) =>
            {
                if (_attentionPageIndex > 1)
                {
                    _attentionPageIndex--;
                    ApplyAttentionFiltersAndRender();
                }
            };

            this.btnAttentionNext = new Button
            {
                Text = ">",
                Width = 30,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(44, 62, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            this.btnAttentionNext.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            this.btnAttentionNext.Click += (s, e) =>
            {
                _attentionPageIndex++;
                ApplyAttentionFiltersAndRender();
            };

            this.lblAttentionPage = new Label
            {
                Text = "Page 1 / 1 (0)",
                AutoSize = true,
                ForeColor = Color.FromArgb(127, 140, 141),
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 10)
            };

            this.cmbAttentionPriority = new ComboBox
            {
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F)
            };
            this.cmbAttentionPriority.Items.AddRange(new object[] { "All priorities", "Critical", "High", "Medium", "Low" });
            this.cmbAttentionPriority.SelectedIndex = 0;

            this.cmbAttentionView = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F)
            };
            this.cmbAttentionView.Items.AddRange(new object[] { "All", "Overdue", "Unassigned" });
            this.cmbAttentionView.SelectedIndex = 0;

            this.txtAttentionSearch = new TextBox
            {
                Width = 220,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F)
            };

            this.chkMyWork = new CheckBox
            {
                Text = "My Work",
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                ForeColor = Color.FromArgb(44, 62, 80),
                BackColor = Color.White
            };
            this.chkMyWork.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            this.chkMyWork.FlatAppearance.CheckedBackColor = Color.FromArgb(46, 204, 113); // Green when active
            
            // Only enabled if we have an employee ID
            this.chkMyWork.Enabled =
                (Yakult.Inventory.App.Session.AppSession.CurrentEmployeeId.HasValue && Yakult.Inventory.App.Session.AppSession.CurrentEmployeeId.Value > 0)
                || !string.IsNullOrWhiteSpace(Yakult.Inventory.App.Session.AppSession.CurrentEmployeeName)
                || !string.IsNullOrWhiteSpace(Yakult.Inventory.App.Session.AppSession.CurrentUserName);
            this.chkMyWork.CheckedChanged += (s, e) => 
            {
                 ApplyAttentionFiltersAndRender(resetPage: true);
                 this.chkMyWork.ForeColor = this.chkMyWork.Checked ? Color.White : Color.FromArgb(44, 62, 80);
            };

            pnlGridTools.Controls.Add(lblFilter);
            pnlGridTools.Controls.Add(this.btnAttentionPrev);
            pnlGridTools.Controls.Add(this.btnAttentionNext);
            pnlGridTools.Controls.Add(this.lblAttentionPage);
            pnlGridTools.Controls.Add(this.cmbAttentionPriority);
            pnlGridTools.Controls.Add(this.cmbAttentionView);
            pnlGridTools.Controls.Add(this.txtAttentionSearch);
            pnlGridTools.Controls.Add(this.chkMyWork);

            pnlGridTools.SizeChanged += (s, e) =>
            {
                // Left-side paging controls
                lblFilter.Location = new Point(0, 10);
                var left = lblFilter.Right + 8;
                this.btnAttentionPrev.Location = new Point(left, 8);
                left += (this.btnAttentionPrev.Width + 4);
                this.btnAttentionNext.Location = new Point(left, 8);
                left += (this.btnAttentionNext.Width + 8);
                this.lblAttentionPage.Location = new Point(left, 10);

                var right = pnlGridTools.Width;
                this.txtAttentionSearch.Location = new Point(right - this.txtAttentionSearch.Width, 8);
                right -= (this.txtAttentionSearch.Width + 10);
                this.cmbAttentionView.Location = new Point(right - this.cmbAttentionView.Width, 8);
                right -= (this.cmbAttentionView.Width + 10);
                this.cmbAttentionPriority.Location = new Point(right - this.cmbAttentionPriority.Width, 8);
                right -= (this.cmbAttentionPriority.Width + 16);
                this.chkMyWork.Location = new Point(right - this.chkMyWork.Width, 8);
            };

            this.cmbAttentionView.SelectedIndexChanged += (s, e) => ApplyAttentionFiltersAndRender(resetPage: true);
            this.cmbAttentionPriority.SelectedIndexChanged += (s, e) => ApplyAttentionFiltersAndRender(resetPage: true);
            this.txtAttentionSearch.TextChanged += (s, e) => ApplyAttentionFiltersAndRender(resetPage: true);

            this.dgvAttention = new DataGridView();
            ConfigureGrid(this.dgvAttention);
            this.dgvAttention.ScrollBars = ScrollBars.None; // Paging handles vertical navigation
            pnlGridContainer.Controls.Add(this.dgvAttention); // Add grid to content panel
            pnlGridContainer.Controls.Add(pnlGridTools);
            this.dgvAttention.BringToFront(); 

            this.grpAttention.Controls.Add(pnlGridContainer);


            // Assemble
            this.Controls.Add(this.tlpMain); // Add to parent first so it inherits size

            this.tlpMain.Controls.Add(pnlKpiSection, 0, 0);
            this.tlpMain.Controls.Add(this.pnlChartsContainer, 0, 1);
            this.tlpMain.Controls.Add(this.grpAttention, 0, 2);

            // Keep KPI cards responsive after the control is first shown and whenever the layout changes.
            this.pnlKpiContainer.SizeChanged += (s, e) => ResizeKpiCards();
            this.SizeChanged += (s, e) => ResizeKpiCards();
            this.Layout += (s, e) => ResizeKpiCards();
            this.VisibleChanged += (s, e) =>
            {
                if (this.Visible)
                    RequestResizeKpiCards();
            };
            RequestResizeKpiCards();

            this.ResumeLayout(false);
        }

        // --- HELPERS ---

        private void RequestResizeKpiCards()
        {
            if (this.IsDisposed)
                return;

            if (this.IsHandleCreated)
            {
                BeginInvoke((Action)(() => ResizeKpiCards()));
                return;
            }

            EventHandler handler = null;
            handler = (_, __) =>
            {
                try
                {
                    this.HandleCreated -= handler;
                }
                catch
                {
                }

                if (!this.IsDisposed && this.IsHandleCreated)
                    BeginInvoke((Action)(() => ResizeKpiCards()));
            };

            this.HandleCreated += handler;
        }

        private void ResizeKpiCards()
        {
            if (this.pnlKpiContainer == null || this.pnlKpiContainer.IsDisposed)
                return;

            var cards = _kpiCards.Where(p => p != null && !p.IsDisposed && p.Visible).ToList();

            if (cards.Count == 0)
                return;

            const int spacing = 16;
            var availableWidth = this.pnlKpiContainer.ClientSize.Width;
            if (availableWidth <= 0)
                return;

            var totalSpacing = spacing * (cards.Count - 1);
            var targetWidth = (availableWidth - totalSpacing) / cards.Count;
            targetWidth = Math.Max(220, targetWidth);

            this.pnlKpiContainer.SuspendLayout();
            try
            {
                for (var i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    card.Margin = new Padding(0, 0, i == cards.Count - 1 ? 0 : spacing, 0);
                    card.Width = targetWidth;
                }
            }
            finally
            {
                this.pnlKpiContainer.ResumeLayout(true);
            }
        }

        private async Task RefreshDashboardAsync()
        {
            if (_isRefreshing)
                return;

            if (_repo == null)
                return;

            try
            {
                _isRefreshing = true;
                if (this.btnRefresh != null) this.btnRefresh.Enabled = false;
                this.Cursor = Cursors.WaitCursor;
                await LoadFromRepositoryAsync(_repo);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (this.btnRefresh != null) this.btnRefresh.Enabled = true;
                _isRefreshing = false;
            }
        }

        private void ApplyAttentionFiltersAndRender(bool resetPage = false)
        {
            if (this.dgvAttention == null)
                return;

            if (resetPage)
                _attentionPageIndex = 1;

            var list = _attentionCache ?? new List<CallTicketListItem>();

            var view = this.cmbAttentionView?.SelectedItem as string;
            var prio = this.cmbAttentionPriority?.SelectedItem as string;
            var search = (this.txtAttentionSearch?.Text ?? string.Empty).Trim();

            IEnumerable<CallTicketListItem> filtered = list;

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(t =>
                    (!string.IsNullOrWhiteSpace(t.TicketCode) && t.TicketCode.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(t.Issue) && t.Issue.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(t.ResponsiblePerson) && t.ResponsiblePerson.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (!string.IsNullOrWhiteSpace(prio) && !string.Equals(prio, "All priorities", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(t => string.Equals(t.Priority, prio, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(view, "Overdue", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(IsTicketOverdue);
            }
            else if (string.Equals(view, "Unassigned", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson));
            }

            if (this.chkMyWork != null && this.chkMyWork.Checked && this.chkMyWork.Enabled)
            {
                var myEmpId = Yakult.Inventory.App.Session.AppSession.CurrentEmployeeId;
                var myName = Yakult.Inventory.App.Session.AppSession.CurrentEmployeeName
                    ?? Yakult.Inventory.App.Session.AppSession.CurrentUserName;

                var canUseEmpId = myEmpId.HasValue
                    && myEmpId.Value > 0
                    && list.Any(t => t.AssignedToEmpId.HasValue && t.AssignedToEmpId.Value > 0);

                if (canUseEmpId)
                {
                    filtered = filtered.Where(t => t.AssignedToEmpId == myEmpId.Value);
                }
                else if (!string.IsNullOrWhiteSpace(myName))
                {
                    var norm = myName.Trim();
                    filtered = filtered.Where(t =>
                        !string.IsNullOrWhiteSpace(t.ResponsiblePerson)
                        && (string.Equals(t.ResponsiblePerson.Trim(), norm, StringComparison.OrdinalIgnoreCase)
                            || t.ResponsiblePerson.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0));
                }
            }

            var filteredList = filtered.ToList();

            var totalCount = filteredList.Count;
            var totalPages = System.Math.Max(1, (int)System.Math.Ceiling(totalCount / (double)AttentionPageSize));
            if (_attentionPageIndex < 1) _attentionPageIndex = 1;
            if (_attentionPageIndex > totalPages) _attentionPageIndex = totalPages;

            var pageItems = filteredList
                .Skip((_attentionPageIndex - 1) * AttentionPageSize)
                .Take(AttentionPageSize)
                .ToList();

            if (this.lblAttentionPage != null)
                this.lblAttentionPage.Text = $"Page {_attentionPageIndex} / {totalPages} ({totalCount})";

            if (this.btnAttentionPrev != null)
                this.btnAttentionPrev.Enabled = _attentionPageIndex > 1;
            if (this.btnAttentionNext != null)
                this.btnAttentionNext.Enabled = _attentionPageIndex < totalPages;

            RenderAttentionGrid(pageItems);
        }

        private bool IsTicketOverdue(CallTicketListItem t)
        {
            if (t == null)
                return false;

            var status = t.Status ?? string.Empty;
            if (string.Equals(status, "Solved", StringComparison.OrdinalIgnoreCase))
                return false;

            var createdUtc = AppTime.AssumeUtc(t.CreatedAt);
            var ageDays = t.TicketAgeDays > 0 ? t.TicketAgeDays : (int)Math.Floor((AppTime.UtcNow - createdUtc).TotalDays);
            return ageDays >= _attentionOverdueDays;
        }

        private void RenderAttentionGrid(List<CallTicketListItem> attention)
        {
            if (this.dgvAttention == null)
                return;

            this.dgvAttention.Rows.Clear();

            foreach (var t in attention ?? new List<CallTicketListItem>())
            {
                var isOverdue = IsTicketOverdue(t);
                var displayStatus = isOverdue ? "Overdue" : (t.Status ?? "-");

                var createdUtc = AppTime.AssumeUtc(t.CreatedAt);
                var ageText = t.TicketAgeDays > 0
                    ? $"{t.TicketAgeDays}d"
                    : $"{Math.Max(0, (int)Math.Floor((AppTime.UtcNow - createdUtc).TotalDays))}d";

                var updatedText = t.UpdatedAt != default(DateTime)
                    ? AppTime.ToLocalString(t.UpdatedAt, "MMM dd HH:mm")
                    : "-";

                var index = this.dgvAttention.Rows.Add(
                    t.TicketCode ?? t.TicketId.ToString(),
                    ageText,
                    t.Priority ?? "-",
                    t.Issue ?? "-",
                    displayStatus,
                    string.IsNullOrWhiteSpace(t.ResponsiblePerson) ? "Unassigned" : t.ResponsiblePerson,
                    updatedText);

                var row = this.dgvAttention.Rows[index];

                // Style Row
                var prio = t.Priority;
                if (string.Equals(prio, "Critical", StringComparison.OrdinalIgnoreCase))
                {
                    row.Cells[2].Style.BackColor = Color.FromArgb(255, 235, 238); // Light Red Bg
                    row.Cells[2].Style.ForeColor = Color.FromArgb(192, 57, 43);   // Dark Red Text
                    row.Cells[2].Style.SelectionBackColor = Color.FromArgb(255, 205, 210);
                    row.Cells[2].Style.SelectionForeColor = Color.FromArgb(146, 43, 33);
                }
                else if (string.Equals(prio, "High", StringComparison.OrdinalIgnoreCase))
                {
                    row.Cells[2].Style.BackColor = Color.FromArgb(255, 243, 224); // Orange Bg
                    row.Cells[2].Style.ForeColor = Color.FromArgb(230, 126, 34);
                }

                if (isOverdue)
                    row.Cells[4].Style.ForeColor = Color.FromArgb(231, 76, 60);
            }
        }

        private async Task ShowDrillDown(string title, Func<Task<System.Collections.Generic.List<Yakult.Inventory.App.Models.CallMonitoring.CallTicketListItem>>> fetchAction)
        {
            if (_repo == null) return;
            
            // Show loading assumption?
            this.Cursor = Cursors.WaitCursor;
            try
            {
                var list = await fetchAction();
                using (var form = new DashboardDrillDownForm(title, list))
                {
                    form.ShowDialog(this.ParentForm);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DashboardHome] Drill-down load failed. Title={title}", ex);
                MessageBox.Show("Error loading details: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private Panel CreateKpiCard(string title, out Label valueLabel, string value, string sub, Color bgColor, Color accentColor, string icon, Action onClick = null)
        {
            var pnl = new Panel
            {
                Size = new Size(220, 130),
                BackColor = bgColor,
                Margin = new Padding(0, 0, 16, 0),
                Padding = new Padding(16)
            };
            
            // Left Border Accent
            var pnlAccent = new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = accentColor
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(8, 0, 0, 0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));

            var lblTitle = new Label
            {
                Text = title.ToUpperInvariant(),
                ForeColor = Color.FromArgb(149, 165, 166),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 0)
            };

            valueLabel = new Label
            {
                Text = value,
                ForeColor = Color.FromArgb(44, 62, 80),
                Font = new Font("Segoe UI", 28F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblSub = new Label
            {
                Text = sub,
                ForeColor = Color.FromArgb(127, 140, 141),
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };

            var lblIcon = new Label
            {
                Text = icon,
                ForeColor = Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B), // Very Transparent
                Font = new Font("Segoe UI Emoji", 40F),
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            content.Controls.Add(lblTitle, 0, 0);
            content.Controls.Add(valueLabel, 0, 1);
            content.Controls.Add(lblSub, 0, 2);
            content.Controls.Add(lblIcon, 1, 0);
            content.SetRowSpan(lblIcon, 3);

            pnl.Controls.Add(content);
            pnl.Controls.Add(pnlAccent);
            _kpiCards.Add(pnl);

            if (onClick != null)
            {
                pnl.Cursor = Cursors.Hand;
                // Bind click to all controls so user doesn't miss the panel
                void WireClick(Control c) 
                {
                    c.Click += (s, e) => onClick();
                    c.Cursor = Cursors.Hand;
                }
                
                WireClick(pnl);
                WireClick(lblTitle);
                WireClick(valueLabel);
                WireClick(lblSub);
                WireClick(lblIcon);
                WireClick(pnlAccent);
            }

            return pnl;
        }

        public async Task LoadFromRepositoryAsync(ICallMonitoringRepository repo)
        {
            this._repo = repo;
            if (repo == null)
                return;

            if (!await repo.CallSchemaExistsAsync())
                return;

            var metrics = await repo.GetDashboardMetricsAsync();
            UpdateAnalyticsTitles();
            if (this.lblOpenValue != null) this.lblOpenValue.Text = metrics.OpenTickets.ToString();
            if (this.lblCriticalValue != null) this.lblCriticalValue.Text = metrics.CriticalTickets.ToString();

            if (this.lblResolutionValue != null)
            {
                if (metrics.AvgResolutionMinutes.HasValue)
                {
                    var minutes = metrics.AvgResolutionMinutes.Value;
                    var hours = minutes / 60;
                    var mins = minutes % 60;
                    this.lblResolutionValue.Text = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
                }
                else
                {
                    this.lblResolutionValue.Text = "-";
                }
            }

            if (this.lblVolumeValue != null) this.lblVolumeValue.Text = metrics.TodaysVolume.ToString();
            await RefreshMobileUpdatesCountAsync();

            if (this.chartVolume != null && this.chartVolume.Series.Count > 0)
            {
                var range = GetAnalyticsRangeUtc();
                var points = await repo.GetTicketVolumeByDayAsync(range.FromUtc, range.ToUtcExclusive);
                var s = this.chartVolume.Series[0];
                s.Points.Clear();
                foreach (var p in points)
                {
                    s.Points.AddXY(p.Day.ToString("ddd"), p.TicketCount);
                }
            }

            if (this.chartIssueTypes != null && this.chartIssueTypes.Series.Count > 0)
                await RefreshPieChartAsync();

            if (this.dgvAttention != null)
            {
                _attentionOverdueDays = await repo.GetOverdueDaysAsync(3);
                _attentionCache = await repo.GetRequiresAttentionAsync(100) ?? new List<CallTicketListItem>();
                _attentionPageIndex = 1;
                ApplyAttentionFiltersAndRender(resetPage: true);
            }

            if (this.lblLastRefreshed != null)
            {
                this.lblLastRefreshed.Text = "Last refreshed: " + DateTime.Now.ToString("MMM dd, h:mm tt");
            }
        }

        private async Task RefreshPieChartAsync()
        {
            if (this.chartIssueTypes == null || this.chartIssueTypes.Series.Count == 0)
                return;

            try
            {
                var s2 = this.chartIssueTypes.Series[0];
                s2.Points.Clear();

                if (this._showResolutionPieChart)
                {
                    UpdateAnalyticsTitles();
                    if (this.btnTogglePieChart != null)
                        this.btnTogglePieChart.Text = "Active Tickets";

                    if (this._repo == null)
                        return;

                    var range = GetAnalyticsRangeUtc();

                    var dist = await this._repo.GetResolutionBucketDistributionAsync(range.FromUtc, range.ToUtcInclusive);
                    var rows = new List<(string Bucket, int TicketCount, Color Color)>();
                    foreach (var p in dist)
                    {
                        var idx = s2.Points.AddXY(p.Bucket, p.TicketCount);
                        var dp = s2.Points[idx];
                        dp.LegendText = p.Bucket;
                        dp.ToolTip = $"{p.Bucket}: {p.TicketCount}";
                        dp.Color = GetResolutionBucketColor(p.Bucket);

                        rows.Add((p.Bucket, p.TicketCount, dp.Color));
                    }

                    RenderPieDataTable(rows);
                }
                else
                {
                    UpdateAnalyticsTitles();
                    if (this.btnTogglePieChart != null)
                        this.btnTogglePieChart.Text = "Resolutions";

                    if (this._repo == null)
                        return;

                    var dist = await this._repo.GetActiveTicketStatusDistributionAsync();
                    var rows = new List<(string Bucket, int TicketCount, Color Color)>();
                    foreach (var p in dist)
                    {
                        var idx = s2.Points.AddXY(p.Bucket, p.TicketCount);
                        var dp = s2.Points[idx];
                        dp.LegendText = p.Bucket;
                        dp.ToolTip = $"{p.Bucket}: {p.TicketCount}";
                        dp.Color = GetActiveStatusColor(p.Bucket);

                        rows.Add((p.Bucket, p.TicketCount, dp.Color));
                    }

                    RenderPieDataTable(rows);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[DashboardHome] RefreshPieChartAsync failed: {ex.Message}");
            }
        }

        private static Color GetActiveStatusColor(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                return Color.FromArgb(156, 163, 175); // gray

            if (string.Equals(bucket, "Pending", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(245, 158, 11); // amber
            if (string.Equals(bucket, "In Progress", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(59, 130, 246); // blue
            if (string.Equals(bucket, "Solved", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(16, 185, 129); // green

            return Color.FromArgb(156, 163, 175); // gray
        }

        private static Color GetResolutionBucketColor(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                return Color.FromArgb(156, 163, 175); // gray

            if (string.Equals(bucket, "Repair", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(59, 130, 246); // blue
            if (string.Equals(bucket, "Replacement", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(139, 92, 246); // purple
            if (string.Equals(bucket, "Temporary Replacement", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(245, 158, 11); // amber
            if (string.Equals(bucket, "Other", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(156, 163, 175); // gray

            return Color.FromArgb(156, 163, 175); // gray
        }

        private void RenderPieDataTable(List<(string Bucket, int TicketCount, Color Color)> rows)
        {
            if (this.tlpPieDataTable == null)
                return;

            try
            {
                this.tlpPieDataTable.SuspendLayout();
                this.tlpPieDataTable.Controls.Clear();
                this.tlpPieDataTable.RowStyles.Clear();

                if (rows == null || rows.Count == 0)
                {
                    this.tlpPieDataTable.RowCount = 1;
                    this.tlpPieDataTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                    this.tlpPieDataTable.Controls.Add(new Label { Width = 12 }, 0, 0);
                    this.tlpPieDataTable.Controls.Add(new Label
                    {
                        Text = "No data",
                        AutoSize = true,
                        ForeColor = Color.FromArgb(127, 140, 141),
                        Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                        Padding = new Padding(0, 2, 0, 2)
                    }, 1, 0);

                    return;
                }

                var total = rows.Sum(r => r.TicketCount);
                total = Math.Max(total, 1);

                this.tlpPieDataTable.RowCount = rows.Count;
                for (var i = 0; i < rows.Count; i++)
                {
                    this.tlpPieDataTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var r = rows[i];

                    var colorBox = new Panel
                    {
                        Width = 12,
                        Height = 12,
                        BackColor = r.Color,
                        Margin = new Padding(0, 5, 6, 0)
                    };

                    var pct = (int)Math.Round((r.TicketCount * 100.0) / total);
                    var nameLabel = new Label
                    {
                        Text = r.Bucket,
                        AutoSize = true,
                        ForeColor = Color.FromArgb(44, 62, 80),
                        Font = new Font("Segoe UI", 8.5F),
                        Padding = new Padding(0, 2, 0, 2)
                    };

                    var countLabel = new Label
                    {
                        Text = $"{r.TicketCount} ({pct}%)",
                        AutoSize = true,
                        ForeColor = Color.FromArgb(127, 140, 141),
                        Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                        Padding = new Padding(10, 2, 0, 2),
                        TextAlign = ContentAlignment.MiddleRight
                    };

                    this.tlpPieDataTable.Controls.Add(colorBox, 0, i);
                    this.tlpPieDataTable.Controls.Add(nameLabel, 1, i);
                    this.tlpPieDataTable.Controls.Add(countLabel, 2, i);

                    // Row click -> open a modal ticket table (similar to resolution drill-down).
                    if (this._repo != null)
                    {
                        void WireClick(Control c)
                        {
                            c.Cursor = Cursors.Hand;
                            c.Click += async (_, __) =>
                            {
                                if (this._showResolutionPieChart)
                                    await OpenResolutionReportsAsync(r.Bucket);
                                else
                                    await OpenActiveTicketsDrillDownAsync(r.Bucket);
                            };
                        }

                        WireClick(colorBox);
                        WireClick(nameLabel);
                        WireClick(countLabel);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                this.tlpPieDataTable.ResumeLayout(true);
            }
        }

        private async Task RefreshMobileUpdatesCountAsync()
        {
            if (this.lblMobileUpdatesValue == null)
                return;

            try
            {
                var updatesRepo = new SetItemUpdateRepository();
                var count = await updatesRepo.GetUnprocessedCountAsync();
                this.lblMobileUpdatesValue.Text = count.ToString();
                this.lblMobileUpdatesValue.ForeColor = count > 0 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(44, 62, 80);
            }
            catch (Exception ex)
            {
                Logger.LogError("[DashboardHome] Failed to load Mobile Updates count.", ex);
                this.lblMobileUpdatesValue.Text = "-";
                this.lblMobileUpdatesValue.ForeColor = Color.FromArgb(44, 62, 80);
            }
        }

        private void OpenMobileUpdates()
        {
            try
            {
                var owner = this.FindForm();
                using (var form = new Form
                {
                    Text = "Mobile Updates",
                    StartPosition = FormStartPosition.CenterParent,
                    Size = new Size(1200, 750),
                    MinimumSize = new Size(1024, 650)
                })
                {
                    var page = new ViewUpdatesPage
                    {
                        Dock = DockStyle.Fill
                    };

                    form.Controls.Add(page);

                    if (owner != null)
                        form.ShowDialog(owner);
                    else
                        form.ShowDialog();
                }

                _ = RefreshMobileUpdatesCountAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("[DashboardHome] Unable to open Mobile Updates.", ex);
                MessageBox.Show($"Unable to open Mobile Updates.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ChartIssueTypes_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (this.chartIssueTypes == null || this.chartIssueTypes.Series.Count == 0)
                    return;

                var hit = this.chartIssueTypes.HitTest(e.X, e.Y);
                if (hit == null)
                    return;

                // Support clicking either the slice or the legend item (color).
                if (hit.ChartElementType != ChartElementType.DataPoint && hit.ChartElementType != ChartElementType.LegendItem)
                    return;

                if (hit.Series == null || hit.PointIndex < 0 || hit.PointIndex >= hit.Series.Points.Count)
                    return;

                var dp = hit.Series.Points[hit.PointIndex];
                var bucket = (dp.AxisLabel ?? dp.LegendText ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(bucket))
                    return;

                if (this._showResolutionPieChart)
                    await OpenResolutionReportsAsync(bucket);
                else
                    await OpenActiveTicketsDrillDownAsync(bucket);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[DashboardHome] ChartIssueTypes click failed: {ex.Message}");
            }
        }

        private async Task OpenActiveTicketsDrillDownAsync(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket) || _repo == null)
                return;

            var title = $"{bucket} Tickets (Current)";
            var list = await _repo.GetTicketsByActiveStatusBucketAsync(bucket.Trim(), maxRows: 500);

            using (var form = new DashboardDrillDownForm(title, list))
            {
                var owner = this.FindForm();
                if (owner != null)
                    form.ShowDialog(owner);
                else
                    form.ShowDialog();
            }
        }

        private async Task OpenResolutionReportsAsync(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket) || _repo == null)
                return;

            var range = GetAnalyticsRangeUtc();
            var title = $"{bucket} Resolutions ({GetAnalyticsRangeTitleText()})";
            var list = await _repo.GetResolvedTicketsByResolutionBucketAsync(range.FromUtc, range.ToUtcInclusive, bucket, maxRows: 500);

            using (var form = new DashboardDrillDownForm(title, list))
            {
                var owner = this.FindForm();
                if (owner != null)
                    form.ShowDialog(owner);
                else
                    form.ShowDialog();
            }
        }

        private void EnsureAnalyticsRangeMenu()
        {
            if (this.cmsAnalyticsRange != null)
            {
                RefreshAnalyticsRangeMenuChecks();
                return;
            }

            this.cmsAnalyticsRange = new ContextMenuStrip();
            this.cmsAnalyticsRange.Items.Add(CreateAnalyticsRangeMenuItem("Last 7 Days", 7));
            this.cmsAnalyticsRange.Items.Add(CreateAnalyticsRangeMenuItem("Last 30 Days", 30));
            this.cmsAnalyticsRange.Items.Add(CreateAnalyticsRangeMenuItem("Last 90 Days", 90));
            RefreshAnalyticsRangeMenuChecks();
        }

        private ToolStripMenuItem CreateAnalyticsRangeMenuItem(string text, int days)
        {
            var item = new ToolStripMenuItem(text) { Tag = days };
            item.Click += async (_, __) =>
            {
                if (_analyticsRangeDays == days)
                    return;

                _analyticsRangeDays = days;
                UpdateAnalyticsTitles();
                if (_repo != null)
                    await RefreshDashboardAsync();
            };
            return item;
        }

        private void RefreshAnalyticsRangeMenuChecks()
        {
            if (this.cmsAnalyticsRange == null)
                return;

            foreach (var item in this.cmsAnalyticsRange.Items.OfType<ToolStripMenuItem>())
            {
                item.Checked = item.Tag is int days && days == _analyticsRangeDays;
            }
        }

        private (DateTime FromUtc, DateTime ToUtcInclusive, DateTime ToUtcExclusive) GetAnalyticsRangeUtc()
        {
            var toUtcExclusive = DateTime.UtcNow.Date.AddDays(1);
            var fromUtc = toUtcExclusive.AddDays(-_analyticsRangeDays);
            return (fromUtc, toUtcExclusive.AddTicks(-1), toUtcExclusive);
        }

        private string GetAnalyticsRangeTitleText()
        {
            return _analyticsRangeDays == 1 ? "LAST 1 DAY" : $"LAST {_analyticsRangeDays} DAYS";
        }

        private void UpdateAnalyticsTitles()
        {
            if (this.lblLeftChartTitle != null)
                this.lblLeftChartTitle.Text = $"TICKET VOLUME ({GetAnalyticsRangeTitleText()})";

            if (this.lblRightChartTitle != null)
            {
                this.lblRightChartTitle.Text = this._showResolutionPieChart
                    ? $"RESOLUTIONS ({GetAnalyticsRangeTitleText()})"
                    : "ACTIVE TICKETS (CURRENT)";
            }

            if (this.btnAnalyticsRange != null)
                this.btnAnalyticsRange.Text = _analyticsRangeDays == 1 ? "Last 1 Day" : $"Last {_analyticsRangeDays} Days";

            RefreshAnalyticsRangeMenuChecks();
        }

        private Panel CreateContentPanel(string title)
        {
            return CreateContentPanel(title, out _, headerRight: null);
        }

        private Panel CreateContentPanel(string title, out Label titleLabel, Control headerRight)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(12)
            };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.Transparent
            };

            titleLabel = new Label
            {
                Text = title.ToUpper(),
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(127, 140, 141),
                TextAlign = ContentAlignment.MiddleLeft
            };

            header.Controls.Add(titleLabel);
            if (headerRight != null)
            {
                headerRight.Dock = DockStyle.Right;
                headerRight.Margin = new Padding(0, 3, 0, 3);
                header.Controls.Add(headerRight);
            }

            // Bottom border for header
            var pnlLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(236, 240, 241) };

            pnl.Controls.Add(pnlLine);
            pnl.Controls.Add(header);
            return pnl;
        }

        private void ConfigureGrid(DataGridView grid)
        {
            // Clear existing
            grid.Columns.Clear();
            
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 44;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(127, 140, 141);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8,0,0,0);
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            grid.DefaultCellStyle.Padding = new Padding(8,4,8,4);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 248, 255); // AliceBlue
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(44, 62, 80);
            
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(236, 240, 241);

            // Fix for text clipping
            grid.RowTemplate.Height = 40;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            
            // Add Columns
            grid.Columns.Add("ID", "TICKET ID");
            grid.Columns.Add("Age", "AGE");
            grid.Columns.Add("Priority", "PRIORITY");
            grid.Columns.Add("Subject", "ISSUE");
            grid.Columns.Add("Status", "STATUS");
            grid.Columns.Add("Assignee", "ASSIGNED TO");
            grid.Columns.Add("Updated", "UPDATED");
            
            grid.Columns[0].FillWeight = 12;
            grid.Columns[1].FillWeight = 8;
            grid.Columns[2].FillWeight = 12;
            grid.Columns[3].FillWeight = 38;
            grid.Columns[4].FillWeight = 12;
            grid.Columns[5].FillWeight = 12;
            grid.Columns[6].FillWeight = 10;
        }

        private void ConfigureLineChart(Chart chart)
        {
            var area = new ChartArea("MainArea");
            area.BackColor = Color.White;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            area.AxisX.LabelStyle.ForeColor = Color.Gray;
            area.AxisY.LabelStyle.ForeColor = Color.Gray;
            area.AxisX.LineColor = Color.LightGray;
            area.AxisY.LineColor = Color.Transparent;
            area.AxisX.Interval = 1;
            area.AxisX.IsMarginVisible = false;
            area.AxisY.Minimum = 0;
            chart.ChartAreas.Add(area);

            var series = new Series("Tickets");
            series.ChartType = SeriesChartType.SplineArea;
            series.Color = Color.FromArgb(200, 52, 152, 219); // Semi-transparent blue
            series.BackGradientStyle = GradientStyle.TopBottom;
            series.BackSecondaryColor = Color.White;
            series.BorderColor = Color.FromArgb(52, 152, 219);
            series.BorderWidth = 2;
            series.IsXValueIndexed = true;
            series.MarkerStyle = MarkerStyle.Circle;
            series.MarkerSize = 6;
            series.MarkerColor = Color.FromArgb(52, 152, 219);
            chart.Series.Add(series);
        }

        private void ConfigureDonutChart(Chart chart)
        {
            var area = new ChartArea("MainArea");
            area.BackColor = Color.White;
            // Make the donut smaller + cleaner: rely on the data table below instead of slice labels/legend.
            // (Chart uses percentages of the chart surface; tweak here to change donut size.)
            // ~3% smaller than the previous layout.
            area.Position = new ElementPosition(10, 6, 77, 79);
            area.InnerPlotPosition = new ElementPosition(13, 11, 76, 84);
            chart.ChartAreas.Add(area);

            var series = new Series("Types");
            series.ChartType = SeriesChartType.Doughnut;
            series["DoughnutRadius"] = "65";
            series.IsValueShownAsLabel = false;
            series.SmartLabelStyle.Enabled = false;
            series["PieLabelStyle"] = "Disabled";
            series["PieLineColor"] = "Transparent";
            chart.Series.Add(series);
            
            // We set colors per-slice; hide built-in legend to avoid duplicating the table below.
            chart.Legends.Clear();
        }
    }
}
