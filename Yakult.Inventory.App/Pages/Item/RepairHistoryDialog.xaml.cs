using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class RepairHistoryDialog : Window, IDisposable
    {
        private static readonly Regex ItCallTagRegex = new Regex(@"\[ITCALL:(?<code>[^\]#]+)(?:#(?<id>\d+))?\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string _connectionString;
        private readonly string _serialNumber;
        private readonly ObservableCollection<RepairHistoryRow> _viewRows = new ObservableCollection<RepairHistoryRow>();
        private List<RepairHistoryRow> _allRows = new List<RepairHistoryRow>();

        public RepairHistoryDialog(string connectionString, string serialNumber)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _serialNumber = (serialNumber ?? string.Empty).Trim();
            InitializeComponent();
            HistoryGrid.ItemsSource = _viewRows;
            Loaded += async (s, e) => { try { await OnLoadedAsync(); } catch (Exception ex) { HandleLoadError(ex); } };
            Dispatcher.UnhandledException += OnDispatcherUnhandled;
            Closed += (s, e) => Dispatcher.UnhandledException -= OnDispatcherUnhandled;
        }
        private void OnDispatcherUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RepairHistory Dispatcher {e.Exception}\r\n"); } catch { }
            System.Windows.MessageBox.Show(e.Exception.Message, "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
        private void HandleLoadError(Exception ex)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RepairHistory OnLoaded {ex}\r\n"); } catch { }
            System.Windows.MessageBox.Show($"Failed to open repair history:\n\n{ex.Message}", "Dialog Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task OnLoadedAsync()
        {
            try
            {
                TxtSubtitle.Text = $"Serial: {(_serialNumber.Length == 0 ? "—" : _serialNumber)}";
                try { TxtSearch.Focus(); } catch { }
                await LoadDataAsync();
            }
            catch (Exception ex) { HandleLoadError(ex); }
        }

        private async Task LoadDataAsync()
        {
            _allRows = new List<RepairHistoryRow>();
            try
            {
                // enrich header best-effort
                try
                {
                    const string itemSql = @"
SELECT TOP 1 i.Name, i.ModelNumber, i.SerialNumber, ic.Name AS CategoryName
FROM dbo.Item i LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId = i.CategoryId
WHERE i.SerialNumber = @SerialNumber ORDER BY i.ItemId DESC;";
                    using (var con = new SqlConnection(_connectionString))
                    using (var cmd = new SqlCommand(itemSql, con))
                    {
                        cmd.Parameters.AddWithValue("@SerialNumber", _serialNumber);
                        await con.OpenAsync();
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var name = reader.IsDBNull(0) ? null : reader.GetString(0);
                                var model = reader.IsDBNull(1) ? null : reader.GetString(1);
                                var serial = reader.IsDBNull(2) ? null : reader.GetString(2);
                                var cat = reader.IsDBNull(3) ? null : reader.GetString(3);
                                var parts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(serial)) parts.Add($"Serial: {serial.Trim()}");
                                if (!string.IsNullOrWhiteSpace(name)) parts.Add(name.Trim());
                                if (!string.IsNullOrWhiteSpace(model)) parts.Add($"Model: {model.Trim()}");
                                if (!string.IsNullOrWhiteSpace(cat)) parts.Add($"Category: {cat.Trim()}");
                                if (parts.Count > 0) TxtSubtitle.Text = string.Join("  •  ", parts);
                            }
                        }
                    }
                }
                catch { }

                const string sql = @"
SELECT TOP 500 RepairId, UpdateId, CreatedAt, ConditionName, RepairAction, PreviousStatus, NewStatus, Remark, SetCode, ProcessedByName
FROM dbo.ItemRepairHistory WHERE SerialNumber = @SerialNumber ORDER BY CreatedAt DESC, RepairId DESC;";
                using (var con = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SerialNumber", _serialNumber);
                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var setCode = reader.IsDBNull(8) ? null : reader.GetString(8);
                            _allRows.Add(new RepairHistoryRow
                            {
                                RepairId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                UpdateId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                CreatedAt = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                                ConditionName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                RepairAction = reader.IsDBNull(4) ? null : reader.GetString(4),
                                PreviousStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                                NewStatus = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Remark = reader.IsDBNull(7) ? null : reader.GetString(7),
                                SetCode = setCode,
                                ReferenceCode = string.IsNullOrWhiteSpace(setCode) ? null : setCode.Trim(),
                                ProcessedByName = reader.IsDBNull(9) ? null : reader.GetString(9)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to load repair history: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var q = (TxtSearch.Text ?? string.Empty).Trim();
            var actionSel = (CmbAction.SelectedItem as ComboBoxItem)?.Content as string ?? "All Actions";
            IEnumerable<RepairHistoryRow> filtered = _allRows;
            if (!string.Equals(actionSel, "All Actions", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(r => string.Equals((r.RepairAction ?? "").Trim(), actionSel, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(q))
                filtered = filtered.Where(r =>
                    (r.ConditionName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.RepairAction ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.Remark ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.SetCode ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.ReferenceCode ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.ProcessedByName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.PreviousStatus ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (r.NewStatus ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            var list = filtered.ToList();
            _viewRows.Clear();
            foreach (var r in list) _viewRows.Add(r);
            UpdateSummary(list);
            TxtEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (list.Count > 0) HistoryGrid.SelectedIndex = 0;
            else SetDetailsFromRow(null);
        }

        private void UpdateSummary(IReadOnlyCollection<RepairHistoryRow> rows)
        {
            var total = rows?.Count ?? 0;
            LblTotal.Text = total.ToString();
            LblRepaired.Text = (rows?.Count(x => string.Equals((x.RepairAction ?? "").Trim(), "Repaired", StringComparison.OrdinalIgnoreCase)) ?? 0).ToString();
            LblUnrepaired.Text = (rows?.Count(x => string.Equals((x.RepairAction ?? "").Trim(), "Unrepaired", StringComparison.OrdinalIgnoreCase)) ?? 0).ToString();
            LblSpare.Text = (rows?.Count(x => string.Equals((x.RepairAction ?? "").Trim(), "Spare", StringComparison.OrdinalIgnoreCase)) ?? 0).ToString();
            var last = rows?.Select(x => x.CreatedAt).Where(x => x.HasValue).Max();
            LblLast.Text = last.HasValue ? last.Value.ToString("yyyy-MM-dd HH:mm") : "—";
        }

        private void SetDetailsFromRow(RepairHistoryRow row)
        {
            if (row == null)
            {
                TxtCreated.Text = ""; TxtAction.Text = ""; TxtCondition.Text = ""; TxtProcessedBy.Text = ""; TxtRef.Text = ""; TxtFrom.Text = ""; TxtTo.Text = ""; TxtIds.Text = ""; TxtRemark.Text = "";
                return;
            }
            TxtCreated.Text = row.CreatedAt.HasValue ? row.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm") : "";
            TxtAction.Text = row.RepairAction ?? "";
            TxtCondition.Text = row.ConditionName ?? "";
            TxtProcessedBy.Text = row.ProcessedByName ?? "";
            TxtRef.Text = row.ReferenceCode ?? (row.SetCode ?? "");
            TxtFrom.Text = row.PreviousStatus ?? "";
            TxtTo.Text = row.NewStatus ?? "";
            TxtIds.Text = $"RepairId={row.RepairId} • UpdateId={row.UpdateId}";
            TxtRemark.Text = row.Remark ?? "";
        }

        // events
        private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();
        private void OnFilterChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
        private void OnClearClick(object sender, RoutedEventArgs e) { TxtSearch.Text = ""; CmbAction.SelectedIndex = 0; }
        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = HistoryGrid.SelectedItem as RepairHistoryRow;
            SetDetailsFromRow(row);
        }
        private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = HistoryGrid.SelectedItem as RepairHistoryRow;
            if (row == null) return;
            // only if Ticket column double-click? For now open if ticket exists
            if (string.IsNullOrWhiteSpace(row.ItCallTicketCode) && !row.ItCallTicketId.HasValue) return;
            await TryOpenItCallTicketAsync(row);
        }
        private void OnCopyRemarkClick(object sender, RoutedEventArgs e) { if (!string.IsNullOrWhiteSpace(TxtRemark.Text)) try { System.Windows.Clipboard.SetText(TxtRemark.Text); } catch { } }
        private void OnCopyAllClick(object sender, RoutedEventArgs e)
        {
            var text = $"Serial: {_serialNumber}\r\nCreated: {TxtCreated.Text}\r\nAction: {TxtAction.Text}\r\nCondition: {TxtCondition.Text}\r\nProcessed By: {TxtProcessedBy.Text}\r\nRef: {TxtRef.Text}\r\nFrom: {TxtFrom.Text}\r\nTo: {TxtTo.Text}\r\nIds: {TxtIds.Text}\r\n\r\nRemark:\r\n{TxtRemark.Text}";
            try { System.Windows.Clipboard.SetText(text); } catch { }
        }
        private void OnCloseClickBtn(object sender, RoutedEventArgs e) => Close();

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) DragMove(); }
        private void OnMinimizeClick(object sender, MouseButtonEventArgs e) { e.Handled = true; Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this); }
        private void OnMaximizeClick(object sender, MouseButtonEventArgs e) { e.Handled = true; WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void OnCloseClick(object sender, MouseButtonEventArgs e) { e.Handled = true; Close(); }

        public void Dispose() { }
        public new WinForms.DialogResult ShowDialog()
        {
            var activeForm = WinForms.Form.ActiveForm;
            if (activeForm != null) new WindowInteropHelper(this).Owner = activeForm.Handle;
            ClampToWorkArea();
            bool? r = base.ShowDialog();
            return r == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }
        public new WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            try { if (owner != null) new WindowInteropHelper(this).Owner = owner.Handle; } catch { }
            ClampToWorkArea();
            bool? r = base.ShowDialog();
            return r == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }
        private void ClampToWorkArea()
        {
            var workArea = SystemParameters.WorkArea;
            const double m = 24;
            if (Width > workArea.Width - m) Width = Math.Max(MinWidth, workArea.Width - m);
            if (Height > workArea.Height - m) Height = Math.Max(MinHeight, workArea.Height - m);
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        private static bool TryExtractItCallTicket(string remark, out string ticketCode, out int ticketId)
        {
            ticketCode = null; ticketId = 0;
            var text = (remark ?? string.Empty).Trim();
            if (text.Length == 0) return false;
            var m = ItCallTagRegex.Match(text);
            if (!m.Success) return false;
            ticketCode = m.Groups["code"]?.Value?.Trim();
            var idRaw = m.Groups["id"]?.Value;
            if (!string.IsNullOrWhiteSpace(idRaw) && int.TryParse(idRaw, out var id) && id > 0) ticketId = id;
            if (string.IsNullOrWhiteSpace(ticketCode)) ticketCode = null;
            return ticketCode != null || ticketId > 0;
        }
        private async Task TryOpenItCallTicketAsync(RepairHistoryRow row)
        {
            if (row == null) return;
            try
            {
                var repo = new CallMonitoringRepository();
                if (!await repo.CallSchemaExistsAsync())
                {
                    System.Windows.MessageBox.Show("IT Call Monitoring is not installed in the current database.", "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var ticketId = row.ItCallTicketId.GetValueOrDefault();
                if (ticketId <= 0 && !string.IsNullOrWhiteSpace(row.ItCallTicketCode))
                {
                    var matches = await repo.GetTicketsAsync(statusFilter: "All", searchText: row.ItCallTicketCode, maxRows: 20);
                    var exact = matches?.FirstOrDefault(t => string.Equals((t.TicketCode ?? string.Empty).Trim(), row.ItCallTicketCode.Trim(), StringComparison.OrdinalIgnoreCase));
                    ticketId = exact?.TicketId ?? 0;
                }
                if (ticketId <= 0) { System.Windows.MessageBox.Show("Ticket not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                using (var dlg = new TicketDetailsDialog(repo, ticketId))
                {
                    dlg.ShowDialog(new WinFormsWrapper(new WindowInteropHelper(this).Handle));
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show("Failed to open ticket:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private sealed class WinFormsWrapper : WinForms.IWin32Window { public WinFormsWrapper(IntPtr h) => Handle = h; public IntPtr Handle { get; } }

        private sealed class RepairHistoryRow
        {
            public int RepairId { get; set; }
            public int UpdateId { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string ConditionName { get; set; }
            public string RepairAction { get; set; }
            public string PreviousStatus { get; set; }
            public string NewStatus { get; set; }
            public string Remark { get; set; }
            public string SetCode { get; set; }
            public string ReferenceCode { get; set; }
            public string ProcessedByName { get; set; }
            public int? ItCallTicketId { get { TryExtractItCallTicket(Remark, out _, out var id); return id > 0 ? (int?)id : null; } }
            public string ItCallTicketCode { get { TryExtractItCallTicket(Remark, out var code, out _); return code; } }
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
