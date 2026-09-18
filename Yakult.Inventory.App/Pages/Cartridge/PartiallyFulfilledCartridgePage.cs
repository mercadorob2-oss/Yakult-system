using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Panel = System.Windows.Forms.Panel;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Read-only page showing partially-fulfilled cartridge exchange sets.
    /// Displays the full exchange set (all models) grouped by submission session,
    /// so you can see which models are still pending alongside which are fulfilled.
    ///
    /// Fulfilled rows: light green background (read-only).
    /// Pending rows:   light orange background (still awaiting stock).
    /// </summary>
    public partial class PartiallyFulfilledCartridgePage : UserControl
    {
        private readonly UnfulfilledCartridgeExchangeRepository _repository;
        private readonly CartridgeManagementRepository _cartridgeRepo;

        private DefaultListPageLayout _layout;

        private List<SessionGroup> _allSessions;
        private List<SessionGroup> _filteredSessions;

        private MaterialCard _cardCount;
        private MaterialCard _cardIssued;
        private MaterialCard _cardPending;
        private Label _lblCount;
        private Label _lblIssued;
        private Label _lblPending;

        private Panel _remarksPanel;
        private Label _lblRemarks;

        private FlowLayoutPanel _sessionsFlow;
        private Panel _scrollContainer;

        private HopeButton _btnFulfill;
        private UnfulfilledCartridgeExchangeDto _selectedDto;

        // Pagination
        private int _currentPage = 1;
        private const int PageSize = 10;
        private System.Windows.Forms.Button _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;
        private Label _lblPageInfo;

        // -----------------------------------------------------------------------
        // Session grouping
        // -----------------------------------------------------------------------
        private sealed class SessionGroup
        {
            public int?  SetId     { get; set; }
            public Guid? SessionId { get; set; }
            public List<UnfulfilledCartridgeExchangeDto> Rows { get; set; } = new List<UnfulfilledCartridgeExchangeDto>();

            public bool IsMultiModel => Rows.Count > 1;
            public string RequesterName => Rows.FirstOrDefault()?.RequesterName ?? "Unknown";
            public string BranchName    => Rows.FirstOrDefault()?.BranchName;
            public int TotalIssued  => Rows.Sum(r => r.IssuedFullQty);
            public int TotalPending => Rows.Sum(r => r.UnfulfilledQty);
        }

        // -----------------------------------------------------------------------
        // Fulfill dialog support
        // -----------------------------------------------------------------------
        private sealed class FulfillRowState
        {
            public UnfulfilledCartridgeExchangeDto Dto { get; set; }
            public int AvailBrandNew { get; set; }
            public int AvailRefilled { get; set; }
            public NumericUpDown NudBrandNew { get; set; }
            public NumericUpDown NudRefilled { get; set; }
            public Label LblIssued  { get; set; }
            public Label LblPending { get; set; }
            public Label LblStatus  { get; set; }
            public TextBox TxtRemarks { get; set; }
            public int TotalToIssue => (int)(NudBrandNew?.Value ?? 0) + (int)(NudRefilled?.Value ?? 0);
            public int PendingAfter => Math.Max(0, Dto.UnfulfilledQty - TotalToIssue);
            public int IssuedAfter  => Dto.IssuedFullQty + TotalToIssue;
        }

        // -----------------------------------------------------------------------
        // Constructor / init
        // -----------------------------------------------------------------------
        public PartiallyFulfilledCartridgePage()
        {
            _repository    = new UnfulfilledCartridgeExchangeRepository();
            _cartridgeRepo = new CartridgeManagementRepository();
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            Dock = DockStyle.Fill;
            ResumeLayout(false);
        }

        // -----------------------------------------------------------------------
        // UI construction
        // -----------------------------------------------------------------------
        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Partially Fulfilled Cartridge Exchanges",
                "Search by requester, branch, department, model, request #...",
                (s, e) => ApplyFilter(),
                () => LoadData());

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[] { "All Fields", "Requester", "Branch", "Department", "Model", "Req #" });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            }

            if (_layout.SortByComboBox != null)
            {
                _layout.SortByComboBox.Items.Clear();
                _layout.SortByComboBox.Items.AddRange(new object[] { "Set # (A→Z)", "Set # (Z→A)" });
                _layout.SortByComboBox.SelectedIndex = 0;
                _layout.SortByComboBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            }

            // Summary cards
            _cardCount   = UiFactory.CreateSummaryCard("Sessions",     out _lblCount,   Color.FromArgb(52, 152, 219));
            _cardIssued  = UiFactory.CreateSummaryCard("Total Issued",  out _lblIssued,  Color.FromArgb(46, 204, 113));
            _cardPending = UiFactory.CreateSummaryCard("Still Pending", out _lblPending, Color.FromArgb(231, 76, 60));

            _layout.SummaryFlow.Controls.Add(_cardCount);
            _layout.SummaryFlow.Controls.Add(_cardIssued);
            _layout.SummaryFlow.Controls.Add(_cardPending);

            // Sessions flow (vertically stacked, scrollable)
            _sessionsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 8, 8, 8)
            };

            _scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 247, 250)
            };
            _scrollContainer.Controls.Add(_sessionsFlow);

            // When the scroll container resizes, update all block widths
            _scrollContainer.SizeChanged += ScrollContainer_SizeChanged;

            // Remarks panel
            _remarksPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Colors.CardBack,
                Padding = new Padding(12, 8, 12, 8)
            };

            var lblDetailsTitle = new Label
            {
                Text = "Remarks:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(12, 8),
                ForeColor = UiTheme.Colors.TextMuted
            };

            _lblRemarks = new Label
            {
                Text = "Click a row to view remarks",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = UiTheme.Colors.TextMuted,
                AutoSize = false,
                Location = new Point(12, 28),
                Size = new Size(1000, 42)
            };

            _remarksPanel.Controls.Add(lblDetailsTitle);
            _remarksPanel.Controls.Add(_lblRemarks);
            _remarksPanel.Resize += (s, e) =>
            {
                if (_lblRemarks != null)
                    _lblRemarks.Size = new Size(Math.Max(0, _remarksPanel.ClientSize.Width - 24), 42);
            };

            // Body: scroll area on top, remarks strip at bottom
            var bodyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bodyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            bodyLayout.Controls.Add(_scrollContainer, 0, 0);
            bodyLayout.Controls.Add(_remarksPanel, 0, 1);

            _layout.GridCard.Controls.Clear();
            _layout.GridCard.Controls.Add(bodyLayout);

            // Fulfill button
            _btnFulfill = new HopeButton
            {
                Text    = "✅  Fulfill Selected",
                Font    = UiTheme.Fonts.Button,
                Size    = new Size(190, UiTheme.Sizes.PillButtonHeight),
                Margin  = new Padding(0, 6, 12, 0),
                Enabled = false
            };
            UiFactory.ConfigureOutlineHopeButton(_btnFulfill, Color.FromArgb(46, 204, 113), Color.FromArgb(230, 245, 230));
            _btnFulfill.Click += BtnFulfill_Click;
            _layout.ButtonLeftFlow.Controls.Add(_btnFulfill);

            // Pagination controls
            _btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 7 };
            _btnPrevPage  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 7 };
            _lblPageInfo  = new Label { AutoSize = false, Width = 200, Left = 100, Top = 12, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            _btnNextPage  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 305, Top = 7 };
            _btnLastPage  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 355, Top = 7 };
            _btnFirstPage.Click += (s, e) => { _currentPage = 1;               UpdatePagination(); };
            _btnPrevPage.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdatePagination(); } };
            _btnNextPage.Click  += (s, e) => { _currentPage++;                 UpdatePagination(); };
            _btnLastPage.Click  += (s, e) => { _currentPage = int.MaxValue;    UpdatePagination(); };
            _layout.PaginationPanel.Controls.AddRange(new Control[] { _btnFirstPage, _btnPrevPage, _lblPageInfo, _btnNextPage, _btnLastPage });

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);
        }

        private void ScrollContainer_SizeChanged(object sender, EventArgs e)
        {
            int newWidth = _scrollContainer.ClientSize.Width - 24;
            if (newWidth < 200) return;

            // Update widths of all existing blocks without rebuilding
            foreach (Control ctrl in _sessionsFlow.Controls)
            {
                if (!(ctrl is Panel block)) continue;
                block.Width = newWidth;
                foreach (Control child in block.Controls)
                {
                    if (child.Tag as string == "toggle")
                        child.Left = newWidth - child.Width;   // reposition, don't resize
                    else
                        child.Width = newWidth;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Data
        // -----------------------------------------------------------------------
        private void LoadData()
        {
            try
            {
                var rows = _repository.GetPartiallyFulfilledExchangesFullSet();
                _allSessions = GroupIntoSessions(rows ?? new List<UnfulfilledCartridgeExchangeDto>());
                ApplyFilter();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static List<SessionGroup> GroupIntoSessions(List<UnfulfilledCartridgeExchangeDto> rows)
        {
            var result    = new List<SessionGroup>();
            var setMap    = new Dictionary<int, SessionGroup>();   // keyed by SetId
            var sessionMap = new Dictionary<Guid, SessionGroup>(); // fallback: keyed by SubmissionSessionId

            foreach (var row in rows)
            {
                // Primary grouping: SetId (written by EnsureSharedSetForPortalCartridgeGroup)
                if (row.SetId.HasValue && row.SetId.Value > 0)
                {
                    if (!setMap.TryGetValue(row.SetId.Value, out var grp))
                    {
                        grp = new SessionGroup
                        {
                            SetId     = row.SetId,
                            SessionId = row.SubmissionSessionId
                        };
                        setMap[row.SetId.Value] = grp;
                        result.Add(grp);
                    }
                    grp.Rows.Add(row);
                }
                // Fallback: SubmissionSessionId (portal requests not yet grouped into a Set)
                else if (row.SubmissionSessionId.HasValue)
                {
                    if (!sessionMap.TryGetValue(row.SubmissionSessionId.Value, out var grp))
                    {
                        grp = new SessionGroup { SessionId = row.SubmissionSessionId };
                        sessionMap[row.SubmissionSessionId.Value] = grp;
                        result.Add(grp);
                    }
                    grp.Rows.Add(row);
                }
                else
                {
                    // Legacy (no Set, no session): each row is its own block
                    var grp = new SessionGroup();
                    grp.Rows.Add(row);
                    result.Add(grp);
                }
            }

            return result;
        }

        private void UpdateSummary()
        {
            if (_allSessions == null) return;

            if (_lblCount   != null) _lblCount.Text   = _allSessions.Count.ToString();
            if (_lblIssued  != null) _lblIssued.Text  = _allSessions.Sum(s => s.TotalIssued).ToString();
            if (_lblPending != null) _lblPending.Text = _allSessions.Sum(s => s.TotalPending).ToString();
        }

        // -----------------------------------------------------------------------
        // Filtering
        // -----------------------------------------------------------------------
        private void ApplyFilter()
        {
            if (_allSessions == null) return;

            string search  = (_layout?.SearchBox?.Text ?? string.Empty).Trim();
            bool hasSearch = !string.IsNullOrWhiteSpace(search)
                          && search != "Search by requester, branch, department, model, request #...";
            string filterBy = _layout?.FilterByComboBox?.SelectedItem?.ToString() ?? "All Fields";

            _filteredSessions = hasSearch
                ? _allSessions.Where(s => SessionMatchesSearch(s, search, filterBy)).ToList()
                : _allSessions.ToList();

            // Sort
            string sortBy = _layout?.SortByComboBox?.SelectedItem?.ToString() ?? "";
            _filteredSessions = sortBy == "Set # (Z→A)"
                ? _filteredSessions.OrderByDescending(s => s.SetId ?? int.MaxValue).ToList()
                : _filteredSessions.OrderBy(s => s.SetId ?? int.MaxValue).ToList();

            _currentPage = 1;
            UpdatePagination();
        }

        private static bool SessionMatchesSearch(SessionGroup session, string search, string filterBy)
        {
            return session.Rows.Any(r => RowMatchesSearch(r, search, filterBy));
        }

        private static bool RowMatchesSearch(UnfulfilledCartridgeExchangeDto x, string search, string filterBy)
        {
            if (x == null) return false;
            bool Has(string v) => !string.IsNullOrEmpty(v) && v.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

            switch (filterBy)
            {
                case "Requester":   return Has(x.RequesterName);
                case "Branch":      return Has(x.BranchName);
                case "Department":  return Has(x.DepartmentName);
                case "Model":       return Has(x.CartridgeModel);
                case "Req #":       return Has(x.ReqId.ToString());
                default:
                    return Has(x.RequesterName)
                        || Has(x.BranchName)
                        || Has(x.DepartmentName)
                        || Has(x.CartridgeModel)
                        || Has(x.ReqId.ToString());
            }
        }

        // -----------------------------------------------------------------------
        // Session blocks
        // -----------------------------------------------------------------------
        private void UpdatePagination()
        {
            int total = _filteredSessions?.Count ?? 0;
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1)          _currentPage = 1;

            var paged = _filteredSessions == null
                ? new List<SessionGroup>()
                : _filteredSessions.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

            RebuildSessionBlocks(paged);

            if (_lblPageInfo != null)
                _lblPageInfo.Text = total == 0
                    ? "Page 0 of 0"
                    : $"Page {_currentPage} of {totalPages}  ({total} sets)";

            if (_btnFirstPage != null) _btnFirstPage.Enabled = _currentPage > 1;
            if (_btnPrevPage  != null) _btnPrevPage.Enabled  = _currentPage > 1;
            if (_btnNextPage  != null) _btnNextPage.Enabled  = _currentPage < totalPages;
            if (_btnLastPage  != null) _btnLastPage.Enabled  = _currentPage < totalPages;
        }

        private void RebuildSessionBlocks(List<SessionGroup> sessions)
        {
            _sessionsFlow.SuspendLayout();
            _sessionsFlow.Controls.Clear();

            if (sessions == null || sessions.Count == 0)
            {
                var lbl = new Label
                {
                    Text      = "No partially fulfilled cartridge exchange sets found.",
                    Font      = new Font("Segoe UI", 10F, FontStyle.Regular),
                    ForeColor = UiTheme.Colors.TextMuted,
                    AutoSize  = true,
                    Margin    = new Padding(16, 24, 16, 8)
                };
                _sessionsFlow.Controls.Add(lbl);
            }
            else
            {
                int blockWidth = _scrollContainer.ClientSize.Width > 100
                    ? _scrollContainer.ClientSize.Width - 24
                    : 800;

                foreach (var session in sessions)
                {
                    var block = CreateSessionBlock(session, blockWidth);
                    _sessionsFlow.Controls.Add(block);
                }
            }

            _sessionsFlow.ResumeLayout(true);
        }

        private Panel CreateSessionBlock(SessionGroup session, int width)
        {
            const int BlockHeaderHeight = 34;
            const int HeaderRowHeight   = 28;
            const int DataRowHeight     = 32;
            const int GridBorderExtra   = 2;
            const int ToggleWidth       = 34;

            // Slightly darker teal for the Set header, standard teal for DGV column headers
            Color headerBack    = Color.FromArgb(0, 120, 108);
            Color dgvHeaderBack = Color.FromArgb(0, 150, 136);

            // ── Header text: Set #, Owner, Location, Pending summary ─────
            string requester  = session.RequesterName;
            string branch     = session.BranchName;
            int    pending    = session.TotalPending;

            string setLabel   = session.SetId.HasValue ? $"Set #{session.SetId}" : "Exchange";
            string headerText = $"  {setLabel}  |  {requester}";
            if (!string.IsNullOrWhiteSpace(branch))
                headerText += $"  |  {branch}";
            if (pending > 0)
                headerText += $"  |  Pending: {pending}";

            // ── DataGridView ──────────────────────────────────────────────
            var dgv = new DataGridView
            {
                ReadOnly                  = true,
                AllowUserToAddRows        = false,
                AllowUserToDeleteRows     = false,
                AllowUserToResizeRows     = false,
                AllowUserToResizeColumns  = true,
                AutoGenerateColumns       = false,
                SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect               = false,
                BackgroundColor           = Color.White,
                GridColor                 = Color.FromArgb(225, 225, 225),
                BorderStyle               = BorderStyle.None,
                CellBorderStyle           = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle  = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                ColumnHeadersVisible      = true,
                RowHeadersVisible         = false,
                AutoSizeColumnsMode       = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars                = ScrollBars.None,
                Dock                      = DockStyle.None
            };

            dgv.ColumnHeadersDefaultCellStyle.BackColor          = dgvHeaderBack;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor          = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgvHeaderBack;
            dgv.ColumnHeadersDefaultCellStyle.Padding            = new Padding(6, 3, 0, 3);
            dgv.ColumnHeadersHeight                              = HeaderRowHeight;
            dgv.ColumnHeadersHeightSizeMode                      = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.DefaultCellStyle.Font                            = new Font("Segoe UI", 8.5F);
            dgv.DefaultCellStyle.Padding                         = new Padding(6, 4, 6, 4);
            dgv.RowTemplate.Height                               = DataRowHeight;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReqId",              HeaderText = "Req #",    FillWeight = 7,  MinimumWidth = 60,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CartridgeModel",     HeaderText = "Model",    FillWeight = 23, MinimumWidth = 120 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReturnedEmptyQty",   HeaderText = "Returned", FillWeight = 9,  MinimumWidth = 75,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IssuedFullQty",      HeaderText = "Issued",   FillWeight = 9,  MinimumWidth = 75,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnfulfilledQty",     HeaderText = "Pending",  FillWeight = 9,  MinimumWidth = 75,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FulfillmentStatusDisplay", HeaderText = "Status", FillWeight = 14, MinimumWidth = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedDate",        HeaderText = "Created",  FillWeight = 10, MinimumWidth = 90,  DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Remarks",            HeaderText = "Remarks",  FillWeight = 25, MinimumWidth = 160 });

            foreach (DataGridViewColumn col in dgv.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgv.DataSource = new List<UnfulfilledCartridgeExchangeDto>(session.Rows);
            dgv.DataBindingComplete += (s, e) => dgv.ClearSelection();

            // Alternating white / light-gray rows; teal selection
            dgv.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= session.Rows.Count) return;
                dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor          = e.RowIndex % 2 == 0 ? Color.White : Color.FromArgb(248, 248, 248);
                dgv.Rows[e.RowIndex].DefaultCellStyle.ForeColor          = Color.FromArgb(50, 50, 50);
                dgv.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = dgvHeaderBack;
                dgv.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = Color.White;
            };

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= session.Rows.Count) return;
                var dto     = session.Rows[e.RowIndex];
                string prop = dgv.Columns[e.ColumnIndex].DataPropertyName;

                if (prop == "FulfillmentStatusDisplay")
                {
                    e.CellStyle.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    e.CellStyle.ForeColor = dto.Status == "Fulfilled"
                        ? Color.FromArgb(39, 174, 96)
                        : dto.IssuedFullQty > 0
                            ? Color.FromArgb(230, 126, 34)
                            : Color.FromArgb(192, 57, 43);
                }
                else if (prop == "UnfulfilledQty" && dto.UnfulfilledQty > 0)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                    e.CellStyle.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }
                else if (prop == "IssuedFullQty" && dto.IssuedFullQty > 0)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                    e.CellStyle.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                }
            };

            dgv.SelectionChanged += (s, e) =>
            {
                if (dgv.SelectedRows.Count > 0
                    && dgv.SelectedRows[0].DataBoundItem is UnfulfilledCartridgeExchangeDto dto)
                {
                    _selectedDto = dto;
                    if (_btnFulfill != null)
                        _btnFulfill.Enabled = dto.Status == "Pending" && dto.UnfulfilledQty > 0;
                    ShowRemarks(dto.Remarks, dto.FulfilledRemarks, dto.Status);
                }
                else
                {
                    _selectedDto = null;
                    if (_btnFulfill != null) _btnFulfill.Enabled = false;
                }
            };

            int dgvHeight      = HeaderRowHeight + session.Rows.Count * DataRowHeight + GridBorderExtra;
            int expandedHeight = BlockHeaderHeight + dgvHeight;
            dgv.Width          = width;
            dgv.Height         = dgvHeight;
            dgv.Location       = new Point(0, BlockHeaderHeight);

            // ── Block panel ───────────────────────────────────────────────
            var block = new Panel
            {
                Width       = width,
                Height      = expandedHeight,
                Margin      = new Padding(0, 4, 0, 14),
                BackColor   = Color.White,
                BorderStyle = BorderStyle.None
            };

            // ── Set header label ──────────────────────────────────────────
            var lblHeader = new Label
            {
                Text      = headerText,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = headerBack,
                AutoSize  = false,
                Width     = width - ToggleWidth,
                Height    = BlockHeaderHeight,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0),
                Location  = new Point(0, 0),
                Cursor    = Cursors.Hand
            };

            // ── Collapse / expand toggle ──────────────────────────────────
            bool collapsed = false;
            var btnToggle = new Label
            {
                Text      = "−",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = headerBack,
                AutoSize  = false,
                Width     = ToggleWidth,
                Height    = BlockHeaderHeight,
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(width - ToggleWidth, 0),
                Cursor    = Cursors.Hand,
                Tag       = "toggle"
            };

            EventHandler toggleCollapse = (s, e) =>
            {
                collapsed      = !collapsed;
                dgv.Visible    = !collapsed;
                block.Height   = collapsed ? BlockHeaderHeight : expandedHeight;
                btnToggle.Text = collapsed ? "+" : "−";
            };
            lblHeader.Click += toggleCollapse;
            btnToggle.Click += toggleCollapse;

            block.Controls.Add(dgv);
            block.Controls.Add(lblHeader);
            block.Controls.Add(btnToggle);

            // Subtle left accent stripe + bottom divider line
            block.Paint += (s, e) =>
            {
                using (var brush = new SolidBrush(headerBack))
                    e.Graphics.FillRectangle(brush, 0, 0, 4, block.Height);
                using (var pen = new Pen(Color.FromArgb(210, 210, 210), 1))
                    e.Graphics.DrawLine(pen, 0, block.Height - 1, block.Width - 1, block.Height - 1);
            };

            return block;
        }

        private void BtnFulfill_Click(object sender, EventArgs e)
        {
            var dto = _selectedDto;
            if (dto == null || dto.Status != "Pending" || dto.UnfulfilledQty <= 0)
            {
                MessageBox.Show("Select a pending row to fulfill.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Gather all pending rows in the same session/Set
            var session = _filteredSessions?.FirstOrDefault(s => s.Rows.Contains(dto))
                       ?? _allSessions?.FirstOrDefault(s => s.Rows.Contains(dto));

            var pendingRows = session?.Rows
                .Where(r => r.Status == "Pending" && r.UnfulfilledQty > 0)
                .ToList()
                ?? new List<UnfulfilledCartridgeExchangeDto> { dto };

            ShowFulfillDialog(pendingRows);
        }

        private void ShowFulfillDialog(List<UnfulfilledCartridgeExchangeDto> pendingRows)
        {
            if (pendingRows == null || pendingRows.Count == 0) return;

            // Resolve Brand New / Refilled stock per model
            var states = new List<FulfillRowState>();
            foreach (var row in pendingRows)
            {
                int brandNew = 0, refilled = 0;
                try
                {
                    int? modelId = _cartridgeRepo.GetCartridgeModelIdByModelNumber(row.CartridgeModel);
                    brandNew = _cartridgeRepo.GetAvailableIssuableStockByCondition(modelId, "Brand New");
                    refilled = _cartridgeRepo.GetAvailableIssuableStockByCondition(modelId, "Refilled");
                }
                catch { }
                states.Add(new FulfillRowState { Dto = row, AvailBrandNew = brandNew, AvailRefilled = refilled });
            }

            const int DlgWidth = 560;
            const int CardPad  = 14;
            int cardWidth = DlgWidth - CardPad * 2 - 18;

            using (var dlg = new Form())
            {
                dlg.Text = pendingRows.Count == 1
                    ? $"Fulfill — Req #{pendingRows[0].ReqId}  |  {pendingRows[0].CartridgeModel}"
                    : $"Fulfill Cartridge Exchange — {pendingRows.Count} Models";
                dlg.StartPosition   = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox     = false;
                dlg.MinimizeBox     = false;

                // ── Header bar ────────────────────────────────────────────
                var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.FromArgb(0, 120, 108) };
                pnlHeader.Controls.Add(new Label
                {
                    Text = pendingRows.Count == 1
                        ? $"Req #{pendingRows[0].ReqId}  ·  {pendingRows[0].RequesterName}"
                        : $"Req #{pendingRows[0].ReqId}  ·  {pendingRows[0].RequesterName}  ·  {pendingRows.Count} Models",
                    Font      = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor = Color.White,
                    AutoSize  = false,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding   = new Padding(16, 0, 0, 0)
                });

                // ── Scrollable cards area ─────────────────────────────────
                var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(245, 247, 250) };
                var cardsFlow = new FlowLayoutPanel
                {
                    Dock          = DockStyle.Top,
                    AutoSize      = true,
                    AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents  = false,
                    BackColor     = Color.FromArgb(245, 247, 250),
                    Padding       = new Padding(CardPad, CardPad, CardPad, 4)
                };
                foreach (var state in states)
                {
                    var card = BuildFulfillCard(state, cardWidth);
                    card.Margin = new Padding(0, 0, 0, 10);
                    cardsFlow.Controls.Add(card);
                }
                scrollPanel.Controls.Add(cardsFlow);

                // ── Footer ────────────────────────────────────────────────
                var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.White };
                pnlFooter.Paint += (s, e2) =>
                    e2.Graphics.DrawLine(new Pen(Color.FromArgb(210, 210, 210)), 0, 0, pnlFooter.Width, 0);

                var btnConfirm = new System.Windows.Forms.Button
                {
                    Text = "Confirm", DialogResult = DialogResult.OK,
                    Size = new Size(96, 30), FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand
                };
                btnConfirm.FlatAppearance.BorderSize = 0;

                var btnCancel = new System.Windows.Forms.Button
                {
                    Text = "Cancel", DialogResult = DialogResult.Cancel,
                    Size = new Size(80, 30), FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(200, 200, 200), ForeColor = Color.FromArgb(60, 60, 60),
                    Font = new Font("Segoe UI", 9F), Cursor = Cursors.Hand
                };
                btnCancel.FlatAppearance.BorderSize = 0;

                pnlFooter.Controls.AddRange(new Control[] { btnConfirm, btnCancel });
                pnlFooter.Resize += (s, e2) =>
                {
                    int r = pnlFooter.Width - 12;
                    btnCancel.Location  = new Point(r - btnCancel.Width, (pnlFooter.Height - btnCancel.Height) / 2);
                    btnConfirm.Location = new Point(btnCancel.Left - btnConfirm.Width - 8, btnCancel.Top);
                };

                int scrollH = Math.Min(states.Count * 230 + CardPad * 2, 480);
                dlg.ClientSize  = new Size(DlgWidth, pnlHeader.Height + scrollH + pnlFooter.Height);
                dlg.Controls.Add(scrollPanel);
                dlg.Controls.Add(pnlFooter);
                dlg.Controls.Add(pnlHeader);
                dlg.AcceptButton = btnConfirm;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (states.All(st => st.TotalToIssue <= 0))
                {
                    MessageBox.Show("No quantity entered to issue.", "Nothing to Issue",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                    foreach (var st in states)
                    {
                        if (st.TotalToIssue <= 0) continue;
                        string remarks = string.IsNullOrWhiteSpace(st.TxtRemarks?.Text)
                            ? CartridgeExchangeRemarks.GenerateRemarks(st.IssuedAfter, st.Dto.ReturnedEmptyQty, st.Dto.CartridgeModel)
                            : st.TxtRemarks.Text.Trim();
                        _repository.FulfillExchange(st.Dto.UnfulfilledId, st.TotalToIssue, userId, remarks);
                    }

                    MessageBox.Show("Fulfillment completed successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    _selectedDto = null;
                    if (_btnFulfill != null) _btnFulfill.Enabled = false;
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error fulfilling exchange:\n\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static Panel BuildFulfillCard(FulfillRowState state, int width)
        {
            var dto         = state.Dto;
            const int LblW  = 88;
            const int NudW  = 68;

            var card = new Panel { Width = width, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            int y = 12;

            // ── Model header ──────────────────────────────────────────────
            card.Controls.Add(new Label
            {
                Text      = dto.CartridgeModel ?? "—",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize  = true,
                Location  = new Point(12, y)
            });
            y += 22;

            card.Controls.Add(new Label
            {
                Text      = $"Req #{dto.ReqId}  ·  Returned: {dto.ReturnedEmptyQty}  ·  Previously Issued: {dto.IssuedFullQty}  ·  Still Pending: {dto.UnfulfilledQty}",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize  = true,
                Location  = new Point(12, y)
            });
            y += 18;

            card.Controls.Add(new Panel { Location = new Point(12, y), Size = new Size(width - 26, 1), BackColor = Color.FromArgb(226, 232, 240) });
            y += 10;

            // ── Step 3A: Qty to Issue ─────────────────────────────────────
            card.Controls.Add(new Label
            {
                Text = "Qty to Issue",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Location = new Point(12, y)
            });
            y += 18;

            // Brand New row
            card.Controls.Add(new Label
            {
                Text = "Brand New:", Font = new Font("Segoe UI", 8.5F),
                AutoSize = false, Width = LblW, Height = 22,
                Location = new Point(12, y + 2), TextAlign = ContentAlignment.MiddleLeft
            });
            int maxBN  = Math.Min(state.AvailBrandNew, dto.UnfulfilledQty);
            var nudBN  = new NumericUpDown { Minimum = 0, Maximum = maxBN, Value = 0, Width = NudW, Location = new Point(12 + LblW, y), Font = new Font("Segoe UI", 9F) };
            card.Controls.Add(nudBN);
            card.Controls.Add(new Label
            {
                Text      = $"({state.AvailBrandNew} available)",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = state.AvailBrandNew > 0 ? Color.FromArgb(21, 128, 61) : Color.FromArgb(180, 60, 60),
                AutoSize  = true,
                Location  = new Point(12 + LblW + NudW + 6, y + 4)
            });
            state.NudBrandNew = nudBN;
            y += 28;

            // Refilled row
            card.Controls.Add(new Label
            {
                Text = "Refilled:", Font = new Font("Segoe UI", 8.5F),
                AutoSize = false, Width = LblW, Height = 22,
                Location = new Point(12, y + 2), TextAlign = ContentAlignment.MiddleLeft
            });
            int maxRF  = Math.Min(state.AvailRefilled, dto.UnfulfilledQty);
            var nudRF  = new NumericUpDown { Minimum = 0, Maximum = maxRF, Value = 0, Width = NudW, Location = new Point(12 + LblW, y), Font = new Font("Segoe UI", 9F) };
            card.Controls.Add(nudRF);
            card.Controls.Add(new Label
            {
                Text      = $"({state.AvailRefilled} available)",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = state.AvailRefilled > 0 ? Color.FromArgb(21, 128, 61) : Color.FromArgb(180, 60, 60),
                AutoSize  = true,
                Location  = new Point(12 + LblW + NudW + 6, y + 4)
            });
            state.NudRefilled = nudRF;
            y += 28;

            card.Controls.Add(new Panel { Location = new Point(12, y), Size = new Size(width - 26, 1), BackColor = Color.FromArgb(226, 232, 240) });
            y += 10;

            // ── Step 3B: Live summary ─────────────────────────────────────
            card.Controls.Add(new Label
            {
                Text = "Fulfillment Summary",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Location = new Point(12, y)
            });
            y += 18;

            var lblIssued  = new Label { Font = new Font("Segoe UI", 8.5F), AutoSize = true, Location = new Point(12, y) };
            var lblPending = new Label { Font = new Font("Segoe UI", 8.5F), AutoSize = true, Location = new Point(130, y) };
            var lblStatus  = new Label { Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), AutoSize = true, Location = new Point(250, y) };
            state.LblIssued  = lblIssued;
            state.LblPending = lblPending;
            state.LblStatus  = lblStatus;
            card.Controls.AddRange(new Control[] { lblIssued, lblPending, lblStatus });
            y += 22;

            card.Controls.Add(new Label
            {
                Text = "Remarks:", Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(71, 85, 105), AutoSize = true,
                Location = new Point(12, y)
            });
            y += 16;

            var txtRemarks = new TextBox
            {
                Location = new Point(12, y),
                Width    = width - 28,
                Font     = new Font("Segoe UI", 8.5F)
            };
            state.TxtRemarks = txtRemarks;
            card.Controls.Add(txtRemarks);
            y += 30;

            card.Height = y + 10;

            // Wire NUDs → enforce total cap + live update
            nudBN.ValueChanged += (s, e) =>
            {
                int cap = dto.UnfulfilledQty - (int)nudRF.Value;
                if ((int)nudBN.Value > cap) nudBN.Value = Math.Max(0, cap);
                UpdateFulfillCard(state);
            };
            nudRF.ValueChanged += (s, e) =>
            {
                int cap = dto.UnfulfilledQty - (int)nudBN.Value;
                if ((int)nudRF.Value > cap) nudRF.Value = Math.Max(0, cap);
                UpdateFulfillCard(state);
            };

            UpdateFulfillCard(state);
            return card;
        }

        private static void UpdateFulfillCard(FulfillRowState state)
        {
            int toIssue     = state.TotalToIssue;
            int issuedAfter = state.Dto.IssuedFullQty + toIssue;
            int pendingAfter = Math.Max(0, state.Dto.UnfulfilledQty - toIssue);
            string statusAfter = pendingAfter <= 0
                ? "Fulfilled"
                : toIssue > 0 ? "Partially Fulfilled" : "Pending";

            if (state.LblIssued  != null) state.LblIssued.Text  = $"Issued: {issuedAfter}";
            if (state.LblPending != null) state.LblPending.Text = $"Pending: {pendingAfter}";
            if (state.LblStatus  != null)
            {
                state.LblStatus.Text      = $"Status: {statusAfter}";
                state.LblStatus.ForeColor = statusAfter == "Fulfilled"
                    ? Color.FromArgb(21, 128, 61)
                    : statusAfter == "Partially Fulfilled"
                        ? Color.FromArgb(180, 100, 0)
                        : Color.FromArgb(185, 28, 28);
            }
        }

        private void ShowRemarks(string remarks, string fulfilledRemarks, string status)
        {
            if (_lblRemarks == null) return;

            string text = "";
            if (!string.IsNullOrWhiteSpace(remarks))
                text = $"Remarks: {remarks}";
            if (status == "Fulfilled" && !string.IsNullOrWhiteSpace(fulfilledRemarks))
            {
                if (!string.IsNullOrWhiteSpace(text)) text += "  |  ";
                text += $"Fulfilled Remarks: {fulfilledRemarks}";
            }

            _lblRemarks.Text      = string.IsNullOrWhiteSpace(text) ? "No remarks" : text;
            _lblRemarks.ForeColor = UiTheme.Colors.TextDark;
            _lblRemarks.Font      = new Font("Segoe UI", 9F);
        }
    }
}
