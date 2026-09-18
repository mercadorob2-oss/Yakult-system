using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.Admin.AccountManagement
{
    public partial class AccountPermissionsPage : UserControl
    {
        // ── Layout ────────────────────────────────────────────────────────────
        private DefaultListPageLayout _layout;
        private DataGridView _dgv;
        private Label _lblCount;
        private System.Windows.Forms.CheckBox _chkShowArchived;
        private ReaLTaiizor.Controls.HopeButton _btnManageTitles;
        private ReaLTaiizor.Controls.HopeButton _btnPositionSummary;
        private System.Windows.Forms.Button _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly string _connectionString;
        private readonly BindingSource _bindingSource = new BindingSource();
        private List<EmployeePermissionRow> _allRows      = new List<EmployeePermissionRow>();
        private List<EmployeePermissionRow> _filteredRows = new List<EmployeePermissionRow>();
        private int _currentPage = 1;
        private const int PageSize = 15;

        // ── Approver titles (loaded from dbo.ApprovalRoleTitle) ───────────────
        private List<ApproverTitleEntry> _approverTitleEntries = new List<ApproverTitleEntry>();
        private sealed class ApproverTitleEntry
        {
            public string PositionTitle { get; set; }
            public string ApprovalRole  { get; set; }
            public int    RolePriority  { get; set; }
        }

        // ── Sort / filter state ───────────────────────────────────────────────
        private string _sortColumnName;
        private bool   _sortAscending = true;
        private readonly Dictionary<string, HashSet<string>> _columnFilters =
            new Dictionary<string, HashSet<string>>();

        // ── Position groups ───────────────────────────────────────────────────
        private static readonly List<(string Key, string Display)> CoordinatorPositions = new List<(string, string)>
        {
            ("COORDINATOR",                "Coordinator"),
            ("ACCOUNT COORDINATOR",        "Account Coordinator"),
            ("ACTING ACCOUNT COORDINATOR", "Acting Account Coordinator"),
            ("ASST. COORDINATOR",          "Asst. Coordinator"),
            ("ASSISTANT COORDINATOR",      "Assistant Coordinator"),
            ("LADY COORDINATOR",           "Lady Coordinator"),
        };

        private static readonly List<(string Key, string Display)> ManagerPositions = new List<(string, string)>
        {
            ("MANAGER",                  "Manager"),
            ("ASST. MANAGER",            "Asst. Manager"),
            ("ASSISTANT MANAGER",        "Assistant Manager"),
            ("JR. ASST. MANAGER",        "Jr. Asst. Manager"),
            ("JUNIOR ASSISTANT MANAGER", "Junior Asst. Manager"),
            ("ACTING JR. ASST. MANAGER", "Acting Jr. Asst. Manager"),
        };

        private static readonly List<(string Key, string Display)> SupervisorPositions = new List<(string, string)>
        {
            ("SUPERVISOR", "Supervisor"),
        };

        private static readonly HashSet<string> HigherUpPositions = new HashSet<string>(
            CoordinatorPositions.Select(p => p.Key)
                .Concat(ManagerPositions.Select(p => p.Key))
                .Concat(SupervisorPositions.Select(p => p.Key)),
            StringComparer.OrdinalIgnoreCase);

        public AccountPermissionsPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUI();
            _ = LoadAsync();
        }

        private void InitializeComponent() { }

        // ── UI Construction ───────────────────────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();

            Dock      = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Approver Management",
                "Search by name, number, position, department, branch...",
                (s, e) => ApplyFilter(),
                () => { _columnFilters.Clear(); _ = LoadAsync(); });

            _layout.SortByComboBox.Visible = false;
            _layout.SortByLabel.Visible    = false;

            _btnManageTitles = new ReaLTaiizor.Controls.HopeButton
            {
                Text   = "Manage Approver Titles",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(210, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnManageTitles, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnManageTitles.Click += (s, e) =>
            {
                using (var dlg = new ManageApproverTitlesDialog(_connectionString))
                {
                    dlg.ShowDialog(this.FindForm());
                    _ = LoadAsync();
                }
            };

            _btnPositionSummary = new ReaLTaiizor.Controls.HopeButton
            {
                Text   = "Position Summary",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(170, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnPositionSummary, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnPositionSummary.Click += (s, e) => ShowPositionSummary();

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnManageTitles, _btnPositionSummary });

            if (_layout.SummaryPanel != null)
            {
                _layout.SummaryPanel.Visible = false;
                _layout.SummaryPanel.Height  = 0;
            }

            // Pagination controls
            _btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            _btnPrevPage  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            _lblCount     = new Label { AutoSize = false, Width = 300, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft, Text = "Loading…" };
            _btnNextPage  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 410, Top = 5 };
            _btnLastPage  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 460, Top = 5 };

            _btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdateDataGridView(); };
            _btnPrevPage.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdateDataGridView(); } };
            _btnNextPage.Click  += (s, e) => { int total = TotalPages(); if (_currentPage < total) { _currentPage++; UpdateDataGridView(); } };
            _btnLastPage.Click  += (s, e) => { _currentPage = TotalPages(); UpdateDataGridView(); };

            _chkShowArchived = new System.Windows.Forms.CheckBox
            {
                Text      = "Show Archived",
                Width     = 150,
                Height    = 28,
                Top       = 6,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Black,
                Checked   = false,
            };
            _chkShowArchived.CheckedChanged += (s, e) => ApplyFilter();

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.AddRange(new Control[] { _btnFirstPage, _btnPrevPage, _lblCount, _btnNextPage, _btnLastPage });
                _layout.PaginationPanel.Controls.Add(_chkShowArchived);
                _layout.PaginationPanel.Resize += (s, e) =>
                    _chkShowArchived.Left = _layout.PaginationPanel.Width - _chkShowArchived.Width - 8;
            }

            // Grid
            _dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = true,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                RowHeadersVisible     = false,
            };
            UiFactory.StyleGrid(_dgv);
            _dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgv.AutoSizeRowsMode    = DataGridViewAutoSizeRowsMode.AllCells;
            _dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // All columns set to Programmatic so header click / sort glyph / filter icon work
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeNumber", HeaderText = "Employee #",     DataPropertyName = "EmployeeNumber", FillWeight = 10, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title",          HeaderText = "Title",          DataPropertyName = "Title",          FillWeight = 7,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName",   HeaderText = "Employee Name",  DataPropertyName = "EmployeeName",   FillWeight = 20, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position",       HeaderText = "Position",       DataPropertyName = "Position",       FillWeight = 18, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Department",     HeaderText = "Department",     DataPropertyName = "Department",     FillWeight = 16, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Company",        HeaderText = "Company",        DataPropertyName = "Company",        FillWeight = 14, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Branch",         HeaderText = "Branch",         DataPropertyName = "Branch",         FillWeight = 14, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsApprover",     HeaderText = "Is Approver",    DataPropertyName = "IsApprover",     FillWeight = 9,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "SystemRoles",    HeaderText = "System Role(s)", DataPropertyName = "SystemRoles",    FillWeight = 14, SortMode = DataGridViewColumnSortMode.Programmatic });

            _dgv.DataSource = _bindingSource;

            // Wire header click + custom cell painting
            _dgv.ColumnHeaderMouseClick += DgvColumnHeaderMouseClick;
            _dgv.CellPainting           += DgvCellPainting;

            if (_layout.GridCard != null)
                _layout.GridCard.Controls.Add(_dgv);

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(false);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _dgv);
        }

        // ── Data ─────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task<List<ApproverTitleEntry>> LoadApproverTitlesFromDbAsync(SqlConnection con)
        {
            var results = new List<ApproverTitleEntry>();
            try
            {
                using (var cmd = new SqlCommand(
                    "SELECT PositionTitle, ApprovalRole, RolePriority FROM dbo.ApprovalRoleTitle WHERE IsActive = 1 ORDER BY RolePriority DESC, PositionTitle", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        results.Add(new ApproverTitleEntry
                        {
                            PositionTitle = r.GetString(0),
                            ApprovalRole  = r.GetString(1),
                            RolePriority  = r.GetInt32(2)
                        });
            }
            catch { /* table may not exist yet — caller falls back to hardcoded */ }
            return results;
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionString)) return;

            try
            {
                if (_layout?.RefreshButton != null)
                    _layout.RefreshButton.Enabled = false;

                _lblCount.Text = "Loading…";
                _bindingSource.DataSource = null;

                var results = new List<EmployeePermissionRow>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    // Load approver titles from DB; fall back to hardcoded if empty
                    _approverTitleEntries = await LoadApproverTitlesFromDbAsync(con);

                    IEnumerable<string> positionKeys = _approverTitleEntries.Count > 0
                        ? _approverTitleEntries.Select(t => t.PositionTitle)
                        : (IEnumerable<string>)HigherUpPositions;

                    var posArray  = positionKeys.ToArray();
                    var posParams = posArray.Select((p, i) => $"@p{i}").ToArray();
                    string inClause = string.Join(", ", posParams);

                    string sql = $@"
                        SELECT
                            e.EmployeeNumber,
                            t.Code,
                            e.Name         AS EmployeeName,
                            e.Position,
                            d.Name         AS Department,
                            c.Name         AS Company,
                            b.Name         AS Branch,
                            CASE WHEN UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause}) THEN 'Yes' ELSE 'No' END AS IsApprover,
                            ISNULL(
                                STUFF((
                                    SELECT ', ' + r.RoleName
                                    FROM dbo.[User] u2
                                    INNER JOIN dbo.UserRole ur ON ur.UserId = u2.UserId
                                    INNER JOIN dbo.Role r      ON r.RoleId  = ur.RoleId AND r.IsActive = 1
                                    WHERE u2.EmpId = e.EmpId
                                    FOR XML PATH(''), TYPE
                                ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
                            'No Account') AS SystemRoles,
                            CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Title        t   ON e.TitleId  = t.TitleId
                        LEFT JOIN dbo.Department   d   ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.Company      c   ON e.ComId    = c.ComId
                        LEFT JOIN dbo.Branch       b   ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause})
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        for (int i = 0; i < posArray.Length; i++)
                            cmd.Parameters.AddWithValue($"@p{i}", posArray[i].ToUpperInvariant());

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(new EmployeePermissionRow
                                {
                                    EmployeeNumber = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Title          = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    EmployeeName   = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Position       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Department     = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Company        = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    Branch         = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    IsApprover     = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    SystemRoles    = reader.IsDBNull(8) ? "No Account" : reader.GetString(8),
                                    IsArchived     = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                                });
                            }
                        }
                    }
                }

                _allRows = results;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employees.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblCount.Text = "Failed to load.";
            }
            finally
            {
                if (_layout?.RefreshButton != null)
                    _layout.RefreshButton.Enabled = true;
            }
        }

        private void ApplyFilter()
        {
            string q = (_layout?.SearchBox?.Text ?? "").Trim();

            bool showArchived = _chkShowArchived?.Checked == true;
            IEnumerable<EmployeePermissionRow> filtered = showArchived
                ? _allRows
                : _allRows.Where(r => !r.IsArchived);

            // Search box
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(x =>
                    x.EmployeeNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.EmployeeName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Position.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Department.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Branch.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Column filters
            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                switch (kvp.Key)
                {
                    case "EmployeeNumber": filtered = filtered.Where(r => selected.Contains(r.EmployeeNumber, StringComparer.OrdinalIgnoreCase)); break;
                    case "Title":          filtered = filtered.Where(r => selected.Contains(r.Title,          StringComparer.OrdinalIgnoreCase)); break;
                    case "EmployeeName":   filtered = filtered.Where(r => selected.Contains(r.EmployeeName,   StringComparer.OrdinalIgnoreCase)); break;
                    case "Position":       filtered = filtered.Where(r => selected.Contains(r.Position,       StringComparer.OrdinalIgnoreCase)); break;
                    case "Department":     filtered = filtered.Where(r => selected.Contains(r.Department,     StringComparer.OrdinalIgnoreCase)); break;
                    case "Company":        filtered = filtered.Where(r => selected.Contains(r.Company,        StringComparer.OrdinalIgnoreCase)); break;
                    case "Branch":         filtered = filtered.Where(r => selected.Contains(r.Branch,         StringComparer.OrdinalIgnoreCase)); break;
                    case "IsApprover":     filtered = filtered.Where(r => selected.Contains(r.IsApprover,     StringComparer.OrdinalIgnoreCase)); break;
                    case "SystemRoles":    filtered = filtered.Where(r => selected.Contains(r.SystemRoles,    StringComparer.OrdinalIgnoreCase)); break;
                }
            }

            _filteredRows = filtered.ToList();
            ApplySort();

            _currentPage = 1;
            UpdateDataGridView();

            UpdateSummaryCards();
            DefaultListPageTemplate.DisableDefaultRowHighlight(_dgv);
        }

        // ── Sort ─────────────────────────────────────────────────────────────

        // Produces a sort key so "141X" (3-digit manager code) sorts beside "141"
        // rather than after all 4-digit numbers: pads the leading digit run to 10 chars.
        private static string EmpNumSortKey(string empNum)
        {
            if (string.IsNullOrEmpty(empNum)) return "";
            int i = 0;
            while (i < empNum.Length && char.IsDigit(empNum[i])) i++;
            return empNum.Substring(0, i).PadLeft(10, '0') + empNum.Substring(i).ToUpperInvariant();
        }

        private void ApplySort()
        {
            if (_sortColumnName == null) return;

            Func<EmployeePermissionRow, string> key;
            switch (_sortColumnName)
            {
                case "EmployeeNumber": key = r => EmpNumSortKey(r.EmployeeNumber); break;
                case "Title":          key = r => r.Title;          break;
                case "EmployeeName":   key = r => r.EmployeeName;   break;
                case "Position":       key = r => r.Position;       break;
                case "Department":     key = r => r.Department;     break;
                case "Company":        key = r => r.Company;        break;
                case "Branch":         key = r => r.Branch;         break;
                case "IsApprover":     key = r => r.IsApprover;     break;
                case "SystemRoles":    key = r => r.SystemRoles;    break;
                default: return;
            }

            _filteredRows = _sortAscending
                ? _filteredRows.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                : _filteredRows.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ── Pagination ────────────────────────────────────────────────────────
        private void UpdateDataGridView()
        {
            if (_filteredRows == null)
            {
                _bindingSource.DataSource = null;
                return;
            }

            var paged = _filteredRows
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _bindingSource.DataSource = paged;
            _dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _dgv.Invalidate();

            int total = TotalPages();
            if (_lblCount != null)
                _lblCount.Text = total == 0
                    ? "Page 0 of 0 (0 rows)"
                    : $"Page {_currentPage} of {total} ({_filteredRows.Count} rows)";

            if (_btnFirstPage != null) _btnFirstPage.Enabled = _currentPage > 1;
            if (_btnPrevPage  != null) _btnPrevPage.Enabled  = _currentPage > 1;
            if (_btnNextPage  != null) _btnNextPage.Enabled  = _currentPage < total;
            if (_btnLastPage  != null) _btnLastPage.Enabled  = _currentPage < total;
        }

        private int TotalPages()
        {
            int count = _filteredRows?.Count ?? 0;
            return count == 0 ? 1 : (int)Math.Ceiling(count / (double)PageSize);
        }

        // ── Column header click ───────────────────────────────────────────────
        private void DgvColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            // Rightmost 18px = filter ▼ zone
            if (e.X >= col.Width - 18)
            {
                ShowColumnFilterPopup(col);
                return;
            }

            // Toggle sort
            if (_sortColumnName == col.Name)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumnName = col.Name;
                _sortAscending  = true;
            }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAscending ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;

            ApplyFilter();
        }

        private void ShowColumnFilterPopup(DataGridViewColumn col)
        {
            Func<EmployeePermissionRow, string> getter;
            switch (col.Name)
            {
                case "EmployeeNumber": getter = r => r.EmployeeNumber; break;
                case "Title":          getter = r => r.Title;          break;
                case "EmployeeName":   getter = r => r.EmployeeName;   break;
                case "Position":       getter = r => r.Position;       break;
                case "Department":     getter = r => r.Department;     break;
                case "Company":        getter = r => r.Company;        break;
                case "Branch":         getter = r => r.Branch;         break;
                case "IsApprover":     getter = r => r.IsApprover;     break;
                case "SystemRoles":    getter = r => r.SystemRoles;    break;
                default: return;
            }

            var distinctValues = _allRows
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _columnFilters.TryGetValue(col.Name, out var currentFilter);

            var colRect  = _dgv.GetColumnDisplayRectangle(col.Index, false);
            var screenPt = _dgv.PointToScreen(new Point(colRect.Left, _dgv.ColumnHeadersHeight));

            using (var popup = new ColumnFilterPopup(col.HeaderText, distinctValues, currentFilter))
            {
                popup.Location = screenPt;

                var screen = Screen.FromPoint(screenPt).WorkingArea;
                if (popup.Right  > screen.Right)  popup.Left = Math.Max(screen.Left, screen.Right - popup.Width);
                if (popup.Bottom > screen.Bottom) popup.Top  = Math.Max(screen.Top, screenPt.Y - popup.Height - _dgv.ColumnHeadersHeight);

                popup.ShowDialog(this.FindForm());

                if (popup.Action == ColumnFilterPopup.PopupAction.SortAscending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending  = true;
                    foreach (DataGridViewColumn c in _dgv.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    ApplyFilter();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.SortDescending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending  = false;
                    foreach (DataGridViewColumn c in _dgv.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                    ApplyFilter();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.Filter)
                {
                    if (popup.SelectedValues == null || popup.SelectedValues.Count == 0
                        || popup.SelectedValues.Count >= distinctValues.Count)
                        _columnFilters.Remove(col.Name);
                    else
                        _columnFilters[col.Name] = popup.SelectedValues;

                    ApplyFilter();
                }
            }
        }

        // ── Custom header painting (sort glyphs + filter icon) ────────────────
        private void DgvCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Gray out archived data rows
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var row = _dgv.Rows[e.RowIndex];
                if (row.DataBoundItem is EmployeePermissionRow emp && emp.IsArchived)
                {
                    e.Handled = true;

                    var backColor = Color.FromArgb(235, 235, 235);
                    var foreColor = Color.FromArgb(160, 160, 160);

                    using (var backBrush = new SolidBrush(backColor))
                        e.Graphics.FillRectangle(backBrush, e.CellBounds);

                    if (e.Value != null)
                    {
                        using (var foreBrush = new SolidBrush(foreColor))
                        using (var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                        {
                            var textRect = new Rectangle(
                                e.CellBounds.X + 4, e.CellBounds.Y,
                                e.CellBounds.Width - 8, e.CellBounds.Height);
                            e.Graphics.DrawString(e.Value.ToString(), e.CellStyle.Font ?? _dgv.Font, foreBrush, textRect, fmt);
                        }
                    }

                    // Bottom border
                    using (var borderPen = new Pen(Color.FromArgb(220, 220, 220)))
                        e.Graphics.DrawLine(borderPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }
                return;
            }

            if (e.ColumnIndex < 0) return;

            var column = _dgv.Columns[e.ColumnIndex];
            if (column == null) return;

            e.Handled = true;

            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            var headerRect = new Rectangle(
                e.CellBounds.X, e.CellBounds.Y,
                e.CellBounds.Width - 1, e.CellBounds.Height - 1);

            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, headerRect);

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var  textRect   = e.CellBounds;
                bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;

                if (isSortable)
                {
                    textRect.Width -= 34;

                    // Sort arrow
                    int glyphX = e.CellBounds.Right - 32;
                    int glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

                    Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                        ? Color.White
                        : Color.FromArgb(160, 255, 255, 255);

                    using (var arrowPen = new Pen(arrowColor, 2))
                    {
                        if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,      glyphY + 6, glyphX + 5, glyphY);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5,  glyphY,     glyphX + 10, glyphY + 6);
                        }
                        else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,      glyphY,     glyphX + 5,  glyphY + 6);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5,  glyphY + 6, glyphX + 10, glyphY);
                        }
                        else
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                        }
                    }

                    // Filter ▼ icon
                    bool hasActiveFilter = _columnFilters.ContainsKey(column.Name);
                    int  filterX    = e.CellBounds.Right - 14;
                    int  filterMidY = e.CellBounds.Y + e.CellBounds.Height / 2;
                    var  filterPts  = new PointF[]
                    {
                        new PointF(filterX,     filterMidY - 4),
                        new PointF(filterX + 9, filterMidY - 4),
                        new PointF(filterX + 4, filterMidY + 3)
                    };
                    using (var filterBrush = new SolidBrush(hasActiveFilter
                        ? Color.FromArgb(255, 230, 80)
                        : Color.FromArgb(140, 255, 255, 255)))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillPolygon(filterBrush, filterPts);
                        e.Graphics.SmoothingMode = SmoothingMode.Default;
                    }
                }

                e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgv.Font, textBrush, textRect, fmt);
            }
        }

        // ── Summary (cards replaced by Position Summary dialog) ──────────────
        private void UpdateSummaryCards() { /* cards removed — use Position Summary button */ }

        private void ShowPositionSummary()
        {
            var counts = _allRows
                .GroupBy(r => r.Position.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var roleOrder = new[] { "Coordinator", "Manager", "Supervisor" };
            var summaryRows = new List<(string Position, string Role, int Count)>();

            if (_approverTitleEntries.Count > 0)
            {
                foreach (string role in roleOrder)
                {
                    var entries = _approverTitleEntries
                        .Where(t => t.ApprovalRole.Equals(role, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(t => t.RolePriority)
                        .ThenBy(t => t.PositionTitle);
                    foreach (var entry in entries)
                    {
                        int count = counts.TryGetValue(entry.PositionTitle, out int c) ? c : 0;
                        summaryRows.Add((entry.PositionTitle, role, count));
                    }
                }
            }
            else
            {
                foreach (var (key, display) in CoordinatorPositions)
                    summaryRows.Add((display, "Coordinator", counts.TryGetValue(key, out int c1) ? c1 : 0));
                foreach (var (key, display) in ManagerPositions)
                    summaryRows.Add((display, "Manager", counts.TryGetValue(key, out int c2) ? c2 : 0));
                foreach (var (key, display) in SupervisorPositions)
                    summaryRows.Add((display, "Supervisor", counts.TryGetValue(key, out int c3) ? c3 : 0));
            }

            using (var dlg = new PositionSummaryDialog(summaryRows))
                dlg.ShowDialog(this.FindForm());
        }

        // ── DTO ───────────────────────────────────────────────────────────────
        private sealed class EmployeePermissionRow
        {
            public string EmployeeNumber { get; set; }
            public string Title          { get; set; }
            public string EmployeeName   { get; set; }
            public string Position       { get; set; }
            public string Department     { get; set; }
            public string Company        { get; set; }
            public string Branch         { get; set; }
            public string IsApprover     { get; set; }
            public string SystemRoles    { get; set; }
            public bool   IsArchived     { get; set; }
        }
    }

    // ── Position Summary dialog ───────────────────────────────────────────────
    internal class PositionSummaryDialog : Form
    {
        private static readonly string[] RoleOrder = { "Coordinator", "Manager", "Supervisor" };

        private static readonly Dictionary<string, string> RoleLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coordinator", "Coordinators" },
            { "Manager",     "Managers"     },
            { "Supervisor",  "Supervisors"  },
        };

        private static readonly Dictionary<string, Color> AccentColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coordinator", Color.FromArgb(41,  128, 185) },
            { "Manager",     Color.FromArgb(142,  68, 173) },
            { "Supervisor",  Color.FromArgb(39,  174,  96) },
        };

        private static readonly Dictionary<string, Color> RowTints = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coordinator", Color.FromArgb(235, 244, 255) },
            { "Manager",     Color.FromArgb(245, 235, 255) },
            { "Supervisor",  Color.FromArgb(235, 255, 242) },
        };

        private static readonly Dictionary<string, Color> TotalTints = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Coordinator", Color.FromArgb(200, 225, 248) },
            { "Manager",     Color.FromArgb(225, 205, 245) },
            { "Supervisor",  Color.FromArgb(200, 245, 215) },
        };

        public PositionSummaryDialog(List<(string Position, string Role, int Count)> rows)
        {
            const int formWidth = 620;
            const int rowH      = 30;
            const int headerH   = 36;
            const int lblH      = 28;
            const int gap       = 16;
            const int padding   = 16;

            Text            = "Position Summary";
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 9.5F);

            var grouped = rows
                .GroupBy(r => r.Role, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Calculate total content height to size the form
            int contentH = 0;
            foreach (string role in RoleOrder)
            {
                if (!grouped.TryGetValue(role, out var grp) || grp.Count == 0) continue;
                contentH += lblH + 4 + headerH + (grp.Count + 1) * rowH + gap;
            }

            int availableH = Screen.PrimaryScreen.WorkingArea.Height - 120;
            int formHeight = Math.Min(availableH, contentH + padding * 2 + 52);

            Size = new Size(formWidth, formHeight);

            // Scroll panel fills all space above the Close button
            int scrollW = ClientSize.Width - padding * 2;
            var scrollPanel = new Panel
            {
                Location   = new Point(padding, padding),
                Size       = new Size(scrollW, ClientSize.Height - padding * 2 - 46),
                AutoScroll = true,
                BackColor  = Color.White,
            };
            Controls.Add(scrollPanel);

            // Inner width accounts for possible scrollbar
            int innerW = scrollW - SystemInformation.VerticalScrollBarWidth - 2;
            int y = 0;

            foreach (string role in RoleOrder)
            {
                if (!grouped.TryGetValue(role, out var grp) || grp.Count == 0) continue;

                AccentColors.TryGetValue(role, out Color accent);
                RoleLabels.TryGetValue(role, out string labelText);
                RowTints.TryGetValue(role, out Color rowTint);
                TotalTints.TryGetValue(role, out Color totalTint);

                var lbl = new Label
                {
                    Text      = labelText ?? role,
                    Location  = new Point(0, y),
                    AutoSize  = false,
                    Width     = innerW,
                    Height    = lblH,
                    Font      = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor = accent,
                };
                scrollPanel.Controls.Add(lbl);
                y += lblH + 4;

                int dgvH = headerH + (grp.Count + 1) * rowH + 2;

                var dgv = new DataGridView
                {
                    Location                    = new Point(0, y),
                    Size                        = new Size(innerW, dgvH),
                    AutoGenerateColumns         = false,
                    AllowUserToAddRows          = false,
                    AllowUserToDeleteRows       = false,
                    ReadOnly                    = true,
                    SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect                 = false,
                    RowHeadersVisible           = false,
                    BackgroundColor             = Color.White,
                    BorderStyle                 = BorderStyle.FixedSingle,
                    GridColor                   = Color.FromArgb(220, 220, 220),
                    ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                    ColumnHeadersHeight         = headerH,
                    RowTemplate                 = { Height = rowH },
                    ScrollBars                  = ScrollBars.None,
                };
                dgv.ColumnHeadersDefaultCellStyle.BackColor = accent;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                dgv.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                dgv.EnableHeadersVisualStyles                = false;
                dgv.DefaultCellStyle.SelectionBackColor     = accent;
                dgv.DefaultCellStyle.SelectionForeColor     = Color.White;
                dgv.DefaultCellStyle.Font                   = new Font("Segoe UI", 9.5F);

                var colPosition = new DataGridViewTextBoxColumn
                {
                    Name         = "colPosition",
                    HeaderText   = "Position",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                };
                colPosition.HeaderCell.Style.BackColor = accent;
                colPosition.HeaderCell.Style.ForeColor = Color.White;
                colPosition.HeaderCell.Style.Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold);

                var colCount = new DataGridViewTextBoxColumn
                {
                    Name             = "colCount",
                    HeaderText       = "Count",
                    Width            = 80,
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
                };
                colCount.HeaderCell.Style.BackColor = accent;
                colCount.HeaderCell.Style.ForeColor = Color.White;
                colCount.HeaderCell.Style.Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                colCount.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                dgv.Columns.Add(colPosition);
                dgv.Columns.Add(colCount);

                int groupTotal = 0;
                foreach (var (pos, _, count) in grp)
                {
                    int idx = dgv.Rows.Add(pos, count);
                    dgv.Rows[idx].DefaultCellStyle.BackColor = rowTint;
                    groupTotal += count;
                }

                int totalIdx = dgv.Rows.Add("TOTAL", groupTotal);
                var totalRow = dgv.Rows[totalIdx];
                totalRow.DefaultCellStyle.Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                totalRow.DefaultCellStyle.BackColor = totalTint;
                totalRow.Cells["colCount"].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                scrollPanel.Controls.Add(dgv);
                y += dgvH + gap;
            }

            var btnClose = new System.Windows.Forms.Button
            {
                Text         = "Close",
                Size         = new Size(90, 32),
                DialogResult = DialogResult.OK,
            };
            btnClose.Location = new Point(ClientSize.Width - btnClose.Width - padding, ClientSize.Height - btnClose.Height - 10);
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;
        }
    }

    // ── Manage Approver Titles dialog ─────────────────────────────────────────
    internal class ManageApproverTitlesDialog : Form
    {
        private readonly string _connectionString;

        private DataGridView _dgv;
        private TextBox      _txtPositionTitle;
        private ComboBox     _cboApprovalRole;
        private NumericUpDown _nudPriority;
        private System.Windows.Forms.Button _btnAdd;
        private System.Windows.Forms.Button _btnCancelEdit;
        private System.Windows.Forms.Button _btnEdit;
        private System.Windows.Forms.Button _btnToggleActive;
        private System.Windows.Forms.Button _btnClose;
        private Label        _lblSection;
        private Label        _lblStatus;
        private int          _editingTitleId = -1;
        private List<TitleRow> _rows;
        private string         _sortColName;
        private bool           _sortAsc = true;

        // Grid DTO
        private sealed class TitleRow
        {
            public int    TitleId       { get; set; }
            public string PositionTitle { get; set; }
            public string ApprovalRole  { get; set; }
            public int    RolePriority  { get; set; }
            public string ActiveDisplay => IsActive ? "Yes" : "No";
            public bool   IsActive      { get; set; }
            public string DateCreated   { get; set; }
        }

        public ManageApproverTitlesDialog(string connectionString)
        {
            _connectionString = connectionString;
            BuildUI();
            LoadTitles();
        }

        private void BuildUI()
        {
            Text            = "Manage Approver Titles";
            Size            = new Size(1080, 820);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 9F);

            // ── Grid ────────────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Location              = new Point(16, 16),
                Size                  = new Size(1032, 480),
                AutoGenerateColumns      = false,
                AllowUserToAddRows       = false,
                AllowUserToDeleteRows    = false,
                AllowUserToResizeColumns = false,
                AllowUserToResizeRows    = false,
                ReadOnly                 = true,
                SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect              = false,
                RowHeadersVisible        = false,
            };
            Helpers.UiFactory.StyleGrid(_dgv);
            _dgv.RowTemplate.Height        = 38;
            _dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId",       HeaderText = "ID",             DataPropertyName = "TitleId",       Width = 55,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle",     HeaderText = "Position Title", DataPropertyName = "PositionTitle",  Width = 510, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRole",      HeaderText = "Approval",       DataPropertyName = "ApprovalRole",   Width = 135, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPriority",  HeaderText = "Priority",       DataPropertyName = "RolePriority",   Width = 85,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colActive",    HeaderText = "Active",         DataPropertyName = "ActiveDisplay",  Width = 80,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCreated",   HeaderText = "Date Added",     DataPropertyName = "DateCreated",    Width = 130, SortMode = DataGridViewColumnSortMode.Programmatic });

            _dgv.ColumnHeaderMouseClick += DgvApproverTitlesHeaderClick;
            _dgv.CellPainting           += DlgApproverTitlesCellPainting;

            _dgv.Columns["colPriority"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["colActive"].DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;

            Controls.Add(_dgv);

            // ── Divider label ────────────────────────────────────────────────
            _lblSection = new Label
            {
                Text      = "Add New Approver Title",
                Location  = new Point(16, 510),
                AutoSize  = false,
                Width     = 1032,
                Height    = 24,
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 80, 160)
            };
            Controls.Add(_lblSection);

            // ── Add form — labels on one row, fields on the next ─────────────
            int labelY = 540;
            int fieldY = 562;

            // Column 1 — Position Title
            Controls.Add(new Label
            {
                Text     = "Position Title:",
                Location = new Point(16, labelY),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F)
            });
            _txtPositionTitle = new TextBox
            {
                Location = new Point(16, fieldY),
                Width    = 280,
                Height   = 26
            };
            Controls.Add(_txtPositionTitle);

            // Column 2 — Role
            Controls.Add(new Label
            {
                Text     = "Role:",
                Location = new Point(316, labelY),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F)
            });
            _cboApprovalRole = new ComboBox
            {
                Location      = new Point(316, fieldY),
                Width         = 145,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboApprovalRole.Items.AddRange(new object[] { "Coordinator", "Manager", "Supervisor" });
            _cboApprovalRole.SelectedIndex = 2; // default to Supervisor
            Controls.Add(_cboApprovalRole);

            // Column 3 — Priority
            Controls.Add(new Label
            {
                Text     = "Priority:",
                Location = new Point(476, labelY),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F)
            });
            _nudPriority = new NumericUpDown
            {
                Location = new Point(476, fieldY),
                Width    = 70,
                Minimum  = 1,
                Maximum  = 10,
                Value    = 5
            };
            Controls.Add(_nudPriority);

            // ── Action buttons row ───────────────────────────────────────────
            int btnY = fieldY + 40;

            _btnAdd = new System.Windows.Forms.Button
            {
                Text      = "Add Title",
                Location  = new Point(16, btnY),
                Width     = 120,
                Height    = 32,
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnAdd.FlatAppearance.BorderSize = 0;
            _btnAdd.Click += BtnAdd_Click;
            Controls.Add(_btnAdd);

            _btnCancelEdit = new System.Windows.Forms.Button
            {
                Text      = "Cancel Edit",
                Location  = new Point(148, btnY),
                Width     = 110,
                Height    = 32,
                Visible   = false,
                FlatStyle = FlatStyle.Flat
            };
            _btnCancelEdit.Click += (s, e) => ExitEditMode();
            Controls.Add(_btnCancelEdit);

            _lblStatus = new Label
            {
                Location  = new Point(270, btnY + 7),
                AutoSize  = false,
                Width     = 480,
                Height    = 20,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(39, 174, 96)
            };
            Controls.Add(_lblStatus);

            // ── Bottom buttons ───────────────────────────────────────────────
            int bottomY = btnY + 52;

            _btnToggleActive = new System.Windows.Forms.Button
            {
                Text      = "Toggle",
                Location  = new Point(16, bottomY),
                Width     = 90,
                Height    = 32,
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnToggleActive.FlatAppearance.BorderSize = 0;
            _btnToggleActive.Click += BtnToggleActive_Click;
            Controls.Add(_btnToggleActive);

            _btnEdit = new System.Windows.Forms.Button
            {
                Text      = "\u270F  Edit",
                Location  = new Point(116, bottomY),
                Width     = 90,
                Height    = 32,
                FlatStyle = FlatStyle.Flat
            };
            _btnEdit.Click += BtnEdit_Click;
            Controls.Add(_btnEdit);

            _btnClose = new System.Windows.Forms.Button
            {
                Text         = "Close",
                Location     = new Point(974, bottomY),
                Width        = 90,
                Height       = 32,
                DialogResult = DialogResult.OK
            };
            Controls.Add(_btnClose);
            AcceptButton = _btnAdd;
            CancelButton = _btnClose;
        }

        private void LoadTitles()
        {
            try
            {
                var rows = new List<TitleRow>();
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TitleId, PositionTitle, ApprovalRole, RolePriority, IsActive,
                               CONVERT(VARCHAR(10), DateCreated, 101) AS DateCreated
                        FROM dbo.ApprovalRoleTitle
                        ORDER BY ApprovalRole, RolePriority DESC, PositionTitle", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new TitleRow
                            {
                                TitleId       = reader.GetInt32(0),
                                PositionTitle = reader.GetString(1),
                                ApprovalRole  = reader.GetString(2),
                                RolePriority  = reader.GetInt32(3),
                                IsActive      = reader.GetBoolean(4),
                                DateCreated   = reader.IsDBNull(5) ? "" : reader.GetString(5)
                            });
                        }
                    }
                }
                _rows = rows;
                ApplySortToRows();
                _dgv.DataSource = null;
                _dgv.DataSource = _rows;
                if (_sortColName != null && _dgv.Columns.Contains(_sortColName))
                    _dgv.Columns[_sortColName].HeaderCell.SortGlyphDirection =
                        _sortAsc ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;
                ColorGridRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load approver titles.\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DlgApproverTitlesCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Only custom-paint the header row
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;

            var column = _dgv.Columns[e.ColumnIndex];
            if (column == null) return;

            e.Handled = true;

            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            var headerRect = new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, headerRect);

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap,
                Trimming      = StringTrimming.EllipsisCharacter,
            })
            {
                var textRect = e.CellBounds;

                if (column.SortMode == DataGridViewColumnSortMode.Programmatic)
                {
                    textRect.Width -= 20;

                    int glyphX = e.CellBounds.Right - 18;
                    int glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

                    Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                        ? Color.White
                        : Color.FromArgb(160, 255, 255, 255);

                    using (var arrowPen = new Pen(arrowColor, 2))
                    {
                        if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,     glyphY + 6, glyphX + 5, glyphY);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY,     glyphX + 10, glyphY + 6);
                        }
                        else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,     glyphY,     glyphX + 5,  glyphY + 6);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                        }
                        else
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                        }
                    }
                }

                e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgv.Font, textBrush, textRect, fmt);
            }
        }

        private void DgvApproverTitlesHeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            if (_sortColName == col.Name)
                _sortAsc = !_sortAsc;
            else
            {
                _sortColName = col.Name;
                _sortAsc     = true;
            }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAsc ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;

            ApplySortToRows();
            _dgv.DataSource = null;
            _dgv.DataSource = _rows;
            ColorGridRows();
        }

        private void ApplySortToRows()
        {
            if (_rows == null || _sortColName == null) return;

            IEnumerable<TitleRow> sorted;
            switch (_sortColName)
            {
                case "colId":       sorted = _sortAsc ? _rows.OrderBy(r => r.TitleId)       : _rows.OrderByDescending(r => r.TitleId);       break;
                case "colTitle":    sorted = _sortAsc ? _rows.OrderBy(r => r.PositionTitle)  : _rows.OrderByDescending(r => r.PositionTitle);  break;
                case "colRole":     sorted = _sortAsc ? _rows.OrderBy(r => r.ApprovalRole)   : _rows.OrderByDescending(r => r.ApprovalRole);   break;
                case "colPriority": sorted = _sortAsc ? _rows.OrderBy(r => r.RolePriority)   : _rows.OrderByDescending(r => r.RolePriority);   break;
                case "colActive":   sorted = _sortAsc ? _rows.OrderBy(r => r.ActiveDisplay)  : _rows.OrderByDescending(r => r.ActiveDisplay);  break;
                case "colCreated":  sorted = _sortAsc ? _rows.OrderBy(r => r.DateCreated)    : _rows.OrderByDescending(r => r.DateCreated);    break;
                default: return;
            }
            _rows = sorted.ToList();
        }

        private void ColorGridRows()
        {
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.DataBoundItem is TitleRow r && !r.IsActive)
                {
                    row.DefaultCellStyle.ForeColor  = Color.FromArgb(160, 160, 160);
                    row.DefaultCellStyle.BackColor  = Color.FromArgb(245, 245, 245);
                }
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string positionTitle = _txtPositionTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(positionTitle))
            {
                _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                _lblStatus.Text = "Position Title is required.";
                _txtPositionTitle.Focus();
                return;
            }

            string approvalRole = _cboApprovalRole.SelectedItem?.ToString();
            int    priority     = (int)_nudPriority.Value;
            bool   isEditMode   = _editingTitleId > -1;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Check for duplicate (exclude current record when editing)
                    using (var chk = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.ApprovalRoleTitle WHERE PositionTitle = @Title AND TitleId != @EditId", con))
                    {
                        chk.Parameters.AddWithValue("@Title", positionTitle);
                        chk.Parameters.AddWithValue("@EditId", isEditMode ? _editingTitleId : -1);
                        int existing = (int)chk.ExecuteScalar();
                        if (existing > 0)
                        {
                            _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                            _lblStatus.Text = $"\"{positionTitle}\" already exists in the list.";
                            return;
                        }
                    }

                    if (isEditMode)
                    {
                        // Update existing record
                        using (var cmd = new SqlCommand(@"
                            UPDATE dbo.ApprovalRoleTitle 
                            SET PositionTitle = @Title, ApprovalRole = @Role, RolePriority = @Priority 
                            WHERE TitleId = @Id", con))
                        {
                            cmd.Parameters.AddWithValue("@Title",    positionTitle);
                            cmd.Parameters.AddWithValue("@Role",     approvalRole);
                            cmd.Parameters.AddWithValue("@Priority", priority);
                            cmd.Parameters.AddWithValue("@Id",       _editingTitleId);
                            cmd.ExecuteNonQuery();
                        }

                        _lblStatus.ForeColor = Color.FromArgb(39, 174, 96);
                        _lblStatus.Text = $"\"{positionTitle}\" updated successfully.";
                        ExitEditMode();
                    }
                    else
                    {
                        // Insert new record
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO dbo.ApprovalRoleTitle (PositionTitle, ApprovalRole, RolePriority, IsActive, CreatedByUserId)
                            VALUES (@Title, @Role, @Priority, 1, @UserId)", con))
                        {
                            cmd.Parameters.AddWithValue("@Title",    positionTitle);
                            cmd.Parameters.AddWithValue("@Role",     approvalRole);
                            cmd.Parameters.AddWithValue("@Priority", priority);
                            cmd.Parameters.AddWithValue("@UserId",
                                Yakult.Inventory.App.Session.AppSession.CurrentUserId > 0
                                    ? (object)Yakult.Inventory.App.Session.AppSession.CurrentUserId
                                    : DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        _lblStatus.ForeColor = Color.FromArgb(39, 174, 96);
                        _lblStatus.Text = $"\"{positionTitle}\" added as a {approvalRole} approver.";
                        _txtPositionTitle.Clear();
                    }
                }

                LoadTitles();
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                _lblStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void BtnToggleActive_Click(object sender, EventArgs e)
        {
            var selected = _dgv.CurrentRow?.DataBoundItem as TitleRow;
            if (selected == null)
            {
                MessageBox.Show("Please select a title to toggle.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool newState = !selected.IsActive;
            string action = newState ? "activate" : "deactivate";

            if (MessageBox.Show(
                    $"Are you sure you want to {action} the approver title:\n\n  \"{selected.PositionTitle}\"?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.ApprovalRoleTitle SET IsActive = @State WHERE TitleId = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@State", newState ? 1 : 0);
                        cmd.Parameters.AddWithValue("@Id",    selected.TitleId);
                        cmd.ExecuteNonQuery();
                    }
                }

                _lblStatus.ForeColor = Color.FromArgb(39, 174, 96);
                _lblStatus.Text = $"\"{selected.PositionTitle}\" {(newState ? "activated" : "deactivated")}.";
                LoadTitles();
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                _lblStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            var selected = _dgv.CurrentRow?.DataBoundItem as TitleRow;
            if (selected == null)
            {
                MessageBox.Show("Please select a title to edit.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Enter edit mode
            _editingTitleId = selected.TitleId;
            _txtPositionTitle.Text = selected.PositionTitle;
            _cboApprovalRole.SelectedItem = selected.ApprovalRole;
            _nudPriority.Value = selected.RolePriority;

            _txtPositionTitle.Enabled = true;
            _cboApprovalRole.Enabled = true;
            _nudPriority.Enabled = true;

            _btnAdd.Text = "Save";
            _btnCancelEdit.Visible = true;
            _btnEdit.Enabled = false;
            _btnToggleActive.Enabled = false;

            _lblStatus.Text = "Editing title. Make changes and click Save to update.";
            _lblStatus.ForeColor = Color.FromArgb(230, 126, 34);
        }

        private void ExitEditMode()
        {
            _editingTitleId = -1;
            _txtPositionTitle.Clear();
            _cboApprovalRole.SelectedIndex = 2; // default to Supervisor
            _nudPriority.Value = 5;

            _txtPositionTitle.Enabled = true;
            _cboApprovalRole.Enabled = true;
            _nudPriority.Enabled = true;

            _btnAdd.Text = "Add Title";
            _btnCancelEdit.Visible = false;
            _btnEdit.Enabled = true;
            _btnToggleActive.Enabled = true;

            _lblStatus.Text = "";
        }
    }
}
