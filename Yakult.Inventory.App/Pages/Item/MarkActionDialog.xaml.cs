using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class MarkActionDialog : Window, IDisposable
    {
        private readonly List<RepairedItemRow> _rows;
        private readonly string _actionLabel;
        private readonly string _actionDisplay;
        private readonly Brush _actionBrush;

        private int? _associatedTicketId;
        private string _associatedTicketCode;
        private List<CallTicketListItem> _cachedTickets;

        public string ReasonText => string.IsNullOrWhiteSpace(TxtReason?.Text) ? null : TxtReason.Text.Trim();
        public bool ResolveCallTicket => ChkReplacement != null && ChkReplacement.IsChecked == true;
        public int? CallTicketId
        {
            get
            {
                if (!ResolveCallTicket) return null;
                if (int.TryParse(TxtTicketId?.Text?.Trim(), out var v) && v > 0) return v;
                return null;
            }
        }
        public string ResolveStatus => ResolveCallTicket ? ((CmbResolveStatus?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Solved").Trim() : null;
        public string ExtraTicketNote => string.IsNullOrWhiteSpace(TxtTicketNote?.Text) ? null : TxtTicketNote.Text.Trim();
        public int? AssociatedTicketId => _associatedTicketId;
        public string AssociatedTicketCode => _associatedTicketCode;

        public string RemarkText
        {
            get
            {
                var reason = ReasonText;
                if (string.IsNullOrWhiteSpace(reason)) return null;
                var parts = new List<string>();
                var tag = BuildItCallTag(_associatedTicketCode, _associatedTicketId);
                if (!string.IsNullOrWhiteSpace(tag)) parts.Add(tag);
                parts.Add("Reason: " + reason);
                var remark = string.IsNullOrWhiteSpace(TxtRemark?.Text) ? null : TxtRemark.Text.Trim();
                if (!string.IsNullOrWhiteSpace(remark)) parts.Add("Remark: " + remark);
                if (ResolveCallTicket && CallTicketId.HasValue && CallTicketId.Value > 0)
                    parts.Add("CallTicketId: " + CallTicketId.Value);
                const int maxLen = 500;
                var composed = string.Join(" | ", parts);
                if (composed.Length <= maxLen) return composed;
                var baseParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(tag)) baseParts.Add(tag);
                baseParts.Add("Reason: " + reason);
                var baseText = string.Join(" | ", baseParts);
                if (baseText.Length >= maxLen) return baseText.Substring(0, maxLen);
                var remainingSpace = maxLen - baseText.Length;
                var optionalParts = parts.Skip(baseParts.Count).ToList();
                var finalParts = new List<string>(baseParts);
                foreach (var p in optionalParts)
                {
                    var next = " | " + p;
                    if (next.Length <= remainingSpace) { finalParts.Add(p); remainingSpace -= next.Length; continue; }
                    if (p.StartsWith("Remark:", StringComparison.OrdinalIgnoreCase) && remainingSpace > 5)
                    {
                        var candidate = p;
                        var maxPartLen = Math.Max(0, remainingSpace - 3);
                        if (candidate.Length > maxPartLen)
                        {
                            var cut = Math.Max(0, maxPartLen - 1);
                            candidate = cut > 0 ? candidate.Substring(0, cut).TrimEnd() + "…" : null;
                        }
                        if (!string.IsNullOrWhiteSpace(candidate)) finalParts.Add(candidate);
                    }
                    break;
                }
                var finalText = string.Join(" | ", finalParts);
                return finalText.Length <= maxLen ? finalText : finalText.Substring(0, maxLen);
            }
        }

        public MarkActionDialog(RepairedItemRow row, string actionLabel)
            : this(row != null ? new List<RepairedItemRow> { row } : new List<RepairedItemRow>(), actionLabel) { }

        public MarkActionDialog(List<RepairedItemRow> rows, string actionLabel)
        {
            _rows = rows?.Where(r => r != null).ToList() ?? new List<RepairedItemRow>();
            _actionLabel = (actionLabel ?? string.Empty).Trim();
            _actionDisplay = GetActionDisplay(_actionLabel);
            _actionBrush = GetActionBrush(_actionLabel);
            InitializeComponent();
            Loaded += OnLoaded;
            // Prevent WPF exception from killing the WinForms process (Application.Current is null in this app)
            Dispatcher.UnhandledException += OnDispatcherUnhandled;
            Closed += (s, e) => Dispatcher.UnhandledException -= OnDispatcherUnhandled;
        }
        private void OnDispatcherUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MarkAction Dispatcher {e.Exception}\r\n"); } catch { }
            System.Windows.MessageBox.Show(e.Exception.Message, "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Batch log for diagnostics
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogBatch.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MarkActionDialog opened {_actionDisplay} with {_rows.Count} rows: {string.Join(", ", _rows.Select(r => r?.Name ?? "?"))}\r\n"); } catch { }

                HeaderBar.Background = _actionBrush;
                TxtTitle.Text = _actionDisplay;
                TxtOkLabel.Text = _actionDisplay;
                BtnOk.Background = _actionBrush;

                var isBulk = _rows.Count > 1;
                var first = _rows.FirstOrDefault();

                // Header subtitle + banner
                if (isBulk)
                {
                    BulkBanner.Visibility = Visibility.Visible;
                    TxtBulkBanner.Text = $"{_rows.Count} items selected — the reason, remark, ticket link and replacement option below will be applied to every selected item.";
                    TxtSubtitle.Text = $"{_rows.Count} items selected • action will apply to all";
                    TxtSelectedHeader.Text = $"SELECTED ITEMS ({_rows.Count})";
                    TxtSelectedCount.Text = $"{_rows.Count} items";
                    TxtMoreHint.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BulkBanner.Visibility = Visibility.Collapsed;
                    var serial = string.IsNullOrWhiteSpace(first?.SerialNumber) ? "—" : first.SerialNumber.Trim();
                    var model = string.IsNullOrWhiteSpace(first?.ModelNumber) ? "—" : first.ModelNumber.Trim();
                    var cat = string.IsNullOrWhiteSpace(first?.Category) ? "—" : first.Category.Trim();
                    TxtSubtitle.Text = $"Serial: {serial}  •  Model: {model}  •  Category: {cat}";
                    TxtSelectedHeader.Text = "SELECTED ITEM";
                    TxtSelectedCount.Text = "1 item";
                    TxtMoreHint.Visibility = Visibility.Collapsed;
                }

                // Show every checked row in the grid — this is the batch-ready table the user asked for
                try
                {
                    SelectedItemsGrid.ItemsSource = _rows;
                    SelectedItemsGrid.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"MarkAction SelectedItemsGrid bind failed {ex}\r\n"); } catch { }
                }

                TxtReason.Focus();
                ValidateState();
            }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MarkAction OnLoaded {ex}\r\n"); } catch { }
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

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            try { if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) DragMove(); } catch { }
        }
        private void OnMinimizeClick(object sender, MouseButtonEventArgs e) { e.Handled = true; try { WindowState = WindowState.Minimized; } catch { } }
        private void OnCloseClick(object sender, MouseButtonEventArgs e) { e.Handled = true; try { Close(); } catch { } }
        private void OnCancelClick(object sender, RoutedEventArgs e) { try { DialogResult = false; Close(); } catch { } }
        private void OnOkClick(object sender, RoutedEventArgs e) { try { if (!BtnOk.IsEnabled) return; DialogResult = true; Close(); } catch (Exception ex) { try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MarkAction OnOk {ex}\r\n"); } catch { } } }

        private void OnReasonChanged(object sender, TextChangedEventArgs e) { try { ValidateState(); } catch { } }
        private void OnRemarkChanged(object sender, TextChangedEventArgs e)
        {
            try { LblCounter.Text = $"{(TxtRemark.Text?.Length ?? 0)} / 500"; ValidateState(); } catch { }
        }
        private void OnReplacementToggled(object sender, RoutedEventArgs e)
        {
            try { var on = ChkReplacement.IsChecked == true; ReplacementGrid.IsEnabled = on; ReplacementGrid.Opacity = on ? 1 : 0.6; ValidateState(); } catch { }
        }
        private void OnTicketIdPreviewTextInput(object sender, TextCompositionEventArgs e) { try { e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$"); } catch { } }
        private void OnTicketIdChanged(object sender, TextChangedEventArgs e) { try { ValidateState(); } catch { } }
        private void OnResolveStatusChanged(object sender, SelectionChangedEventArgs e) { try { ValidateState(); } catch { } }

        private void ValidateState()
        {
            try
            {
                var reasonOk = !string.IsNullOrWhiteSpace(ReasonText);
                var ticketOk = true;
                if (ResolveCallTicket) ticketOk = CallTicketId.HasValue && CallTicketId.Value > 0;
                if (BtnOk != null) BtnOk.IsEnabled = reasonOk && ticketOk;
                if (LblReasonHint != null) LblReasonHint.Text = reasonOk ? "" : "Required";
                if (TxtReason != null)
                {
                    if (!reasonOk) TxtReason.BorderBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    else TxtReason.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 210, 220));
                }
            }
            catch { }
        }

        private static string BuildItCallTag(string ticketCode, int? ticketId)
        {
            var code = (ticketCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code.Replace("]", string.Empty).Replace("#", string.Empty);
            if (code.Length > 60) code = code.Substring(0, 60);
            var id = ticketId.GetValueOrDefault();
            if (id > 0) return $"[ITCALL:{code}#{id}]";
            return $"[ITCALL:{code}]";
        }

        private void ClearAssociatedTicket()
        {
            _associatedTicketId = null;
            _associatedTicketCode = null;
            TxtAssociatedTicket.Text = "Not linked";
            BtnOpenTicket.IsEnabled = false;
            BtnClearTicket.IsEnabled = false;
        }

        private async Task OpenAssociatedTicketAsync()
        {
            try
            {
                var repo = new CallMonitoringRepository();
                if (!await repo.CallSchemaExistsAsync())
                {
                    System.Windows.MessageBox.Show("IT Call Monitoring is not installed in the current database.", "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var ticketId = _associatedTicketId.GetValueOrDefault();
                if (ticketId <= 0 && !string.IsNullOrWhiteSpace(_associatedTicketCode))
                {
                    var matches = await repo.GetTicketsAsync(statusFilter: "All", searchText: _associatedTicketCode.Trim(), maxRows: 20);
                    var exact = matches?.FirstOrDefault(t => string.Equals((t.TicketCode ?? string.Empty).Trim(), _associatedTicketCode.Trim(), StringComparison.OrdinalIgnoreCase));
                    ticketId = exact?.TicketId ?? 0;
                }
                if (ticketId <= 0)
                {
                    System.Windows.MessageBox.Show("Ticket not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                using (var dlg = new TicketDetailsDialog(repo, ticketId))
                {
                    dlg.ShowDialog(new WinFormsWrapper(new WindowInteropHelper(this).Handle));
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to open ticket:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PickAssociatedTicketAsync()
        {
            var btn = FindName("BtnPick") as Button; // fallback; actual button uses InlineBtn style
            // Use the first Select button via visual tree not needed; just proceed
            try
            {
                var repo = new CallMonitoringRepository();
                if (!await repo.CallSchemaExistsAsync())
                {
                    System.Windows.MessageBox.Show("IT Call Monitoring is not installed in the current database.", "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (_cachedTickets == null)
                    _cachedTickets = await repo.GetTicketsAsync(statusFilter: "All", searchText: null, maxRows: 500);
                if (_cachedTickets == null || _cachedTickets.Count == 0)
                {
                    System.Windows.MessageBox.Show("No tickets found.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                using (var picker = new TicketPickerDialog(_cachedTickets, initialQuery: _associatedTicketCode))
                {
                    if (picker.ShowDialog(new WinFormsWrapper(new WindowInteropHelper(this).Handle)) != WinForms.DialogResult.OK) return;
                    var selected = picker.SelectedTicket;
                    if (selected == null || selected.TicketId <= 0) return;
                    _associatedTicketId = selected.TicketId;
                    _associatedTicketCode = string.IsNullOrWhiteSpace(selected.TicketCode) ? selected.TicketId.ToString() : selected.TicketCode.Trim();
                    var status = string.IsNullOrWhiteSpace(selected.Status) ? string.Empty : $" • {selected.Status.Trim()}";
                    TxtAssociatedTicket.Text = $"{_associatedTicketCode}{status}";
                    BtnClearTicket.IsEnabled = true;
                    BtnOpenTicket.IsEnabled = _associatedTicketId.HasValue && _associatedTicketId.Value > 0;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(TxtTicketId.Text) || TxtTicketId.Text.Trim() == "0")
                        {
                            if (selected.TicketId > 0) TxtTicketId.Text = selected.TicketId.ToString();
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to load tickets:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnPickTicketClick(object sender, RoutedEventArgs e) => await PickAssociatedTicketAsync();
        private async void OnOpenTicketClick(object sender, RoutedEventArgs e) => await OpenAssociatedTicketAsync();
        private void OnClearTicketClick(object sender, RoutedEventArgs e) => ClearAssociatedTicket();

        private static Brush GetActionBrush(string actionLabel)
        {
            if (string.IsNullOrWhiteSpace(actionLabel)) return new SolidColorBrush(Color.FromRgb(58, 142, 246));
            actionLabel = actionLabel.Trim();
            if (actionLabel.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.FromRgb(231, 76, 60));
            if (actionLabel.Equals(RepairedItemRow.SpareRepairAction, StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.FromRgb(52, 152, 219));
            if (actionLabel.StartsWith("Repaired", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.FromRgb(39, 174, 96));
            return new SolidColorBrush(Color.FromRgb(58, 142, 246));
        }
        private static string GetActionDisplay(string actionLabel)
        {
            if (string.IsNullOrWhiteSpace(actionLabel)) return "Confirm";
            if (actionLabel.Trim().Equals(RepairedItemRow.SpareRepairAction, StringComparison.OrdinalIgnoreCase)) return "Mark Spare";
            if (actionLabel.Trim().Equals("Unrepaired", StringComparison.OrdinalIgnoreCase)) return "Mark Unrepaired";
            if (actionLabel.Trim().Equals("Repaired", StringComparison.OrdinalIgnoreCase)) return "Mark Repaired";
            return $"Mark {actionLabel.Trim()}";
        }

        private sealed class WinFormsWrapper : WinForms.IWin32Window
        {
            public WinFormsWrapper(IntPtr h) => Handle = h;
            public IntPtr Handle { get; }
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
