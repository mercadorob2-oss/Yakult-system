using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Yakult.Inventory.App.Forms.Dashboard
{
    /// <summary>
    /// Dashboard UserControl - Contains ONLY UI logic
    /// NO SQL code in this file
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private readonly DashboardService _dashboardService;
        private Panel _mainPanel;
        private TableLayoutPanel _centeringPanel;
        private ComboBox _cboAnalyticsScope;
        private Label _dashboardTitle;
        private DashboardScope _currentScope = DashboardScope.Categories;

        private int _pendingMobileUpdatesCount;
        private ReaLTaiizor.Controls.HopeRoundButton _btnViewUpdates;
        private Label _lblUpdatesBadge;

        private void ApplyPieChartStyle(Chart chart)
        {
            if (chart == null)
                return;

            foreach (var s in chart.Series)
            {
                s.IsValueShownAsLabel = false;
                s.SmartLabelStyle.Enabled = false;
                if (s.ChartType == SeriesChartType.Pie)
                {
                    s["PieLabelStyle"] = "Disabled";
                    s["PieLineColor"] = "Transparent";
                }
                if (s.ChartType == SeriesChartType.Doughnut)
                {
                    s["DoughnutLabelStyle"] = "Disabled";
                    s["PieLabelStyle"] = "Disabled";
                    s["PieLineColor"] = "Transparent";
                }

                foreach (var p in s.Points)
                {
                    p.Label = string.Empty;
                }
            }

            foreach (var l in chart.Legends)
            {
                l.IsTextAutoFit = false;
            }
        }

        private void ApplyBarChartStyle(Chart chart)
        {
            if (chart == null)
                return;

            foreach (var area in chart.ChartAreas)
            {
                area.AxisX.Interval = 1;
                area.AxisX.LabelStyle.Angle = -90;
                area.AxisX.LabelStyle.IsStaggered = false;
                area.AxisX.IsLabelAutoFit = false;

                area.AxisX.MajorGrid.Enabled = false;
                area.AxisX.MinorGrid.Enabled = false;
                area.AxisY.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
                area.AxisY.MinorGrid.Enabled = false;

                area.AxisX.MajorTickMark.Enabled = false;
                area.AxisX.MinorTickMark.Enabled = false;
                area.AxisY.MinorTickMark.Enabled = false;

                area.AxisX.LineColor = Color.FromArgb(220, 220, 220);
                area.AxisY.LineColor = Color.FromArgb(220, 220, 220);
            }

            foreach (var s in chart.Series)
            {
                s.IsValueShownAsLabel = true;
                s.SmartLabelStyle.Enabled = true;
                s.SmartLabelStyle.AllowOutsidePlotArea = LabelOutsidePlotAreaStyle.Yes;
            }
        }

        // Navigation callbacks
        public Action OnViewInventoryClicked { get; set; }
        public Action OnViewUpdatesClicked { get; set; }
        public Action OnRefreshClicked { get; set; }

        public void SetMobileUpdatesPendingCount(int pendingCount)
        {
            if (this.IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SetMobileUpdatesPendingCount(pendingCount)));
                return;
            }

            _pendingMobileUpdatesCount = Math.Max(0, pendingCount);
            ApplyPendingUpdatesBadge();
        }

        public DashboardView()
        {
            _dashboardService = new DashboardService();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Main panel setup
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(240, 242, 245);
            this.Dock = DockStyle.Fill;
            this.Name = "DashboardView";
            this.Size = new Size(800, 600);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Public method to load dashboard with specific scope and filter.
        /// Data is fetched on a background thread so the UI never freezes.
        /// For Categories scope the data comes from the pre-aggregated
        /// DashboardCategoryCache table (fast flat SELECT).
        /// </summary>
        public async void LoadDashboard(DashboardScope scope, DashboardFilter filter = null)
        {
            _currentScope = scope;
            this.Controls.Clear();

            // Show a lightweight loading indicator while the DB call runs
            var lblLoading = new Label
            {
                Text      = "Loading dashboard…",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 11F),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblLoading);

            try
            {
                var data = await Task.Run(() => _dashboardService.GetDashboardData(scope, filter));
                this.Controls.Clear();
                RenderDashboard(data, filter);
            }
            catch (Exception ex)
            {
                this.Controls.Clear();
                var lblError = new Label
                {
                    Text      = $"Dashboard failed to load: {ex.Message}",
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(231, 76, 60),
                    Font      = new Font("Segoe UI", 10F)
                };
                this.Controls.Add(lblError);
                System.Diagnostics.Debug.WriteLine($"[DashboardView] LoadDashboard error: {ex}");
            }
        }

        /// <summary>
        /// Render dashboard UI from data (reusable for all scopes)
        /// </summary>
        private void RenderDashboard(DashboardData data, DashboardFilter filter)
        {
            // Store data reference for later use
            var _data = data;

            // Build UI
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(240, 242, 245),
                Padding = new Padding(0)
            };

            // Main centered container
            _centeringPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 6,
                Padding = new Padding(20, 20, 20, 30)
            };
            _centeringPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Left spacer
            _centeringPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Content
            _centeringPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Right spacer
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: Header
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: Scope Selector
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: Quick Actions
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 3: Summary Cards
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 4: Quick Stats
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 5: Charts
            _centeringPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 6: Data table

            // Add components
            AddHeader(data.Title);
            AddScopeSelector(filter);
            AddQuickActions();
            AddSummaryCards(data.Kpis);
            AddQuickStats(data.Stat1, data.Stat2, data.Stat3);
            AddCharts(data);
            AddDataTable(data);

            _mainPanel.Controls.Add(_centeringPanel);
            this.Controls.Add(_mainPanel);
        }

        private void AddHeader(string title)
        {
            var headerWrapper = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 10)
            };

            _dashboardTitle = new Label
            {
                Text = title ?? "📊 Dashboard",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(0, 0)
            };
            headerWrapper.Controls.Add(_dashboardTitle);
            _centeringPanel.Controls.Add(headerWrapper, 1, 0);
        }

        private void AddScopeSelector(DashboardFilter filter)
        {
            var selectorWrapper = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 20),
                Margin = new Padding(0)
            };

            var label = new Label
            {
                Text = "Analytics Scope:",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            };
            selectorWrapper.Controls.Add(label);

            _cboAnalyticsScope = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                Width = 250,
                Margin = new Padding(0, 2, 0, 0)
            };

            // Populate with enum values
            foreach (DashboardScope scope in Enum.GetValues(typeof(DashboardScope)))
            {
                _cboAnalyticsScope.Items.Add(new { Text = GetScopeDisplayName(scope), Value = scope });
            }
            _cboAnalyticsScope.DisplayMember = "Text";
            _cboAnalyticsScope.ValueMember = "Value";
            _cboAnalyticsScope.SelectedIndex = Array.IndexOf(Enum.GetValues(typeof(DashboardScope)), _currentScope);

            _cboAnalyticsScope.SelectedIndexChanged += (s, e) =>
            {
                if (_cboAnalyticsScope.SelectedItem != null)
                {
                    var selectedScope = (DashboardScope)((dynamic)_cboAnalyticsScope.SelectedItem).Value;
                    if (selectedScope != _currentScope)
                    {
                        LoadDashboard(selectedScope, filter);
                    }
                }
            };

            selectorWrapper.Controls.Add(_cboAnalyticsScope);
            _centeringPanel.Controls.Add(selectorWrapper, 1, 1);
        }

        private string GetScopeDisplayName(DashboardScope scope)
        {
            switch (scope)
            {
                case DashboardScope.Categories: return "📁 Categories";
                case DashboardScope.Items: return "📦 Items";
                case DashboardScope.InventoryMovements: return "📊 Inventory Movements";
                case DashboardScope.Requests: return "📝 Requests";
                case DashboardScope.Renewals: return "🔄 Renewals";
                case DashboardScope.InvoicesServices: return "💼 Invoices & Services";
                case DashboardScope.Employees: return "👥 Employees";
                case DashboardScope.Vendors: return "🏪 Vendors";
                case DashboardScope.ArchivedEntities: return "🗄️ Archived Entities";
                default: return scope.ToString();
            }
        }

        private void AddQuickActions()
        {
            var quickActionsWrapper = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 25)
            };

            var quickActionsPanel = CreateQuickActionsSection();
            quickActionsWrapper.Controls.Add(quickActionsPanel);
            _centeringPanel.Controls.Add(quickActionsWrapper, 1, 2);  // Row 2
        }

        private void AddSummaryCards(List<DashboardKpi> kpis)
        {
            var summaryRowWrapper = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 25),
                MinimumSize = new Size(1180, 0)
            };

            var summaryRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            foreach (var kpi in kpis)
            {
                summaryRow.Controls.Add(CreateSummaryCard(kpi.Title, kpi.Value.ToString(), kpi.AccentColor, kpi.Icon));
            }

            summaryRowWrapper.Controls.Add(summaryRow);
            _centeringPanel.Controls.Add(summaryRowWrapper, 1, 3);  // Row 3
        }

        private void AddQuickStats(DashboardQuickStat stat1, DashboardQuickStat stat2, DashboardQuickStat stat3)
        {
            var quickStatsWrapper = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 25),
                MinimumSize = new Size(1180, 0)
            };

            var quickStatsRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            if (stat1 != null)
                quickStatsRow.Controls.Add(CreateQuickStatBadge(stat1.Label, stat1.Value, stat1.Color));
            if (stat2 != null)
                quickStatsRow.Controls.Add(CreateQuickStatBadge(stat2.Label, stat2.Value, stat2.Color));
            if (stat3 != null)
                quickStatsRow.Controls.Add(CreateQuickStatBadge(stat3.Label, stat3.Value, stat3.Color));

            quickStatsWrapper.Controls.Add(quickStatsRow);
            _centeringPanel.Controls.Add(quickStatsWrapper, 1, 4);  // Row 4
        }

        private void AddCharts(DashboardData data)
        {
            var chartsWrapper = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 25)
            };

            var chartsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            chartsPanel.Controls.Add(CreatePieChart(data.PrimaryChartMetrics, data.PrimaryChartTitle));
            chartsPanel.Controls.Add(CreateBarChart(data.SecondaryChartMetrics, data.SecondaryChartTitle));
            chartsPanel.Controls.Add(CreateDonutChart(data.TertiaryChartMetrics, data.TertiaryChartTitle));

            chartsWrapper.Controls.Add(chartsPanel);
            _centeringPanel.Controls.Add(chartsWrapper, 1, 5);  // Row 5
        }

        private void AddDataTable(DashboardData data)
        {
            var topCategoriesWrapper = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 25)
            };

            var tablePanel = CreateGenericDataTable(data.TableData, data.TableTitle ?? "Data Table");
            topCategoriesWrapper.Controls.Add(tablePanel);
            _centeringPanel.Controls.Add(topCategoriesWrapper, 1, 6);  // Row 6
        }

        // ===== UI Component Builders =====

        private ReaLTaiizor.Controls.MaterialCard CreateQuickActionsSection()
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 1180,
                Height = 110,
                BackColor = Color.White,
                Margin = new Padding(0)
            };

            var headerBar = new Panel
            {
                Height = 4,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(52, 152, 219)
            };
            card.Controls.Add(headerBar);

            var titleLabel = new Label
            {
                Text = "⚡ QUICK ACTIONS",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(20, 15),
                AutoSize = true
            };
            card.Controls.Add(titleLabel);

            var actionsFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Location = new Point(20, 55),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            actionsFlow.Controls.Add(CreateQuickActionButton("View Inventory", Color.FromArgb(46, 204, 113), () => OnViewInventoryClicked?.Invoke()));
            actionsFlow.Controls.Add(CreateUpdatesQuickAction());
            actionsFlow.Controls.Add(CreateQuickActionButton("Refresh", Color.FromArgb(52, 152, 219), () => OnRefreshClicked?.Invoke()));

            card.Controls.Add(actionsFlow);
            return card;
        }

        private Control CreateUpdatesQuickAction()
        {
            var wrapper = new Panel
            {
                Size = new Size(150, 40),
                Margin = new Padding(0, 0, 10, 0),
                BackColor = Color.White
            };

            _btnViewUpdates = CreateQuickActionButton("View Updates", Color.FromArgb(155, 89, 182), () => OnViewUpdatesClicked?.Invoke());
            _btnViewUpdates.Location = new Point(0, 0);
            _btnViewUpdates.Margin = new Padding(0);
            wrapper.Controls.Add(_btnViewUpdates);

            _lblUpdatesBadge = new Label
            {
                AutoSize = false,
                Size = new Size(34, 18),
                BackColor = Color.OrangeRed,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _lblUpdatesBadge.Location = new Point(wrapper.Width - _lblUpdatesBadge.Width - 2, 2);
            wrapper.Controls.Add(_lblUpdatesBadge);
            _lblUpdatesBadge.BringToFront();

            ApplyPendingUpdatesBadge();
            return wrapper;
        }

        private void ApplyPendingUpdatesBadge()
        {
            if (_lblUpdatesBadge == null || _lblUpdatesBadge.IsDisposed)
                return;

            var pending = Math.Max(0, _pendingMobileUpdatesCount);
            if (pending <= 0)
            {
                _lblUpdatesBadge.Visible = false;
                _lblUpdatesBadge.Text = string.Empty;
                return;
            }

            _lblUpdatesBadge.Text = pending > 99 ? "99+" : pending.ToString();
            _lblUpdatesBadge.Visible = true;
        }

        private ReaLTaiizor.Controls.HopeRoundButton CreateQuickActionButton(string text, Color color, Action onClick)
        {
            var btn = new ReaLTaiizor.Controls.HopeRoundButton
            {
                Text = text,
                Size = new Size(150, 40),
                Margin = new Padding(0, 0, 10, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextColor = Color.White,
                PrimaryColor = color,
                DefaultColor = color,
                BorderColor = color,
                ButtonType = ReaLTaiizor.Util.HopeButtonType.Primary
            };
            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }

        private ReaLTaiizor.Controls.MaterialCard CreateSummaryCard(string title, string value, Color accentColor, string icon = "")
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 240,
                Height = 130,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 15, 0),
                Cursor = Cursors.Hand
            };

            var accentBar = new Panel
            {
                Height = 4,
                Dock = DockStyle.Top,
                BackColor = accentColor
            };
            card.Controls.Add(accentBar);

            if (!string.IsNullOrEmpty(icon))
            {
                var iconLabel = new Label
                {
                    Text = icon,
                    Font = new Font("Segoe UI", 28F),
                    Location = new Point(175, 45),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 200, 200)
                };
                card.Controls.Add(iconLabel);
            }

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location = new Point(15, 15),
                AutoSize = true
            };
            card.Controls.Add(lblTitle);

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = accentColor,
                Location = new Point(15, 40),
                AutoSize = true
            };
            card.Controls.Add(lblValue);

            // Add click handling for drill-down using helper method
            // This ensures ALL child controls respond to clicks
            AttachClickHandlerRecursive(card, title, Color.White);

            // Add tooltip
            var tooltip = new ToolTip();
            tooltip.SetToolTip(card, $"Click to view {title} details");

            return card;
        }

        private ReaLTaiizor.Controls.MaterialCard CreateQuickStatBadge(string label, string value, Color color)
        {
            var badge = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 220,
                Height = 105,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 15, 0),
                Cursor = Cursors.Hand
            };

            var leftBar = new Panel
            {
                Width = 5,
                Dock = DockStyle.Left,
                BackColor = color
            };
            badge.Controls.Add(leftBar);

            var lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(18, 12),
                AutoSize = true
            };
            badge.Controls.Add(lblLabel);

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = color,
                Location = new Point(18, 34),
                AutoSize = true
            };
            badge.Controls.Add(lblValue);

            // Add click handling for drill-down using helper method
            // This ensures ALL child controls respond to clicks
            AttachClickHandlerRecursive(badge, label, Color.White);

            // Add tooltip
            var tooltip = new ToolTip();
            tooltip.SetToolTip(badge, $"Click to view {label} items");

            return badge;
        }

        /// <summary>
        /// Attach click handler recursively to a control and all its children
        /// This ensures clicking ANYWHERE on a summary card triggers the drill-down
        /// PHASE 2 FIX: Makes entire card surface clickable
        /// </summary>
        /// <param name="control">Root control (e.g., MaterialCard)</param>
        /// <param name="title">Card title for drill-down routing</param>
        /// <param name="originalBackColor">Original background color for hover effect</param>
        private void AttachClickHandlerRecursive(Control control, string title, Color originalBackColor)
        {
            // Attach click handler to this control
            control.Click += (s, e) => OnSummaryCardClick(title);
            control.Cursor = Cursors.Hand;

            // Add hover effect to root control only
            if (control is ReaLTaiizor.Controls.MaterialCard)
            {
                control.MouseEnter += (s, e) =>
                {
                    control.BackColor = Color.FromArgb(245, 247, 250);
                };
                control.MouseLeave += (s, e) =>
                {
                    control.BackColor = originalBackColor;
                };
            }

            // Recursively attach to all child controls
            foreach (Control child in control.Controls)
            {
                AttachClickHandlerRecursive(child, title, originalBackColor);
            }
        }

        /// <summary>
        /// Generate visually distinct colors from a predefined diverse palette
        /// </summary>
        private Color[] GenerateDistinctColors(int count)
        {
            // High-contrast, vibrant dashboard palette with distinct color families
            var distinctPalette = new Color[]
            {
                Color.FromArgb(52, 152, 219),   // Bright Blue
                Color.FromArgb(46, 204, 113),   // Vibrant Green
                Color.FromArgb(231, 76, 60),    // Clear Red
                Color.FromArgb(241, 196, 15),   // Bright Yellow
                Color.FromArgb(155, 89, 182),   // Rich Purple
                Color.FromArgb(230, 126, 34),   // Orange
                Color.FromArgb(26, 188, 156),    // Teal
                Color.FromArgb(255, 159, 64),    // Coral
                Color.FromArgb(91, 134, 229),    // Indigo
                Color.FromArgb(0, 184, 148),     // Emerald
                Color.FromArgb(255, 99, 132),     // Pink
                Color.FromArgb(106, 176, 76),    // Lime Green
                Color.FromArgb(255, 206, 86),    // Amber
                Color.FromArgb(123, 104, 238),   // Periwinkle
                Color.FromArgb(0, 150, 136),      // Dark Turquoise
                Color.FromArgb(255, 121, 63),    // Deep Orange
                Color.FromArgb(74, 144, 226),    // Sky Blue
                Color.FromArgb(168, 85, 247),    // Lavender
                Color.FromArgb(255, 71, 87),     // Rose
                Color.FromArgb(34, 197, 94)      // Forest Green
            };

            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                colors[i] = distinctPalette[i % distinctPalette.Length];
            }
            return colors;
        }

        private ReaLTaiizor.Controls.MaterialCard CreatePieChart(List<DashboardMetric> metrics, string title)
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 450,
                Height = 380,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 20, 0)
            };

            var titleLabel = new Label
            {
                Text = title ?? "DISTRIBUTION",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(15, 15),
                AutoSize = true
            };
            card.Controls.Add(titleLabel);

            var chart = new Chart
            {
                Width = 420,
                Height = 310,
                Location = new Point(15, 50),
                BackColor = Color.White,
                Tag = title
            };

            var chartArea = new ChartArea();
            chartArea.BackColor = Color.White;
            chartArea.Area3DStyle.Enable3D = false;
            chartArea.Position.Auto = false;
            chartArea.Position.X = 10;
            chartArea.Position.Y = 2;
            chartArea.Position.Width = 80;
            chartArea.Position.Height = 58;
            chart.ChartAreas.Add(chartArea);

            var series = new Series
            {
                Name = "Categories",
                ChartType = SeriesChartType.Pie,
                Font = new Font("Segoe UI", 8F)
            };

            // Generate visually distinct colors dynamically
            var colors = GenerateDistinctColors(metrics.Count);

            for (int i = 0; i < metrics.Count; i++)
            {
                var point = series.Points.Add(metrics[i].Value);
                point.Label = string.Empty;
                point.LegendText = $"{metrics[i].Label} ({metrics[i].Value})";
                point.Color = colors[i];
                // Store category name in AxisLabel for drill-down
                point.AxisLabel = metrics[i].Label;
                point.Tag = metrics[i].Label;
            }

            chart.Series.Add(series);

            var legend = new Legend
            {
                Docking = Docking.Bottom,
                Alignment = StringAlignment.Center,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 8F)
            };
            chart.Legends.Add(legend);

            ApplyPieChartStyle(chart);

            // Add drill-down click handling
            chart.MouseClick += OnChartClick;
            chart.MouseMove += OnChartMouseMove;
            chart.Cursor = Cursors.Hand;

            // Add tooltip
            var tooltip = new ToolTip();
            tooltip.SetToolTip(chart, "Click on a slice to view detailed items");

            card.Controls.Add(chart);
            return card;
        }

        private ReaLTaiizor.Controls.MaterialCard CreateBarChart(List<DashboardMetric> metrics, string title)
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 500,
                Height = 350,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 20, 0)
            };

            var titleLabel = new Label
            {
                Text = title ?? "LEVELS",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(15, 15),
                AutoSize = true
            };
            card.Controls.Add(titleLabel);

            var chart = new Chart
            {
                Width = 470,
                Height = 280,
                Location = new Point(15, 50),
                BackColor = Color.White,
                Tag = title
            };

            var chartArea = new ChartArea();
            chartArea.BackColor = Color.White;
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            chart.ChartAreas.Add(chartArea);

            var series = new Series
            {
                Name = "Active Stock",
                ChartType = SeriesChartType.Column,
                Color = Color.FromArgb(52, 152, 219),
                Font = new Font("Segoe UI", 8F)
            };

            foreach (var metric in metrics)
            {
                var point = series.Points.AddXY(metric.Label, metric.Value);
                series.Points[series.Points.Count - 1].Label = metric.Value.ToString();
                // Store category name in AxisLabel for drill-down
                series.Points[series.Points.Count - 1].AxisLabel = metric.Label;
                series.Points[series.Points.Count - 1].Tag = metric.Label;
            }

            chart.Series.Add(series);
            ApplyBarChartStyle(chart);

            // Add drill-down click handling
            chart.MouseClick += OnChartClick;
            chart.MouseMove += OnChartMouseMove;
            chart.Cursor = Cursors.Hand;

            // Add tooltip
            var tooltip = new ToolTip();
            tooltip.SetToolTip(chart, "Click on a bar to view detailed items");

            card.Controls.Add(chart);
            return card;
        }

        private ReaLTaiizor.Controls.MaterialCard CreateDonutChart(List<DashboardMetric> metrics, string title)
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 380,
                Height = 350,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 20, 0)
            };

            var titleLabel = new Label
            {
                Text = title ?? "BREAKDOWN",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(15, 15),
                AutoSize = true
            };
            card.Controls.Add(titleLabel);

            var chart = new Chart
            {
                Width = 350,
                Height = 280,
                Location = new Point(15, 50),
                BackColor = Color.White,
                Tag = title
            };

            var chartArea = new ChartArea();
            chartArea.BackColor = Color.White;
            chart.ChartAreas.Add(chartArea);

            var series = new Series
            {
                Name = "Condition",
                ChartType = SeriesChartType.Doughnut,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            var colors = new[] {
                Color.FromArgb(46, 204, 113),
                Color.FromArgb(231, 76, 60),
                Color.FromArgb(52, 152, 219),
                Color.FromArgb(241, 196, 15)
            };

            for (int i = 0; i < metrics.Count && i < colors.Length; i++)
            {
                var point = series.Points.Add(metrics[i].Value);
                point.Label = string.Empty;
                point.LegendText = $"{metrics[i].Label} ({metrics[i].Value})";
                point.Color = colors[i];
                // Store label in AxisLabel for drill-down (e.g., "Good", "Damaged")
                point.AxisLabel = metrics[i].Label;
                point.Tag = metrics[i].Label;
            }
            series["DoughnutRadius"] = "60";
            chart.Series.Add(series);

            var legend = new Legend
            {
                Docking = Docking.Bottom,
                Alignment = StringAlignment.Center,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            chart.Legends.Add(legend);

            ApplyPieChartStyle(chart);

            // Add drill-down click handling
            chart.MouseClick += OnChartClick;
            chart.MouseMove += OnChartMouseMove;
            chart.Cursor = Cursors.Hand;

            // Add tooltip
            var tooltip = new ToolTip();
            tooltip.SetToolTip(chart, "Click on a segment to view detailed items");

            card.Controls.Add(chart);
            return card;
        }

        /// <summary>
        /// Get proper column header text based on current scope and column number
        /// </summary>
        private string GetColumnHeader(int columnNumber)
        {
            switch (_currentScope)
            {
                case DashboardScope.Categories:
                    switch (columnNumber)
                    {
                        case 1: return "Category Name";
                        case 2: return "Active Stock";
                        case 3: return "Good";
                        case 4: return "Damaged";
                        case 5: return "Item Records";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Items:
                    switch (columnNumber)
                    {
                        case 1: return "Item Name";
                        case 2: return "Category";
                        case 3: return "Item Type";
                        case 4: return "Stock";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.InventoryMovements:
                    switch (columnNumber)
                    {
                        case 1: return "Date";
                        case 2: return "Item Name";
                        case 3: return "Category";
                        case 4: return "Entry Type";
                        case 5: return "Stock +/-";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Requests:
                    switch (columnNumber)
                    {
                        case 1: return "Date Requested";
                        case 2: return "Employee";
                        case 3: return "Item Name";
                        case 4: return "Category";
                        case 5: return "Quantity";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Renewals:
                    switch (columnNumber)
                    {
                        case 1: return "Item Name";
                        case 2: return "Invoice Type";
                        case 3: return "Renewal Start Date";
                        case 4: return "Renewal End Date";
                        case 5: return "Days Before Expiry";
                        case 6: return "Renewal Status";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.InvoicesServices:
                    switch (columnNumber)
                    {
                        case 1: return "Set Code";
                        case 2: return "Document Number";
                        case 3: return "Invoice Type";
                        case 4: return "Date Created";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Employees:
                    switch (columnNumber)
                    {
                        case 1: return "Employee Name";
                        case 2: return "Position";
                        case 3: return "Department";
                        case 4: return "Branch";
                        case 5: return "Company";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Vendors:
                    switch (columnNumber)
                    {
                        case 1: return "Vendor Name";
                        case 2: return "Item Quantity";
                        case 3: return "Invoices";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.ArchivedEntities:
                    switch (columnNumber)
                    {
                        case 1: return "Table";
                        case 2: return "Data Name";
                        case 4: return "Archive Date";
                        case 5: return "Archived By";
                        default: return $"Column {columnNumber}";
                    }

                default:
                    return $"Column {columnNumber}";
            }
        }

        private ReaLTaiizor.Controls.MaterialCard CreateGenericDataTable(List<DashboardTableRow> rows, string tableTitle)
        {
            if (rows == null || rows.Count == 0)
            {
                return new ReaLTaiizor.Controls.MaterialCard { Width = 1180, Height = 100, BackColor = Color.White, Margin = new Padding(0) };
            }

            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 1180,
                Height = 520,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 20)
            };

            var headerPanel = new Panel
            {
                Width = 1180,
                Height = 60,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(52, 152, 219)
            };

            var titleLabel = new Label
            {
                Text = tableTitle,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 18),
                AutoSize = true
            };
            headerPanel.Controls.Add(titleLabel);

            var countLabel = new Label
            {
                Text = $"Showing {rows.Count} Records",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(230, 240, 255),
                Location = new Point(900, 20),
                AutoSize = true
            };
            headerPanel.Controls.Add(countLabel);
            card.Controls.Add(headerPanel);

            var scrollPanel = new Panel
            {
                Width = 1150,
                Height = 430,
                Location = new Point(15, 75),
                AutoScroll = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };

            var dgv = new ReaLTaiizor.Controls.PoisonDataGridView
            {
                Width = 1130,
                Height = rows.Count * 42 + 55,
                Location = new Point(0, 0),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 50,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                RowTemplate = { Height = 42 },
                GridColor = Color.FromArgb(240, 240, 240),
                Theme = ReaLTaiizor.Enum.Poison.ThemeStyle.Light,
                Style = ReaLTaiizor.Enum.Poison.ColorStyle.Blue,
                MultiSelect = false
            };

            // Dynamically add columns based on first row
            bool hasCol1 = rows.Any(r => !string.IsNullOrEmpty(r.Col1));
            bool hasCol2 = rows.Any(r => !string.IsNullOrEmpty(r.Col2));
            bool hasCol3 = rows.Any(r => !string.IsNullOrEmpty(r.Col3));
            bool hasCol4 = rows.Any(r => !string.IsNullOrEmpty(r.Col4));
            bool hasCol5 = rows.Any(r => !string.IsNullOrEmpty(r.Col5));
            bool hasCol6 = rows.Any(r => !string.IsNullOrEmpty(r.Col6));
            bool hasCol7 = rows.Any(r => !string.IsNullOrEmpty(r.Col7));
            bool hasStatus = rows.Any(r => !string.IsNullOrEmpty(r.StatusBadge));

            // Skip Col3 and Col6 for ArchivedEntities (Entity ID and Archive Reason)
            bool skipCol3 = _currentScope == DashboardScope.ArchivedEntities;
            bool skipCol6 = _currentScope == DashboardScope.ArchivedEntities;

            if (hasCol1) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col1", HeaderText = GetColumnHeader(1), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            if (hasCol2) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col2", HeaderText = GetColumnHeader(2), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            if (hasCol3 && !skipCol3) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col3", HeaderText = GetColumnHeader(3), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            if (hasCol4) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col4", HeaderText = GetColumnHeader(4), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            if (hasCol5) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col5", HeaderText = GetColumnHeader(5), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            if (hasCol6 && !skipCol6) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col6", HeaderText = GetColumnHeader(6), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            if (hasCol7) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Col7", HeaderText = GetColumnHeader(7), DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            if (hasStatus) dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.EnableHeadersVisualStyles = false;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 230, 255);
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            foreach (var row in rows)
            {
                var values = new List<object>();
                if (hasCol1) values.Add(row.Col1 ?? "");
                if (hasCol2) values.Add(row.Col2 ?? "");
                if (hasCol3 && !skipCol3) values.Add(row.Col3 ?? "");
                if (hasCol4) values.Add(row.Col4 ?? "");
                if (hasCol5) values.Add(row.Col5 ?? "");
                if (hasCol6 && !skipCol6) values.Add(row.Col6 ?? "");
                if (hasCol7) values.Add(row.Col7 ?? "");
                if (hasStatus) values.Add(row.StatusBadge ?? "");

                var rowIndex = dgv.Rows.Add(values.ToArray());

                if (rowIndex % 2 == 0)
                {
                    dgv.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
                }

                if (!string.IsNullOrEmpty(row.StatusBadge) && row.StatusColor != Color.Empty)
                {
                    dgv.Rows[rowIndex].Cells["Status"].Style.ForeColor = row.StatusColor;
                    dgv.Rows[rowIndex].Cells["Status"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
            }

            scrollPanel.Controls.Add(dgv);
            card.Controls.Add(scrollPanel);

            dgv.ClearSelection();

            return card;
        }

        // Legacy method for backward compatibility - now calls generic table
        private ReaLTaiizor.Controls.MaterialCard CreateTopCategoriesSection(List<CategoryDashboardData> summaries)
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Width = 1180,
                Height = 520,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 20)
            };

            var headerPanel = new Panel
            {
                Width = 1180,
                Height = 60,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(52, 152, 219)
            };

            var titleLabel = new Label
            {
                Text = "📋 TOP CATEGORIES BY STOCK",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 18),
                AutoSize = true
            };
            headerPanel.Controls.Add(titleLabel);

            var countLabel = new Label
            {
                Text = $"Showing All {summaries.Count} Categories",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(230, 240, 255),
                Location = new Point(900, 20),
                AutoSize = true
            };
            headerPanel.Controls.Add(countLabel);
            card.Controls.Add(headerPanel);

            var scrollPanel = new Panel
            {
                Width = 1150,
                Height = 430,
                Location = new Point(15, 75),
                AutoScroll = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };

            var dgv = new ReaLTaiizor.Controls.PoisonDataGridView
            {
                Width = 1130,
                Height = summaries.Count * 42 + 55,
                Location = new Point(0, 0),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 50,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                RowHeadersVisible = false,
                RowTemplate = { Height = 42 },
                GridColor = Color.FromArgb(240, 240, 240),
                Theme = ReaLTaiizor.Enum.Poison.ThemeStyle.Light,
                Style = ReaLTaiizor.Enum.Poison.ColorStyle.Blue,
                MultiSelect = false
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rank", HeaderText = "#", Width = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "CategoryName", HeaderText = "Category Name", Width = 220, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActiveStock", HeaderText = "Active Stock", Width = 140, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10F, FontStyle.Bold) } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "GoodCount", HeaderText = "Good ✓", Width = 140, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(46, 204, 113) } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DamagedCount", HeaderText = "Damaged ⚠", Width = 150, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(231, 76, 60) } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalItems", HeaderText = "Item Records", Width = 140, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10F, FontStyle.Bold) } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "StockStatus", HeaderText = "Status", Width = 150, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.EnableHeadersVisualStyles = false;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 230, 255);
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            int rank = 1;
            foreach (var cat in summaries.OrderByDescending(s => s.ActiveStock))
            {
                string status = cat.ActiveStock <= 0 ? "🔴 Out of Stock" :
                               cat.ActiveStock < 5 ? "🟡 Low Stock" :
                               "🟢 In Stock";

                var rowIndex = dgv.Rows.Add(rank++, cat.CategoryName, cat.ActiveStock, cat.GoodCount, cat.DamagedCount, cat.TotalItems, status);

                if (rowIndex % 2 == 0)
                {
                    dgv.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
                }

                if (cat.ActiveStock <= 0)
                {
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.BackColor = Color.FromArgb(255, 230, 230);
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.ForeColor = Color.FromArgb(200, 50, 50);
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else if (cat.ActiveStock < 5)
                {
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.BackColor = Color.FromArgb(255, 250, 205);
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.ForeColor = Color.FromArgb(180, 140, 0);
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else
                {
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.BackColor = Color.FromArgb(230, 255, 230);
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.ForeColor = Color.FromArgb(40, 150, 60);
                    dgv.Rows[rowIndex].Cells["StockStatus"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }

                if (rank <= 4)
                {
                    dgv.Rows[rowIndex].Cells["Rank"].Style.BackColor = Color.FromArgb(255, 215, 0);
                    dgv.Rows[rowIndex].Cells["Rank"].Style.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
                    dgv.Rows[rowIndex].Cells["Rank"].Style.ForeColor = Color.FromArgb(139, 69, 0);
                }
            }

            scrollPanel.Controls.Add(dgv);
            card.Controls.Add(scrollPanel);

            dgv.ClearSelection();

            return card;
        }

        private void ShowNotImplementedMessage(DashboardScope scope)
        {
            var message = new Label
            {
                Text = $"{scope} dashboard is not yet implemented.\nPlease use the Categories dashboard.",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(50, 50)
            };
            this.Controls.Add(message);
        }

        // ===== DRILL-DOWN FUNCTIONALITY =====

        /// <summary>
        /// Handle chart clicks for drill-down functionality
        /// Uses HitTest to detect clicks on chart elements (pie slices, bars, etc.)
        /// </summary>
        private void OnChartClick(object sender, MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[OnChartClick] ENTRY - Chart clicked");
            
            if (!(sender is Chart chart))
            {
                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Sender is not a Chart, exiting");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[OnChartClick] Performing hit test...");
            // Perform hit test to detect clicked element
            var result = chart.HitTest(e.X, e.Y);

            // Only proceed if a data point was clicked
            if (result.ChartElementType == ChartElementType.DataPoint && result.PointIndex >= 0 && result.Series != null)
            {
                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Data point clicked");
                var series = result.Series;
                var point = series.Points[result.PointIndex];

                // Extract category/label from the clicked point
                string categoryName = (point.Tag as string) ?? point.AxisLabel;
                if (string.IsNullOrWhiteSpace(categoryName) && !string.IsNullOrWhiteSpace(point.LegendText))
                {
                    var legendText = point.LegendText;
                    var parenIndex = legendText.LastIndexOf(" (", StringComparison.Ordinal);
                    categoryName = parenIndex > 0 ? legendText.Substring(0, parenIndex) : legendText;
                }
                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Category: {categoryName}, Scope: {_currentScope}");

                if (!string.IsNullOrEmpty(categoryName))
                {
                    // Chart axis labels can contain line breaks/extra whitespace depending on rotation/auto-fit.
                    categoryName = categoryName
                        .Replace("\r", " ")
                        .Replace("\n", " ")
                        .Trim();

                    // Collapse multiple spaces so it matches DB values more reliably.
                    while (categoryName.Contains("  "))
                        categoryName = categoryName.Replace("  ", " ");
                }

                if (string.IsNullOrEmpty(categoryName))
                {
                    System.Diagnostics.Debug.WriteLine($"[OnChartClick] Category name is empty, exiting");
                    return;
                }

                // Map current scope to DrillDownContext
                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Mapping scope to context...");
                DrillDownContext? context = MapScopeToContext(_currentScope);
                if (!context.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[OnChartClick] No context mapping found, exiting");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Mapped to context: {context.Value}");
                
                // Build new drill-down request
                // For chart clicks, pass the clicked label for selective drill-down on supported dashboards
                var effectiveContext = context.Value;
                string filterKey = null;

                var chartTitle = (chart.Tag as string) ?? string.Empty;

                // Scope-specific mappings so drill-down shows the underlying records the user expects.
                // Charts reuse labels like "Good", "Damaged", "Yes", "No" across different scopes.
                if (_currentScope == DashboardScope.Categories)
                {
                    // Categories charts should drill into *Items*, not category summary rows.
                    effectiveContext = DrillDownContext.Items;

                    if (series.ChartType == SeriesChartType.Doughnut)
                    {
                        if (string.Equals(categoryName, "Good", StringComparison.OrdinalIgnoreCase))
                            filterKey = "Good Condition";
                        else if (string.Equals(categoryName, "Damaged", StringComparison.OrdinalIgnoreCase))
                            filterKey = "Damaged Items";
                        else
                            filterKey = categoryName;
                    }
                    else
                    {
                        // Pie/Bar segments are category names.
                        filterKey = categoryName;
                    }
                }
                else if (_currentScope == DashboardScope.InvoicesServices && series.ChartType == SeriesChartType.Doughnut)
                {
                    // Invoices donut uses Yes/No -> Approved/Not Approved.
                    if (string.Equals(categoryName, "Yes", StringComparison.OrdinalIgnoreCase))
                        filterKey = "Approved";
                    else if (string.Equals(categoryName, "No", StringComparison.OrdinalIgnoreCase))
                        filterKey = "Not Approved";
                    else
                        filterKey = categoryName;
                }
                else if (_currentScope == DashboardScope.ArchivedEntities && series.ChartType == SeriesChartType.Doughnut)
                {
                    // Archived donut uses Items/Others.
                    filterKey = categoryName;
                }
                else if (_currentScope == DashboardScope.Renewals)
                {
                    // Renewals bar chart labels are item names (Expiring Soon). Prefix to let service interpret correctly.
                    if (!string.IsNullOrWhiteSpace(chartTitle) &&
                        chartTitle.IndexOf("EXPIRING", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filterKey = $"Item:{categoryName}";
                    }
                }
                else if (_currentScope == DashboardScope.Requests)
                {
                    // Requests bar chart labels are item names (Top Requested Items)
                    if (!string.IsNullOrWhiteSpace(chartTitle) &&
                        chartTitle.IndexOf("TOP REQUEST", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filterKey = $"Item:{categoryName}";
                    }
                }

                // Default behavior: pass label through as filter key for supported contexts.
                if (filterKey == null &&
                    (effectiveContext == DrillDownContext.Categories ||
                     effectiveContext == DrillDownContext.Items ||
                     effectiveContext == DrillDownContext.Movements ||
                     effectiveContext == DrillDownContext.Vendors ||
                     effectiveContext == DrillDownContext.Employees ||
                     effectiveContext == DrillDownContext.Requests) &&
                    !string.IsNullOrEmpty(categoryName))
                {
                    // Items dashboard: the "TOP ITEMS BY CATEGORY" bar chart uses item names as labels.
                    // Prefix to disambiguate from category-name filtering in the generic Items drill-down.
                    if (effectiveContext == DrillDownContext.Items &&
                        !string.IsNullOrWhiteSpace(chartTitle) &&
                        chartTitle.IndexOf("TOP ITEMS", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filterKey = $"Item:{categoryName}";
                    }
                    else
                    {
                        filterKey = categoryName;
                    }
                }
                 
                var request = new DrillDownRequest
                {
                    Context = effectiveContext,
                    FilterKey = filterKey
                };

                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Calling ShowNewDrillDownDialog with FilterKey={filterKey}...");
                ShowNewDrillDownDialog(request);
                System.Diagnostics.Debug.WriteLine($"[OnChartClick] EXIT - Dialog closed");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[OnChartClick] Not a data point click, exiting");
            }
        }

        /// <summary>
        /// Handle chart clicks specifically for Inventory Movements dashboard
        /// CRITICAL: Fixed Assets are excluded from all movement drill-downs
        /// </summary>
        private void HandleMovementChartClick(Series series, string categoryName, string chartTitle)
        {
            GenericDrillDownRequest request = null;

            // Determine drill-down type based on chart title and series name
            var normalizedTitle = (chartTitle ?? string.Empty).Trim();

            // MOVEMENTS BY ENTRY TYPE (pie) -> entry type drill-down
            if (!string.IsNullOrWhiteSpace(normalizedTitle) &&
                normalizedTitle.IndexOf("ENTRY TYPE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                request = new GenericDrillDownRequest
                {
                    Context = LegacyDrillDownContext.MovementsByEntryType,
                    FilterValue = categoryName,
                    Scope = _currentScope,
                    DisplayTitle = $"Entry Type: {categoryName} - Movements"
                };
            }
            // TOP CATEGORY BY MOVEMENT (bar) -> category drill-down
            else if (!string.IsNullOrWhiteSpace(normalizedTitle) &&
                     normalizedTitle.IndexOf("CATEGORY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                request = new GenericDrillDownRequest
                {
                    Context = LegacyDrillDownContext.MovementsByCategory,
                    FilterValue = categoryName,
                    Scope = _currentScope,
                    DisplayTitle = $"Category: {categoryName} - Movements"
                };
            }
            else if (series.Name == "Active Stock" || series.Name == "Categories")
            {
                // Bar chart or Pie chart - category-based drill-down
                request = new GenericDrillDownRequest
                {
                    Context = LegacyDrillDownContext.MovementsByCategory,
                    FilterValue = categoryName,
                    Scope = _currentScope,
                    DisplayTitle = $"Category: {categoryName} - Movements"
                };
            }
            else if (series.Name == "Condition")
            {
                // Donut chart - entry type drill-down
                if (string.Equals(categoryName, "Positive", StringComparison.OrdinalIgnoreCase))
                {
                    request = new GenericDrillDownRequest
                    {
                        Context = LegacyDrillDownContext.MovementsByEntryType,
                        FilterValue = "Positive",
                        Scope = _currentScope,
                        DisplayTitle = "Positive Movements"
                    };
                }
                else if (string.Equals(categoryName, "Negative", StringComparison.OrdinalIgnoreCase))
                {
                    request = new GenericDrillDownRequest
                    {
                        Context = LegacyDrillDownContext.MovementsByEntryType,
                        FilterValue = "Negative",
                        Scope = _currentScope,
                        DisplayTitle = "Negative Movements"
                    };
                }
            }

            if (request != null)
            {
                ShowGenericDrillDownDialog(request);
            }
        }

        /// <summary>
        /// Handle mouse movement over chart to change cursor when hovering over clickable elements
        /// </summary>
        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            if (!(sender is Chart chart))
                return;

            var result = chart.HitTest(e.X, e.Y);

            if (result.ChartElementType == ChartElementType.DataPoint)
            {
                chart.Cursor = Cursors.Hand;
            }
            else
            {
                chart.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Handle summary card clicks for drill-down functionality
        /// </summary>
        private void OnSummaryCardClick(string cardTitle)
        {
            // Map current scope to DrillDownContext
            DrillDownContext? context = MapScopeToContext(_currentScope);
            if (!context.HasValue)
                return;

            // Map card title to filter key
            string filterKey = null;
            switch (cardTitle.ToLower())
            {
                case "total movements":
                case "out of stock":
                case "low stock":
                case "in stock":
                case "good condition":
                case "damaged items":
                    filterKey = cardTitle;
                    break;
                case "total quantity":
                case "total items":
                case "total categories":
                case "total employees":
                case "total requests":
                case "total renewals":
                case "total invoices":
                case "total vendors":
                case "total archived":
                    filterKey = null; // Show all
                    break;
                default:
                    filterKey = cardTitle; // Pass through as-is
                    break;
            }

            var request = new DrillDownRequest
            {
                Context = context.Value,
                FilterKey = filterKey
            };

            ShowNewDrillDownDialog(request);
        }

        /// <summary>
        /// Handle summary card clicks specifically for Inventory Movements dashboard
        /// CRITICAL: Fixed Assets are excluded from all movement drill-downs
        /// </summary>
        private void HandleMovementCardClick(string cardTitle)
        {
            GenericDrillDownRequest request = null;

            switch (cardTitle.ToLower())
            {
                case "total movements":
                    request = new GenericDrillDownRequest
                    {
                        Context = LegacyDrillDownContext.AllMovements,
                        Scope = _currentScope,
                        DisplayTitle = "All Movements"
                    };
                    break;

                case "positive":
                case "qty in":
                    request = new GenericDrillDownRequest
                    {
                        Context = LegacyDrillDownContext.MovementsByEntryType,
                        FilterValue = "Positive",
                        Scope = _currentScope,
                        DisplayTitle = "Positive Movements"
                    };
                    break;

                case "negative":
                case "qty out":
                    request = new GenericDrillDownRequest
                    {
                        Context = LegacyDrillDownContext.MovementsByEntryType,
                        FilterValue = "Negative",
                        Scope = _currentScope,
                        DisplayTitle = "Negative Movements"
                    };
                    break;
            }

            if (request != null)
            {
                ShowGenericDrillDownDialog(request);
            }
        }

        /// <summary>
        /// Query the service and display drill-down results in a modal dialog
        /// CENTRALIZED: All drill-down logic goes through this method
        /// </summary>
        private void ShowDrillDownDialog(DashboardDrillDownRequest request)
        {
            try
            {
                // Fetch filtered data from service
                var data = _dashboardService.GetDrillDownItems(request);

                // Show dialog
                var dialog = new DashboardDrillDownDialog();
                dialog.SetData(data, request.DisplayTitle);
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading drill-down data: {ex.Message}",
                    "Drill-Down Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Query the service and display movement drill-down results in a modal dialog
        /// CENTRALIZED: All movement drill-down logic goes through this method
        /// CRITICAL: Fixed Assets are excluded from all movement drill-downs
        /// </summary>
        private void ShowMovementDrillDownDialog(MovementDrillDownRequest request)
        {
            try
            {
                // Fetch filtered movement data from service
                var data = _dashboardService.GetMovementDrillDownData(request);

                // Show dialog
                var dialog = new DashboardDrillDownDialog();
                dialog.SetData(data, request.DisplayTitle);
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading movement drill-down data: {ex.Message}",
                    "Movement Drill-Down Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Query the service and display drill-down results using DTO-based approach (NEW)
        /// CENTRALIZED: All drill-down logic goes through this method
        /// Uses dynamic DTO binding for flexible column layouts
        /// </summary>
        private void ShowGenericDrillDownDialog(GenericDrillDownRequest request)
        {
            try
            {
                // Fetch filtered data from service (returns List<TDto>)
                var dtoList = _dashboardService.GetDrillDownDataAsDto(request);

                // Show dialog with DTO-based binding
                var dialog = new DashboardDrillDownDialog();
                dialog.SetDataFromDto(dtoList, request.DisplayTitle);
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading drill-down data: {ex.Message}",
                    "Drill-Down Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Map DashboardScope to DrillDownContext
        /// </summary>
        private DrillDownContext? MapScopeToContext(DashboardScope scope)
        {
            switch (scope)
            {
                case DashboardScope.Categories:
                    return DrillDownContext.Categories;
                case DashboardScope.Items:
                    return DrillDownContext.Items;
                case DashboardScope.InventoryMovements:
                    return DrillDownContext.Movements;
                case DashboardScope.Requests:
                    return DrillDownContext.Requests;
                case DashboardScope.Renewals:
                    return DrillDownContext.Renewals;
                case DashboardScope.InvoicesServices:
                    return DrillDownContext.Invoices;
                case DashboardScope.Employees:
                    return DrillDownContext.Employees;
                case DashboardScope.Vendors:
                    return DrillDownContext.Vendors;
                case DashboardScope.ArchivedEntities:
                    return DrillDownContext.Archived;
                default:
                    return null;
            }
        }

        /// <summary>
        /// NEW: Show drill-down using simplified DrillDownRequest that mirrors View page data
        /// </summary>
        private void ShowNewDrillDownDialog(DrillDownRequest request)
        {
            var originalCursor = this.Cursor;
            try
            {
                this.Cursor = Cursors.WaitCursor;
                System.Diagnostics.Debug.WriteLine($"[DrillDown] Starting: Context={request.Context}, FilterKey={request.FilterKey}");

                // Fetch data from service on background thread to avoid UI deadlock
                System.Diagnostics.Debug.WriteLine($"[DrillDown] Fetching data on background thread...");
                var data = System.Threading.Tasks.Task.Run(() => _dashboardService.GetDrillDownItems(request)).Result;
                System.Diagnostics.Debug.WriteLine($"[DrillDown] Data fetched successfully");

                // Build dynamic title
                string title = $"{request.Context}";
                if (!string.IsNullOrWhiteSpace(request.FilterKey))
                    title += $" – {request.FilterKey}";

                System.Diagnostics.Debug.WriteLine($"[DrillDown] Creating dialog...");
                // Show dialog with DTO-based binding
                var dialog = new DashboardDrillDownDialog();
                dialog.SetDataFromDto(data, title);
                
                this.Cursor = originalCursor;
                System.Diagnostics.Debug.WriteLine($"[DrillDown] Showing dialog...");
                dialog.ShowDialog(this);
                System.Diagnostics.Debug.WriteLine($"[DrillDown] Dialog closed");
            }
            catch (Exception ex)
            {
                this.Cursor = originalCursor;
                System.Diagnostics.Debug.WriteLine($"[DrillDown] ERROR: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Error loading drill-down data: {ex.Message}\n\nDetails: {ex.StackTrace}",
                    "Drill-Down Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}
