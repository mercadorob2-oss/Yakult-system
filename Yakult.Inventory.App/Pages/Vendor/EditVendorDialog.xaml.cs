using System;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

// Disambiguate MessageBox / DialogResult — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Vendor
{
    public partial class EditVendorDialog : Window, IDisposable
    {
        private readonly VendorDto _vendor;
        private readonly VendorRepository _repository;
        private readonly string _connectionString;
        private readonly bool _originalIsArchived;

        public EditVendorDialog(VendorDto vendor)
        {
            _vendor = vendor ?? throw new ArgumentNullException(nameof(vendor));
            _repository = new VendorRepository();
            _originalIsArchived = _vendor.IsArchived;
            _connectionString = DatabaseConfig.ConnectionString;

            InitializeComponent();

            LoadVendorData();
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
            // This dialog is opened from VendorPageView, a WPF UserControl hosted inside a
            // WinForms ElementHost — there is no WPF Window ancestor to inherit an Owner from.
            // Without a real Win32 owner, the window doesn't participate correctly in the
            // taskbar's Z-order: switching to another application leaves it stuck on top
            // instead of going behind. Resolve the active WinForms window as owner, same
            // fallback EditItemDialog/EditBranchDialog use for this exact interop gap.
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
            Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this);
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
        // This dialog has no ComboBox controls today, but the handler is kept in sync
        // with the FormCombo style shared across all converted dialogs.
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
        private void LoadVendorData()
        {
            TxtVendorId.Text = _vendor.VendorId.ToString();
            TxtVendorName.Text = _vendor.VendorName;
            TxtAddress.Text = _vendor.Address ?? string.Empty;
            TxtTIN.Text = _vendor.TIN ?? string.Empty;
            ChkActive.IsChecked     = _vendor.IsActive;
            ChkIsArchived.IsChecked = _vendor.IsArchived;
            ChkIsRefiller.IsChecked = _vendor.IsRefiller;
            ChkIsDisposer.IsChecked = _vendor.IsDisposer;
            ChkIsBuyer.IsChecked    = _vendor.IsBuyer;
            TxtCreatedDate.Text = _vendor.CreatedDate.ToString("MM/dd/yyyy HH:mm");
        }

        private void ChkIsArchived_Checked(object sender, RoutedEventArgs e)
        {
            // When vendor is archived, automatically set it to inactive
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
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(TxtVendorName.Text))
                {
                    WinMsgBox.Show("Vendor Name is required.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtVendorName.Focus();
                    return;
                }

                // Disable buttons to prevent double-click
                BtnSave.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                BtnSave.Content = "Saving...";
                Cursor = Cursors.Wait;

                // Check if archive status changed
                bool archiveStatusChanged = (ChkIsArchived.IsChecked == true) != _originalIsArchived;

                // Handle archive status changes
                if (archiveStatusChanged)
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
                                    // Archive the vendor - insert into ArchiveStatus table
                                    string insertArchiveSql = @"
                                        INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        VALUES ('Vendor', @VendorId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                    using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@VendorId", _vendor.VendorId);
                                        cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                        cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Vendor Dialog");
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    // Unarchive the vendor - delete from ArchiveStatus table
                                    string deleteArchiveSql = @"
                                        DELETE FROM ArchiveStatus
                                        WHERE EntityType = 'Vendor' AND EntityId = @VendorId";

                                    using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@VendorId", _vendor.VendorId);
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

                var vendor = new VendorDto
                {
                    VendorId = _vendor.VendorId,
                    VendorName = TxtVendorName.Text.Trim(),
                    Address = string.IsNullOrWhiteSpace(TxtAddress.Text) ? null : TxtAddress.Text.Trim(),
                    TIN = string.IsNullOrWhiteSpace(TxtTIN.Text) ? null : TxtTIN.Text.Trim(),
                    IsActive   = ChkActive.IsChecked == true,
                    IsArchived = ChkIsArchived.IsChecked == true,
                    IsRefiller = ChkIsRefiller.IsChecked == true,
                    IsDisposer = ChkIsDisposer.IsChecked == true,
                    IsBuyer    = ChkIsBuyer.IsChecked == true,
                    CreatedDate = _vendor.CreatedDate
                };

                bool success = await _repository.UpdateVendorAsync(vendor);

                Cursor = null;

                if (success)
                {
                    string successMessage = archiveStatusChanged && ChkIsArchived.IsChecked == true
                        ? $"Vendor '{vendor.VendorName}' archived successfully!"
                        : $"Vendor '{vendor.VendorName}' updated successfully!";

                    WinMsgBox.Show(successMessage, "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    WinMsgBox.Show("Failed to update vendor. Please try again.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnSave.IsEnabled = true;
                    BtnCancel.IsEnabled = true;
                    BtnSave.Content = "Save";
                }
            }
            catch (Exception ex)
            {
                Cursor = null;
                BtnSave.IsEnabled = true;
                BtnCancel.IsEnabled = true;
                BtnSave.Content = "Save";

                WinMsgBox.Show(
                    $"Failed to update vendor:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
