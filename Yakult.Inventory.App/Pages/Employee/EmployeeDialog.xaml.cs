using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;

// Disambiguate MessageBox / DialogResult — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Employee
{
    public partial class EmployeeDialog : Window, IDisposable
    {
        // Store the result — same public contract as the WinForms version
        public EmployeeDto ResultEmployee { get; private set; }

        // For Edit mode — this dialog is dual-purpose (Add when employee == null, Edit
        // otherwise), distinguished by which constructor overload the caller uses, same
        // as the original.
        private readonly bool _isEditMode;
        private readonly EmployeeDto _existingEmployee;
        private readonly bool _originalIsArchived;
        private readonly string _connectionString;

        // Suppresses SelectionChanged cascades while ComboBox Items are being rebuilt
        // (during filter-as-you-type / dropdown-close restore) — mirrors the WinForms original.
        private bool _suppressComboBoxEvents;

        // An editable ComboBox's PART_EditableTextBox isn't materialized until the control is
        // actually rendered, so setting SelectedIndex during the constructor (before the Window
        // is shown) can raise TextChanged on a deferred layout pass — AFTER the synchronous
        // _suppressComboBoxEvents guard around that assignment has already been reset. That let
        // the auto-open-dropdown logic below fire once the window appeared, even though the
        // "typed" text was really just the initial selection being loaded. Gate the auto-open
        // on the window being fully Loaded so it only ever reacts to genuine user keystrokes.
        private bool _isDialogLoaded;

        public EmployeeDialog(EmployeeDto employee = null)
        {
            InitializeComponent();

            _isEditMode = employee != null;
            _existingEmployee = employee;
            _connectionString = DatabaseConfig.ConnectionString;

            if (_isEditMode && _existingEmployee != null)
            {
                _originalIsArchived = _existingEmployee.IsArchived;
            }

            ConfigureForMode();
            Loaded += (s, e) => _isDialogLoaded = true;

            // Wire up searchable filtering — Tag (backing list) is set inside each Load method
            ConfigureSearchableComboBox(CmbCompany, CmbDepartment);
            ConfigureSearchableComboBox(CmbDepartment, CmbBranch);
            ConfigureSearchableComboBox(CmbBranch, CmbDistributor);
            ConfigureSearchableComboBox(CmbDistributor, CmbPosition);
            ConfigureSearchableComboBox(CmbPosition, null);

            // Uppercase position on commit (Leave/Enter) — matches the original's ToUpper()
            // normalization. The original also uppercased on every keystroke via WinForms
            // KeyPress; WPF's TextCompositionEventArgs.Text is read-only so per-keystroke
            // transformation isn't available the same way. Simplified to commit-time only.
            CmbPosition.LostKeyboardFocus += (s, e) =>
            {
                if (!string.IsNullOrEmpty(CmbPosition.Text))
                    CmbPosition.Text = CmbPosition.Text.ToUpper();
            };

            LoadCompanies();
            LoadPositions();
            LoadTitles();
            LoadDepartments();
            LoadBranches();
            LoadDistributors();

            if (_isEditMode)
            {
                LoadEmployeeData();
            }
            else
            {
                TxtCreatedBy.Text = AppSession.CurrentUserName ?? string.Empty;
                DpCreated.SelectedDate = DateTime.Now;
            }
        }

        private void ConfigureForMode()
        {
            Title = _isEditMode ? "Edit Employee" : "Add Employee";
            HeaderTitle.Text = _isEditMode ? "Edit Employee" : "Add Employee";
            InfoBanner.Text = _isEditMode
                ? "Update employee details and associations, then click Save."
                : "Fill in employee details and required associations, then click Save.";

            // IsArchived only applies to an existing employee.
            SectionStatus.Visibility = _isEditMode ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IDisposable — no-op; required for WinForms `using` compatibility
        // ─────────────────────────────────────────────────────────────────────────
        public void Dispose() { }

        // ─────────────────────────────────────────────────────────────────────────
        // ShowDialog overloads — shadow WPF's bool? version and return WinForms type
        // ─────────────────────────────────────────────────────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            // This dialog is opened from EmployeePageView, a WPF UserControl hosted inside a
            // WinForms ElementHost (and from MainForm / EmployeeManagementPage directly) —
            // there is no WPF Window ancestor to inherit an Owner from. Without a real Win32
            // owner, the window doesn't participate correctly in the taskbar's Z-order:
            // switching to another application leaves it stuck on top instead of going behind.
            // Resolve the active WinForms window as owner, same fallback
            // EditItemDialog/EditBranchDialog/CompanyDialog use for this exact interop gap.
            var activeForm = WinForms.Form.ActiveForm;
            if (activeForm != null)
                new WindowInteropHelper(this).Owner = activeForm.Handle;

            ClampToWorkArea();

            bool? result = base.ShowDialog();
            return (result == true) ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        // WindowStartupLocation="CenterOwner" centers using the full screen bounds, not the
        // taskbar-trimmed work area, so a window this tall can land with its footer
        // (Save/Cancel) rendered behind the taskbar. Shrink to fit and center within the
        // actual work area before the window is shown.
        private void ClampToWorkArea()
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 24;

            if (Width > workArea.Width - margin) Width = Math.Max(MinWidth, workArea.Width - margin);
            if (Height > workArea.Height - margin) Height = Math.Max(MinHeight, workArea.Height - margin);

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Header drag / minimize / maximize / close
        // ─────────────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
                DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            BtnMaximizeGlyph.Text = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        // WindowStyle="None" + ResizeMode="CanResizeWithGrip" windows keep the invisible
        // resize-frame Windows normally reserves for sizable windows, which insets a
        // maximized window from the real work area unless we report it ourselves.
        // See BatchAddItemDialog.xaml.cs for the original diagnosis of this issue.
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                hwndSource.AddHook(WmGetMinMaxInfoHook);
        }

        private IntPtr WmGetMinMaxInfoHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = (NativeMethods.MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MINMAXINFO));

                var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new NativeMethods.MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                    NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

                    var workArea    = monitorInfo.rcWork;
                    var monitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
                    mmi.ptMaxSize.X     = workArea.Right - workArea.Left;
                    mmi.ptMaxSize.Y     = workArea.Bottom - workArea.Top;
                    mmi.ptMaxTrackSize  = mmi.ptMaxSize;
                }

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static class NativeMethods
        {
            public const int MONITOR_DEFAULTTONEAREST = 0x00000002;

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT { public int X; public int Y; }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT { public int Left, Top, Right, Bottom; }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINMAXINFO
            {
                public POINT ptReserved;
                public POINT ptMaxSize;
                public POINT ptMaxPosition;
                public POINT ptMinTrackSize;
                public POINT ptMaxTrackSize;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

            [DllImport("user32.dll")]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        }

        // A closed ComboBox still consumes the mouse wheel to change its selected value,
        // which caused accidental field changes while users scrolled through the dialog.
        // Swallow the wheel event here and re-raise it on the parent so the dialog's
        // ScrollViewer scrolls instead. Only suppressed while the dropdown is closed —
        // scrolling an open dropdown list still works normally.
        private void FormCombo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var combo = (ComboBox)sender;
            if (combo.IsDropDownOpen)
                return;

            e.Handled = true;

            if (combo.Parent is UIElement parent)
            {
                var bubbled = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = combo
                };
                parent.RaiseEvent(bubbled);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Searchable ComboBox filtering — WPF port of the WinForms ConfigureSearchableComboBox
        // pattern (BatchAddItemDialog / original EmployeeDialog.cs). The full backing list is
        // stored in cmb.Tag as List<object> after each Load method populates the ComboBox.
        // ─────────────────────────────────────────────────────────────────────────
        private void ConfigureSearchableComboBox(ComboBox cmb, Control nextFocusControl)
        {
            cmb.PreviewKeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    string typed = cmb.Text?.Trim();
                    if (cmb.IsDropDownOpen) cmb.IsDropDownOpen = false;

                    if (!string.IsNullOrEmpty(typed))
                    {
                        var allItems = (cmb.Tag as List<object>) ?? cmb.Items.Cast<object>().ToList();
                        int match = -1;

                        // Exact match first
                        for (int i = 0; i < allItems.Count; i++)
                            if (string.Equals(allItems[i].ToString(), typed, StringComparison.OrdinalIgnoreCase))
                            { match = i; break; }

                        // StartsWith fallback
                        if (match < 0)
                            for (int i = 0; i < allItems.Count; i++)
                                if (allItems[i].ToString().StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                                { match = i; break; }

                        if (match >= 0)
                        {
                            RestoreComboItems(cmb);
                            if (cmb.SelectedIndex != match) cmb.SelectedIndex = match;
                        }
                    }

                    nextFocusControl?.Focus();
                    ev.Handled = true;
                }
                else if (ev.Key == Key.Escape && cmb.IsDropDownOpen)
                {
                    // Restore full item list BEFORE closing so WPF never encounters an
                    // empty Items collection during dropdown close.
                    string savedText = cmb.Text;
                    RestoreComboItems(cmb);
                    cmb.IsDropDownOpen = false;
                    SetComboText(cmb, savedText);
                    ev.Handled = true;
                }
            };

            // TextChanged fires on user keystrokes for an editable ComboBox (also on
            // programmatic Text assignment, which is why every programmatic write in this
            // class is wrapped with _suppressComboBoxEvents via SetComboText/RestoreComboItems).
            // Filter items from the backing list, restore typed text, then open the dropdown.
            cmb.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, ev) =>
            {
                if (_suppressComboBoxEvents) return;

                var allItems = cmb.Tag as List<object>;
                if (allItems == null) return;

                string savedText = cmb.Text;

                var filtered = string.IsNullOrEmpty(savedText)
                    ? allItems
                    : allItems
                        .Where(item => item.ToString().IndexOf(savedText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                _suppressComboBoxEvents = true;
                try
                {
                    cmb.Items.Clear();
                    foreach (var item in filtered)
                        cmb.Items.Add(item);
                }
                finally
                {
                    _suppressComboBoxEvents = false;
                }

                SetComboText(cmb, savedText);

                if (_isDialogLoaded && !cmb.IsDropDownOpen && filtered.Count > 0 && !string.IsNullOrEmpty(savedText))
                    cmb.IsDropDownOpen = true;

                SetComboText(cmb, savedText);
            }));

            // DropDownClosed: restore full item list and re-commit selection by reference or text
            cmb.DropDownClosed += (s, ev) =>
            {
                var allItems = cmb.Tag as List<object>;
                if (allItems == null || cmb.Items.Count == allItems.Count) return;

                var selectedItem = cmb.Items.Count > 0 ? cmb.SelectedItem : null;
                string currentText = cmb.Text?.Trim();

                RestoreComboItems(cmb);

                // Re-commit by object reference (click selection)
                if (selectedItem != null)
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (ReferenceEquals(cmb.Items[i], selectedItem))
                        {
                            if (cmb.SelectedIndex != i) cmb.SelectedIndex = i;
                            return;
                        }
                    }
                }

                // Re-commit by exact text match (keyboard entry)
                if (!string.IsNullOrWhiteSpace(currentText))
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (string.Equals(cmb.Items[i].ToString(), currentText, StringComparison.OrdinalIgnoreCase))
                        {
                            cmb.SelectedIndex = i;
                            return;
                        }
                    }
                }
            };
        }

        // Restores a ComboBox's Items to the full backing list (cmb.Tag) while suppressing events.
        private void RestoreComboItems(ComboBox cmb)
        {
            var allItems = cmb.Tag as List<object>;
            if (allItems == null || cmb.Items.Count == allItems.Count) return;

            _suppressComboBoxEvents = true;
            try
            {
                cmb.Items.Clear();
                foreach (var item in allItems)
                    cmb.Items.Add(item);
            }
            finally
            {
                _suppressComboBoxEvents = false;
            }
        }

        // Sets the editable ComboBox's Text without re-triggering the filter handler, and
        // parks the caret at the end (mirrors the WinForms SelectionStart/SelectionLength reset).
        private void SetComboText(ComboBox cmb, string text)
        {
            _suppressComboBoxEvents = true;
            try
            {
                cmb.Text = text;
                if (cmb.Template?.FindName("PART_EditableTextBox", cmb) is TextBox editableTextBox)
                {
                    editableTextBox.SelectionStart = text?.Length ?? 0;
                    editableTextBox.SelectionLength = 0;
                }
            }
            finally
            {
                _suppressComboBoxEvents = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Data loading
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadCompanies()
        {
            CmbCompany.Items.Clear();

            var defaultItem = new CompanyItem { Id = null, Name = "-- Select Company --", Active = true };
            CmbCompany.Items.Add(defaultItem);

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString)) return;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Load only active companies for new employees.
                    // In edit mode, also load the specific inactive company if needed.
                    string query = @"
                        SELECT ComId, Name, ISNULL(Active, 1) AS Active
                        FROM dbo.Company
                        WHERE ISNULL(Active, 1) = 1";

                    if (_isEditMode && _existingEmployee != null)
                    {
                        query += " OR ComId = @ExistingComId";
                    }

                    query += " ORDER BY Name";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        if (_isEditMode && _existingEmployee != null)
                        {
                            cmd.Parameters.AddWithValue("@ExistingComId", _existingEmployee.CompanyId);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new CompanyItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Active = reader.GetBoolean(2)
                                };
                                CmbCompany.Items.Add(item);
                            }
                        }
                    }
                }

                if (CmbCompany.Items.Count > 0)
                    CmbCompany.SelectedIndex = 0;

                CmbCompany.Tag = CmbCompany.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load companies: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadTitles()
        {
            CmbTitle.Items.Clear();
            CmbTitle.Items.Add(new TitleItem { Id = null, Display = "-- Select Title --" });

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString)) return;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT TitleId, Code, Description FROM dbo.Title ORDER BY TitleId", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CmbTitle.Items.Add(new TitleItem
                            {
                                Id = reader.GetInt32(0),
                                Display = $"{reader.GetString(1)} - {reader.GetString(2)}"
                            });
                        }
                    }
                }

                CmbTitle.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load titles: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadPositions()
        {
            CmbPosition.Items.Clear();

            var defaultItem = new PositionItem { Id = null, Name = "-- Select Position --" };
            CmbPosition.Items.Add(defaultItem);

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString)) return;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT DISTINCT Position FROM dbo.Employee WHERE Position IS NOT NULL AND Position != '' ORDER BY Position", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new PositionItem
                            {
                                Id = null,
                                Name = reader.GetString(0)
                            };
                            CmbPosition.Items.Add(item);
                        }
                    }
                }

                if (CmbPosition.Items.Count > 0)
                    CmbPosition.SelectedIndex = 0;

                CmbPosition.Tag = CmbPosition.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load positions: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadDepartments()
        {
            CmbDepartment.Items.Clear();

            var defaultItem = new DepartmentItem { Id = null, Name = "-- Select Department --", Active = true };
            CmbDepartment.Items.Add(defaultItem);

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString)) return;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT DeptId, Name, ISNULL(Active, 1) AS Active
                        FROM dbo.Department
                        WHERE ISNULL(Active, 1) = 1";

                    bool includeExistingDept = _isEditMode && _existingEmployee != null && _existingEmployee.DepartmentId.HasValue;
                    if (includeExistingDept)
                    {
                        query += " OR DeptId = @ExistingDeptId";
                    }

                    query += " ORDER BY Name";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        if (includeExistingDept)
                        {
                            cmd.Parameters.AddWithValue("@ExistingDeptId", _existingEmployee.DepartmentId.Value);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new DepartmentItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Active = reader.GetBoolean(2)
                                };
                                CmbDepartment.Items.Add(item);
                            }
                        }
                    }
                }

                if (CmbDepartment.Items.Count > 0)
                    CmbDepartment.SelectedIndex = 0;

                CmbDepartment.Tag = CmbDepartment.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load departments: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadBranches()
        {
            CmbBranch.Items.Clear();

            var defaultItem = new BranchItem { Id = null, Name = "-- Select Branch --", Active = true };
            CmbBranch.Items.Add(defaultItem);

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString)) return;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT BranchId, Name, ISNULL(Active, 1) AS Active
                        FROM dbo.Branch
                        WHERE ISNULL(Active, 1) = 1";

                    if (_isEditMode && _existingEmployee != null)
                    {
                        query += " OR BranchId = @ExistingBranchId";
                    }

                    query += " ORDER BY Name";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        if (_isEditMode && _existingEmployee != null)
                        {
                            cmd.Parameters.AddWithValue("@ExistingBranchId", _existingEmployee.BranchId);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new BranchItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Active = reader.GetBoolean(2)
                                };
                                CmbBranch.Items.Add(item);
                            }
                        }
                    }
                }

                if (CmbBranch.Items.Count > 0)
                    CmbBranch.SelectedIndex = 0;

                CmbBranch.Tag = CmbBranch.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load branches: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadDistributors()
        {
            CmbDistributor.Items.Clear();

            var defaultItem = new DistributorItem { Id = null, Name = "(None)", Active = true };
            CmbDistributor.Items.Add(defaultItem);

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString)) return;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Independent sales distributors (dbo.Distributor) for field-designee
                    // postings. Missing table on older DBs leaves "(None)" only.
                    // Optional: only a selected few employees carry one.
                    string query = @"
                        IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL
                        SELECT DistributorId, Name, ISNULL(IsActive, 1) AS Active
                        FROM dbo.Distributor
                        WHERE ISNULL(IsActive, 1) = 1";

                    bool includeExistingDistributor = _isEditMode && _existingEmployee != null && _existingEmployee.DistributorId.HasValue;
                    if (includeExistingDistributor)
                    {
                        query += " OR DistributorId = @ExistingDistributorId";
                    }

                    query += " ORDER BY SortOrder, Name";

                    using (var cmd = new SqlCommand(query, con))
                    {
                        if (includeExistingDistributor)
                        {
                            cmd.Parameters.AddWithValue("@ExistingDistributorId", _existingEmployee.DistributorId.Value);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new DistributorItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Active = reader.GetBoolean(2)
                                };
                                CmbDistributor.Items.Add(item);
                            }
                        }
                    }
                }

                if (CmbDistributor.Items.Count > 0)
                    CmbDistributor.SelectedIndex = 0;

                CmbDistributor.Tag = CmbDistributor.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load distributors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Warning label visibility
        // ─────────────────────────────────────────────────────────────────────────
        private void CmbCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;
            var item = CmbCompany.SelectedItem as CompanyItem;
            LblCompanyWarning.Visibility = (item != null && !item.Active) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbDepartment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;
            var item = CmbDepartment.SelectedItem as DepartmentItem;
            LblDepartmentWarning.Visibility = (item != null && !item.Active) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;
            var item = CmbBranch.SelectedItem as BranchItem;
            LblBranchWarning.Visibility = (item != null && !item.Active) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbDistributor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;
            var item = CmbDistributor.SelectedItem as DistributorItem;
            LblDistributorWarning.Visibility = (item != null && item.Id.HasValue && !item.Active) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Data loading (edit mode)
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadEmployeeData()
        {
            if (_existingEmployee == null) return;

            TxtName.Text = _existingEmployee.Name ?? string.Empty;
            TxtEmployeeNumber.Text = _existingEmployee.EmployeeNumber ?? string.Empty;

            // Setting Text directly is safe here — the filter handler only reacts to real
            // keystrokes; a straight assignment during initial load doesn't need suppression
            // because there's no stale filtered list yet, but we route through SetComboText
            // for parity/safety anyway.
            SetComboText(CmbPosition, _existingEmployee.Position ?? string.Empty);

            TxtDescription.Text = _existingEmployee.Description ?? string.Empty;
            DpCreated.SelectedDate = _existingEmployee.DateCreated;
            TxtCreatedBy.Text = _existingEmployee.CreatedByName ?? string.Empty;

            // Select title
            if (_existingEmployee.TitleId.HasValue)
            {
                for (int i = 0; i < CmbTitle.Items.Count; i++)
                {
                    if (CmbTitle.Items[i] is TitleItem ti && ti.Id == _existingEmployee.TitleId.Value)
                    {
                        CmbTitle.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Selecting an item on these editable ComboBoxes also updates their Text, which
            // would otherwise re-trigger the TextChanged filter handler and pop the dropdown
            // open during initial load (WPF fires TextChanged on programmatic Text changes,
            // not just keystrokes) — suppress while assigning SelectedIndex here, same as
            // every other programmatic write in this class does via SetComboText/RestoreComboItems.
            _suppressComboBoxEvents = true;
            try
            {
                // Select company
                for (int i = 0; i < CmbCompany.Items.Count; i++)
                {
                    if (CmbCompany.Items[i] is CompanyItem item && item.Id == _existingEmployee.CompanyId)
                    {
                        CmbCompany.SelectedIndex = i;
                        break;
                    }
                }

                // Select department
                for (int i = 0; i < CmbDepartment.Items.Count; i++)
                {
                    if (CmbDepartment.Items[i] is DepartmentItem item && item.Id == _existingEmployee.DepartmentId)
                    {
                        CmbDepartment.SelectedIndex = i;
                        break;
                    }
                }

                // Select branch
                for (int i = 0; i < CmbBranch.Items.Count; i++)
                {
                    if (CmbBranch.Items[i] is BranchItem item && item.Id == _existingEmployee.BranchId)
                    {
                        CmbBranch.SelectedIndex = i;
                        break;
                    }
                }

                // Select distributor (optional, selected few only; stays on "(None)" when null)
                for (int i = 0; i < CmbDistributor.Items.Count; i++)
                {
                    if (CmbDistributor.Items[i] is DistributorItem distItem && distItem.Id == _existingEmployee.DistributorId)
                    {
                        CmbDistributor.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                _suppressComboBoxEvents = false;
            }

            // The warning labels rely on SelectionChanged, which we just suppressed above —
            // evaluate them explicitly now that the selections have settled.
            CmbCompany_SelectionChanged(CmbCompany, null);
            CmbDepartment_SelectionChanged(CmbDepartment, null);
            CmbBranch_SelectionChanged(CmbBranch, null);
            CmbDistributor_SelectionChanged(CmbDistributor, null);

            // Load IsArchived
            ChkIsArchived.IsChecked = _existingEmployee.IsArchived;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Footer buttons
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                WinMsgBox.Show("Please enter a Name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            // Validate Company selection
            if (!(CmbCompany.SelectedItem is CompanyItem compItem) || !compItem.Id.HasValue)
            {
                WinMsgBox.Show("Please select a Company.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbCompany.Focus();
                return;
            }

            // Department is optional (dbo.Employee.DeptId is nullable)
            var deptItem = CmbDepartment.SelectedItem as DepartmentItem;

            // Distributor posting is optional (only a selected few employees carry one)
            var distItem = CmbDistributor.SelectedItem as DistributorItem;

            // Validate Branch selection
            if (!(CmbBranch.SelectedItem is BranchItem branchItem) || !branchItem.Id.HasValue)
            {
                WinMsgBox.Show("Please select a Branch.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbBranch.Focus();
                return;
            }

            // Handle archive status changes (edit mode only)
            if (_isEditMode)
            {
                bool archiveStatusChanged = (ChkIsArchived.IsChecked == true) != _originalIsArchived;

                if (archiveStatusChanged)
                {
                    try
                    {
                        using (var con = new SqlConnection(_connectionString))
                        {
                            con.Open();
                            using (var transaction = con.BeginTransaction())
                            {
                                try
                                {
                                    if (ChkIsArchived.IsChecked == true)
                                    {
                                        // Archive the employee - insert into ArchiveStatus table
                                        string insertArchiveSql = @"
                                            INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                            VALUES ('Employee', @EmpId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                        using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@EmpId", _existingEmployee.EmpId);
                                            cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                            cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Employee Dialog");
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        // Unarchive the employee - delete from ArchiveStatus table
                                        string deleteArchiveSql = @"
                                            DELETE FROM ArchiveStatus
                                            WHERE EntityType = 'Employee' AND EntityId = @EmpId";

                                        using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@EmpId", _existingEmployee.EmpId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }

                                    transaction.Commit();
                                }
                                catch
                                {
                                    transaction.Rollback();
                                    throw;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WinMsgBox.Show($"Failed to update archive status: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            // Warning if selecting inactive items for NEW employee
            if (!_isEditMode)
            {
                bool deptInactive = deptItem != null && deptItem.Id.HasValue && !deptItem.Active;
                bool distInactive = distItem != null && distItem.Id.HasValue && !distItem.Active;
                if (!compItem.Active || deptInactive || !branchItem.Active || distInactive)
                {
                    string warnings = "Warning: You are assigning this employee to inactive item(s):\n";
                    if (!compItem.Active) warnings += "• Company (Inactive)\n";
                    if (deptInactive)     warnings += "• Department (Inactive)\n";
                    if (!branchItem.Active) warnings += "• Branch (Inactive)\n";
                    if (distInactive)     warnings += "• Distributor (Inactive)\n";
                    warnings += "\nThis is not recommended. Do you want to continue?";

                    var result = WinMsgBox.Show(warnings, "Inactive Selection Warning",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;
                }
            }

            ResultEmployee = new EmployeeDto
            {
                EmpId = _isEditMode ? _existingEmployee.EmpId : 0,
                Name = TxtName.Text.Trim(),
                EmployeeNumber = string.IsNullOrWhiteSpace(TxtEmployeeNumber.Text) ? null : TxtEmployeeNumber.Text.Trim(),
                Position = CmbPosition.Text.Trim(),
                Description = TxtDescription.Text.Trim(),
                DateCreated = DpCreated.SelectedDate ?? DateTime.Now,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName,
                CompanyId = compItem.Id.Value,
                DepartmentId = deptItem?.Id,
                BranchId = branchItem.Id.Value,
                DistributorId = EmployeeDistributorHelper.NormalizeDistributorId(distItem?.Id),
                TitleId = (CmbTitle.SelectedItem is TitleItem ti && ti.Id.HasValue) ? ti.Id : null,
                IsArchived = _isEditMode && ChkIsArchived.IsChecked == true
            };

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Inline "+ Add" quick-create popup for Position — kept as a small ad-hoc WinForms
        // Form, same as EditItemDialog's CreateQuickAddCartridgeModelDialog/
        // CreateQuickAddConsumableModelDialog. Not worth converting to WPF for a
        // three-control popup.
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnAddPosition_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new WinForms.Form())
            {
                dlg.Text = "Add Position";
                dlg.Width = 380;
                dlg.Height = 168;
                dlg.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                dlg.StartPosition = WinForms.FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.Font = new System.Drawing.Font("Segoe UI", 9.5F);

                var lbl = new WinForms.Label { Text = "New position name:", Left = 16, Top = 18, AutoSize = true };
                var txt = new WinForms.TextBox { Left = 16, Top = 42, Width = 330, Height = 28 };
                var btnOk = new WinForms.Button
                {
                    Text = "Add",
                    DialogResult = WinForms.DialogResult.OK,
                    Left = 176, Top = 88, Width = 80, Height = 32,
                    BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
                    ForeColor = System.Drawing.Color.White,
                    FlatStyle = WinForms.FlatStyle.Flat
                };
                var btnCnl = new WinForms.Button
                {
                    Text = "Cancel",
                    DialogResult = WinForms.DialogResult.Cancel,
                    Left = 266, Top = 88, Width = 80, Height = 32,
                    FlatStyle = WinForms.FlatStyle.Flat
                };

                txt.KeyPress += (s2, e2) => { if (char.IsLetter(e2.KeyChar)) e2.KeyChar = char.ToUpper(e2.KeyChar); };
                dlg.Controls.AddRange(new WinForms.Control[] { lbl, txt, btnOk, btnCnl });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCnl;

                var activeForm = WinForms.Form.ActiveForm;
                var dialogResult = activeForm != null ? dlg.ShowDialog(activeForm) : dlg.ShowDialog();
                if (dialogResult != WinForms.DialogResult.OK) return;

                string newPos = txt.Text.Trim().ToUpper();
                if (string.IsNullOrEmpty(newPos)) return;

                var allItems = CmbPosition.Tag as List<object>;
                if (allItems == null) return;

                // If it already exists, just select it
                for (int i = 0; i < allItems.Count; i++)
                {
                    if (string.Equals(allItems[i].ToString(), newPos, StringComparison.OrdinalIgnoreCase))
                    {
                        RestoreComboItems(CmbPosition);
                        for (int j = 0; j < CmbPosition.Items.Count; j++)
                            if (string.Equals(CmbPosition.Items[j].ToString(), newPos, StringComparison.OrdinalIgnoreCase))
                            { CmbPosition.SelectedIndex = j; return; }
                        return;
                    }
                }

                // Add new position to the list and select it
                RestoreComboItems(CmbPosition);
                var newItem = new PositionItem { Id = null, Name = newPos };
                CmbPosition.Items.Add(newItem);
                allItems.Add(newItem);
                CmbPosition.SelectedIndex = CmbPosition.Items.Count - 1;
            }
        }

        // Helper classes for ComboBox items with Active property
        private class CompanyItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; }
            public override string ToString() => Name;
        }

        private class DepartmentItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; }
            public override string ToString() => Name;
        }

        private class BranchItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; }
            public override string ToString() => Name;
        }

        private class DistributorItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; }
            public override string ToString() => Name;
        }

        private class PositionItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class TitleItem
        {
            public int? Id { get; set; }
            public string Display { get; set; }
            public override string ToString() => Display;
        }
    }
}
