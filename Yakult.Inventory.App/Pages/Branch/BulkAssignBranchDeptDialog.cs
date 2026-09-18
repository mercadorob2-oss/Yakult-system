using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Branch
{
    /// <summary>
    /// Dialog for bulk-assigning multiple branches to a target department.
    ///
    /// Replaces manual SQL UPDATE scripts for re-assigning distributors/companies.
    ///
    /// Usage:
    ///     using (var dlg = new BulkAssignBranchDeptDialog())
    ///         dlg.ShowDialog(this);
    /// </summary>
    public class BulkAssignBranchDeptDialog : Form
    {
        // ── Accent colors ─────────────────────────────────────────────────────
        private static readonly Color Teal = Color.FromArgb(0, 150, 136);
        private static readonly Color Gray = Color.FromArgb(149, 165, 166);

        // ── Filter controls ───────────────────────────────────────────────────
        private ComboBox _cmbDepartment;
        private TextBox  _txtSearch;
        private ComboBox _cmbTypeFilter;
        private CheckBox _chkHideAssigned;

        // ── Grid ──────────────────────────────────────────────────────────────
        private DataGridView _dgvBranches;
        private Label        _lblSelStatus;
        private CheckState   _headerCheckState = CheckState.Unchecked;

        // ── Buttons ───────────────────────────────────────────────────────────
        private Button _btnAssign;
        private Button _btnCancel;

        // ── In-memory data ────────────────────────────────────────────────────
        private List<BranchRow>   _allBranches = new List<BranchRow>();
        private List<BranchRow>   _filtered    = new List<BranchRow>();
        private readonly List<int>    _deptIds   = new List<int>();
        private readonly List<string> _deptNames = new List<string>();

        private readonly BranchRepository _repo = new BranchRepository();

        // ── Per-row data model ────────────────────────────────────────────────
        private class BranchRow
        {
            public int    BranchId      { get; set; }
            public string Name          { get; set; }
            public int?   CurrentDeptId { get; set; }
            public string CurrentDept   { get; set; }
            public string BranchType    { get; set; }
            public string CompanyName   { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────────
        public BulkAssignBranchDeptDialog()
        {
            BuildUi();
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Text            = "Bulk Assign Branches to Department";
            Size            = new Size(940, 630);
            MinimumSize     = new Size(760, 480);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = Color.White;

            // ── Header ───────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Teal };
            header.Controls.Add(new Label
            {
                Text      = "Bulk Assign Branches to Department",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 12)
            });

            // ── Filter bar ───────────────────────────────────────────────────
            var filterBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 62,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            int fx = 16;

            filterBar.Controls.Add(new Label
            {
                Text     = "Assign to:",
                Location = new Point(fx, 20),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold)
            });
            fx += 76;

            _cmbDepartment = new ComboBox
            {
                Location      = new Point(fx, 16),
                Width         = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F)
            };
            filterBar.Controls.Add(_cmbDepartment);
            fx += 274;

            filterBar.Controls.Add(new Label
            {
                Text     = "Type:",
                Location = new Point(fx, 20),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F)
            });
            fx += 42;

            _cmbTypeFilter = new ComboBox
            {
                Location      = new Point(fx, 16),
                Width         = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F)
            };
            _cmbTypeFilter.Items.AddRange(new object[]
                { "All Types", "Distributor", "Depot", "Center", "Factory", "Office" });
            _cmbTypeFilter.SelectedIndex = 0;
            filterBar.Controls.Add(_cmbTypeFilter);
            fx += 134;

            filterBar.Controls.Add(new Label
            {
                Text     = "Search:",
                Location = new Point(fx, 20),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F)
            });
            fx += 54;

            _txtSearch = new TextBox
            {
                Location = new Point(fx, 16),
                Width    = 180,
                Font     = new Font("Segoe UI", 9F)
            };
            filterBar.Controls.Add(_txtSearch);
            fx += 194;

            _chkHideAssigned = new CheckBox
            {
                Text     = "Hide already assigned",
                Location = new Point(fx, 19),
                AutoSize = true,
                Font     = new Font("Segoe UI", 8.5F)
            };
            filterBar.Controls.Add(_chkHideAssigned);

            _cmbDepartment.SelectedIndexChanged += (s, e) => ApplyFilter();
            _cmbTypeFilter.SelectedIndexChanged  += (s, e) => ApplyFilter();
            _txtSearch.TextChanged               += (s, e) => ApplyFilter();
            _chkHideAssigned.CheckedChanged      += (s, e) => ApplyFilter();

            // ── Grid ─────────────────────────────────────────────────────────
            _dgvBranches = new DataGridView
            {
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = true,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.FixedSingle,
                GridColor             = Color.FromArgb(224, 224, 224),
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars            = ScrollBars.Both
            };
            _dgvBranches.ColumnHeadersDefaultCellStyle.BackColor  = Teal;
            _dgvBranches.ColumnHeadersDefaultCellStyle.ForeColor  = Color.White;
            _dgvBranches.ColumnHeadersDefaultCellStyle.Font       = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgvBranches.EnableHeadersVisualStyles                 = false;
            _dgvBranches.ColumnHeadersHeightSizeMode               = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvBranches.ColumnHeadersHeight                       = 34;
            _dgvBranches.DefaultCellStyle.SelectionBackColor       = Color.FromArgb(178, 235, 242);
            _dgvBranches.DefaultCellStyle.SelectionForeColor       = Color.Black;
            _dgvBranches.DefaultCellStyle.Font                     = new Font("Segoe UI", 9F);
            _dgvBranches.RowTemplate.Height                        = 26;
            _dgvBranches.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 252, 252);

            _dgvBranches.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name         = "chkSelect",
                HeaderText   = "",
                Width        = 34,
                ReadOnly     = false,
                FalseValue   = false,
                TrueValue    = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            _dgvBranches.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colBranchId",
                HeaderText       = "ID",
                DataPropertyName = "BranchId",
                Width            = 52,
                ReadOnly         = true
            });
            _dgvBranches.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colName",
                HeaderText       = "Branch / Company Name",
                DataPropertyName = "Name",
                Width            = 270,
                ReadOnly         = true
            });
            _dgvBranches.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colCurrentDept",
                HeaderText       = "Current Department",
                DataPropertyName = "CurrentDept",
                Width            = 200,
                ReadOnly         = true
            });
            _dgvBranches.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colType",
                HeaderText       = "Type",
                DataPropertyName = "BranchType",
                Width            = 100,
                ReadOnly         = true
            });
            _dgvBranches.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colCompany",
                HeaderText       = "Company",
                DataPropertyName = "CompanyName",
                Width            = 140,
                ReadOnly         = true,
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.Fill
            });

            _dgvBranches.CellPainting              += DgvBranches_CellPainting;
            _dgvBranches.CellMouseClick            += DgvBranches_CellMouseClick;
            _dgvBranches.CellValueChanged          += DgvBranches_CellValueChanged;
            _dgvBranches.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_dgvBranches.IsCurrentCellDirty)
                    _dgvBranches.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // ── Status label ─────────────────────────────────────────────────
            _lblSelStatus = new Label
            {
                Text      = "0 rows selected",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true
            };

            // ── Body panel (grid + status) ────────────────────────────────────
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            body.Controls.Add(_dgvBranches);
            body.Controls.Add(_lblSelStatus);
            body.Resize += (s, e) =>
            {
                _dgvBranches.Location  = new Point(16, 8);
                _dgvBranches.Width     = body.ClientSize.Width  - 32;
                _dgvBranches.Height    = body.ClientSize.Height - 36;
                _lblSelStatus.Location = new Point(16, body.ClientSize.Height - 22);
            };

            // ── Button bar ───────────────────────────────────────────────────
            var btnBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 55,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            _btnCancel = new Button
            {
                Text      = "Cancel",
                Width     = 90,
                Height    = 34,
                BackColor = Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand,
                Location  = new Point(12, 10)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnAssign = new Button
            {
                Text      = "Assign Selected",
                Width     = 150,
                Height    = 34,
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnAssign.FlatAppearance.BorderSize = 0;
            _btnAssign.Click += async (s, e) => await BtnAssign_ClickAsync();

            btnBar.Controls.Add(_btnCancel);
            btnBar.Controls.Add(_btnAssign);
            btnBar.Resize += (s, e) =>
            {
                _btnAssign.Location = new Point(btnBar.ClientSize.Width - _btnAssign.Width - 12, 10);
            };

            Controls.Add(body);
            Controls.Add(filterBar);
            Controls.Add(btnBar);
            Controls.Add(header);

            CancelButton = _btnCancel;
            Shown += async (s, e) => await LoadDataAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async Task LoadDataAsync()
        {
            try
            {
                await LoadDepartmentsAsync();
                await LoadBranchesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load data:\n" + ex.Message,
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadDepartmentsAsync()
        {
            _deptIds.Clear();
            _deptNames.Clear();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    SELECT d.DeptId, d.Name
                    FROM   dbo.Department d
                    WHERE  d.Active = 1
                    ORDER  BY d.Name", con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        _deptIds.Add(reader.GetInt32(0));
                        _deptNames.Add(reader.IsDBNull(1) ? "(unnamed)" : reader.GetString(1));
                    }
                }
            }

            _cmbDepartment.Items.Clear();
            _cmbDepartment.Items.AddRange(_deptNames.ToArray<object>());
            if (_cmbDepartment.Items.Count > 0)
                _cmbDepartment.SelectedIndex = 0;
        }

        private async Task LoadBranchesAsync()
        {
            _allBranches.Clear();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    SELECT
                        b.BranchId,
                        b.Name,
                        ISNULL(bdc_dept.DeptId, 0)           AS DeptId,
                        ISNULL(bdc_dept.DeptName, '(none)')  AS CurrentDept,
                        ISNULL(b.BranchType,
                            CASE
                                WHEN b.IsDistributor = 1 THEN N'Distributor'
                                WHEN b.IsDepot       = 1 THEN N'Depot'
                                WHEN b.IsCenter      = 1 THEN N'Center'
                                WHEN b.IsFactory     = 1 THEN N'Factory'
                                ELSE N'Office'
                            END)                              AS BranchType,
                        ISNULL(bdc_co.CompanyName, '(none)') AS CompanyName
                    FROM       dbo.Branch b
                    OUTER APPLY (
                        SELECT TOP 1 bdc.DepartmentID AS DeptId, d.Name AS DeptName
                        FROM   dbo.BranchDepartmentCompany bdc
                        JOIN   dbo.Department d ON bdc.DepartmentID = d.DeptId
                        WHERE  bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
                        ORDER BY bdc.BranchDeptCompanyID
                    ) bdc_dept
                    OUTER APPLY (
                        SELECT TOP 1 c.Name AS CompanyName
                        FROM   dbo.BranchDepartmentCompany bdc
                        JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
                        WHERE  bdc.BranchID = b.BranchId
                        ORDER BY bdc.BranchDeptCompanyID
                    ) bdc_co
                    WHERE      b.Active = 1
                    ORDER BY   b.Name", con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        _allBranches.Add(new BranchRow
                        {
                            BranchId      = reader.GetInt32(0),
                            Name          = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            CurrentDeptId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                            CurrentDept   = reader.GetString(3),
                            BranchType    = reader.GetString(4),
                            CompanyName   = reader.GetString(5)
                        });
                    }
                }
            }

            ApplyFilter();
        }

        // ═════════════════════════════════════════════════════════════════════
        // FILTERING
        // ═════════════════════════════════════════════════════════════════════

        private void ApplyFilter()
        {
            var search       = (_txtSearch.Text ?? "").Trim();
            var typeFilter   = _cmbTypeFilter.SelectedIndex > 0
                                   ? (string)_cmbTypeFilter.SelectedItem : null;
            int? targetDeptId = _cmbDepartment.SelectedIndex >= 0
                                    ? _deptIds[_cmbDepartment.SelectedIndex]
                                    : (int?)null;
            bool hideAssigned = _chkHideAssigned.Checked;

            _filtered = _allBranches.Where(b =>
            {
                if (typeFilter != null && b.BranchType != typeFilter)
                    return false;
                if (hideAssigned && targetDeptId.HasValue && b.CurrentDeptId == targetDeptId)
                    return false;
                if (!string.IsNullOrEmpty(search)
                    && b.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && b.CompanyName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
                return true;
            }).ToList();

            RebindGrid();
        }

        private void RebindGrid()
        {
            _dgvBranches.CellValueChanged -= DgvBranches_CellValueChanged;
            _dgvBranches.Rows.Clear();

            int? targetDeptId = _cmbDepartment.SelectedIndex >= 0
                                    ? _deptIds[_cmbDepartment.SelectedIndex]
                                    : (int?)null;

            foreach (var b in _filtered)
            {
                int idx = _dgvBranches.Rows.Add(
                    false,
                    b.BranchId,
                    b.Name,
                    b.CurrentDept,
                    b.BranchType,
                    b.CompanyName);

                // Tint rows already assigned to the chosen target dept
                if (targetDeptId.HasValue && b.CurrentDeptId == targetDeptId)
                {
                    _dgvBranches.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(0, 130, 115);
                    _dgvBranches.Rows[idx].DefaultCellStyle.Font      =
                        new Font("Segoe UI", 9F, FontStyle.Italic);
                }
            }

            _headerCheckState = CheckState.Unchecked;
            _dgvBranches.InvalidateCell(0, -1);
            _dgvBranches.CellValueChanged += DgvBranches_CellValueChanged;
            UpdateSelectionStatus();
        }

        // ═════════════════════════════════════════════════════════════════════
        // GRID EVENTS
        // ═════════════════════════════════════════════════════════════════════

        private void DgvBranches_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex >= 0)
            {
                SyncHeaderCheckState();
                UpdateSelectionStatus();
            }
        }

        private void SyncHeaderCheckState()
        {
            int total    = _dgvBranches.Rows.Count;
            int selected = CountSelected();

            if (selected == 0)           _headerCheckState = CheckState.Unchecked;
            else if (selected == total)  _headerCheckState = CheckState.Checked;
            else                         _headerCheckState = CheckState.Indeterminate;

            _dgvBranches.InvalidateCell(0, -1);
        }

        private int CountSelected()
        {
            int n = 0;
            foreach (DataGridViewRow row in _dgvBranches.Rows)
                if (row.Cells["chkSelect"].Value is bool b && b) n++;
            return n;
        }

        private void UpdateSelectionStatus()
        {
            int selected = CountSelected();
            _lblSelStatus.Text = $"{selected} of {_dgvBranches.Rows.Count} branch(es) selected";
            _btnAssign.Text = selected > 0
                ? $"Assign {selected} Branch(es)"
                : "Assign Selected";
        }

        // ── Header checkbox: paint ────────────────────────────────────────────
        private void DgvBranches_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.RowIndex != -1) return;

            e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            var g   = e.Graphics;
            int cx  = e.CellBounds.Left + (e.CellBounds.Width  - 13) / 2;
            int cy  = e.CellBounds.Top  + (e.CellBounds.Height - 13) / 2;
            var box = new Rectangle(cx, cy, 13, 13);

            ControlPaint.DrawCheckBox(g, box,
                _headerCheckState == CheckState.Checked
                    ? ButtonState.Checked
                    : ButtonState.Normal);

            // Indeterminate dash
            if (_headerCheckState == CheckState.Indeterminate)
            {
                using (var br = new SolidBrush(Color.FromArgb(160, Teal)))
                    g.FillRectangle(br, cx + 3, cy + 3, 7, 7);
            }

            e.Handled = true;
        }

        // ── Header checkbox: click ────────────────────────────────────────────
        private void DgvBranches_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.RowIndex != -1) return;

            bool setTo = _headerCheckState != CheckState.Checked;

            _dgvBranches.CellValueChanged -= DgvBranches_CellValueChanged;
            foreach (DataGridViewRow row in _dgvBranches.Rows)
                row.Cells["chkSelect"].Value = setTo;
            _headerCheckState = setTo ? CheckState.Checked : CheckState.Unchecked;
            _dgvBranches.InvalidateCell(0, -1);
            _dgvBranches.CellValueChanged += DgvBranches_CellValueChanged;
            UpdateSelectionStatus();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ASSIGN ACTION
        // ═════════════════════════════════════════════════════════════════════

        private async Task BtnAssign_ClickAsync()
        {
            // ── Validate: department selected ────────────────────────────────
            if (_cmbDepartment.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a target department.",
                    "No Department Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ── Collect selected branch IDs ──────────────────────────────────
            var selectedIds = new List<int>();
            foreach (DataGridViewRow row in _dgvBranches.Rows)
                if (row.Cells["chkSelect"].Value is bool b && b)
                    selectedIds.Add((int)row.Cells["colBranchId"].Value);

            if (selectedIds.Count == 0)
            {
                MessageBox.Show("No branches selected. Check at least one row.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int    targetDeptId   = _deptIds[_cmbDepartment.SelectedIndex];
            string targetDeptName = _deptNames[_cmbDepartment.SelectedIndex];

            // ── Confirm ──────────────────────────────────────────────────────
            var answer = MessageBox.Show(
                $"Assign {selectedIds.Count} branch(es) to:\n\n" +
                $"    Department:  {targetDeptName}  (ID {targetDeptId})\n\n" +
                $"This will overwrite each branch's current department.\n" +
                $"Continue?",
                "Confirm Bulk Assignment",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            // ── Execute ──────────────────────────────────────────────────────
            _btnAssign.Enabled = false;
            _btnCancel.Enabled = false;
            _btnAssign.Text    = "Assigning…";

            try
            {
                int rows = await _repo.BulkAssignDepartmentAsync(selectedIds, targetDeptId);

                MessageBox.Show(
                    $"{rows} branch(es) successfully assigned to {targetDeptName}.",
                    "Assignment Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Assignment failed:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                _btnAssign.Enabled = true;
                _btnCancel.Enabled = true;
                _btnAssign.Text    = $"Assign {selectedIds.Count} Branch(es)";
            }
        }
    }
}
