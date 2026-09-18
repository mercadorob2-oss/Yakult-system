using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class CallMonitoringReportsForm : Form
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly Action<int> _navigateToTicket;

        private DateTimePicker dtFrom;
        private DateTimePicker dtTo;
        private ComboBox cboQuickRange;
        private ComboBox cboType;
        private ComboBox cboDepartment;
        private ComboBox cboLocationMode;
        private ComboBox cboResponsible;
        private ComboBox cboPriority;
        private ComboBox cboCompany;
        private TextBox txtSearch;
        private Button btnClearFilters;
        private Button btnRefresh;
        private Button btnExportCsv;
        private Button btnExportPdf;
        private Button btnToggleFilters;
        private TabControl tabReports;
        private DataGridView dgvResolution;
        private DataGridView dgvSla;
        private Label lblSlaSummary;
        private DataGridView dgvSolved;
        private Label lblSolvedSummary;
        private Label lblStatus;

        private List<CallResolutionReportRow> _resolutionRowsAll = new List<CallResolutionReportRow>();
        private List<CallResolutionReportRow> _resolutionRowsFiltered = new List<CallResolutionReportRow>();
        private List<SlaComplianceDisplayRow> _slaRowsAll = new List<SlaComplianceDisplayRow>();
        private List<SlaComplianceDisplayRow> _slaRowsFiltered = new List<SlaComplianceDisplayRow>();
        private List<CallSolvedSummaryReportRow> _solvedRowsAll = new List<CallSolvedSummaryReportRow>();
        private List<CallSolvedSummaryReportRow> _solvedRowsFiltered = new List<CallSolvedSummaryReportRow>();
        private const int MaxReportRows = 2000;
        private const int PageSize = 14;
        private int _pageIndexRes = 1;
        private int _pageIndexSla = 1;
        private int _pageIndexSolved = 1;

        private Button _btnPrevRes, _btnNextRes;
        private Label _lblPageRes;
        private Button _btnPrevSla, _btnNextSla;
        private Label _lblPageSla;
        private Button _btnPrevSolved, _btnNextSolved;
        private Label _lblPageSolved;

        private readonly HashSet<int> _selectedResolutionTicketIds = new HashSet<int>();
        private readonly HashSet<int> _selectedSlaTicketIds = new HashSet<int>();
        private readonly HashSet<int> _selectedSolvedTicketIds = new HashSet<int>();

        // Colors (Matching Profiles Form)
        private readonly Color clrBackground = Color.FromArgb(243, 244, 246);
        private readonly Color clrTextDark = Color.FromArgb(17, 24, 39);
        private readonly Color clrTextMuted = Color.FromArgb(107, 114, 128);
        private readonly Color clrPrimary = Color.FromArgb(59, 130, 246);

        public CallMonitoringReportsForm(ICallMonitoringRepository repo, Action<int> navigateToTicket = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _navigateToTicket = navigateToTicket;
            InitializeComponent();
            this.Load += async (_, __) => await RefreshActiveIfEmptyAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.BackColor = clrBackground;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(20);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = clrBackground
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // Filters
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Grid

            const float ExpandedFiltersHeight = 165F;
            const float CollapsedFiltersHeight = 80F;

            // 1. Filter Section
            var filterPanel = CreateCardPanel();
            filterPanel.Dock = DockStyle.Fill;
            filterPanel.Padding = new Padding(15, 10, 15, 10);

            dtFrom = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10F) };
            dtTo = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10F) };
            cboQuickRange = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboQuickRange.Items.AddRange(new object[] { "Last 30 days", "Today", "Last 7 days", "This month", "This year" });
            cboQuickRange.SelectedIndex = 2;
            cboQuickRange.SelectedIndexChanged += async (_, __) =>
            {
                ApplyQuickRange();
                await RefreshActiveAsync();
            };

            cboType = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboType.Items.AddRange(new object[] { "All", "Service Only", "Replacement", "Temporary Replacement", "Temporary Service" });
            cboType.SelectedIndex = 0;
            cboType.SelectedIndexChanged += async (_, __) => await RefreshActiveAsync();

            dtTo.Value = DateTime.Today;
            dtFrom.Value = DateTime.Today.AddDays(-30);

            cboDepartment = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboLocationMode = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboLocationMode.Items.AddRange(new object[] { "All", "Department only", "Branch only" });
            cboLocationMode.SelectedIndex = 0;
            cboResponsible = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboPriority = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboPriority.Items.AddRange(new object[] { "All Priorities", "Critical", "High", "Medium", "Low" });
            cboPriority.SelectedIndex = 0;

            cboCompany = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
            cboCompany.Items.Add("All Companies");
            cboCompany.SelectedIndex = 0;

            txtSearch = new TextBox { Width = 220, Font = new Font("Segoe UI", 10F) };
            txtSearch.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    ApplyFilters();
                }
            };

            btnRefresh = new Button
            {
                Text = "Refresh Report",
                Width = 130,
                Height = 32,
                BackColor = clrPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (_, __) => await RefreshActiveAsync();

            btnExportCsv = new Button
            {
                Text = "Export CSV",
                Width = 110,
                Height = 32,
                BackColor = Color.FromArgb(99, 102, 241),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnExportCsv.FlatAppearance.BorderSize = 0;
            btnExportCsv.Click += (_, __) => ExportCsv();

            btnExportPdf = new Button
            {
                Text = "Export PDF",
                Width = 110,
                Height = 32,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnExportPdf.FlatAppearance.BorderSize = 0;
            btnExportPdf.Click += (_, __) => ExportPdf();

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

            var filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));

            TableLayoutPanel CreateFilterRow(Control left, Control right)
            {
                var row = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
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
            
            Panel CreateFilterGroup(string labelText, Control control, int spacerWidth = 15)
            {
                var p = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 0, spacerWidth, 0),
                    Padding = new Padding(0)
                };
                
                var lbl = CreateFilterLabel(labelText);
                lbl.Margin = new Padding(0, 4, 5, 0); // vertically center with input

                p.Controls.Add(lbl);
                p.Controls.Add(control);
                return p;
            }

            var row1Left = MakeFlow();
            row1Left.Controls.Add(CreateFilterGroup("From:", dtFrom));
            row1Left.Controls.Add(CreateFilterGroup("To:", dtTo));
            row1Left.Controls.Add(CreateFilterGroup("Quick:", cboQuickRange));
            row1Left.Controls.Add(CreateFilterGroup("Type:", cboType));
            // Priority moved to Row 2 to prevent wrapping

            var row1Right = MakeFlow();
            row1Right.WrapContents = false;
            row1Right.Controls.Add(btnRefresh);
            row1Right.Controls.Add(btnExportCsv);
            row1Right.Controls.Add(btnExportPdf);
            row1Right.Controls.Add(btnToggleFilters);
            row1Right.Controls.Add(btnClearFilters);
            row1Right.Controls.Add(lblStatus);

            var row2Left = MakeFlow();

            // Expanded row: keep everything aligned by splitting into two lines.
            // Line 1 (left): Company/Dept/Show/Priority
            // Line 2 (left): Responsible
            // Line 2 (right): Search (aligned with Responsible)
            var row2LeftTop = MakeFlow();
            row2LeftTop.WrapContents = true;
            row2LeftTop.Controls.Add(CreateFilterGroup("Company:", cboCompany, 10));
            row2LeftTop.Controls.Add(CreateFilterGroup("Dept:", cboDepartment, 10));
            row2LeftTop.Controls.Add(CreateFilterGroup("Show:", cboLocationMode, 10));
            row2LeftTop.Controls.Add(CreateFilterGroup("Priority:", cboPriority, 10));

            var row2LeftBottom = MakeFlow();
            row2LeftBottom.WrapContents = false;
            row2LeftBottom.Controls.Add(CreateFilterGroup("Responsible:", cboResponsible, 10));

            var row2RightTop = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0) };

            var row2RightBottom = MakeFlow();
            row2RightBottom.WrapContents = false;
            row2RightBottom.Controls.Add(CreateFilterGroup("Search:", txtSearch));

            var row2 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row2.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            row2LeftTop.Dock = DockStyle.Fill;
            row2LeftBottom.Dock = DockStyle.Fill;
            row2RightTop.Dock = DockStyle.Right;
            row2RightBottom.Dock = DockStyle.Right;

            row2.Controls.Add(row2LeftTop, 0, 0);
            row2.Controls.Add(row2RightTop, 1, 0);
            row2.Controls.Add(row2LeftBottom, 0, 1);
            row2.Controls.Add(row2RightBottom, 1, 1);

            var row1 = CreateFilterRow(row1Left, row1Right);

            filterLayout.Controls.Add(row1, 0, 0);
            filterLayout.Controls.Add(row2, 0, 1);

            void SetFiltersExpanded(bool expanded)
            {
                try
                {
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

            // 2. Grid Section
            var gridContainer = CreateCardPanel();
            gridContainer.Dock = DockStyle.Fill;
            gridContainer.Padding = new Padding(5);
            gridContainer.Margin = new Padding(0, 15, 0, 0); // Space between filter and grid

            tabReports = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F) };
            tabReports.SelectedIndexChanged += async (_, __) =>
            {
                UpdateFilterStateForActiveTab();
                await RefreshActiveIfEmptyAsync();
            };

            var tpResolution = new TabPage("Resolution") { BackColor = Color.White, Padding = new Padding(5) };
            
            var resLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            resLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            resLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            dgvResolution = CreateStyledGrid();
            SetupResolutionColumns();
            dgvResolution.CellDoubleClick += DgvResolution_CellDoubleClick;
            
            resLayout.Controls.Add(dgvResolution, 0, 0);
            resLayout.Controls.Add(CreatePagerPanel(out _btnPrevRes, out _btnNextRes, out _lblPageRes, () => MovePageRes(-1), () => MovePageRes(1)), 0, 1);

            tpResolution.Controls.Add(resLayout);

            var tpSla = new TabPage("SLA Compliance") { BackColor = Color.White, Padding = new Padding(5) };
            tpSla.Controls.Add(BuildSlaPanel());

            tabReports.TabPages.Add(tpResolution);
            tabReports.TabPages.Add(tpSla);

            var tpSolved = new TabPage("Solved Summary") { BackColor = Color.White, Padding = new Padding(5) };
            tpSolved.Controls.Add(BuildSolvedSummaryPanel());
            tabReports.TabPages.Add(tpSolved);

            gridContainer.Controls.Add(tabReports);
            mainLayout.Controls.Add(gridContainer, 0, 1);

            this.Controls.Add(mainLayout);

            dtTo.Value = DateTime.Today;
            dtFrom.Value = DateTime.Today.AddDays(-7);
            ApplyQuickRange();
            UpdateFilterStateForActiveTab();
            InitializeFilterLists();

            this.ResumeLayout(false);
        }

        private Panel CreateCardPanel()
        {
            var p = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(1)
            };
            p.Paint += (s, e) =>
            {
                var rect = p.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (var pen = new Pen(Color.FromArgb(229, 231, 235)))
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };
            return p;
        }

        private Panel CreatePagerPanel(out Button btnPrev, out Button btnNext, out Label lblPage, Action onPrev, Action onNext)
        {
            var p = new Panel { Dock = DockStyle.Fill, Height = 40, BackColor = Color.White };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 5, 10, 0)
            };

            btnPrev = new Button { Text = "<", Width = 30, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnPrev.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnPrev.Click += (_, __) => onPrev();

            btnNext = new Button { Text = ">", Width = 30, Height = 30, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnNext.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnNext.Click += (_, __) => onNext();

            lblPage = new Label
            {
                Text = "Page 1 of 1",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 100,
                Height = 30,
                Font = new Font("Segoe UI", 9F)
            };

            flow.Controls.Add(btnPrev);
            flow.Controls.Add(lblPage);
            flow.Controls.Add(btnNext);
            p.Controls.Add(flow);

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

        private DataGridView CreateStyledGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false, // allow checkbox selection column
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(229, 231, 235)
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = clrTextMuted;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10);
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            grid.DefaultCellStyle.ForeColor = clrTextDark;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
            grid.DefaultCellStyle.SelectionForeColor = clrTextDark;
            grid.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            grid.RowTemplate.Height = 40;

            grid.CellFormatting += (s, e) =>
            {
                 if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                 var prop = grid.Columns[e.ColumnIndex].DataPropertyName;
                 if ((prop == "ResolutionMarkedAt" || prop.EndsWith("At") || prop.EndsWith("Date") || prop.EndsWith("Utc")) && e.Value is DateTime dt)
                 {
                     if (dt.Kind == DateTimeKind.Utc)
                         e.Value = dt.ToLocalTime().ToString("g");
                     else if (dt.Kind == DateTimeKind.Unspecified)
                         e.Value = DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime().ToString("g"); // Assume Unspec is UTC from DB
                     else
                         e.Value = dt.ToString("g");
                         
                     e.FormattingApplied = true;
                 }
            };

            return grid;
        }

        private void EnableRowCheckboxSelection<T>(
            DataGridView grid,
            HashSet<int> selectedIds,
            Func<T, int> getId)
            where T : class
        {
            if (grid == null) return;
            if (selectedIds == null) return;
            if (getId == null) return;

            const string colName = "colSelect";

            if (!grid.Columns.Contains(colName))
            {
                var col = new DataGridViewCheckBoxColumn
                {
                    Name = colName,
                    HeaderText = "✓",
                    Width = 36,
                    FillWeight = 6,
                    ReadOnly = false,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells,
                    Resizable = DataGridViewTriState.False
                };
                grid.Columns.Insert(0, col);
            }

            foreach (DataGridViewColumn c in grid.Columns)
            {
                if (c.Name == colName) continue;
                c.ReadOnly = true;
            }

            grid.CurrentCellDirtyStateChanged += (_, __) =>
            {
                try
                {
                    if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
                        grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
                catch { }
            };

            grid.CellValueChanged += (_, e) =>
            {
                try
                {
                    if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                    if (grid.Columns[e.ColumnIndex].Name != colName) return;

                    var row = grid.Rows[e.RowIndex];
                    if (!(row?.DataBoundItem is T item)) return;

                    var id = getId(item);
                    if (id <= 0) return;

                    var isChecked = false;
                    try { isChecked = Convert.ToBoolean(row.Cells[colName].Value); } catch { }

                    if (isChecked) selectedIds.Add(id);
                    else selectedIds.Remove(id);
                }
                catch { }
            };

            grid.DataBindingComplete += (_, __) =>
            {
                try
                {
                    if (!grid.Columns.Contains(colName)) return;
                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        if (!(r?.DataBoundItem is T item)) continue;
                        var id = getId(item);
                        if (id <= 0) continue;
                        r.Cells[colName].Value = selectedIds.Contains(id);
                    }
                }
                catch { }
            };

            grid.ColumnHeaderMouseClick += (_, e) =>
            {
                try
                {
                    if (e.ColumnIndex < 0) return;
                    if (grid.Columns[e.ColumnIndex].Name != colName) return;

                    var anyUnchecked = grid.Rows
                        .Cast<DataGridViewRow>()
                        .Any(r =>
                        {
                            if (!(r?.DataBoundItem is T item)) return false;
                            var id = getId(item);
                            return id > 0 && !selectedIds.Contains(id);
                        });

                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        if (!(r?.DataBoundItem is T item)) continue;
                        var id = getId(item);
                        if (id <= 0) continue;

                        if (anyUnchecked) selectedIds.Add(id);
                        else selectedIds.Remove(id);
                    }

                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        if (!(r?.DataBoundItem is T item)) continue;
                        var id = getId(item);
                        if (id <= 0) continue;
                        r.Cells[colName].Value = selectedIds.Contains(id);
                    }
                }
                catch { }
            };
        }

        private void SetupResolutionColumns()
        {
            EnableRowCheckboxSelection<CallResolutionReportRow>(dgvResolution, _selectedResolutionTicketIds, r => r.TicketId);
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TicketCode", HeaderText = "TICKET", FillWeight = 12 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TicketStatus", HeaderText = "STATUS", FillWeight = 12 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Priority", HeaderText = "PRIORITY", FillWeight = 10 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResolutionType", HeaderText = "TYPE", FillWeight = 12 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Location", HeaderText = "LOCATION", FillWeight = 18 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResponsiblePerson", HeaderText = "RESPONSIBLE", FillWeight = 16 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReplacementOldItem", HeaderText = "OLD ITEM", FillWeight = 18 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReplacementNewItem", HeaderText = "NEW ITEM", FillWeight = 18 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReplacementQty", HeaderText = "QTY", FillWeight = 7 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MarkedByName", HeaderText = "MARKED BY", FillWeight = 12 });
            dgvResolution.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResolutionMarkedAt", HeaderText = "MARKED AT", FillWeight = 12 });
        }

        private sealed class SlaComplianceDisplayRow
        {
            public int TicketId { get; set; }
            public string TicketCode { get; set; }
            public string Department { get; set; }
            public string Location { get; set; }
            public string ResponsiblePerson { get; set; }
            public string Priority { get; set; }
            public string CompletedByName { get; set; }
            public string CompletedStatus { get; set; }

            public DateTime CreatedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }

            public int TargetHours { get; set; }
            public double ResolutionHours { get; set; }
            public string SlaResult { get; set; }
            public double BreachedByHours { get; set; }
        }

        private Control BuildSlaPanel()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var summaryPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 10, 8, 0), BackColor = Color.White };
            lblSlaSummary = new Label
            {
                Text = "SLA: (no data)",
                AutoSize = true,
                ForeColor = clrTextMuted,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            summaryPanel.Controls.Add(lblSlaSummary);

            dgvSla = CreateStyledGrid();
            SetupSlaColumns();
            dgvSla.CellDoubleClick += DgvSla_CellDoubleClick;

            root.Controls.Add(summaryPanel, 0, 0);
            root.Controls.Add(dgvSla, 0, 1);
            root.Controls.Add(CreatePagerPanel(out _btnPrevSla, out _btnNextSla, out _lblPageSla, () => MovePageSla(-1), () => MovePageSla(1)), 0, 2);
            return root;
        }

        private void SetupSlaColumns()
        {
            EnableRowCheckboxSelection<SlaComplianceDisplayRow>(dgvSla, _selectedSlaTicketIds, r => r.TicketId);
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TicketCode", HeaderText = "TICKET", FillWeight = 12 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Location", HeaderText = "LOCATION", FillWeight = 20 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Priority", HeaderText = "PRIORITY", FillWeight = 10 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResponsiblePerson", HeaderText = "RESPONSIBLE", FillWeight = 16 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompletedByName", HeaderText = "COMPLETED BY", FillWeight = 14 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompletedStatus", HeaderText = "FINAL STATUS", FillWeight = 12 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAtUtc", HeaderText = "CREATED", FillWeight = 12 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompletedAtUtc", HeaderText = "COMPLETED", FillWeight = 12 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TargetHours", HeaderText = "TARGET (H)", FillWeight = 10 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResolutionHours", HeaderText = "RESOLUTION (H)", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0" } });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SlaResult", HeaderText = "SLA", FillWeight = 10 });
            dgvSla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BreachedByHours", HeaderText = "BREACHED BY (H)", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0" } });
        }

        private Control BuildSolvedSummaryPanel()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var summaryPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 10, 8, 0), BackColor = Color.White };
            lblSolvedSummary = new Label
            {
                Text = "Solved Summary: (no data)",
                AutoSize = true,
                ForeColor = clrTextMuted,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            summaryPanel.Controls.Add(lblSolvedSummary);

            dgvSolved = CreateStyledGrid();
            SetupSolvedColumns();
            dgvSolved.CellDoubleClick += DgvSolved_CellDoubleClick;

            root.Controls.Add(summaryPanel, 0, 0);
            root.Controls.Add(dgvSolved, 0, 1);
            root.Controls.Add(CreatePagerPanel(out _btnPrevSolved, out _btnNextSolved, out _lblPageSolved, () => MovePageSolved(-1), () => MovePageSolved(1)), 0, 2);
            return root;
        }

        private void SetupSolvedColumns()
        {
            EnableRowCheckboxSelection<CallSolvedSummaryReportRow>(dgvSolved, _selectedSolvedTicketIds, r => r.TicketId);
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TicketCode", HeaderText = "TICKET", FillWeight = 10 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAtUtc", HeaderText = "RECEIVED", FillWeight = 11 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SolvedAtUtc", HeaderText = "SOLVED", FillWeight = 11 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Company", HeaderText = "COMPANY", FillWeight = 12 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CallerName", HeaderText = "CALLER", FillWeight = 10 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Location", HeaderText = "LOCATION", FillWeight = 14 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Priority", HeaderText = "PRIORITY", FillWeight = 8 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResponsiblePerson", HeaderText = "RESPONSIBLE", FillWeight = 12 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FinalStatus", HeaderText = "STATUS", FillWeight = 10 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Problem", HeaderText = "PROBLEM", FillWeight = 18 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Solution", HeaderText = "SOLUTION", FillWeight = 18 });
            dgvSolved.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ResolutionHours", HeaderText = "HOURS", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0" } });
        }

        private Task RefreshAsync() => RefreshActiveAsync();

        private async Task RefreshActiveIfEmptyAsync()
        {
            if (this.IsDisposed)
                return;

            try
            {
                if (tabReports?.SelectedIndex == 1)
                {
                    if (_slaRowsAll == null || _slaRowsAll.Count == 0)
                        await RefreshActiveAsync();
                }
                else if (tabReports?.SelectedIndex == 2)
                {
                    if (_solvedRowsAll == null || _solvedRowsAll.Count == 0)
                        await RefreshActiveAsync();
                }
                else
                {
                    if (_resolutionRowsAll == null || _resolutionRowsAll.Count == 0)
                        await RefreshActiveAsync();
                }
            }
            catch
            {
            }
        }

        private async Task RefreshActiveAsync()
        {
            try
            {
                btnRefresh.Enabled = false;
                btnExportCsv.Enabled = false;
                if (btnExportPdf != null) btnExportPdf.Enabled = false;
                lblStatus.Text = "Loading...";

                _selectedResolutionTicketIds.Clear();
                _selectedSlaTicketIds.Clear();
                _selectedSolvedTicketIds.Clear();

                if (!await _repo.CallSchemaExistsAsync())
                {
                    lblStatus.Text = "Call Monitoring schema not installed.";
                    dgvResolution.DataSource = null;
                    dgvSla.DataSource = null;
                    dgvSolved.DataSource = null;
                    _resolutionRowsAll = new List<CallResolutionReportRow>();
                    _resolutionRowsFiltered = new List<CallResolutionReportRow>();
                    _slaRowsAll = new List<SlaComplianceDisplayRow>();
                    _slaRowsFiltered = new List<SlaComplianceDisplayRow>();
                    _solvedRowsAll = new List<CallSolvedSummaryReportRow>();
                    _solvedRowsFiltered = new List<CallSolvedSummaryReportRow>();
                    return;
                }

                if (dtFrom.Value.Date > dtTo.Value.Date)
                {
                    MessageBox.Show("'From' date must be earlier than or equal to 'To' date.", "Call Monitoring Reports",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "Invalid date range";
                    return;
                }

                var fromLocal = DateTime.SpecifyKind(dtFrom.Value.Date, DateTimeKind.Local);
                var toLocal = DateTime.SpecifyKind(dtTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
                var fromUtc = fromLocal.ToUniversalTime();
                var toUtc = toLocal.ToUniversalTime();

                if (tabReports?.SelectedIndex == 1)
                    await RefreshSlaAsync(fromUtc, toUtc);
                else if (tabReports?.SelectedIndex == 2)
                    await RefreshSolvedAsync(fromUtc, toUtc);
                else
                    await RefreshResolutionAsync(fromUtc, toUtc);

                ApplyFilters();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error";
                MessageBox.Show(ex.Message, "Call Monitoring Reports", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
                btnExportCsv.Enabled = HasExportableRows();
                if (btnExportPdf != null) btnExportPdf.Enabled = HasExportableRows();
            }
        }

        private async Task RefreshResolutionAsync(DateTime fromUtc, DateTime toUtc)
        {
            var type = cboType.SelectedItem?.ToString() ?? "All";

            List<CallResolutionReportRow> rows;
            if (string.Equals(type, "Temporary Replacement", StringComparison.OrdinalIgnoreCase))
            {
                rows = await _repo.GetResolutionReportAsync(fromUtc, toUtc, "Replacement", MaxReportRows);
                rows = (rows ?? new List<CallResolutionReportRow>())
                    .Where(r => string.Equals(r.TicketStatus, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var r in rows)
                    r.ResolutionType = "Temporary Replacement";
            }
            else if (string.Equals(type, "Temporary Service", StringComparison.OrdinalIgnoreCase))
            {
                rows = await _repo.GetResolutionReportAsync(fromUtc, toUtc, "Service Only", MaxReportRows);
                rows = (rows ?? new List<CallResolutionReportRow>())
                    .Where(r => string.Equals(r.TicketStatus, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var r in rows)
                    r.ResolutionType = "Temporary Service";
            }
            else
            {
                rows = await _repo.GetResolutionReportAsync(fromUtc, toUtc, type, MaxReportRows);
            }

            _resolutionRowsAll = rows ?? new List<CallResolutionReportRow>();
            RebuildDepartmentAndResponsibleLists(_resolutionRowsAll.Select(r => r.Department), _resolutionRowsAll.Select(r => r.ResponsiblePerson));
        }

        private async Task RefreshSlaAsync(DateTime fromUtc, DateTime toUtc)
        {
            var rows = await _repo.GetSlaComplianceReportAsync(fromUtc, toUtc, MaxReportRows);
            rows = rows ?? new List<CallSlaComplianceReportRow>();

            _slaRowsAll = rows.Select(BuildSlaDisplayRow).Where(r => r != null).ToList();
            RebuildDepartmentAndResponsibleLists(_slaRowsAll.Select(r => r.Department), _slaRowsAll.Select(r => r.ResponsiblePerson));
        }

        private async Task RefreshSolvedAsync(DateTime fromUtc, DateTime toUtc)
        {
            var rows = await _repo.GetSolvedSummaryReportAsync(fromUtc, toUtc, MaxReportRows);
            _solvedRowsAll = rows ?? new List<CallSolvedSummaryReportRow>();

            RebuildDepartmentAndResponsibleLists(_solvedRowsAll.Select(r => r.Department), _solvedRowsAll.Select(r => r.ResponsiblePerson));
            RebuildCompanyList(_solvedRowsAll.Select(r => r.Company));
        }

        private static SlaComplianceDisplayRow BuildSlaDisplayRow(CallSlaComplianceReportRow row)
        {
            if (row == null || row.TicketId <= 0)
                return null;

            var createdUtc = row.CreatedAtUtc.Kind == DateTimeKind.Utc ? row.CreatedAtUtc : DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc);
            var completedUtc = row.CompletedAtUtc.Kind == DateTimeKind.Utc ? row.CompletedAtUtc : DateTime.SpecifyKind(row.CompletedAtUtc, DateTimeKind.Utc);

            var targetHours = GetResolutionSlaTargetHours(row.Priority);
            var dueUtc = createdUtc.AddHours(targetHours);
            var resolutionHours = Math.Max(0, (completedUtc - createdUtc).TotalHours);
            var breachedBy = Math.Max(0, (completedUtc - dueUtc).TotalHours);

            return new SlaComplianceDisplayRow
            {
                TicketId = row.TicketId,
                TicketCode = row.TicketCode ?? string.Empty,
                Department = row.Department ?? string.Empty,
                Location = row.Location ?? string.Empty,
                Priority = row.Priority ?? string.Empty,
                ResponsiblePerson = row.AssignedToName ?? string.Empty,
                CompletedByName = row.CompletedByName ?? string.Empty,
                CompletedStatus = row.CompletedStatus ?? string.Empty,
                CreatedAtUtc = createdUtc,
                CompletedAtUtc = completedUtc,
                TargetHours = targetHours,
                ResolutionHours = resolutionHours,
                SlaResult = breachedBy > 0 ? "Breached" : "Met",
                BreachedByHours = breachedBy
            };
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

        private void RebuildDepartmentAndResponsibleLists(IEnumerable<string> departments, IEnumerable<string> responsibles)
        {
            var prevDept = cboDepartment.SelectedItem?.ToString() ?? "All Departments";
            var prevResp = cboResponsible.SelectedItem?.ToString() ?? "All Responsible";

            var deptList = (departments ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var respList = (responsibles ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cboDepartment.BeginUpdate();
            cboDepartment.Items.Clear();
            cboDepartment.Items.Add("All Departments");
            foreach (var d in deptList) cboDepartment.Items.Add(d);
            cboDepartment.EndUpdate();

            cboResponsible.BeginUpdate();
            cboResponsible.Items.Clear();
            cboResponsible.Items.Add("All Responsible");
            foreach (var r in respList) cboResponsible.Items.Add(r);
            cboResponsible.EndUpdate();

            var deptIdx = cboDepartment.FindStringExact(prevDept);
            cboDepartment.SelectedIndex = deptIdx >= 0 ? deptIdx : 0;

            var respIdx = cboResponsible.FindStringExact(prevResp);
            cboResponsible.SelectedIndex = respIdx >= 0 ? respIdx : 0;
        }

        private void RebuildCompanyList(IEnumerable<string> companies)
        {
            if (cboCompany == null)
                return;

            var prev = cboCompany.SelectedItem?.ToString() ?? "All Companies";

            var list = (companies ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cboCompany.BeginUpdate();
            cboCompany.Items.Clear();
            cboCompany.Items.Add("All Companies");
            foreach (var c in list) cboCompany.Items.Add(c);
            cboCompany.EndUpdate();

            var idx = cboCompany.FindStringExact(prev);
            cboCompany.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private bool HasExportableRows()
        {
            if (tabReports?.SelectedIndex == 1)
                return _slaRowsFiltered != null && _slaRowsFiltered.Count > 0;
            if (tabReports?.SelectedIndex == 2)
                return _solvedRowsFiltered != null && _solvedRowsFiltered.Count > 0;
            return _resolutionRowsFiltered != null && _resolutionRowsFiltered.Count > 0;
        }

        private void InitializeFilterLists()
        {
            cboDepartment.Items.Clear();
            cboDepartment.Items.Add("All Departments");
            cboDepartment.SelectedIndex = 0;

            cboResponsible.Items.Clear();
            cboResponsible.Items.Add("All Responsible");
            cboResponsible.SelectedIndex = 0;

            if (cboCompany != null)
            {
                cboCompany.Items.Clear();
                cboCompany.Items.Add("All Companies");
                cboCompany.SelectedIndex = 0;
            }

            cboDepartment.SelectedIndexChanged += (_, __) => ApplyFilters();
            cboResponsible.SelectedIndexChanged += (_, __) => ApplyFilters();
            cboPriority.SelectedIndexChanged += (_, __) => ApplyFilters();
            if (cboCompany != null) cboCompany.SelectedIndexChanged += (_, __) => ApplyFilters();
            cboLocationMode.SelectedIndexChanged += (_, __) =>
            {
                UpdateDepartmentFilterEnabledState();
                ApplyFilters();
            };

            UpdateDepartmentFilterEnabledState();
        }

        private void UpdateFilterStateForActiveTab()
        {
            var idx = tabReports?.SelectedIndex ?? 0;
            if (cboType != null) cboType.Enabled = idx == 0;
            if (cboCompany != null)
            {
                cboCompany.Enabled = idx == 2;
                if (idx != 2 && cboCompany.Items.Count > 0 && cboCompany.SelectedIndex != 0)
                    cboCompany.SelectedIndex = 0;
            }
        }

        private void UpdateDepartmentFilterEnabledState()
        {
            try
            {
                var branchOnly = cboLocationMode != null && cboLocationMode.SelectedIndex == 2;
                if (cboDepartment != null)
                {
                    cboDepartment.Enabled = !branchOnly;
                    if (branchOnly && cboDepartment.Items.Count > 0 && cboDepartment.SelectedIndex != 0)
                        cboDepartment.SelectedIndex = 0;
                }
            }
            catch
            {
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
                if (cboLocationMode.Items.Count > 0) cboLocationMode.SelectedIndex = 0;
                if (cboDepartment.Items.Count > 0) cboDepartment.SelectedIndex = 0;
                if (cboResponsible.Items.Count > 0) cboResponsible.SelectedIndex = 0;
                if (cboCompany != null && cboCompany.Items.Count > 0) cboCompany.SelectedIndex = 0;
                cboPriority.SelectedIndex = 0;
                txtSearch.Text = string.Empty;
                ApplyFilters();
            }
            catch
            {
            }
        }

        private void ApplyFilters()
        {
            UpdateDepartmentFilterEnabledState();

            if (tabReports?.SelectedIndex == 1)
                ApplySlaFilters();
            else if (tabReports?.SelectedIndex == 2)
                ApplySolvedFilters();
            else
                ApplyResolutionFilters();

            btnExportCsv.Enabled = HasExportableRows();
            if (btnExportPdf != null) btnExportPdf.Enabled = HasExportableRows();
        }

        private void ApplyResolutionFilters()
        {
            var locationMode = cboLocationMode?.SelectedIndex ?? 0; // 0=All, 1=Dept only, 2=Branch only

            var dept = cboDepartment.SelectedItem?.ToString();
            if (string.Equals(dept, "All Departments", StringComparison.OrdinalIgnoreCase))
                dept = null;

            if (locationMode == 2)
                dept = null;

            var resp = cboResponsible.SelectedItem?.ToString();
            if (string.Equals(resp, "All Responsible", StringComparison.OrdinalIgnoreCase))
                resp = null;

            var prio = cboPriority.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(prio) && prio.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                prio = null;

            var q = (txtSearch.Text ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);
            var query = q.ToUpperInvariant();

            _resolutionRowsFiltered = (_resolutionRowsAll ?? new List<CallResolutionReportRow>())
                .Where(r =>
                {
                    if (r == null) return false;

                    var hasDept = !string.IsNullOrWhiteSpace(r.Department);
                    var hasLocation = !string.IsNullOrWhiteSpace(r.Location);
                    if (locationMode == 1 && !hasDept) return false;
                    if (locationMode == 2 && (hasDept || !hasLocation)) return false;

                    if (!string.IsNullOrWhiteSpace(dept) && !string.Equals((r.Department ?? string.Empty).Trim(), dept.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(resp) && !string.Equals((r.ResponsiblePerson ?? string.Empty).Trim(), resp.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(prio) && !string.Equals((r.Priority ?? string.Empty).Trim(), prio.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (hasQuery)
                    {
                        var hay = $"{r.TicketCode} {r.TicketStatus} {r.Priority} {r.ResolutionType} {r.Location} {r.Department} {r.ResponsiblePerson} {r.ReplacementOldItem} {r.ReplacementNewItem} {r.MarkedByName} {r.Remarks}".ToUpperInvariant();
                        if (!hay.Contains(query)) return false;
                    }
                    return true;
                })
                .ToList();

            _selectedResolutionTicketIds.IntersectWith(_resolutionRowsFiltered.Select(r => r?.TicketId ?? 0).Where(id => id > 0));

            _pageIndexRes = 1;
            UpdateResolutionPage();
            lblStatus.Text = _resolutionRowsFiltered.Count >= MaxReportRows
                ? $"{_resolutionRowsFiltered.Count} row(s) found (showing first {MaxReportRows})"
                : $"{_resolutionRowsFiltered.Count} row(s) found";
        }

        private void ApplySlaFilters()
        {
            var locationMode = cboLocationMode?.SelectedIndex ?? 0; // 0=All, 1=Dept only, 2=Branch only

            var dept = cboDepartment.SelectedItem?.ToString();
            if (string.Equals(dept, "All Departments", StringComparison.OrdinalIgnoreCase))
                dept = null;

            if (locationMode == 2)
                dept = null;

            var resp = cboResponsible.SelectedItem?.ToString();
            if (string.Equals(resp, "All Responsible", StringComparison.OrdinalIgnoreCase))
                resp = null;

            var prio = cboPriority.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(prio) && prio.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                prio = null;

            var q = (txtSearch.Text ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);
            var query = q.ToUpperInvariant();

            _slaRowsFiltered = (_slaRowsAll ?? new List<SlaComplianceDisplayRow>())
                .Where(r =>
                {
                    if (r == null) return false;

                    var hasDept = !string.IsNullOrWhiteSpace(r.Department);
                    var hasLocation = !string.IsNullOrWhiteSpace(r.Location);
                    if (locationMode == 1 && !hasDept) return false;
                    if (locationMode == 2 && (hasDept || !hasLocation)) return false;

                    if (!string.IsNullOrWhiteSpace(dept) && !string.Equals((r.Department ?? string.Empty).Trim(), dept.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(resp) && !string.Equals((r.ResponsiblePerson ?? string.Empty).Trim(), resp.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(prio) && !string.Equals((r.Priority ?? string.Empty).Trim(), prio.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (hasQuery)
                    {
                        var hay = $"{r.TicketCode} {r.Location} {r.Department} {r.Priority} {r.ResponsiblePerson} {r.CompletedByName} {r.CompletedStatus} {r.SlaResult}".ToUpperInvariant();
                        if (!hay.Contains(query)) return false;
                    }
                    return true;
                })
                .ToList();

            _selectedSlaTicketIds.IntersectWith(_slaRowsFiltered.Select(r => r?.TicketId ?? 0).Where(id => id > 0));

            _pageIndexSla = 1;
            UpdateSlaPage();
            UpdateSlaSummary();
            lblStatus.Text = _slaRowsFiltered.Count >= MaxReportRows
                ? $"{_slaRowsFiltered.Count} row(s) found (showing first {MaxReportRows})"
                : $"{_slaRowsFiltered.Count} row(s) found";
        }

        private void ApplySolvedFilters()
        {
            var locationMode = cboLocationMode?.SelectedIndex ?? 0; // 0=All, 1=Dept only, 2=Branch only

            var company = cboCompany?.SelectedItem?.ToString();
            if (string.Equals(company, "All Companies", StringComparison.OrdinalIgnoreCase))
                company = null;

            var dept = cboDepartment.SelectedItem?.ToString();
            if (string.Equals(dept, "All Departments", StringComparison.OrdinalIgnoreCase))
                dept = null;

            if (locationMode == 2)
                dept = null;

            var resp = cboResponsible.SelectedItem?.ToString();
            if (string.Equals(resp, "All Responsible", StringComparison.OrdinalIgnoreCase))
                resp = null;

            var prio = cboPriority.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(prio) && prio.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                prio = null;

            var q = (txtSearch.Text ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);
            var query = q.ToUpperInvariant();

            _solvedRowsFiltered = (_solvedRowsAll ?? new List<CallSolvedSummaryReportRow>())
                .Where(r =>
                {
                    if (r == null) return false;

                    var hasDept = !string.IsNullOrWhiteSpace(r.Department);
                    var hasLocation = !string.IsNullOrWhiteSpace(r.Location);
                    if (locationMode == 1 && !hasDept) return false;
                    if (locationMode == 2 && (hasDept || !hasLocation)) return false;

                    if (!string.IsNullOrWhiteSpace(company) && !string.Equals((r.Company ?? string.Empty).Trim(), company.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(dept) && !string.Equals((r.Department ?? string.Empty).Trim(), dept.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(resp) && !string.Equals((r.ResponsiblePerson ?? string.Empty).Trim(), resp.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(prio) && !string.Equals((r.Priority ?? string.Empty).Trim(), prio.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    if (hasQuery)
                    {
                        var hay = $"{r.TicketCode} {r.Company} {r.Location} {r.CallerName} {r.Priority} {r.ResponsiblePerson} {r.FinalStatus} {r.Problem} {r.Solution}".ToUpperInvariant();
                        if (!hay.Contains(query)) return false;
                    }
                    return true;
                })
                .ToList();

            _selectedSolvedTicketIds.IntersectWith(_solvedRowsFiltered.Select(r => r?.TicketId ?? 0).Where(id => id > 0));

            _pageIndexSolved = 1;
            UpdateSolvedPage();
            UpdateSolvedSummary();
            lblStatus.Text = _solvedRowsFiltered.Count >= MaxReportRows
                ? $"{_solvedRowsFiltered.Count} row(s) found (showing first {MaxReportRows})"
                : $"{_solvedRowsFiltered.Count} row(s) found";
        }

        private void UpdateSlaSummary()
        {
            if (lblSlaSummary == null)
                return;

            var rows = _slaRowsFiltered ?? new List<SlaComplianceDisplayRow>();
            var total = rows.Count;
            var met = rows.Count(r => string.Equals(r.SlaResult, "Met", StringComparison.OrdinalIgnoreCase));
            var breached = rows.Count(r => string.Equals(r.SlaResult, "Breached", StringComparison.OrdinalIgnoreCase));
            var pct = total > 0 ? (met * 100.0 / total) : 0;
            var avg = total > 0 ? rows.Average(r => r.ResolutionHours) : 0;

            lblSlaSummary.Text = $"SLA: {pct:0.#}% met • {met} met, {breached} breached • avg {avg:0.#}h";
        }

        private void UpdateSolvedSummary()
        {
            if (lblSolvedSummary == null)
                return;

            var rows = _solvedRowsFiltered ?? new List<CallSolvedSummaryReportRow>();
            var total = rows.Count;
            var avg = total > 0 ? rows.Average(r => r.ResolutionHours) : 0;

            var critical = rows.Count(r => string.Equals(r.Priority, "Critical", StringComparison.OrdinalIgnoreCase));
            var high = rows.Count(r => string.Equals(r.Priority, "High", StringComparison.OrdinalIgnoreCase));
            var med = rows.Count(r => string.Equals(r.Priority, "Medium", StringComparison.OrdinalIgnoreCase));
            var low = rows.Count(r => string.Equals(r.Priority, "Low", StringComparison.OrdinalIgnoreCase));

            lblSolvedSummary.Text = $"Solved: {total} • avg {avg:0.#}h • Critical {critical}, High {high}, Medium {med}, Low {low}";
        }

        public Task ApplyFilterAsync(DateTime fromLocal, DateTime toLocal, string type)
        {
            if (this.IsDisposed)
                return Task.CompletedTask;

            if (this.InvokeRequired)
            {
                var tcs = new TaskCompletionSource<object>();
                this.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await ApplyFilterAsync(fromLocal, toLocal, type);
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
                return tcs.Task;
            }

            if (dtFrom != null) dtFrom.Value = fromLocal.Date;
            if (dtTo != null) dtTo.Value = toLocal.Date;

            var desired = (type ?? "All").Trim();
            if (cboType != null)
            {
                var idx = cboType.FindStringExact(desired);
                cboType.SelectedIndex = idx >= 0 ? idx : 0;
            }

            if (tabReports != null)
                tabReports.SelectedIndex = 0;

            return RefreshActiveAsync();
        }

        private void DgvResolution_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvResolution?.Rows == null || e.RowIndex >= dgvResolution.Rows.Count) return;

            var row = dgvResolution.Rows[e.RowIndex];
            if (!(row?.DataBoundItem is CallResolutionReportRow item)) return;

            if (_navigateToTicket != null && item.TicketId > 0)
            {
                _navigateToTicket(item.TicketId);
                return;
            }

            var ticketCode = item.TicketCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ticketCode)) return;

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

        private void DgvSla_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvSla?.Rows == null || e.RowIndex >= dgvSla.Rows.Count) return;

            var row = dgvSla.Rows[e.RowIndex];
            if (!(row?.DataBoundItem is SlaComplianceDisplayRow item)) return;

            if (_navigateToTicket != null && item.TicketId > 0)
            {
                _navigateToTicket(item.TicketId);
                return;
            }

            var ticketCode = item.TicketCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ticketCode)) return;

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

        private void DgvSolved_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvSolved?.Rows == null || e.RowIndex >= dgvSolved.Rows.Count) return;

            var row = dgvSolved.Rows[e.RowIndex];
            if (!(row?.DataBoundItem is CallSolvedSummaryReportRow item)) return;
            if (item.TicketId <= 0) return;

            if (_navigateToTicket != null)
            {
                _navigateToTicket(item.TicketId);
                return;
            }

            var ticketCode = item.TicketCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ticketCode)) return;

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

        private void ExportCsv()
        {
            using (var sfd = new SaveFileDialog
            {
                Title = "Export Call Monitoring Report",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = tabReports?.SelectedIndex == 1
                    ? $"call_monitoring_sla_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    : tabReports?.SelectedIndex == 2
                        ? $"call_monitoring_solved_summary_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                        : $"call_monitoring_resolution_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                string Csv(string s)
                {
                    s = s ?? string.Empty;
                    if (s.Contains("\"")) s = s.Replace("\"", "\"\"");
                    return (s.Contains(",") || s.Contains("\n") || s.Contains("\r") || s.Contains("\""))
                        ? $"\"{s}\""
                        : s;
                }

                string LocalDate(DateTime? dt)
                {
                    if (!dt.HasValue) return string.Empty;
                    var value = dt.Value.Kind == DateTimeKind.Utc
                        ? dt.Value.ToLocalTime()
                        : DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc).ToLocalTime();
                    return value.ToString("g");
                }

                var sb = new StringBuilder();

                if (tabReports?.SelectedIndex == 1)
                {
                    if (_slaRowsFiltered == null || _slaRowsFiltered.Count == 0)
                    {
                        MessageBox.Show("No rows to export.", "Call Monitoring Reports", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    sb.AppendLine("TicketCode,Location,Priority,ResponsiblePerson,CompletedBy,FinalStatus,CreatedAt,CompletedAt,TargetHours,ResolutionHours,SlaResult,BreachedByHours");
                    foreach (var r in _slaRowsFiltered)
                    {
                        sb.Append(Csv(r.TicketCode)).Append(",");
                        sb.Append(Csv(r.Location)).Append(",");
                        sb.Append(Csv(r.Priority)).Append(",");
                        sb.Append(Csv(r.ResponsiblePerson)).Append(",");
                        sb.Append(Csv(r.CompletedByName)).Append(",");
                        sb.Append(Csv(r.CompletedStatus)).Append(",");
                        sb.Append(Csv(LocalDate(r.CreatedAtUtc))).Append(",");
                        sb.Append(Csv(LocalDate(r.CompletedAtUtc))).Append(",");
                        sb.Append(Csv(r.TargetHours.ToString())).Append(",");
                        sb.Append(Csv(r.ResolutionHours.ToString("0.0"))).Append(",");
                        sb.Append(Csv(r.SlaResult)).Append(",");
                        sb.Append(Csv(r.BreachedByHours.ToString("0.0")));
                        sb.AppendLine();
                    }
                }
                else if (tabReports?.SelectedIndex == 2)
                {
                    if (_solvedRowsFiltered == null || _solvedRowsFiltered.Count == 0)
                    {
                        MessageBox.Show("No rows to export.", "Call Monitoring Reports", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    sb.AppendLine("TicketCode,ReceivedAt,SolvedAt,Company,Caller,Location,Priority,Responsible,Status,Problem,Solution,ResolutionHours");
                    foreach (var r in _solvedRowsFiltered)
                    {
                        sb.Append(Csv(r.TicketCode)).Append(",");
                        sb.Append(Csv(LocalDate(r.CreatedAtUtc))).Append(",");
                        sb.Append(Csv(LocalDate(r.SolvedAtUtc))).Append(",");
                        sb.Append(Csv(r.Company)).Append(",");
                        sb.Append(Csv(r.CallerName)).Append(",");
                        sb.Append(Csv(r.Location)).Append(",");
                        sb.Append(Csv(r.Priority)).Append(",");
                        sb.Append(Csv(r.ResponsiblePerson)).Append(",");
                        sb.Append(Csv(r.FinalStatus)).Append(",");
                        sb.Append(Csv(r.Problem)).Append(",");
                        sb.Append(Csv(r.Solution)).Append(",");
                        sb.Append(Csv(r.ResolutionHours.ToString("0.0")));
                        sb.AppendLine();
                    }
                }
                else
                {
                    if (_resolutionRowsFiltered == null || _resolutionRowsFiltered.Count == 0)
                    {
                        MessageBox.Show("No rows to export.", "Call Monitoring Reports", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    sb.AppendLine("TicketCode,Status,Priority,ResolutionType,Location,ResponsiblePerson,OldItem,NewItem,Qty,MarkedBy,MarkedAt,Remarks");
                    foreach (var r in _resolutionRowsFiltered)
                    {
                        sb.Append(Csv(r.TicketCode)).Append(",");
                        sb.Append(Csv(r.TicketStatus)).Append(",");
                        sb.Append(Csv(r.Priority)).Append(",");
                        sb.Append(Csv(r.ResolutionType)).Append(",");
                        sb.Append(Csv(r.Location)).Append(",");
                        sb.Append(Csv(r.ResponsiblePerson)).Append(",");
                        sb.Append(Csv(r.ReplacementOldItem)).Append(",");
                        sb.Append(Csv(r.ReplacementNewItem)).Append(",");
                        sb.Append(Csv(r.ReplacementQty?.ToString())).Append(",");
                        sb.Append(Csv(r.MarkedByName)).Append(",");
                        sb.Append(Csv(LocalDate(r.ResolutionMarkedAt))).Append(",");
                        sb.Append(Csv(r.Remarks));
                        sb.AppendLine();
                    }
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                lblStatus.Text = tabReports?.SelectedIndex == 1
                    ? $"Exported {_slaRowsFiltered.Count} row(s)"
                    : tabReports?.SelectedIndex == 2
                        ? $"Exported {_solvedRowsFiltered.Count} row(s)"
                    : $"Exported {_resolutionRowsFiltered.Count} row(s)";
            }
        }

        private void ExportPdf()
        {
            if (!HasExportableRows())
            {
                MessageBox.Show("No rows to export.", "Call Monitoring Reports", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tabIndex = tabReports?.SelectedIndex ?? 0;
            var defaultName = tabIndex == 1
                ? $"call_monitoring_sla_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                : tabIndex == 2
                    ? $"call_monitoring_solved_summary_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                    : $"call_monitoring_resolution_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            using (var sfd = new SaveFileDialog
            {
                Title = "Export Call Monitoring Report (PDF)",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = defaultName
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                string LocalDate(DateTime? dt)
                {
                    if (!dt.HasValue) return string.Empty;
                    var value = dt.Value.Kind == DateTimeKind.Utc
                        ? dt.Value.ToLocalTime()
                        : DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc).ToLocalTime();
                    return value.ToString("g");
                }

                string LocalDateNonNull(DateTime dt)
                {
                    var value = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
                    return value.ToString("g");
                }

                try
                {
                    if (tabIndex == 1)
                    {
                        var selected = (_slaRowsFiltered ?? new List<SlaComplianceDisplayRow>())
                            .Where(r => r != null && _selectedSlaTicketIds.Contains(r.TicketId))
                            .ToList();

                        if (selected.Count == 0)
                        {
                            if (MessageBox.Show("No rows are checked. Export all filtered rows?", "Export PDF", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                                return;
                            selected = (_slaRowsFiltered ?? new List<SlaComplianceDisplayRow>()).Where(r => r != null).ToList();
                        }

                        var cols = new List<TabularPdfGenerator.Col>
                        {
                            new TabularPdfGenerator.Col("Ticket", 80),
                            new TabularPdfGenerator.Col("Location", 150, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Priority", 60),
                            new TabularPdfGenerator.Col("Responsible", 110, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Completed By", 110, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Final Status", 90),
                            new TabularPdfGenerator.Col("Created", 95),
                            new TabularPdfGenerator.Col("Completed", 95),
                            new TabularPdfGenerator.Col("Target (H)", 65, rightAlign:true),
                            new TabularPdfGenerator.Col("Resolution (H)", 80, rightAlign:true),
                            new TabularPdfGenerator.Col("SLA", 55),
                            new TabularPdfGenerator.Col("Breached (H)", 80, rightAlign:true),
                        };

                        var rows = selected.Select(r => new[]
                        {
                            r.TicketCode,
                            r.Location,
                            r.Priority,
                            r.ResponsiblePerson,
                            r.CompletedByName,
                            r.CompletedStatus,
                            LocalDateNonNull(r.CreatedAtUtc),
                            LocalDateNonNull(r.CompletedAtUtc),
                            r.TargetHours.ToString(),
                            r.ResolutionHours.ToString("0.0"),
                            r.SlaResult,
                            r.BreachedByHours.ToString("0.0")
                        }).ToList();

                        TabularPdfGenerator.GenerateTablePdf(
                            title: "SLA Compliance Report",
                            subject: "Yakult IT Call Monitoring",
                            columns: cols,
                            rows: rows,
                            outputPath: sfd.FileName);
                    }
                    else if (tabIndex == 2)
                    {
                        var selected = (_solvedRowsFiltered ?? new List<CallSolvedSummaryReportRow>())
                            .Where(r => r != null && _selectedSolvedTicketIds.Contains(r.TicketId))
                            .ToList();

                        if (selected.Count == 0)
                        {
                            if (MessageBox.Show("No rows are checked. Export all filtered rows?", "Export PDF", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                                return;
                            selected = (_solvedRowsFiltered ?? new List<CallSolvedSummaryReportRow>()).Where(r => r != null).ToList();
                        }

                        var cols = new List<TabularPdfGenerator.Col>
                        {
                            new TabularPdfGenerator.Col("Ticket", 70),
                            new TabularPdfGenerator.Col("Received", 95),
                            new TabularPdfGenerator.Col("Solved", 95),
                            new TabularPdfGenerator.Col("Company", 80, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Caller", 95, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Location", 140, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Priority", 60),
                            new TabularPdfGenerator.Col("Responsible", 110, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Status", 75),
                            new TabularPdfGenerator.Col("Problem", 170, wrap:true, maxLines:3),
                            new TabularPdfGenerator.Col("Solution", 170, wrap:true, maxLines:3),
                            new TabularPdfGenerator.Col("Hours", 55, rightAlign:true),
                        };

                        var rows = selected.Select(r => new[]
                        {
                            r.TicketCode,
                            LocalDateNonNull(r.CreatedAtUtc),
                            LocalDateNonNull(r.SolvedAtUtc),
                            r.Company,
                            r.CallerName,
                            r.Location,
                            r.Priority,
                            r.ResponsiblePerson,
                            r.FinalStatus,
                            r.Problem,
                            r.Solution,
                            r.ResolutionHours.ToString("0.0")
                        }).ToList();

                        TabularPdfGenerator.GenerateTablePdf(
                            title: "Solved Summary Report",
                            subject: "Yakult IT Call Monitoring",
                            columns: cols,
                            rows: rows,
                            outputPath: sfd.FileName);
                    }
                    else
                    {
                        var selected = (_resolutionRowsFiltered ?? new List<CallResolutionReportRow>())
                            .Where(r => r != null && _selectedResolutionTicketIds.Contains(r.TicketId))
                            .ToList();

                        if (selected.Count == 0)
                        {
                            if (MessageBox.Show("No rows are checked. Export all filtered rows?", "Export PDF", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                                return;
                            selected = (_resolutionRowsFiltered ?? new List<CallResolutionReportRow>()).Where(r => r != null).ToList();
                        }

                        var cols = new List<TabularPdfGenerator.Col>
                        {
                            new TabularPdfGenerator.Col("Ticket", 70),
                            new TabularPdfGenerator.Col("Status", 75),
                            new TabularPdfGenerator.Col("Priority", 60),
                            new TabularPdfGenerator.Col("Type", 90),
                            new TabularPdfGenerator.Col("Location", 140, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Responsible", 110, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Old Item", 160, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("New Item", 160, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Qty", 45, rightAlign:true),
                            new TabularPdfGenerator.Col("Marked By", 95, wrap:true, maxLines:2),
                            new TabularPdfGenerator.Col("Marked At", 95),
                            new TabularPdfGenerator.Col("Remarks", 180, wrap:true, maxLines:3),
                        };

                        var rows = selected.Select(r => new[]
                        {
                            r.TicketCode,
                            r.TicketStatus,
                            r.Priority,
                            r.ResolutionType,
                            r.Location,
                            r.ResponsiblePerson,
                            r.ReplacementOldItem,
                            r.ReplacementNewItem,
                            r.ReplacementQty?.ToString() ?? string.Empty,
                            r.MarkedByName,
                            LocalDate(r.ResolutionMarkedAt),
                            r.Remarks
                        }).ToList();

                        TabularPdfGenerator.GenerateTablePdf(
                            title: "Resolution Report",
                            subject: "Yakult IT Call Monitoring",
                            columns: cols,
                            rows: rows,
                            outputPath: sfd.FileName);
                    }

                    lblStatus.Text = "PDF generated";
                    TabularPdfGenerator.TryOpen(sfd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Export PDF Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MovePageRes(int delta)
        {
            _pageIndexRes += delta;
            UpdateResolutionPage();
        }

        private void UpdateResolutionPage()
        {
            if (_resolutionRowsFiltered == null) _resolutionRowsFiltered = new List<CallResolutionReportRow>();
            var totalCount = _resolutionRowsFiltered.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            if (totalPages < 1) totalPages = 1;

            if (_pageIndexRes < 1) _pageIndexRes = 1;
            if (_pageIndexRes > totalPages) _pageIndexRes = totalPages;

            var paged = _resolutionRowsFiltered
                .Skip((_pageIndexRes - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            dgvResolution.DataSource = paged;
            
            if (_lblPageRes != null) _lblPageRes.Text = $"Page {_pageIndexRes} of {totalPages}";
            if (_btnPrevRes != null) _btnPrevRes.Enabled = _pageIndexRes > 1;
            if (_btnNextRes != null) _btnNextRes.Enabled = _pageIndexRes < totalPages;
        }

        private void MovePageSla(int delta)
        {
            _pageIndexSla += delta;
            UpdateSlaPage();
        }

        private void UpdateSlaPage()
        {
            if (_slaRowsFiltered == null) _slaRowsFiltered = new List<SlaComplianceDisplayRow>();
            var totalCount = _slaRowsFiltered.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            if (totalPages < 1) totalPages = 1;

            if (_pageIndexSla < 1) _pageIndexSla = 1;
            if (_pageIndexSla > totalPages) _pageIndexSla = totalPages;

            var paged = _slaRowsFiltered
                .Skip((_pageIndexSla - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            dgvSla.DataSource = paged;

            if (_lblPageSla != null) _lblPageSla.Text = $"Page {_pageIndexSla} of {totalPages}";
            if (_btnPrevSla != null) _btnPrevSla.Enabled = _pageIndexSla > 1;
            if (_btnNextSla != null) _btnNextSla.Enabled = _pageIndexSla < totalPages;
        }

        private void MovePageSolved(int delta)
        {
            _pageIndexSolved += delta;
            UpdateSolvedPage();
        }

        private void UpdateSolvedPage()
        {
            if (_solvedRowsFiltered == null) _solvedRowsFiltered = new List<CallSolvedSummaryReportRow>();
            var totalCount = _solvedRowsFiltered.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            if (totalPages < 1) totalPages = 1;

            if (_pageIndexSolved < 1) _pageIndexSolved = 1;
            if (_pageIndexSolved > totalPages) _pageIndexSolved = totalPages;

            var paged = _solvedRowsFiltered
                .Skip((_pageIndexSolved - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            dgvSolved.DataSource = paged;

            if (_lblPageSolved != null) _lblPageSolved.Text = $"Page {_pageIndexSolved} of {totalPages}";
            if (_btnPrevSolved != null) _btnPrevSolved.Enabled = _pageIndexSolved > 1;
            if (_btnNextSolved != null) _btnNextSolved.Enabled = _pageIndexSolved < totalPages;
        }
    }
}
