using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Security;

namespace Yakult.Inventory.App.Pages.Admin.Security
{
    public partial class PortalAccessPage : UserControl
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 11;

        private readonly string _connectionString;

        // Data
        private List<(int RoleId, string RoleName)> _roles;
        private List<(int PortalId, string PortalKey, string DisplayName)> _portals;
        private HashSet<(int RoleId, int PortalId)> _currentAccess;

        // checkbox map: (roleId, portalId) → CheckBox
        private Dictionary<(int, int), CheckBox> _checkboxes;

        // UI refs
        private Panel _scrollPanel;
        private Label _lblStatus;
        private Button _btnSave;
        private Button _btnEdit;
        private Button _btnCancel;

        // Edit-mode state
        private bool _isEditing;
        private HashSet<(int RoleId, int PortalId)> _snapshotAccess;

        // Table metrics
        private const int RoleColWidth  = 210;
        private const int RowHeight     = 48;
        private const int HeaderHeight  = 50;

        public PortalAccessPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildShell();
            _ = LoadAsync();
        }

        private void InitializeComponent() { }

        // ── Shell (permanent controls) ────────────────────────────────────────

        private void BuildShell()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // ── Bottom action bar ─────────────────────────────────────────────
            var bottomBar = new Panel
            {
                Dock   = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White
            };
            bottomBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220)))
                    e.Graphics.DrawLine(pen, 0, 0, ((Panel)s).Width, 0);
            };

            _btnEdit = new Button
            {
                Text      = "Edit",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size      = new Size(100, 36),
                Location  = new Point(24, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnEdit.FlatAppearance.BorderSize         = 0;
            _btnEdit.FlatAppearance.MouseOverBackColor  = Color.FromArgb(55, 130, 225);
            _btnEdit.Click += BtnEdit_Click;
            bottomBar.Controls.Add(_btnEdit);

            _btnSave = new Button
            {
                Text      = "Save Changes",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size      = new Size(130, 36),
                Location  = new Point(132, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            _btnSave.FlatAppearance.BorderSize         = 0;
            _btnSave.FlatAppearance.MouseOverBackColor  = Color.FromArgb(55, 130, 225);
            _btnSave.Click += BtnSave_Click;
            bottomBar.Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text      = "Cancel",
                Font      = new Font("Segoe UI", 10F),
                Size      = new Size(90, 36),
                Location  = new Point(270, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.FromArgb(50, 50, 50),
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            _btnCancel.FlatAppearance.BorderSize         = 0;
            _btnCancel.FlatAppearance.MouseOverBackColor  = Color.FromArgb(200, 200, 200);
            _btnCancel.Click += BtnCancel_Click;
            bottomBar.Controls.Add(_btnCancel);

            _lblStatus = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location  = new Point(372, 20),
                AutoSize  = true
            };
            bottomBar.Controls.Add(_lblStatus);
            Controls.Add(bottomBar);

            // ── Scrollable table area ─────────────────────────────────────────
            _scrollPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };
            Controls.Add(_scrollPanel);

            // ── Page header (added LAST so it appears at the top) ─────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 96,
                BackColor = Color.White
            };
            header.Controls.Add(new Label
            {
                Text      = "Portal Access Management",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Location  = new Point(24, 14),
                AutoSize  = true
            });
            header.Controls.Add(new Label
            {
                Text      = "Configure which roles can access each portal. Changes take effect on the user's next login.",
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(110, 110, 110),
                Location  = new Point(24, 54),
                AutoSize  = true
            });
            Controls.Add(header);
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            try
            {
                _roles         = new List<(int, string)>();
                _portals        = new List<(int, string, string)>();
                _currentAccess = new HashSet<(int, int)>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand(
                        "SELECT RoleId, RoleName FROM dbo.Role WHERE IsActive = 1 ORDER BY RoleName", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            _roles.Add((r.GetInt32(0), r.GetString(1)));

                    using (var cmd = new SqlCommand(
                        "SELECT PortalId, PortalKey, DisplayName FROM dbo.Portal WHERE IsActive = 1 ORDER BY PortalId", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            _portals.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));

                    using (var cmd = new SqlCommand("SELECT RoleId, PortalId FROM dbo.RolePortalAccess", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            _currentAccess.Add((r.GetInt32(0), r.GetInt32(1)));
                }

                BuildTable();
                _btnEdit.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Table construction ────────────────────────────────────────────────

        private void BuildTable()
        {
            _checkboxes = new Dictionary<(int, int), CheckBox>();

            int portalCount = _portals.Count;

            var tlp = new TableLayoutPanel
            {
                Dock            = DockStyle.Top,
                AutoSize        = true,
                AutoSizeMode    = AutoSizeMode.GrowAndShrink,
                ColumnCount     = 1 + portalCount,
                RowCount        = 1 + _roles.Count,
                BackColor       = Color.FromArgb(215, 215, 215),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding         = new Padding(0)
            };

            // Column sizing
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RoleColWidth));
            for (int p = 0; p < portalCount; p++)
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / portalCount));

            // Row sizing
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
            for (int r = 0; r < _roles.Count; r++)
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

            // Header row
            tlp.Controls.Add(MakeHeaderCell("Role", leftAlign: true), 0, 0);
            for (int p = 0; p < portalCount; p++)
                tlp.Controls.Add(MakeHeaderCell(_portals[p].DisplayName), p + 1, 0);

            // Data rows
            for (int r = 0; r < _roles.Count; r++)
            {
                var (roleId, roleName) = _roles[r];
                Color rowColor = r % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);

                tlp.Controls.Add(MakeRoleCell(roleName, rowColor), 0, r + 1);

                for (int p = 0; p < portalCount; p++)
                {
                    int portalId = _portals[p].PortalId;
                    bool isChecked = _currentAccess.Contains((roleId, portalId));

                    var chk = new CheckBox
                    {
                        Checked    = isChecked,
                        Text       = "",
                        Dock       = DockStyle.Fill,
                        CheckAlign = ContentAlignment.MiddleCenter,
                        BackColor  = rowColor,
                        Cursor     = Cursors.Default,
                        Enabled    = false
                    };

                    _checkboxes[(roleId, portalId)] = chk;

                    var cell = new Panel { Dock = DockStyle.Fill, BackColor = rowColor };
                    cell.Controls.Add(chk);
                    tlp.Controls.Add(cell, p + 1, r + 1);
                }
            }

            // Freeze redraws on the scroll panel, swap the table in, then repaint once
            SendMessage(_scrollPanel.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _scrollPanel.Controls.Clear();
                _scrollPanel.Controls.Add(tlp);
            }
            finally
            {
                SendMessage(_scrollPanel.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                _scrollPanel.Refresh();
            }
        }

        private static Label MakeHeaderCell(string text, bool leftAlign = false)
        {
            return new Label
            {
                Text      = text,
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = leftAlign
                    ? ContentAlignment.MiddleLeft
                    : ContentAlignment.MiddleCenter,
                Padding   = leftAlign ? new Padding(14, 0, 0, 0) : Padding.Empty
            };
        }

        private static Label MakeRoleCell(string roleName, Color backColor)
        {
            return new Label
            {
                Text      = roleName,
                Dock      = DockStyle.Fill,
                BackColor = backColor,
                ForeColor = Color.FromArgb(35, 35, 35),
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0)
            };
        }

        // ── Edit / Cancel / Save ──────────────────────────────────────────────

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            // Snapshot current state so Cancel can revert
            _snapshotAccess = new HashSet<(int, int)>();
            foreach (var kvp in _checkboxes)
                if (kvp.Value.Checked) _snapshotAccess.Add(kvp.Key);

            // Enable all checkboxes
            foreach (var chk in _checkboxes.Values)
            {
                chk.Enabled = true;
                chk.Cursor  = Cursors.Hand;
            }

            _isEditing       = true;
            _btnEdit.Visible = false;
            _btnSave.Visible = true;
            _btnCancel.Visible = true;
            _lblStatus.Text  = "";
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            // Revert checkboxes to snapshot
            foreach (var kvp in _checkboxes)
                kvp.Value.Checked = _snapshotAccess != null && _snapshotAccess.Contains(kvp.Key);

            SetLockedMode();
            _lblStatus.Text = "";
        }

        private void SetLockedMode()
        {
            foreach (var chk in _checkboxes.Values)
            {
                chk.Enabled = false;
                chk.Cursor  = Cursors.Default;
            }

            _isEditing         = false;
            _btnSave.Enabled   = true;
            _btnCancel.Enabled = true;
            _btnSave.Visible   = false;
            _btnCancel.Visible = false;
            _btnEdit.Visible   = true;
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            _btnSave.Enabled   = false;
            _btnCancel.Enabled = false;
            _lblStatus.Text      = "Saving...";
            _lblStatus.ForeColor = Color.FromArgb(120, 120, 120);

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var tx = con.BeginTransaction())
                    {
                        const string deleteSql = @"
                            DELETE rpa
                            FROM dbo.RolePortalAccess rpa
                            INNER JOIN dbo.Role r ON r.RoleId = rpa.RoleId
                            WHERE r.IsActive = 1";
                        using (var cmd = new SqlCommand(deleteSql, con, tx))
                            await cmd.ExecuteNonQueryAsync();

                        const string insertSql =
                            "INSERT INTO dbo.RolePortalAccess (RoleId, PortalId) VALUES (@RoleId, @PortalId)";

                        foreach (var kvp in _checkboxes)
                        {
                            if (!kvp.Value.Checked) continue;
                            using (var cmd = new SqlCommand(insertSql, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@RoleId",   kvp.Key.Item1);
                                cmd.Parameters.AddWithValue("@PortalId", kvp.Key.Item2);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        tx.Commit();
                    }
                }

                PermissionResolver.Invalidate();
                SetLockedMode();

                _lblStatus.Text      = "✓  Saved. Users will see updated access on their next login.";
                _lblStatus.ForeColor = Color.FromArgb(78, 154, 252);
            }
            catch (Exception ex)
            {
                _lblStatus.Text      = "Save failed.";
                _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                MessageBox.Show($"Failed to save:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnSave.Enabled   = true;
                _btnCancel.Enabled = true;
            }
        }
    }
}
