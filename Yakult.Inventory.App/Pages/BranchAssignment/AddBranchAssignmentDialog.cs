using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Pages.BranchAssignment
{
    /// <summary>
    /// Dialog for adding a single entry to dbo.BranchDepartmentCompany.
    /// Calls dbo.usp_AssignBranchDeptCompany to prevent duplicates.
    /// </summary>
    public class AddBranchAssignmentDialog : Form
    {
        private static readonly Color Blue = Color.FromArgb(41, 128, 185);
        private static readonly Color Gray = Color.FromArgb(149, 165, 166);

        // ── Controls ─────────────────────────────────────────────────────────
        private ComboBox _cmbCompany;
        private ComboBox _cmbBranch;
        private ComboBox _cmbDepartment;
        private Button   _btnSave;
        private Button   _btnCancel;

        // ── Lookup lists ─────────────────────────────────────────────────────
        private readonly List<int>    _companyIds   = new List<int>();
        private readonly List<string> _companyNames = new List<string>();
        private readonly List<int>    _branchIds    = new List<int>();
        private readonly List<string> _branchNames  = new List<string>();
        private readonly List<int?>   _deptIds      = new List<int?>();   // null = No Department
        private readonly List<string> _deptNames    = new List<string>();

        public AddBranchAssignmentDialog()
        {
            BuildUi();
            Shown += async (s, e) => await LoadDropdownsAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Text            = "Add Branch Assignment";
            Size            = new Size(480, 310);
            MinimumSize     = new Size(400, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Blue };
            header.Controls.Add(new Label
            {
                Text      = "Add Branch Assignment",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(14, 10)
            });

            // Form body
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 0), BackColor = Color.White };

            int y = 0;

            body.Controls.Add(MakeLabel("Company *", y));
            _cmbCompany = MakeDropdown(y + 20);
            body.Controls.Add(_cmbCompany);
            y += 60;

            body.Controls.Add(MakeLabel("Branch *", y));
            _cmbBranch = MakeDropdown(y + 20);
            body.Controls.Add(_cmbBranch);
            y += 60;

            body.Controls.Add(MakeLabel("Department  (optional — leave as \"No Department\" for YMC)", y));
            _cmbDepartment = MakeDropdown(y + 20);
            body.Controls.Add(_cmbDepartment);

            // Button bar
            var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(245, 247, 250) };

            _btnCancel = new Button
            {
                Text      = "Cancel",
                Width     = 85,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Gray,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand,
                Location  = new Point(12, 10)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnSave = new Button
            {
                Text      = "Save Assignment",
                Width     = 140,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += async (s, e) => await BtnSave_ClickAsync();

            btnBar.Controls.Add(_btnCancel);
            btnBar.Controls.Add(_btnSave);
            btnBar.Resize += (s, e) =>
                _btnSave.Location = new Point(btnBar.ClientSize.Width - _btnSave.Width - 12, 10);

            Controls.Add(body);
            Controls.Add(btnBar);
            Controls.Add(header);
            CancelButton = _btnCancel;
        }

        private static Label MakeLabel(string text, int y) =>
            new Label
            {
                Text     = text,
                Location = new Point(20, y),
                AutoSize = true,
                Font     = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

        private static ComboBox MakeDropdown(int y) =>
            new ComboBox
            {
                Location      = new Point(20, y),
                Width         = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F)
            };

        // ═════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadDropdownsAsync()
        {
            _btnSave.Enabled = false;
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();

                    // Companies
                    using (var cmd = new SqlCommand(
                        "SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            _companyIds.Add(r.GetInt32(0));
                            _companyNames.Add(r.IsDBNull(1) ? "(unnamed)" : r.GetString(1));
                        }
                    }
                    _cmbCompany.Items.AddRange(_companyNames.ToArray<object>());
                    if (_cmbCompany.Items.Count > 0) _cmbCompany.SelectedIndex = 0;

                    // Branches
                    using (var cmd = new SqlCommand(
                        "SELECT BranchId, Name FROM dbo.Branch WHERE Active = 1 ORDER BY Name", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            _branchIds.Add(r.GetInt32(0));
                            _branchNames.Add(r.IsDBNull(1) ? "(unnamed)" : r.GetString(1));
                        }
                    }
                    _cmbBranch.Items.AddRange(_branchNames.ToArray<object>());
                    if (_cmbBranch.Items.Count > 0) _cmbBranch.SelectedIndex = 0;

                    // Departments — first entry is null (No Department)
                    _deptIds.Add(null);
                    _deptNames.Add("(No Department)");
                    using (var cmd = new SqlCommand(
                        "SELECT DeptId, Name FROM dbo.Department WHERE Active = 1 ORDER BY Name", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            _deptIds.Add(r.GetInt32(0));
                            _deptNames.Add(r.IsDBNull(1) ? "(unnamed)" : r.GetString(1));
                        }
                    }
                    _cmbDepartment.Items.AddRange(_deptNames.ToArray<object>());
                    _cmbDepartment.SelectedIndex = 0;  // defaults to "(No Department)"
                }
                _btnSave.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load dropdowns:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // SAVE
        // ═════════════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task BtnSave_ClickAsync()
        {
            if (_cmbCompany.SelectedIndex < 0 || _cmbBranch.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a Company and a Branch.",
                    "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int  branchId  = _branchIds[_cmbBranch.SelectedIndex];
            int  companyId = _companyIds[_cmbCompany.SelectedIndex];
            int? deptId    = _cmbDepartment.SelectedIndex >= 0
                                 ? _deptIds[_cmbDepartment.SelectedIndex]
                                 : null;

            _btnSave.Enabled = false;
            _btnSave.Text    = "Saving…";

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand("dbo.usp_AssignBranchDeptCompany", con))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@BranchID",     branchId);
                        cmd.Parameters.AddWithValue("@CompanyID",    companyId);
                        cmd.Parameters.AddWithValue("@DepartmentID", deptId.HasValue ? (object)deptId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchEmail",  DBNull.Value);

                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                string action = r.IsDBNull(1) ? "saved" : r.GetString(1).ToLower();
                                MessageBox.Show($"Assignment {action} successfully.",
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save assignment:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnSave.Enabled = true;
                _btnSave.Text    = "Save Assignment";
            }
        }
    }
}
