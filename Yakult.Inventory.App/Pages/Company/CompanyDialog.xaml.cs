using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;

// Disambiguate MessageBox / DialogResult — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Company
{
    public partial class CompanyDialog : Window, IDisposable
    {
        // Store the result — same public contract as the WinForms version
        public CompanyDto ResultCompany { get; private set; }

        // For Edit mode — this dialog is dual-purpose (Add when company == null, Edit otherwise),
        // distinguished by which constructor overload the caller uses, same as the original.
        private readonly bool _isEditMode;
        private readonly CompanyDto _existingCompany;
        private readonly bool _originalIsArchived;
        private readonly string _connectionString;

        public CompanyDialog(CompanyDto company = null)
        {
            InitializeComponent();

            _isEditMode = company != null;
            _existingCompany = company;
            _connectionString = DatabaseConfig.ConnectionString;

            if (_isEditMode && _existingCompany != null)
            {
                _originalIsArchived = _existingCompany.IsArchived;
            }

            ConfigureForMode();

            if (_isEditMode)
            {
                LoadCompanyData();
            }
            else
            {
                TxtCreatedBy.Text = AppSession.CurrentUserName ?? string.Empty;
                DpCreated.SelectedDate = DateTime.Now;
            }
        }

        private void ConfigureForMode()
        {
            Title = _isEditMode ? "Edit Company" : "Add Company";
            HeaderTitle.Text = _isEditMode ? "Edit Company" : "Add Company";
            InfoBanner.Text = _isEditMode
                ? "Update company details, then click Save."
                : "Enter company details. You may optionally add departments and branches, then click Save.";

            // Active/Is Archived only apply to an existing company.
            SectionStatus.Visibility = _isEditMode ? Visibility.Visible : Visibility.Collapsed;

            // "Add Departments"/"Add Branches" wizard sub-panels only make sense in create mode —
            // matches the original BuildUI() hiding departmentsSection/branchesSection in edit mode.
            SectionDepartments.Visibility = _isEditMode ? Visibility.Collapsed : Visibility.Visible;
            SectionBranches.Visibility = _isEditMode ? Visibility.Collapsed : Visibility.Visible;
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
            // This dialog is opened from CompanyPageView, a WPF UserControl hosted inside a
            // WinForms ElementHost (and from MainForm directly) — there is no WPF Window
            // ancestor to inherit an Owner from. Without a real Win32 owner, the window doesn't
            // participate correctly in the taskbar's Z-order: switching to another application
            // leaves it stuck on top instead of going behind. Resolve the active WinForms window
            // as owner, same fallback EditItemDialog/EditCategoryDialog/EditBranchDialog use for
            // this exact interop gap.
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
        // scrolling an open dropdown list still works normally. (No ComboBox is currently used
        // in this dialog, but kept for parity with the shared template and in case one is added.)
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
        // Status section (edit mode only)
        // ─────────────────────────────────────────────────────────────────────────
        private void ChkIsArchived_Checked(object sender, RoutedEventArgs e)
        {
            // When company is archived, automatically set it to inactive
            if (ChkIsArchived.IsChecked == true)
            {
                ChkActive.IsChecked = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Departments wizard sub-panel (create mode only)
        // ─────────────────────────────────────────────────────────────────────────
        private void ChkAddDepartments_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool isChecked = ChkAddDepartments.IsChecked == true;
            PnlDepartmentRows.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            BtnAddDept.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            if (isChecked && PnlDepartmentRows.Children.Count == 0)
            {
                AddDepartmentRow();
            }
        }

        private void BtnAddDept_Click(object sender, RoutedEventArgs e)
        {
            AddDepartmentRow();
        }

        private void AddDepartmentRow(string name = null, string description = null)
        {
            var row = new NameDescriptionRow(name, description);
            row.RemoveRequested += (s, e) => PnlDepartmentRows.Children.Remove(row);
            PnlDepartmentRows.Children.Add(row);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Branches wizard sub-panel (create mode only)
        // ─────────────────────────────────────────────────────────────────────────
        private void ChkAddBranches_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool isChecked = ChkAddBranches.IsChecked == true;
            PnlBranchRows.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            BtnAddBranch.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            if (isChecked && PnlBranchRows.Children.Count == 0)
            {
                AddBranchRow();
            }
        }

        private void BtnAddBranch_Click(object sender, RoutedEventArgs e)
        {
            AddBranchRow();
        }

        private void AddBranchRow(string name = null, string description = null)
        {
            var row = new NameDescriptionRow(name, description);
            row.RemoveRequested += (s, e) => PnlBranchRows.Children.Remove(row);
            PnlBranchRows.Children.Add(row);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Data loading (edit mode)
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadCompanyData()
        {
            if (_existingCompany == null) return;

            TxtCompanyName.Text = _existingCompany.Name ?? string.Empty;
            TxtCompanyDesc.Text = _existingCompany.Description ?? string.Empty;
            DpCreated.SelectedDate = _existingCompany.DateCreated;
            TxtCreatedBy.Text = _existingCompany.CreatedByName ?? string.Empty;

            ChkActive.IsChecked = _existingCompany.Active;
            ChkIsArchived.IsChecked = _existingCompany.IsArchived;

            // Load departments if any — sections are hidden in edit mode, but preserved for
            // parity with the original (which always populated these regardless of visibility).
            if (_existingCompany.Departments?.Any() == true)
            {
                ChkAddDepartments.IsChecked = true;
                PnlDepartmentRows.Children.Clear();
                foreach (var dept in _existingCompany.Departments)
                {
                    AddDepartmentRow(dept.Name, dept.Description);
                }
            }

            // Load branches if any
            if (_existingCompany.Branches?.Any() == true)
            {
                ChkAddBranches.IsChecked = true;
                PnlBranchRows.Children.Clear();
                foreach (var branch in _existingCompany.Branches)
                {
                    AddBranchRow(branch.Name, branch.Description);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Footer buttons
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(TxtCompanyName.Text))
            {
                WinMsgBox.Show("Please enter a Company Name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCompanyName.Focus();
                return;
            }

            // Handle archive status changes (edit mode only)
            if (_isEditMode)
            {
                bool archiveStatusChanged = ChkIsArchived.IsChecked == true != _originalIsArchived;

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
                                        // Archive the company - insert into ArchiveStatus table
                                        string insertArchiveSql = @"
                                            INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                            VALUES ('Company', @ComId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                        using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@ComId", _existingCompany.ComId);
                                            cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                            cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Company Dialog");
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        // Unarchive the company - delete from ArchiveStatus table
                                        string deleteArchiveSql = @"
                                            DELETE FROM ArchiveStatus
                                            WHERE EntityType = 'Company' AND EntityId = @ComId";

                                        using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@ComId", _existingCompany.ComId);
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

            var companyDate = DpCreated.SelectedDate ?? DateTime.Now;
            var currentUserId = AppSession.CurrentUserId;
            var currentUserName = AppSession.CurrentUserName;

            ResultCompany = new CompanyDto
            {
                ComId = _isEditMode ? _existingCompany.ComId : 0,
                Name = TxtCompanyName.Text.Trim(),
                Description = TxtCompanyDesc.Text.Trim(),
                DateCreated = companyDate,
                CreatedByUserId = currentUserId,
                CreatedByName = currentUserName,
                Active = _isEditMode && ChkActive.IsChecked.HasValue ? ChkActive.IsChecked == true : true,
                IsArchived = _isEditMode && ChkIsArchived.IsChecked.HasValue && ChkIsArchived.IsChecked == true,
                Departments = new List<DepartmentDto>(),
                Branches = new List<BranchDto>()
            };

            // Collect departments if checked
            if (ChkAddDepartments.IsChecked == true)
            {
                ResultCompany.Departments = PnlDepartmentRows.Children.OfType<NameDescriptionRow>()
                    .Select(d => new DepartmentDto
                    {
                        Name = d.EntryName,
                        Description = d.EntryDescription,
                        DateCreated = companyDate,
                        CreatedByUserId = currentUserId,
                        CreatedByName = currentUserName
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .ToList();
            }

            // Collect branches if checked
            if (ChkAddBranches.IsChecked == true)
            {
                ResultCompany.Branches = PnlBranchRows.Children.OfType<NameDescriptionRow>()
                    .Select(b => new BranchDto
                    {
                        Name = b.EntryName,
                        Description = b.EntryDescription,
                        DateCreated = companyDate,
                        CreatedByUserId = currentUserId,
                        CreatedByName = currentUserName
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .ToList();
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NameDescriptionRow — WPF replacement for the WinForms DepartmentEntry/BranchEntry
    // Panel subclasses that used to live in CompanyDialog.cs. A single generic row shape
    // (Name + Description + remove button) covers both the Departments and Branches wizard
    // sub-panels, exactly as the original two near-identical classes did.
    // ─────────────────────────────────────────────────────────────────────────
    internal sealed class NameDescriptionRow : Border
    {
        private readonly TextBox _txtName;
        private readonly TextBox _txtDesc;

        public event EventHandler RemoveRequested;

        public string EntryName => _txtName.Text;
        public string EntryDescription => _txtDesc.Text;

        public NameDescriptionRow(string name = null, string description = null)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEC));
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(10);
            Margin = new Thickness(0, 0, 0, 8);
            Background = Brushes.White;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            nameStack.Children.Add(new TextBlock { Text = "Name *", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x5A, 0x6A)), Margin = new Thickness(0, 0, 0, 4) });
            _txtName = new TextBox
            {
                Height = 30,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xDC)),
                Text = name ?? string.Empty
            };
            nameStack.Children.Add(_txtName);
            Grid.SetColumn(nameStack, 0);
            grid.Children.Add(nameStack);

            var descStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            descStack.Children.Add(new TextBlock { Text = "Description", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x5A, 0x6A)), Margin = new Thickness(0, 0, 0, 4) });
            _txtDesc = new TextBox
            {
                Height = 30,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xDC)),
                Text = description ?? string.Empty
            };
            descStack.Children.Add(_txtDesc);
            Grid.SetColumn(descStack, 1);
            grid.Children.Add(descStack);

            var btnRemove = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Bottom,
                Foreground = Brushes.Red,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.Bold
            };
            btnRemove.Click += (s, e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(btnRemove, 2);
            grid.Children.Add(btnRemove);

            Child = grid;
        }
    }
}
