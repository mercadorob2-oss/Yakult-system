using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

using WinForms  = System.Windows.Forms;
using WinMsgBox = System.Windows.MessageBox;

namespace Yakult.Inventory.App.Pages.Request
{
    /// <summary>
    /// One-page builder for a Request Set — the on-screen counterpart to Batch Add Request +
    /// "Add to Set", modelled on <see cref="Yakult.Inventory.App.Pages.Item.InvoiceBuilderDialog"/>.
    ///
    /// Every line is entered here: the requester (or a Dept-level org unit), and the line items.
    /// A line whose "Existing?" box is left unticked describes a brand-new item — nothing is
    /// written for it until <see cref="BtnCreate_Click"/> runs, which then, in one sequence:
    ///   1. creates the Set,
    ///   2. creates the dbo.Item for every new-item line,
    ///   3. creates one dbo.Request per line,
    ///   4. links each Request to the Set (which stamps SetType and materialises SetItem rows).
    ///
    /// So "make the set" is what adds the new items to the system, exactly like Build Invoice
    /// creates its items only when the invoice is built.
    /// </summary>
    public partial class RequestSetBuilderDialog : Window, IDisposable
    {
        // Bound by the grid's DataGridComboBoxColumns via x:Static, so they must be static and
        // keep their identity — populate in place rather than reassigning (same pattern as
        // InvoiceBuilderDialog.CategoryOptions / ItemTypeOptions / VendorOptions).
        public static ObservableCollection<string> ItemTypeOptions { get; } =
            new ObservableCollection<string> { "Hardware", "Software/License", "Services" };

        public static ObservableCollection<VendorItem> VendorOptions { get; } =
            new ObservableCollection<VendorItem>();

        public static ObservableCollection<ConditionItem> ConditionOptions { get; } =
            new ObservableCollection<ConditionItem>();

        private readonly List<CategoryItem>  _categories = new List<CategoryItem>();
        private readonly List<ItemLookup>    _allItems   = new List<ItemLookup>();
        private readonly List<EmployeeItem>  _employees  = new List<EmployeeItem>();
        private readonly List<OrgItem>       _companies   = new List<OrgItem>();
        private readonly List<OrgItem>       _departments = new List<OrgItem>();
        private readonly List<OrgItem>       _branches    = new List<OrgItem>();

        private readonly ObservableCollection<LineVm> _lines = new ObservableCollection<LineVm>();

        // Real dbo.Condition id for "Good" — this DB does not guarantee it is 1, and a bad id
        // trips FK_Item_Condition. Resolved once in LoadLookups, same as InvoiceCsvImportDialog.
        private int _defaultConditionId = 1;

        // Type-to-filter state for the Requester ComboBox — WPF's IsTextSearchEnabled only does
        // prefix auto-complete, so a live CollectionView filter is wired up instead, mirroring
        // BatchAddRequestDialog's FilterOrgCombo. The filter pass is debounced so a fast typist
        // isn't blocked re-running it (and re-generating dropdown containers) on every keystroke.
        private System.Windows.Data.ListCollectionView _empView;
        private System.Windows.Threading.DispatcherTimer _empFilterTimer;
        private bool _suppressEmpFilter;

        // Manual maximise/restore — AllowsTransparency windows don't honour WindowState.Maximized
        // bounds at the right time, same handling as BatchAddRequestDialog.
        private bool   _isMaximized;
        private double _normalLeft, _normalTop, _normalWidth, _normalHeight;

        /// <summary>Set when the dialog created a Set, so the caller knows to refresh.</summary>
        public bool CreatedSet { get; private set; }

        /// <summary>SetId of the Set created (0 until Create succeeds).</summary>
        public int CreatedSetId { get; private set; }

        public RequestSetBuilderDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public void Dispose() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DgvLines.ItemsSource = _lines;
            LoadLookups();
            AddLine();

            _empFilterTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(140)
            };
            _empFilterTimer.Tick += (s, _) => { _empFilterTimer.Stop(); ApplyEmployeeFilter(); };

            // The TextChanged bubbles from the ComboBox's inner PART_EditableTextBox. Keystrokes
            // just (re)start the debounce timer — the actual filter pass runs once typing pauses.
            CmbEmployee.AddHandler(
                System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler((s, _) =>
                {
                    if (_suppressEmpFilter) return;
                    if (!CmbEmployee.IsDropDownOpen) CmbEmployee.IsDropDownOpen = true;
                    _empFilterTimer.Stop();
                    _empFilterTimer.Start();
                }));
        }

        // ── ShowDialog WinForms compatibility ────────────────────────────────────
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

        // ── Window chrome ───────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !_isMaximized) DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this);
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_isMaximized)
            {
                _isMaximized  = false;
                MaxWidth      = double.PositiveInfinity;
                MaxHeight     = double.PositiveInfinity;
                Left          = _normalLeft;
                Top           = _normalTop;
                Width         = _normalWidth;
                Height        = _normalHeight;
                BtnMaximizeGlyph.Text = "□";
            }
            else
            {
                _normalLeft   = Left;
                _normalTop    = Top;
                _normalWidth  = Width;
                _normalHeight = Height;
                var wa        = GetCurrentMonitorWorkArea();
                MaxWidth      = wa.Width;
                MaxHeight     = wa.Height;
                Left          = wa.Left;
                Top           = wa.Top;
                Width         = wa.Width;
                Height        = wa.Height;
                _isMaximized  = true;
                BtnMaximizeGlyph.Text = "❐";
            }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = CreatedSet;
        }

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

        // ── Lookups ─────────────────────────────────────────────────────────────
        private void LoadLookups()
        {
            var cs = DatabaseConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
            {
                LblStatus.Text = "No database connection — lookups could not be loaded.";
                return;
            }

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
                            _categories.Add(new CategoryItem { CategoryId = r.GetInt32(0), Name = name });
                        }
                    }

                    const string itemSql = @"
                        SELECT i.ItemId, i.Name, i.CategoryId, i.SerialNumber, i.ModelNumber, i.Amount
                        FROM dbo.Item i
                        WHERE i.Active = 1
                        ORDER BY i.Name";
                    using (var cmd = new SqlCommand(itemSql, con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            _allItems.Add(new ItemLookup
                            {
                                Id           = r.GetInt32(0),
                                Name         = r.IsDBNull(1) ? "" : r.GetString(1),
                                CategoryId   = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                                SerialNumber = r.IsDBNull(3) ? null : r.GetString(3),
                                ModelNumber  = r.IsDBNull(4) ? null : r.GetString(4),
                                Amount       = r.IsDBNull(5) ? 0m : r.GetDecimal(5)
                            });
                    }

                    using (var cmd = new SqlCommand(
                        "SELECT EmpId, Name, EmployeeNumber, Position, ComId, DeptId, BranchId " +
                        "FROM dbo.Employee WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            _employees.Add(new EmployeeItem
                            {
                                Id             = r.GetInt32(0),
                                Name           = r.IsDBNull(1) ? "" : r.GetString(1),
                                EmployeeNumber = r.IsDBNull(2) ? null : r.GetString(2),
                                Position       = r.IsDBNull(3) ? "" : r.GetString(3),
                                ComId          = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                                DeptId         = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                                BranchId       = r.IsDBNull(6) ? (int?)null : r.GetInt32(6)
                            });
                    }

                    _companies.Add(new OrgItem { Id = 0, Name = "-- Select Company --" });
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) _companies.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    _departments.Add(new OrgItem { Id = 0, Name = "-- Any Dept. --" });
                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) _departments.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    _branches.Add(new OrgItem { Id = 0, Name = "-- Any Branch --" });
                    using (var cmd = new SqlCommand("SELECT BranchId, Name FROM dbo.Branch WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) _branches.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    VendorOptions.Clear();
                    VendorOptions.Add(new VendorItem { Id = 0, Name = "(none)" });
                    using (var cmd = new SqlCommand(
                        "SELECT VendorID, VendorName FROM dbo.Vendor WHERE IsActive = 1 ORDER BY VendorName", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            if (!r.IsDBNull(1))
                                VendorOptions.Add(new VendorItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    ConditionOptions.Clear();
                    using (var cmd = new SqlCommand(
                        "SELECT ConditionId, ConditionName FROM dbo.Condition ORDER BY ConditionId", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            ConditionOptions.Add(new ConditionItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    var good = ConditionOptions.FirstOrDefault(c =>
                                   string.Equals(c.Name, "Good", StringComparison.OrdinalIgnoreCase))
                               ?? ConditionOptions.FirstOrDefault();
                    if (good != null) _defaultConditionId = good.Id;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("RequestSetBuilderDialog: failed to load lookups", ex);
                LblStatus.Text = "Some lookups could not be loaded — you can still type values in manually.";
            }

            if (VendorOptions.Count == 0)
                VendorOptions.Add(new VendorItem { Id = 0, Name = "(none)" });

            _empView = new System.Windows.Data.ListCollectionView(_employees);
            _empView.MoveCurrentToPosition(-1);   // no employee pre-selected
            CmbEmployee.ItemsSource   = _empView;
            CmbCompany.ItemsSource    = _companies;    CmbCompany.SelectedIndex    = 0;
            CmbDepartment.ItemsSource = _departments;  CmbDepartment.SelectedIndex = 0;
            CmbBranch.ItemsSource     = _branches;     CmbBranch.SelectedIndex     = 0;
        }

        private void ChkNoEmployee_Changed(object sender, RoutedEventArgs e)
        {
            bool noEmp = ChkNoEmployee.IsChecked == true;
            PnlEmployee.Visibility = noEmp ? Visibility.Collapsed : Visibility.Visible;
            PnlOrg.Visibility      = noEmp ? Visibility.Visible   : Visibility.Collapsed;
            TxtInfoBanner.Text = noEmp
                ? "Dept-level Request Set: pick a Company (required), then add line items. New items are created when you press Create Request Set."
                : "Build a Request Set on one page. Add line items — leave \"Existing?\" unticked to enter a brand-new item; it is created in the system when you press Create Request Set. Nothing is saved until then.";
        }

        // ── Requester type-to-filter ───────────────────────────────────────────
        private TextBox EmployeeEditBox =>
            CmbEmployee.Template?.FindName("PART_EditableTextBox", CmbEmployee) as TextBox;

        // Runs once the debounce timer fires. Reads the live text (not a stale keystroke value)
        // so a burst of typing collapses into a single filter pass.
        private void ApplyEmployeeFilter()
        {
            if (_empView == null) return;

            var tb = EmployeeEditBox;
            string filter = (tb?.Text ?? string.Empty);
            int caret = tb?.SelectionStart ?? filter.Length;

            // Text that exactly matches the current selection means a pick was just committed.
            if (CmbEmployee.SelectedItem is EmployeeItem picked &&
                string.Equals(picked.DisplayText, filter, StringComparison.OrdinalIgnoreCase))
                return;

            _suppressEmpFilter = true;
            try
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    _empView.Filter = null;
                }
                else
                {
                    string f = filter.Trim();
                    _empView.Filter = o =>
                        o is EmployeeItem e &&
                        ((e.DisplayText != null && e.DisplayText.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         (e.Name != null && e.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         (e.EmployeeNumber != null && e.EmployeeNumber.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         (e.Position != null && e.Position.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!CmbEmployee.IsDropDownOpen) CmbEmployee.IsDropDownOpen = true;
                }

                // Re-applying the filter can drop the selection and blank the editable text —
                // restore exactly what the user has typed, caret included.
                if (tb != null && tb.Text != filter)
                {
                    tb.Text = filter;
                    tb.Select(Math.Min(caret, filter.Length), 0);
                }
            }
            finally { _suppressEmpFilter = false; }
        }

        private void CmbEmployee_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressEmpFilter || _empView == null) return;

            _suppressEmpFilter = true;
            _empView.Filter = null;
            _suppressEmpFilter = false;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                EmployeeEditBox?.SelectAll();
                if (!CmbEmployee.IsDropDownOpen) CmbEmployee.IsDropDownOpen = true;
            }));
        }

        private void CmbEmployee_DropDownClosed(object sender, EventArgs e)
        {
            _empFilterTimer?.Stop();
            _suppressEmpFilter = true;
            try
            {
                // Nothing clicked but the filter narrowed to a single match — take it.
                if (!(CmbEmployee.SelectedItem is EmployeeItem) && _empView != null)
                {
                    var visible = _empView.Cast<object>().OfType<EmployeeItem>().Take(2).ToList();
                    if (visible.Count == 1) CmbEmployee.SelectedItem = visible[0];
                }

                var sel = CmbEmployee.SelectedItem as EmployeeItem;
                var tb  = EmployeeEditBox;
                if (tb != null && sel != null)
                {
                    tb.Text = sel.DisplayText;
                    tb.Select(tb.Text.Length, 0);
                }

                if (_empView != null) _empView.Filter = null;
            }
            finally { _suppressEmpFilter = false; }
        }

        // ── Line management ─────────────────────────────────────────────────────
        private LineVm AddLine()
        {
            var vm = new LineVm(_categories, _allItems);
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LineVm.IsSelected)) UpdateRemoveButton();
                else RefreshStatusLine();
            };
            _lines.Add(vm);
            RefreshStatusLine();
            return vm;
        }

        private void UpdateRemoveButton() => BtnRemoveLine.IsEnabled = _lines.Any(l => l.IsSelected);

        private void RefreshStatusLine()
        {
            int filled   = _lines.Count(l => !l.IsBlank);
            int newItems = _lines.Count(l => !l.IsBlank && !l.IsExistingItem);
            LblStatus.Text = filled == 0
                ? "Add at least one line item."
                : $"{filled} line item(s) — {newItems} new, {filled - newItems} existing.";
        }

        private void BtnAddLine_Click(object sender, RoutedEventArgs e) => AddLine();

        private void BtnAddFiveLines_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 5; i++) AddLine();
        }

        private void BtnRemoveLine_Click(object sender, RoutedEventArgs e)
        {
            var selected = _lines.Where(l => l.IsSelected).ToList();
            if (selected.Count == 0) return;

            if (WinMsgBox.Show(
                    selected.Count == 1 ? "Remove the selected line?" : $"Remove {selected.Count} selected lines?",
                    "Remove Line", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            foreach (var l in selected) _lines.Remove(l);
            if (_lines.Count == 0) AddLine();
            UpdateRemoveButton();
            RefreshStatusLine();
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool check = (sender as CheckBox)?.IsChecked == true;
            foreach (var l in _lines) l.IsSelected = check;
            UpdateRemoveButton();
        }

        // ── DataGrid behaviour ─────────────────────────────────────────────────
        private void DgvLines_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell != null && !cell.IsEditing && !cell.IsReadOnly)
            {
                cell.Focus();
                DgvLines.BeginEdit();
            }
        }

        // Columns that describe the new item itself — locked once the row points at an item that
        // already exists (its values come from the picked item), like InvoiceBuilderDialog's
        // ExistingItemLockedCell. Item Name and Category stay editable: this dialog picks the
        // existing item in-cell, and Category filters that picker.
        private static readonly HashSet<string> ExistingItemLockedHeaders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Description", "Item Type", "Condition", "Serial Number", "Model Number",
            "Vendor", "UoM", "License #", "Part #", "Warranty (Yrs)", "Remarks",
            "Date Purchased", "Warranty Start", "Warranty End"
        };

        private void DgvLines_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row?.Item is LineVm vm && vm.IsExistingItem
                && e.Column?.Header is string header && ExistingItemLockedHeaders.Contains(header))
            {
                e.Cancel = true;
            }
        }

        // ── Create ─────────────────────────────────────────────────────────────
        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            DgvLines.CommitEdit(DataGridEditingUnit.Cell, true);
            DgvLines.CommitEdit(DataGridEditingUnit.Row, true);

            var errors = new List<string>();

            // Requester / org unit
            bool noEmp = ChkNoEmployee.IsChecked == true;
            EmployeeItem emp = null;
            int? comId = null, deptId = null, branchId = null;

            if (noEmp)
            {
                var c = CmbCompany.SelectedItem as OrgItem;
                if (c == null || c.Id <= 0) errors.Add("Pick a Company for this Dept-level Request Set.");
                else comId = c.Id;
                if (CmbDepartment.SelectedItem is OrgItem d && d.Id > 0) deptId = d.Id;
                if (CmbBranch.SelectedItem is OrgItem b && b.Id > 0) branchId = b.Id;
            }
            else
            {
                emp = CmbEmployee.SelectedItem as EmployeeItem;
                if (emp == null)
                {
                    var typed = (CmbEmployee.Text ?? string.Empty).Trim();
                    emp = _employees.FirstOrDefault(x =>
                        string.Equals(x.DisplayText, typed, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase));
                }
                if (emp == null) errors.Add("Select a requester, or tick \"No employee (Dept. level)\".");
                else { comId = emp.ComId; deptId = emp.DeptId; branchId = emp.BranchId; }
            }

            // Lines
            var work = new List<LineVm>();
            int n = 0;
            foreach (var vm in _lines)
            {
                if (vm.IsBlank) continue;
                n++;

                if (vm.SelectedCategory == null)
                    errors.Add($"Line {n}: Category is required.");

                if (vm.IsExistingItem)
                {
                    if (vm.SelectedItem == null || !vm.SelectedItem.Id.HasValue)
                        errors.Add($"Line {n}: \"Existing?\" is ticked but no item was picked.");
                }
                else if (string.IsNullOrWhiteSpace(vm.NewItemName))
                {
                    errors.Add($"Line {n}: Item name is required for a new item.");
                }

                if (vm.Quantity <= 0)
                    errors.Add($"Line {n}: Qty must be greater than 0.");

                work.Add(vm);
            }

            if (work.Count == 0)
                errors.Add("Add at least one line item.");

            if (errors.Count > 0)
            {
                WinMsgBox.Show(
                    "Fix the following before creating the Request Set:\n\n   " +
                    string.Join("\n   ", errors.Take(15)) +
                    (errors.Count > 15 ? $"\n   … and {errors.Count - 15} more" : string.Empty),
                    "Incomplete Request Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int newCount = work.Count(w => !w.IsExistingItem);
            var confirm = WinMsgBox.Show(
                $"Create a Request Set with {work.Count} line(s)?\n\n" +
                $"{newCount} new item(s) will be created in the system and bundled into the Set.",
                "Create Request Set", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            BtnCreate.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            LblStatus.Text = "Creating Request Set…";

            int setId = 0;
            int linesDone = 0;
            try
            {
                var itemRepo = new ItemRepository();
                var reqRepo  = new RequestRepository();
                var setRepo  = new SetRepository();

                DateTime? dispatch = DpDispatchDate.SelectedDate;
                string remarks = string.IsNullOrWhiteSpace(TxtSetRemarks.Text) ? null : TxtSetRemarks.Text.Trim();

                // 1) The Set first — if this fails, nothing else has happened.
                setId = await setRepo.CreateSetAsync(AppSession.CurrentUserId, remarks, dispatch);

                // 2) For each line: create the Item (new rows), create the Request, link it.
                // Suppress per-item "added X" rows — one "Request Set created" row is emitted below.
                using var _suppressItemRows = Yakult.Inventory.App.Services.InventoryActivityNotifier.SuppressItemAdded();
                var activityItems = new System.Collections.Generic.List<Yakult.Inventory.App.Services.InventoryActivityNotifier.ActivityItemDetail>();
                foreach (var vm in work)
                {
                    int itemId;
                    string itemName;

                    if (vm.IsExistingItem)
                    {
                        itemId   = vm.SelectedItem.Id.Value;
                        itemName = vm.SelectedItem.Name;
                    }
                    else
                    {
                        itemName = vm.NewItemName.Trim();
                        itemId   = itemRepo.AddItem(BuildNewItemDto(vm, itemName));
                        if (itemId <= 0)
                            throw new InvalidOperationException($"Could not create item '{itemName}'.");
                    }

                    activityItems.Add(new Yakult.Inventory.App.Services.InventoryActivityNotifier.ActivityItemDetail
                    {
                        id     = itemId,
                        name   = itemName,
                        type   = string.IsNullOrWhiteSpace(vm.ItemType) ? null : vm.ItemType.Trim(),
                        serial = string.IsNullOrWhiteSpace(vm.SerialNumber) ? null : vm.SerialNumber.Trim(),
                        qty    = vm.Quantity > 1 ? vm.Quantity : 0,
                    });

                    var dto = new RequestDto
                    {
                        EmpId            = emp?.Id,
                        EmployeeName     = emp?.Name,
                        EmployeePosition = emp?.Position,
                        ComId            = comId,
                        DeptId           = deptId,
                        BranchId         = branchId,
                        ItemId           = itemId,
                        ItemName         = itemName,
                        Quantity         = vm.Quantity,
                        IssuedQty        = 0,
                        UnitPrice        = vm.UnitPrice,
                        DateRequested    = vm.DateRequested,
                        // dbo.Request.Description is NOT NULL — fall back to the item name, same
                        // as InvoiceCsvImportDialog / BatchAddRequestDialog.
                        Description      = string.IsNullOrWhiteSpace(vm.Description) ? itemName : vm.Description.Trim(),
                        Remarks          = string.IsNullOrWhiteSpace(vm.Remarks) ? null : vm.Remarks.Trim(),
                        Status           = vm.Status,
                        ConditionID      = vm.SelectedCondition?.Id,
                        EntryType        = "Negative",
                        RequestSource    = "INTERNAL",
                        DateCreated      = DateTime.Now,
                        CreatedByUserId  = AppSession.CurrentUserId,
                        ModifiedByUserId = AppSession.CurrentUserId,
                        CreatedByName    = AppSession.CurrentUserName,
                        SerialNumber     = string.IsNullOrWhiteSpace(vm.SerialNumber) ? null : vm.SerialNumber.Trim()
                    };

                    int reqId = reqRepo.AddRequest(dto);
                    if (reqId <= 0)
                        throw new InvalidOperationException($"Request for '{itemName}' could not be created.");

                    await setRepo.AddRequestToSetAsync(reqId, setId);
                    linesDone++;
                }

                Mouse.OverrideCursor = null;
                CreatedSet   = true;
                CreatedSetId = setId;

                Yakult.Inventory.App.Services.InventoryActivityNotifier.NotifySetCreated(
                    setId, linesDone, AppSession.CurrentUserId, "Request Set", activityItems);

                WinMsgBox.Show(
                    $"Request Set created with {linesDone} line(s).",
                    "Create Request Set", MessageBoxButton.OK, MessageBoxImage.Information);

                OpenSetDetail(setId);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                BtnCreate.IsEnabled = true;
                Logger.LogError("RequestSetBuilderDialog: create failed", ex);

                if (setId > 0 && linesDone > 0)
                {
                    // Some lines committed and are linked to a real Set — keep it and let the
                    // user finish it in Set detail rather than silently discarding their work.
                    LblStatus.Text = $"Stopped after {linesDone} line(s).";
                    CreatedSet   = true;
                    CreatedSetId = setId;
                    WinMsgBox.Show(
                        $"Only {linesDone} of {work.Count} line(s) were added before an error:\n\n{ex.Message}\n\n" +
                        "The Set was created with the lines that succeeded — open it to review and add the rest.",
                        "Create Request Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OpenSetDetail(setId);
                    DialogResult = true;
                    Close();
                    return;
                }

                if (setId > 0)
                {
                    // Empty Set, nothing linked — roll it back so no stray Set is left behind.
                    try { await new SetRepository().DeleteSetAsync(setId); }
                    catch (Exception delEx) { Logger.LogError("RequestSetBuilderDialog: rollback DeleteSet failed", delEx); }
                }

                LblStatus.Text = "Request Set was not created.";
                WinMsgBox.Show("The Request Set could not be created:\n\n" + ex.Message,
                    "Create Request Set", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSetDetail(int setId)
        {
            try
            {
                var detail = new Yakult.Inventory.App.Pages.Set.ViewSetDetailPage(setId);
                var ownerHandle = new System.Windows.Interop.WindowInteropHelper(this).Owner;
                if (ownerHandle == IntPtr.Zero)
                    ownerHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (ownerHandle != IntPtr.Zero)
                    new System.Windows.Interop.WindowInteropHelper(detail).Owner = ownerHandle;
                detail.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.LogError("RequestSetBuilderDialog: could not open Set detail", ex);
            }
        }

        // dbo.Item for a new-item line, carrying the same fields Batch Add Items captures
        // (CaptureCommonItemFields) — ItemRepository.AddItem also writes the baseline Inventory
        // row, the Renewal record (which holds Part #), and the audit trail. AffectsInventory /
        // AcquisitionType are derived from Item Type exactly as Batch Add Items does it.
        private ItemDto BuildNewItemDto(LineVm vm, string name)
        {
            string itemType = string.IsNullOrWhiteSpace(vm.ItemType) ? "Hardware" : vm.ItemType.Trim();
            bool isHardware  = string.Equals(itemType, "Hardware", StringComparison.OrdinalIgnoreCase);
            var  now         = DateTime.Now;

            return new ItemDto
            {
                Name              = name,
                Description       = string.IsNullOrWhiteSpace(vm.Description) ? name : vm.Description.Trim(),
                ModelNumber       = string.IsNullOrWhiteSpace(vm.ModelNumber) ? null : vm.ModelNumber.Trim(),
                SerialNumber      = string.IsNullOrWhiteSpace(vm.SerialNumber) ? null : vm.SerialNumber.Trim(),
                CategoryId        = vm.SelectedCategory?.CategoryId ?? 0,
                Category          = vm.SelectedCategory?.Name,
                UnitOfMeasure     = string.IsNullOrWhiteSpace(vm.UnitOfMeasure) ? "Unit" : vm.UnitOfMeasure.Trim(),
                StockOnHand       = 0,
                Amount            = vm.UnitPrice,
                ItemType          = itemType,
                ConditionId       = vm.SelectedCondition?.Id ?? _defaultConditionId,
                ConditionName     = vm.SelectedCondition?.Name ?? "Good",
                AffectsInventory  = isHardware,
                AcquisitionType   = isHardware ? "Both" : "Invoice",
                VendorId          = (vm.SelectedVendor != null && vm.SelectedVendor.Id > 0)
                                        ? (int?)vm.SelectedVendor.Id : null,
                VendorName        = (vm.SelectedVendor != null && vm.SelectedVendor.Id > 0)
                                        ? vm.SelectedVendor.Name : null,
                LicenseNumber     = string.IsNullOrWhiteSpace(vm.LicenseNumber) ? null : vm.LicenseNumber.Trim(),
                PartNumber        = string.IsNullOrWhiteSpace(vm.PartNumber) ? null : vm.PartNumber.Trim(),
                Remarks           = string.IsNullOrWhiteSpace(vm.Remarks) ? null : vm.Remarks.Trim(),
                WarrantyYears     = vm.WarrantyYears < 0 ? 0 : vm.WarrantyYears,
                // Same auto rule as Batch Add Items / Edit Item: a typed date wins, otherwise
                // Start defaults to creation time when Years > 0 and AddItem derives End = Start + Years.
                WarrantyStartDate = vm.WarrantyStartDate ?? (vm.WarrantyYears > 0 ? now : (DateTime?)null),
                WarrantyEndDate   = vm.WarrantyEndDate,
                DatePurchased     = vm.DatePurchased,
                Active            = true,
                DateCreated       = now,
                CreatedByUserId   = AppSession.CurrentUserId
            };
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = CreatedSet;

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LineVm — one row in the builder grid
        // ═══════════════════════════════════════════════════════════════════════
        public class LineVm : INotifyPropertyChanged
        {
            private readonly List<CategoryItem> _allCategories;
            private readonly List<ItemLookup>   _allItems;

            private bool         _isSelected;
            private bool         _isExistingItem;
            private CategoryItem _selectedCategory;
            private ItemLookup   _selectedItem;
            private string       _newItemName   = string.Empty;
            private string       _description   = string.Empty;
            private string       _itemType      = "Hardware";
            private VendorItem   _selectedVendor;
            private ConditionItem _selectedCondition;
            private string       _unitOfMeasure = "Unit";
            private string       _modelNumber   = string.Empty;
            private string       _serialNumber  = string.Empty;
            private string       _licenseNumber = string.Empty;
            private string       _partNumber    = string.Empty;
            private string       _remarks       = string.Empty;
            private int          _warrantyYears;
            private int          _quantity      = 1;
            private decimal      _unitPrice;
            private DateTime?    _datePurchased;
            private DateTime?    _warrantyStartDate;
            private DateTime?    _warrantyEndDate;
            private string       _status        = "Under Review";
            private DateTime     _dateRequested = DateTime.Now;

            public LineVm(List<CategoryItem> categories, List<ItemLookup> allItems)
            {
                _allCategories = categories;
                _allItems      = allItems;
                _selectedCondition = ConditionOptions.FirstOrDefault(c =>
                        string.Equals(c.Name, "Good", StringComparison.OrdinalIgnoreCase))
                    ?? ConditionOptions.FirstOrDefault();
                RefreshFilteredItems();
            }

            public List<CategoryItem> Categories => _allCategories;

            public ObservableCollection<ItemLookup> FilteredItems { get; } =
                new ObservableCollection<ItemLookup>();

            /// <summary>An untouched spare row — ignored on Create, never an error.</summary>
            public bool IsBlank =>
                !_isExistingItem
                && _selectedItem == null
                && string.IsNullOrWhiteSpace(_newItemName)
                && string.IsNullOrWhiteSpace(_modelNumber)
                && string.IsNullOrWhiteSpace(_serialNumber);

            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; Notify(); }
            }

            public bool IsExistingItem
            {
                get => _isExistingItem;
                set
                {
                    if (_isExistingItem == value) return;
                    _isExistingItem = value;
                    if (value) _newItemName = string.Empty;
                    else       _selectedItem = null;
                    Notify();
                    Notify(nameof(NewItemName));
                    Notify(nameof(SelectedItem));
                    Notify(nameof(ItemDisplay));
                    Notify(nameof(ItemDisplayForeground));
                }
            }

            public CategoryItem SelectedCategory
            {
                get => _selectedCategory;
                set
                {
                    if (_selectedCategory == value) return;
                    _selectedCategory = value;
                    Notify();
                    RefreshFilteredItems();
                    _selectedItem = null;
                    Notify(nameof(SelectedItem));
                    Notify(nameof(ItemDisplay));
                    Notify(nameof(ItemDisplayForeground));
                }
            }

            public ItemLookup SelectedItem
            {
                get => _selectedItem;
                set
                {
                    _selectedItem = value;
                    if (value != null && value.Id.HasValue)
                    {
                        _unitPrice    = value.Amount;
                        _modelNumber  = value.ModelNumber  ?? _modelNumber;
                        _serialNumber = value.SerialNumber ?? _serialNumber;
                        _description  = value.Name         ?? _description;
                    }
                    Notify();
                    Notify(nameof(UnitPrice));
                    Notify(nameof(ModelNumber));
                    Notify(nameof(SerialNumber));
                    Notify(nameof(Description));
                    Notify(nameof(ItemDisplay));
                    Notify(nameof(ItemDisplayForeground));
                }
            }

            public string NewItemName
            {
                get => _newItemName;
                set
                {
                    _newItemName = value;
                    Notify();
                    Notify(nameof(ItemDisplay));
                    Notify(nameof(ItemDisplayForeground));
                }
            }

            public string ItemDisplay
            {
                get
                {
                    if (_isExistingItem)
                        return _selectedItem != null && _selectedItem.Id.HasValue
                            ? _selectedItem.DisplayName
                            : (_selectedCategory == null ? "Pick a category first" : "-- pick an existing item --");
                    return string.IsNullOrWhiteSpace(_newItemName)
                        ? "-- type a new item name --"
                        : _newItemName;
                }
            }

            public Brush ItemDisplayForeground =>
                ((_isExistingItem && _selectedItem != null && _selectedItem.Id.HasValue) ||
                 (!_isExistingItem && !string.IsNullOrWhiteSpace(_newItemName)))
                    ? new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x33))
                    : new SolidColorBrush(Color.FromRgb(0xA0, 0xAD, 0xB9));

            public string    Description       { get => _description;   set { _description   = value; Notify(); Notify(nameof(ItemDisplay)); } }
            public string    ItemType          { get => _itemType;      set { _itemType      = value; Notify(); } }
            public VendorItem SelectedVendor   { get => _selectedVendor; set { _selectedVendor = value; Notify(); } }
            public ConditionItem SelectedCondition { get => _selectedCondition; set { _selectedCondition = value; Notify(); } }
            public string    UnitOfMeasure     { get => _unitOfMeasure; set { _unitOfMeasure = value; Notify(); } }
            public string    ModelNumber       { get => _modelNumber;   set { _modelNumber   = value; Notify(); Notify(nameof(ItemDisplay)); } }
            public string    SerialNumber      { get => _serialNumber;  set { _serialNumber  = value; Notify(); Notify(nameof(ItemDisplay)); } }
            public string    LicenseNumber     { get => _licenseNumber; set { _licenseNumber = value; Notify(); } }
            public string    PartNumber        { get => _partNumber;    set { _partNumber    = value; Notify(); } }
            public string    Remarks           { get => _remarks;       set { _remarks       = value; Notify(); } }
            public int       WarrantyYears     { get => _warrantyYears; set { _warrantyYears = value; Notify(); } }
            public int       Quantity          { get => _quantity;      set { _quantity      = value; Notify(); Notify(nameof(Amount)); } }
            public decimal   UnitPrice         { get => _unitPrice;     set { _unitPrice     = value; Notify(); Notify(nameof(Amount)); } }
            public decimal   Amount            => _quantity * _unitPrice;
            public DateTime? DatePurchased     { get => _datePurchased;     set { _datePurchased     = value; Notify(); } }
            public DateTime? WarrantyStartDate { get => _warrantyStartDate; set { _warrantyStartDate = value; Notify(); } }
            public DateTime? WarrantyEndDate   { get => _warrantyEndDate;   set { _warrantyEndDate   = value; Notify(); } }
            public string    Status            { get => _status;        set { _status        = value; Notify(); } }
            public DateTime  DateRequested     { get => _dateRequested; set { _dateRequested = value; Notify(); } }

            private void RefreshFilteredItems()
            {
                FilteredItems.Clear();
                if (_selectedCategory == null) return;
                foreach (var it in _allItems.Where(x => x.CategoryId == _selectedCategory.CategoryId))
                    FilteredItems.Add(it);
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Notify([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ── Lookup rows ────────────────────────────────────────────────────────
        public class CategoryItem
        {
            public int    CategoryId { get; set; }
            public string Name       { get; set; }
            public override string ToString() => Name;
        }

        public class ItemLookup
        {
            public int?    Id           { get; set; }
            public string  Name         { get; set; }
            public int?    CategoryId   { get; set; }
            public string  SerialNumber { get; set; }
            public string  ModelNumber  { get; set; }
            public decimal Amount       { get; set; }

            public string DisplayName =>
                string.IsNullOrWhiteSpace(SerialNumber) ? Name : $"{Name} (SN: {SerialNumber})";

            public override string ToString() => DisplayName;
        }

        private class EmployeeItem
        {
            public int    Id             { get; set; }
            public string Name           { get; set; }
            public string EmployeeNumber { get; set; }
            public string Position       { get; set; }
            public int?   ComId          { get; set; }
            public int?   DeptId         { get; set; }
            public int?   BranchId       { get; set; }

            public string DisplayText
            {
                get
                {
                    string num = string.IsNullOrWhiteSpace(EmployeeNumber) ? "" : EmployeeNumber + " - ";
                    string pos = string.IsNullOrWhiteSpace(Position) ? "" : $" ({Position})";
                    return $"{num}{Name ?? string.Empty}{pos}";
                }
            }

            public override string ToString() => DisplayText;
        }

        private class OrgItem
        {
            public int    Id   { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class VendorItem
        {
            public int    Id   { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class ConditionItem
        {
            public int    Id   { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}
