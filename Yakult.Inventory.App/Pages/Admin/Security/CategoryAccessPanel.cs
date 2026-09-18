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
    /// Per-user item-category restriction editor. dbo.ItemCategory can have dozens of rows, so
    /// unlike PermissionGridPanel this does not render one column per category — an admin picks
    /// a user, then searches for and adds specific categories to restrict for that user.
    /// Restricted categories are shown as removable chips; changes apply immediately.
    /// </summary>
    public partial class CategoryAccessPanel : UserControl
    {
        private readonly PermissionItemRepository _repo = new PermissionItemRepository();

        private List<(int UserId, string Name)> _users = new List<(int, string)>();
        private List<string> _allCategories = new List<string>();
        private Dictionary<int, HashSet<string>> _restrictedByUser = new Dictionary<int, HashSet<string>>();

        private ComboBox _cmbUser;
        private ComboBox _cmbAddCategory;
        private Button _btnAdd;
        private FlowLayoutPanel _chipsPanel;
        private Label _lblEmptyState;
        private Label _lblStatus;
        private Label _lblRestrictedHeader;

        private static readonly Color CardBorder = Color.FromArgb(226, 230, 235);
        private static readonly Color ChipBg = Color.FromArgb(255, 235, 235);
        private static readonly Color ChipBorder = Color.FromArgb(235, 190, 190);
        private static readonly Color ChipText = Color.FromArgb(160, 50, 50);
        private static readonly Color AccentBlue = Color.FromArgb(78, 154, 252);

        public CategoryAccessPanel()
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
                Text = "Restrict which item categories a user may add via the batch Add Item / Add Request dialogs. Changes apply immediately.",
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(110, 110, 110),
                Padding = new Padding(24, 8, 24, 0)
            };

            // ── User picker card ──────────────────────────────────────────────
            var userCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Margin = new Padding(24, 12, 24, 0),
                BackColor = Color.White
            };
            var userCardBorder = MakeCard(userCard);
            userCard.Controls.Add(new Label
            {
                Text = "USER",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 140, 140),
                Location = new Point(16, 8),
                AutoSize = true
            });
            _cmbUser = new ComboBox
            {
                Location = new Point(16, 28),
                Width = 320,
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            _cmbUser.SelectedIndexChanged += (s, e) => RefreshForSelectedUser();
            userCard.Controls.Add(_cmbUser);

            // ── Add-restriction card ──────────────────────────────────────────
            var addCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Margin = new Padding(24, 12, 24, 0),
                BackColor = Color.White
            };
            MakeCard(addCard);
            addCard.Controls.Add(new Label
            {
                Text = "SEARCH CATEGORY TO RESTRICT",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 140, 140),
                Location = new Point(16, 8),
                AutoSize = true
            });
            _cmbAddCategory = new ComboBox
            {
                Location = new Point(16, 28),
                Width = 320,
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            addCard.Controls.Add(_cmbAddCategory);
            _btnAdd = new Button
            {
                Text = "+  Restrict",
                Location = new Point(346, 26),
                Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAdd.FlatAppearance.BorderSize = 0;
            _btnAdd.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 130, 225);
            _btnAdd.Click += BtnAdd_Click;
            addCard.Controls.Add(_btnAdd);
            _lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = AccentBlue,
                Location = new Point(470, 34),
                AutoSize = true
            };
            addCard.Controls.Add(_lblStatus);

            // ── Restricted chips section ──────────────────────────────────────
            _lblRestrictedHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Padding = new Padding(24, 12, 24, 0),
                Text = "Restricted categories"
            };

            var chipsWrap = new Panel
            {
                Dock = DockStyle.Top,
                Height = 180,
                Margin = new Padding(24, 4, 24, 0),
                BackColor = Color.White
            };
            MakeCard(chipsWrap);
            _chipsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(12),
                BackColor = Color.White
            };
            chipsWrap.Controls.Add(_chipsPanel);

            _lblEmptyState = new Label
            {
                Text = "No restrictions for this user — all categories are available.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(160, 160, 160)
            };
            _chipsPanel.Controls.Add(_lblEmptyState);

            var filler = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Dock=Top controls stack with the LAST one added ending up topmost.
            Controls.Add(filler);
            Controls.Add(chipsWrap);
            Controls.Add(_lblRestrictedHeader);
            Controls.Add(addCard);
            Controls.Add(userCard);
            Controls.Add(subtitle);
        }

        /// <summary>Draws a subtle 1px border + light background around a card-style panel.</summary>
        private Panel MakeCard(Panel card)
        {
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBorder))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            card.BackColor = Color.FromArgb(250, 251, 252);
            return card;
        }

        public async Task LoadAsync(List<(int UserId, string Name)> users)
        {
            _users = users;
            _allCategories = await _repo.GetItemCategoryNamesAsync();

            var overrides = await _repo.GetUserOverridesAsync("ItemCategory");
            _restrictedByUser = new Dictionary<int, HashSet<string>>();
            foreach (var kvp in overrides)
            {
                if (kvp.Value) continue; // IsGranted=true override is a no-op re-grant, not a restriction
                var (userId, key) = kvp.Key;
                if (!_restrictedByUser.TryGetValue(userId, out var set))
                    _restrictedByUser[userId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(key);
            }

            _cmbUser.Items.Clear();
            foreach (var (_, name) in _users)
                _cmbUser.Items.Add(name);

            if (_cmbUser.Items.Count > 0)
                _cmbUser.SelectedIndex = 0;
            else
                RefreshForSelectedUser();
        }

        private void RefreshForSelectedUser()
        {
            if (_cmbUser.SelectedIndex < 0 || _cmbUser.SelectedIndex >= _users.Count)
            {
                _lblRestrictedHeader.Text = "Restricted categories";
                RenderChips(new List<string>());
                _cmbAddCategory.Items.Clear();
                return;
            }

            var (userId, name) = _users[_cmbUser.SelectedIndex];
            _lblRestrictedHeader.Text = $"Restricted categories for {name}";

            var restricted = _restrictedByUser.TryGetValue(userId, out var set)
                ? set
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RenderChips(restricted.OrderBy(c => c).ToList());

            _cmbAddCategory.Items.Clear();
            foreach (var cat in _allCategories.Where(c => !restricted.Contains(c)))
                _cmbAddCategory.Items.Add(cat);
            _cmbAddCategory.Text = "";
        }

        private void RenderChips(List<string> restrictedCategories)
        {
            _chipsPanel.SuspendLayout();
            _chipsPanel.Controls.Clear();

            if (restrictedCategories.Count == 0)
            {
                _chipsPanel.Controls.Add(_lblEmptyState);
            }
            else
            {
                foreach (var category in restrictedCategories)
                    _chipsPanel.Controls.Add(MakeChip(category));
            }

            _chipsPanel.ResumeLayout();
        }

        private Panel MakeChip(string category)
        {
            var lbl = new Label
            {
                Text = category,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = ChipText,
                Location = new Point(12, 6),
                BackColor = Color.Transparent
            };

            var btnRemove = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(20, 20),
                BackColor = Color.Transparent,
                ForeColor = ChipText,
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 210, 210);
            btnRemove.Click += (s, e) => RemoveRestriction(category);

            int chipWidth = lbl.PreferredWidth + 12 + btnRemove.Width + 10;
            var chip = new Panel
            {
                Size = new Size(chipWidth, 32),
                BackColor = ChipBg,
                Margin = new Padding(4)
            };
            chip.Paint += (s, e) =>
            {
                using (var pen = new Pen(ChipBorder))
                    e.Graphics.DrawRectangle(pen, 0, 0, chip.Width - 1, chip.Height - 1);
            };

            btnRemove.Location = new Point(chipWidth - btnRemove.Width - 6, 6);

            chip.Controls.Add(lbl);
            chip.Controls.Add(btnRemove);
            return chip;
        }

        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            if (_cmbUser.SelectedIndex < 0)
            {
                MessageBox.Show("Select a user first.", "No User Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var (userId, name) = _users[_cmbUser.SelectedIndex];
            string category = _allCategories.FirstOrDefault(c =>
                string.Equals(c, _cmbAddCategory.Text, StringComparison.OrdinalIgnoreCase));

            if (category == null)
            {
                MessageBox.Show("Select a valid category from the list.", "Invalid Category", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _btnAdd.Enabled = false;
            try
            {
                await _repo.SaveUserOverridesAsync("ItemCategory", new[] { (userId, category, category, (bool?)false) });
                PermissionResolver.Invalidate();

                if (!_restrictedByUser.TryGetValue(userId, out var set))
                    _restrictedByUser[userId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(category);

                RefreshForSelectedUser();
                _lblStatus.Text = $"✓  Restricted \"{category}\" for {name}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnAdd.Enabled = true;
            }
        }

        private async void RemoveRestriction(string category)
        {
            if (_cmbUser.SelectedIndex < 0) return;
            var (userId, name) = _users[_cmbUser.SelectedIndex];

            try
            {
                await _repo.SaveUserOverridesAsync("ItemCategory", new[] { (userId, category, category, (bool?)null) });
                PermissionResolver.Invalidate();

                if (_restrictedByUser.TryGetValue(userId, out var set))
                    set.Remove(category);

                RefreshForSelectedUser();
                _lblStatus.Text = $"✓  Removed restriction on \"{category}\" for {name}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
