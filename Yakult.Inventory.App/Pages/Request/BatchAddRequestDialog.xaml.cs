using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Pages.Employee;

using WinForms  = System.Windows.Forms;
using WinMsgBox = System.Windows.MessageBox;

namespace Yakult.Inventory.App.Pages.Request
{
    public partial class BatchAddRequestDialog : Window, IDisposable
    {
        // ── Lookups ──────────────────────────────────────────────────────────────
        private readonly List<CategoryItem>  _categories  = new List<CategoryItem>();
        private readonly List<ItemItem>      _allItems    = new List<ItemItem>();
        private readonly List<EmployeeItem>  _employees   = new List<EmployeeItem>();
        private readonly List<OrgItem>       _companies   = new List<OrgItem>();
        private readonly List<OrgItem>       _departments = new List<OrgItem>();
        private readonly List<OrgItem>       _branches    = new List<OrgItem>();
        private readonly List<OrgItem>       _distributors = new List<OrgItem>();

        // ── Grid rows ────────────────────────────────────────────────────────────
        private readonly ObservableCollection<RequestRowVm> _rows = new ObservableCollection<RequestRowVm>();

        // ── Maximize / restore state ──────────────────────────────────────────────
        // WindowState.Maximized on AllowsTransparency="True" windows doesn't respect
        // MaxWidth/MaxHeight at the right time, so we manage the state manually.
        private bool   _isMaximized;
        private double _normalLeft, _normalTop, _normalWidth, _normalHeight;

        // ── Org-unit filter-as-you-type ───────────────────────────────────────────
        private System.Windows.Data.ListCollectionView _companyView;
        private System.Windows.Data.ListCollectionView _deptView;
        private System.Windows.Data.ListCollectionView _branchView;
        private System.Windows.Data.ListCollectionView _distributorView;
        private bool _suppressOrgFilter;

        // Cascade predicates — set when a parent filter changes; composed with text filter in FilterOrgCombo
        private Predicate<object> _deptCascadeFilter   = null;
        private Predicate<object> _branchCascadeFilter = null;

        // ── Employee search state ────────────────────────────────────────────────
        private EmployeeItem _selectedEmployee    = null;
        private bool         _suppressEmpFilter   = false;

        // Active org filters driving the employee search
        private int? _filterComId    = null;
        private int? _filterDeptId   = null;
        private int? _filterBranchId = null;

        // ── Details panel sync guard ──────────────────────────────────────────────
        private bool _suppressDetailsSync = false;

        // Item IDs to prepopulate the grid with (e.g. from ViewItemsPage's Bulk Add Request).
        private readonly List<int> _prefillItemIds;

        // ─────────────────────────────────────────────────────────────────────────
        public BatchAddRequestDialog() : this(null) { }

        // Reuses the standard Batch Add Request workflow, but seeds the grid with one row
        // per given ItemId instead of a single blank row — used by ViewItemsPage's
        // "Bulk Add Request" action so callers don't have to duplicate row-building logic.
        public BatchAddRequestDialog(IEnumerable<int> prefillItemIds)
        {
            _prefillItemIds = prefillItemIds?.ToList();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DgvRequests.ItemsSource = _rows;
            LoadLookups();
            PopulateInitialRows();

            // Hook TextChanged that bubbles up from the inner PART_EditableTextBox.
            // The sender IS that inner TextBox, so we read its .Text directly —
            // cmb.Text would give the selected item's display name, not the typed text.
            var tcEvent = System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent;
            CmbCompany.AddHandler(tcEvent, new TextChangedEventHandler((s, _) =>
                FilterOrgCombo(CmbCompany, _companyView,
                    (s as System.Windows.Controls.TextBox)?.Text ?? CmbCompany.Text)));
            CmbDepartment.AddHandler(tcEvent, new TextChangedEventHandler((s, _) =>
                FilterOrgCombo(CmbDepartment, _deptView,
                    (s as System.Windows.Controls.TextBox)?.Text ?? CmbDepartment.Text)));
            CmbBranch.AddHandler(tcEvent, new TextChangedEventHandler((s, _) =>
                FilterOrgCombo(CmbBranch, _branchView,
                    (s as System.Windows.Controls.TextBox)?.Text ?? CmbBranch.Text)));
            CmbDistributor.AddHandler(tcEvent, new TextChangedEventHandler((s, _) =>
                FilterOrgCombo(CmbDistributor, _distributorView,
                    (s as System.Windows.Controls.TextBox)?.Text ?? CmbDistributor.Text)));
        }

        public void Dispose() { }

        // ── ShowDialog WinForms compatibility ─────────────────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? r = base.ShowDialog();
            return r == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        // ── Window chrome ─────────────────────────────────────────────────────────

        // Returns the working area of whichever monitor this window is currently on,
        // in WPF logical pixels (DPI-aware). Falls back to the primary work area.
        private Rect GetCurrentMonitorWorkArea()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var screen = WinForms.Screen.FromHandle(hwnd);
                var wa     = screen.WorkingArea;
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var m  = source.CompositionTarget.TransformFromDevice;
                    var tl = m.Transform(new Point(wa.Left, wa.Top));
                    var br = m.Transform(new Point(wa.Right, wa.Bottom));
                    return new Rect(tl, br);
                }
            }
            return SystemParameters.WorkArea;
        }

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !_isMaximized) DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            // Minimize only this window. ShowInTaskbar="True" (set in XAML) ensures it
            // remains accessible from the taskbar; the main app is not affected.
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_isMaximized)
            {
                _isMaximized          = false;
                MaxWidth              = double.PositiveInfinity;
                MaxHeight             = double.PositiveInfinity;
                Left                  = _normalLeft;
                Top                   = _normalTop;
                Width                 = _normalWidth;
                Height                = _normalHeight;
                BtnMaximizeGlyph.Text = "□";
            }
            else
            {
                _normalLeft           = Left;
                _normalTop            = Top;
                _normalWidth          = Width;
                _normalHeight         = Height;
                var wa                = GetCurrentMonitorWorkArea();
                MaxWidth              = wa.Width;
                MaxHeight             = wa.Height;
                Left                  = wa.Left;
                Top                   = wa.Top;
                Width                 = wa.Width;
                Height                = wa.Height;
                _isMaximized          = true;
                BtnMaximizeGlyph.Text = "❐";
            }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = false;
        }

        // Popups hosted by this Window (the org filter ComboBoxes' dropdowns and the
        // Employee search popup) are AllowsTransparency="True" inside an
        // AllowsTransparency/WindowStyle=None host Window — StaysOpen="False" alone is
        // unreliable in that combination, so click-outside-to-close is enforced explicitly
        // here. Clicks landing inside a popup never reach this handler (each popup is its
        // own top-level HWND), so this only fires for clicks elsewhere in the window.
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            CloseOrgDropdownIfOutside(CmbCompany, source);
            CloseOrgDropdownIfOutside(CmbDepartment, source);
            CloseOrgDropdownIfOutside(CmbBranch, source);
            CloseOrgDropdownIfOutside(CmbDistributor, source);

            if (PopupEmployee.IsOpen && !IsDescendant(GridEmployee, source))
                PopupEmployee.IsOpen = false;
        }

        private static void CloseOrgDropdownIfOutside(ComboBox cmb, DependencyObject source)
        {
            if (cmb.IsDropDownOpen && !IsDescendant(cmb, source))
                cmb.IsDropDownOpen = false;
        }

        private static bool IsDescendant(DependencyObject ancestor, DependencyObject node)
        {
            while (node != null)
            {
                if (node == ancestor) return true;
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
            }
            return false;
        }

        // ── Data loading ──────────────────────────────────────────────────────────
        private void LoadLookups()
        {
            LoadEmployees();
            LoadCategories();
            LoadItems();
            LoadOrgUnits();
        }

        private void LoadEmployees()
        {
            _employees.Clear();

            var cs = DatabaseConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs)) return;
            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    // Optional posting column; older DBs predate it.
                    bool hasEmpDistributor = false;
                    using (var chk = new SqlCommand("SELECT CASE WHEN COL_LENGTH('dbo.Employee', 'DistributorId') IS NULL THEN 0 ELSE 1 END", con))
                        hasEmpDistributor = Convert.ToInt32(chk.ExecuteScalar()) == 1;
                    string distSelect = hasEmpDistributor
                        ? ", DistributorId"
                        : ", CAST(NULL AS INT) AS DistributorId";
                    const string sqlBase =
                        "SELECT EmpId, Name, EmployeeNumber, Position, ComId, DeptId, BranchId";
                    using (var cmd = new SqlCommand(sqlBase + distSelect +
                        " FROM dbo.Employee WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            _employees.Add(new EmployeeItem
                            {
                                Id             = r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                                Name           = r.IsDBNull(1) ? "" : r.GetString(1),
                                EmployeeNumber = r.IsDBNull(2) ? null : r.GetString(2),
                                Position       = r.IsDBNull(3) ? "" : r.GetString(3),
                                ComId          = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                                DeptId         = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                                BranchId       = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                                DistributorId  = r.IsDBNull(7) ? (int?)null : r.GetInt32(7)
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load employees: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private IEnumerable<EmployeeItem> GetFilteredEmployees()
        {
            return _employees.Where(e => e.Id.HasValue &&
                (_filterComId    == null || e.ComId    == _filterComId) &&
                (_filterDeptId   == null || e.DeptId   == _filterDeptId) &&
                (_filterBranchId == null || e.BranchId == _filterBranchId));
        }

        private void LoadCategories()
        {
            _categories.Clear();
            var cs = DatabaseConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs)) return;
            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string name = r.GetString(1);
                            if (!PermissionResolver.CanUseItemCategory(name)) continue;
                            _categories.Add(new CategoryItem
                            {
                                CategoryId = r.GetInt32(0),
                                Name       = name
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load categories: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadItems()
        {
            _allItems.Clear();
            var cs = DatabaseConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs)) return;
            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    const string sql = @"
                        SELECT i.ItemId, i.Name, i.StockOnHand, i.CategoryId, i.Category,
                               i.SerialNumber, i.ModelNumber, i.Amount,
                               CASE WHEN EXISTS (SELECT 1 FROM dbo.Request r WHERE r.ItemId = i.ItemId)
                                    THEN 0 ELSE 1 END AS IsAvailable,
                               i.IsTrackedAsset, i.ConsumableModelId, cm.ModelNumber AS ConsumableModelName,
                               i.Remarks
                        FROM dbo.Item i
                        LEFT JOIN dbo.ConsumableModel cm ON cm.ConsumableModelId = i.ConsumableModelId
                        WHERE i.Active = 1 ORDER BY i.Name";
                    using (var cmd = new SqlCommand(sql, con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            _allItems.Add(new ItemItem
                            {
                                Id                  = r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                                Name                = r.IsDBNull(1) ? "" : r.GetString(1),
                                StockOnHand         = r.IsDBNull(2) ? 0 : r.GetInt32(2),
                                CategoryId          = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                                CategoryName        = r.IsDBNull(4) ? null : r.GetString(4),
                                SerialNumber        = r.IsDBNull(5) ? null : r.GetString(5),
                                ModelNumber         = r.IsDBNull(6) ? null : r.GetString(6),
                                Amount              = r.IsDBNull(7) ? 0 : r.GetDecimal(7),
                                IsAvailable         = r.IsDBNull(8) ? true : r.GetInt32(8) == 1,
                                IsTrackedAsset      = r.IsDBNull(9) ? false : r.GetBoolean(9),
                                ConsumableModelId   = r.IsDBNull(10) ? (int?)null : r.GetInt32(10),
                                ConsumableModelName = r.IsDBNull(11) ? null : r.GetString(11),
                                Remarks             = r.IsDBNull(12) ? null : r.GetString(12)
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadOrgUnits()
        {
            var cs = DatabaseConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs)) return;
            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    _companies.Clear();
                    _companies.Add(new OrgItem { Id = 0, Name = "-- Any Company --" });
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) _companies.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    _departments.Clear();
                    _departments.Add(new OrgItem { Id = 0, Name = "-- Any Dept. --" });
                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) _departments.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    _branches.Clear();
                    _branches.Add(new OrgItem { Id = 0, Name = "-- Any Branch --" });
                    using (var cmd = new SqlCommand(
                        "SELECT BranchId, Name, CASE WHEN COL_LENGTH('dbo.Branch', 'IsDistributor') IS NULL THEN 0 ELSE ISNULL(IsDistributor, 0) END AS IsDistributor FROM dbo.Branch WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string branchName = r.GetString(1);
                            if (Convert.ToInt32(r.GetValue(2)) == 1)
                                branchName += " (Distributor)";
                            _branches.Add(new OrgItem { Id = r.GetInt32(0), Name = branchName });
                        }

                    // Independent sales distributors (dbo.Distributor) for dept-level
                    // batches. Missing table on older DBs leaves "(None)" only.
                    _distributors.Clear();
                    _distributors.Add(new OrgItem { Id = 0, Name = "(None)" });
                    try
                    {
                        using (var cmd = new SqlCommand(
                            "IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL SELECT DistributorId, Name FROM dbo.Distributor WHERE IsActive = 1 ORDER BY SortOrder, Name", con))
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                _distributors.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });
                    }
                    catch
                    {
                        // Distributor catalog unavailable — keep "(None)" only.
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load org units: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _companyView = new System.Windows.Data.ListCollectionView(_companies);
            _deptView    = new System.Windows.Data.ListCollectionView(_departments);
            _branchView  = new System.Windows.Data.ListCollectionView(_branches);
            _distributorView = new System.Windows.Data.ListCollectionView(_distributors);

            CmbCompany.DisplayMemberPath    = "Name";
            CmbCompany.ItemsSource          = _companyView;
            CmbCompany.SelectedIndex        = 0;

            CmbDepartment.DisplayMemberPath = "Name";
            CmbDepartment.ItemsSource       = _deptView;
            CmbDepartment.SelectedIndex     = 0;

            CmbBranch.DisplayMemberPath     = "Name";
            CmbBranch.ItemsSource           = _branchView;
            CmbBranch.SelectedIndex         = 0;

            CmbDistributor.DisplayMemberPath = "Name";
            CmbDistributor.ItemsSource       = _distributorView;
            CmbDistributor.SelectedIndex     = 0;
        }

        // ── Row management ────────────────────────────────────────────────────────
        private void AddRow()
        {
            var vm = new RequestRowVm(_categories, _allItems);
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RequestRowVm.IsSelected))
                    UpdateRemoveRowButton();
            };
            _rows.Add(vm);
        }

        // Seeds the grid: one row per prefill ItemId if given, otherwise the usual single
        // blank row. Items that no longer exist/are inactive are silently skipped.
        private void PopulateInitialRows()
        {
            if (_prefillItemIds == null || _prefillItemIds.Count == 0)
            {
                AddRow();
                return;
            }

            foreach (var itemId in _prefillItemIds)
            {
                var item = _allItems.FirstOrDefault(x => x.Id == itemId);
                if (item == null) continue;
                AddPrefilledRow(item);
            }

            if (_rows.Count == 0)
                AddRow();
        }

        private void AddPrefilledRow(ItemItem item)
        {
            var vm = new RequestRowVm(_categories, _allItems);
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RequestRowVm.IsSelected))
                    UpdateRemoveRowButton();
            };

            var category = _categories.FirstOrDefault(c => c.CategoryId == item.CategoryId);
            if (category != null)
                vm.SelectedCategory = category;
            vm.SetItemDirectly(item);

            _rows.Add(vm);
        }

        private void UpdateRemoveRowButton()
        {
            BtnRemoveRow.IsEnabled = _rows.Any(r => r.IsSelected);
        }

        // ── Employee search popup ─────────────────────────────────────────────────
        private void ShowEmployeePopup(string filter)
        {
            var pool    = GetFilteredEmployees();
            var matches = string.IsNullOrEmpty(filter)
                ? pool.ToList()
                : pool.Where(emp =>
                        emp.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (emp.EmployeeNumber != null &&
                         emp.EmployeeNumber.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (emp.Position != null &&
                         emp.Position.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                  .ToList();

            if (matches.Count == 0) { PopupEmployee.IsOpen = false; return; }

            LstEmployeeSearch.Items.Clear();
            foreach (var m in matches)
                LstEmployeeSearch.Items.Add(m);

            PopupEmployee.Width   = TxtEmployee.ActualWidth;
            PopupEmployee.IsOpen  = true;

            if (LstEmployeeSearch.SelectedIndex < 0)
                LstEmployeeSearch.SelectedIndex = 0;
        }

        private void TxtEmployee_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateClearEmployeeButtonVisibility();
            if (_suppressEmpFilter) return;
            _selectedEmployee = null;
            ShowEmployeePopup(TxtEmployee.Text.Trim());
        }

        private void UpdateClearEmployeeButtonVisibility()
        {
            BtnClearEmployee.Visibility = string.IsNullOrEmpty(TxtEmployee.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnClearEmployee_Click(object sender, RoutedEventArgs e)
        {
            _suppressEmpFilter = true;
            TxtEmployee.Text   = string.Empty;
            _suppressEmpFilter = false;

            _selectedEmployee = null;
            PopupEmployee.IsOpen = false;
            TxtEmployee.ClearValue(TextBox.BorderBrushProperty);
            UpdateClearEmployeeButtonVisibility();
            TxtEmployee.Focus();
        }

        private void TxtEmployee_GotFocus(object sender, RoutedEventArgs e)
        {
            // Defer so WPF's StaysOpen=False outside-click detection fires first,
            // otherwise the popup opens and immediately closes on the same mouse event.
            Dispatcher.BeginInvoke(new Action(() => ShowEmployeePopup(TxtEmployee.Text.Trim())));
        }

        private void TxtEmployee_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Re-clicking when already focused doesn't fire GotFocus, so open here.
            if (TxtEmployee.IsFocused && !PopupEmployee.IsOpen)
                Dispatcher.BeginInvoke(new Action(() => ShowEmployeePopup(TxtEmployee.Text.Trim())));
        }

        // PreviewKeyDown intercepts Up/Down/Enter/Escape BEFORE the TextBox consumes
        // them — setting e.Handled = true here prevents the TextBox from swallowing
        // the arrow keys, which is why navigation was broken when using KeyDown.
        private void TxtEmployee_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!PopupEmployee.IsOpen) return;

            switch (e.Key)
            {
                case Key.Down:
                    LstEmployeeSearch.SelectedIndex =
                        Math.Min(LstEmployeeSearch.SelectedIndex + 1, LstEmployeeSearch.Items.Count - 1);
                    LstEmployeeSearch.ScrollIntoView(LstEmployeeSearch.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (LstEmployeeSearch.SelectedIndex > 0)
                        LstEmployeeSearch.SelectedIndex--;
                    LstEmployeeSearch.ScrollIntoView(LstEmployeeSearch.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    SelectEmployee(LstEmployeeSearch.SelectedItem as EmployeeItem);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    PopupEmployee.IsOpen = false;
                    e.Handled = true;
                    break;
            }
        }

        private void LstEmployeeSearch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SelectEmployee(LstEmployeeSearch.SelectedItem as EmployeeItem);
        }

        private void SelectEmployee(EmployeeItem emp)
        {
            if (emp == null || !emp.Id.HasValue) return;
            _suppressEmpFilter    = true;
            TxtEmployee.Text      = emp.DisplayText;
            _selectedEmployee     = emp;
            _suppressEmpFilter    = false;
            PopupEmployee.IsOpen  = false;
            UpdateClearEmployeeButtonVisibility();
            AutofillOrgFromEmployee(emp);
            DgvRequests.Focus();
        }

        // Auto-fill the org filter row from the picked employee's own
        // associations (company/dept/branch/distributor). Suppressed so the
        // cascade handlers don't wipe the just-applied selections, and the
        // employee-popup filters are aligned to the same org.
        private void AutofillOrgFromEmployee(EmployeeItem emp)
        {
            _suppressOrgFilter = true;
            try
            {
                SelectOrgComboItem(CmbCompany, emp.ComId);
                SelectOrgComboItem(CmbDepartment, emp.DeptId);
                SelectOrgComboItem(CmbBranch, emp.BranchId);
                SelectOrgComboItem(CmbDistributor, emp.DistributorId);
            }
            finally
            {
                _suppressOrgFilter = false;
            }

            _filterComId    = emp.ComId;
            _filterDeptId   = emp.DeptId;
            _filterBranchId = emp.BranchId;
        }

        private static void SelectOrgComboItem(System.Windows.Controls.ComboBox cmb, int? id)
        {
            if (cmb == null || cmb.Items.Count == 0) return;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is OrgItem oi && oi.Id == id)
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
            // No match (e.g. inactive org filtered out, or null): fall back to
            // the first "(Any)/(None)" entry so the row never shows stale org.
            cmb.SelectedIndex = 0;
        }

        // ── No-Employee / Org-unit toggle ─────────────────────────────────────────
        private void ChkNoEmployee_Changed(object sender, RoutedEventArgs e)
        {
            bool noEmp = ChkNoEmployee.IsChecked == true;
            PnlEmployee.Visibility = noEmp ? Visibility.Collapsed : Visibility.Visible;
            // Distributor is always visible (filter in employee mode, org unit in
            // dept mode). No selection reset: what the user sees is what saves.
            PnlDistributor.Visibility = Visibility.Visible;
            LblOrgRowPrefix.Text   = noEmp ? "Org Unit:" : "Filter:";
            TxtInfoBanner.Text     = noEmp
                ? "Batch add requests. Select a Company or a Distributor (at least one required), plus optional Department and Branch, then fill each row and click Save."
                : "Batch add requests. Optionally filter by Company, Dept, Branch, or Distributor, then select an employee and fill each row.";

            // The department/branch ListCollectionViews can still have an employee-derived
            // cascade from the previous mode. Reapply the selected company for the current
            // mode so No Employee always exposes the active organization master lists.
            var company = CmbCompany.SelectedItem as OrgItem;
            ApplyCompanyCascade(company?.Id > 0 ? company.Id : (int?)null);
        }

        // Handles item selection for all three org ComboBoxes (click or Enter after navigation).
        private void CmbOrg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressOrgFilter || !(sender is ComboBox cmb)) return;

            if (cmb == CmbCompany &&
                CmbCompany.BorderBrush is SolidColorBrush b && b.Color == Colors.Red)
                CmbCompany.BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xDC));

            var item = e.AddedItems.Count > 0 ? e.AddedItems[0] as OrgItem : null;
            if (item == null) return;

            CommitOrgSelection(cmb, item);

            if      (cmb == CmbCompany)    ApplyCompanyCascade(item.Id > 0 ? item.Id : (int?)null);
            else if (cmb == CmbDepartment) ApplyDeptCascade(item.Id > 0 ? item.Id : (int?)null);
            else if (cmb == CmbBranch)
            {
                _filterBranchId = item.Id > 0 ? item.Id : (int?)null;
                if (PopupEmployee.IsOpen && ChkNoEmployee.IsChecked != true)
                    ShowEmployeePopup(TxtEmployee.Text.Trim());
            }
        }

        // After the dropdown closes: restore the cascade filter (which typing may have replaced with
        // a text-search filter), then sync the text box back to the actual selection.
        private void CmbOrg_DropDownClosed(object sender, EventArgs e)
        {
            if (!(sender is ComboBox cmb)) return;

            // Restore cascade filter — text-based filtering may have overwritten it.
            _suppressOrgFilter = true;
            if      (cmb == CmbDepartment) _deptView.Filter   = _deptCascadeFilter;
            else if (cmb == CmbBranch)     _branchView.Filter = _branchCascadeFilter;
            _suppressOrgFilter = false;

            var item = cmb.SelectedItem as OrgItem;
            var tb   = cmb.Template?.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
            if (tb == null) return;

            // Sync the text box to the current selection (or placeholder if nothing selected).
            string target = item?.Name
                ?? (cmb == CmbCompany    && _companies.Count   > 0 ? _companies[0].Name
                 :  cmb == CmbDepartment && _departments.Count > 0 ? _departments[0].Name
                 :  cmb == CmbBranch     && _branches.Count    > 0 ? _branches[0].Name
                 :  cmb == CmbDistributor && _distributors.Count > 0 ? _distributors[0].Name
                 :  string.Empty);

            if (tb.Text == target) return;
            _suppressOrgFilter = true;
            tb.Text = target;
            tb.Select(target.Length, 0);
            _suppressOrgFilter = false;
        }

        private void CommitOrgSelection(ComboBox cmb, OrgItem item)
        {
            var tb = cmb.Template?.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
            if (tb == null) return;

            _suppressOrgFilter = true;
            if (tb.Text != item.Name)
                tb.Text = item.Name;
            tb.Select(item.Name.Length, 0);
            _suppressOrgFilter = false;

            if (cmb.IsDropDownOpen)
                cmb.IsDropDownOpen = false;
        }

        private void ApplyCompanyCascade(int? comId)
        {
            _filterComId    = comId;
            _filterDeptId   = null;
            _filterBranchId = null;

            var preserveDeptLevelSelection = ChkNoEmployee.IsChecked == true;
            var selectedDept = preserveDeptLevelSelection ? CmbDepartment.SelectedItem as OrgItem : null;
            var selectedBranch = preserveDeptLevelSelection ? CmbBranch.SelectedItem as OrgItem : null;

            // Department-level requests use the organization master lists directly.
            // Do not derive valid departments/branches from employees, and do not
            // erase an already-selected Storage Section or branch when the company changes.
            if (preserveDeptLevelSelection)
            {
                _deptCascadeFilter = null;
                _branchCascadeFilter = null;
                _deptView.Filter = null;
                _branchView.Filter = null;
            }
            else if (comId.HasValue)
            {
                var validDeptIds = _employees
                    .Where(emp => emp.ComId == comId && emp.DeptId.HasValue)
                    .Select(emp => emp.DeptId.Value)
                    .Distinct().ToHashSet();
                _deptCascadeFilter = o => o is OrgItem it && (it.Id == 0 || validDeptIds.Contains(it.Id));
                _branchCascadeFilter = null;
                _deptView.Filter = _deptCascadeFilter;
                _branchView.Filter = null;
            }
            else
            {
                _deptCascadeFilter = null;
                _branchCascadeFilter = null;
                _deptView.Filter = null;
                _branchView.Filter = null;
            }

            _suppressOrgFilter = true;
            if (preserveDeptLevelSelection && selectedDept != null && selectedDept.Id > 0)
            {
                CmbDepartment.SelectedItem = selectedDept;
                ResetEditText(CmbDepartment, selectedDept.Name);
            }
            else
            {
                CmbDepartment.SelectedIndex = 0;
                if (_departments.Count > 0) ResetEditText(CmbDepartment, _departments[0].Name);
            }

            if (preserveDeptLevelSelection && selectedBranch != null && selectedBranch.Id > 0)
            {
                CmbBranch.SelectedItem = selectedBranch;
                ResetEditText(CmbBranch, selectedBranch.Name);
            }
            else
            {
                CmbBranch.SelectedIndex = 0;
                if (_branches.Count > 0) ResetEditText(CmbBranch, _branches[0].Name);
            }
            _suppressOrgFilter = false;

            if (PopupEmployee.IsOpen && ChkNoEmployee.IsChecked != true)
                ShowEmployeePopup(TxtEmployee.Text.Trim());
        }

        private void ApplyDeptCascade(int? deptId)
        {
            _filterDeptId   = deptId;
            _filterBranchId = null;

            var validBranchIds = _employees
                .Where(emp => (_filterComId == null || emp.ComId == _filterComId) &&
                              (deptId       == null || emp.DeptId == deptId) &&
                              emp.BranchId.HasValue)
                .Select(emp => emp.BranchId.Value)
                .Distinct().ToHashSet();

            _branchCascadeFilter = validBranchIds.Count > 0
                ? (Predicate<object>)(o => o is OrgItem it && (it.Id == 0 || validBranchIds.Contains(it.Id)))
                : null;
            _branchView.Filter = _branchCascadeFilter;

            _suppressOrgFilter = true;
            CmbBranch.SelectedIndex = 0;
            if (_branches.Count > 0) ResetEditText(CmbBranch, _branches[0].Name);
            _suppressOrgFilter = false;

            if (PopupEmployee.IsOpen && ChkNoEmployee.IsChecked != true)
                ShowEmployeePopup(TxtEmployee.Text.Trim());
        }

        private void ResetEditText(ComboBox cmb, string text)
        {
            var tb = cmb.Template?.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
            if (tb == null) return;
            tb.Text = text;
            tb.Select(0, 0);
        }

        // On focus: show the dropdown with the correct cascaded list and select all text so the
        // first keystroke replaces rather than appends.  Company resets to full list; Dept and
        // Branch restore their cascade filter so only contextually valid items are shown.
        private void CmbOrg_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressOrgFilter) return;
            if (!(sender is ComboBox cmb) || !cmb.IsEditable) return;

            _suppressOrgFilter = true;
            if      (cmb == CmbCompany)    _companyView.Filter = null;
            else if (cmb == CmbDepartment) _deptView.Filter    = _deptCascadeFilter;
            else if (cmb == CmbBranch)     _branchView.Filter  = _branchCascadeFilter;
            else if (cmb == CmbDistributor && _distributorView != null) _distributorView.Filter = null;
            _suppressOrgFilter = false;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_suppressOrgFilter) return;
                var tb = cmb.Template?.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
                tb?.SelectAll();
                if (!cmb.IsDropDownOpen) cmb.IsDropDownOpen = true;
            }));
        }

        // ── Item search (TextBox + Popup ListBox) ─────────────────────────────────
        // The Item cell uses a plain TextBox plus a Popup ListBox rather than an editable
        // ComboBox. A ComboBox re-syncs its Text from SelectedItem whenever its ItemsSource
        // changes, which erased the user's typed search term on every keystroke as the list
        // was filtered. Keeping the two controls separate avoids that coupling entirely.

        private static System.Windows.Controls.Primitives.Popup FindItemPopup(FrameworkElement fromTextBox)
        {
            var grid = fromTextBox?.Parent as Grid;
            if (grid == null) return null;
            foreach (var child in grid.Children)
                if (child is System.Windows.Controls.Primitives.Popup popup) return popup;
            return null;
        }

        private static ListBox FindItemList(System.Windows.Controls.Primitives.Popup popup)
            => (popup?.Child as Border)?.Child as ListBox;

        private void ItemSearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.TextBox tb)) return;

            // Entering edit mode starts a fresh search so the full option list is shown
            // first, rather than pre-filtering by the previously chosen item's name.
            if (tb.DataContext is RequestRowVm vm)
                vm.ItemSearchText = string.Empty;

            tb.Focus();
            tb.SelectAll();

            var popup = FindItemPopup(tb);
            if (popup != null) popup.IsOpen = true;
        }

        private void ItemSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // The TwoWay binding already pushed the new text into ItemSearchText, which
            // rebuilt FilteredItems. Just make sure the option list is visible.
            if (!(sender is System.Windows.Controls.TextBox tb)) return;
            var popup = FindItemPopup(tb);
            if (popup != null && !popup.IsOpen) popup.IsOpen = true;
        }

        private void ItemSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is System.Windows.Controls.TextBox tb)) return;
            var popup = FindItemPopup(tb);
            var list  = FindItemList(popup);
            if (popup == null || list == null) return;

            switch (e.Key)
            {
                case Key.Down:
                    if (!popup.IsOpen) popup.IsOpen = true;
                    if (list.Items.Count > 0)
                    {
                        list.SelectedIndex = list.SelectedIndex < 0
                            ? 0
                            : Math.Min(list.SelectedIndex + 1, list.Items.Count - 1);
                        list.ScrollIntoView(list.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (list.Items.Count > 0 && list.SelectedIndex > 0)
                    {
                        list.SelectedIndex--;
                        list.ScrollIntoView(list.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                    CommitItemPick(tb, list.SelectedItem as ItemItem);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    popup.IsOpen = false;
                    e.Handled = true;
                    break;
            }
        }

        private void LstItems_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListBox list)) return;

            // Resolve the clicked row directly from the hit-tested element rather than from
            // list.SelectedItem: ListBoxItem can mark the mouse event handled and selection
            // may not have been applied yet at Preview time.
            var clickedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            var picked = (clickedItem?.DataContext ?? list.SelectedItem) as ItemItem;

            // The Popup lives outside the cell's visual tree, so resolve the row VM straight
            // from the ListBox's DataContext, and walk up to the Popup to close it.
            var popup = FindVisualParent<System.Windows.Controls.Primitives.Popup>(list);
            CommitItemPick(null, picked, list.DataContext as RequestRowVm, popup);
            e.Handled = true;
        }

        // Applies the chosen item to the row and closes the option list. Ignores the
        // placeholder/blank entries (Id == null) so they can't be committed as a pick.
        private void CommitItemPick(System.Windows.Controls.TextBox tb, ItemItem picked,
                                    RequestRowVm vmOverride = null,
                                    System.Windows.Controls.Primitives.Popup popupOverride = null)
        {
            var vm = vmOverride ?? tb?.DataContext as RequestRowVm;
            if (vm == null || picked == null || !picked.Id.HasValue) return;

            vm.SelectedItem = picked;

            var popup = popupOverride ?? (tb != null ? FindItemPopup(tb) : null);
            if (popup != null) popup.IsOpen = false;

            // Leave edit mode so the committed value renders in the cell's display template.
            DgvRequests.CommitEdit(DataGridEditingUnit.Cell, true);
        }

        private void FilterOrgCombo(ComboBox cmb, System.Windows.Data.ListCollectionView view, string filter)
        {
            if (_suppressOrgFilter || view == null) return;

            // Text change came from WPF committing a selection — don't re-filter.
            if (cmb.SelectedItem is OrgItem picked &&
                string.Equals(picked.Name, filter, StringComparison.OrdinalIgnoreCase))
                return;

            // Dept and Branch use a cascade base filter; Company has none.
            Predicate<object> cascade = cmb == CmbDepartment ? _deptCascadeFilter
                                      : cmb == CmbBranch     ? _branchCascadeFilter
                                      : null;

            _suppressOrgFilter = true;
            try
            {
                if (string.IsNullOrEmpty(filter))
                {
                    view.Filter = cascade;
                }
                else
                {
                    view.Filter = cascade == null
                        ? (Predicate<object>)(o => o is OrgItem item &&
                              item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        : o => cascade(o) && o is OrgItem item &&
                              item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (!string.IsNullOrEmpty(filter))
                    cmb.IsDropDownOpen = true;

                var tb = cmb.Template?.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
                if (tb != null)
                {
                    if (tb.Text != filter) tb.Text = filter;
                    tb.Select(filter.Length, 0);
                }
            }
            finally
            {
                _suppressOrgFilter = false;
            }
        }

        // Clear glyph embedded in FilterComboStyle's ControlTemplate — TemplatedParent resolves
        // to whichever ComboBox (Company/Dept/Branch) instance owns this particular button.
        private void BtnClearOrg_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.TemplatedParent is ComboBox cmb)) return;
            cmb.SelectedIndex = 0;
            DgvRequests.Focus();
        }

        private void CmbOrg_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is ComboBox cmb) || !cmb.IsDropDownOpen) return;

            switch (e.Key)
            {
                case Key.Down:
                    MoveOrgSelection(cmb, +1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    MoveOrgSelection(cmb, -1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    cmb.IsDropDownOpen = false;
                    e.Handled = true;
                    break;
                case Key.Escape:
                    cmb.IsDropDownOpen = false;
                    e.Handled = true;
                    break;
            }
        }

        private void MoveOrgSelection(ComboBox cmb, int delta)
        {
            var view = cmb == CmbCompany ? _companyView
                     : cmb == CmbDepartment ? _deptView
                     : cmb == CmbDistributor ? _distributorView
                     : _branchView;
            if (view == null) return;

            var visible = view.Cast<object>().ToList();
            if (visible.Count == 0) return;

            int cur  = cmb.SelectedItem != null ? visible.IndexOf(cmb.SelectedItem) : -1;
            int next = Math.Max(0, Math.Min(cur + delta, visible.Count - 1));
            if (next == cur) return;

            _suppressOrgFilter = true;
            cmb.SelectedItem = visible[next];
            var tb = cmb.Template?.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
            if (tb != null)
            {
                string name = (visible[next] as OrgItem)?.Name ?? string.Empty;
                tb.Text = name;
                tb.Select(name.Length, 0);
            }
            _suppressOrgFilter = false;
        }

        // ── DataGrid events ───────────────────────────────────────────────────────
        private void DgvRequests_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Single-click enters edit mode for the clicked cell
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell != null && !cell.IsEditing && !cell.IsReadOnly)
            {
                cell.Focus();
                DgvRequests.BeginEdit();
            }
        }

        private void DgvRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DgvRequests.SelectedItem as RequestRowVm;
            BindDetailsPanel(vm);
        }

        private void DgvRequests_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            var vm = e.Row.Item as RequestRowVm;
            if (vm == null) return;

            // After model/serial edit commits, attempt item auto-resolve
            var col = e.Column;
            if (col == ColItem) return; // item selection handled by VM setter

            if (col.Header?.ToString() == "Model Number" || col.Header?.ToString() == "Serial Number")
            {
                var tb = e.EditingElement as TextBox;
                string value = tb?.Text?.Trim() ?? string.Empty;

                bool isModel = col.Header?.ToString() == "Model Number";
                Dispatcher.BeginInvoke(new Action(() =>
                    ResolveItemFromTypedValue(vm, isModel, value)));
            }
        }

        private void ResolveItemFromTypedValue(RequestRowVm vm, bool isModelColumn, string typedValue)
        {
            if (string.IsNullOrWhiteSpace(typedValue)) return;

            var categoryItems = vm.SelectedCategory != null
                ? _allItems.Where(x => x.CategoryId == vm.SelectedCategory.CategoryId)
                : (IEnumerable<ItemItem>)_allItems;

            ItemItem resolved = null;

            if (isModelColumn)
            {
                var matches = categoryItems
                    .Where(x => string.Equals(x.ModelNumber, typedValue, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 1)
                    resolved = matches[0];
                else if (matches.Count > 1)
                    resolved = matches.FirstOrDefault(x => x.IsAvailable) ?? matches[0];
            }
            else
            {
                resolved = categoryItems.FirstOrDefault(x =>
                    string.Equals(x.SerialNumber, typedValue, StringComparison.OrdinalIgnoreCase));
            }

            if (resolved == null || !resolved.Id.HasValue) return;

            // Update category if needed
            if (vm.SelectedCategory == null || vm.SelectedCategory.CategoryId != resolved.CategoryId)
            {
                var cat = _categories.FirstOrDefault(c => c.CategoryId == resolved.CategoryId);
                if (cat != null) vm.SelectedCategory = cat;
            }

            vm.SetItemDirectly(resolved);
        }

        // ── Row checkbox ──────────────────────────────────────────────────────────
        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var chk     = sender as CheckBox;
            bool check  = chk?.IsChecked == true;
            foreach (var row in _rows)
                row.IsSelected = check;
            UpdateRemoveRowButton();
        }

        private void RowCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRemoveRowButton();
        }

        // ── Details panel ─────────────────────────────────────────────────────────
        private void BindDetailsPanel(RequestRowVm vm)
        {
            _suppressDetailsSync = true;
            try
            {
                TxtDescription.Text = vm?.Description ?? string.Empty;
                TxtRemarks.Text     = vm?.Remarks     ?? string.Empty;
                TxtRemarks.IsReadOnly = vm == null;
            }
            finally { _suppressDetailsSync = false; }
        }

        private void TxtRemarks_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDetailsSync) return;
            if (DgvRequests.SelectedItem is RequestRowVm vm)
                vm.Remarks = TxtRemarks.Text;
        }

        // ── Buttons ───────────────────────────────────────────────────────────────
        private void BtnAddRow_Click(object sender, RoutedEventArgs e)    => AddRow();
        private void BtnCancel_Click(object sender, RoutedEventArgs e)    => DialogResult = false;
        private async void BtnSave_Click(object sender, RoutedEventArgs e) => await SaveBatch();

        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = _rows.Where(r => r.IsSelected).ToList();
            if (toRemove.Count == 0) return;

            string msg = toRemove.Count == 1
                ? "Remove the selected row?"
                : $"Remove {toRemove.Count} selected rows?";

            if (WinMsgBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            foreach (var r in toRemove) _rows.Remove(r);
            UpdateRemoveRowButton();
        }

        private void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new QuickAddEmployeeDialog())
                {
                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        int? newId = null;
                        try { newId = (int?)dialog.GetType().GetProperty("NewEmployeeId")?.GetValue(dialog); }
                        catch { }
                        LoadEmployees();
                        if (newId.HasValue)
                        {
                            var match = _employees.FirstOrDefault(x => x.Id == newId.Value);
                            if (match != null) SelectEmployee(match);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to open Add Employee dialog: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Save logic ────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task SaveBatch()
        {
            // Commit any open cell edit
            DgvRequests.CommitEdit(DataGridEditingUnit.Cell, true);
            DgvRequests.CommitEdit(DataGridEditingUnit.Row, true);

            // Resolve employee / org-unit
            EmployeeItem batchEmp = null;
            int? batchComId = null, batchDeptId = null, batchBranchId = null;
            int? batchDistributorId = null;
            string batchCompanyName = null, batchDeptName = null, batchBranchName = null;
            string batchDistributorName = null;

            bool noEmp = ChkNoEmployee.IsChecked == true;

            // Distributor is always visible and optional in both modes: org unit
            // in dept mode, extra tag in employee mode. Resolved before the mode
            // branch so dept-mode validation sees it. What is shown is saved.
            var selDistributor = CmbDistributor.SelectedItem as OrgItem;
            if (selDistributor != null && selDistributor.Id > 0) { batchDistributorId = selDistributor.Id; batchDistributorName = selDistributor.Name; }

            if (noEmp)
            {
                // Company OR Distributor required (distributors are independent
                // of Company/Dept/Branch). Dept/Branch optional.
                var selCompany = CmbCompany.SelectedItem as OrgItem;
                if (selCompany != null && selCompany.Id > 0)
                {
                    batchComId = selCompany.Id;
                    batchCompanyName = selCompany.Name;
                }

                var selDept = CmbDepartment.SelectedItem as OrgItem;
                if (selDept != null && selDept.Id > 0) { batchDeptId = selDept.Id; batchDeptName = selDept.Name; }

                var selBranch = CmbBranch.SelectedItem as OrgItem;
                if (selBranch != null && selBranch.Id > 0) { batchBranchId = selBranch.Id; batchBranchName = selBranch.Name; }

                if (!batchComId.HasValue && !batchDistributorId.HasValue)
                {
                    CmbCompany.BorderBrush = new SolidColorBrush(Colors.Red);
                    WinMsgBox.Show("Please select a Company or a Distributor for this batch.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CmbCompany.ClearValue(Control.BorderBrushProperty);
            }
            else
            {
                batchEmp = _selectedEmployee;
                if (batchEmp == null || !batchEmp.Id.HasValue)
                {
                    // Try to match by typed text
                    var typed = TxtEmployee.Text.Trim();
                    batchEmp = _employees.FirstOrDefault(emp => emp.Id.HasValue &&
                        string.Equals(emp.DisplayText, typed, StringComparison.OrdinalIgnoreCase));
                }
                if (batchEmp == null || !batchEmp.Id.HasValue)
                {
                    TxtEmployee.BorderBrush = new SolidColorBrush(Colors.Red);
                    WinMsgBox.Show("Please select an employee for this batch.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtEmployee.Focus();
                    return;
                }
                TxtEmployee.ClearValue(TextBox.BorderBrushProperty);
            }

            // Validate and collect rows
            var requests      = new List<RequestDto>();
            var errors        = new List<string>();

            for (int i = 0; i < _rows.Count; i++)
            {
                var vm = _rows[i];
                bool rowError = false;

                if (vm.SelectedCategory == null)
                { errors.Add($"Row {i + 1}: Category is required."); rowError = true; }

                if (vm.SelectedItem == null || !vm.SelectedItem.Id.HasValue)
                {
                    // Try to resolve from serial/model
                    if (!string.IsNullOrWhiteSpace(vm.SerialNumber))
                    {
                        var found = _allItems.FirstOrDefault(x =>
                            string.Equals(x.SerialNumber, vm.SerialNumber, StringComparison.OrdinalIgnoreCase));
                        if (found != null) vm.SetItemDirectly(found);
                    }
                    if (vm.SelectedItem == null || !vm.SelectedItem.Id.HasValue)
                    { errors.Add($"Row {i + 1}: Item is required."); rowError = true; }
                }

                if (vm.Quantity <= 0)
                { errors.Add($"Row {i + 1}: Quantity must be greater than 0."); rowError = true; }

                if (rowError) continue;

                var item = vm.SelectedItem;
                // Available stock at the moment of encoding is the only reliable signal we
                // have for how much of this request can actually be issued — there is no
                // separate portal/fulfillment step for non-cartridge requests. Items are
                // still allowed through with 0 available stock (common for Ink/Toner/Print
                // Head consumables) — they're just recorded as fully unfulfilled.
                // Prefer the formal ConsumableModel grouping when the item has one assigned
                // (Ink/Toner/Print Head) — summing by Name+CategoryId alone silently pools
                // stock across any accidental duplicate catalog rows for the same product.
                var availableStock = item.ConsumableModelId.HasValue
                    ? _allItems.Where(x => x.ConsumableModelId == item.ConsumableModelId && x.StockOnHand > 0).Sum(x => x.StockOnHand)
                    : _allItems.Where(x => x.Name == item.Name && x.CategoryId == item.CategoryId && x.StockOnHand > 0).Sum(x => x.StockOnHand);

                requests.Add(new RequestDto
                {
                    EmpId            = batchEmp?.Id,
                    EmployeeName     = batchEmp?.Name,
                    EmployeePosition = batchEmp?.Position,
                    ComId            = batchComId,
                    DeptId           = batchDeptId,
                    BranchId         = batchBranchId,
                    DistributorId    = batchDistributorId,
                    CompanyName      = batchCompanyName,
                    DepartmentName   = batchDeptName,
                    BranchName       = batchBranchName,
                    DistributorName  = batchDistributorName,
                    ItemId           = item.Id.Value,
                    ItemName         = item.Name,
                    Quantity         = vm.Quantity,
                    IssuedQty        = Math.Min(vm.Quantity, availableStock),
                    UnitPrice        = vm.UnitPrice,
                    DateRequested    = vm.DateRequested,
                    Description      = vm.Description,
                    Remarks          = vm.Remarks,
                    Status           = vm.Status,
                    EntryType        = "Negative",
                    DateCreated      = DateTime.Now,
                    CreatedByUserId  = AppSession.CurrentUserId,
                    ModifiedByUserId = AppSession.CurrentUserId,
                    CreatedByName    = AppSession.CurrentUserName,
                    SerialNumber     = vm.SerialNumber
                });
            }

            if (errors.Count > 0)
            {
                WinMsgBox.Show("Please fix the following issues:\n\n" + string.Join("\n", errors),
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (requests.Count == 0)
            {
                WinMsgBox.Show("No requests to save.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var repo       = new RequestRepository();
                var insertedIds = new List<int>();

                foreach (var req in requests)
                {
                    int newId = repo.AddRequest(req);
                    if (newId > 0) insertedIds.Add(newId);
                    else WinMsgBox.Show($"Unexpected id={newId} for item {req.ItemName}", "Warning",
                             MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Mouse.OverrideCursor = null;

                if (insertedIds.Count > 0)
                {
                    WinMsgBox.Show($"Successfully inserted {insertedIds.Count} request(s).", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // A batch is naturally "one request with several items" from the user's
                    // point of view, even though dbo.Request stores one row per item. Offer to
                    // group the rows just created under a Set so they stay linked together —
                    // this fires for every entry point that reuses this dialog (Batch Add
                    // Request, Bulk Add Request from ViewItemsPage, etc.), not just one of them.
                    var addToSet = WinMsgBox.Show(
                        $"Add {(insertedIds.Count == 1 ? "this request" : "these " + insertedIds.Count + " requests")} to a Set?",
                        "Add to Set", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (addToSet == MessageBoxResult.Yes)
                        await OfferAddToSetAsync(insertedIds);
                }
                else
                {
                    WinMsgBox.Show("No requests were inserted. Check the data and try again.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                WinMsgBox.Show($"Failed saving requests:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Lets the user pick an existing Set or create a new one, then links every request
        // just saved to it via SetRepository.AddRequestToSetAsync (same mechanism used by
        // the Set management screens) so the whole batch is grouped together.
        private async System.Threading.Tasks.Task OfferAddToSetAsync(List<int> insertedIds)
        {
            using (var dlg = new Yakult.Inventory.App.Pages.Set.AddRequestsToSetDialog(insertedIds.Count))
            {
                if (dlg.ShowDialog() != WinForms.DialogResult.OK || !dlg.ResultSetId.HasValue)
                    return;

                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    var setRepo = new SetRepository();
                    foreach (var reqId in insertedIds)
                        await setRepo.AddRequestToSetAsync(reqId, dlg.ResultSetId.Value);
                    Mouse.OverrideCursor = null;

                    WinMsgBox.Show($"Linked {insertedIds.Count} request(s) to the Set.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Jump straight to the Set that was created/selected instead of leaving
                    // the user to go find it from the Set management screens.
                    using (var detail = new Yakult.Inventory.App.Pages.Set.ViewSetDetailPage(dlg.ResultSetId.Value))
                    {
                        var ownerHandle = new System.Windows.Interop.WindowInteropHelper(this).Owner;
                        if (ownerHandle != IntPtr.Zero)
                            new System.Windows.Interop.WindowInteropHelper(detail).Owner = ownerHandle;
                        detail.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;
                    WinMsgBox.Show($"Requests were saved, but linking them to the Set failed:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ── Visual-tree helper ────────────────────────────────────────────────────
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // RequestRowVm — one row in the DataGrid
        // ═════════════════════════════════════════════════════════════════════════
        public class RequestRowVm : INotifyPropertyChanged
        {
            private readonly List<CategoryItem> _allCategories;
            private readonly List<ItemItem>     _allItems;

            // ── Bindable properties ─────────────────────────────────────────────
            private bool         _isSelected;
            private CategoryItem _selectedCategory;
            private ItemItem     _selectedItem;
            private string       _serialNumber   = string.Empty;
            private string       _modelNumber    = string.Empty;
            private int          _quantity       = 1;
            private decimal      _unitPrice;
            private DateTime     _dateRequested  = DateTime.Now;
            private string       _fixedAsset     = string.Empty;
            private string       _status         = "Under Review";
            private string       _description    = string.Empty;
            private string       _remarks        = string.Empty;

            // ── Categories list (shared ref) ────────────────────────────────────
            public List<CategoryItem> Categories => _allCategories;

            // ── Filtered items for this row ─────────────────────────────────────
            // _candidateItems is the full list valid for the current Category (or every
            // active item when no Category is selected). FilteredItems is what the dropdown
            // actually binds to, and is rebuilt from _candidateItems whenever the search
            // text changes — typing narrows the visible options WITHOUT auto-selecting one,
            // so the user must still click (or press Enter on) an option to choose it.
            // This replaces WPF's default IsTextSearchEnabled behavior, which inline-completes
            // to the first prefix match as soon as a few characters are typed.
            //
            // Rebuilding the ObservableCollection directly (rather than applying a
            // ListCollectionView filter) is deliberate: the collection raises
            // CollectionChanged so the open dropdown reliably refreshes its visible items.
            private readonly List<ItemItem> _candidateItems = new List<ItemItem>();

            public ObservableCollection<ItemItem> FilteredItems { get; } =
                new ObservableCollection<ItemItem>();

            private string _itemSearchText = string.Empty;
            public string ItemSearchText
            {
                get => _itemSearchText;
                set
                {
                    if (_itemSearchText == value) return;
                    _itemSearchText = value ?? string.Empty;
                    Notify();
                    RebuildVisibleItems();
                }
            }

            // Repopulates FilteredItems from _candidateItems, applying the current search
            // text. The currently-selected item is always kept present so an active
            // selection is never dropped out of the list. No placeholder row is added —
            // the search TextBox itself serves as the prompt, and the cell's display
            // template shows "-- Select Item --" when nothing is chosen yet.
            private void RebuildVisibleItems()
            {
                FilteredItems.Clear();

                string term = _itemSearchText;
                foreach (var item in _candidateItems)
                {
                    bool matches = string.IsNullOrEmpty(term) ||
                        (item.DisplayName != null &&
                         item.DisplayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (matches || (_selectedItem != null && ReferenceEquals(item, _selectedItem)))
                        FilteredItems.Add(item);
                }
            }

            // ── IsSelected ─────────────────────────────────────────────────────
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; Notify(); }
            }

            // ── SelectedCategory ───────────────────────────────────────────────
            // NOTE: Category is now a convenience filter, not a prerequisite. Changing it
            // narrows FilteredItems, but the Item search box works immediately without
            // picking a Category first — see SelectedItem's setter, which auto-fills
            // Category (along with everything else) once an Item is chosen.
            public CategoryItem SelectedCategory
            {
                get => _selectedCategory;
                set
                {
                    if (_selectedCategory == value) return;
                    _selectedCategory = value;
                    Notify();
                    RefreshFilteredItems();

                    // Only clear the selected item if it no longer matches the new category
                    // filter — avoids wiping out an item the user just picked via search,
                    // whose category selection was just auto-set to match (see SelectedItem).
                    if (_selectedItem != null && value != null &&
                        _selectedItem.CategoryId != value.CategoryId)
                    {
                        _selectedItem = null;
                        Notify(nameof(SelectedItem));
                        Notify(nameof(SelectedItemDisplay));
                        Notify(nameof(SelectedItemForeground));
                    }
                }
            }

            // ── SelectedItem ───────────────────────────────────────────────────
            public ItemItem SelectedItem
            {
                get => _selectedItem;
                set
                {
                    _selectedItem = value;
                    Notify();
                    Notify(nameof(SelectedItemDisplay));
                    Notify(nameof(SelectedItemForeground));

                    // Clear the live search filter now that a choice was made — the next
                    // time this row's combo is opened it should show the full list again.
                    if (_itemSearchText.Length > 0)
                    {
                        _itemSearchText = string.Empty;
                        Notify(nameof(ItemSearchText));
                        RebuildVisibleItems();
                    }

                    if (value != null && value.Id.HasValue)
                    {
                        UnitPrice    = value.Amount;
                        SerialNumber = value.SerialNumber ?? string.Empty;
                        ModelNumber  = value.ModelNumber  ?? string.Empty;
                        Description  = value.Name         ?? string.Empty;
                        FixedAsset   = value.IsTrackedAsset ? "Yes" : "No";

                        // Searching/picking an Item directly overrides whatever Category was
                        // selected — auto-populate Category to match the chosen Item instead
                        // of requiring the user to pick Category first.
                        if (value.CategoryId.HasValue &&
                            (_selectedCategory == null || _selectedCategory.CategoryId != value.CategoryId))
                        {
                            var matchingCategory = _allCategories.FirstOrDefault(c => c.CategoryId == value.CategoryId);
                            if (matchingCategory != null)
                            {
                                _selectedCategory = matchingCategory;
                                Notify(nameof(SelectedCategory));
                                RefreshFilteredItems();
                            }
                        }
                    }
                }
            }

            // Allow code to set item without re-triggering serial/model overwrite
            public void SetItemDirectly(ItemItem item)
            {
                _selectedItem = item;
                if (item != null && item.Id.HasValue)
                {
                    _unitPrice   = item.Amount;
                    _fixedAsset  = item.IsTrackedAsset ? "Yes" : "No";
                    _description = item.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(_serialNumber))
                        _serialNumber = item.SerialNumber ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(_modelNumber))
                        _modelNumber  = item.ModelNumber  ?? string.Empty;
                }
                Notify(nameof(SelectedItem));
                Notify(nameof(SelectedItemDisplay));
                Notify(nameof(SelectedItemForeground));
                Notify(nameof(UnitPrice));
                Notify(nameof(SerialNumber));
                Notify(nameof(ModelNumber));
                Notify(nameof(Description));
                Notify(nameof(FixedAsset));
            }

            // ── Display helpers ────────────────────────────────────────────────
            public string SelectedItemDisplay
            {
                get
                {
                    if (_selectedItem == null || !_selectedItem.Id.HasValue)
                        return "-- Select Item --";
                    return _selectedItem.DisplayName;
                }
            }

            public Brush SelectedItemForeground =>
                (_selectedItem == null || !_selectedItem.Id.HasValue)
                    ? new SolidColorBrush(Color.FromRgb(0xA0, 0xAD, 0xB9))
                    : new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x33));

            // ── Plain properties ───────────────────────────────────────────────
            public string  SerialNumber   { get => _serialNumber;  set { _serialNumber  = value; Notify(); } }
            public string  ModelNumber    { get => _modelNumber;   set { _modelNumber   = value; Notify(); } }
            public int     Quantity       { get => _quantity;      set { _quantity      = value; Notify(); } }
            public decimal UnitPrice      { get => _unitPrice;     set { _unitPrice     = value; Notify(); } }
            public DateTime DateRequested { get => _dateRequested; set { _dateRequested = value; Notify(); } }
            public string  FixedAsset     { get => _fixedAsset;    set { _fixedAsset    = value; Notify(); } }
            public string  Status         { get => _status;        set { _status        = value; Notify(); } }
            public string  Description    { get => _description;   set { _description   = value; Notify(); } }
            public string  Remarks        { get => _remarks;       set { _remarks       = value; Notify(); } }

            // ── Constructor ────────────────────────────────────────────────────
            public RequestRowVm(List<CategoryItem> categories, List<ItemItem> allItems)
            {
                _allCategories = categories;
                _allItems      = allItems;
                RefreshFilteredItems();
            }

            // Categories in scope for the ConsumableModel grouping — matches the fuzzy
            // (case/space-insensitive) filter used in RequestRepository.FulfillmentTrackedRequestsCte.
            private static string CanonicalConsumableCategory(string categoryName)
            {
                if (string.IsNullOrWhiteSpace(categoryName)) return null;
                var normalized = categoryName.Replace(" ", "").ToLowerInvariant();
                if (normalized.Contains("ink")) return "Ink";
                if (normalized.Contains("toner")) return "Toner";
                if (normalized.Contains("printhead")) return "Print Head";
                return null;
            }

            // ── Refresh item dropdown for current category ─────────────────────
            // Category is a convenience filter now, not a prerequisite: with no Category
            // selected, every active Item is offered so the user can search directly by
            // name/serial without picking a Category first (SelectedItem's setter then
            // auto-fills Category from whatever Item gets chosen).
            private void RefreshFilteredItems()
            {
                _candidateItems.Clear();

                var categoryItems = _selectedCategory == null
                    ? _allItems
                    : _allItems.Where(x => x.CategoryId == _selectedCategory.CategoryId).ToList();

                bool isConsumableCategory = _selectedCategory != null &&
                    CanonicalConsumableCategory(_selectedCategory.Name) != null;

                if (!isConsumableCategory)
                {
                    _candidateItems.AddRange(categoryItems);
                    RebuildVisibleItems();
                    return;
                }

                // Ink/Toner/Print Head: collapse to one entry per ConsumableModel so
                // near-duplicate/typo'd Item names don't show up as separate picks —
                // mirrors the same de-duplication ConsumableModel already provides for
                // stock aggregation (see availableStock computation in the save handler).
                var withModel    = categoryItems.Where(x => x.ConsumableModelId.HasValue);
                var withoutModel = categoryItems.Where(x => !x.ConsumableModelId.HasValue);

                foreach (var group in withModel.GroupBy(x => x.ConsumableModelId.Value))
                {
                    // Represent the whole model by whichever underlying Item currently
                    // holds the most stock — that's the one actually worth linking the
                    // new Request to (ItemId is still a required single-row FK).
                    var representative = group.OrderByDescending(x => x.StockOnHand).ThenBy(x => x.Id).First();
                    _candidateItems.Add(new ItemItem
                    {
                        Id                  = representative.Id,
                        Name                = !string.IsNullOrWhiteSpace(representative.ConsumableModelName)
                                                   ? representative.ConsumableModelName
                                                   : representative.Name,
                        StockOnHand         = group.Sum(x => x.StockOnHand),
                        CategoryId          = representative.CategoryId,
                        CategoryName        = representative.CategoryName,
                        SerialNumber        = representative.SerialNumber,
                        ModelNumber         = representative.ModelNumber,
                        Amount              = representative.Amount,
                        IsAvailable         = representative.IsAvailable,
                        IsTrackedAsset      = representative.IsTrackedAsset,
                        ConsumableModelId   = representative.ConsumableModelId,
                        ConsumableModelName = representative.ConsumableModelName,
                        Remarks             = representative.Remarks
                    });
                }

                // Items not yet linked to a model still show individually so nothing
                // silently disappears from the picker.
                _candidateItems.AddRange(withoutModel);

                RebuildVisibleItems();
            }

            // ── INotifyPropertyChanged ─────────────────────────────────────────
            public event PropertyChangedEventHandler PropertyChanged;
            private void Notify([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ═════════════════════════════════════════════════════════════════════════
        // Inner data classes (mirrors the originals)
        // ═════════════════════════════════════════════════════════════════════════
        private class OrgItem
        {
            public int    Id   { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class EmployeeItem
        {
            public int?   Id             { get; set; }
            public string Name           { get; set; }
            public string EmployeeNumber { get; set; }
            public string Position       { get; set; }
            public int?   ComId          { get; set; }
            public int?   DeptId         { get; set; }
            public int?   BranchId       { get; set; }
            public int?   DistributorId  { get; set; }

            public string DisplayText
            {
                get
                {
                    string num  = string.IsNullOrWhiteSpace(EmployeeNumber) ? "" : EmployeeNumber + " - ";
                    string pos  = string.IsNullOrWhiteSpace(Position)       ? "" : $" ({Position})";
                    return $"{num}{Name ?? string.Empty}{pos}";
                }
            }

            public override string ToString() => DisplayText;
        }

        public class CategoryItem
        {
            public int    CategoryId { get; set; }
            public string Name       { get; set; }
            public override string ToString() => Name;
        }

        public class ItemItem
        {
            public int?    Id                 { get; set; }
            public string  Name               { get; set; }
            public int     StockOnHand        { get; set; }
            public int?    CategoryId         { get; set; }
            public string  CategoryName       { get; set; }
            public string  SerialNumber       { get; set; }
            public string  ModelNumber        { get; set; }
            public decimal Amount             { get; set; }
            public bool    IsAvailable        { get; set; }
            public bool    IsTrackedAsset     { get; set; }
            public int?    ConsumableModelId  { get; set; }
            public string  ConsumableModelName { get; set; }
            public string  Remarks            { get; set; }

            public string DisplayName
            {
                get
                {
                    string label = string.IsNullOrWhiteSpace(SerialNumber)
                        ? Name
                        : $"{Name} (SN: {SerialNumber})";
                    return string.IsNullOrWhiteSpace(Remarks)
                        ? label
                        : $"{label} — {Remarks}";
                }
            }

            public override string ToString() => DisplayName;
        }
    }
}
