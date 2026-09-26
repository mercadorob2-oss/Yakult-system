using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Department;
using Yakult.Inventory.App.Session;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.AccountManagement
{
    public partial class DepartmentAccountsPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView _dgv;
        private DataGridViewButtonColumn _colCreate;
        private DataGridViewButtonColumn _colChangePwd;
        private DataGridViewButtonColumn _colShowPwd;
        private DataGridViewButtonColumn _colEdit;
        private DataGridViewButtonColumn _colDelete;
        private System.Windows.Forms.Button _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;
        private Label _lblPageInfo;

        private List<DepartmentAccountRowDto> _allRows;
        private List<DepartmentAccountRowDto> _filteredRows;
        private readonly string _connectionString;
        private int _currentPage = 1;
        private const int PageSize = 15;

        // ── Sort state ───────────────────────────────────────────────────────────────
        private string _sortColumnName = null;
        private bool _sortAscending = true;

        // ── Column value filters (Excel-style) ───────────────────────────────────────
        private Dictionary<string, HashSet<string>> _columnFilters = new Dictionary<string, HashSet<string>>();

        // ── Inline email editing ─────────────────────────────────────────────────────
        private const int DeptEmailColIdx   = 3;
        private const int BranchEmailColIdx = 4;

        private bool   _isSaving;
        private string _editOriginalValue  = string.Empty;
        private int    _editingColumnIndex = -1;

        private List<string>            _emailCache = new List<string>();
        private Dictionary<string, int> _emailIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private Label                      _lblStatus;
        private System.Windows.Forms.Timer _statusTimer;

        // ── Constructor ─────────────────────────────────────────────────────────────
        public DepartmentAccountsPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        // ── UI Construction ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Department Accounts",
                "Search by Company, Department, Branch, or Email...",
                (s, e) => ApplyFilters(),
                () =>
                {
                    _columnFilters.Clear();
                    if (_dgv != null)
                    {
                        foreach (DataGridViewColumn c in _dgv.Columns)
                            c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                        _dgv.Invalidate();
                    }
                    LoadData();
                });

            _layout.SortByComboBox.Visible = false;
            _layout.SortByLabel.Visible    = false;

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[]
                {
                    "All", "Active", "Needs Password", "Inactive",
                    "Has Branch Email", "No Branch Email", "No Email (Both Empty)"
                });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilters();
            }

            // ── Status bar ───────────────────────────────────────────────────────────
            _lblStatus = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(39, 174, 96),
                BackColor = Color.White,
                Padding   = new Padding(18, 0, 0, 0),
                Text      = string.Empty
            };
            _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); _lblStatus.Text = string.Empty; };

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;

            if (_layout.ButtonBarPanel != null)
                _layout.ButtonBarPanel.Visible = true;

            var btnAddDept = new HopeButton
            {
                Text   = "\u2795  Add Department",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(200, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(btnAddDept, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            btnAddDept.Click += (s, e) =>
            {
                using (var dialog = new AddDepartmentDialog())
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                        LoadData();
                }
            };
            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { btnAddDept });

            // ── DataGridView ─────────────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
            };
            UiFactory.StyleGrid(_dgv);
            _dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgv.AutoSizeRowsMode    = DataGridViewAutoSizeRowsMode.AllCells;
            _dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Text columns
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Company",
                HeaderText = "Company",
                DataPropertyName = "CompanyName",
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Department",
                HeaderText = "Department",
                DataPropertyName = "DepartmentName",
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Branch",
                HeaderText = "Branch",
                DataPropertyName = "BranchName",
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            // Department Email (parent) — editable inline, shared by all branches in a dept
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colDeptEmail",
                HeaderText       = "Dept Email  ✎",
                DataPropertyName = "DepartmentEmail",
                MinimumWidth     = 190,
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.AllCells,
                ReadOnly         = false,
                SortMode         = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(22, 100, 170) }
            });
            // Branch Email (child) — editable inline, overrides dept email for this branch
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colBranchEmail",
                HeaderText       = "Branch Email  ✎",
                DataPropertyName = "BranchEmail",
                MinimumWidth     = 190,
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.AllCells,
                ReadOnly         = false,
                SortMode         = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(22, 100, 170) }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Username",
                HeaderText = "Username",
                DataPropertyName = "Username",
                MinimumWidth = 130,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                DataPropertyName = "AccountStatus",
                MinimumWidth = 100,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });

            // Action button columns
            _colCreate = new DataGridViewButtonColumn
            {
                Name = "ColCreate",
                HeaderText = "Actions",
                Text = "Create Account",
                UseColumnTextForButtonValue = true,
                Width = 130,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            _dgv.Columns.Add(_colCreate);

            _colChangePwd = new DataGridViewButtonColumn
            {
                Name = "ColChangePwd",
                HeaderText = "",
                Text = "Change Password",
                UseColumnTextForButtonValue = true,
                Width = 150,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            _dgv.Columns.Add(_colChangePwd);

            _colShowPwd = new DataGridViewButtonColumn
            {
                Name = "ColShowPwd",
                HeaderText = "",
                Text = "Show Password",
                UseColumnTextForButtonValue = true,
                Width = 130,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            _dgv.Columns.Add(_colShowPwd);

            _colEdit = new DataGridViewButtonColumn
            {
                Name = "ColEdit",
                HeaderText = "",
                Text = "Edit",
                UseColumnTextForButtonValue = true,
                Width = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            _dgv.Columns.Add(_colEdit);

            _colDelete = new DataGridViewButtonColumn
            {
                Name = "ColDelete",
                HeaderText = "",
                Text = "Delete",
                UseColumnTextForButtonValue = true,
                Width = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            _dgv.Columns.Add(_colDelete);

            _dgv.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == DeptEmailColIdx)
                    e.ToolTipText = "Department email — shared by all branches in this department";
                else if (e.ColumnIndex == BranchEmailColIdx)
                    e.ToolTipText = "Branch email — overrides the department email for this branch";
            };
            _dgv.CellBeginEdit         += DgvCellBeginEdit;
            _dgv.EditingControlShowing += DgvEditingControlShowing;
            _dgv.CellEndEdit           += DgvCellEndEdit;
            _dgv.CellPainting          += DgvCellPainting;
            _dgv.CellMouseUp           += DgvCellMouseUp;
            _dgv.ColumnHeaderMouseClick += DgvColumnHeaderMouseClick;

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgv);
            }

            // ── Pagination ───────────────────────────────────────────────────────────
            _btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0, Top = 5 };
            _btnPrevPage = new System.Windows.Forms.Button { Text = "<", Width = 45, Height = 25, Left = 50, Top = 5 };
            _lblPageInfo = new Label { AutoSize = false, Width = 300, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            _btnNextPage = new System.Windows.Forms.Button { Text = ">", Width = 45, Height = 25, Left = 410, Top = 5 };
            _btnLastPage = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 460, Top = 5 };

            _btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdateDataGridView(); };
            _btnPrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdateDataGridView(); } };
            _btnNextPage.Click += (s, e) =>
            {
                int total = TotalPages();
                if (_currentPage < total) { _currentPage++; UpdateDataGridView(); }
            };
            _btnLastPage.Click += (s, e) =>
            {
                _currentPage = TotalPages();
                UpdateDataGridView();
            };

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[] { _btnFirstPage, _btnPrevPage, _lblPageInfo, _btnNextPage, _btnLastPage });
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_lblStatus);
            Controls.Add(_layout.HeaderPanel);
            ResumeLayout(false);
        }

        // ── Data Loading ─────────────────────────────────────────────────────────────

        private async void LoadData()
        {
            try
            {

                _allRows = new List<DepartmentAccountRowDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        WITH Combinations AS (
                            -- Combinations derived from active employees
                            SELECT DISTINCT
                                c.ComId, c.Name AS CompanyName, c.Acronym AS CompanyAcronym,
                                d.DeptId, d.Name AS DepartmentName, d.Acronym AS DeptAcronym,
                                b.BranchId, b.Name AS BranchName, b.Acronym AS BranchAcronym
                            FROM dbo.Employee e
                            INNER JOIN dbo.Company    c ON e.ComId    = c.ComId
                            INNER JOIN dbo.Department d ON e.DeptId   = d.DeptId
                            INNER JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                            WHERE e.Active = 1

                            UNION

                            -- Combinations from BranchDepartmentCompany (normalized hierarchy)
                            SELECT DISTINCT
                                c.ComId, c.Name AS CompanyName, c.Acronym AS CompanyAcronym,
                                d.DeptId, d.Name AS DepartmentName, d.Acronym AS DeptAcronym,
                                b.BranchId, b.Name AS BranchName, b.Acronym AS BranchAcronym
                            FROM dbo.BranchDepartmentCompany bdc
                            INNER JOIN dbo.Company    c ON bdc.CompanyID    = c.ComId
                            INNER JOIN dbo.Branch     b ON bdc.BranchID     = b.BranchId
                            INNER JOIN dbo.Department d ON bdc.DepartmentID = d.DeptId
                            WHERE b.Active = 1
                              AND d.Active = 1
                        )
                        SELECT
                            comb.CompanyName,
                            comb.DepartmentName,
                            comb.BranchName,
                            comb.CompanyAcronym,
                            comb.DeptAcronym,
                            comb.BranchAcronym,
                            da.Id           AS AccountId,
                            da.Username,
                            ISNULL(da.IsActive, 0)                              AS AccountIsActive,
                            da.DateCreated                                       AS AccountDateCreated,
                            CASE WHEN da.PasswordHash IS NOT NULL THEN 1 ELSE 0 END AS HasPassword,
                            de.EmailAddressId                                   AS DeptEmailAddressId,
                            dea.EmailAddress                                    AS DepartmentEmail,
                            da.EmailAddressId                                   AS BranchEmailAddressId,
                            bea.EmailAddress                                    AS BranchEmail
                        FROM Combinations comb
                        LEFT JOIN dbo.DepartmentAccount da
                               ON  da.CompanyName    = comb.CompanyName
                               AND da.DepartmentName = comb.DepartmentName
                               AND da.BranchName     = comb.BranchName
                        LEFT JOIN dbo.DepartmentEmail de
                               ON  de.CompanyName    = comb.CompanyName
                               AND de.DepartmentName = comb.DepartmentName
                        LEFT JOIN dbo.EmailAddress dea ON dea.EmailId = de.EmailAddressId
                        LEFT JOIN dbo.EmailAddress bea ON bea.EmailId = da.EmailAddressId
                        ORDER BY comb.CompanyName, comb.DepartmentName, comb.BranchName";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _allRows.Add(new DepartmentAccountRowDto
                            {
                                CompanyName        = reader.GetString(0),
                                DepartmentName     = reader.GetString(1),
                                BranchName         = reader.GetString(2),
                                CompanyAcronym     = reader.IsDBNull(3)  ? null            : reader.GetString(3),
                                DeptAcronym        = reader.IsDBNull(4)  ? null            : reader.GetString(4),
                                BranchAcronym      = reader.IsDBNull(5)  ? null            : reader.GetString(5),
                                AccountId          = reader.IsDBNull(6)  ? (int?)null      : reader.GetInt32(6),
                                Username           = reader.IsDBNull(7)  ? null            : reader.GetString(7),
                                AccountIsActive    = !reader.IsDBNull(8) && reader.GetBoolean(8),
                                AccountDateCreated = reader.IsDBNull(9)  ? (DateTime?)null : reader.GetDateTime(9),
                                HasPassword        = !reader.IsDBNull(10) && reader.GetInt32(10) == 1,
                                DeptEmailAddressId = reader.IsDBNull(11) ? (int?)null      : reader.GetInt32(11),
                                DepartmentEmail    = reader.IsDBNull(12) ? "(None)"        : reader.GetString(12),
                                EmailAddressId     = reader.IsDBNull(13) ? (int?)null      : reader.GetInt32(13),
                                BranchEmail        = reader.IsDBNull(14) ? "(None)"        : reader.GetString(14)
                            });
                        }
                    }

                    // Load email cache for autocomplete
                    const string emailSql = @"
                        SELECT EmailId, EmailAddress FROM dbo.EmailAddress
                        WHERE IsActive = 1 ORDER BY EmailAddress";

                    _emailCache = new List<string>();
                    _emailIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    using (var cmd2 = new SqlCommand(emailSql, con))
                    using (var er = await cmd2.ExecuteReaderAsync())
                    {
                        while (await er.ReadAsync())
                        {
                            int    id   = er.GetInt32(0);
                            string addr = er.GetString(1);
                            _emailCache.Add(addr);
                            _emailIdMap[addr] = id;
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load department accounts:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Inline Email Editing ──────────────────────────────────────────────────────

        private void DgvCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex != DeptEmailColIdx && e.ColumnIndex != BranchEmailColIdx)
            {
                e.Cancel = true;
                return;
            }
            _editingColumnIndex = e.ColumnIndex;
            string raw = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            _editOriginalValue = raw.Equals("(None)", StringComparison.OrdinalIgnoreCase) ? string.Empty : raw;
        }

        private void DgvEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            int col = _dgv.CurrentCell?.ColumnIndex ?? -1;
            if (col != DeptEmailColIdx && col != BranchEmailColIdx) return;
            if (!(e.Control is TextBox tb)) return;

            tb.TextChanged -= EmailEditor_TextChanged;
            tb.AutoCompleteMode = AutoCompleteMode.None;

            var source = new AutoCompleteStringCollection();
            if (_emailCache.Count > 0) source.AddRange(_emailCache.ToArray());
            tb.AutoCompleteCustomSource = source;
            tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;

            if (tb.Text.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                tb.Text = string.Empty;

            tb.TextChanged += EmailEditor_TextChanged;
        }

        private void EmailEditor_TextChanged(object sender, EventArgs e) { }

        private void DgvCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if ((e.ColumnIndex != DeptEmailColIdx && e.ColumnIndex != BranchEmailColIdx) || _isSaving)
                return;

            var row = _dgv.Rows[e.RowIndex].DataBoundItem as DepartmentAccountRowDto;
            if (row == null) return;

            string newValue = (_dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty).Trim();
            if (newValue.Equals("(None)", StringComparison.OrdinalIgnoreCase)) newValue = string.Empty;

            if (string.Equals(newValue, _editOriginalValue, StringComparison.OrdinalIgnoreCase)) return;

            if (e.ColumnIndex == DeptEmailColIdx)
                _ = CommitDeptEmailChangeAsync(e.RowIndex, row, newValue);
            else
                _ = CommitBranchEmailChangeAsync(e.RowIndex, row, newValue);
        }

        // ── Commit: Department Email (parent) ─────────────────────────────────────────
        private async System.Threading.Tasks.Task CommitDeptEmailChangeAsync(int rowIndex, DepartmentAccountRowDto row, string newEmail)
        {
            _isSaving = true;
            try
            {
                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    var confirm = MessageBox.Show(
                        $"Remove the department email for:\n\n  {row.CompanyName}  /  {row.DepartmentName}\n\nThis affects all branches in this department.",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertEmailCell(rowIndex, DeptEmailColIdx); return; }

                    await DeleteDeptEmailAsync(row.CompanyName, row.DepartmentName);
                    PropagateParentEmail(row.CompanyName, row.DepartmentName, null, "(None)");
                    ShowStatus($"Department email removed for \"{row.DepartmentName}\".", success: false);
                    FlashEmailCell(rowIndex, DeptEmailColIdx, Color.FromArgb(255, 210, 210));
                    return;
                }

                if (!IsValidEmailFormat(newEmail)) { ShowEmailValidationError(rowIndex, DeptEmailColIdx); return; }

                int emailId = await ResolveOrCreateEmailAsync(newEmail, rowIndex, DeptEmailColIdx);
                if (emailId < 0) return;

                int branches = _allRows.Count(r => r.CompanyName == row.CompanyName && r.DepartmentName == row.DepartmentName);
                if (branches > 1)
                {
                    var confirm = MessageBox.Show(
                        $"This email will apply to all {branches} branches of:\n\n  {row.CompanyName}  /  {row.DepartmentName}\n\nContinue?",
                        "Confirm Department Email", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertEmailCell(rowIndex, DeptEmailColIdx); return; }
                }

                await UpsertDeptEmailAsync(row.CompanyName, row.DepartmentName, emailId);
                PropagateParentEmail(row.CompanyName, row.DepartmentName, emailId, newEmail);
                ShowStatus($"Department email updated for \"{row.DepartmentName}\" ({branches} branch{(branches == 1 ? "" : "es")}).");
                FlashEmailCell(rowIndex, DeptEmailColIdx, Color.FromArgb(195, 240, 210));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving department email:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RevertEmailCell(rowIndex, DeptEmailColIdx);
            }
            finally { _isSaving = false; }
        }

        // ── Commit: Branch Email (child) ──────────────────────────────────────────────
        private async System.Threading.Tasks.Task CommitBranchEmailChangeAsync(int rowIndex, DepartmentAccountRowDto row, string newEmail)
        {
            _isSaving = true;
            try
            {
                if (!row.AccountId.HasValue)
                {
                    // No placeholder row yet — insert one silently (same as what the seed script does
                    // for existing departments). The user can create credentials later via Create Account.
                    try
                    {
                        const string insertPlaceholderSql = @"
                            INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName)
                            OUTPUT INSERTED.Id
                            VALUES (@Company, @Dept, @Branch)";
                        using (var con = new SqlConnection(_connectionString))
                        {
                            await con.OpenAsync();
                            using (var cmd = new SqlCommand(insertPlaceholderSql, con))
                            {
                                cmd.Parameters.AddWithValue("@Company", row.CompanyName);
                                cmd.Parameters.AddWithValue("@Dept",    row.DepartmentName);
                                cmd.Parameters.AddWithValue("@Branch",  row.BranchName);
                                row.AccountId = (int)await cmd.ExecuteScalarAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving branch email:\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        RevertEmailCell(rowIndex, BranchEmailColIdx);
                        return;
                    }
                    // row.AccountId is now set — fall through to save the email
                }

                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    var confirm = MessageBox.Show(
                        $"Remove the branch email for:\n\n  {row.CompanyName}  /  {row.DepartmentName}  /  {row.BranchName}",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) { RevertEmailCell(rowIndex, BranchEmailColIdx); return; }

                    await SaveBranchEmailAsync(row.AccountId.Value, null);
                    row.EmailAddressId = null;
                    row.BranchEmail = "(None)";
                    _dgv.Rows[rowIndex].Cells[BranchEmailColIdx].Value = "(None)";
                    ShowStatus($"Branch email removed from \"{row.BranchName}\".", success: false);
                    FlashEmailCell(rowIndex, BranchEmailColIdx, Color.FromArgb(255, 210, 210));
                    return;
                }

                if (!IsValidEmailFormat(newEmail)) { ShowEmailValidationError(rowIndex, BranchEmailColIdx); return; }

                int emailId = await ResolveOrCreateEmailAsync(newEmail, rowIndex, BranchEmailColIdx);
                if (emailId < 0) return;

                await SaveBranchEmailAsync(row.AccountId.Value, emailId);
                row.EmailAddressId = emailId;
                row.BranchEmail = newEmail;
                ShowStatus($"Branch email updated for \"{row.BranchName}\".");
                FlashEmailCell(rowIndex, BranchEmailColIdx, Color.FromArgb(195, 240, 210));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving branch email:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RevertEmailCell(rowIndex, BranchEmailColIdx);
            }
            finally { _isSaving = false; }
        }

        // ── Email Helpers ─────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task<int> ResolveOrCreateEmailAsync(string email, int rowIndex, int colIdx)
        {
            if (_emailIdMap.TryGetValue(email, out int existing)) return existing;

            var confirm = MessageBox.Show(
                $"\"{email}\" does not exist in the email list.\n\nInsert it now?",
                "New Email Address", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) { RevertEmailCell(rowIndex, colIdx); return -1; }

            int newId = await InsertEmailAddressAsync(email);
            _emailIdMap[email] = newId;
            _emailCache.Add(email);
            return newId;
        }

        private void PropagateParentEmail(string company, string department, int? emailId, string emailText)
        {
            foreach (var r in _allRows)
            {
                if (r.CompanyName != company || r.DepartmentName != department) continue;
                r.DeptEmailAddressId = emailId;
                r.DepartmentEmail    = emailText;
            }
            _dgv.Invalidate();
        }

        private void RevertEmailCell(int rowIndex, int colIdx)
        {
            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count) return;
            _dgv.Rows[rowIndex].Cells[colIdx].Value =
                string.IsNullOrEmpty(_editOriginalValue) ? "(None)" : _editOriginalValue;
        }

        private void FlashEmailCell(int rowIndex, int colIdx, Color flashColor)
        {
            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count) return;
            var cell = _dgv.Rows[rowIndex].Cells[colIdx];
            cell.Style.BackColor = flashColor;
            var t = new System.Windows.Forms.Timer { Interval = 1600 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (rowIndex < _dgv.Rows.Count) _dgv.Rows[rowIndex].Cells[colIdx].Style.BackColor = Color.Empty; };
            t.Start();
        }

        private void ShowEmailValidationError(int rowIndex, int colIdx)
        {
            MessageBox.Show("Please enter a valid email address (e.g. name@domain.com).",
                "Invalid Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RevertEmailCell(rowIndex, colIdx);
        }

        private void ShowStatus(string message, bool success = true)
        {
            _lblStatus.Text      = message;
            _lblStatus.ForeColor = success ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private static bool IsValidEmailFormat(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            int at  = email.IndexOf('@');
            if (at <= 0 || at == email.Length - 1) return false;
            int dot = email.LastIndexOf('.');
            return dot > at + 1 && dot < email.Length - 1;
        }

        // ── Email DB Operations ───────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task UpsertDeptEmailAsync(string company, string department, int emailId)
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

        private async System.Threading.Tasks.Task DeleteDeptEmailAsync(string company, string department)
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

        private async System.Threading.Tasks.Task SaveBranchEmailAsync(int deptAccountId, int? emailId)
        {
            const string sql = "UPDATE dbo.DepartmentAccount SET EmailAddressId = @EmailAddressId WHERE Id = @Id";
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", deptAccountId);
                    cmd.Parameters.Add(new SqlParameter("@EmailAddressId", System.Data.SqlDbType.Int)
                    {
                        Value = emailId.HasValue ? (object)emailId.Value : DBNull.Value
                    });
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task<int> InsertEmailAddressAsync(string email)
        {
            // Reactivate if it exists but is inactive; insert only if it truly doesn't exist;
            // then return the ID regardless of which path was taken — no UNIQUE KEY violations.
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
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        // ── Column Header Sort ────────────────────────────────────────────────────────
        private void DgvColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            // Click in the filter ▼ icon zone (rightmost 18 px) → show filter popup
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
                _sortAscending = true;
            }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAscending
                ? System.Windows.Forms.SortOrder.Ascending
                : System.Windows.Forms.SortOrder.Descending;

            _currentPage = 1;
            ApplySortToFiltered();
            UpdateDataGridView();
        }

        private void ShowColumnFilterPopup(DataGridViewColumn col)
        {
            if (_allRows == null) return;

            Func<DepartmentAccountRowDto, string> getter;
            switch (col.Name)
            {
                case "Company":    getter = r => r.CompanyName    ?? ""; break;
                case "Department": getter = r => r.DepartmentName ?? ""; break;
                case "Branch":     getter = r => r.BranchName     ?? ""; break;
                case "Username":   getter = r => r.Username       ?? ""; break;
                case "Status":     getter = r => r.AccountStatus  ?? ""; break;
                default: return;
            }

            var distinctValues = _allRows
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _columnFilters.TryGetValue(col.Name, out var currentFilter);

            var headerCell  = _dgv.GetCellDisplayRectangle(col.Index, -1, false);
            var screenBelow = _dgv.PointToScreen(new Point(headerCell.Left, headerCell.Bottom));
            var screenAbove = _dgv.PointToScreen(new Point(headerCell.Left, headerCell.Top));

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
                    _sortAscending = true;
                    foreach (DataGridViewColumn c in _dgv.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    _currentPage = 1;
                    ApplySortToFiltered();
                    UpdateDataGridView();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.SortDescending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending = false;
                    foreach (DataGridViewColumn c in _dgv.Columns)
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

        private void ApplySortToFiltered()
        {
            if (_filteredRows == null || _sortColumnName == null) return;

            Func<DepartmentAccountRowDto, string> key;
            switch (_sortColumnName)
            {
                case "Company":      key = r => r.CompanyName    ?? ""; break;
                case "Department":   key = r => r.DepartmentName ?? ""; break;
                case "Branch":       key = r => r.BranchName     ?? ""; break;
                // EmailAddress column removed — email cols are NotSortable
                case "Username":     key = r => r.Username       ?? ""; break;
                case "Status":       key = r => r.AccountStatus  ?? ""; break;
                default: return;
            }

            _filteredRows = _sortAscending
                ? _filteredRows.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                : _filteredRows.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void ApplyFilters()
        {
            if (_allRows == null)
            {
                _filteredRows = new List<DepartmentAccountRowDto>();
                UpdateDataGridView();
                return;
            }

            var search = _layout.SearchBox?.Text?.Trim().ToLower() ?? "";
            var filter = _layout.FilterByComboBox?.SelectedItem?.ToString() ?? "All";

            _filteredRows = _allRows.Where(r =>
            {
                if (!string.IsNullOrEmpty(search))
                {
                    bool hit = r.CompanyName.ToLower().Contains(search)
                        || r.DepartmentName.ToLower().Contains(search)
                        || r.BranchName.ToLower().Contains(search)
                        || (r.Username        ?? "").ToLower().Contains(search)
                        || (r.DepartmentEmail ?? "").ToLower().Contains(search)
                        || (r.BranchEmail     ?? "").ToLower().Contains(search);
                    if (!hit) return false;
                }

                switch (filter)
                {
                    case "Active":              return r.HasAccount && r.HasPassword && r.AccountIsActive;
                    case "Needs Password":      return r.HasAccount && !r.HasPassword;
                    case "Inactive":            return r.HasAccount && !r.AccountIsActive;
                    case "Has Branch Email":    return r.EmailAddressId.HasValue;
                    case "No Branch Email":     return !r.EmailAddressId.HasValue;
                    case "No Email (Both Empty)": return !r.EmailAddressId.HasValue && !r.DeptEmailAddressId.HasValue;
                    default:                    return true;
                }
            }).ToList();

            // Apply per-column value filters (Excel-style)
            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                switch (kvp.Key)
                {
                    case "Company":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.CompanyName    ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Department":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.DepartmentName ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Branch":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.BranchName     ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    // email columns are NotSortable — no column filter popup for them
                    case "Username":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.Username       ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Status":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.AccountStatus  ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                }
            }

            ApplySortToFiltered();
            _currentPage = 1;
            UpdateDataGridView();
        }

        private void UpdateDataGridView()
        {
            if (_filteredRows == null)
            {
                _dgv.DataSource = null;
                return;
            }

            var paged = _filteredRows
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _dgv.DataSource = paged;
            _dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _dgv.Invalidate();

            int total = TotalPages();
            if (_lblPageInfo != null)
                _lblPageInfo.Text = total == 0
                    ? "Page 0 of 0 (0 rows)"
                    : $"Page {_currentPage} of {total} ({_filteredRows.Count} rows)";

            if (_btnFirstPage != null) _btnFirstPage.Enabled = _currentPage > 1;
            if (_btnPrevPage != null) _btnPrevPage.Enabled = _currentPage > 1;
            if (_btnNextPage != null) _btnNextPage.Enabled = _currentPage < total;
            if (_btnLastPage != null) _btnLastPage.Enabled = _currentPage < total;
        }

        private int TotalPages()
        {
            int count = _filteredRows?.Count ?? 0;
            return count == 0 ? 1 : (int)Math.Ceiling(count / (double)PageSize);
        }

        // ── Cell Painting (headers + action buttons) ────────────────────────────────
        private void DgvCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // ── Header rows: paint background + sort arrows ──────────────────────────
            if (e.RowIndex < 0)
            {
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
                    e.Graphics.DrawLine(pen,
                        e.CellBounds.Right - 1, e.CellBounds.Top,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                    e.Graphics.DrawLine(pen,
                        e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                using (var textBrush = new SolidBrush(Color.White))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var textRect = e.CellBounds;
                    bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;

                    if (isSortable)
                    {
                        // Reserve space for sort arrow (14 px) + filter icon (14 px) + gaps
                        textRect.Width -= 34;

                        // Sort arrow — shifted left to make room for filter ▼ icon
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

                        // Filter ▼ icon — rightmost 14 px; yellow when filter is active
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
                            ? Color.FromArgb(255, 230, 80)           // yellow = active filter
                            : Color.FromArgb(140, 255, 255, 255)))   // dim = no filter
                        {
                            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                            e.Graphics.FillPolygon(filterBrush, filterPts);
                            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
                        }
                    }

                    e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgv.Font, textBrush, textRect, fmt);
                }
                return;
            }

            // ── Data rows: paint action button columns only ──────────────────────────
            if (_colChangePwd == null || _colShowPwd == null || _colEdit == null || _colDelete == null) return;
            if (e.ColumnIndex != _colCreate.Index &&
                e.ColumnIndex != _colChangePwd.Index &&
                e.ColumnIndex != _colShowPwd.Index &&
                e.ColumnIndex != _colEdit.Index &&
                e.ColumnIndex != _colDelete.Index) return;

            var row = _dgv.Rows[e.RowIndex].DataBoundItem as DepartmentAccountRowDto;
            bool hasAccount = row?.HasAccount ?? false;
            bool needsPassword = hasAccount && !(row?.HasPassword ?? false);

            bool isEnabled;
            string btnText;
            Color backColor, foreColor;

            if (e.ColumnIndex == _colCreate.Index)
            {
                isEnabled = !hasAccount;
                btnText = hasAccount ? "✓ Created" : "Create Account";
                backColor = hasAccount
                    ? Color.FromArgb(210, 210, 210)
                    : Color.FromArgb(0, 120, 215);
                foreColor = hasAccount ? Color.DimGray : Color.White;
            }
            else if (e.ColumnIndex == _colChangePwd.Index)
            {
                isEnabled = hasAccount;
                btnText = needsPassword ? "Set Password" : "Change Password";
                backColor = isEnabled
                    ? (needsPassword ? Color.FromArgb(220, 100, 0) : Color.FromArgb(255, 140, 0))
                    : Color.FromArgb(210, 210, 210);
                foreColor = isEnabled ? Color.White : Color.DimGray;
            }
            else if (e.ColumnIndex == _colShowPwd.Index)
            {
                isEnabled = hasAccount && (row?.HasPassword ?? false);
                btnText = "Show Password";
                backColor = isEnabled ? Color.FromArgb(70, 130, 180) : Color.FromArgb(210, 210, 210);
                foreColor = isEnabled ? Color.White : Color.DimGray;
            }
            else if (e.ColumnIndex == _colEdit.Index)
            {
                isEnabled = hasAccount;
                btnText = "Edit";
                backColor = isEnabled ? Color.FromArgb(40, 167, 69) : Color.FromArgb(210, 210, 210);
                foreColor = isEnabled ? Color.White : Color.DimGray;
            }
            else
            {
                isEnabled = hasAccount;
                btnText = "Delete";
                backColor = isEnabled ? Color.FromArgb(220, 53, 69) : Color.FromArgb(210, 210, 210);
                foreColor = isEnabled ? Color.White : Color.DimGray;
            }

            // Cell background
            e.Graphics.FillRectangle(SystemBrushes.Control, e.CellBounds);

            // Draw button rect
            var btnRect = new Rectangle(
                e.CellBounds.Left + 3,
                e.CellBounds.Top + 4,
                e.CellBounds.Width - 6,
                e.CellBounds.Height - 8);

            using (var brush = new SolidBrush(backColor))
                e.Graphics.FillRectangle(brush, btnRect);

            // Button text
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            using (var textBrush = new SolidBrush(foreColor))
            {
                var font = e.CellStyle?.Font ?? _dgv.Font;
                e.Graphics.DrawString(btnText, font, textBrush, btnRect, sf);
            }

            // Cursor hint
            if (isEnabled)
                _dgv.Cursor = Cursors.Hand;

            e.Handled = true;
        }

        // ── Cell Click Handler ───────────────────────────────────────────────────────
        private void DgvCellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0) return;
            if (_colChangePwd == null || _colShowPwd == null || _colEdit == null || _colDelete == null) return;
            if (e.ColumnIndex != _colCreate.Index &&
                e.ColumnIndex != _colChangePwd.Index &&
                e.ColumnIndex != _colShowPwd.Index &&
                e.ColumnIndex != _colEdit.Index &&
                e.ColumnIndex != _colDelete.Index) return;

            var row = _dgv.Rows[e.RowIndex].DataBoundItem as DepartmentAccountRowDto;
            if (row == null) return;

            if (e.ColumnIndex == _colCreate.Index && !row.HasAccount)
                HandleCreateAccount(row);
            else if (e.ColumnIndex == _colChangePwd.Index && row.HasAccount)
                HandleChangePassword(row);
            else if (e.ColumnIndex == _colShowPwd.Index && row.HasAccount && row.HasPassword)
                HandleShowPassword(row);
            else if (e.ColumnIndex == _colEdit.Index && row.HasAccount)
                HandleEdit(row);
            else if (e.ColumnIndex == _colDelete.Index && row.HasAccount)
                HandleDeleteAccount(row);
        }

        // ── Action: Create Account ───────────────────────────────────────────────────
        private async void HandleCreateAccount(DepartmentAccountRowDto row)
        {
            string suggested = row.BuildSuggestedUsername();
            using (var dlg = new CreateDeptAccountDialog(row.CompanyName, row.DepartmentName, row.BranchName, suggested))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (!await TryCreateAccountAsync(row, dlg.Username, dlg.Password)) return;
                    MessageBox.Show("Department account created successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create account:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── Shared: create or fill-in a DepartmentAccount row ───────────────────────
        private async System.Threading.Tasks.Task<bool> TryCreateAccountAsync(DepartmentAccountRowDto row, string username, string password)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string checkSql = @"
                    SELECT COUNT(*) FROM dbo.DepartmentAccount WHERE Username = @Username
                    UNION ALL
                    SELECT COUNT(*) FROM dbo.[User]             WHERE Name     = @Username";
                using (var checkCmd = new SqlCommand(checkSql, con))
                {
                    checkCmd.Parameters.AddWithValue("@Username", username);
                    using (var r = await checkCmd.ExecuteReaderAsync())
                    {
                        int total = 0;
                        while (await r.ReadAsync()) total += r.GetInt32(0);
                        if (total > 0)
                        {
                            MessageBox.Show("That username already exists. Please choose a different one.",
                                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return false;
                        }
                    }
                }

                byte[] salt = PasswordHelper.GenerateSalt();
                byte[] hash = PasswordHelper.HashPassword(password, salt);

                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // 1. Create the dbo.[User] row
                        const string insertUserSql = @"
                            INSERT INTO dbo.[User] (Name, PasswordHash, PasswordSalt, IsActive, DateCreated)
                            VALUES (@Name, @Hash, @Salt, 1, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        int newUserId;
                        using (var cmd = new SqlCommand(insertUserSql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@Name", username);
                            cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                            cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                            newUserId = (int)await cmd.ExecuteScalarAsync();
                        }

                        // 2a. UPDATE existing placeholder row
                        if (row.AccountId.HasValue)
                        {
                            const string updateSql = @"
                                UPDATE dbo.DepartmentAccount
                                SET Username = @Username, PasswordHash = @Hash, PasswordSalt = @Salt,
                                    IsActive = 1, DateCreated = GETDATE(), UserId = @UserId,
                                    PlainPassword = @PlainPassword
                                WHERE Id = @Id";
                            using (var cmd = new SqlCommand(updateSql, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@Id",            row.AccountId.Value);
                                cmd.Parameters.AddWithValue("@Username",      username);
                                cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                                cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                                cmd.Parameters.AddWithValue("@UserId",        newUserId);
                                cmd.Parameters.AddWithValue("@PlainPassword", AesEncryptionHelper.Encrypt(password));
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            // 2b. INSERT new row — no placeholder exists for this department
                            const string insertSql = @"
                                INSERT INTO dbo.DepartmentAccount
                                    (CompanyName, DepartmentName, BranchName, Username, PasswordHash, PasswordSalt, IsActive, DateCreated, UserId, PlainPassword)
                                OUTPUT INSERTED.Id
                                VALUES (@Company, @Dept, @Branch, @Username, @Hash, @Salt, 1, GETDATE(), @UserId, @PlainPassword)";
                            using (var cmd = new SqlCommand(insertSql, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@Company",       row.CompanyName);
                                cmd.Parameters.AddWithValue("@Dept",          row.DepartmentName);
                                cmd.Parameters.AddWithValue("@Branch",        row.BranchName);
                                cmd.Parameters.AddWithValue("@Username",      username);
                                cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                                cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                                cmd.Parameters.AddWithValue("@UserId",        newUserId);
                                cmd.Parameters.AddWithValue("@PlainPassword", AesEncryptionHelper.Encrypt(password));
                                row.AccountId = (int)await cmd.ExecuteScalarAsync();
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        // ── Action: Change Password ──────────────────────────────────────────────────
        private async void HandleChangePassword(DepartmentAccountRowDto row)
        {
            using (var dlg = new ChangeDeptPasswordDialog(row.CompanyName, row.DepartmentName, row.BranchName))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    byte[] salt = PasswordHelper.GenerateSalt();
                    byte[] hash = PasswordHelper.HashPassword(dlg.NewPassword, salt);

                    using (var con = new SqlConnection(_connectionString))
                    {
                        await con.OpenAsync();

                        // Update DepartmentAccount credentials
                        const string sqlDept = @"
                            UPDATE dbo.DepartmentAccount
                            SET PasswordHash = @Hash, PasswordSalt = @Salt, PlainPassword = @PlainPassword
                            WHERE Id = @Id";

                        using (var cmd = new SqlCommand(sqlDept, con))
                        {
                            cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                            cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                            cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                            cmd.Parameters.AddWithValue("@PlainPassword", AesEncryptionHelper.Encrypt(dlg.NewPassword));
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Also update the linked dbo.[User] row so login still works
                        const string sqlUser = @"
                            UPDATE dbo.[User]
                            SET PasswordHash = @Hash, PasswordSalt = @Salt
                            WHERE UserId = (SELECT UserId FROM dbo.DepartmentAccount WHERE Id = @Id)";

                        using (var cmd = new SqlCommand(sqlUser, con))
                        {
                            cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                            cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                            cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    MessageBox.Show("Password changed successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to change password:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── Action: Show Password ────────────────────────────────────────────────────
        private async void HandleShowPassword(DepartmentAccountRowDto row)
        {
            using (var verifyDlg = new VerifyAdminPasswordDialog(AppSession.CurrentUserName))
            {
                if (verifyDlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    // Verify the current admin's own password against dbo.[User]
                    bool verified = false;
                    using (var con = new SqlConnection(_connectionString))
                    {
                        await con.OpenAsync();
                        const string verifySql = @"
                            SELECT PasswordHash, PasswordSalt
                            FROM dbo.[User]
                            WHERE UserId = @UserId";
                        using (var cmd = new SqlCommand(verifySql, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", AppSession.CurrentUserId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    int hashOrd = reader.GetOrdinal("PasswordHash");
                                    int saltOrd = reader.GetOrdinal("PasswordSalt");
                                    if (!reader.IsDBNull(hashOrd) && !reader.IsDBNull(saltOrd))
                                    {
                                        byte[] storedHash = (byte[])reader[hashOrd];
                                        byte[] storedSalt = (byte[])reader[saltOrd];
                                        verified = PasswordHelper.VerifyPassword(
                                            verifyDlg.EnteredPassword, storedHash, storedSalt);
                                    }
                                }
                            }
                        }
                    }

                    if (!verified)
                    {
                        MessageBox.Show("Incorrect password. Access denied.", "Authentication Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Fetch the department account's stored plaintext password
                    string plainPassword = null;
                    using (var con = new SqlConnection(_connectionString))
                    {
                        await con.OpenAsync();
                        const string pwdSql = "SELECT PlainPassword FROM dbo.DepartmentAccount WHERE Id = @Id";
                        using (var cmd = new SqlCommand(pwdSql, con))
                        {
                            cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                            var result = await cmd.ExecuteScalarAsync();
                            plainPassword = AesEncryptionHelper.Decrypt(result as string);
                        }
                    }

                    if (string.IsNullOrEmpty(plainPassword))
                    {
                        MessageBox.Show(
                            "No stored password is available for this account.\n\n" +
                            "This may happen for accounts created before the Show Password feature was added. " +
                            "Use Change Password to set a new one.",
                            "Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    using (var showDlg = new ShowDeptPasswordDialog(
                        row.CompanyName, row.DepartmentName, row.BranchName, row.Username, plainPassword))
                    {
                        showDlg.ShowDialog(this);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error retrieving password:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── Action: Edit ─────────────────────────────────────────────────────────────
        private async void HandleEdit(DepartmentAccountRowDto row)
        {
            // Load available email addresses for the dropdown
            var emails = new List<(int EmailId, string Label)>();
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string emailSql = @"
                        SELECT EmailId, EmailAddress, ISNULL(DisplayName, '')
                        FROM dbo.EmailAddress
                        WHERE IsActive = 1
                        ORDER BY DisplayName, EmailAddress";
                    using (var cmd = new SqlCommand(emailSql, con))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            int id = r.GetInt32(0);
                            string addr = r.GetString(1);
                            string disp = r.GetString(2);
                            string label = string.IsNullOrWhiteSpace(disp) ? addr : $"{disp} ({addr})";
                            emails.Add((id, label));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load email addresses:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var dlg = new EditDeptAccountDialog(row, emails))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    using (var con = new SqlConnection(_connectionString))
                    {
                        await con.OpenAsync();

                        const string sql = @"
                            UPDATE dbo.DepartmentAccount
                            SET Username = @Username, IsActive = @IsActive, EmailAddressId = @EmailAddressId
                            WHERE Id = @Id";

                        using (var cmd = new SqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                            cmd.Parameters.AddWithValue("@Username", dlg.Username);
                            cmd.Parameters.AddWithValue("@IsActive", dlg.IsActive);
                            cmd.Parameters.Add(new SqlParameter("@EmailAddressId", SqlDbType.Int)
                            {
                                Value = dlg.EmailAddressId.HasValue ? (object)dlg.EmailAddressId.Value : DBNull.Value
                            });
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    MessageBox.Show("Account updated successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update account:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── Action: Delete Account ───────────────────────────────────────────────────
        // Clears credentials only — the Company/Department/Branch row is kept.
        // Status reverts to "Needs Password" and a new password can be set via the button.
        private async void HandleDeleteAccount(DepartmentAccountRowDto row)
        {
            var confirm = MessageBox.Show(
                $"Delete the account credentials for:\n\n" +
                $"  Company:     {row.CompanyName}\n" +
                $"  Department:  {row.DepartmentName}\n" +
                $"  Branch:      {row.BranchName}\n" +
                $"  Username:    {row.Username}\n\n" +
                $"The entry will remain but credentials will be cleared.\n" +
                $"Use \"Set Password\" to restore access.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        UPDATE dbo.DepartmentAccount
                        SET PasswordHash = NULL,
                            PasswordSalt = NULL,
                            IsActive     = 0
                        WHERE Id = @Id";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show("Account credentials cleared. Status is now \"Needs Password\".", "Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete account:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ── DTO ───────────────────────────────────────────────────────────────────────────
    internal class DepartmentAccountRowDto
    {
        public string    CompanyName        { get; set; }
        public string    DepartmentName     { get; set; }
        public string    BranchName         { get; set; }
        public string    CompanyAcronym     { get; set; }
        public string    DeptAcronym        { get; set; }
        public string    BranchAcronym      { get; set; }
        public int?      AccountId          { get; set; }
        public string    Username           { get; set; }
        public bool      AccountIsActive    { get; set; }
        public DateTime? AccountDateCreated { get; set; }
        public bool      HasPassword        { get; set; }

        // Stable IDs of this Company / Department / Branch combination. Saved on
        // dbo.DepartmentAccount so the account survives a rename (login prefers them).
        public int       ComId              { get; set; }
        public int       DeptId             { get; set; }
        public int       BranchId           { get; set; }

        // Request Portal submissions for this department (see DepartmentRequestHistoryRepository).
        public int       DeptLevelRequestCount { get; set; }
        public int       EmployeeRequestCount  { get; set; }
        public int       TotalRequestCount     => DeptLevelRequestCount + EmployeeRequestCount;
        public string    RequestCountDisplay   => TotalRequestCount == 0
            ? "—"
            : DeptLevelRequestCount > 0 ? $"{TotalRequestCount} ({DeptLevelRequestCount} dept.)" : TotalRequestCount.ToString();

        // Returns e.g. "YPI_MKT_NCR" from acronyms, falling back to full names if acronym is null.
        public string BuildSuggestedUsername()
        {
            string co   = !string.IsNullOrWhiteSpace(CompanyAcronym)    ? CompanyAcronym    : CompanyName;
            string dept = !string.IsNullOrWhiteSpace(DeptAcronym)       ? DeptAcronym       : DepartmentName;
            string br   = !string.IsNullOrWhiteSpace(BranchAcronym)     ? BranchAcronym     : BranchName;
            return $"{co}_{dept}_{br}";
        }

        // Department-level email (parent — shared by all branches of a dept)
        public int?   DeptEmailAddressId { get; set; }
        public string DepartmentEmail    { get; set; }

        // Branch-level email (child — overrides dept email for this specific branch)
        public int?   EmailAddressId     { get; set; }   // kept as-is for Edit dialog compatibility
        public string BranchEmail        { get; set; }

        public bool HasAccount => !string.IsNullOrEmpty(Username);

        public string AccountStatus
        {
            get
            {
                if (!HasAccount) return "—";
                if (!HasPassword) return "Needs Password";
                return AccountIsActive ? "Active" : "Inactive";
            }
        }
    }

    // ── Dialog: Create Account ───────────────────────────────────────────────────────
    internal class CreateDeptAccountDialog : Form
    {
        private readonly TextBox _txtUsername;
        private readonly TextBox _txtPassword;
        private readonly TextBox _txtConfirm;

        public string Username => _txtUsername.Text.Trim();
        public string Password => _txtPassword.Text;

        public CreateDeptAccountDialog(string company, string department, string branch, string suggestedUsername = null)
        {
            Text = "Create Department Account";
            Size = new Size(460, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(0, 120, 215) };
            header.Controls.Add(new Label
            {
                Text = "Create Department Account",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(18, 14)
            });

            var lblInfo = new Label
            {
                Text = $"{company}  /  {department}  /  {branch}",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = false,
                Width = 420,
                Location = new Point(20, 70)
            };

            _txtUsername = AddRow("Username:", 108, false);
            if (!string.IsNullOrWhiteSpace(suggestedUsername))
                _txtUsername.Text = suggestedUsername;
            _txtPassword = AddRow("Password:", 148, true);
            _txtConfirm = AddRow("Confirm Password:", 188, true);

            var chkShowCreate = new CheckBox
            {
                Text = "Show Password",
                Location = new Point(158, 222),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            chkShowCreate.CheckedChanged += (s, e) =>
            {
                _txtPassword.PasswordChar = chkShowCreate.Checked ? '\0' : '*';
                _txtConfirm.PasswordChar = chkShowCreate.Checked ? '\0' : '*';
            };

            var btnCreate = new Button
            {
                Text = "Create Account",
                Location = new Point(230, 285),
                Size = new Size(130, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(Username))
                { MessageBox.Show("Username is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (string.IsNullOrWhiteSpace(_txtPassword.Text))
                { MessageBox.Show("Password is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_txtPassword.Text != _txtConfirm.Text)
                { MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(370, 285),
                Size = new Size(70, 32),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(header);
            Controls.Add(lblInfo);
            Controls.Add(_txtUsername);
            Controls.Add(_txtPassword);
            Controls.Add(_txtConfirm);
            Controls.Add(chkShowCreate);
            Controls.Add(btnCreate);
            Controls.Add(btnCancel);
            AcceptButton = btnCreate;
            CancelButton = btnCancel;
        }

        private TextBox AddRow(string label, int top, bool password)
        {
            Controls.Add(new Label
            {
                Text = label,
                Location = new Point(20, top + 2),
                Width = 130,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            });
            var tb = new TextBox
            {
                Location = new Point(158, top),
                Width = 270,
                Font = new Font("Segoe UI", 9F)
            };
            if (password) tb.PasswordChar = '*';
            return tb;
        }
    }

    // ── Dialog: Change Password ──────────────────────────────────────────────────────
    internal class ChangeDeptPasswordDialog : Form
    {
        private readonly TextBox _txtNew;
        private readonly TextBox _txtConfirm;

        public string NewPassword => _txtNew.Text;

        public ChangeDeptPasswordDialog(string company, string department, string branch)
        {
            Text = "Change Password";
            Size = new Size(500, 580);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = Color.White;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(255, 140, 0) };
            header.Controls.Add(new Label
            {
                Text = "Change Password",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(22, 16)
            });

            // Info — stacked vertically so long names never overlap
            int y = 82;
            Controls.Add(new Label { Text = "Company",    Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = company,      Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 430, Location = new Point(30, y + 16) });
            y += 50;
            Controls.Add(new Label { Text = "Department", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = department,   Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 430, Location = new Point(30, y + 16) });
            y += 50;
            Controls.Add(new Label { Text = "Branch",     Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = branch,       Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 430, Location = new Point(30, y + 16) });
            y += 56;

            // Divider
            Controls.Add(new Panel { Location = new Point(20, y), Size = new Size(450, 1), BackColor = Color.FromArgb(220, 220, 220) });
            y += 16;

            // New Password
            Controls.Add(new Label { Text = "New Password", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(60,60,60), AutoSize = true, Location = new Point(30, y) });
            y += 20;
            _txtNew = new TextBox { Location = new Point(30, y), Size = new Size(430, 28), Font = new Font("Segoe UI", 10F), PasswordChar = '*' };
            y += 46;

            // Confirm Password
            Controls.Add(new Label { Text = "Confirm Password", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(60,60,60), AutoSize = true, Location = new Point(30, y) });
            y += 20;
            _txtConfirm = new TextBox { Location = new Point(30, y), Size = new Size(430, 28), Font = new Font("Segoe UI", 10F), PasswordChar = '*' };
            y += 38;

            // Show Password checkbox
            var chkShowChange = new CheckBox
            {
                Text = "Show Password",
                Location = new Point(30, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            chkShowChange.CheckedChanged += (s, e) =>
            {
                _txtNew.PasswordChar = chkShowChange.Checked ? '\0' : '*';
                _txtConfirm.PasswordChar = chkShowChange.Checked ? '\0' : '*';
            };
            y += 40;

            // Buttons
            var btnOk = new Button
            {
                Text = "Change Password",
                Location = new Point(220, y),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(255, 140, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtNew.Text))
                { MessageBox.Show("Password is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_txtNew.Text != _txtConfirm.Text)
                { MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button { Text = "Cancel", Location = new Point(370, y), Size = new Size(100, 36), DialogResult = DialogResult.Cancel, Font = new Font("Segoe UI", 9.5F) };

            Controls.Add(header);
            Controls.Add(_txtNew);
            Controls.Add(_txtConfirm);
            Controls.Add(chkShowChange);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }

    // ── Dialog: Verify Admin Identity (before showing a dept account password) ──────
    internal class VerifyAdminPasswordDialog : Form
    {
        private readonly TextBox _txtPassword;
        public string EnteredPassword => _txtPassword.Text;

        public VerifyAdminPasswordDialog(string currentUsername)
        {
            Text = "Verify Your Identity";
            Size = new Size(440, 290);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(70, 130, 180) };
            header.Controls.Add(new Label
            {
                Text = "Verify Your Identity",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(18, 14)
            });

            Controls.Add(new Label
            {
                Text = $"Enter your password to view the account password ({currentUsername}):",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(60, 60, 60),
                AutoSize = false,
                Width = 390,
                Height = 36,
                Location = new Point(22, 74)
            });

            _txtPassword = new TextBox
            {
                Location = new Point(22, 116),
                Width = 390,
                Font = new Font("Segoe UI", 10F),
                PasswordChar = '*'
            };

            var btnConfirm = new Button
            {
                Text = "Confirm",
                Location = new Point(210, 170),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtPassword.Text))
                { MessageBox.Show("Please enter your password.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(320, 170),
                Size = new Size(90, 34),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9.5F)
            };

            Controls.Add(header);
            Controls.Add(_txtPassword);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            AcceptButton = btnConfirm;
            CancelButton = btnCancel;
        }
    }

    // ── Dialog: Show Department Account Password ──────────────────────────────────────
    internal class ShowDeptPasswordDialog : Form
    {
        public ShowDeptPasswordDialog(string company, string department, string branch, string username, string password)
        {
            Text = "Department Account Password";
            Size = new Size(480, 460);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(70, 130, 180) };
            header.Controls.Add(new Label
            {
                Text = "Department Account Password",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(18, 16)
            });

            int y = 74;
            Controls.Add(new Label { Text = "Company",    Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = company,      Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 410, Location = new Point(30, y + 16) });
            y += 46;
            Controls.Add(new Label { Text = "Department", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = department,   Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 410, Location = new Point(30, y + 16) });
            y += 46;
            Controls.Add(new Label { Text = "Branch",     Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = branch,       Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 410, Location = new Point(30, y + 16) });
            y += 46;
            Controls.Add(new Label { Text = "Username",   Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = username,     Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 410, Location = new Point(30, y + 16) });
            y += 52;

            Controls.Add(new Panel { Location = new Point(20, y), Size = new Size(430, 1), BackColor = Color.FromArgb(220, 220, 220) });
            y += 14;

            Controls.Add(new Label { Text = "Password", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            y += 18;

            var txtPassword = new TextBox
            {
                Location = new Point(30, y),
                Size = new Size(320, 30),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = password,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245),
                ForeColor = Color.FromArgb(20, 20, 20)
            };

            var btnCopy = new Button
            {
                Text = "Copy",
                Location = new Point(358, y),
                Size = new Size(72, 30),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(password);
                btnCopy.Text = "Copied!";
                btnCopy.BackColor = Color.FromArgb(40, 167, 69);
                var t = new System.Windows.Forms.Timer { Interval = 2000 };
                t.Tick += (ts, te) =>
                {
                    t.Stop(); t.Dispose();
                    if (!btnCopy.IsDisposed) { btnCopy.Text = "Copy"; btnCopy.BackColor = Color.FromArgb(70, 130, 180); }
                };
                t.Start();
            };
            y += 54;

            var btnClose = new Button
            {
                Text = "Close",
                Location = new Point(360, y),
                Size = new Size(80, 36),
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9.5F)
            };

            Controls.Add(header);
            Controls.Add(txtPassword);
            Controls.Add(btnCopy);
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;
        }
    }

    // ── Dialog: Edit Account ─────────────────────────────────────────────────────────
    internal class EditDeptAccountDialog : Form
    {
        private struct EmailEntry
        {
            public int? EmailId;
            public string Label;
            public override string ToString() => Label;
        }

        private readonly TextBox _txtUsername;
        private readonly CheckBox _chkIsActive;
        private readonly ComboBox _cmbEmail;

        public string Username => _txtUsername.Text.Trim();
        public bool IsActive => _chkIsActive.Checked;
        public int? EmailAddressId => (_cmbEmail.SelectedItem is EmailEntry e && e.EmailId.HasValue) ? e.EmailId : (int?)null;

        public EditDeptAccountDialog(DepartmentAccountRowDto row, List<(int EmailId, string Label)> emails)
        {
            Text = "Edit Department Account";
            Size = new Size(500, 530);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = Color.White;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(40, 167, 69) };
            header.Controls.Add(new Label
            {
                Text = "Edit Department Account",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(22, 16)
            });

            // Info — stacked vertically so long names never overlap
            int y = 82;
            Controls.Add(new Label { Text = "Company",    Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = row.CompanyName,    Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 430, Location = new Point(30, y + 16) });
            y += 50;
            Controls.Add(new Label { Text = "Department", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = row.DepartmentName, Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 430, Location = new Point(30, y + 16) });
            y += 50;
            Controls.Add(new Label { Text = "Branch",     Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(120,120,120), AutoSize = true, Location = new Point(30, y) });
            Controls.Add(new Label { Text = row.BranchName,     Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(30,30,30), AutoSize = false, Width = 430, Location = new Point(30, y + 16) });
            y += 56;

            // Divider
            Controls.Add(new Panel { Location = new Point(20, y), Size = new Size(450, 1), BackColor = Color.FromArgb(220, 220, 220) });
            y += 16;

            // Username field
            Controls.Add(new Label { Text = "Username", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(60,60,60), AutoSize = true, Location = new Point(30, y) });
            y += 20;
            _txtUsername = new TextBox { Location = new Point(30, y), Size = new Size(430, 28), Font = new Font("Segoe UI", 10F), Text = row.Username ?? "" };
            y += 42;

            // Active checkbox
            _chkIsActive = new CheckBox { Text = "Account is Active", Location = new Point(30, y), Checked = row.AccountIsActive, Font = new Font("Segoe UI", 9.5F), AutoSize = true };
            y += 46;

            // Email Address field
            Controls.Add(new Label { Text = "Email Address", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(60,60,60), AutoSize = true, Location = new Point(30, y) });
            y += 20;
            _cmbEmail = new ComboBox { Location = new Point(30, y), Size = new Size(430, 28), Font = new Font("Segoe UI", 9F), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbEmail.Items.Add(new EmailEntry { EmailId = null, Label = "(none)" });
            foreach (var em in emails)
                _cmbEmail.Items.Add(new EmailEntry { EmailId = em.EmailId, Label = em.Label });
            _cmbEmail.SelectedIndex = 0;
            if (row.EmailAddressId.HasValue)
            {
                for (int i = 1; i < _cmbEmail.Items.Count; i++)
                {
                    if (((EmailEntry)_cmbEmail.Items[i]).EmailId == row.EmailAddressId)
                    {
                        _cmbEmail.SelectedIndex = i;
                        break;
                    }
                }
            }
            y += 42;

            // Buttons
            var btnSave = new Button
            {
                Text = "Save Changes",
                Location = new Point(230, y),
                Size = new Size(120, 36),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(Username))
                { MessageBox.Show("Username is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button { Text = "Cancel", Location = new Point(360, y), Size = new Size(100, 36), DialogResult = DialogResult.Cancel, Font = new Font("Segoe UI", 9.5F) };

            Controls.Add(header);
            Controls.Add(_txtUsername);
            Controls.Add(_chkIsActive);
            Controls.Add(_cmbEmail);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }
    }
}
