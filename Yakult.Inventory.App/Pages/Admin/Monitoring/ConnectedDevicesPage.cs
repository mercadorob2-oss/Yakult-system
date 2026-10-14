using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.Monitoring
{
    public sealed class ConnectedDeviceDto
    {
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string ClientVersion { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public bool Online { get; set; }
        public string StatusDisplay => Online ? "● Online" : "○ Stale";
        public string LastSeenDisplay => LastSeenUtc == DateTime.MinValue ? "" : LastSeenUtc.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss");
        public string FirstSeenDisplay => FirstSeenUtc == DateTime.MinValue ? "" : FirstSeenUtc.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss");
    }

    /// <summary>
    /// Admin-only live view of desktops beating into dbo.CallClientPresence
    /// (same data as the ITCM server Devices tab, Workspace module only).
    /// Each row has a STOP button that queues a surprise STOP alert for that PC.
    /// </summary>
    public partial class ConnectedDevicesPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView _dgv;
        private HopeButton _btnRefresh;
        private Label _lblSummary;
        private Label _lblStatus;
        private Timer _refreshTimer;

        private List<ConnectedDeviceDto> _all = new List<ConnectedDeviceDto>();
        private List<ConnectedDeviceDto> _filtered = new List<ConnectedDeviceDto>();
        private readonly string _connectionString;
        private string _presenceError;

        public ConnectedDevicesPage()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUI();
            LoadData();
            Disposed += (_, __) =>
            {
                try { if (_refreshTimer != null) { _refreshTimer.Stop(); _refreshTimer.Dispose(); } } catch { }
            };
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Connected Devices",
                "Search by machine or user...",
                (s, e) => ApplyFilters(),
                () => LoadData());

            DefaultListPageTemplate.HideSortDropdown(_layout);

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[] { "All", "Online Only", "Stale Only" });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilters();
            }

            _btnRefresh = new HopeButton
            {
                Text = "Refresh",
                Font = UiTheme.Fonts.Button,
                Size = new Size(150, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnRefresh, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnRefresh.Click += (s, e) => LoadData();

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnRefresh });

            _lblSummary = new Label { Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(32, 38, 53), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            if (_layout.SummaryPanel != null)
            {
                _layout.SummaryPanel.Controls.Clear();
                _layout.SummaryPanel.Controls.Add(_lblSummary);
                _layout.SummaryPanel.Visible = true;
            }

            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            UiFactory.StyleGrid(_dgv);
            _dgv.CellFormatting += Dgv_CellFormatting;
            _dgv.CellContentClick += Dgv_CellContentClick;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DataPropertyName = "StatusDisplay", FillWeight = 12 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MachineName", HeaderText = "Machine", DataPropertyName = "MachineName", FillWeight = 20 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserName", HeaderText = "User", DataPropertyName = "UserName", FillWeight = 22 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ClientVersion", HeaderText = "Version", DataPropertyName = "ClientVersion", FillWeight = 12 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "FirstSeenDisplay", HeaderText = "First seen", DataPropertyName = "FirstSeenDisplay", FillWeight = 17 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastSeenDisplay", HeaderText = "Last seen", DataPropertyName = "LastSeenDisplay", FillWeight = 17 });
            _dgv.Columns.Add(new DataGridViewButtonColumn { Name = "StopAction", HeaderText = "Action", Text = "STOP", UseColumnTextForButtonValue = true, FillWeight = 10 });

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgv);
            }

            _lblStatus = new Label { AutoSize = true, Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(103, 112, 129), Margin = new Padding(12, 10, 0, 0) };
            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.Add(_lblStatus);
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);
            ResumeLayout(false);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _dgv, _btnRefresh);

            _refreshTimer = new Timer { Interval = 15000 };
            _refreshTimer.Tick += (_, __) => LoadData();
            _refreshTimer.Start();
        }

        private async void LoadData()
        {
            _presenceError = null;
            try
            {
                var list = new List<ConnectedDeviceDto>();
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string guard = "SELECT CASE WHEN OBJECT_ID('dbo.CallClientPresence') IS NOT NULL THEN 1 ELSE 0 END";
                    using (var cmd = new SqlCommand(guard, con))
                    {
                        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
                        {
                            _presenceError = "dbo.CallClientPresence not found. Desktops must beat at least once.";
                            _all = list;
                            ApplyFilters();
                            return;
                        }
                    }
                    const string sql = @"
                        SELECT MachineName, UserName, ClientVersion, FirstSeenUtc, LastSeenUtc,
                               CASE WHEN LastSeenUtc >= DATEADD(minute, -15, SYSUTCDATETIME()) THEN 1 ELSE 0 END AS Online
                        FROM dbo.CallClientPresence
                        WHERE Module = 'Workspace'
                        ORDER BY Online DESC, LastSeenUtc DESC";
                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ConnectedDeviceDto
                            {
                                MachineName = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                UserName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                ClientVersion = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                FirstSeenUtc = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                                LastSeenUtc = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                                Online = !reader.IsDBNull(5) && reader.GetInt32(5) == 1
                            });
                        }
                    }
                }
                _all = list;
            }
            catch (Exception ex)
            {
                _presenceError = "Could not load presence: " + ex.Message;
                _all = new List<ConnectedDeviceDto>();
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_dgv == null) return;
            string query = (_layout.SearchBox?.Text ?? "").Trim();
            string mode = (_layout.FilterByComboBox?.SelectedItem as string) ?? "All";

            _filtered = _all.Where(d =>
                (mode == "All"
                    || (mode == "Online Only" && d.Online)
                    || (mode == "Stale Only" && !d.Online))
                && (query.Length == 0
                    || ((d.MachineName + " " + d.UserName).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
            ).ToList();

            _dgv.DataSource = null;
            _dgv.DataSource = _filtered;

            int online = _all.Count(d => d.Online);
            if (_lblSummary != null)
                _lblSummary.Text = _presenceError ?? (online + " online / " + _all.Count + " known");
            if (_lblStatus != null)
                _lblStatus.Text = _presenceError ?? (_filtered.Count + " shown. STOP queues a surprise alert for that PC.");
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
            var row = _filtered[e.RowIndex];
            var col = _dgv.Columns[e.ColumnIndex].Name;
            if (col == "Status")
            {
                e.CellStyle.ForeColor = row.Online ? Color.FromArgb(39, 174, 96) : Color.FromArgb(127, 140, 141);
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            else if (col == "MachineName"
                && string.Equals(row.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                e.Value = row.MachineName + "  (this device)";
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
        }

        private void Dgv_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
            if (_dgv.Columns[e.ColumnIndex].Name != "StopAction") return;
            var target = _filtered[e.RowIndex];

            if (!(Yakult.Inventory.App.Session.AppSession.IsDeveloper
                || Yakult.Inventory.App.Session.AppSession.IsSuperAdmin
                || Yakult.Inventory.App.Session.AppSession.IsAdmin))
            {
                MessageBox.Show("Access denied. Admin privileges required to send alerts.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "Send STOP prank alert to " + target.MachineName + " (" + target.UserName + ")?",
                "Send STOP", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            int id = new Repositories.AdminAlertRepository().SendStopAlert(
                target.MachineName, target.UserName,
                Yakult.Inventory.App.Session.AppSession.CurrentUserId,
                Yakult.Inventory.App.Session.AppSession.CurrentUserName);
            if (_lblStatus != null)
                _lblStatus.Text = id > 0
                    ? "STOP queued for " + target.MachineName + ". It pops there within ~20 seconds."
                    : "Could not queue STOP. Check the database connection.";
        }
    }
}
