using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

// Disambiguate MessageBox / DialogResult — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Branch
{
    public partial class EditBranchDialog : Window, IDisposable
    {
        private readonly BranchDto _branch;
        private readonly BranchRepository _repository;
        private readonly string _connectionString;
        private readonly bool _originalIsArchived;

        private readonly List<CompanyLookup> _companies = new List<CompanyLookup>();
        private readonly List<DepartmentLookup> _departments = new List<DepartmentLookup>();

        public EditBranchDialog(BranchDto branch)
        {
            _branch = branch ?? throw new ArgumentNullException(nameof(branch));
            _repository = new BranchRepository();
            _connectionString = DatabaseConfig.ConnectionString;
            _originalIsArchived = branch.IsArchived;

            InitializeComponent();

            LoadDropdowns();
            LoadBranchData();
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
            // This dialog is opened from BranchPageView, a WPF UserControl hosted inside a
            // WinForms ElementHost — there is no WPF Window ancestor to inherit an Owner from.
            // Without a real Win32 owner, the window doesn't participate correctly in the
            // taskbar's Z-order: switching to another application leaves it stuck on top
            // instead of going behind. Resolve the active WinForms window as owner, same
            // fallback EditItemDialog/EditCategoryDialog use for this exact interop gap.
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
        // Data loading
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadDropdowns()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Load Companies
                    using (var cmd = new SqlCommand("SELECT ComId, Name, Active FROM dbo.Company ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _companies.Add(new CompanyLookup { ComId = 0, Name = "-- None --", Active = true });
                        while (reader.Read())
                        {
                            _companies.Add(new CompanyLookup
                            {
                                ComId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Active = reader.GetBoolean(2)
                            });
                        }
                    }

                    // Load Departments
                    using (var cmd = new SqlCommand("SELECT DeptId, Name, Active FROM dbo.Department ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        _departments.Add(new DepartmentLookup { DeptId = 0, Name = "-- None --", Active = true });
                        while (reader.Read())
                        {
                            _departments.Add(new DepartmentLookup
                            {
                                DeptId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Active = reader.GetBoolean(2)
                            });
                        }
                    }
                }

                // Bind to comboboxes
                CmbCompany.ItemsSource = _companies;
                CmbDepartment.ItemsSource = _departments;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load dropdown data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadBranchData()
        {
            TxtBranchId.Text = _branch.BranchId.ToString();
            TxtName.Text = _branch.Name;
            TxtDescription.Text = _branch.Description;

            // Set company
            if (_branch.CompanyId.HasValue)
            {
                var company = _companies.FirstOrDefault(c => c.ComId == _branch.CompanyId.Value);
                if (company != null)
                {
                    CmbCompany.SelectedItem = company;
                    LblCompanyWarning.Visibility = company.Active ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            else
            {
                CmbCompany.SelectedIndex = 0; // Select "-- None --"
                LblCompanyWarning.Visibility = Visibility.Collapsed;
            }

            // Set department
            if (_branch.DepartmentId.HasValue)
            {
                var department = _departments.FirstOrDefault(d => d.DeptId == _branch.DepartmentId.Value);
                if (department != null)
                {
                    CmbDepartment.SelectedItem = department;
                    LblDepartmentWarning.Visibility = department.Active ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            else
            {
                CmbDepartment.SelectedIndex = 0; // Select "-- None --"
                LblDepartmentWarning.Visibility = Visibility.Collapsed;
            }

            // Set checkboxes
            ChkIsFactory.IsChecked = _branch.IsFactory;
            ChkIsDepot.IsChecked = _branch.IsDepot;
            ChkIsCenter.IsChecked = _branch.IsCenter;
            ChkIsDistributor.IsChecked = _branch.IsDistributor;
            ChkActive.IsChecked = _branch.Active;
            ChkIsArchived.IsChecked = _branch.IsArchived;
        }

        private void CmbCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbCompany.SelectedItem is CompanyLookup company)
            {
                LblCompanyWarning.Visibility = (company.ComId != 0 && !company.Active)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                LblCompanyWarning.Visibility = Visibility.Collapsed;
            }
        }

        private void CmbDepartment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDepartment.SelectedItem is DepartmentLookup department)
            {
                LblDepartmentWarning.Visibility = (department.DeptId != 0 && !department.Active)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                LblDepartmentWarning.Visibility = Visibility.Collapsed;
            }
        }

        private void ChkIsArchived_Checked(object sender, RoutedEventArgs e)
        {
            // When branch is archived, automatically set it to inactive
            if (ChkIsArchived.IsChecked == true)
            {
                ChkActive.IsChecked = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Footer buttons
        // ─────────────────────────────────────────────────────────────────────────
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                WinMsgBox.Show("Branch name is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            // Check if archive status changed
            bool archiveStatusChanged = (ChkIsArchived.IsChecked == true) != _originalIsArchived;

            // Handle archive status changes
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
                                    // Archive the branch - insert into ArchiveStatus table
                                    string insertArchiveSql = @"
                                        INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        VALUES ('Branch', @BranchId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                    using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@BranchId", _branch.BranchId);
                                        cmd.Parameters.AddWithValue("@ArchivedBy", Session.AppSession.CurrentUserName ?? "System");
                                        cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Branch Dialog");
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    // Unarchive the branch - delete from ArchiveStatus table
                                    string deleteArchiveSql = @"
                                        DELETE FROM ArchiveStatus
                                        WHERE EntityType = 'Branch' AND EntityId = @BranchId";

                                    using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@BranchId", _branch.BranchId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }

                                transaction.Commit();
                                string archiveAction = ChkIsArchived.IsChecked == true ? "archived" : "unarchived";
                                ActivityLogger.Log(ActivityLogger.Actions.Update, "Branch", _branch.BranchId, $"Branch '{_branch.Name}' {archiveAction}");
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

            try
            {
                BtnSave.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                Cursor = System.Windows.Input.Cursors.Wait;

                // Update DTO
                _branch.Name = TxtName.Text.Trim();
                _branch.Description = string.IsNullOrWhiteSpace(TxtDescription.Text) ? null : TxtDescription.Text.Trim();

                var selectedCompany = CmbCompany.SelectedItem as CompanyLookup;
                var selectedCompanyId = selectedCompany?.ComId ?? 0;
                _branch.CompanyId = selectedCompanyId == 0 ? (int?)null : selectedCompanyId;

                var selectedDepartment = CmbDepartment.SelectedItem as DepartmentLookup;
                var selectedDeptId = selectedDepartment?.DeptId ?? 0;
                _branch.DepartmentId = selectedDeptId == 0 ? (int?)null : selectedDeptId;

                _branch.IsFactory = ChkIsFactory.IsChecked == true;
                _branch.IsDepot = ChkIsDepot.IsChecked == true;
                _branch.IsCenter = ChkIsCenter.IsChecked == true;
                _branch.IsDistributor = ChkIsDistributor.IsChecked == true;
                _branch.Active = ChkActive.IsChecked == true;
                _branch.IsArchived = ChkIsArchived.IsChecked == true;

                _branch.ModifiedByUserId = Session.AppSession.CurrentUserId;
                _branch.DateModified = DateTime.Now;

                // Save to database
                bool success = await _repository.UpdateAsync(_branch);

                if (success)
                {
                    ActivityLogger.Log(ActivityLogger.Actions.Update, "Branch", _branch.BranchId, $"Branch '{_branch.Name}' updated");
                    WinMsgBox.Show("Branch updated successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    WinMsgBox.Show("Failed to update branch. No rows were affected.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Error updating branch: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
                BtnCancel.IsEnabled = true;
                Cursor = null;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Helper classes
        private class CompanyLookup
        {
            public int ComId { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; }
        }

        private class DepartmentLookup
        {
            public int DeptId { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; }
        }
    }
}
