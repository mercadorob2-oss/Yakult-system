using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Admin.Security
{
    /// <summary>
    /// Reusable User x Column permission grid: renders a table of checkboxes with an
    /// Edit / Save / Cancel workflow. Used for the Portals, Pages, Item Categories, and
    /// Actions tabs on UserPortalAccessPage. The panel only renders and tracks checkbox
    /// state — all load/save I/O is supplied by the host via Configure()/SaveRequested.
    /// </summary>
    public partial class PermissionGridPanel : UserControl
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 11;

        /// <summary>Raised when Save is clicked. Handler should persist the grid and return true on success.</summary>
        public event Func<Dictionary<(int UserId, string Key), bool>, Task<bool>> SaveRequested;

        private List<(int UserId, string Name)> _users;
        private List<(string Key, string DisplayName)> _columns;
        private Dictionary<(int, string), bool> _baselineChecked; // null = no "✓ Role"-style badge feature
        private Dictionary<(int, string), CheckBox> _checkboxes;
        private Dictionary<(int, string), bool> _snapshot;

        private Panel _scrollPanel;
        private Label _lblStatus;
        private Button _btnSave;
        private Button _btnEdit;
        private Button _btnCancel;
        private Label _lblSubtitle;

        private const int UserColWidth = 220;
        private const int RowHeight = 48;
        private const int HeaderHeight = 50;

        private static readonly Color RoleCellBg = Color.FromArgb(219, 234, 254);
        private static readonly Color RoleCellText = Color.FromArgb(30, 100, 200);

        public PermissionGridPanel()
        {
            InitializeComponent();
            BuildShell();
        }

        private void InitializeComponent() { }

        private void BuildShell()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.White };
            bottomBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220)))
                    e.Graphics.DrawLine(pen, 0, 0, ((Panel)s).Width, 0);
            };

            _btnEdit = new Button
            {
                Text = "Edit",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size = new Size(100, 36),
                Location = new Point(24, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnEdit.FlatAppearance.BorderSize = 0;
            _btnEdit.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 130, 225);
            _btnEdit.Click += BtnEdit_Click;
            bottomBar.Controls.Add(_btnEdit);

            _btnSave = new Button
            {
                Text = "Save Changes",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size = new Size(130, 36),
                Location = new Point(132, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 130, 225);
            _btnSave.Click += BtnSave_Click;
            bottomBar.Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 10F),
                Size = new Size(90, 36),
                Location = new Point(270, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
            _btnCancel.Click += BtnCancel_Click;
            bottomBar.Controls.Add(_btnCancel);

            _lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(372, 20),
                AutoSize = true
            };
            bottomBar.Controls.Add(_lblStatus);
            Controls.Add(bottomBar);

            _scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            Controls.Add(_scrollPanel);

            _lblSubtitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(110, 110, 110),
                Padding = new Padding(24, 8, 24, 0)
            };
            Controls.Add(_lblSubtitle);
        }

        /// <summary>
        /// (Re)builds the grid. baselineChecked, when non-null, marks cells whose default-checked
        /// state came from a system baseline (e.g. role) rather than a stored per-user row — these
        /// get a "✓ Role"-style badge and are still fully editable.
        /// </summary>
        public void Configure(
            string subtitle,
            List<(int UserId, string Name)> users,
            List<(string Key, string DisplayName)> columns,
            Dictionary<(int UserId, string Key), bool> initialChecked,
            Dictionary<(int UserId, string Key), bool> baselineChecked = null)
        {
            _users = users;
            _columns = columns;
            _baselineChecked = baselineChecked;
            _lblSubtitle.Text = subtitle;

            BuildTable(initialChecked);
            SetLockedMode();
        }

        private void BuildTable(Dictionary<(int UserId, string Key), bool> initialChecked)
        {
            _checkboxes = new Dictionary<(int, string), CheckBox>();

            int colCount = _columns.Count;

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1 + colCount,
                RowCount = 1 + _users.Count,
                BackColor = Color.FromArgb(215, 215, 215),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding = new Padding(0)
            };

            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UserColWidth));
            for (int c = 0; c < colCount; c++)
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colCount));

            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
            for (int r = 0; r < _users.Count; r++)
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

            tlp.Controls.Add(MakeHeaderCell("User", leftAlign: true), 0, 0);
            for (int c = 0; c < colCount; c++)
                tlp.Controls.Add(MakeHeaderCell(_columns[c].DisplayName), c + 1, 0);

            for (int r = 0; r < _users.Count; r++)
            {
                var (userId, name) = _users[r];
                Color rowColor = r % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);

                tlp.Controls.Add(MakeUserCell(name, rowColor), 0, r + 1);

                for (int c = 0; c < colCount; c++)
                {
                    string key = _columns[c].Key;
                    bool isChecked = initialChecked.TryGetValue((userId, key), out var v) && v;
                    bool fromBaseline = _baselineChecked != null &&
                        _baselineChecked.TryGetValue((userId, key), out var b) && b;

                    var chk = new CheckBox
                    {
                        Checked = isChecked,
                        Text = "",
                        Dock = DockStyle.Fill,
                        CheckAlign = ContentAlignment.MiddleCenter,
                        BackColor = fromBaseline ? RoleCellBg : rowColor,
                        Cursor = Cursors.Default,
                        Enabled = false
                    };

                    if (fromBaseline)
                    {
                        var tt = new ToolTip();
                        tt.SetToolTip(chk, "Access granted by this user's role assignment. Uncheck to override.");
                    }

                    _checkboxes[(userId, key)] = chk;

                    var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = fromBaseline ? RoleCellBg : rowColor };
                    wrapper.Controls.Add(chk);
                    tlp.Controls.Add(wrapper, c + 1, r + 1);
                }
            }

            int totalHeight = HeaderHeight + RowHeight * _users.Count;

            SendMessage(_scrollPanel.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _scrollPanel.Controls.Clear();
                _scrollPanel.AutoScrollMinSize = new Size(0, totalHeight);
                _scrollPanel.Controls.Add(tlp);
            }
            finally
            {
                SendMessage(_scrollPanel.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                _scrollPanel.PerformLayout();
                _scrollPanel.Refresh();
            }
        }

        private static Label MakeHeaderCell(string text, bool leftAlign = false)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = leftAlign ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter,
                Padding = leftAlign ? new Padding(14, 0, 0, 0) : Padding.Empty
            };
        }

        private static Label MakeUserCell(string name, Color backColor)
        {
            return new Label
            {
                Text = name,
                Dock = DockStyle.Fill,
                BackColor = backColor,
                ForeColor = Color.FromArgb(35, 35, 35),
                Font = new Font("Segoe UI", 9.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            _snapshot = new Dictionary<(int, string), bool>();
            foreach (var kvp in _checkboxes)
                _snapshot[kvp.Key] = kvp.Value.Checked;

            foreach (var chk in _checkboxes.Values)
            {
                chk.Enabled = true;
                chk.Cursor = Cursors.Hand;
            }

            _btnEdit.Visible = false;
            _btnSave.Visible = true;
            _btnCancel.Visible = true;
            _lblStatus.Text = "";
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            foreach (var kvp in _checkboxes)
                kvp.Value.Checked = _snapshot != null && _snapshot.TryGetValue(kvp.Key, out var v) && v;

            SetLockedMode();
            _lblStatus.Text = "";
        }

        private void SetLockedMode()
        {
            foreach (var chk in _checkboxes.Values)
            {
                chk.Enabled = false;
                chk.Cursor = Cursors.Default;
            }

            _btnSave.Enabled = true;
            _btnCancel.Enabled = true;
            _btnSave.Visible = false;
            _btnCancel.Visible = false;
            _btnEdit.Visible = true;
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            _btnSave.Enabled = false;
            _btnCancel.Enabled = false;
            _lblStatus.Text = "Saving...";
            _lblStatus.ForeColor = Color.FromArgb(120, 120, 120);

            try
            {
                var current = new Dictionary<(int, string), bool>();
                foreach (var kvp in _checkboxes)
                    current[kvp.Key] = kvp.Value.Checked;

                bool ok = SaveRequested != null && await SaveRequested.Invoke(current);

                if (ok)
                {
                    SetLockedMode();
                    _lblStatus.Text = "✓  Saved. Users will see updated access on their next login.";
                    _lblStatus.ForeColor = Color.FromArgb(78, 154, 252);
                }
                else
                {
                    _lblStatus.Text = "Save failed.";
                    _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                    _btnSave.Enabled = true;
                    _btnCancel.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Save failed.";
                _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnSave.Enabled = true;
                _btnCancel.Enabled = true;
            }
        }
    }
}
