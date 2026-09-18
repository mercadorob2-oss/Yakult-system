using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using ReaLTaiizor.Enum.Poison;
using ReaLTaiizor.Util;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.BranchAssignment
{
    /// <summary>
    /// Page for viewing and managing dbo.BranchDepartmentCompany entries.
    /// Separates "which branch serves which company/department" from the
    /// Department master list.
    /// </summary>
    public class ViewBranchAssignmentPage : UserControl
    {
        // ── Design constants ──────────────────────────────────────────────────
        private static readonly Color Blue          = Color.FromArgb(52,  152, 219);
        private static readonly Color BlueDark      = Color.FromArgb(41,  128, 185);
        private static readonly Color HeaderBg      = Color.FromArgb(245, 247, 250);

        // ── Layout panels ─────────────────────────────────────────────────────
        private System.Windows.Forms.Panel _headerPanel;
        private System.Windows.Forms.Panel _buttonBarPanel;
        private System.Windows.Forms.Panel _summaryPanel;
        private System.Windows.Forms.Panel _paginationPanel;
        private ReaLTaiizor.Controls.Panel _bodyPanel;
        private MaterialCard               _gridCard;

        // ── Header controls ───────────────────────────────────────────────────
        private HopeTextBox _txtSearch;
        private ComboBox    _cmbFilterCompany;
        private ComboBox    _cmbFilterBranchType;
        private ComboBox    _cmbFilterDept;

        // ── Buttons ───────────────────────────────────────────────────────────
        private HopeButton _btnAdd;
        private HopeButton _btnDelete;
        private HopeButton _btnRefresh;
        private System.Windows.Forms.CheckBox _selectAllCheckBox;

        // ── Summary card value labels ─────────────────────────────────────────
        private Label _lblTotalCount;
        private Label _lblWithDeptCount;
        private Label _lblNoDeptCount;
        private Label _lblFactoryCount;

        // ── Grid ──────────────────────────────────────────────────────────────
        private PoisonDataGridView _dgv;

        // ── Pagination ────────────────────────────────────────────────────────
        private System.Windows.Forms.Button _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;
        private Label  _lblPageInfo;
        private int    _currentPage = 1;
        private const int PageSize  = 15;

        // ── Status label ──────────────────────────────────────────────────────
        private Label _lblStatus;

        // ── In-memory data ────────────────────────────────────────────────────
        private List<AssignmentRow> _all      = new List<AssignmentRow>();
        private List<AssignmentRow> _filtered = new List<AssignmentRow>();

        // Company filter lookup
        private readonly List<int?>   _filterCompanyIds   = new List<int?>();
        private readonly List<string> _filterCompanyNames = new List<string>();
        // Department filter lookup
        private readonly List<int?>   _filterDeptIds   = new List<int?>();
        private readonly List<string> _filterDeptNames = new List<string>();

        private bool _filtersLoaded = false;

        // ── Sorting state ─────────────────────────────────────────────────────
        private DataGridViewColumn              _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;

        private class AssignmentRow
        {
            public int      BranchDeptCompanyID { get; set; }
            public int      BranchID            { get; set; }
            public int      CompanyID           { get; set; }
            public int?     DepartmentID        { get; set; }
            public string   BranchName          { get; set; }
            public string   BranchType          { get; set; }
            public string   CompanyName         { get; set; }
            public string   DepartmentName      { get; set; }   // "N/A" when NULL
            public DateTime CreatedDate         { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────────
        public ViewBranchAssignmentPage()
        {
            BuildUi();
            Load += async (s, e) => await LoadAllAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.White;
            SuspendLayout();
            Controls.Clear();

            // ── Header ───────────────────────────────────────────────────────
            _headerPanel = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Top,
                Height    = 84,
                BackColor = HeaderBg,
                Padding   = new Padding(15, 10, 15, 10)
            };

            var titleLabel = new Label
            {
                Text      = "Branch Assignments",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location  = new Point(15, 10)
            };

            _txtSearch = new HopeTextBox
            {
                BackColor    = Color.White,
                BaseColor    = Color.White,
                BorderColorA = Color.FromArgb(220, 220, 220),
                BorderColorB = Color.FromArgb(220, 220, 220),
                ForeColor    = Color.Gray,
                Font         = new Font("Segoe UI", 9F),
                Location     = new Point(15, 44),
                Size         = new Size(290, 30),
                Text         = "Search by Branch, Company, or Department..."
            };
            _txtSearch.GotFocus += (s, e) =>
            {
                if (_txtSearch.Text == "Search by Branch, Company, or Department...")
                { _txtSearch.Text = string.Empty; _txtSearch.ForeColor = Color.FromArgb(40, 40, 40); }
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                { _txtSearch.Text = "Search by Branch, Company, or Department..."; _txtSearch.ForeColor = Color.Gray; }
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();

            // Inline filter combos
            var lblCompany = MakeHeaderLabel("Company:", new Point(315, 52));
            _cmbFilterCompany = MakeHeaderCombo(new Point(385, 48), 130);
            _cmbFilterCompany.SelectedIndexChanged += (s, e) => ApplyFilter();

            var lblType = MakeHeaderLabel("Type:", new Point(526, 52));
            _cmbFilterBranchType = MakeHeaderCombo(new Point(568, 48), 130);
            _cmbFilterBranchType.Items.AddRange(new object[]
                { "All Types", "Factory", "Depot", "Center", "Distributor", "Office" });
            _cmbFilterBranchType.SelectedIndex = 0;
            _cmbFilterBranchType.SelectedIndexChanged += (s, e) => ApplyFilter();

            var lblDept = MakeHeaderLabel("Department:", new Point(710, 52));
            _cmbFilterDept = MakeHeaderCombo(new Point(798, 48), 160);
            _cmbFilterDept.SelectedIndexChanged += (s, e) => ApplyFilter();

            _headerPanel.Controls.Add(titleLabel);
            _headerPanel.Controls.Add(_txtSearch);
            _headerPanel.Controls.Add(lblCompany);
            _headerPanel.Controls.Add(_cmbFilterCompany);
            _headerPanel.Controls.Add(lblType);
            _headerPanel.Controls.Add(_cmbFilterBranchType);
            _headerPanel.Controls.Add(lblDept);
            _headerPanel.Controls.Add(_cmbFilterDept);

            // ── Button bar ───────────────────────────────────────────────────
            _buttonBarPanel = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Top,
                Height    = 74,
                BackColor = HeaderBg,
                Padding   = new Padding(12, 0, 12, 10)
            };

            var buttonCard = new MaterialCard
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Padding   = new Padding(14, 10, 14, 10)
            };

            var buttonTable = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1
            };
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));

            var leftFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                AutoScroll    = true,
                BackColor     = Color.Transparent,
                Padding       = new Padding(0),
                Margin        = new Padding(0)
            };

            _btnAdd = new HopeButton
            {
                Text   = "\u2795  Add Assignment",
                Font   = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size   = new Size(200, 46),
                Margin = new Padding(0, 4, 12, 0)
            };
            ConfigurePillHopeButton(_btnAdd, Blue, BlueDark);
            _btnAdd.Click += BtnAdd_Click;

            _btnDelete = new HopeButton
            {
                Text   = "\U0001F5D1  Delete",
                Font   = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size   = new Size(140, 46),
                Margin = new Padding(0, 4, 12, 0)
            };
            ConfigureOutlineHopeButton(_btnDelete, Color.FromArgb(231, 76, 60), Color.FromArgb(255, 240, 240));
            _btnDelete.Click += BtnDelete_Click;

            leftFlow.Controls.Add(_btnAdd);
            leftFlow.Controls.Add(_btnDelete);

            var rightFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents  = false,
                BackColor     = Color.Transparent,
                Margin        = new Padding(0),
                Padding       = new Padding(0)
            };

            _btnRefresh = new HopeButton
            {
                Text        = "\u27F3",
                Font        = new Font("Segoe UI", 18F, FontStyle.Bold),
                Size        = new Size(58, 58),
                MinimumSize = new Size(58, 58),
                MaximumSize = new Size(58, 58),
                Margin      = new Padding(0)
            };
            ConfigurePillHopeButton(_btnRefresh, Blue, BlueDark);
            _btnRefresh.Click += async (s, e) => await LoadAllAsync();
            _btnRefresh.Resize += (s, e) =>
            {
                using (var path = new GraphicsPath())
                {
                    int w = Math.Max(1, _btnRefresh.Width  - 1);
                    int h = Math.Max(1, _btnRefresh.Height - 1);
                    path.AddEllipse(0, 0, w, h);
                    _btnRefresh.Region = new Region(path);
                }
            };
            rightFlow.Controls.Add(_btnRefresh);

            buttonTable.Controls.Add(leftFlow,  0, 0);
            buttonTable.Controls.Add(rightFlow, 1, 0);
            buttonCard.Controls.Add(buttonTable);
            _buttonBarPanel.Controls.Add(buttonCard);

            // ── Summary cards ─────────────────────────────────────────────────
            _summaryPanel = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Top,
                Height    = 100,
                BackColor = Color.White,
                Padding   = new Padding(15, 14, 15, 8)
            };

            var summaryFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                BackColor     = Color.Transparent,
                Padding       = new Padding(0),
                Margin        = new Padding(0)
            };

            summaryFlow.Controls.Add(CreateSummaryCard("Total",         out _lblTotalCount,    Blue));
            summaryFlow.Controls.Add(CreateSummaryCard("With Dept",     out _lblWithDeptCount, Color.FromArgb(39,  174, 96)));
            summaryFlow.Controls.Add(CreateSummaryCard("No Department", out _lblNoDeptCount,   Color.FromArgb(149, 165, 166)));
            summaryFlow.Controls.Add(CreateSummaryCard("Factory",       out _lblFactoryCount,  Color.FromArgb(230, 126, 34)));
            _summaryPanel.Controls.Add(summaryFlow);

            // ── Instructions bar ──────────────────────────────────────────────
            var instructionsPanel = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Top,
                Height    = 34,
                BackColor = Color.FromArgb(232, 244, 253),
                Padding   = new Padding(16, 0, 16, 0)
            };
            instructionsPanel.Controls.Add(new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = BlueDark,
                AutoSize  = false,
                Text      = "  Browse Branch \u2192 Company \u2192 Department bindings.   |   \u2795 Add to create a new binding.   |   \u2611 Check rows \u2192 \U0001F5D1 Delete to remove."
            });

            // ── Grid ─────────────────────────────────────────────────────────
            _bodyPanel = new ReaLTaiizor.Controls.Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Padding   = new Padding(15, 12, 15, 15)
            };

            _gridCard = new MaterialCard
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Padding   = new Padding(10)
            };

            _dgv = new PoisonDataGridView
            {
                Dock                        = DockStyle.Fill,
                ReadOnly                    = false,
                AllowUserToAddRows          = false,
                AllowUserToDeleteRows       = false,
                AllowUserToResizeRows       = false,
                AllowUserToResizeColumns    = true,
                AutoGenerateColumns         = false,
                AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect                 = true,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight         = 40,
                BackgroundColor             = Color.White,
                GridColor                   = Color.White,
                BorderStyle                 = BorderStyle.None,
                CellBorderStyle             = DataGridViewCellBorderStyle.Single,
                ColumnHeadersBorderStyle    = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles   = false,
                ColumnHeadersVisible        = true,
                RowHeadersVisible           = false
            };

            _dgv.ColumnHeadersDefaultCellStyle.BackColor          = Blue;
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor          = Color.White;
            _dgv.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Blue;
            _dgv.ColumnHeadersDefaultCellStyle.Alignment          = DataGridViewContentAlignment.MiddleLeft;
            _dgv.ColumnHeadersDefaultCellStyle.Padding            = new Padding(8, 4, 0, 4);

            _dgv.DefaultCellStyle.BackColor          = Color.FromArgb(240, 246, 252);
            _dgv.DefaultCellStyle.Font               = new Font("Segoe UI", 9F);
            _dgv.DefaultCellStyle.ForeColor          = Color.FromArgb(40, 40, 40);
            _dgv.DefaultCellStyle.SelectionBackColor = BlueDark;
            _dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            _dgv.DefaultCellStyle.Padding            = new Padding(8, 6, 8, 6);
            _dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(232, 240, 248);
            _dgv.RowTemplate.Height    = 38;
            _dgv.RowTemplate.Resizable = DataGridViewTriState.False;

            // Checkbox column (select for delete)
            _dgv.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name       = "colSelect",
                HeaderText = "",
                Width      = 40,
                ReadOnly   = false,
                FalseValue = false,
                TrueValue  = true
            });
            DefaultListPageTemplate.StyleSelectionCheckBoxColumn(_dgv);

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId",        HeaderText = "ID",          DataPropertyName = "BranchDeptCompanyID", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 55,  ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBranch",    HeaderText = "Branch",      DataPropertyName = "BranchName",          FillWeight = 22, MinimumWidth = 130, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colType",      HeaderText = "Type",        DataPropertyName = "BranchType",          FillWeight = 11, MinimumWidth = 90,  ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCompany",   HeaderText = "Company",     DataPropertyName = "CompanyName",         FillWeight = 14, MinimumWidth = 100, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDept",      HeaderText = "Department",  DataPropertyName = "DepartmentName",      FillWeight = 29, MinimumWidth = 140, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCreated",   HeaderText = "Assigned On", DataPropertyName = "CreatedDate",         FillWeight = 13, MinimumWidth = 100, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy", Alignment = DataGridViewContentAlignment.MiddleRight } });

            _dgv.CellFormatting += DgvAssignment_CellFormatting;
            _dgv.CellPainting   += DgvAssignment_CellPainting;

            _dgv.CellValueChanged += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0)
                    DefaultListPageTemplate.UpdateSelectAllCheckBoxState(_selectAllCheckBox);
            };
            _dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_dgv.CurrentCell is DataGridViewCheckBoxCell)
                    _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _dgv.SelectionChanged += (s, e) =>
                DefaultListPageTemplate.SyncActionButtonVisualState(_dgv, _btnAdd, _btnDelete, _btnRefresh);

            _selectAllCheckBox = DefaultListPageTemplate.AddSelectAllCheckBox(_dgv);
            EnableSortingGlyphs();

            _gridCard.Controls.Add(_dgv);
            _bodyPanel.Controls.Add(_gridCard);

            // ── Pagination ────────────────────────────────────────────────────
            _paginationPanel = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 40,
                Padding   = new Padding(12, 5, 12, 5),
                BackColor = Color.White
            };

            _btnFirstPage = MakePagingButton("<<",   0);
            _btnPrevPage  = MakePagingButton("<",   50);
            _lblPageInfo  = new Label { AutoSize = false, Width = 340, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            _btnNextPage  = MakePagingButton(">",  450);
            _btnLastPage  = MakePagingButton(">>", 500);

            _btnFirstPage.Click += (s, e) => { _currentPage = 1;            RefreshPage(); };
            _btnPrevPage.Click  += (s, e) => { if (_currentPage > 1)      { _currentPage--; RefreshPage(); } };
            _btnNextPage.Click  += (s, e) => { if (_currentPage < MaxPage()) { _currentPage++; RefreshPage(); } };
            _btnLastPage.Click  += (s, e) => { _currentPage = MaxPage();   RefreshPage(); };

            _paginationPanel.Controls.AddRange(new Control[] { _btnFirstPage, _btnPrevPage, _lblPageInfo, _btnNextPage, _btnLastPage });

            // ── Status label ──────────────────────────────────────────────────
            _lblStatus = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 22,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(110, 110, 110),
                BackColor = Color.White,
                Padding   = new Padding(14, 4, 0, 0)
            };

            // ── Assemble (Controls added in reverse: last added = topmost) ────
            Controls.Add(_bodyPanel);
            Controls.Add(_paginationPanel);
            Controls.Add(_lblStatus);
            Controls.Add(_summaryPanel);
            Controls.Add(instructionsPanel);
            Controls.Add(_buttonBarPanel);
            Controls.Add(_headerPanel);

            ResumeLayout(true);

            DefaultListPageTemplate.SetupInitialPageFocus(_txtSearch, _dgv, _btnAdd, _btnDelete, _btnRefresh);
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static MaterialCard CreateSummaryCard(string title, out Label valueLabel, Color accent)
        {
            var card = new MaterialCard
            {
                Size      = new Size(160, 58),
                BackColor = Color.White,
                Padding   = new Padding(12),
                Margin    = new Padding(0, 0, 12, 8)
            };
            card.Controls.Add(new Label
            {
                Text      = title,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location  = new Point(12, 8)
            });
            valueLabel = new Label
            {
                Text      = "0",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = accent,
                Location  = new Point(12, 28)
            };
            card.Controls.Add(valueLabel);
            return card;
        }

        private void ConfigurePillHopeButton(HopeButton btn, Color baseColor, Color hoverColor)
        {
            btn.ButtonType    = HopeButtonType.Primary;
            btn.PrimaryColor  = baseColor;
            btn.DefaultColor  = baseColor;
            btn.BorderColor   = baseColor;
            btn.TextColor     = Color.White;
            btn.HoverTextColor = Color.White;
            btn.Cursor        = Cursors.Hand;
            btn.MouseEnter   += (s, e) => { btn.PrimaryColor = hoverColor; btn.DefaultColor = hoverColor; btn.BorderColor = hoverColor; btn.Invalidate(); };
            btn.MouseLeave   += (s, e) => { btn.PrimaryColor = baseColor;  btn.DefaultColor = baseColor;  btn.BorderColor = baseColor;  btn.Invalidate(); };
            btn.Resize       += (s, e) => MakePill(btn);
            MakePill(btn);
        }

        private void ConfigureOutlineHopeButton(HopeButton btn, Color borderColor, Color hoverBackColor)
        {
            btn.ButtonType    = HopeButtonType.Primary;
            btn.PrimaryColor  = Color.White;
            btn.DefaultColor  = Color.White;
            btn.BorderColor   = borderColor;
            btn.TextColor     = borderColor;
            btn.HoverTextColor = borderColor;
            btn.Cursor        = Cursors.Hand;
            btn.Paint += (s, e) =>
            {
                var b = s as Control;
                if (b == null || b.Width <= 1 || b.Height <= 1) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen  = new Pen(borderColor, 2.2f))
                using (var path = new GraphicsPath())
                {
                    pen.Alignment = PenAlignment.Inset;
                    var rect = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                    int d    = Math.Max(8, rect.Height);
                    path.AddArc(rect.X,          rect.Y, d, d, 90,  180);
                    path.AddLine(rect.X + d / 2, rect.Bottom, rect.Right - d / 2, rect.Bottom);
                    path.AddArc(rect.Right - d,  rect.Y, d, d, 270, 180);
                    path.AddLine(rect.Right - d / 2, rect.Y, rect.X + d / 2, rect.Y);
                    path.CloseFigure();
                    e.Graphics.DrawPath(pen, path);
                }
            };
            btn.MouseEnter += (s, e) => { btn.PrimaryColor = hoverBackColor; btn.DefaultColor = hoverBackColor; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { btn.PrimaryColor = Color.White;    btn.DefaultColor = Color.White;    btn.Invalidate(); };
            btn.Resize     += (s, e) => MakePill(btn);
            MakePill(btn);
        }

        private static void MakePill(Control c)
        {
            if (c == null || c.Width <= 0 || c.Height <= 0) return;
            int r = c.Height;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0,           0, r, r, 90,  180);
                path.AddArc(c.Width - r, 0, r, r, 270, 180);
                path.CloseAllFigures();
                c.Region = new Region(path);
            }
        }

        private static Label MakeHeaderLabel(string text, Point loc) =>
            new Label { Text = text, AutoSize = false, Width = loc.X < 600 ? 68 : 88, Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(100, 100, 100), Location = loc };

        private static ComboBox MakeHeaderCombo(Point loc, int width) =>
            new ComboBox { Location = loc, Width = width, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), FlatStyle = FlatStyle.Flat };

        private static System.Windows.Forms.Button MakePagingButton(string text, int left) =>
            new System.Windows.Forms.Button { Text = text, Width = 45, Height = 25, Left = left, Top = 5 };

        private int MaxPage() =>
            Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));

        private void DgvAssignment_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string colName = _dgv.Columns[e.ColumnIndex]?.Name;

            // Muted italic for "(No Department)" rows
            if (colName == "colDept" && e.Value?.ToString() == "N/A")
            {
                e.CellStyle.ForeColor = Color.FromArgb(160, 160, 160);
                e.CellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Italic);
                e.FormattingApplied   = true;
                return;
            }

            // Coloured badge text for BranchType
            if (colName == "colType")
            {
                switch (e.Value?.ToString())
                {
                    case "Factory":     e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34);  e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold); break;
                    case "Depot":       e.CellStyle.ForeColor = Color.FromArgb(41,  128, 185); e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold); break;
                    case "Center":      e.CellStyle.ForeColor = Color.FromArgb(142,  68, 173); e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold); break;
                    case "Distributor": e.CellStyle.ForeColor = Color.FromArgb(39,  174, 96);  e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold); break;
                    default:            e.CellStyle.ForeColor = Color.FromArgb(100, 100, 100); break;
                }
                e.FormattingApplied = true;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async Task LoadAllAsync()
        {
            _btnAdd.Enabled     = false;
            _btnDelete.Enabled  = false;
            _btnRefresh.Enabled = false;
            _lblStatus.Text     = "Loading\u2026";

            try
            {
                await LoadDataAsync();
                if (!_filtersLoaded)
                {
                    await LoadFilterDropdownsAsync();
                    _filtersLoaded = true;
                }
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load assignments:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnAdd.Enabled     = true;
                _btnDelete.Enabled  = true;
                _btnRefresh.Enabled = true;
            }
        }

        private async Task LoadDataAsync()
        {
            _all.Clear();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    SELECT
                        bdc.BranchDeptCompanyID,
                        bdc.BranchID,
                        bdc.CompanyID,
                        bdc.DepartmentID,
                        b.Name                          AS BranchName,
                        ISNULL(b.BranchType, N'Office') AS BranchType,
                        c.Name                          AS CompanyName,
                        ISNULL(d.Name, N'N/A')          AS DepartmentName,
                        bdc.CreatedDate
                    FROM       dbo.BranchDepartmentCompany bdc
                    JOIN       dbo.Branch     b ON b.BranchId = bdc.BranchID
                    JOIN       dbo.Company    c ON c.ComId    = bdc.CompanyID
                    LEFT JOIN  dbo.Department d ON d.DeptId   = bdc.DepartmentID
                    ORDER BY   c.Name, b.Name, d.Name", con))
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        _all.Add(new AssignmentRow
                        {
                            BranchDeptCompanyID = r.GetInt32(0),
                            BranchID            = r.GetInt32(1),
                            CompanyID           = r.GetInt32(2),
                            DepartmentID        = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                            BranchName          = r.IsDBNull(4) ? ""       : r.GetString(4),
                            BranchType          = r.IsDBNull(5) ? "Office" : r.GetString(5),
                            CompanyName         = r.IsDBNull(6) ? ""       : r.GetString(6),
                            DepartmentName      = r.IsDBNull(7) ? "N/A"   : r.GetString(7),
                            CreatedDate         = r.GetDateTime(8)
                        });
                    }
                }
            }
        }

        private async Task LoadFilterDropdownsAsync()
        {
            _filterCompanyIds.Add(null);  _filterCompanyNames.Add("All Companies");
            _filterDeptIds.Add(null);     _filterDeptNames.Add("All Departments");
            _filterDeptIds.Add(-1);       _filterDeptNames.Add("(No Department)");   // -1 = filter for NULL dept

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                    { _filterCompanyIds.Add(r.GetInt32(0)); _filterCompanyNames.Add(r.IsDBNull(1) ? "" : r.GetString(1)); }

                using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department WHERE Active=1 ORDER BY Name", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                    { _filterDeptIds.Add(r.GetInt32(0)); _filterDeptNames.Add(r.IsDBNull(1) ? "" : r.GetString(1)); }
            }

            _cmbFilterCompany.Items.AddRange(_filterCompanyNames.ToArray<object>());
            _cmbFilterDept.Items.AddRange(_filterDeptNames.ToArray<object>());
            _cmbFilterCompany.SelectedIndex = 0;
            _cmbFilterDept.SelectedIndex    = 0;
        }

        // ═════════════════════════════════════════════════════════════════════
        // FILTERING & PAGINATION
        // ═════════════════════════════════════════════════════════════════════

        private void ApplyFilter()
        {
            var data = _all.AsEnumerable();

            // Company
            if (_cmbFilterCompany.SelectedIndex > 0 && _filterCompanyIds.Count > _cmbFilterCompany.SelectedIndex)
            {
                int? cId = _filterCompanyIds[_cmbFilterCompany.SelectedIndex];
                if (cId.HasValue) data = data.Where(r => r.CompanyID == cId.Value);
            }

            // BranchType
            if (_cmbFilterBranchType.SelectedIndex > 0)
            {
                string type = _cmbFilterBranchType.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(type))
                    data = data.Where(r => string.Equals(r.BranchType, type, StringComparison.OrdinalIgnoreCase));
            }

            // Department — -1 means show only NULL-department rows
            if (_cmbFilterDept.SelectedIndex > 0 && _filterDeptIds.Count > _cmbFilterDept.SelectedIndex)
            {
                int? dId = _filterDeptIds[_cmbFilterDept.SelectedIndex];
                if (dId.HasValue && dId.Value == -1)
                    data = data.Where(r => r.DepartmentID == null);
                else if (dId.HasValue)
                    data = data.Where(r => r.DepartmentID == dId.Value);
            }

            // Text search (ignore placeholder)
            var term = _txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(term) && term != "Search by Branch, Company, or Department...")
            {
                data = data.Where(r =>
                    r.BranchName.IndexOf(term,     StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.BranchType.IndexOf(term,     StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.CompanyName.IndexOf(term,    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.DepartmentName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _filtered = data.ToList();
            if (_sortColumn != null)
                SortDataSource();
            _currentPage = 1;
            UpdateSummaryCards();
            RefreshPage();
        }

        private void RefreshPage()
        {
            int max  = MaxPage();
            _currentPage = Math.Max(1, Math.Min(_currentPage, max));

            var page = _filtered
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _dgv.DataSource = page;
            DefaultListPageTemplate.DisableDefaultRowHighlight(_dgv, _btnAdd, _btnDelete, _btnRefresh);

            // Restore sort glyph after DataSource rebind resets it
            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            if (_sortColumn != null)
                _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;
            _dgv.Invalidate();

            _lblPageInfo.Text     = $"Page {_currentPage} of {max}   ({_filtered.Count} total)";
            _lblStatus.Text       = $"{_filtered.Count} of {_all.Count} assignment(s) shown";
            _btnFirstPage.Enabled = _currentPage > 1;
            _btnPrevPage.Enabled  = _currentPage > 1;
            _btnNextPage.Enabled  = _currentPage < max;
            _btnLastPage.Enabled  = _currentPage < max;
        }

        private void UpdateSummaryCards()
        {
            _lblTotalCount.Text    = _filtered.Count.ToString();
            _lblWithDeptCount.Text = _filtered.Count(r => r.DepartmentID.HasValue).ToString();
            _lblNoDeptCount.Text   = _filtered.Count(r => !r.DepartmentID.HasValue).ToString();
            _lblFactoryCount.Text  = _filtered.Count(r =>
                string.Equals(r.BranchType, "Factory", StringComparison.OrdinalIgnoreCase)).ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddBranchAssignmentDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _ = LoadAllAsync();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // SORTING
        // ═════════════════════════════════════════════════════════════════════

        private void EnableSortingGlyphs()
        {
            if (_dgv == null) return;

            foreach (DataGridViewColumn col in _dgv.Columns)
            {
                col.SortMode =
                    col is DataGridViewCheckBoxColumn ||
                    col is DataGridViewButtonColumn  ||
                    col is DataGridViewImageColumn
                        ? DataGridViewColumnSortMode.NotSortable
                        : DataGridViewColumnSortMode.Programmatic;
            }

            _dgv.EnableHeadersVisualStyles       = false;
            _dgv.ColumnHeaderMouseClick         -= DgvAssignment_ColumnHeaderMouseClick;
            _dgv.ColumnHeaderMouseClick         += DgvAssignment_ColumnHeaderMouseClick;
        }

        private void DgvAssignment_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode == DataGridViewColumnSortMode.NotSortable) return;

            string prop = !string.IsNullOrWhiteSpace(col.DataPropertyName)
                ? col.DataPropertyName
                : col.Name;
            if (string.IsNullOrWhiteSpace(prop)) return;

            var dir = col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;

            _sortColumn = col;
            _sortOrder  = dir == System.ComponentModel.ListSortDirection.Ascending
                ? System.Windows.Forms.SortOrder.Ascending
                : System.Windows.Forms.SortOrder.Descending;

            SortDataSource();
            _currentPage = 1;
            RefreshPage();
        }

        private void SortDataSource()
        {
            if (_sortColumn == null || string.IsNullOrWhiteSpace(_sortColumn.DataPropertyName)) return;
            string prop = _sortColumn.DataPropertyName;
            try
            {
                _filtered = _sortOrder == System.Windows.Forms.SortOrder.Ascending
                    ? _filtered.OrderBy(x => x.GetType().GetProperty(prop)?.GetValue(x)).ToList()
                    : _filtered.OrderByDescending(x => x.GetType().GetProperty(prop)?.GetValue(x)).ToList();
            }
            catch { /* ignore reflection errors */ }
        }

        private void DgvAssignment_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var col = _dgv.Columns[e.ColumnIndex];

            // ── Checkbox column data cells — drawn manually so glyph is visible ──
            if (e.RowIndex >= 0 && col.Name == "colSelect")
            {
                e.Handled = true;

                // Row background (respect alternating rows and selection)
                bool rowSelected = _dgv.Rows[e.RowIndex].Selected;
                Color bg = rowSelected
                    ? _dgv.DefaultCellStyle.SelectionBackColor
                    : (e.RowIndex % 2 == 0
                        ? _dgv.DefaultCellStyle.BackColor
                        : _dgv.AlternatingRowsDefaultCellStyle.BackColor);
                using (var bgBrush = new SolidBrush(bg))
                    e.Graphics.FillRectangle(bgBrush, e.CellBounds);

                // Checkbox box
                const int BoxSize = 14;
                int bx = e.CellBounds.X + (e.CellBounds.Width  - BoxSize) / 2;
                int by = e.CellBounds.Y + (e.CellBounds.Height - BoxSize) / 2;
                var box = new Rectangle(bx, by, BoxSize, BoxSize);

                e.Graphics.FillRectangle(Brushes.White, box);
                using (var borderPen = new Pen(Color.FromArgb(160, 160, 160), 1.5f))
                    e.Graphics.DrawRectangle(borderPen, box);

                // Checkmark
                bool isChecked = e.Value is true;
                if (isChecked)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var tickPen = new Pen(Blue, 2f))
                    {
                        e.Graphics.DrawLine(tickPen, bx + 2,  by + 7,  bx + 5,  by + 10);
                        e.Graphics.DrawLine(tickPen, bx + 5,  by + 10, bx + 11, by + 3);
                    }
                    e.Graphics.SmoothingMode = SmoothingMode.Default;
                }
                return;
            }

            // ── Header rows ───────────────────────────────────────────────────────
            if (e.RowIndex >= 0) return;

            e.Handled = true;

            // Background: 1-px white gap acts as column separator
            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);
            var fill = new Rectangle(
                e.CellBounds.X,
                e.CellBounds.Y,
                e.CellBounds.Width  - 1,
                e.CellBounds.Height - 1);
            using (var brush = new SolidBrush(Blue))
                e.Graphics.FillRectangle(brush, fill);

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen,
                    e.CellBounds.Right - 1, e.CellBounds.Top,
                    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen,
                    e.CellBounds.Left,  e.CellBounds.Bottom - 1,
                    e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            // Sort glyph (sortable columns only)
            bool isSortable = col.SortMode == DataGridViewColumnSortMode.Programmatic;
            var textRect = e.CellBounds;

            if (isSortable)
            {
                textRect.Width -= 16;   // reserve room for glyph

                int gx = e.CellBounds.Right - 18;
                int gy = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

                Color arrowColor = col.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                    ? Color.White
                    : Color.FromArgb(160, 255, 255, 255);   // dimmed when inactive

                using (var pen = new Pen(arrowColor, 2))
                {
                    if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                    {
                        // ▲  up
                        e.Graphics.DrawLine(pen, gx,      gy + 6, gx + 5, gy);
                        e.Graphics.DrawLine(pen, gx + 5,  gy,     gx + 10, gy + 6);
                    }
                    else if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                    {
                        // ▼  down
                        e.Graphics.DrawLine(pen, gx,      gy,     gx + 5,  gy + 6);
                        e.Graphics.DrawLine(pen, gx + 5,  gy + 6, gx + 10, gy);
                    }
                    else
                    {
                        // ↕  both (inactive)
                        e.Graphics.DrawLine(pen, gx + 2,  gy + 2, gx + 5,  gy - 1);
                        e.Graphics.DrawLine(pen, gx + 5,  gy - 1, gx + 8,  gy + 2);
                        e.Graphics.DrawLine(pen, gx + 2,  gy + 4, gx + 5,  gy + 7);
                        e.Graphics.DrawLine(pen, gx + 5,  gy + 7, gx + 8,  gy + 4);
                    }
                }
            }

            // Header text (skip blank checkbox column header)
            if (!string.IsNullOrEmpty(col.HeaderText))
            {
                bool isCentred = col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.MiddleCenter;
                using (var textBrush = new SolidBrush(Color.White))
                using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center })
                {
                    if (isCentred)
                    {
                        fmt.Alignment = StringAlignment.Center;
                    }
                    else
                    {
                        fmt.Alignment = StringAlignment.Near;
                        textRect = new Rectangle(textRect.X + 8, textRect.Y, textRect.Width - 8, textRect.Height);
                    }
                    e.Graphics.DrawString(col.HeaderText, e.CellStyle.Font ?? _dgv.Font, textBrush, textRect, fmt);
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            // Collect rows checked via the checkbox column across all pages
            var ids   = new List<int>();
            var names = new List<string>();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.Cells["colSelect"]?.Value is true && row.DataBoundItem is AssignmentRow ar)
                {
                    ids.Add(ar.BranchDeptCompanyID);
                    names.Add($"  \u2022 {ar.BranchName} / {ar.CompanyName} / {ar.DepartmentName}");
                }
            }

            if (ids.Count == 0)
            {
                MessageBox.Show("Check one or more rows to delete.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete {ids.Count} assignment(s)?\n\n{string.Join("\n", names)}\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var tx = con.BeginTransaction())
                    {
                        foreach (int id in ids)
                        {
                            using (var cmd = new SqlCommand(
                                "DELETE FROM dbo.BranchDepartmentCompany WHERE BranchDeptCompanyID = @Id",
                                con, tx))
                            {
                                cmd.Parameters.AddWithValue("@Id", id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                    }
                }
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete failed:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
