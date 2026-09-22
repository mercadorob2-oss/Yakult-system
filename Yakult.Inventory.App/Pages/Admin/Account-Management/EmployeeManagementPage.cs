using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using ExcelDataReader;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.AccountManagement
{
    public partial class EmployeeManagementPage : UserControl
    {
        // ── Column indices ────────────────────────────────────────────────────
        // 0=EmpId(hidden), 1=EmployeeNumber, 2=Name, 3=Position,
        // 4=Company, 5=Department, 6=Branch, 7=Status,
        // 8=DeptEmail(editable, shared per dept),
        // 9=BranchEmail(editable, shared per branch),
        // 10=PersonalEmail(editable, per employee)
        private const int DeptEmailColIdx     = 9;
        private const int BranchEmailColIdx   = 10;
        private const int PersonalEmailColIdx = 11;

        // ── Layout ────────────────────────────────────────────────────────────
        private DefaultListPageLayout _layout;
        private DataGridView _dgvEmployees;
        private HopeButton _btnAdd;
        private HopeButton _btnEdit;
        private HopeButton _btnArchive;
        private HopeButton _btnExport;
        private HopeButton _btnImport;
        private HopeButton _btnDownloadTemplate;
        private System.Windows.Forms.Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private Label lblPageInfo;
        private Label _lblStatus;
        private System.Windows.Forms.CheckBox _chkShowArchived;
        private System.Windows.Forms.Timer _statusTimer;

        // ── Data ──────────────────────────────────────────────────────────────
        private List<EmployeeManagementDto> _allEmployees;
        private List<EmployeeManagementDto> _filteredEmployees;
        private string _connectionString;
        private int _currentPage = 1;
        private int _pageSize    = 15;

        // ── Sort / filter state ───────────────────────────────────────────────
        private string _sortColumnName;
        private bool   _sortAscending = true;
        private Dictionary<string, HashSet<string>> _columnFilters = new Dictionary<string, HashSet<string>>();

        // ── Email editing state ───────────────────────────────────────────────
        private List<string>            _emailCache  = new List<string>();
        private Dictionary<string, int> _emailIdMap  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private bool   _isSaving;
        private string _editOriginalValue;

        public EmployeeManagementPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUI();
            _ = LoadEmployeesAsync();
        }

        // ── UI Construction ───────────────────────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();

            Dock      = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Employee Management",
                "Search by name, number, position, email...",
                (s, e) => ApplyFilters(),
                () =>
                {
                    _columnFilters.Clear();
                    if (_dgvEmployees != null)
                    {
                        foreach (DataGridViewColumn c in _dgvEmployees.Columns)
                            c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                        _dgvEmployees.Invalidate();
                    }
                    _ = LoadEmployeesAsync();
                });

            _layout.SortByComboBox.Visible = false;
            _layout.SortByLabel.Visible    = false;

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[] { "All", "Active Only", "Inactive Only" });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilters();
            }

            // Toolbar buttons
            _btnAdd = new HopeButton
            {
                Text   = "\u2795  Add Employee",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(200, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnAdd, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnAdd.Click += BtnAdd_Click;

            _btnEdit = new HopeButton
            {
                Text   = "\u270F  Edit",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(130, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnEdit, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnEdit.Click += BtnEdit_Click;

            _btnArchive = new HopeButton
            {
                Text   = "📦  Archive",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(150, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnArchive, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnArchive.Click += BtnArchive_Click;

            _btnExport = new HopeButton
            {
                Text   = "📤  Export",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(140, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnExport, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnExport.Click += BtnExport_Click;

            _btnImport = new HopeButton
            {
                Text   = "📥  Import",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(140, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnImport, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnImport.Click += BtnImport_Click;

            _btnDownloadTemplate = new HopeButton
            {
                Text   = "📋  Template",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(155, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnDownloadTemplate, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnAdd, _btnEdit, _btnArchive, _btnExport, _btnImport, _btnDownloadTemplate });

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;

            // Status bar for inline email save feedback (sits inside the grid card)
            _lblStatus = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 22,
                Font      = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0),
                Visible   = false
            };
            _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _statusTimer.Tick += (s, e) =>
            {
                _statusTimer.Stop();
                _lblStatus.Visible = false;
                _lblStatus.Text    = string.Empty;
            };

            // Grid
            _dgvEmployees = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                RowHeadersVisible     = false,
            };
            UiFactory.StyleGrid(_dgvEmployees);
            _dgvEmployees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvEmployees.AutoSizeRowsMode    = DataGridViewAutoSizeRowsMode.AllCells;
            _dgvEmployees.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Double-click opens edit dialog (skip email column — that stays inline)
            _dgvEmployees.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex != PersonalEmailColIdx)
                    BtnEdit_Click(s, e);
            };

            // Columns
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "EmpId",
                HeaderText       = "ID",
                DataPropertyName = "EmpId",
                Width            = 60,
                Visible          = false,
                ReadOnly         = true
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "EmployeeNumber",
                HeaderText       = "Employee #",
                DataPropertyName = "EmployeeNumber",
                Width            = 110,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Title",
                HeaderText       = "Title",
                DataPropertyName = "TitleCode",
                Width            = 80,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Name",
                HeaderText       = "Name",
                DataPropertyName = "Name",
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Position",
                HeaderText       = "Position",
                DataPropertyName = "Position",
                Width            = 130,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Company",
                HeaderText       = "Company",
                DataPropertyName = "CompanyName",
                Width            = 120,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Department",
                HeaderText       = "Department",
                DataPropertyName = "DepartmentName",
                Width            = 130,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Branch",
                HeaderText       = "Branch",
                DataPropertyName = "BranchName",
                Width            = 120,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Programmatic
            });
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "Status",
                HeaderText       = "Status",
                DataPropertyName = "ActiveDisplay",
                Width            = 80,
                ReadOnly         = true
            });
            // Dept Email (col 8) — editable, shared across the whole department (dbo.DepartmentEmail)
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colDeptEmail",
                HeaderText       = "Dept Email  \u270E",
                DataPropertyName = "DepartmentEmail",
                MinimumWidth     = 160,
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.AllCells,
                ReadOnly         = false,
                SortMode         = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(0, 130, 110)
                }
            });
            // Branch Email (col 9) — editable, shared across the whole branch (dbo.BranchEmail)
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colBranchEmail",
                HeaderText       = "Branch Email  \u270E",
                DataPropertyName = "BranchEmail",
                MinimumWidth     = 160,
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.AllCells,
                ReadOnly         = false,
                SortMode         = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(150, 80, 0)
                }
            });
            // Personal Email (col 10) — editable inline, stored in dbo.EmployeeEmail (IsPrimary=1)
            _dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colPersonalEmail",
                HeaderText       = "Personal Email  \u270E",
                DataPropertyName = "PrimaryEmail",
                MinimumWidth     = 180,
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.AllCells,
                ReadOnly         = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(22, 100, 170)
                }
            });

            // Tooltips
            _dgvEmployees.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == DeptEmailColIdx)
                    e.ToolTipText = "Department email — shared across all employees in this department. Click to edit.";
                else if (e.ColumnIndex == BranchEmailColIdx)
                    e.ToolTipText = "Branch email — shared across all employees in this branch. Click to edit.";
                else if (e.ColumnIndex == PersonalEmailColIdx)
                    e.ToolTipText = "Personal email — overrides the department/branch email. Click to edit.";
            };

            // Column filter / sort events
            _dgvEmployees.CellPainting           += DgvCellPainting;
            _dgvEmployees.ColumnHeaderMouseClick += DgvColumnHeaderMouseClick;

            // Inline email editing events
            _dgvEmployees.CellBeginEdit          += DgvEmployees_CellBeginEdit;
            _dgvEmployees.EditingControlShowing  += DgvEmployees_EditingControlShowing;
            _dgvEmployees.CellEndEdit            += DgvEmployees_CellEndEdit;

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgvEmployees);
                _layout.GridCard.Controls.Add(_lblStatus);
            }

            // Pagination
            btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            btnPrevPage  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            lblPageInfo  = new Label { AutoSize = false, Width = 240, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            btnNextPage  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 350, Top = 5 };
            btnLastPage  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 400, Top = 5 };

            btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdateDataGridView(); };
            btnPrevPage.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdateDataGridView(); } };
            btnNextPage.Click  += (s, e) =>
            {
                int total = (int)Math.Ceiling((_filteredEmployees?.Count ?? 0) / (double)_pageSize);
                if (_currentPage < total) { _currentPage++; UpdateDataGridView(); }
            };
            btnLastPage.Click += (s, e) =>
            {
                int total = (int)Math.Ceiling((_filteredEmployees?.Count ?? 0) / (double)_pageSize);
                _currentPage = total;
                UpdateDataGridView();
            };

            _chkShowArchived = new System.Windows.Forms.CheckBox
            {
                Text      = "Show Archived",
                Width     = 150,
                Height    = 28,
                Top       = 6,
                Left      = 0,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Black,
                Checked   = false,
            };
            _chkShowArchived.CheckedChanged += (s, e) => ApplyFilters();

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[] { btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage });
                _layout.PaginationPanel.Controls.Add(_chkShowArchived);
                _layout.PaginationPanel.Resize += (s, e) =>
                    _chkShowArchived.Left = _layout.PaginationPanel.Width - _chkShowArchived.Width - 8;
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);
            ResumeLayout(false);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _dgvEmployees, _btnAdd, _btnEdit, _btnArchive);
        }

        // ── Data loading ──────────────────────────────────────────────────────
        private async Task LoadEmployeesAsync()
        {
            try
            {
                _allEmployees = new List<EmployeeManagementDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    // Check whether optional DepartmentEmail table exists (requires one-time migration)
                    bool hasDeptEmailTable;
                    using (var chk = new SqlCommand("SELECT CAST(OBJECT_ID('dbo.DepartmentEmail') AS INT)", con))
                    {
                        var res = await chk.ExecuteScalarAsync();
                        hasDeptEmailTable = res != null && res != DBNull.Value;
                    }

                    // Build SQL dynamically based on which tables exist
                    // Branch email is stored in dbo.DepartmentAccount.EmailAddressId (always exists)
                    string deptEmailSelect = hasDeptEmailTable ? "dea.EmailAddress AS DepartmentEmail," : "NULL AS DepartmentEmail,";
                    string deptEmailJoin = hasDeptEmailTable
                        ? @"LEFT JOIN dbo.DepartmentEmail de
                                   ON de.CompanyName    = c.Name AND de.DepartmentName = d.Name
                            LEFT JOIN dbo.EmailAddress dea
                                   ON dea.EmailId = de.EmailAddressId"
                        : "";

                    string sql = $@"
                        SELECT
                            e.EmpId,
                            e.EmployeeNumber,
                            e.Name,
                            e.Position,
                            e.Active,
                            c.Name  AS CompanyName,
                            b.Name  AS BranchName,
                            d.Name  AS DepartmentName,
                            {deptEmailSelect}
                            branch_ea.EmailAddress AS BranchEmail,
                            emp_ea.EmailAddress AS PrimaryEmail,
                            CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived,
                            t.Code  AS TitleCode
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Company    c   ON e.ComId    = c.ComId
                        LEFT JOIN dbo.Branch     b   ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.Department d   ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.Title      t   ON e.TitleId  = t.TitleId
                        LEFT JOIN dbo.EmployeeEmail ee
                               ON ee.EmpId = e.EmpId AND ee.IsPrimary = 1 AND ee.IsActive = 1
                        LEFT JOIN dbo.EmailAddress emp_ea
                               ON emp_ea.EmailId = ee.EmailId AND emp_ea.IsActive = 1
                        {deptEmailJoin}
                        LEFT JOIN dbo.DepartmentAccount da_br
                               ON da_br.CompanyName    = c.Name
                              AND da_br.DepartmentName = d.Name
                              AND da_br.BranchName     = b.Name
                        LEFT JOIN dbo.EmailAddress branch_ea
                               ON branch_ea.EmailId = da_br.EmailAddressId
                        LEFT JOIN dbo.ArchiveStatus arc
                               ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _allEmployees.Add(new EmployeeManagementDto
                            {
                                EmpId           = reader.GetInt32(0),
                                EmployeeNumber  = reader.IsDBNull(1)  ? ""       : reader.GetString(1),
                                Name            = reader.GetString(2),
                                Position        = reader.IsDBNull(3)  ? ""       : reader.GetString(3),
                                Active          = reader.GetBoolean(4),
                                CompanyName     = reader.IsDBNull(5)  ? ""       : reader.GetString(5),
                                BranchName      = reader.IsDBNull(6)  ? ""       : reader.GetString(6),
                                DepartmentName  = reader.IsDBNull(7)  ? ""       : reader.GetString(7),
                                DepartmentEmail = reader.IsDBNull(8)  ? "(None)" : reader.GetString(8),
                                BranchEmail     = reader.IsDBNull(9)  ? "(None)" : reader.GetString(9),
                                PrimaryEmail    = reader.IsDBNull(10) ? "(None)" : reader.GetString(10),
                                IsArchived      = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                                TitleCode       = reader.IsDBNull(12) ? ""       : reader.GetString(12),
                            });
                        }
                    }

                    // Load email cache for autocomplete (reuse open connection)
                    var emails = await GetEmailAddressesAsync(con);
                    _emailCache = emails.Select(ea => ea.EmailAddress).ToList();
                    _emailIdMap = emails.ToDictionary(ea => ea.EmailAddress, ea => ea.EmailId, StringComparer.OrdinalIgnoreCase);
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employees: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static async Task<List<EmailAddressDto>> GetEmailAddressesAsync(SqlConnection con)
        {
            var results = new List<EmailAddressDto>();
            const string sql = @"
                SELECT EmailId, EmailAddress, DisplayName, IsActive
                FROM dbo.EmailAddress
                WHERE IsActive = 1
                ORDER BY EmailAddress";

            using (var cmd = new SqlCommand(sql, con))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    results.Add(new EmailAddressDto
                    {
                        EmailId      = reader.GetInt32(0),
                        EmailAddress = reader.GetString(1),
                        DisplayName  = reader.IsDBNull(2) ? null : reader.GetString(2),
                        IsActive     = reader.GetBoolean(3)
                    });
                }
            }

            return results;
        }

        // ── Filtering / display ───────────────────────────────────────────────
        private void ApplyFilters()
        {
            if (_allEmployees == null)
            {
                _filteredEmployees = new List<EmployeeManagementDto>();
                UpdateDataGridView();
                return;
            }

            var searchText   = _layout.SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
            var filterBy     = _layout.FilterByComboBox?.SelectedItem?.ToString() ?? "All";
            bool showArchived = _chkShowArchived?.Checked == true;

            _filteredEmployees = _allEmployees.Where(emp =>
            {
                if (!showArchived && emp.IsArchived) return false;
                if (!string.IsNullOrEmpty(searchText))
                {
                    // Split into tokens so e.g. "rodel cabantac" matches "Rodel A. Cabantac"
                    var tokens = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool matches = tokens.All(token =>
                        (emp.Name            ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.EmployeeNumber  ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.TitleCode       ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.Position        ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.CompanyName     ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.DepartmentName  ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.BranchName      ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.DepartmentEmail ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.BranchEmail     ?? "").ToLowerInvariant().Contains(token) ||
                        (emp.PrimaryEmail    ?? "").ToLowerInvariant().Contains(token)
                    );

                    if (!matches) return false;
                }

                if (filterBy == "Active Only")  return emp.Active;
                if (filterBy == "Inactive Only") return !emp.Active;
                return true;
            }).ToList();

            // Apply per-column value filters (Excel-style)
            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                switch (kvp.Key)
                {
                    case "Title":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.TitleCode      ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Name":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.Name           ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Position":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.Position       ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Company":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.CompanyName    ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Department":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.DepartmentName ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Branch":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.BranchName     ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "colDeptEmail":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.DepartmentEmail ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "colBranchEmail":
                        _filteredEmployees = _filteredEmployees.Where(r => selected.Contains(r.BranchEmail ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                }
            }

            ApplySortToFiltered();
            _currentPage = 1;
            UpdateDataGridView();
        }

        private void UpdateDataGridView()
        {
            if (_filteredEmployees == null)
            {
                _dgvEmployees.DataSource = null;
                return;
            }

            var paged = _filteredEmployees
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            _dgvEmployees.DataSource = paged;

            int totalPages = (int)Math.Ceiling(_filteredEmployees.Count / (double)_pageSize);

            if (lblPageInfo != null)
                lblPageInfo.Text = totalPages == 0
                    ? "Page 0 of 0 (0 employees)"
                    : $"Page {_currentPage} of {totalPages} ({_filteredEmployees.Count} employees)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage  != null) btnPrevPage.Enabled  = _currentPage > 1;
            if (btnNextPage  != null) btnNextPage.Enabled  = _currentPage < totalPages;
            if (btnLastPage  != null) btnLastPage.Enabled  = _currentPage < totalPages;

            DefaultListPageTemplate.DisableDefaultRowHighlight(_dgvEmployees, _btnAdd, _btnEdit, _btnArchive);
        }

        // ── Toolbar button handlers ───────────────────────────────────────────
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var owner = this.FindForm();
            Cursor.Current = Cursors.WaitCursor;
            EmployeeDialog dialog;
            try
            {
                dialog = new EmployeeDialog();
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                MessageBox.Show($"Failed to open Add Employee dialog: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Cursor.Current = Cursors.Default;

            using (dialog)
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var emp = dialog.ResultEmployee;
                    if (SaveNewEmployee(emp))
                        _ = LoadEmployeesAsync();
                }
            }
        }

        private bool SaveNewEmployee(Yakult.Inventory.App.Pages.EmployeeDto emp)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Employee (Name, Position, Description, DateCreated, Createdby, ComId, DeptId, BranchId, EmployeeNumber, TitleId)
                        VALUES (@Name, @Position, @Description, @DateCreated, @CreatedBy, @ComId, @DeptId, @BranchId, @EmployeeNumber, @TitleId)", con))
                    {
                        cmd.Parameters.AddWithValue("@Name",           emp.Name);
                        cmd.Parameters.AddWithValue("@Position",       (object)emp.Position       ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description",    (object)emp.Description    ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateCreated",    emp.DateCreated);
                        cmd.Parameters.AddWithValue("@CreatedBy",      emp.CreatedByUserId);
                        cmd.Parameters.AddWithValue("@ComId",          emp.CompanyId);
                        cmd.Parameters.AddWithValue("@DeptId",         emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId",       emp.BranchId);
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber);
                        cmd.Parameters.AddWithValue("@TitleId",        emp.TitleId.HasValue ? (object)emp.TitleId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save employee: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (_dgvEmployees.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an employee to edit.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int empId = Convert.ToInt32(_dgvEmployees.SelectedRows[0].Cells["EmpId"].Value);
            var emp   = LoadEmployeeForEdit(empId);
            if (emp == null) return;

            using (var dialog = new EmployeeDialog(emp))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SaveEmployeeUpdate(dialog.ResultEmployee);
                    _ = LoadEmployeesAsync();
                }
            }
        }

        private EmployeeDto LoadEmployeeForEdit(int empId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT e.EmpId, e.Name, e.Position, e.Description, e.DateCreated,
                               e.ComId, e.DeptId, e.BranchId, u.Name AS CreatedByName,
                               e.EmployeeNumber, e.TitleId,
                               CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived
                        FROM dbo.Employee e
                        LEFT JOIN dbo.[User] u ON e.Createdby = u.UserId
                        LEFT JOIN dbo.ArchiveStatus arc
                               ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE e.EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new EmployeeDto
                                {
                                    EmpId          = reader.GetInt32(0),
                                    Name           = reader.GetString(1),
                                    Position       = reader.IsDBNull(2)  ? null      : reader.GetString(2),
                                    Description    = reader.IsDBNull(3)  ? null      : reader.GetString(3),
                                    DateCreated    = reader.GetDateTime(4),
                                    CompanyId      = reader.GetInt32(5),
                                    DepartmentId   = reader.IsDBNull(6)  ? (int?)null : reader.GetInt32(6),
                                    BranchId       = reader.GetInt32(7),
                                    CreatedByName  = reader.IsDBNull(8)  ? "N/A"     : reader.GetString(8),
                                    EmployeeNumber = reader.IsDBNull(9)  ? null      : reader.GetString(9),
                                    TitleId        = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                                    IsArchived     = !reader.IsDBNull(11) && reader.GetInt32(11) == 1
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employee: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
        }

        private void SaveEmployeeUpdate(EmployeeDto emp)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.Employee
                        SET Name           = @Name,
                            Position       = @Position,
                            Description    = @Description,
                            ComId          = @ComId,
                            DeptId         = @DeptId,
                            BranchId       = @BranchId,
                            EmployeeNumber = @EmployeeNumber,
                            TitleId        = @TitleId
                        WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId",          emp.EmpId);
                        cmd.Parameters.AddWithValue("@Name",           emp.Name);
                        cmd.Parameters.AddWithValue("@Position",       (object)emp.Position    ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description",    (object)emp.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ComId",          emp.CompanyId);
                        cmd.Parameters.AddWithValue("@DeptId",         emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId",       emp.BranchId);
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber);
                        cmd.Parameters.AddWithValue("@TitleId",        emp.TitleId.HasValue ? (object)emp.TitleId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save employee: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnArchive_Click(object sender, EventArgs e)
        {
            if (_dgvEmployees.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an employee to archive.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int empId    = Convert.ToInt32(_dgvEmployees.SelectedRows[0].Cells["EmpId"].Value);
            var employee = _allEmployees.FirstOrDefault(emp => emp.EmpId == empId);
            if (employee == null) return;

            string message =
                $"Are you sure you want to archive this employee?\n\n" +
                $"Name: {employee.Name}\n" +
                $"Position: {employee.Position ?? "N/A"}\n" +
                $"Company: {employee.CompanyName}\n\n" +
                $"The employee will be moved to the archive.";

            if (MessageBox.Show(message, "Confirm Archive", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Employee', @EmpId, 1, GETDATE(), @ArchivedBy, 'Archived from Employee Management page')",
                                con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EmpId",      employee.EmpId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show("Employee archived successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _ = LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to archive employee: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Column header: sort + filter ─────────────────────────────────────
        private void DgvColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgvEmployees.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            // Click in the filter ▼ zone (rightmost 18 px) → show filter popup
            if (e.X >= col.Width - 18)
            {
                ShowColumnFilterPopup(col);
                return;
            }

            // Click elsewhere → toggle sort
            if (_sortColumnName == col.Name)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumnName = col.Name;
                _sortAscending  = true;
            }

            foreach (DataGridViewColumn c in _dgvEmployees.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAscending ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;

            _currentPage = 1;
            ApplySortToFiltered();
            UpdateDataGridView();
        }

        private void ShowColumnFilterPopup(DataGridViewColumn col)
        {
            if (_allEmployees == null) return;

            Func<EmployeeManagementDto, string> getter;
            switch (col.Name)
            {
                case "EmployeeNumber": getter = r => r.EmployeeNumber  ?? ""; break;
                case "Title":      getter = r => r.TitleCode      ?? ""; break;
                case "Name":       getter = r => r.Name           ?? ""; break;
                case "Position":   getter = r => r.Position       ?? ""; break;
                case "Company":    getter = r => r.CompanyName    ?? ""; break;
                case "Department": getter = r => r.DepartmentName ?? ""; break;
                case "Branch":     getter = r => r.BranchName     ?? ""; break;
                case "colDeptEmail":    getter = r => r.DepartmentEmail ?? ""; break;
                case "colBranchEmail": getter = r => r.BranchEmail     ?? ""; break;
                default: return;
            }

            var distinctValues = _allEmployees
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _columnFilters.TryGetValue(col.Name, out var currentFilter);

            var headerCell  = _dgvEmployees.GetCellDisplayRectangle(col.Index, -1, false);
            var screenBelow = _dgvEmployees.PointToScreen(new Point(headerCell.Left, headerCell.Bottom));
            var screenAbove = _dgvEmployees.PointToScreen(new Point(headerCell.Left, headerCell.Top));

            using (var popup = new ColumnFilterPopup(col.HeaderText, distinctValues, currentFilter))
            {
                var wa = Screen.FromPoint(screenBelow).WorkingArea;

                int popupY = (screenBelow.Y + popup.Height <= wa.Bottom)
                    ? screenBelow.Y
                    : Math.Max(wa.Top, screenAbove.Y - popup.Height);

                int popupX = Math.Max(wa.Left, Math.Min(screenBelow.X, wa.Right - popup.Width));

                popup.Location = new Point(popupX, popupY);

                popup.ShowDialog(this.FindForm());

                if (popup.Action == ColumnFilterPopup.PopupAction.SortAscending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending  = true;
                    foreach (DataGridViewColumn c in _dgvEmployees.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    _currentPage = 1;
                    ApplySortToFiltered();
                    UpdateDataGridView();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.SortDescending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending  = false;
                    foreach (DataGridViewColumn c in _dgvEmployees.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                    _currentPage = 1;
                    ApplySortToFiltered();
                    UpdateDataGridView();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.Filter)
                {
                    if (popup.SelectedValues == null || popup.SelectedValues.Count == 0
                        || popup.SelectedValues.Count >= distinctValues.Count)
                        _columnFilters.Remove(col.Name);
                    else
                        _columnFilters[col.Name] = popup.SelectedValues;

                    _currentPage = 1;
                    ApplyFilters();
                }
            }
        }

        // Produces a sort key so "141X" (3-digit manager code) sorts beside "141"
        // rather than after all 4-digit numbers: pads the leading digit run to 10 chars.
        private static string EmpNumSortKey(string empNum)
        {
            if (string.IsNullOrEmpty(empNum)) return "";
            int i = 0;
            while (i < empNum.Length && char.IsDigit(empNum[i])) i++;
            return empNum.Substring(0, i).PadLeft(10, '0') + empNum.Substring(i).ToUpperInvariant();
        }

        private void ApplySortToFiltered()
        {
            if (_filteredEmployees == null || _sortColumnName == null) return;

            Func<EmployeeManagementDto, string> key;
            switch (_sortColumnName)
            {
                case "EmployeeNumber": key = r => EmpNumSortKey(r.EmployeeNumber); break;
                case "Title":        key = r => r.TitleCode      ?? ""; break;
                case "Name":         key = r => r.Name           ?? ""; break;
                case "Position":     key = r => r.Position       ?? ""; break;
                case "Company":      key = r => r.CompanyName    ?? ""; break;
                case "Department":   key = r => r.DepartmentName ?? ""; break;
                case "Branch":       key = r => r.BranchName     ?? ""; break;
                case "colDeptEmail":    key = r => r.DepartmentEmail ?? ""; break;
                case "colBranchEmail": key = r => r.BranchEmail     ?? ""; break;
                default: return;
            }

            _filteredEmployees = _sortAscending
                ? _filteredEmployees.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                : _filteredEmployees.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ── Cell painting (header sort arrows + filter icons) ─────────────────
        private void DgvCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Gray out archived data rows
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var row = _dgvEmployees.Rows[e.RowIndex];
                if (row.DataBoundItem is EmployeeManagementDto emp && emp.IsArchived)
                {
                    e.Handled = true;

                    using (var backBrush = new SolidBrush(Color.FromArgb(235, 235, 235)))
                        e.Graphics.FillRectangle(backBrush, e.CellBounds);

                    if (e.Value != null)
                    {
                        using (var foreBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                        using (var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                        {
                            var textRect = new Rectangle(
                                e.CellBounds.X + 4, e.CellBounds.Y,
                                e.CellBounds.Width - 8, e.CellBounds.Height);
                            e.Graphics.DrawString(e.Value.ToString(), e.CellStyle.Font ?? _dgvEmployees.Font, foreBrush, textRect, fmt);
                        }
                    }

                    using (var borderPen = new Pen(Color.FromArgb(220, 220, 220)))
                        e.Graphics.DrawLine(borderPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }
                return;
            }

            if (e.ColumnIndex < 0) return;

            var column = _dgvEmployees.Columns[e.ColumnIndex];
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
                e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top,    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.CellBounds.Left,      e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var  textRect    = e.CellBounds;
                bool isSortable  = column.SortMode == DataGridViewColumnSortMode.Programmatic;

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
                            e.Graphics.DrawLine(arrowPen, glyphX,      glyphY + 6, glyphX + 5,  glyphY);
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

                e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgvEmployees.Font, textBrush, textRect, fmt);
            }
        }

        // ── Inline email editing ──────────────────────────────────────────────
        private void DgvEmployees_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex != DeptEmailColIdx && e.ColumnIndex != BranchEmailColIdx && e.ColumnIndex != PersonalEmailColIdx)
            {
                e.Cancel = true;
                return;
            }

            string raw = _dgvEmployees.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            _editOriginalValue = raw.Equals("(None)", StringComparison.OrdinalIgnoreCase) ? string.Empty : raw;
        }

        private void DgvEmployees_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            int col = _dgvEmployees.CurrentCell?.ColumnIndex ?? -1;
            if (col != DeptEmailColIdx && col != BranchEmailColIdx && col != PersonalEmailColIdx) return;

            if (e.Control is TextBox tb)
            {
                tb.AutoCompleteMode = AutoCompleteMode.None;
                var source = new AutoCompleteStringCollection();
                source.AddRange(_emailCache.ToArray());
                tb.AutoCompleteCustomSource = source;
                tb.AutoCompleteSource       = AutoCompleteSource.CustomSource;
                tb.AutoCompleteMode         = AutoCompleteMode.SuggestAppend;

                if (tb.Text.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                    tb.Text = string.Empty;
            }
        }

        private void DgvEmployees_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if ((e.ColumnIndex != DeptEmailColIdx && e.ColumnIndex != BranchEmailColIdx && e.ColumnIndex != PersonalEmailColIdx) || _isSaving)
                return;

            var row     = _dgvEmployees.Rows[e.RowIndex];
            var dataRow = row.DataBoundItem as EmployeeManagementDto;
            if (dataRow == null) return;

            string newValue = row.Cells[e.ColumnIndex].Value?.ToString()?.Trim() ?? string.Empty;
            if (newValue.Equals("(None)", StringComparison.OrdinalIgnoreCase)) newValue = string.Empty;

            if (string.Equals(newValue, _editOriginalValue, StringComparison.OrdinalIgnoreCase)) return;

            if (e.ColumnIndex == DeptEmailColIdx)
                _ = CommitDeptEmailChangeAsync(e.RowIndex, dataRow, newValue);
            else if (e.ColumnIndex == BranchEmailColIdx)
                _ = CommitBranchEmailChangeAsync(e.RowIndex, dataRow, newValue);
            else
                _ = CommitPersonalEmailChangeAsync(e.RowIndex, dataRow, newValue);
        }

        // ── Commit: Department Email (shared per dept) ────────────────────────
        private async Task CommitDeptEmailChangeAsync(int rowIndex, EmployeeManagementDto dataRow, string newEmail)
        {
            _isSaving = true;
            try
            {
                string original = _editOriginalValue ?? string.Empty;

                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    var confirm = MessageBox.Show(
                        $"Remove the department email for:\n\n  {dataRow.CompanyName}  /  {dataRow.DepartmentName}\n\nThis affects all employees in this department.",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original); return; }

                    try
                    {
                        await DeleteDeptEmailAsync(dataRow.CompanyName, dataRow.DepartmentName);
                        PropagateParentDeptEmail(dataRow.CompanyName, dataRow.DepartmentName, "(None)");
                        FlashCell(rowIndex, DeptEmailColIdx, success: true);
                        ShowStatus($"Department email removed for \"{dataRow.DepartmentName}\".", success: false);
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, DeptEmailColIdx, success: false);
                        ShowStatus($"Error removing department email: {ex.Message}", success: false);
                        Logger.LogError("EmployeeManagementPage.DeleteDeptEmailAsync failed", ex);
                    }
                    return;
                }

                if (!IsValidEmailFormat(newEmail))
                {
                    MessageBox.Show($"\"{newEmail}\" is not a valid email address.", "Invalid Email",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                    return;
                }

                int emailId;
                if (!_emailIdMap.TryGetValue(newEmail, out emailId))
                {
                    var confirm = MessageBox.Show(
                        $"\"{newEmail}\" is not in the email list. Add it?",
                        "New Email", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original); return; }

                    try
                    {
                        emailId = await InsertEmailAddressAsync(newEmail);
                        _emailCache.Add(newEmail);
                        _emailIdMap[newEmail] = emailId;
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, DeptEmailColIdx, success: false);
                        ShowStatus($"Error inserting email: {ex.Message}", success: false);
                        Logger.LogError("EmployeeManagementPage.InsertEmailAddressAsync (dept) failed", ex);
                        return;
                    }
                }

                int affected = _allEmployees.Count(r => r.CompanyName == dataRow.CompanyName && r.DepartmentName == dataRow.DepartmentName);
                if (affected > 1)
                {
                    var confirm = MessageBox.Show(
                        $"This email will apply to all {affected} employees in:\n\n  {dataRow.CompanyName}  /  {dataRow.DepartmentName}\n\nContinue?",
                        "Confirm Department Email", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original); return; }
                }

                try
                {
                    await UpsertDeptEmailAsync(dataRow.CompanyName, dataRow.DepartmentName, emailId);
                    PropagateParentDeptEmail(dataRow.CompanyName, dataRow.DepartmentName, newEmail);
                    FlashCell(rowIndex, DeptEmailColIdx, success: true);
                    ShowStatus($"Department email updated for \"{dataRow.DepartmentName}\" ({affected} employee{(affected == 1 ? "" : "s")}).", success: true);
                }
                catch (Exception ex)
                {
                    RevertCell(rowIndex, DeptEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                    FlashCell(rowIndex, DeptEmailColIdx, success: false);
                    ShowStatus($"Error saving department email: {ex.Message}", success: false);
                    Logger.LogError("EmployeeManagementPage.UpsertDeptEmailAsync failed", ex);
                }
            }
            finally { _isSaving = false; }
        }

        // ── Commit: Branch Email (shared per branch) ─────────────────────────
        private async Task CommitBranchEmailChangeAsync(int rowIndex, EmployeeManagementDto dataRow, string newEmail)
        {
            _isSaving = true;
            try
            {
                string original = _editOriginalValue ?? string.Empty;

                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    var confirm = MessageBox.Show(
                        $"Remove the branch email for:\n\n  {dataRow.CompanyName}  /  {dataRow.BranchName}\n\nThis affects all employees in this branch.",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original); return; }

                    try
                    {
                        await DeleteBranchEmailAsync(dataRow.CompanyName, dataRow.DepartmentName, dataRow.BranchName);
                        PropagateBranchEmail(dataRow.CompanyName, dataRow.BranchName, "(None)");
                        FlashCell(rowIndex, BranchEmailColIdx, success: true);
                        ShowStatus($"Branch email removed for \"{dataRow.BranchName}\".", success: false);
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, BranchEmailColIdx, success: false);
                        ShowStatus($"Error removing branch email: {ex.Message}", success: false);
                        Logger.LogError("EmployeeManagementPage.DeleteBranchEmailAsync failed", ex);
                    }
                    return;
                }

                if (!IsValidEmailFormat(newEmail))
                {
                    MessageBox.Show($"\"{newEmail}\" is not a valid email address.", "Invalid Email",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                    return;
                }

                int emailId;
                if (!_emailIdMap.TryGetValue(newEmail, out emailId))
                {
                    var confirm = MessageBox.Show(
                        $"\"{newEmail}\" is not in the email list. Add it?",
                        "New Email", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original); return; }

                    try
                    {
                        emailId = await InsertEmailAddressAsync(newEmail);
                        _emailCache.Add(newEmail);
                        _emailIdMap[newEmail] = emailId;
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, BranchEmailColIdx, success: false);
                        ShowStatus($"Error inserting email: {ex.Message}", success: false);
                        Logger.LogError("EmployeeManagementPage.InsertEmailAddressAsync (branch) failed", ex);
                        return;
                    }
                }

                int affected = _allEmployees.Count(r => r.CompanyName == dataRow.CompanyName && r.BranchName == dataRow.BranchName);
                if (affected > 1)
                {
                    var confirm = MessageBox.Show(
                        $"This email will apply to all {affected} employees in:\n\n  {dataRow.CompanyName}  /  {dataRow.BranchName}\n\nContinue?",
                        "Confirm Branch Email", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original); return; }
                }

                try
                {
                    await UpsertBranchEmailAsync(dataRow.CompanyName, dataRow.DepartmentName, dataRow.BranchName, emailId);
                    PropagateBranchEmail(dataRow.CompanyName, dataRow.BranchName, newEmail);
                    FlashCell(rowIndex, BranchEmailColIdx, success: true);
                    ShowStatus($"Branch email updated for \"{dataRow.BranchName}\" ({affected} employee{(affected == 1 ? "" : "s")}).", success: true);
                }
                catch (Exception ex)
                {
                    RevertCell(rowIndex, BranchEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                    FlashCell(rowIndex, BranchEmailColIdx, success: false);
                    ShowStatus($"Error saving branch email: {ex.Message}", success: false);
                    Logger.LogError("EmployeeManagementPage.UpsertBranchEmailAsync failed", ex);
                }
            }
            finally { _isSaving = false; }
        }

        // ── Commit: Personal Email (per employee) ─────────────────────────────
        private async Task CommitPersonalEmailChangeAsync(int rowIndex, EmployeeManagementDto dataRow, string newValue)
        {
            _isSaving = true;
            try
            {
                string original = _editOriginalValue ?? string.Empty;

                // Empty → remove binding
                if (string.IsNullOrWhiteSpace(newValue))
                {
                    var confirm = MessageBox.Show(
                        $"Remove email binding for {dataRow.Name}?",
                        "Confirm Remove",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes)
                    {
                        RevertCell(rowIndex, PersonalEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        return;
                    }

                    try
                    {
                        await RemoveEmployeeEmailBindingAsync(dataRow.EmpId);
                        dataRow.PrimaryEmail = "(None)";
                        _dgvEmployees.Rows[rowIndex].Cells[PersonalEmailColIdx].Value = "(None)";
                        FlashCell(rowIndex, PersonalEmailColIdx, success: true);
                        ShowStatus($"Email binding removed for {dataRow.Name}.", success: true);
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, PersonalEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, PersonalEmailColIdx, success: false);
                        ShowStatus($"Error removing binding: {ex.Message}", success: false);
                        Logger.LogError("EmployeeManagementPage.RemoveEmployeeEmailBindingAsync failed", ex);
                    }
                    return;
                }

                // Validate format
                if (!IsValidEmailFormat(newValue))
                {
                    MessageBox.Show($"\"{newValue}\" is not a valid email address.", "Invalid Email",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RevertCell(rowIndex, PersonalEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                    return;
                }

                // Lookup email ID
                int emailId;
                if (!_emailIdMap.TryGetValue(newValue, out emailId))
                {
                    var confirm = MessageBox.Show(
                        $"\"{newValue}\" is not in the email list. Add it and assign to {dataRow.Name}?",
                        "New Email",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes)
                    {
                        RevertCell(rowIndex, PersonalEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                        return;
                    }

                    try
                    {
                        emailId = await InsertEmailAddressAsync(newValue);
                        _emailCache.Add(newValue);
                        _emailIdMap[newValue] = emailId;
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, PersonalEmailColIdx, success: false);
                        ShowStatus($"Error inserting email: {ex.Message}", success: false);
                        Logger.LogError("EmployeeManagementPage.InsertEmailAddressAsync failed", ex);
                        return;
                    }
                }

                // Save binding
                try
                {
                    await SaveEmployeeEmailBindingAsync(dataRow.EmpId, emailId);
                    dataRow.PrimaryEmail = newValue;
                    _dgvEmployees.Rows[rowIndex].Cells[PersonalEmailColIdx].Value = newValue;
                    FlashCell(rowIndex, PersonalEmailColIdx, success: true);
                    ShowStatus($"Email updated for {dataRow.Name}.", success: true);
                }
                catch (Exception ex)
                {
                    RevertCell(rowIndex, PersonalEmailColIdx, string.IsNullOrEmpty(original) ? "(None)" : original);
                    FlashCell(rowIndex, PersonalEmailColIdx, success: false);
                    ShowStatus($"Error saving binding: {ex.Message}", success: false);
                    Logger.LogError("EmployeeManagementPage.SaveEmployeeEmailBindingAsync failed", ex);
                }
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void PropagateParentDeptEmail(string company, string department, string emailText)
        {
            foreach (var r in _allEmployees)
            {
                if (r.CompanyName == company && r.DepartmentName == department)
                    r.DepartmentEmail = emailText;
            }
            _dgvEmployees.Invalidate();
        }

        private void PropagateBranchEmail(string company, string branch, string emailText)
        {
            foreach (var r in _allEmployees)
            {
                if (r.CompanyName == company && r.BranchName == branch)
                    r.BranchEmail = emailText;
            }
            _dgvEmployees.Invalidate();
        }

        private void RevertCell(int rowIndex, int colIndex, string value)
        {
            if (rowIndex >= 0 && rowIndex < _dgvEmployees.Rows.Count)
                _dgvEmployees.Rows[rowIndex].Cells[colIndex].Value = value;
        }

        // Overload used when only flashing is needed (no specific revert value)
        private void RevertCell(int rowIndex, int colIndex, bool success)
        {
            string fallback = _dgvEmployees.Rows[rowIndex].Cells[colIndex].Value?.ToString() ?? "(None)";
            RevertCell(rowIndex, colIndex, fallback);
        }

        private void FlashCell(int rowIndex, int colIndex, bool success)
        {
            if (rowIndex < 0 || rowIndex >= _dgvEmployees.Rows.Count) return;
            var cell = _dgvEmployees.Rows[rowIndex].Cells[colIndex];
            cell.Style.BackColor = success ? Color.FromArgb(180, 240, 180) : Color.FromArgb(255, 180, 180);

            var t = new System.Windows.Forms.Timer { Interval = 1600 };
            t.Tick += (s, e) =>
            {
                t.Stop(); t.Dispose();
                if (rowIndex < _dgvEmployees.Rows.Count)
                    _dgvEmployees.Rows[rowIndex].Cells[colIndex].Style.BackColor = Color.Empty;
            };
            t.Start();
        }

        private void ShowStatus(string message, bool success)
        {
            _lblStatus.Text      = message;
            _lblStatus.BackColor = success ? Color.FromArgb(210, 245, 210) : Color.FromArgb(255, 220, 220);
            _lblStatus.ForeColor = success ? Color.FromArgb(30, 120, 30)   : Color.FromArgb(160, 0, 0);
            _lblStatus.Visible   = true;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private static bool IsValidEmailFormat(string email)
        {
            int atIdx  = email.IndexOf('@');
            if (atIdx <= 0) return false;
            int dotIdx = email.LastIndexOf('.');
            return dotIdx > atIdx + 1 && dotIdx < email.Length - 1;
        }

        // ── Email DB methods ──────────────────────────────────────────────────
        private async Task UpsertBranchEmailAsync(string company, string department, string branch, int emailId)
        {
            // Branch email is stored in dbo.DepartmentAccount.EmailAddressId
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.DepartmentAccount WHERE CompanyName = @Company AND DepartmentName = @Dept AND BranchName = @Branch)
                    UPDATE dbo.DepartmentAccount SET EmailAddressId = @EmailId WHERE CompanyName = @Company AND DepartmentName = @Dept AND BranchName = @Branch
                ELSE
                    INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName, EmailAddressId) VALUES (@Company, @Dept, @Branch, @EmailId)";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Dept",    department);
                    cmd.Parameters.AddWithValue("@Branch",  branch);
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task DeleteBranchEmailAsync(string company, string department, string branch)
        {
            // Null out the EmailAddressId but keep the DepartmentAccount row intact
            const string sql = @"
                UPDATE dbo.DepartmentAccount SET EmailAddressId = NULL
                WHERE CompanyName = @Company AND DepartmentName = @Dept AND BranchName = @Branch";
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Dept",    department);
                    cmd.Parameters.AddWithValue("@Branch",  branch);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpsertDeptEmailAsync(string company, string department, int emailId)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.DepartmentEmail WHERE CompanyName = @Company AND DepartmentName = @Dept)
                    UPDATE dbo.DepartmentEmail SET EmailAddressId = @EmailId WHERE CompanyName = @Company AND DepartmentName = @Dept
                ELSE
                    INSERT INTO dbo.DepartmentEmail (CompanyName, DepartmentName, EmailAddressId) VALUES (@Company, @Dept, @EmailId)";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Dept",    department);
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task DeleteDeptEmailAsync(string company, string department)
        {
            const string sql = "DELETE FROM dbo.DepartmentEmail WHERE CompanyName = @Company AND DepartmentName = @Dept";
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Dept",    department);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<int> InsertEmailAddressAsync(string email)
        {
            const string sql = @"
                UPDATE dbo.EmailAddress SET IsActive = 1
                WHERE EmailAddress = @Email AND IsActive = 0;

                IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
                    INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive)
                    VALUES (@Email, @Email, 1);

                SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        private async Task SaveEmployeeEmailBindingAsync(int empId, int emailId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@EmpId", empId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        int count;
                        using (var cmd = new SqlCommand(
                            "SELECT COUNT(*) FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@EmpId",   empId);
                            cmd.Parameters.AddWithValue("@EmailId", emailId);
                            count = (int)await cmd.ExecuteScalarAsync();
                        }

                        if (count > 0)
                        {
                            using (var cmd = new SqlCommand(
                                "UPDATE dbo.EmployeeEmail SET IsPrimary = 1, IsActive = 1 WHERE EmpId = @EmpId AND EmailId = @EmailId", con, tx))
                            {
                                cmd.Parameters.AddWithValue("@EmpId",   empId);
                                cmd.Parameters.AddWithValue("@EmailId", emailId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            using (var cmd = new SqlCommand(
                                "INSERT INTO dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive) VALUES (@EmpId, @EmailId, 'Work', 1, 1)", con, tx))
                            {
                                cmd.Parameters.AddWithValue("@EmpId",   empId);
                                cmd.Parameters.AddWithValue("@EmailId", emailId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        tx.Commit();
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        private async Task RemoveEmployeeEmailBindingAsync(int empId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand("DELETE FROM dbo.EmployeeEmail WHERE EmpId = @EmpId", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ─── Excel Export ─────────────────────────────────────────────────────

        private void BtnExport_Click(object sender, EventArgs e)
        {
            var source = _filteredEmployees ?? _allEmployees;
            if (source == null || source.Count == 0)
            {
                MessageBox.Show("No employees to export.", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter   = "Excel Files|*.xlsx";
                dlg.FileName = $"Employees_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                dlg.Title    = "Export Employees to Excel";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Employees");

                        var headers = new[] { "Employee Number", "Name", "Title", "Position",
                                              "Company", "Department", "Branch", "Status",
                                              "Personal Email", "Dept Email", "Branch Email" };
                        for (int i = 0; i < headers.Length; i++)
                            ws.Cell(1, i + 1).Value = headers[i];

                        var hdr = ws.Range(1, 1, 1, headers.Length);
                        hdr.Style.Font.Bold = true;
                        hdr.Style.Fill.BackgroundColor = XLColor.FromArgb(41, 128, 185);
                        hdr.Style.Font.FontColor = XLColor.White;
                        hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        int row = 2;
                        foreach (var emp in source)
                        {
                            ws.Cell(row, 1).Value  = emp.EmployeeNumber ?? "";
                            ws.Cell(row, 2).Value  = emp.Name ?? "";
                            ws.Cell(row, 3).Value  = emp.TitleCode ?? "";
                            ws.Cell(row, 4).Value  = emp.Position ?? "";
                            ws.Cell(row, 5).Value  = emp.CompanyName ?? "";
                            ws.Cell(row, 6).Value  = emp.DepartmentName ?? "";
                            ws.Cell(row, 7).Value  = emp.BranchName ?? "";
                            ws.Cell(row, 8).Value  = emp.Active ? "Active" : "Inactive";
                            ws.Cell(row, 9).Value  = (emp.PrimaryEmail    == "(None)" ? "" : emp.PrimaryEmail)    ?? "";
                            ws.Cell(row, 10).Value = (emp.DepartmentEmail == "(None)" ? "" : emp.DepartmentEmail) ?? "";
                            ws.Cell(row, 11).Value = (emp.BranchEmail     == "(None)" ? "" : emp.BranchEmail)     ?? "";
                            row++;
                        }

                        // Set explicit column widths (AdjustToContents triggers a SixLabors.Fonts version conflict)
                        int[] widths = { 18, 30, 10, 25, 22, 25, 22, 12, 30, 30, 30 };
                        for (int c = 0; c < widths.Length; c++)
                            ws.Column(c + 1).Width = widths[c];

                        wb.SaveAs(dlg.FileName);
                    }

                    ActivityLogger.Log(ActivityLogger.Actions.Export, "Employee", 0,
                        $"Exported {source.Count} employees to Excel");

                    var open = MessageBox.Show(
                        $"Exported {source.Count} employee(s) successfully.\n\nOpen file?",
                        "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (open == DialogResult.Yes)
                        System.Diagnostics.Process.Start(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ─── Excel Template ───────────────────────────────────────────────────

        private void BtnDownloadTemplate_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter   = "Excel Files|*.xlsx";
                dlg.FileName = "Employee_Import_Template.xlsx";
                dlg.Title    = "Save Import Template";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Employees");

                        var cols = new (string Header, bool Required)[]
                        {
                            ("Employee Number", true),
                            ("Name",            true),
                            ("Title",           false),
                            ("Position",        false),
                            ("Description",     false),
                            ("Company",         true),
                            ("Branch",          true),
                            ("Department",      false)
                        };

                        for (int i = 0; i < cols.Length; i++)
                        {
                            var cell = ws.Cell(1, i + 1);
                            cell.Value = cols[i].Header;
                            cell.Style.Font.Bold = true;
                            cell.Style.Fill.BackgroundColor = cols[i].Required
                                ? XLColor.FromArgb(41, 128, 185)
                                : XLColor.FromArgb(100, 149, 237);
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            if (cols[i].Required)
                                cell.GetComment().AddText("Required");
                        }

                        // Sample row
                        ws.Cell(2, 1).Value = "EMP-001";
                        ws.Cell(2, 2).Value = "Juan Dela Cruz";
                        ws.Cell(2, 3).Value = "MR";
                        ws.Cell(2, 4).Value = "Sales Representative";
                        ws.Cell(2, 5).Value = "Senior sales rep";
                        ws.Cell(2, 6).Value = "Yakult Philippines";
                        ws.Cell(2, 7).Value = "Manila Branch";
                        ws.Cell(2, 8).Value = "Sales Department";

                        var sample = ws.Range(2, 1, 2, cols.Length);
                        sample.Style.Fill.BackgroundColor = XLColor.FromArgb(235, 245, 251);
                        sample.Style.Font.Italic = true;
                        sample.Style.Font.FontColor = XLColor.FromArgb(100, 100, 100);

                        var wsNotes = wb.Worksheets.Add("Instructions");
                        wsNotes.Cell(1, 1).Value = "Employee Import Instructions";
                        wsNotes.Cell(1, 1).Style.Font.Bold = true;
                        wsNotes.Cell(1, 1).Style.Font.FontSize = 12;
                        wsNotes.Cell(3, 1).Value = "Required columns (dark blue): Employee Number, Name, Company, Branch";
                        wsNotes.Cell(4, 1).Value = "Optional columns (light blue): Title, Position, Description, Department";
                        wsNotes.Cell(5, 1).Value = "Employee Number is the matching key — existing = update; new = insert.";
                        wsNotes.Cell(6, 1).Value = "Company, Branch, Department, and Title must exactly match system names.";
                        wsNotes.Cell(7, 1).Value = "Delete row 2 (the sample row) before importing.";
                        wsNotes.Column(1).Width = 90;

                        // Explicit widths — AdjustToContents triggers a SixLabors.Fonts version conflict
                        int[] colWidths = { 18, 28, 10, 25, 30, 24, 22, 25 };
                        for (int c = 0; c < colWidths.Length; c++)
                            ws.Column(c + 1).Width = colWidths[c];

                        wb.SaveAs(dlg.FileName);
                    }

                    var open = MessageBox.Show("Template saved.\n\nOpen file?",
                        "Template Ready", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (open == DialogResult.Yes)
                        System.Diagnostics.Process.Start(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate template:\n\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ─── Excel Import ─────────────────────────────────────────────────────

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Excel Files|*.xlsx";
                dlg.Title  = "Select Employee Import File";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    Cursor = Cursors.WaitCursor;
                    var batch = ParseImportFile(dlg.FileName);
                    Cursor = Cursors.Default;

                    using (var preview = new EmployeeExcelImportPreviewForm(batch))
                    {
                        if (preview.ShowDialog(this.FindForm()) != DialogResult.OK) return;
                        ExecuteImport(batch, preview.IncludeArchiving);
                    }
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    MessageBox.Show($"Import failed:\n\n{ex.Message}", "Import Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private EmployeeImportBatch ParseImportFile(string filePath)
        {
            var companies   = LoadImportLookup("SELECT ComId,    Name FROM dbo.Company");
            var branches    = LoadImportLookup("SELECT BranchId, Name FROM dbo.Branch");
            var departments = LoadImportLookup("SELECT DeptId,   Name FROM dbo.Department");
            var titles      = LoadImportLookup("SELECT TitleId,  Code FROM dbo.Title");

            var existingByNumber = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Fallback: employees without a number matched by "Name|CompanyName"
            var existingByNameCompany = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var existingById = new Dictionary<int, EmployeeManagementDto>();
            foreach (var emp in _allEmployees ?? new List<EmployeeManagementDto>())
            {
                existingById[emp.EmpId] = emp;
                if (!string.IsNullOrWhiteSpace(emp.EmployeeNumber))
                {
                    existingByNumber[emp.EmployeeNumber.Trim()] = emp.EmpId;
                }
                else
                {
                    string nameKey = $"{(emp.Name ?? "").Trim()}|{(emp.CompanyName ?? "").Trim()}";
                    if (!existingByNameCompany.ContainsKey(nameKey))
                        existingByNameCompany[nameKey] = emp.EmpId;
                }
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            DataTable table;
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var conf = new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                };
                var ds = reader.AsDataSet(conf);
                table = ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
            }

            var missing = new[] { "Name", "Company", "Branch" }
                .Where(c => !table.Columns.Contains(c)).ToList();
            if (missing.Count > 0)
                throw new Exception(
                    $"Missing required column(s): {string.Join(", ", missing)}\n\n" +
                    "Use 'Template' to download the correct column layout.");

            var batch       = new EmployeeImportBatch();
            var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                var dr     = table.Rows[i];
                int rowNum = i + 2;

                string empNum   = GetImportCell(dr, "Employee Number");
                string name     = GetImportCell(dr, "Name");
                string title    = GetImportCell(dr, "Title");
                string position = GetImportCell(dr, "Position");
                string desc     = GetImportCell(dr, "Description");
                string company  = GetImportCell(dr, "Company");
                string branch   = GetImportCell(dr, "Branch");
                string dept     = GetImportCell(dr, "Department");

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(empNum) && string.IsNullOrWhiteSpace(company))
                    continue;

                if (!string.IsNullOrWhiteSpace(empNum))
                    seenNumbers.Add(empNum.Trim());

                var row = new EmployeeImportRow
                {
                    RowNumber      = rowNum,
                    EmployeeNumber = string.IsNullOrWhiteSpace(empNum) ? null : empNum.Trim(),
                    Name           = name,
                    TitleCode      = title,
                    Position       = position,
                    Description    = desc,
                    CompanyName    = company,
                    BranchName     = branch,
                    DepartmentName = dept
                };

                if (string.IsNullOrWhiteSpace(name))
                    row.Errors.Add("Name is required");

                if (string.IsNullOrWhiteSpace(company))
                {
                    row.Errors.Add("Company is required");
                }
                else
                {
                    int id = LookupImportId(companies, company.Trim());
                    if (id == 0) row.Errors.Add($"Company '{company}' not found");
                    else         row.CompanyId = id;
                }

                if (string.IsNullOrWhiteSpace(branch))
                {
                    row.Errors.Add("Branch is required");
                }
                else
                {
                    int id = LookupImportId(branches, branch.Trim());
                    if (id == 0) row.Errors.Add($"Branch '{branch}' not found");
                    else         row.BranchId = id;
                }

                if (!string.IsNullOrWhiteSpace(dept))
                {
                    int id = LookupImportId(departments, dept.Trim());
                    if (id == 0) row.Errors.Add($"Department '{dept}' not found");
                    else         row.DepartmentId = id;
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    int id = LookupImportId(titles, title.Trim());
                    if (id == 0) row.Errors.Add($"Title code '{title}' not found");
                    else         row.TitleId = id;
                }

                if (!row.IsValid) { batch.InvalidRows.Add(row); continue; }

                int matchedId = 0;
                if (!string.IsNullOrWhiteSpace(row.EmployeeNumber) &&
                    existingByNumber.TryGetValue(row.EmployeeNumber, out int byNum))
                {
                    matchedId = byNum;
                }
                else if (string.IsNullOrWhiteSpace(row.EmployeeNumber))
                {
                    string nameKey = $"{row.Name.Trim()}|{row.CompanyName.Trim()}";
                    existingByNameCompany.TryGetValue(nameKey, out matchedId);
                }

                if (matchedId != 0)
                {
                    row.EmpId = matchedId;
                    if (existingById.TryGetValue(matchedId, out var existing) && !ImportRowHasChanges(row, existing))
                        continue; // no fields changed — skip entirely
                    batch.UpdatedRecords.Add(row);
                }
                else
                {
                    batch.NewRecords.Add(row);
                }
            }

            // Employees with a number that didn't appear in the file are archive candidates
            foreach (var emp in _allEmployees ?? new List<EmployeeManagementDto>())
            {
                if (string.IsNullOrWhiteSpace(emp.EmployeeNumber) || emp.IsArchived) continue;
                if (!seenNumbers.Contains(emp.EmployeeNumber.Trim()))
                {
                    batch.ArchiveCandidates.Add(new EmployeeArchiveCandidate
                    {
                        EmpId          = emp.EmpId,
                        Name           = emp.Name,
                        EmployeeNumber = emp.EmployeeNumber,
                        Position       = emp.Position,
                        CompanyName    = emp.CompanyName
                    });
                }
            }

            return batch;
        }

        private void ExecuteImport(EmployeeImportBatch batch, bool includeArchiving)
        {
            try
            {
                int inserted = 0, updated = 0, archived = 0;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            foreach (var row in batch.NewRecords)
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Employee
                                        (Name, Position, Description, DateCreated, Createdby, ComId, DeptId, BranchId, EmployeeNumber, TitleId)
                                    VALUES
                                        (@Name, @Position, @Description, GETDATE(), @CreatedBy, @ComId, @DeptId, @BranchId, @EmployeeNumber, @TitleId)",
                                    con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name",           row.Name);
                                    cmd.Parameters.AddWithValue("@Position",       (object)row.Position    ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Description",    (object)row.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@CreatedBy",      AppSession.CurrentUserId);
                                    cmd.Parameters.AddWithValue("@ComId",          row.CompanyId);
                                    cmd.Parameters.AddWithValue("@DeptId",         row.DepartmentId.HasValue ? (object)row.DepartmentId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@BranchId",       row.BranchId);
                                    cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(row.EmployeeNumber) ? (object)DBNull.Value : row.EmployeeNumber);
                                    cmd.Parameters.AddWithValue("@TitleId",        row.TitleId.HasValue ? (object)row.TitleId.Value : DBNull.Value);
                                    cmd.ExecuteNonQuery();
                                }
                                inserted++;
                            }

                            foreach (var row in batch.UpdatedRecords)
                            {
                                using (var cmd = new SqlCommand(@"
                                    UPDATE dbo.Employee
                                    SET Name           = @Name,
                                        -- Preserve existing casing when the only difference is upper/lower case
                                        Position       = CASE
                                                             WHEN @Position IS NULL THEN NULL
                                                             WHEN UPPER(LTRIM(RTRIM(ISNULL(Position,'')))) = UPPER(LTRIM(RTRIM(@Position))) THEN Position
                                                             ELSE @Position
                                                         END,
                                        Description    = @Description,
                                        ComId          = @ComId,
                                        DeptId         = @DeptId,
                                        BranchId       = @BranchId,
                                        EmployeeNumber = @EmployeeNumber,
                                        TitleId        = @TitleId
                                    WHERE EmpId = @EmpId",
                                    con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@EmpId",          row.EmpId.Value);
                                    cmd.Parameters.AddWithValue("@Name",           row.Name);
                                    cmd.Parameters.AddWithValue("@Position",       (object)row.Position    ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Description",    (object)row.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ComId",          row.CompanyId);
                                    cmd.Parameters.AddWithValue("@DeptId",         row.DepartmentId.HasValue ? (object)row.DepartmentId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@BranchId",       row.BranchId);
                                    cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(row.EmployeeNumber) ? (object)DBNull.Value : row.EmployeeNumber);
                                    cmd.Parameters.AddWithValue("@TitleId",        row.TitleId.HasValue ? (object)row.TitleId.Value : DBNull.Value);
                                    cmd.ExecuteNonQuery();
                                }
                                updated++;
                            }

                            if (includeArchiving)
                            {
                                foreach (var c in batch.ArchiveCandidates)
                                {
                                    using (var cmd = new SqlCommand(@"
                                        IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus WHERE EntityType='Employee' AND EntityId=@EmpId AND IsArchived=1)
                                        INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        VALUES ('Employee', @EmpId, 1, GETDATE(), @ArchivedBy, 'Archived via Excel Import')",
                                        con, tx))
                                    {
                                        cmd.Parameters.AddWithValue("@EmpId",      c.EmpId);
                                        cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                        cmd.ExecuteNonQuery();
                                    }
                                    archived++;
                                }
                            }

                            tx.Commit();
                        }
                        catch { tx.Rollback(); throw; }
                    }
                }

                ActivityLogger.Log(ActivityLogger.Actions.Import, "Employee", 0,
                    $"Excel import: {inserted} inserted, {updated} updated, {archived} archived by {AppSession.CurrentUserName}");

                MessageBox.Show(
                    $"Import completed successfully!\n\n" +
                    $"  Inserted:  {inserted}\n" +
                    $"  Updated:   {updated}\n" +
                    $"  Archived:  {archived}",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _ = LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed and was rolled back:\n\n{ex.Message}",
                    "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─── Import helpers ───────────────────────────────────────────────────

        private Dictionary<string, int> LoadImportLookup(string sql)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int id    = r.GetInt32(0);
                        string nm = r.IsDBNull(1) ? null : r.GetString(1);
                        if (!string.IsNullOrWhiteSpace(nm) && !dict.ContainsKey(nm.Trim()))
                            dict[nm.Trim()] = id;
                    }
                }
            }
            return dict;
        }

        private static int LookupImportId(Dictionary<string, int> lookup, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            return lookup.TryGetValue(name.Trim(), out int id) ? id : 0;
        }

        private static string GetImportCell(DataRow dr, string column)
        {
            if (!dr.Table.Columns.Contains(column)) return null;
            var v = dr[column];
            return v == null || v == DBNull.Value ? null : v.ToString()?.Trim();
        }

        private static bool ImportRowHasChanges(EmployeeImportRow row, EmployeeManagementDto existing)
        {
            static string N(string s) => (s ?? "").Trim();
            static bool Diff(string a, string b) =>
                !string.Equals(N(a), N(b), StringComparison.OrdinalIgnoreCase);

            return Diff(row.Name,           existing.Name)
                || Diff(row.TitleCode,       existing.TitleCode)
                || Diff(row.Position,        existing.Position)
                || Diff(row.CompanyName,     existing.CompanyName)
                || Diff(row.BranchName,      existing.BranchName)
                || Diff(row.DepartmentName,  existing.DepartmentName);
        }
    }

    // ── DTO ───────────────────────────────────────────────────────────────────
    public class EmployeeManagementDto : System.ComponentModel.INotifyPropertyChanged
    {
        public int    EmpId           { get; set; }
        public string EmployeeNumber  { get; set; }
        public string Name            { get; set; }
        public string TitleCode       { get; set; }
        public string Position        { get; set; }
        public bool   Active          { get; set; }
        public string CompanyName     { get; set; }
        public string BranchName      { get; set; }
        public string DepartmentName  { get; set; }
        public string DistributorName { get; set; }
        public string DepartmentEmail { get; set; }
        public string BranchEmail     { get; set; }
        public string PrimaryEmail    { get; set; }
        public bool   IsArchived      { get; set; }
        // Computed — used as DataPropertyName for the Status column
        public string ActiveDisplay   => Active ? "Active" : "Inactive";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    // ── Edit / Add dialog ─────────────────────────────────────────────────────
    public class EmployeeEditDialog : Form
    {
        private readonly string _connectionString;
        private readonly EmployeeManagementDto _employee;
        private readonly bool _isEdit;

        private TextBox txtEmployeeNumber;
        private TextBox txtName;
        private TextBox txtPosition;
        private ComboBox cboCompany;
        private ComboBox cboBranch;
        private ComboBox cboDepartment;
        private CheckBox chkActive;
        private TextBox txtDeptEmail;
        private TextBox txtBranchEmail;
        private TextBox txtPersonalEmail;
        private Button btnSave;
        private Button btnCancel;

        private List<CompanyLookup>    _companies;
        private List<BranchLookup>     _branches;
        private List<DepartmentLookup> _departments;

        public EmployeeEditDialog(string connectionString, EmployeeManagementDto employee = null)
        {
            _connectionString = connectionString;
            _employee         = employee;
            _isEdit           = employee != null;

            InitializeDialog();
            LoadData();
        }

        private void InitializeDialog()
        {
            Text            = _isEdit ? "Edit Employee" : "Add New Employee";
            Size            = new Size(500, 590);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;

            int yPos         = 20;
            int labelWidth   = 120;
            int controlWidth = 320;

            // Employee Number
            Controls.Add(new Label { Text = "Employee Number:", Location = new Point(20, yPos), Width = labelWidth });
            txtEmployeeNumber = new TextBox { Location = new Point(150, yPos), Width = controlWidth };
            Controls.Add(txtEmployeeNumber);
            yPos += 35;

            // Name
            Controls.Add(new Label { Text = "Name: *", Location = new Point(20, yPos), Width = labelWidth });
            txtName = new TextBox { Location = new Point(150, yPos), Width = controlWidth };
            Controls.Add(txtName);
            yPos += 35;

            // Position
            Controls.Add(new Label { Text = "Position:", Location = new Point(20, yPos), Width = labelWidth });
            txtPosition = new TextBox { Location = new Point(150, yPos), Width = controlWidth };
            Controls.Add(txtPosition);
            yPos += 35;

            // Company
            Controls.Add(new Label { Text = "Company: *", Location = new Point(20, yPos), Width = labelWidth });
            cboCompany = new ComboBox { Location = new Point(150, yPos), Width = controlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(cboCompany);
            yPos += 35;

            // Branch
            Controls.Add(new Label { Text = "Branch: *", Location = new Point(20, yPos), Width = labelWidth });
            cboBranch = new ComboBox { Location = new Point(150, yPos), Width = controlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(cboBranch);
            yPos += 35;

            // Department
            Controls.Add(new Label { Text = "Department: *", Location = new Point(20, yPos), Width = labelWidth });
            cboDepartment = new ComboBox { Location = new Point(150, yPos), Width = controlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(cboDepartment);
            yPos += 35;

            // ── Email Addresses ──────────────────────────────────────────────
            var lblEmailSection = new Label
            {
                Text      = "Email Addresses",
                Location  = new Point(20, yPos),
                Width     = 460,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            Controls.Add(lblEmailSection);
            yPos += 26;

            // Dept Email
            Controls.Add(new Label { Text = "Dept Email:", Location = new Point(20, yPos), Width = labelWidth, AutoSize = false });
            txtDeptEmail = new TextBox { Location = new Point(150, yPos), Width = controlWidth };
            var ttDept = new ToolTip();
            ttDept.SetToolTip(txtDeptEmail, "Shared across all employees in this department.");
            Controls.Add(txtDeptEmail);
            yPos += 35;

            // Branch Email
            Controls.Add(new Label { Text = "Branch Email:", Location = new Point(20, yPos), Width = labelWidth, AutoSize = false });
            txtBranchEmail = new TextBox { Location = new Point(150, yPos), Width = controlWidth };
            var ttBranch = new ToolTip();
            ttBranch.SetToolTip(txtBranchEmail, "Shared across all employees in this branch.");
            Controls.Add(txtBranchEmail);
            yPos += 35;

            // Personal Email
            Controls.Add(new Label { Text = "Personal Email:", Location = new Point(20, yPos), Width = labelWidth, AutoSize = false });
            txtPersonalEmail = new TextBox { Location = new Point(150, yPos), Width = controlWidth };
            var ttPersonal = new ToolTip();
            ttPersonal.SetToolTip(txtPersonalEmail, "This employee's personal/primary email.");
            Controls.Add(txtPersonalEmail);
            yPos += 40;

            // Active
            chkActive = new CheckBox { Text = "Active", Location = new Point(150, yPos), Checked = true };
            Controls.Add(chkActive);
            yPos += 40;

            // Buttons
            btnSave = new Button { Text = "Save", Location = new Point(270, yPos), Width = 90, DialogResult = DialogResult.OK };
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel = new Button { Text = "Cancel", Location = new Point(370, yPos), Width = 90, DialogResult = DialogResult.Cancel };
            Controls.Add(btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private async void LoadData()
        {
            try
            {
                _companies   = new List<CompanyLookup>();
                _branches    = new List<BranchLookup>();
                _departments = new List<DepartmentLookup>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM Company WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            _companies.Add(new CompanyLookup { ComId = reader.GetInt32(0), Name = reader.GetString(1) });

                    using (var cmd = new SqlCommand("SELECT BranchId, Name FROM Branch WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            _branches.Add(new BranchLookup { BranchId = reader.GetInt32(0), Name = reader.GetString(1) });

                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM Department WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            _departments.Add(new DepartmentLookup { DeptId = reader.GetInt32(0), Name = reader.GetString(1) });
                }

                cboCompany.DisplayMember    = "Name";
                cboCompany.ValueMember      = "ComId";
                cboCompany.DataSource       = _companies;

                cboBranch.DisplayMember     = "Name";
                cboBranch.ValueMember       = "BranchId";
                cboBranch.DataSource        = _branches;

                cboDepartment.DisplayMember = "Name";
                cboDepartment.ValueMember   = "DeptId";
                cboDepartment.DataSource    = _departments;

                if (_isEdit)
                    PopulateFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateFields()
        {
            txtEmployeeNumber.Text = _employee.EmployeeNumber ?? "";
            txtName.Text           = _employee.Name;
            txtPosition.Text       = _employee.Position ?? "";
            chkActive.Checked      = _employee.Active;

            var company = _companies?.FirstOrDefault(c => c.Name == _employee.CompanyName);
            if (company != null) cboCompany.SelectedValue = company.ComId;

            var branch = _branches?.FirstOrDefault(b => b.Name == _employee.BranchName);
            if (branch != null) cboBranch.SelectedValue = branch.BranchId;

            var dept = _departments?.FirstOrDefault(d => d.Name == _employee.DepartmentName);
            if (dept != null) cboDepartment.SelectedValue = dept.DeptId;

            string deptEmail    = _employee.DepartmentEmail;
            string branchEmail  = _employee.BranchEmail;
            string personalEmail = _employee.PrimaryEmail;

            txtDeptEmail.Text     = (deptEmail    == null || deptEmail    == "(None)") ? "" : deptEmail;
            txtBranchEmail.Text   = (branchEmail  == null || branchEmail  == "(None)") ? "" : branchEmail;
            txtPersonalEmail.Text = (personalEmail == null || personalEmail == "(None)") ? "" : personalEmail;
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter employee name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }
            if (cboCompany.SelectedValue == null)
            {
                MessageBox.Show("Please select a company.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboCompany.Focus();
                return;
            }
            if (cboBranch.SelectedValue == null)
            {
                MessageBox.Show("Please select a branch.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboBranch.Focus();
                return;
            }
            if (cboDepartment.SelectedValue == null)
            {
                MessageBox.Show("Please select a department.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboDepartment.Focus();
                return;
            }

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    if (_isEdit)
                    {
                        const string sql = @"
                            UPDATE Employee
                            SET EmployeeNumber = @EmployeeNumber,
                                Name           = @Name,
                                Position       = @Position,
                                ComId          = @ComId,
                                BranchId       = @BranchId,
                                DeptId         = @DeptId,
                                Active         = @Active
                            WHERE EmpId = @EmpId";

                        using (var cmd = new SqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@EmpId",          _employee.EmpId);
                            cmd.Parameters.AddWithValue("@EmployeeNumber",  (object)txtEmployeeNumber.Text.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Name",            txtName.Text.Trim());
                            cmd.Parameters.AddWithValue("@Position",        (object)txtPosition.Text.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ComId",           cboCompany.SelectedValue);
                            cmd.Parameters.AddWithValue("@BranchId",        cboBranch.SelectedValue);
                            cmd.Parameters.AddWithValue("@DeptId",          cboDepartment.SelectedValue);
                            cmd.Parameters.AddWithValue("@Active",          chkActive.Checked);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await SaveEmailChangesAsync(con);

                        MessageBox.Show("Employee updated successfully.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        const string sql = @"
                            INSERT INTO Employee (EmployeeNumber, Name, Position, ComId, BranchId, DeptId, DateCreated, CreatedBy, Active)
                            VALUES (@EmployeeNumber, @Name, @Position, @ComId, @BranchId, @DeptId, GETDATE(), @CreatedBy, @Active)";

                        using (var cmd = new SqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@EmployeeNumber",  (object)txtEmployeeNumber.Text.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Name",            txtName.Text.Trim());
                            cmd.Parameters.AddWithValue("@Position",        (object)txtPosition.Text.Trim() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ComId",           cboCompany.SelectedValue);
                            cmd.Parameters.AddWithValue("@BranchId",        cboBranch.SelectedValue);
                            cmd.Parameters.AddWithValue("@DeptId",          cboDepartment.SelectedValue);
                            cmd.Parameters.AddWithValue("@CreatedBy",       AppSession.CurrentUserId);
                            cmd.Parameters.AddWithValue("@Active",          chkActive.Checked);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        MessageBox.Show("Employee created successfully.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save employee: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        // ── Email saving helpers ──────────────────────────────────────────────
        private async Task SaveEmailChangesAsync(SqlConnection con)
        {
            string origDept     = (_employee.DepartmentEmail  == "(None)" || _employee.DepartmentEmail  == null) ? "" : _employee.DepartmentEmail;
            string origBranch   = (_employee.BranchEmail      == "(None)" || _employee.BranchEmail      == null) ? "" : _employee.BranchEmail;
            string origPersonal = (_employee.PrimaryEmail     == "(None)" || _employee.PrimaryEmail     == null) ? "" : _employee.PrimaryEmail;

            string newDept     = txtDeptEmail.Text.Trim();
            string newBranch   = txtBranchEmail.Text.Trim();
            string newPersonal = txtPersonalEmail.Text.Trim();

            // Dept Email
            if (!string.Equals(newDept, origDept, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(newDept))
                {
                    using (var cmd = new SqlCommand("DELETE FROM dbo.DepartmentEmail WHERE CompanyName = @Company AND DepartmentName = @Dept", con))
                    {
                        cmd.Parameters.AddWithValue("@Company", _employee.CompanyName);
                        cmd.Parameters.AddWithValue("@Dept",    _employee.DepartmentName);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    int emailId = await GetOrCreateEmailIdAsync(con, newDept);
                    using (var cmd = new SqlCommand(@"
                        IF EXISTS (SELECT 1 FROM dbo.DepartmentEmail WHERE CompanyName = @Company AND DepartmentName = @Dept)
                            UPDATE dbo.DepartmentEmail SET EmailAddressId = @EmailId WHERE CompanyName = @Company AND DepartmentName = @Dept
                        ELSE
                            INSERT INTO dbo.DepartmentEmail (CompanyName, DepartmentName, EmailAddressId) VALUES (@Company, @Dept, @EmailId)", con))
                    {
                        cmd.Parameters.AddWithValue("@Company", _employee.CompanyName);
                        cmd.Parameters.AddWithValue("@Dept",    _employee.DepartmentName);
                        cmd.Parameters.AddWithValue("@EmailId", emailId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // Branch Email
            if (!string.Equals(newBranch, origBranch, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(newBranch))
                {
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.DepartmentAccount SET EmailAddressId = NULL WHERE CompanyName = @Company AND DepartmentName = @Dept AND BranchName = @Branch", con))
                    {
                        cmd.Parameters.AddWithValue("@Company", _employee.CompanyName);
                        cmd.Parameters.AddWithValue("@Dept",    _employee.DepartmentName);
                        cmd.Parameters.AddWithValue("@Branch",  _employee.BranchName);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    int emailId = await GetOrCreateEmailIdAsync(con, newBranch);
                    using (var cmd = new SqlCommand(@"
                        IF EXISTS (SELECT 1 FROM dbo.DepartmentAccount WHERE CompanyName = @Company AND DepartmentName = @Dept AND BranchName = @Branch)
                            UPDATE dbo.DepartmentAccount SET EmailAddressId = @EmailId WHERE CompanyName = @Company AND DepartmentName = @Dept AND BranchName = @Branch
                        ELSE
                            INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName, EmailAddressId) VALUES (@Company, @Dept, @Branch, @EmailId)", con))
                    {
                        cmd.Parameters.AddWithValue("@Company", _employee.CompanyName);
                        cmd.Parameters.AddWithValue("@Dept",    _employee.DepartmentName);
                        cmd.Parameters.AddWithValue("@Branch",  _employee.BranchName);
                        cmd.Parameters.AddWithValue("@EmailId", emailId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // Personal Email
            if (!string.Equals(newPersonal, origPersonal, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(newPersonal))
                {
                    using (var cmd = new SqlCommand("DELETE FROM dbo.EmployeeEmail WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", _employee.EmpId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    int emailId = await GetOrCreateEmailIdAsync(con, newPersonal);

                    using (var cmd = new SqlCommand("UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", _employee.EmpId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    int count;
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId",   _employee.EmpId);
                        cmd.Parameters.AddWithValue("@EmailId", emailId);
                        count = (int)await cmd.ExecuteScalarAsync();
                    }

                    if (count > 0)
                    {
                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.EmployeeEmail SET IsPrimary = 1, IsActive = 1 WHERE EmpId = @EmpId AND EmailId = @EmailId", con))
                        {
                            cmd.Parameters.AddWithValue("@EmpId",   _employee.EmpId);
                            cmd.Parameters.AddWithValue("@EmailId", emailId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        using (var cmd = new SqlCommand(
                            "INSERT INTO dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive) VALUES (@EmpId, @EmailId, 'Work', 1, 1)", con))
                        {
                            cmd.Parameters.AddWithValue("@EmpId",   _employee.EmpId);
                            cmd.Parameters.AddWithValue("@EmailId", emailId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        private static async Task<int> GetOrCreateEmailIdAsync(SqlConnection con, string email)
        {
            const string sql = @"
                UPDATE dbo.EmailAddress SET IsActive = 1 WHERE EmailAddress = @Email AND IsActive = 0;
                IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
                    INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive) VALUES (@Email, @Email, 1);
                SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Email", email);
                return (int)await cmd.ExecuteScalarAsync();
            }
        }

        private class CompanyLookup    { public int ComId    { get; set; } public string Name { get; set; } }
        private class BranchLookup    { public int BranchId  { get; set; } public string Name { get; set; } }
        private class DepartmentLookup { public int DeptId   { get; set; } public string Name { get; set; } }
    }
}
