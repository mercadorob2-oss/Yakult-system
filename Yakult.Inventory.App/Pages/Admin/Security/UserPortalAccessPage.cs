using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;

namespace Yakult.Inventory.App.Pages.Admin.Security
{
    /// <summary>
    /// Admin page for access overrides: Portals (role-based, fully overridable grant/deny),
    /// Pages (per-user), Role Pages (per-role baseline for the same Page permission type — a
    /// per-user override on the Pages tab always wins), Item Categories, and Actions
    /// (default-allow, restrict-by-exception). Portals/Pages/Actions use PermissionGridPanel or
    /// the Portal->Menu->Page panels; Item Categories uses CategoryAccessPanel since
    /// dbo.ItemCategory can have dozens of rows and doesn't fit a grid. This class owns the
    /// load/save I/O for the grid-based tabs.
    /// </summary>
    public partial class UserPortalAccessPage : UserControl
    {
        private readonly string _connectionString;

        private List<(int UserId, string Name)> _users;
        private List<(int PortalId, string PortalKey, string DisplayName)> _portals;

        private PermissionGridPanel _portalsGrid;
        private PageAccessPanel _pagePanel;
        private RolePageAccessPanel _rolePagePanel;
        private CategoryAccessPanel _categoryPanel;
        private PermissionGridPanel _actionsGrid;

        public UserPortalAccessPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildShell();
            _ = LoadAllAsync();
        }

        private void InitializeComponent() { }

        private void BuildShell()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.White, Padding = new Padding(24, 10, 24, 0) };
            var subtitleLabel = new Label
            {
                Text = "Grant or restrict individual users' access, in addition to their role-based access. Changes take effect on the user's next login.",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(110, 110, 110)
            };
            var titleLabel = new Label
            {
                Text = "User Access",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            // Dock=Top stacks with the LAST one added ending up topmost.
            header.Controls.Add(subtitleLabel);
            header.Controls.Add(titleLabel);
            Controls.Add(header);

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

            var tabPortals = new TabPage("Portals");
            _portalsGrid = new PermissionGridPanel();
            _portalsGrid.SaveRequested += SavePortalsAsync;
            tabPortals.Controls.Add(_portalsGrid);
            tabs.TabPages.Add(tabPortals);

            var tabPages = new TabPage("Pages");
            _pagePanel = new PageAccessPanel();
            tabPages.Controls.Add(_pagePanel);
            tabs.TabPages.Add(tabPages);

            var tabRolePages = new TabPage("Role Pages");
            _rolePagePanel = new RolePageAccessPanel();
            tabRolePages.Controls.Add(_rolePagePanel);
            tabs.TabPages.Add(tabRolePages);

            var tabCategories = new TabPage("Item Categories");
            _categoryPanel = new CategoryAccessPanel();
            tabCategories.Controls.Add(_categoryPanel);
            tabs.TabPages.Add(tabCategories);

            var tabActions = new TabPage("Actions");
            _actionsGrid = new PermissionGridPanel();
            _actionsGrid.SaveRequested += grid => SaveItemOverridesAsync("Action", grid);
            tabActions.Controls.Add(_actionsGrid);
            tabs.TabPages.Add(tabActions);

            Controls.Add(tabs);
            tabs.BringToFront();
        }

        private async Task LoadAllAsync()
        {
            try
            {
                _users = new List<(int, string)>();
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(
                        @"SELECT u.UserId, u.Name
                          FROM dbo.[User] u
                          WHERE u.IsActive = 1 AND u.IsDeveloper = 0
                          ORDER BY u.Name", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            _users.Add((r.GetInt32(0), r.GetString(1)));
                }

                await LoadPortalsTabAsync();
                await _pagePanel.LoadAsync(_users, _portals);
                await _rolePagePanel.LoadAsync(_portals);
                await _categoryPanel.LoadAsync(_users);
                await LoadItemTabAsync("Action", _actionsGrid, "Restrict which of these actions a user may perform.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Portals tab ───────────────────────────────────────────────────────

        private async Task LoadPortalsTabAsync()
        {
            var portals = new List<(int PortalId, string PortalKey, string DisplayName)>();
            var currentAccess = new Dictionary<(int, int), bool>(); // explicit UserPortalAccess rows
            var roleAccess = new HashSet<(int, int)>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(
                    "SELECT PortalId, PortalKey, DisplayName FROM dbo.Portal WHERE IsActive = 1 ORDER BY PortalId", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        portals.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));

                _portals = portals;

                using (var cmd = new SqlCommand("SELECT UserId, PortalId, IsGranted FROM dbo.UserPortalAccess", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        currentAccess[(r.GetInt32(0), r.GetInt32(1))] = r.GetBoolean(2);

                const string roleSql = @"
                    SELECT DISTINCT ur.UserId, rpa.PortalId
                    FROM dbo.UserRole ur
                    INNER JOIN dbo.RolePortalAccess rpa ON rpa.RoleId = ur.RoleId
                    INNER JOIN dbo.Role r ON r.RoleId = ur.RoleId
                    WHERE r.IsActive = 1";

                using (var cmd = new SqlCommand(roleSql, con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        roleAccess.Add((r.GetInt32(0), r.GetInt32(1)));
            }

            var columns = portals.Select(p => (Key: p.PortalId.ToString(), p.DisplayName)).ToList();
            var initialChecked = new Dictionary<(int, string), bool>();
            var baselineChecked = new Dictionary<(int, string), bool>();

            foreach (var (userId, _) in _users)
            {
                foreach (var p in portals)
                {
                    bool baseline = roleAccess.Contains((userId, p.PortalId));
                    bool isChecked = currentAccess.TryGetValue((userId, p.PortalId), out var granted) ? granted : baseline;

                    baselineChecked[(userId, p.PortalId.ToString())] = baseline;
                    initialChecked[(userId, p.PortalId.ToString())] = isChecked;
                }
            }

            _portalsGrid.Configure(
                "= Access granted by role (still editable — uncheck to override for this user)",
                _users, columns, initialChecked, baselineChecked);
        }

        private async Task<bool> SavePortalsAsync(Dictionary<(int UserId, string Key), bool> grid)
        {
            try
            {
                var roleAccess = new HashSet<(int, int)>();
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string roleSql = @"
                        SELECT DISTINCT ur.UserId, rpa.PortalId
                        FROM dbo.UserRole ur
                        INNER JOIN dbo.RolePortalAccess rpa ON rpa.RoleId = ur.RoleId
                        INNER JOIN dbo.Role r ON r.RoleId = ur.RoleId
                        WHERE r.IsActive = 1";
                    using (var cmd = new SqlCommand(roleSql, con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            roleAccess.Add((r.GetInt32(0), r.GetInt32(1)));

                    using (var tx = con.BeginTransaction())
                    {
                        foreach (var kvp in grid)
                        {
                            int userId = kvp.Key.UserId;
                            int portalId = int.Parse(kvp.Key.Key);
                            bool @checked = kvp.Value;
                            bool baseline = roleAccess.Contains((userId, portalId));

                            if (@checked == baseline)
                            {
                                using (var del = new SqlCommand(
                                    "DELETE FROM dbo.UserPortalAccess WHERE UserId = @UserId AND PortalId = @PortalId", con, tx))
                                {
                                    del.Parameters.AddWithValue("@UserId", userId);
                                    del.Parameters.AddWithValue("@PortalId", portalId);
                                    await del.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                using (var upsert = new SqlCommand(
                                    @"MERGE dbo.UserPortalAccess AS target
                                      USING (SELECT @UserId AS UserId, @PortalId AS PortalId) AS src
                                      ON target.UserId = src.UserId AND target.PortalId = src.PortalId
                                      WHEN MATCHED THEN UPDATE SET IsGranted = @IsGranted
                                      WHEN NOT MATCHED THEN INSERT (UserId, PortalId, IsGranted) VALUES (@UserId, @PortalId, @IsGranted);", con, tx))
                                {
                                    upsert.Parameters.AddWithValue("@UserId", userId);
                                    upsert.Parameters.AddWithValue("@PortalId", portalId);
                                    upsert.Parameters.AddWithValue("@IsGranted", @checked);
                                    await upsert.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        tx.Commit();
                    }
                }

                PermissionResolver.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ── Pages / Actions tabs (pre-seeded PermissionItem rows) ────────────────

        private async Task LoadItemTabAsync(string permissionType, PermissionGridPanel grid, string subtitle)
        {
            var repo = new PermissionItemRepository();
            var items = await repo.GetPermissionItemsAsync(permissionType);
            var overrides = await repo.GetUserOverridesAsync(permissionType);

            var columns = items.Select(i => (i.ItemKey, i.DisplayName)).ToList();
            var initialChecked = new Dictionary<(int, string), bool>();

            foreach (var (userId, _) in _users)
                foreach (var item in items)
                    initialChecked[(userId, item.ItemKey)] =
                        !overrides.TryGetValue((userId, item.ItemKey), out var granted) || granted;

            grid.Configure(subtitle, _users, columns, initialChecked);
        }

        private async Task<bool> SaveItemOverridesAsync(string permissionType, Dictionary<(int UserId, string Key), bool> grid)
        {
            try
            {
                var repo = new PermissionItemRepository();
                var toSave = grid.Select(kvp => (
                    UserId: kvp.Key.UserId,
                    ItemKey: kvp.Key.Key,
                    DisplayName: kvp.Key.Key,
                    Value: kvp.Value ? (bool?)null : false));

                await repo.SaveUserOverridesAsync(permissionType, toSave);
                PermissionResolver.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
