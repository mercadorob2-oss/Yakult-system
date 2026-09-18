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
using Yakult.Inventory.App.Session;

// Disambiguate MessageBox / DialogResult — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Department
{
    public partial class DepartmentDialog : Window, IDisposable
    {
        // Store the result — same public contract as the WinForms version
        public List<DepartmentDto> ResultDepartments { get; private set; }

        // For backward compatibility - returns first department
        public DepartmentDto ResultDepartment => ResultDepartments?.FirstOrDefault();

        // Edit mode — this dialog is dual-purpose (Add when department == null, Edit otherwise),
        // distinguished by which constructor overload the caller uses, same as the original.
        private readonly bool _isEditMode;
        private readonly DepartmentDto _existingDepartment;
        private readonly bool _originalIsArchived;
        private readonly string _connectionString;

        public DepartmentDialog(DepartmentDto department = null)
        {
            InitializeComponent();

            _isEditMode = department != null;
            _existingDepartment = department;
            _connectionString = DatabaseConfig.ConnectionString;

            if (_isEditMode && _existingDepartment != null)
            {
                _originalIsArchived = _existingDepartment.IsArchived;
            }

            ConfigureForMode();

            if (_isEditMode)
            {
                LoadDepartmentData();
            }
            else
            {
                TxtCreatedBy.Text = AppSession.CurrentUserName ?? string.Empty;
                TxtDateCreated.Text = DateTime.Now.ToString("MM/dd/yyyy");
            }
        }

        private void ConfigureForMode()
        {
            Title = _isEditMode ? "Edit Department" : "Add Department";
            HeaderTitle.Text = _isEditMode ? "Edit Department" : "Add Department";
            InfoBanner.Text = _isEditMode
                ? "Update department details, then click Save."
                : "Add department details, then click Save.";

            // Active/Is Archived only apply to an existing department.
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
            // This dialog is opened from DepartmentPageView, a WPF UserControl hosted inside a
            // WinForms ElementHost — there is no WPF Window ancestor to inherit an Owner from.
            // Without a real Win32 owner, the window doesn't participate correctly in the
            // taskbar's Z-order: switching to another application leaves it stuck on top
            // instead of going behind. Resolve the active WinForms window as owner, same
            // fallback EditItemDialog/EditCategoryDialog/EditBranchDialog/CompanyDialog use for
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
            // When department is archived, automatically set it to inactive
            if (ChkIsArchived.IsChecked == true)
            {
                ChkActive.IsChecked = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Data loading (edit mode)
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadDepartmentData()
        {
            if (_existingDepartment == null) return;

            TxtDeptName.Text = _existingDepartment.Name ?? string.Empty;
            TxtDeptDesc.Text = _existingDepartment.Description ?? string.Empty;
            TxtDateCreated.Text = _existingDepartment.DateCreated.ToString("MM/dd/yyyy");
            TxtCreatedBy.Text = _existingDepartment.CreatedByName ?? string.Empty;

            ChkActive.IsChecked = _existingDepartment.Active;
            ChkIsArchived.IsChecked = _existingDepartment.IsArchived;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Footer buttons
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(TxtDeptName.Text))
            {
                WinMsgBox.Show("Please enter a Department Name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDeptName.Focus();
                return;
            }

            // Handle archive logic for edit mode
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
                                        // Archive the department - upsert into ArchiveStatus table
                                        string upsertArchiveSql = @"
                                            IF NOT EXISTS (
                                                SELECT 1 FROM ArchiveStatus
                                                WHERE EntityType = 'Department' AND EntityId = @DeptId
                                            )
                                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                                VALUES ('Department', @DeptId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)
                                            ELSE
                                                UPDATE ArchiveStatus
                                                SET IsArchived = 1, ArchivedAt = GETDATE(), ArchivedBy = @ArchivedBy, ArchiveReason = @ArchiveReason
                                                WHERE EntityType = 'Department' AND EntityId = @DeptId";

                                        using (var cmd = new SqlCommand(upsertArchiveSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@DeptId", _existingDepartment.DeptId);
                                            cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                            cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Department Dialog");
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        // Unarchive the department - delete from ArchiveStatus table
                                        string deleteArchiveSql = @"
                                            DELETE FROM ArchiveStatus
                                            WHERE EntityType = 'Department' AND EntityId = @DeptId";

                                        using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@DeptId", _existingDepartment.DeptId);
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

            var department = new DepartmentDto
            {
                DeptId = _isEditMode ? _existingDepartment.DeptId : 0,
                Name = TxtDeptName.Text.Trim(),
                Description = TxtDeptDesc.Text.Trim(),
                DateCreated = _isEditMode ? _existingDepartment.DateCreated : DateTime.Now,
                CreatedByUserId = _isEditMode ? _existingDepartment.CreatedByUserId : AppSession.CurrentUserId,
                CreatedByName = _isEditMode ? _existingDepartment.CreatedByName : AppSession.CurrentUserName,
                Active = _isEditMode && ChkActive.IsChecked.HasValue ? ChkActive.IsChecked == true : true,
                IsArchived = _isEditMode && ChkIsArchived.IsChecked.HasValue && ChkIsArchived.IsChecked == true
            };

            ResultDepartments = new List<DepartmentDto> { department };

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
