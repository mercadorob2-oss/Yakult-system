using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.WPF.RepairedItems.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.RepairedItems.Views
{
    public partial class RepairedItemsPageView : UserControl
    {
        private readonly RepairedItemsPageViewModel _vm;

        public RepairedItemsPageView()
        {
            InitializeComponent();

            _vm = new RepairedItemsPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ErrorOccurred += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.CopyToClipboard = text => { try { System.Windows.Clipboard.SetText(text); } catch { } };
            _vm.RequestMarkAction += OnRequestMarkAction;
            _vm.RequestSellDispose += OnRequestSellDispose;
            _vm.RequestViewRepairHistory += OnRequestViewRepairHistory;
            _vm.RequestSaveFilePath += OnRequestSaveFilePath;
            _vm.RebuildSortByOptionsRequested += RebuildSortByOptions;
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        public void NotifyClosed() => _vm.MarkDisposed();

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(ItemsGrid);
            _vm.SetSortByOptionsOnce(options);
        }

        private void ItemsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key);

            foreach (var col in ItemsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = _vm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;
        }

        private void OnRequestMarkAction(System.Collections.Generic.List<RepairedItemRow> rows, string actionLabel)
        {
            try
            {
                if (rows == null || rows.Count == 0) return;
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogBatch.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnRequestMarkAction {actionLabel} rows={rows.Count} ids={string.Join(",", rows.Select(r => r?.ItemId.ToString() ?? "?"))} pagedChecked={_vm.PagedRows.Count(r => r.Selected)} totalPaged={_vm.PagedRows.Count}\r\n"); } catch { }
                using (var dlg = new MarkActionDialog(rows, actionLabel))
                {
                    WinForms.DialogResult result;
                    try { result = dlg.ShowDialog(GetOwner()); }
                    catch (Exception ex) { ShowDialogError("Mark Action", ex); return; }
                    if (result != WinForms.DialogResult.OK) return;
                    _ = _vm.ConfirmAndSaveMarkActionAsync(rows, actionLabel, dlg.RemarkText,
                        dlg.ResolveCallTicket, dlg.CallTicketId, dlg.ResolveStatus, dlg.ReasonText, dlg.ExtraTicketNote);
                }
            }
            catch (Exception ex) { ShowDialogError("Mark Action", ex); }
        }

        private void OnRequestSellDispose(System.Collections.Generic.List<RepairedItemRow> rows, string action)
        {
            try
            {
                if (rows == null || rows.Count == 0) return;
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogBatch.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnRequestSellDispose {action} rows={rows.Count} ids={string.Join(",", rows.Select(r => r?.ItemId.ToString() ?? "?"))} pagedChecked={_vm.PagedRows.Count(r => r.Selected)}\r\n"); } catch { }
                using (var dlg = new SellDisposeDialog(rows, action))
                {
                    WinForms.DialogResult result;
                    try { result = dlg.ShowDialog(GetOwner()); }
                    catch (Exception ex) { ShowDialogError("Sell/Dispose", ex); return; }
                    if (result != WinForms.DialogResult.OK) return;
                    _ = _vm.ConfirmAndExecuteLifecycleActionAsync(rows, action, dlg.Quantity, dlg.RecipientName, dlg.SaleAmount, dlg.Remarks);
                }
            }
            catch (Exception ex) { ShowDialogError("Sell/Dispose", ex); }
        }

        private void OnRequestViewRepairHistory(string serialNumber)
        {
            try
            {
                using (var dlg = new RepairHistoryDialog(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString, serialNumber))
                {
                    try { dlg.ShowDialog(GetOwner()); }
                    catch (Exception ex) { ShowDialogError("Repair History", ex); }
                }
            }
            catch (Exception ex) { ShowDialogError("Repair History", ex); }
        }

        private static void ShowDialogError(string context, Exception ex)
        {
            try
            {
                var msg = $"{context} dialog failed:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YakultDialogErrors.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context} {ex}\r\n"); } catch { }
                WinForms.MessageBox.Show(GetOwner(), msg, "Dialog Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
            catch { }
        }

        private string OnRequestSaveFilePath(string defaultFileName)
        {
            using (var sfd = new WinForms.SaveFileDialog())
            {
                sfd.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                sfd.FileName = defaultFileName;
                sfd.Title = "Export Repair Items to CSV";

                return sfd.ShowDialog() == WinForms.DialogResult.OK ? sfd.FileName : null;
            }
        }
    }

    // ── Cell-color converters, mirroring Grid_CellFormatting's color rules ──────

    public sealed class RepairStatusToBrushConverter : IValueConverter
    {
        private static readonly Brush Repaired = new SolidColorBrush(Color.FromRgb(46, 204, 113));
        private static readonly Brush Unrepaired = new SolidColorBrush(Color.FromRgb(231, 76, 60));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? string.Empty;
            if (string.Equals(status, "Repaired", StringComparison.OrdinalIgnoreCase)) return Repaired;
            if (string.Equals(status, "Unrepaired", StringComparison.OrdinalIgnoreCase)) return Unrepaired;

            // NeedsRepairLabel values (Condition-based "Repair" column): "Needs Repair" reads as
            // the same alert red as "Unrepaired"; "Good" reads as the same green as "Repaired".
            // Any other raw Condition name (Spare, For Disposal, etc.) falls through to black.
            if (string.Equals(status, "Needs Repair", StringComparison.OrdinalIgnoreCase)) return Unrepaired;
            if (string.Equals(status, "Good", StringComparison.OrdinalIgnoreCase)) return Repaired;

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class SpareToBrushConverter : IValueConverter
    {
        private static readonly Brush SpareBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value as string, "Yes", StringComparison.OrdinalIgnoreCase) ? SpareBrush : Brushes.Black;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class LocationToBrushConverter : IValueConverter
    {
        private static readonly Brush Deployed = new SolidColorBrush(Color.FromRgb(142, 68, 173));
        private static readonly Brush InSet = new SolidColorBrush(Color.FromRgb(52, 73, 94));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var label = value as string ?? string.Empty;
            if (string.Equals(label, "Deployed", StringComparison.OrdinalIgnoreCase)) return Deployed;
            if (string.Equals(label, "In Set", StringComparison.OrdinalIgnoreCase)) return InSet;
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class OriginToBrushConverter : IValueConverter
    {
        private static readonly Brush Itcm = new SolidColorBrush(Color.FromRgb(41, 128, 185));
        private static readonly Brush Manual = new SolidColorBrush(Color.FromRgb(52, 73, 94));
        private static readonly Brush Mobile = new SolidColorBrush(Color.FromRgb(39, 174, 96));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(122, 138, 154));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var label = (value as string ?? string.Empty).Trim();
            if (label.Equals("ITCM", StringComparison.OrdinalIgnoreCase)) return Itcm;
            if (label.Equals("Manual", StringComparison.OrdinalIgnoreCase)) return Manual;
            if (label.Equals("Mobile", StringComparison.OrdinalIgnoreCase)) return Mobile;
            if (label.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return Muted;
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
