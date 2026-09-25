using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.UserAccess.ViewModels
{
    /// <summary>
    /// WPF version of Pages\Admin\Security\UserPortalAccessPage (Admin Portal > User Access).
    /// Tabs: Portals (role-based, fully overridable grant/deny), Pages (per-user), Role Pages
    /// (per-role baseline; a per-user override on Pages always wins), Item Categories, and
    /// Actions (default-allow, restrict-by-exception). Owns the load/save I/O for the grid tabs;
    /// the Pages / Role Pages / Item Categories editors save through PermissionItemRepository.
    /// </summary>
    public sealed class UserAccessViewModel : ViewModelBase
    {
        private readonly string _cs = DatabaseConfig.ConnectionString;
        private readonly PermissionItemRepository _repo = new PermissionItemRepository();

        private List<(int UserId, string Name)> _users = new List<(int, string)>();
        private List<(int PortalId, string PortalKey, string DisplayName)> _portals = new List<(int, string, string)>();
        private bool _isLoading;

        public PermissionGridViewModel PortalsGrid { get; } = new PermissionGridViewModel();
        public PermissionGridViewModel ActionsGrid { get; } = new PermissionGridViewModel();
        public PageAccessEditorViewModel PagesEditor { get; } = new PageAccessEditorViewModel(isRole: false);
        public PageAccessEditorViewModel RolePagesEditor { get; } = new PageAccessEditorViewModel(isRole: true);
        public CategoryAccessViewModel Categories { get; } = new CategoryAccessViewModel();

        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        public UserAccessViewModel()
        {
            PortalsGrid.SaveHandler = SavePortalsAsync;
            ActionsGrid.SaveHandler = grid => SaveItemOverridesAsync("Action", grid);
        }

        public async Task LoadAllAsync()
        {
            IsLoading = true;
            try
            {
                _users = new List<(int, string)>();
                using (var con = new SqlConnection(_cs))
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

                var navEntries = await _repo.GetPageNavEntriesAsync();
                var catalog = navEntries.Select(n => new PageCatalogEntry(n.ItemKey, n.DisplayName, n.PortalKey, n.MenuGroup)).ToList();

                var userPageOverrides = await _repo.GetUserOverridesAsync("Page");
                PagesEditor.Load(
                    _users.Select(u => new SubjectItem(u.UserId, u.Name)).ToList(),
                    _portals, catalog,
                    userPageOverrides.ToDictionary(k => (k.Key.UserId, k.Key.ItemKey), v => v.Value));

                var roles = await _repo.GetRolesAsync();
                var rolePageOverrides = await _repo.GetRoleOverridesAsync("Page");
                RolePagesEditor.Load(
                    roles.Select(r => new SubjectItem(r.RoleId, r.RoleName)).ToList(),
                    _portals, catalog,
                    rolePageOverrides.ToDictionary(k => (k.Key.RoleId, k.Key.ItemKey), v => v.Value));

                var categories = await _repo.GetItemCategoryNamesAsync();
                var categoryOverrides = await _repo.GetUserOverridesAsync("ItemCategory");
                Categories.Load(_users.Select(u => new SubjectItem(u.UserId, u.Name)).ToList(), categories, categoryOverrides);

                await LoadItemTabAsync("Action", ActionsGrid, "Restrict which of these actions a user may perform.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Portals tab ───────────────────────────────────────────────────────

        private const string RolePortalSql = @"
            SELECT DISTINCT ur.UserId, rpa.PortalId
            FROM dbo.UserRole ur
            INNER JOIN dbo.RolePortalAccess rpa ON rpa.RoleId = ur.RoleId
            INNER JOIN dbo.Role r ON r.RoleId = ur.RoleId
            WHERE r.IsActive = 1";

        private async Task LoadPortalsTabAsync()
        {
            var portals = new List<(int PortalId, string PortalKey, string DisplayName)>();
            var currentAccess = new Dictionary<(int, int), bool>(); // explicit UserPortalAccess rows
            var roleAccess = new HashSet<(int, int)>();

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(
                    "SELECT PortalId, PortalKey, DisplayName FROM dbo.Portal WHERE IsActive = 1 ORDER BY PortalId", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        portals.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));

                using (var cmd = new SqlCommand("SELECT UserId, PortalId, IsGranted FROM dbo.UserPortalAccess", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        currentAccess[(r.GetInt32(0), r.GetInt32(1))] = r.GetBoolean(2);

                using (var cmd = new SqlCommand(RolePortalSql, con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        roleAccess.Add((r.GetInt32(0), r.GetInt32(1)));
            }

            _portals = portals;

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

            PortalsGrid.Configure(
                "Blue cell = access granted by role (still editable, uncheck to override for this user)",
                _users, columns, initialChecked, baselineChecked);
        }

        private async Task<bool> SavePortalsAsync(Dictionary<(int UserId, string Key), bool> grid)
        {
            try
            {
                var roleAccess = new HashSet<(int, int)>();
                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(RolePortalSql, con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            roleAccess.Add((r.GetInt32(0), r.GetInt32(1)));

                    using (var tx = con.BeginTransaction())
                    {
                        foreach (var kvp in grid)
                        {
                            int userId = kvp.Key.UserId;
                            int portalId = int.Parse(kvp.Key.Key);
                            bool isChecked = kvp.Value;
                            bool baseline = roleAccess.Contains((userId, portalId));

                            if (isChecked == baseline)
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
                                    upsert.Parameters.AddWithValue("@IsGranted", isChecked);
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
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ── Actions tab (pre-seeded PermissionItem rows) ─────────────────────────

        private async Task LoadItemTabAsync(string permissionType, PermissionGridViewModel grid, string subtitle)
        {
            var items = await _repo.GetPermissionItemsAsync(permissionType);
            var overrides = await _repo.GetUserOverridesAsync(permissionType);

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
                var toSave = grid.Select(kvp => (
                    UserId: kvp.Key.UserId,
                    ItemKey: kvp.Key.Key,
                    DisplayName: kvp.Key.Key,
                    Value: kvp.Value ? (bool?)null : false));

                await _repo.SaveUserOverridesAsync(permissionType, toSave);
                PermissionResolver.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // User x Column checkbox grid (Portals / Actions) with search + pagination
    // ══════════════════════════════════════════════════════════════════════════

    public sealed class GridCellViewModel : ViewModelBase
    {
        private bool _isChecked;

        public GridCellViewModel(string key, bool isChecked, bool isBaseline)
        {
            Key = key;
            _isChecked = isChecked;
            IsBaseline = isBaseline;
        }

        public string Key { get; }

        /// <summary>True when the default-checked state came from the user's role (shown shaded blue).</summary>
        public bool IsBaseline { get; }

        public bool IsChecked { get => _isChecked; set => SetField(ref _isChecked, value); }
    }

    public sealed class GridRowViewModel
    {
        public GridRowViewModel(int userId, string name, List<GridCellViewModel> cells)
        {
            UserId = userId;
            Name = name;
            Cells = cells;
        }

        public int UserId { get; }
        public string Name { get; }
        public List<GridCellViewModel> Cells { get; }
    }

    public sealed class PermissionGridViewModel : ViewModelBase
    {
        public const int PageSize = 15;

        private List<GridRowViewModel> _allRows = new List<GridRowViewModel>();
        private List<GridRowViewModel> _filtered = new List<GridRowViewModel>();
        private Dictionary<(int, string), bool> _snapshot;
        private int _currentPage = 1;
        private int _totalPages = 1;

        private string _subtitle = string.Empty;
        private string _searchText = string.Empty;
        private bool _isEditing;
        private bool _isSaving;
        private string _statusText = string.Empty;
        private bool _statusIsError;
        private string _pageInfo = "Page 1 of 1  (0 users)";
        private bool _canGoPrev;
        private bool _canGoNext;

        /// <summary>Persists the whole grid (every user, not just the current page). Returns true on success.</summary>
        public Func<Dictionary<(int UserId, string Key), bool>, Task<bool>> SaveHandler { get; set; }

        /// <summary>Raised after Configure() so the view can rebuild its DataGrid columns.</summary>
        public event EventHandler ColumnsChanged;

        public List<(string Key, string DisplayName)> Columns { get; private set; } = new List<(string, string)>();
        public ObservableCollection<GridRowViewModel> PagedRows { get; } = new ObservableCollection<GridRowViewModel>();

        public string Subtitle      { get => _subtitle;      private set => SetField(ref _subtitle, value); }
        public bool   IsSaving      { get => _isSaving;      private set => SetField(ref _isSaving, value); }
        public string StatusText    { get => _statusText;    private set => SetField(ref _statusText, value); }
        public bool   StatusIsError { get => _statusIsError; private set => SetField(ref _statusIsError, value); }
        public string PageInfo      { get => _pageInfo;      private set => SetField(ref _pageInfo, value); }
        public bool   CanGoPrev     { get => _canGoPrev;     private set => SetField(ref _canGoPrev, value); }
        public bool   CanGoNext     { get => _canGoNext;     private set => SetField(ref _canGoNext, value); }

        public bool IsEditing
        {
            get => _isEditing;
            private set { if (SetField(ref _isEditing, value)) OnPropertyChanged(nameof(IsNotEditing)); }
        }
        public bool IsNotEditing => !_isEditing;

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        public void Configure(
            string subtitle,
            List<(int UserId, string Name)> users,
            List<(string Key, string DisplayName)> columns,
            Dictionary<(int UserId, string Key), bool> initialChecked,
            Dictionary<(int UserId, string Key), bool> baselineChecked = null)
        {
            Subtitle = subtitle;
            Columns = columns;

            _allRows = users.Select(u => new GridRowViewModel(
                u.UserId, u.Name,
                columns.Select(c => new GridCellViewModel(
                    c.Key,
                    initialChecked.TryGetValue((u.UserId, c.Key), out var v) && v,
                    baselineChecked != null && baselineChecked.TryGetValue((u.UserId, c.Key), out var b) && b)).ToList()))
                .ToList();

            _snapshot = null;
            IsEditing = false;
            StatusText = string.Empty;
            _currentPage = 1;

            ColumnsChanged?.Invoke(this, EventArgs.Empty);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var tokens = (_searchText ?? "").Trim().ToLowerInvariant()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            _filtered = tokens.Length == 0
                ? _allRows
                : _allRows.Where(r => tokens.All(t => (r.Name ?? "").ToLowerInvariant().Contains(t))).ToList();

            RebuildPage();
        }

        private void RebuildPage()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} user{(_filtered.Count == 1 ? "" : "s")})";
        }

        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RebuildPage(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)            { _currentPage--;             RebuildPage(); } }
        public void GoToNextPage()  { if (_currentPage < _totalPages)  { _currentPage++;             RebuildPage(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPage(); } }

        public void BeginEdit()
        {
            _snapshot = CurrentState();
            IsEditing = true;
            StatusText = string.Empty;
        }

        public void CancelEdit()
        {
            if (_snapshot != null)
                foreach (var row in _allRows)
                    foreach (var cell in row.Cells)
                        cell.IsChecked = _snapshot.TryGetValue((row.UserId, cell.Key), out var v) && v;

            _snapshot = null;
            IsEditing = false;
            StatusText = string.Empty;
        }

        public async Task SaveAsync()
        {
            if (SaveHandler == null) return;

            IsSaving = true;
            StatusIsError = false;
            StatusText = "Saving...";
            try
            {
                bool ok = await SaveHandler(CurrentState());
                if (ok)
                {
                    _snapshot = null;
                    IsEditing = false;
                    StatusIsError = false;
                    StatusText = "✓  Saved. Users will see updated access on their next login.";
                }
                else
                {
                    StatusIsError = true;
                    StatusText = "Save failed.";
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        private Dictionary<(int UserId, string Key), bool> CurrentState()
        {
            var state = new Dictionary<(int, string), bool>();
            foreach (var row in _allRows)
                foreach (var cell in row.Cells)
                    state[(row.UserId, cell.Key)] = cell.IsChecked;
            return state;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Portal -> Main Menu -> Page editor (Pages tab per user, Role Pages tab per role)
    // ══════════════════════════════════════════════════════════════════════════

    public sealed class SubjectItem
    {
        public SubjectItem(int id, string name) { Id = id; Name = name; }
        public int Id { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    public sealed class PageCatalogEntry
    {
        public PageCatalogEntry(string key, string displayName, string portal, string menu)
        {
            Key = key; DisplayName = displayName; Portal = portal; Menu = menu;
        }
        public string Key { get; }
        public string DisplayName { get; }
        public string Portal { get; }
        public string Menu { get; }
    }

    public sealed class PortalTabViewModel : ViewModelBase
    {
        private bool _isSelected;

        public PortalTabViewModel(string key, string displayName, bool hasPages)
        {
            Key = key; DisplayName = displayName; HasPages = hasPages;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public bool HasPages { get; }
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
    }

    public sealed class PageCheckViewModel : ViewModelBase
    {
        private bool _isChecked;

        public PageCheckViewModel(string key, string displayName, bool isChecked)
        {
            Key = key; DisplayName = displayName; _isChecked = isChecked;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public bool IsChecked { get => _isChecked; set => SetField(ref _isChecked, value); }
    }

    public sealed class PageAccessEditorViewModel : ViewModelBase
    {
        private readonly PermissionItemRepository _repo = new PermissionItemRepository();

        private List<PageCatalogEntry> _catalog = new List<PageCatalogEntry>();
        private Dictionary<(int Id, string Key), bool> _overrides = new Dictionary<(int, string), bool>();

        private SubjectItem _selectedSubject;
        private string _selectedPortalKey;
        private string _selectedMenu;
        private string _statusText = string.Empty;
        private bool _isBusy;

        public PageAccessEditorViewModel(bool isRole)
        {
            IsRole = isRole;
        }

        /// <summary>True = edits a Role's baseline (dbo.RolePermissionItem); false = a single user's overrides.</summary>
        public bool IsRole { get; }

        public string SubjectLabel => IsRole ? "Role:" : "User:";
        public string Subtitle => IsRole
            ? "Set the default page access for everyone in a role. A per-user override on the Pages tab always wins over this."
            : "Restrict which pages a user may open, following the app's own Portal → Menu → Page structure.";

        public ObservableCollection<SubjectItem> Subjects { get; } = new ObservableCollection<SubjectItem>();
        public ObservableCollection<PortalTabViewModel> Portals { get; } = new ObservableCollection<PortalTabViewModel>();
        public ObservableCollection<string> Menus { get; } = new ObservableCollection<string>();
        public ObservableCollection<PageCheckViewModel> Pages { get; } = new ObservableCollection<PageCheckViewModel>();

        public bool HasPages => Pages.Count > 0;
        public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        public SubjectItem SelectedSubject
        {
            get => _selectedSubject;
            set { if (SetField(ref _selectedSubject, value)) RefreshPages(); }
        }

        public string SelectedMenu
        {
            get => _selectedMenu;
            set { if (SetField(ref _selectedMenu, value)) RefreshPages(); }
        }

        public void Load(
            List<SubjectItem> subjects,
            List<(int PortalId, string PortalKey, string DisplayName)> portals,
            List<PageCatalogEntry> catalog,
            Dictionary<(int Id, string Key), bool> overrides)
        {
            _catalog = catalog;
            _overrides = overrides;

            Subjects.Clear();
            foreach (var s in subjects) Subjects.Add(s);

            Portals.Clear();
            foreach (var p in portals)
                Portals.Add(new PortalTabViewModel(p.PortalKey, p.DisplayName, _catalog.Any(c => c.Portal == p.PortalKey)));

            _selectedSubject = Subjects.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedSubject));

            string defaultPortal = Portals.FirstOrDefault(p => p.HasPages)?.Key ?? Portals.FirstOrDefault()?.Key;
            if (defaultPortal != null)
                SelectPortal(defaultPortal);
            else
                RefreshPages();
        }

        public void SelectPortal(string portalKey)
        {
            _selectedPortalKey = portalKey;
            foreach (var p in Portals)
                p.IsSelected = p.Key == portalKey;

            Menus.Clear();
            foreach (var m in _catalog.Where(c => c.Portal == portalKey).Select(c => c.Menu).Distinct())
                Menus.Add(m);

            _selectedMenu = Menus.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedMenu));
            RefreshPages();
        }

        private void RefreshPages()
        {
            Pages.Clear();

            if (_selectedSubject != null && _selectedPortalKey != null && _selectedMenu != null)
            {
                foreach (var page in _catalog.Where(c => c.Portal == _selectedPortalKey && c.Menu == _selectedMenu))
                {
                    bool isChecked = !_overrides.TryGetValue((_selectedSubject.Id, page.Key), out var granted) || granted;
                    Pages.Add(new PageCheckViewModel(page.Key, page.DisplayName, isChecked));
                }
            }

            OnPropertyChanged(nameof(HasPages));
        }

        public async Task SaveAsync()
        {
            var subject = _selectedSubject;
            if (subject == null)
            {
                MessageBox.Show(IsRole ? "Select a role first." : "Select a user first.",
                    IsRole ? "No Role Selected" : "No User Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (Pages.Count == 0) return;

            var toSave = Pages
                .Select(p => (Id: subject.Id, ItemKey: p.Key, DisplayName: p.DisplayName, Value: p.IsChecked ? (bool?)null : false))
                .ToList();

            IsBusy = true;
            try
            {
                if (IsRole)
                    await _repo.SaveRoleOverridesAsync("Page", toSave);
                else
                    await _repo.SaveUserOverridesAsync("Page", toSave);
                PermissionResolver.Invalidate();

                foreach (var o in toSave)
                {
                    if (o.Value == null) _overrides.Remove((o.Id, o.ItemKey));
                    else                 _overrides[(o.Id, o.ItemKey)] = o.Value.Value;
                }

                StatusText = IsRole
                    ? $"✓  Saved default page access for the {subject.Name} role."
                    : $"✓  Saved page access for {subject.Name}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Role Pages only: removes every page restriction for the selected role.</summary>
        public async Task RestoreDefaultsAsync()
        {
            var subject = _selectedSubject;
            if (!IsRole) return;
            if (subject == null)
            {
                MessageBox.Show("Select a role first.", "No Role Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Restore default page access for the {subject.Name} role?\n\nThis removes ALL page restrictions set for this role (across every portal/menu), not just the ones currently shown.",
                "Restore to Default", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                await _repo.ClearRoleOverridesAsync("Page", subject.Id);
                PermissionResolver.Invalidate();

                foreach (var key in _overrides.Keys.Where(k => k.Id == subject.Id).ToList())
                    _overrides.Remove(key);

                RefreshPages();
                StatusText = $"✓  Restored default page access for the {subject.Name} role.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore defaults:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Item Categories: per-user restriction chips (changes apply immediately)
    // ══════════════════════════════════════════════════════════════════════════

    public sealed class CategoryAccessViewModel : ViewModelBase
    {
        private readonly PermissionItemRepository _repo = new PermissionItemRepository();

        private List<string> _allCategories = new List<string>();
        private Dictionary<int, HashSet<string>> _restrictedByUser = new Dictionary<int, HashSet<string>>();

        private SubjectItem _selectedUser;
        private string _categoryText = string.Empty;
        private string _statusText = string.Empty;
        private string _header = "Restricted categories";
        private bool _isBusy;

        public ObservableCollection<SubjectItem> Users { get; } = new ObservableCollection<SubjectItem>();
        public ObservableCollection<string> AvailableCategories { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> RestrictedCategories { get; } = new ObservableCollection<string>();

        public bool HasRestrictions => RestrictedCategories.Count > 0;
        public string CategoryText { get => _categoryText; set => SetField(ref _categoryText, value); }
        public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
        public string Header { get => _header; private set => SetField(ref _header, value); }
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        public SubjectItem SelectedUser
        {
            get => _selectedUser;
            set { if (SetField(ref _selectedUser, value)) Refresh(); }
        }

        public void Load(List<SubjectItem> users, List<string> categories, Dictionary<(int UserId, string ItemKey), bool> overrides)
        {
            _allCategories = categories;
            _restrictedByUser = new Dictionary<int, HashSet<string>>();
            foreach (var kvp in overrides)
            {
                if (kvp.Value) continue; // IsGranted=true override is a no-op re-grant, not a restriction
                if (!_restrictedByUser.TryGetValue(kvp.Key.UserId, out var set))
                    _restrictedByUser[kvp.Key.UserId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(kvp.Key.ItemKey);
            }

            Users.Clear();
            foreach (var u in users) Users.Add(u);

            _selectedUser = Users.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedUser));
            Refresh();
        }

        private void Refresh()
        {
            RestrictedCategories.Clear();
            AvailableCategories.Clear();

            if (_selectedUser == null)
            {
                Header = "Restricted categories";
            }
            else
            {
                Header = $"Restricted categories for {_selectedUser.Name}";
                var restricted = _restrictedByUser.TryGetValue(_selectedUser.Id, out var set)
                    ? set
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var c in restricted.OrderBy(c => c)) RestrictedCategories.Add(c);
                foreach (var c in _allCategories.Where(c => !restricted.Contains(c))) AvailableCategories.Add(c);
            }

            CategoryText = string.Empty;
            OnPropertyChanged(nameof(HasRestrictions));
        }

        public async Task AddAsync()
        {
            var user = _selectedUser;
            if (user == null)
            {
                MessageBox.Show("Select a user first.", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string category = _allCategories.FirstOrDefault(c => string.Equals(c, (_categoryText ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (category == null)
            {
                MessageBox.Show("Select a valid category from the list.", "Invalid Category", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsBusy = true;
            try
            {
                await _repo.SaveUserOverridesAsync("ItemCategory", new[] { (user.Id, category, category, (bool?)false) });
                PermissionResolver.Invalidate();

                if (!_restrictedByUser.TryGetValue(user.Id, out var set))
                    _restrictedByUser[user.Id] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(category);

                Refresh();
                StatusText = $"✓  Restricted \"{category}\" for {user.Name}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RemoveAsync(string category)
        {
            var user = _selectedUser;
            if (user == null || category == null) return;

            try
            {
                await _repo.SaveUserOverridesAsync("ItemCategory", new[] { (user.Id, category, category, (bool?)null) });
                PermissionResolver.Invalidate();

                if (_restrictedByUser.TryGetValue(user.Id, out var set))
                    set.Remove(category);

                Refresh();
                StatusText = $"✓  Removed restriction on \"{category}\" for {user.Name}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
