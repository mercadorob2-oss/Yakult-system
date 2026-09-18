using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Pages.Admin.EmailManagement
{
    /// <summary>
    /// Page for binding email addresses to employees
    /// </summary>
    public class EmployeeEmailBindingPage : UserControl
    {
        // 0=ID, 1=Name, 2=Position, 3=Company, 4=Department, 5=Branch, 6=DeptEmail(RO), 7=PersonalEmail(editable)
        private const int DeptEmailColIdx     = 6;
        private const int PersonalEmailColIdx = 7;

        // Keep old alias pointing to the editable column for clarity in edit handlers
        private const int EmailColIdx = PersonalEmailColIdx;

        private Panel _headerPanel, _gridPanel, _paginationPanel;
        private Label lblTitle;
        private DataGridView dgvEmployees;
        private ReaLTaiizor.Controls.HopeButton btnRefresh;
        private ReaLTaiizor.Controls.MaterialCard _gridCard;
        private Button btnPrevPage, btnNextPage;
        private Label lblPageInfo;
        private TextBox _txtSearch;
        private Label _lblStatus;
        private System.Windows.Forms.Timer _statusTimer;

        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalRecords = 0;
        private List<EmployeeEmailRow> _allEmployees;
        private List<EmployeeEmailRow> _filteredEmployees;

        private List<string> _emailCache = new List<string>();
        private Dictionary<string, int> _emailIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private bool _isSaving;
        private string _editOriginalValue;

        public EmployeeEmailBindingPage()
        {
            InitializeComponent();
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(15, 10, 15, 10)
            };

            lblTitle = new Label
            {
                Text = "Employee Email Binding",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(15, 10)
            };

            btnRefresh = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "⟳",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Size = new Size(58, 58),
                MinimumSize = new Size(58, 58),
                MaximumSize = new Size(58, 58),
                Location = new Point(_headerPanel.Width - 73, 13),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            ConfigurePillHopeButton(btnRefresh, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));
            btnRefresh.Click += async (s, e) => await LoadDataAsync();
            btnRefresh.Resize += (s, e) =>
            {
                using (var path = new GraphicsPath())
                {
                    int w = Math.Max(1, btnRefresh.Width - 1);
                    int h = Math.Max(1, btnRefresh.Height - 1);
                    path.AddEllipse(0, 0, w, h);
                    btnRefresh.Region = new Region(path);
                }
            };

            var lblSearch = new Label
            {
                Text = "Search:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(-375, 33),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            _txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 9.5F),
                Size = new Size(230, 27),
                Location = new Point(-313, 29),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _txtSearch.TextChanged += (s, e) => ApplySearch();

            _headerPanel.Controls.Add(lblTitle);
            _headerPanel.Controls.Add(lblSearch);
            _headerPanel.Controls.Add(_txtSearch);
            _headerPanel.Controls.Add(btnRefresh);

            // Status bar (thin, docked top of grid panel area)
            _lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Visible = false
            };

            _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _statusTimer.Tick += (s, e) =>
            {
                _statusTimer.Stop();
                _lblStatus.Visible = false;
                _lblStatus.Text = string.Empty;
            };

            // Grid Panel
            _gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15, 12, 15, 8)
            };

            _gridCard = new ReaLTaiizor.Controls.MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            dgvEmployees = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ScrollBars = ScrollBars.Vertical
            };

            UiFactory.StyleGrid(dgvEmployees);
            dgvEmployees.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
            dgvEmployees.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgvEmployees.GridColor = Color.FromArgb(200, 200, 200);
            dgvEmployees.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "EmpId",
                HeaderText = "ID",
                Width = 55,
                MinimumWidth = 55,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Name",
                FillWeight = 25,
                MinimumWidth = 150,
                ReadOnly = true,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Position",
                HeaderText = "Position",
                FillWeight = 20,
                MinimumWidth = 120,
                ReadOnly = true,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CompanyName",
                HeaderText = "Company",
                FillWeight = 20,
                MinimumWidth = 120,
                ReadOnly = true,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DepartmentName",
                HeaderText = "Department",
                FillWeight = 20,
                MinimumWidth = 130,
                ReadOnly = true,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BranchName",
                HeaderText = "Branch",
                FillWeight = 20,
                MinimumWidth = 120,
                ReadOnly = true,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            // Dept Email (col 6) — read-only, inherited from dbo.DepartmentEmail; shown as fallback reference
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colDeptEmail",
                DataPropertyName = "DepartmentEmail",
                HeaderText       = "Dept Email",
                FillWeight       = 25,
                MinimumWidth     = 160,
                ReadOnly         = true,
                HeaderCell       = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(140, 140, 140)   // grayed — read-only fallback
                }
            });

            // Personal Email (col 7) — editable, stored in dbo.EmployeeEmail (IsPrimary=1)
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colEmployeeEmail",
                DataPropertyName = "PrimaryEmail",
                HeaderText       = "Personal Email  ✎",
                FillWeight       = 30,
                MinimumWidth     = 180,
                ReadOnly         = false,
                HeaderCell       = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(22, 100, 170)
                }
            });

            dgvEmployees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgvEmployees.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == DeptEmailColIdx)
                    e.ToolTipText = "Department email — fallback when this employee has no personal email";
                else if (e.ColumnIndex == PersonalEmailColIdx)
                    e.ToolTipText = "Personal email — overrides the department email for this employee";
            };

            dgvEmployees.CellBeginEdit += DgvEmployees_CellBeginEdit;
            dgvEmployees.EditingControlShowing += DgvEmployees_EditingControlShowing;
            dgvEmployees.CellEndEdit += DgvEmployees_CellEndEdit;

            _gridCard.Controls.Add(dgvEmployees);
            _gridCard.Controls.Add(_lblStatus);
            _gridPanel.Controls.Add(_gridCard);

            // Pagination Panel
            _paginationPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.White,
                Padding = new Padding(15, 8, 15, 8)
            };

            var paginationFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };

            btnPrevPage = new Button
            {
                Text = "← Previous",
                Width = 100,
                Height = 32,
                Enabled = false,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnPrevPage.Click += (s, e) => ChangePage(-1);

            lblPageInfo = new Label
            {
                Text = "Page 1 of 1",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 10, 0)
            };

            btnNextPage = new Button
            {
                Text = "Next →",
                Width = 100,
                Height = 32,
                Enabled = false,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnNextPage.Click += (s, e) => ChangePage(1);

            paginationFlow.Controls.Add(btnPrevPage);
            paginationFlow.Controls.Add(lblPageInfo);
            paginationFlow.Controls.Add(btnNextPage);
            _paginationPanel.Controls.Add(paginationFlow);

            Controls.Add(_gridPanel);
            Controls.Add(_paginationPanel);
            Controls.Add(_headerPanel);

            ResumeLayout(true);
        }

        // ── Inline editing ───────────────────────────────────────────────────

        private void DgvEmployees_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex != EmailColIdx)
            {
                e.Cancel = true;
                return;
            }

            string raw = dgvEmployees.Rows[e.RowIndex].Cells[EmailColIdx].Value?.ToString() ?? string.Empty;
            _editOriginalValue = raw.Equals("(None)", StringComparison.OrdinalIgnoreCase) ? string.Empty : raw;
        }

        private void DgvEmployees_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgvEmployees.CurrentCell?.ColumnIndex != EmailColIdx)
                return;

            if (e.Control is TextBox tb)
            {
                tb.TextChanged -= EmailEditor_TextChanged;

                tb.AutoCompleteMode = AutoCompleteMode.None;
                var source = new AutoCompleteStringCollection();
                source.AddRange(_emailCache.ToArray());
                tb.AutoCompleteCustomSource = source;
                tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;

                if (tb.Text.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                    tb.Text = string.Empty;
            }
        }

        private void EmailEditor_TextChanged(object sender, EventArgs e) { }

        private void DgvEmployees_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != EmailColIdx || _isSaving)
                return;

            var row = dgvEmployees.Rows[e.RowIndex];
            var dataRow = row.DataBoundItem as EmployeeEmailRow;
            if (dataRow == null) return;

            string newValue = row.Cells[EmailColIdx].Value?.ToString()?.Trim() ?? string.Empty;
            _ = CommitEmailChangeAsync(e.RowIndex, dataRow, newValue);
        }

        private async Task CommitEmailChangeAsync(int rowIndex, EmployeeEmailRow dataRow, string newValue)
        {
            _isSaving = true;
            try
            {
                string original = _editOriginalValue ?? string.Empty;

                // No change
                if (string.Equals(newValue, original, StringComparison.OrdinalIgnoreCase))
                    return;

                // Empty → remove binding
                if (string.IsNullOrWhiteSpace(newValue) || newValue.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                {
                    var confirm = MessageBox.Show(
                        $"Remove email binding for {dataRow.Name}?",
                        "Confirm Remove",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes)
                    {
                        RevertCell(rowIndex, string.IsNullOrEmpty(original) ? "(None)" : original);
                        return;
                    }

                    try
                    {
                        await RemoveEmployeeEmailBindingAsync(dataRow.EmpId);
                        dataRow.PrimaryEmail = "(None)";
                        dgvEmployees.Rows[rowIndex].Cells[EmailColIdx].Value = "(None)";
                        FlashCell(rowIndex, EmailColIdx, success: true);
                        ShowStatus($"Email binding removed for {dataRow.Name}.", success: true);
                    }
                    catch (Exception ex)
                    {
                        RevertCell(rowIndex, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, EmailColIdx, success: false);
                        ShowStatus($"Error removing binding: {ex.Message}", success: false);
                        Logger.LogError("EmployeeEmailBindingPage.RemoveEmployeeEmailBindingAsync failed", ex);
                    }
                    return;
                }

                // Validate format
                if (!IsValidEmailFormat(newValue))
                {
                    MessageBox.Show($"\"{newValue}\" is not a valid email address.", "Invalid Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RevertCell(rowIndex, string.IsNullOrEmpty(original) ? "(None)" : original);
                    return;
                }

                // Lookup email ID
                int emailId;
                if (_emailIdMap.TryGetValue(newValue, out emailId))
                {
                    // Known email — save directly
                }
                else
                {
                    // Unknown email — confirm insertion
                    var confirm = MessageBox.Show(
                        $"\"{newValue}\" is not in the email list. Add it and assign to {dataRow.Name}?",
                        "New Email",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes)
                    {
                        RevertCell(rowIndex, string.IsNullOrEmpty(original) ? "(None)" : original);
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
                        RevertCell(rowIndex, string.IsNullOrEmpty(original) ? "(None)" : original);
                        FlashCell(rowIndex, EmailColIdx, success: false);
                        ShowStatus($"Error inserting email: {ex.Message}", success: false);
                        Logger.LogError("EmployeeEmailBindingPage.InsertEmailAddressAsync failed", ex);
                        return;
                    }
                }

                // Save binding
                try
                {
                    await SaveEmployeeEmailBindingAsync(dataRow.EmpId, emailId);
                    dataRow.PrimaryEmail = newValue;
                    dgvEmployees.Rows[rowIndex].Cells[EmailColIdx].Value = newValue;
                    FlashCell(rowIndex, EmailColIdx, success: true);
                    ShowStatus($"Email updated for {dataRow.Name}.", success: true);
                }
                catch (Exception ex)
                {
                    RevertCell(rowIndex, string.IsNullOrEmpty(original) ? "(None)" : original);
                    FlashCell(rowIndex, EmailColIdx, success: false);
                    ShowStatus($"Error saving binding: {ex.Message}", success: false);
                    Logger.LogError("EmployeeEmailBindingPage.SaveEmployeeEmailBindingAsync failed", ex);
                }
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void RevertCell(int rowIndex, string value)
        {
            if (rowIndex >= 0 && rowIndex < dgvEmployees.Rows.Count)
                dgvEmployees.Rows[rowIndex].Cells[EmailColIdx].Value = value;
        }

        private void FlashCell(int rowIndex, int colIndex, bool success)
        {
            if (rowIndex < 0 || rowIndex >= dgvEmployees.Rows.Count) return;
            var cell = dgvEmployees.Rows[rowIndex].Cells[colIndex];
            cell.Style.BackColor = success ? Color.FromArgb(180, 240, 180) : Color.FromArgb(255, 180, 180);

            var t = new System.Windows.Forms.Timer { Interval = 1600 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                if (rowIndex < dgvEmployees.Rows.Count)
                    dgvEmployees.Rows[rowIndex].Cells[colIndex].Style.BackColor = Color.Empty;
            };
            t.Start();
        }

        private void ShowStatus(string message, bool success)
        {
            _lblStatus.Text = message;
            _lblStatus.BackColor = success ? Color.FromArgb(210, 245, 210) : Color.FromArgb(255, 220, 220);
            _lblStatus.ForeColor = success ? Color.FromArgb(30, 120, 30) : Color.FromArgb(160, 0, 0);
            _lblStatus.Visible = true;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private static bool IsValidEmailFormat(string email)
        {
            int atIdx = email.IndexOf('@');
            if (atIdx <= 0) return false;
            int dotIdx = email.LastIndexOf('.');
            return dotIdx > atIdx + 1 && dotIdx < email.Length - 1;
        }

        // ── Pagination / search ──────────────────────────────────────────────

        private void ChangePage(int direction)
        {
            _currentPage += direction;
            UpdateGridDisplay();
        }

        private void ApplySearch()
        {
            var term = _txtSearch?.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(term))
            {
                _filteredEmployees = _allEmployees;
            }
            else
            {
                _filteredEmployees = _allEmployees?.Where(e =>
                    (e.Name?.ToLowerInvariant().Contains(term) == true) ||
                    (e.Position?.ToLowerInvariant().Contains(term) == true) ||
                    (e.CompanyName?.ToLowerInvariant().Contains(term) == true) ||
                    (e.DepartmentName?.ToLowerInvariant().Contains(term) == true) ||
                    (e.BranchName?.ToLowerInvariant().Contains(term) == true) ||
                    (e.DepartmentEmail?.ToLowerInvariant().Contains(term) == true) ||
                    (e.PrimaryEmail?.ToLowerInvariant().Contains(term) == true)
                ).ToList();
            }

            _totalRecords = _filteredEmployees?.Count ?? 0;
            _currentPage = 1;
            UpdateGridDisplay();
        }

        private void UpdateGridDisplay()
        {
            var source = _filteredEmployees ?? _allEmployees;
            if (source == null || source.Count == 0)
            {
                dgvEmployees.DataSource = null;
                lblPageInfo.Text = "Page 0 of 0";
                btnPrevPage.Enabled = false;
                btnNextPage.Enabled = false;
                return;
            }

            int totalPages = (int)Math.Ceiling((double)_totalRecords / _pageSize);
            _currentPage = Math.Max(1, Math.Min(_currentPage, totalPages));

            var pagedData = source
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            dgvEmployees.DataSource = pagedData;
            lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({_totalRecords} total)";
            btnPrevPage.Enabled = _currentPage > 1;
            btnNextPage.Enabled = _currentPage < totalPages;
        }

        private void ConfigurePillHopeButton(ReaLTaiizor.Controls.HopeButton button, Color baseColor, Color hoverColor)
        {
            if (button == null) return;

            button.ButtonType = ReaLTaiizor.Util.HopeButtonType.Primary;
            button.PrimaryColor = baseColor;
            button.DefaultColor = baseColor;
            button.BorderColor = baseColor;
            button.TextColor = Color.White;
            button.HoverTextColor = Color.White;
            button.Cursor = Cursors.Hand;

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverColor;
                button.DefaultColor = hoverColor;
                button.BorderColor = hoverColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = baseColor;
                button.DefaultColor = baseColor;
                button.BorderColor = baseColor;
                button.Invalidate();
            };
        }

        // ── Data loading ─────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            try
            {
                _allEmployees = await GetEmployeesWithEmailsAsync();
                _totalRecords = _allEmployees?.Count ?? 0;
                _currentPage = 1;

                var emails = await GetEmailAddressesAsync(activeOnly: true);
                _emailCache = emails.Select(e => e.EmailAddress).ToList();
                _emailIdMap = emails.ToDictionary(e => e.EmailAddress, e => e.EmailId, StringComparer.OrdinalIgnoreCase);

                ApplySearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("EmployeeEmailBindingPage.LoadDataAsync failed", ex);
            }
        }

        private async Task<List<EmailAddressDto>> GetEmailAddressesAsync(bool activeOnly)
        {
            var results = new List<EmailAddressDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    SELECT
                        EmailId,
                        EmailAddress,
                        DisplayName,
                        IsActive
                    FROM dbo.EmailAddress
                    WHERE (@ActiveOnly = 0 OR IsActive = 1)
                    ORDER BY EmailAddress";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new EmailAddressDto
                            {
                                EmailId = reader.GetInt32(0),
                                EmailAddress = reader.GetString(1),
                                DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                IsActive = reader.GetBoolean(3)
                            });
                        }
                    }
                }
            }

            return results;
        }

        private async Task<List<EmployeeEmailRow>> GetEmployeesWithEmailsAsync()
        {
            var results = new List<EmployeeEmailRow>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    SELECT
                        e.EmpId,
                        e.Name,
                        e.Position,
                        c.Name  AS CompanyName,
                        d.Name  AS DepartmentName,
                        b.Name  AS BranchName,
                        -- Department-level email (parent fallback)
                        dept_ea.EmailAddress  AS DepartmentEmail,
                        -- Employee personal email (child — overrides dept email)
                        emp_ea.EmailAddress   AS PrimaryEmail
                    FROM dbo.Employee e
                    INNER JOIN dbo.Company    c  ON e.ComId    = c.ComId
                    INNER JOIN dbo.Department d  ON e.DeptId   = d.DeptId
                    INNER JOIN dbo.Branch     b  ON e.BranchId = b.BranchId
                    -- Personal email: primary active entry in dbo.EmployeeEmail
                    LEFT JOIN dbo.EmployeeEmail ee
                           ON ee.EmpId = e.EmpId AND ee.IsPrimary = 1 AND ee.IsActive = 1
                    LEFT JOIN dbo.EmailAddress emp_ea
                           ON emp_ea.EmailId = ee.EmailId AND emp_ea.IsActive = 1
                    -- Department-level fallback email
                    LEFT JOIN dbo.DepartmentEmail de
                           ON de.CompanyName    = c.Name
                          AND de.DepartmentName = d.Name
                    LEFT JOIN dbo.EmailAddress dept_ea
                           ON dept_ea.EmailId = de.EmailAddressId
                    WHERE e.Active = 1
                    ORDER BY c.Name, d.Name, e.Name";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new EmployeeEmailRow
                        {
                            EmpId          = reader.GetInt32(0),
                            Name           = reader.GetString(1),
                            Position       = reader.IsDBNull(2) ? ""       : reader.GetString(2),
                            CompanyName    = reader.IsDBNull(3) ? "N/A"    : reader.GetString(3),
                            DepartmentName = reader.IsDBNull(4) ? "N/A"    : reader.GetString(4),
                            BranchName     = reader.IsDBNull(5) ? "N/A"    : reader.GetString(5),
                            DepartmentEmail = reader.IsDBNull(6) ? "(None)" : reader.GetString(6),
                            PrimaryEmail   = reader.IsDBNull(7) ? "(None)" : reader.GetString(7)
                        });
                    }
                }
            }

            return results;
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

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
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
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                            UPDATE dbo.EmployeeEmail
                            SET IsPrimary = 0
                            WHERE EmpId = @EmpId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@EmpId", empId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        int existingCount;
                        using (var cmd = new SqlCommand(@"
                            SELECT COUNT(*)
                            FROM dbo.EmployeeEmail
                            WHERE EmpId = @EmpId AND EmailId = @EmailId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@EmpId", empId);
                            cmd.Parameters.AddWithValue("@EmailId", emailId);
                            existingCount = (int)await cmd.ExecuteScalarAsync();
                        }

                        if (existingCount > 0)
                        {
                            using (var cmd = new SqlCommand(@"
                                UPDATE dbo.EmployeeEmail
                                SET IsPrimary = 1, IsActive = 1
                                WHERE EmpId = @EmpId AND EmailId = @EmailId", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EmpId", empId);
                                cmd.Parameters.AddWithValue("@EmailId", emailId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive)
                                VALUES (@EmpId, @EmailId, 'Work', 1, 1)", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EmpId", empId);
                                cmd.Parameters.AddWithValue("@EmailId", emailId);
                                await cmd.ExecuteNonQueryAsync();
                            }
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
        }

        private async Task RemoveEmployeeEmailBindingAsync(int empId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    DELETE FROM dbo.EmployeeEmail
                    WHERE EmpId = @EmpId", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private class EmployeeEmailRow
        {
            public int    EmpId          { get; set; }
            public string Name           { get; set; }
            public string Position       { get; set; }
            public string CompanyName    { get; set; }
            public string DepartmentName { get; set; }
            public string BranchName     { get; set; }
            // Parent — from dbo.DepartmentEmail (fallback when no personal email)
            public string DepartmentEmail { get; set; }
            // Child — from dbo.EmployeeEmail (IsPrimary=1, overrides dept email)
            public string PrimaryEmail   { get; set; }
        }
    }
}
