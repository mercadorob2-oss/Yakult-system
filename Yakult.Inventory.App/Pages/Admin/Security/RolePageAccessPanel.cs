using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;

namespace Yakult.Inventory.App.Pages.Admin.Security
{
    /// <summary>
    /// Role-level Page access editor — same Portal (tabs) -> Main Menu (dropdown) -> Pages
    /// structure as PageAccessPanel, but sets the baseline for a whole Role (dbo.RolePermissionItem)
    /// instead of a single user. A per-user override in the Pages tab always wins over this
    /// baseline (see Security.PermissionResolver.CheckItemOverride).
    /// </summary>
    public partial class RolePageAccessPanel : UserControl
    {
        private List<(string Key, string DisplayName, string Portal, string Menu)> _pageCatalog =
            new List<(string, string, string, string)>();

        private readonly PermissionItemRepository _repo = new PermissionItemRepository();

        private List<(int RoleId, string Name)> _roles = new List<(int, string)>();
        private List<(int PortalId, string PortalKey, string DisplayName)> _portals = new List<(int, string, string)>();
        private Dictionary<(int RoleId, string PageKey), bool> _overrides = new Dictionary<(int, string), bool>();

        private ComboBox _cmbRole;
        private FlowLayoutPanel _portalBar;
        private ComboBox _cmbMenu;
        private FlowLayoutPanel _pagesList;
        private Label _lblEmptyState;
        private Button _btnSave;
        private Button _btnRestore;
        private Label _lblStatus;

        private string _selectedPortalKey;
        private readonly Dictionary<string, Button> _portalButtons = new Dictionary<string, Button>();

        private static readonly Color AccentBlue = Color.FromArgb(78, 154, 252);
        private static readonly Color CardBorder = Color.FromArgb(226, 230, 235);

        public RolePageAccessPanel()
        {
            InitializeComponent();
            BuildShell();
        }

        private void InitializeComponent() { }

        private void BuildShell()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var subtitle = new Label
            {
                Text = "Set the default page access for everyone in a role. A per-user override on the Pages tab always wins over this.",
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(110, 110, 110),
                Padding = new Padding(24, 8, 24, 0)
            };

            var roleRow = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(24, 8, 24, 0), BackColor = Color.White };
            roleRow.Controls.Add(new Label
            {
                Text = "Role:",
                AutoSize = false,
                Size = new Size(100, 24),
                Location = new Point(0, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            });
            _cmbRole = new ComboBox
            {
                Location = new Point(104, 4),
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F)
            };
            _cmbRole.SelectedIndexChanged += (s, e) => RefreshPagesList();
            roleRow.Controls.Add(_cmbRole);

            var portalHeader = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(24, 8, 24, 0), BackColor = Color.White };
            _portalBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };
            portalHeader.Controls.Add(_portalBar);

            var menuRow = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(24, 8, 24, 0), BackColor = Color.White };
            menuRow.Controls.Add(new Label
            {
                Text = "Main Menu:",
                AutoSize = false,
                Size = new Size(100, 24),
                Location = new Point(0, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            });
            _cmbMenu = new ComboBox
            {
                Location = new Point(104, 4),
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F)
            };
            _cmbMenu.SelectedIndexChanged += (s, e) => RefreshPagesList();
            menuRow.Controls.Add(_cmbMenu);

            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.White };
            bottomBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBorder))
                    e.Graphics.DrawLine(pen, 0, 0, ((Panel)s).Width, 0);
            };
            _btnRestore = new Button
            {
                Text = "Restore to Default",
                Font = new Font("Segoe UI", 9.5F),
                Size = new Size(150, 36),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand
            };
            _btnRestore.FlatAppearance.BorderSize = 0;
            _btnRestore.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
            _btnRestore.Click += BtnRestore_Click;
            bottomBar.Controls.Add(_btnRestore);

            _btnSave = new Button
            {
                Text = "Save",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size = new Size(110, 36),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 130, 225);
            _btnSave.Click += BtnSave_Click;
            bottomBar.Controls.Add(_btnSave);
            _lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9F),
                ForeColor = AccentBlue,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(24, 0, 0, 0)
            };
            bottomBar.Controls.Add(_lblStatus);
            _lblStatus.BringToFront();

            var listCard = new Panel { Dock = DockStyle.Fill, Margin = new Padding(24, 8, 24, 8), Padding = new Padding(24, 8, 24, 8), BackColor = Color.White };
            _pagesList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            listCard.Controls.Add(_pagesList);

            _lblEmptyState = new Label
            {
                Text = "No restrictable pages under this portal yet.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(160, 160, 160)
            };

            // Dock=Top controls stack with the LAST one added ending up topmost.
            Controls.Add(bottomBar);
            Controls.Add(listCard);
            Controls.Add(menuRow);
            Controls.Add(portalHeader);
            Controls.Add(roleRow);
            Controls.Add(subtitle);
        }

        public async Task LoadAsync(List<(int PortalId, string PortalKey, string DisplayName)> portals)
        {
            _portals = portals;
            _roles = await _repo.GetRolesAsync();

            var navEntries = await _repo.GetPageNavEntriesAsync();
            _pageCatalog = navEntries.Select(n => (n.ItemKey, n.DisplayName, n.PortalKey, n.MenuGroup)).ToList();

            var overrides = await _repo.GetRoleOverridesAsync("Page");
            _overrides = overrides;

            _cmbRole.Items.Clear();
            foreach (var (_, name) in _roles)
                _cmbRole.Items.Add(name);

            BuildPortalBar();

            if (_cmbRole.Items.Count > 0)
                _cmbRole.SelectedIndex = 0;

            SelectDefaultPortal();
        }

        private void BuildPortalBar()
        {
            _portalBar.Controls.Clear();
            _portalButtons.Clear();

            foreach (var p in _portals)
            {
                bool hasPages = _pageCatalog.Any(pc => pc.Portal == p.PortalKey);

                var btn = new Button
                {
                    Text = p.DisplayName,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Height = 40,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(14, 0, 14, 0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(235, 238, 242),
                    ForeColor = hasPages ? Color.FromArgb(60, 60, 60) : Color.FromArgb(180, 180, 180),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 4, 0),
                    Tag = p.PortalKey
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) => SelectPortal((string)((Button)s).Tag);
                _portalBar.Controls.Add(btn);
                _portalButtons[p.PortalKey] = btn;
            }
        }

        private void SelectDefaultPortal()
        {
            string defaultPortal = _portals.Select(p => p.PortalKey).FirstOrDefault(k => _pageCatalog.Any(pc => pc.Portal == k))
                ?? _portals.Select(p => p.PortalKey).FirstOrDefault();

            if (defaultPortal != null)
                SelectPortal(defaultPortal);
        }

        private void SelectPortal(string portalKey)
        {
            _selectedPortalKey = portalKey;

            foreach (var kvp in _portalButtons)
            {
                bool selected = kvp.Key == portalKey;
                kvp.Value.BackColor = selected ? AccentBlue : Color.FromArgb(235, 238, 242);
                kvp.Value.ForeColor = selected
                    ? Color.White
                    : (_pageCatalog.Any(pc => pc.Portal == kvp.Key) ? Color.FromArgb(60, 60, 60) : Color.FromArgb(180, 180, 180));
            }

            var menus = _pageCatalog.Where(pc => pc.Portal == portalKey).Select(pc => pc.Menu).Distinct().ToList();

            _cmbMenu.Items.Clear();
            foreach (var m in menus)
                _cmbMenu.Items.Add(m);

            if (_cmbMenu.Items.Count > 0)
                _cmbMenu.SelectedIndex = 0;
            else
                RefreshPagesList();
        }

        private void RefreshPagesList()
        {
            _pagesList.Controls.Clear();

            if (_cmbRole.SelectedIndex < 0 || _selectedPortalKey == null || _cmbMenu.SelectedItem == null)
            {
                _pagesList.Controls.Add(_lblEmptyState);
                return;
            }

            string menu = (string)_cmbMenu.SelectedItem;
            var (roleId, _) = _roles[_cmbRole.SelectedIndex];

            var pages = _pageCatalog.Where(pc => pc.Portal == _selectedPortalKey && pc.Menu == menu).ToList();

            if (pages.Count == 0)
            {
                _pagesList.Controls.Add(_lblEmptyState);
                return;
            }

            foreach (var page in pages)
            {
                bool isChecked = !_overrides.TryGetValue((roleId, page.Key), out var granted) || granted;

                var chk = new CheckBox
                {
                    Text = page.DisplayName,
                    Checked = isChecked,
                    Font = new Font("Segoe UI", 9.5F),
                    AutoSize = true,
                    Margin = new Padding(4, 6, 4, 6),
                    Tag = page.Key
                };
                _pagesList.Controls.Add(chk);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (_cmbRole.SelectedIndex < 0)
            {
                MessageBox.Show("Select a role first.", "No Role Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var (roleId, name) = _roles[_cmbRole.SelectedIndex];

            var toSave = new List<(int RoleId, string ItemKey, string DisplayName, bool? Value)>();
            foreach (CheckBox chk in _pagesList.Controls.OfType<CheckBox>())
            {
                string pageKey = (string)chk.Tag;
                string displayName = _pageCatalog.First(pc => pc.Key == pageKey).DisplayName;
                toSave.Add((roleId, pageKey, displayName, chk.Checked ? (bool?)null : false));
            }

            if (toSave.Count == 0) return;

            _btnSave.Enabled = false;
            try
            {
                await _repo.SaveRoleOverridesAsync("Page", toSave);
                PermissionResolver.Invalidate();

                foreach (var (rid, pageKey, _, value) in toSave)
                {
                    if (value == null)
                        _overrides.Remove((rid, pageKey));
                    else
                        _overrides[(rid, pageKey)] = value.Value;
                }

                _lblStatus.Text = $"✓  Saved default page access for the {name} role.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnSave.Enabled = true;
            }
        }

        private async void BtnRestore_Click(object sender, EventArgs e)
        {
            if (_cmbRole.SelectedIndex < 0)
            {
                MessageBox.Show("Select a role first.", "No Role Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var (roleId, name) = _roles[_cmbRole.SelectedIndex];

            var confirm = MessageBox.Show(
                $"Restore default page access for the {name} role?\n\nThis removes ALL page restrictions set for this role (across every portal/menu), not just the ones currently shown.",
                "Restore to Default", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            _btnRestore.Enabled = false;
            try
            {
                await _repo.ClearRoleOverridesAsync("Page", roleId);
                PermissionResolver.Invalidate();

                var keysToRemove = _overrides.Keys.Where(k => k.RoleId == roleId).ToList();
                foreach (var key in keysToRemove)
                    _overrides.Remove(key);

                RefreshPagesList();
                _lblStatus.Text = $"✓  Restored default page access for the {name} role.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore defaults:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRestore.Enabled = true;
            }
        }
    }
}
