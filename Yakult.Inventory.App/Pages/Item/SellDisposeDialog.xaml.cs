using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class SellDisposeDialog : Window, IDisposable
    {
        private readonly List<RepairedItemRow> _rows;
        private readonly string _action;
        private readonly bool _isSell;

        public int Quantity
        {
            get
            {
                if (int.TryParse(TxtQty?.Text?.Trim(), out var v) && v > 0) return v;
                return 1;
            }
        }
        public string RecipientName => TxtRecipient?.Text?.Trim();
        public decimal? SaleAmount
        {
            get
            {
                if (!_isSell) return null;
                if (decimal.TryParse(TxtAmount?.Text?.Trim(), out var v) && v > 0) return v;
                return null;
            }
        }
        public string Remarks => string.IsNullOrWhiteSpace(TxtRemarks?.Text) ? null : TxtRemarks.Text.Trim();

        public SellDisposeDialog(RepairedItemRow row, string action)
            : this(row != null ? new List<RepairedItemRow> { row } : new List<RepairedItemRow>(), action) { }

        public SellDisposeDialog(List<RepairedItemRow> rows, string action)
        {
            _rows = rows?.Where(r => r != null).ToList() ?? new List<RepairedItemRow>();
            _action = (action ?? string.Empty).Trim();
            _isSell = string.Equals(_action, "Sold", StringComparison.OrdinalIgnoreCase);
            InitializeComponent();
            Loaded += OnLoaded;
            Dispatcher.UnhandledException += OnDispatcherUnhandled;
            Closed += (s, e) => Dispatcher.UnhandledException -= OnDispatcherUnhandled;
        }
        private void OnDispatcherUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SellDispose Dispatcher {e.Exception}\r\n"); } catch { }
            System.Windows.MessageBox.Show(e.Exception.Message, "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogBatch.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SellDisposeDialog opened {(_isSell ? "Sell" : "Dispose")} with {_rows.Count} rows: {string.Join(", ", _rows.Select(r => r?.Name ?? "?"))}\r\n"); } catch { }

                var title = _isSell ? "Sell Item" : "Dispose Item";
                var subtitle = _isSell ? "Record a sale and archive the item(s)" : "Dispose and archive the item(s)";
                TxtTitle.Text = title;
                TxtSubtitle.Text = subtitle;
                TxtOkLabel.Text = title;
                var brush = _isSell ? new SolidColorBrush(Color.FromRgb(58, 142, 246)) : new SolidColorBrush(Color.FromRgb(231, 76, 60));
                HeaderBar.Background = brush;
                BtnOk.Background = brush;
                CountBadge.Background = brush;
                TxtWarning.Text = _isSell
                    ? "This will mark the item(s) as Sold and archive them. You can restore from Archive later if needed."
                    : "This will mark the item(s) as Disposed and archive them. You can restore from Archive later if needed.";

                AmountPanel.Visibility = _isSell ? Visibility.Visible : Visibility.Collapsed;
                AmountPanel.IsEnabled = _isSell;

                var isBulk = _rows.Count > 1;
                if (isBulk)
                {
                    BulkBanner.Visibility = Visibility.Visible;
                    TxtBulkBanner.Text = $"{_rows.Count} items selected — quantity, recipient, amount and remarks will be applied identically to each item.";
                    TxtSubtitle.Text = $"{_rows.Count} items will be archived as {(_isSell ? "Sold" : "Disposed")} — quantity is applied per item.";
                    TxtSelectedHeader.Text = $"ITEMS TO ARCHIVE ({_rows.Count})";
                    TxtSelectedCount.Text = $"{_rows.Count} items";
                    TxtMoreHint.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BulkBanner.Visibility = Visibility.Collapsed;
                    var first = _rows.FirstOrDefault();
                    var serial = string.IsNullOrWhiteSpace(first?.SerialNumber) ? "—" : first.SerialNumber.Trim();
                    TxtSubtitle.Text = $"Serial: {serial} • {title.ToLower()} single item";
                    TxtSelectedHeader.Text = "ITEM TO ARCHIVE";
                    TxtSelectedCount.Text = "1 item";
                    TxtMoreHint.Visibility = Visibility.Collapsed;
                }

                try
                {
                    SelectedItemsGrid.ItemsSource = _rows;
                    SelectedItemsGrid.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"SellDispose SelectedItemsGrid bind failed {ex}\r\n"); } catch { }
                }
                TxtQty.Text = "1";
                ValidateQty();
            }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SellDispose OnLoaded {ex}\r\n"); } catch { }
                System.Windows.MessageBox.Show($"Failed to open dialog:\n\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose() { }

        public new WinForms.DialogResult ShowDialog()
        {
            var activeForm = WinForms.Form.ActiveForm;
            if (activeForm != null) new WindowInteropHelper(this).Owner = activeForm.Handle;
            ClampToWorkArea();
            bool? result = base.ShowDialog();
            return (result == true) ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }
        public new WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            try { if (owner != null) new WindowInteropHelper(this).Owner = owner.Handle; } catch { }
            ClampToWorkArea();
            bool? result = base.ShowDialog();
            return (result == true) ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }
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
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e) { try { if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) DragMove(); } catch { } }
        private void OnMinimizeClick(object sender, MouseButtonEventArgs e) { e.Handled = true; try { WindowState = WindowState.Minimized; } catch { } }
        private void OnCloseClick(object sender, MouseButtonEventArgs e) { e.Handled = true; try { Close(); } catch { } }
        private void OnCancelClick(object sender, RoutedEventArgs e) { try { DialogResult = false; Close(); } catch { } }
        private void OnOkClick(object sender, RoutedEventArgs e) { try { if (!ValidateQty()) return; DialogResult = true; Close(); } catch (Exception ex) { try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SellDispose OnOk {ex}\r\n"); } catch { } } }

        private void OnQtyPreviewTextInput(object sender, TextCompositionEventArgs e) { try { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); } catch { } }
        private void OnQtyChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { try { ValidateQty(); } catch { } }
        private bool ValidateQty()
        {
            try
            {
                if (TxtQty == null || BtnOk == null) return false;
                if (!int.TryParse(TxtQty.Text.Trim(), out var v) || v < 1)
                {
                    TxtQty.BorderBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    BtnOk.IsEnabled = false;
                    return false;
                }
                var first = _rows.FirstOrDefault();
                int max = 9999;
                if (_rows.Count > 1) max = _rows.Min(r => r.StockOnHand > 0 ? r.StockOnHand : 9999);
                else if (first != null && first.StockOnHand > 0) max = first.StockOnHand;
                if (v > max)
                {
                    TxtQty.BorderBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    BtnOk.IsEnabled = false;
                    return false;
                }
                TxtQty.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 210, 220));
                BtnOk.IsEnabled = true;
                return true;
            }
            catch { return false; }
        }
        private void OnAmountPreviewTextInput(object sender, TextCompositionEventArgs e) { try { e.Handled = !Regex.IsMatch(e.Text, "^[0-9.]+$"); } catch { } }
        private void OnAmountChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (!_isSell || TxtAmount == null) return;
                var t = TxtAmount.Text.Trim();
                if (string.IsNullOrEmpty(t)) { TxtAmount.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 210, 220)); return; }
                if (decimal.TryParse(t, out _)) TxtAmount.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 210, 220));
                else TxtAmount.BorderBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }
            catch { }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource hs) hs.AddHook(WmGetMinMaxInfoHook);
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
                    var mi = new NativeMethods.MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                    NativeMethods.GetMonitorInfo(monitor, ref mi);
                    var work = mi.rcWork; var mon = mi.rcMonitor;
                    mmi.ptMaxPosition.X = Math.Abs(work.Left - mon.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(work.Top - mon.Top);
                    mmi.ptMaxSize.X = work.Right - work.Left;
                    mmi.ptMaxSize.Y = work.Bottom - work.Top;
                    mmi.ptMaxTrackSize = mmi.ptMaxSize;
                }
                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }
        private static class NativeMethods
        {
            public const int MONITOR_DEFAULTTONEAREST = 0x00000002;
            [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
            [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
            [StructLayout(LayoutKind.Sequential)] public struct MINMAXINFO { public POINT ptReserved; public POINT ptMaxSize; public POINT ptMaxPosition; public POINT ptMinTrackSize; public POINT ptMaxTrackSize; }
            [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }
            [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);
            [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        }
    }
}
