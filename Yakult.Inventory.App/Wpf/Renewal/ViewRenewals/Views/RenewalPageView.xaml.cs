using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.WPF.Renewal.LinkRenewal.Views;
using Yakult.Inventory.App.WPF.Renewal.RenewalDetail.Views;
using Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.Views;
using Yakult.Inventory.App.WPF.Renewal.ViewRenewals.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Renewal.ViewRenewals.Views
{
    public partial class RenewalPageView : UserControl
    {
        private readonly RenewalPageViewModel _vm;

        public RenewalPageView()
        {
            InitializeComponent();

            _vm = new RenewalPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Information) == WinForms.DialogResult.Yes;
            _vm.RequestSaveFilePath = OnRequestSaveFilePath;
            _vm.RequestViewRenewalDetail += OnRequestViewRenewalDetail;
            _vm.RequestManageRenewalItems += OnRequestManageRenewalItems;
            _vm.RequestLinkExisting += OnRequestLinkExisting;
            _vm.RequestGenerateReport += (setIds, filter) =>
            {
                if (setIds != null) ReportLauncher.ShowRenewalsReport(setIds, filter);
                else ReportLauncher.ShowRenewalsReport();
            };

            RebuildSortByOptions();
            _vm.LoadRenewals();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        // A DataGridTemplateColumn CheckBox's TwoWay IsChecked binding does not reliably commit
        // back to the row when clicked (see RenewalPageView.xaml CellTemplate comment) — read the
        // toggled state explicitly here and write it back to the row manually.
        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is RenewalRow row)
                row.Selected = cb.IsChecked == true;

            // Belt-and-suspenders alongside the ViewModel's own Row_PropertyChanged subscription
            // — see RefreshSelectionState's doc comment.
            _vm.RefreshSelectionState();
        }

        // Purely cosmetic: closes the Export split-button popup after a menu item is clicked.
        // The bound Command still does the actual work — this just tidies the dropdown.
        // CloseManageDropdown is commented out along with the Manage dropdown itself in the XAML
        // (Manage Items is now its own primary button) — ManageDropdownToggle no longer exists.
        // private void CloseManageDropdown(object sender, RoutedEventArgs e) => ManageDropdownToggle.IsChecked = false;
        private void CloseExportDropdown(object sender, RoutedEventArgs e) => ExportDropdownToggle.IsChecked = false;

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestViewRenewalDetail(int setId)
        {
            try
            {
                var wpfWindow = new RenewalDetailWindow(setId);
                wpfWindow.ShowDialog();
                _vm.LoadRenewals();
            }
            catch (System.Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to open renewal details: {ex.Message}\n\n{ex.StackTrace}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private void MoreDocumentDates_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dlg = new Yakult.Inventory.App.WPF.Shared.Views.DocumentDateOverflowWindow(_vm.DocumentDateFilterRows, _vm.AddDocumentDateRowCommand);
            var owner = GetOwner();
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(dlg).Owner = owner.Handle;
            dlg.ShowDialog();
        }

        private void OnRequestManageRenewalItems(int setId)
        {
            try
            {
                var workspace = new RenewalWorkspaceWindow(setId);
                var owner = GetOwner();
                if (owner != null)
                    new System.Windows.Interop.WindowInteropHelper(workspace).Owner = owner.Handle;
                workspace.ShowDialog();
                _vm.LoadRenewals();
            }
            catch (System.Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to open renewal items: {ex.Message}\n\n{ex.StackTrace}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async void OnRequestLinkExisting(RenewalRow original)
        {
            if (original == null) return;

            System.Collections.Generic.List<Yakult.Inventory.App.Models.InvoiceSetPickerDto> available;
            try
            {
                available = await Task.Run(() => _vm.GetAvailableSetsForLinking(original.SetId));
            }
            catch (System.Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to load invoice sets:\n{ex.Message}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                return;
            }

            if (available.Count == 0)
            {
                WinForms.MessageBox.Show(GetOwner(),
                    "No invoice sets available to link. All eligible sets are already part of a renewal chain.",
                    "None Available", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            var wpfDlg = new LinkRenewalWindow(original.Dto, available);
            bool? linked = wpfDlg.ShowDialog();
            if (linked != true || !wpfDlg.SelectedSetId.HasValue) return;

            WinForms.MessageBox.Show(GetOwner(),
                "Successfully linked!\n\n" +
                $"Original:  {original.SetCode}  ({original.DocumentNumber ?? "—"})\n" +
                $"Renewal:   {wpfDlg.SelectedSetCode}\n\n" +
                "The original set's items have been marked as 'Renewed'.\n" +
                "Open either set's detail page to see the full renewal chain.",
                "Link Complete", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

            _vm.LoadRenewals();
        }

        private string OnRequestSaveFilePath(string defaultFileName)
        {
            using (var sfd = new WinForms.SaveFileDialog())
            {
                bool isPdf = defaultFileName.EndsWith(".pdf");
                sfd.Filter = isPdf ? "PDF (*.pdf)|*.pdf" : "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                sfd.FileName = defaultFileName;
                return sfd.ShowDialog(GetOwner()) == WinForms.DialogResult.OK ? sfd.FileName : null;
            }
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(RenewalsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(RenewalsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void RenewalsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key);

            foreach (var col in RenewalsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = _vm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;
        }

        private void DateColumnFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var columnName = button?.Tag as string;
            if (string.IsNullOrEmpty(columnName)) return;

            var current = _vm.GetDateFilter(columnName);
            var headerText = columnName == "StartDate" ? "Start Date" : "End Date";

            using (var popup = new DateRangeFilterPopup(headerText, current?.Item1, current?.Item2))
            {
                popup.Location = WinForms.Cursor.Position;
                popup.ShowDialog(GetOwner());

                switch (popup.Action)
                {
                    case DateRangeFilterPopup.PopupAction.SortAscending:
                        _vm.SortByColumn(columnName, System.ComponentModel.ListSortDirection.Ascending);
                        SyncSortGlyph(columnName);
                        break;
                    case DateRangeFilterPopup.PopupAction.SortDescending:
                        _vm.SortByColumn(columnName, System.ComponentModel.ListSortDirection.Descending);
                        SyncSortGlyph(columnName);
                        break;
                    case DateRangeFilterPopup.PopupAction.Filter:
                        _vm.SetDateFilter(columnName, popup.DateFrom, popup.DateTo);
                        break;
                    case DateRangeFilterPopup.PopupAction.Clear:
                        _vm.ClearDateFilter(columnName);
                        break;
                }
            }
        }

        private void SyncSortGlyph(string columnKey)
        {
            foreach (var col in RenewalsGrid.Columns) col.SortDirection = null;
            var target = RenewalsGrid.Columns.FirstOrDefault(c => ListSortHelper.SortKey(c) == columnKey);
            if (target != null)
                target.SortDirection = _vm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending;
        }

        private void SelectedBadge_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedRows().Select(r => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = r.SetId,
                Values = new[] { r.SetId.ToString(), r.SetCode, r.CompanyName, r.ExpiryStatus }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Renewals", "renewals",
                new[] { "ID", "Set Code", "Company", "Status" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            if (dialog.RemovedIds.Count > 0)
            {
                var removed = new System.Collections.Generic.HashSet<int>(dialog.RemovedIds);
                foreach (var row in _vm.GetSelectedRows())
                    if (removed.Contains(row.SetId))
                        row.Selected = false;
            }
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }
    }

    /// <summary>Maps SetLevelStatus text to its foreground color — same thresholds as the
    /// original DgvRenewals_CellFormatting.</summary>
    internal sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "Expired": return new SolidColorBrush(Color.FromRgb(231, 76, 60));
                case "Expiring Soon": return new SolidColorBrush(Color.FromRgb(230, 126, 34));
                case "Warning": return new SolidColorBrush(Color.FromRgb(183, 149, 11));
                case "Fully Renewed": return new SolidColorBrush(Color.FromRgb(41, 128, 185));
                case "Partially Renewed": return new SolidColorBrush(Color.FromRgb(155, 89, 182));
                case "Active": return new SolidColorBrush(Color.FromRgb(46, 204, 113));
                default: return new SolidColorBrush(Color.FromRgb(26, 35, 51));
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }

    /// <summary>Bold for Expired/Expiring Soon/Fully Renewed/Partially Renewed, matching the original.</summary>
    internal sealed class StatusToWeightConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "Expired":
                case "Expiring Soon":
                case "Fully Renewed":
                case "Partially Renewed":
                    return FontWeights.Bold;
                default:
                    return FontWeights.Normal;
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }

    /// <summary>Maps DaysUntilExpiry (nullable int) to a foreground color — same thresholds as the
    /// original DgvRenewals_CellFormatting (no special coloring when null, unlike the Invoice page).</summary>
    internal sealed class DaysUntilExpiryToBrushConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int days)) return new SolidColorBrush(Color.FromRgb(26, 35, 51));
            if (days < 0) return new SolidColorBrush(Color.FromRgb(192, 57, 43));
            if (days <= 30) return new SolidColorBrush(Color.FromRgb(211, 84, 0));
            if (days <= 90) return new SolidColorBrush(Color.FromRgb(183, 149, 11));
            return new SolidColorBrush(Color.FromRgb(26, 35, 51));
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }

    internal sealed class DaysUntilExpiryToWeightConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int days && days <= 30) return FontWeights.Bold;
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }
}
