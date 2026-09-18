using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Invoice;
using Yakult.Inventory.App.Pages.Software;
using Yakult.Inventory.App.WPF.Invoice.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Invoice.Views
{
    public partial class InvoicePageView : UserControl
    {
        private readonly InvoicePageViewModel _vm;

        public InvoicePageView()
        {
            InitializeComponent();

            _vm = new InvoicePageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
            _vm.RequestOpenInvoice += OnRequestOpenInvoice;
            _vm.RequestEditInvoice += OnRequestEditInvoice;
            _vm.RequestDeleteInvoices += OnRequestDeleteInvoices;
            _vm.RequestGenerateReport += codes => { if (codes != null) ReportLauncher.ShowInvoiceReport(codes); else ReportLauncher.ShowInvoiceReport(); };
            _vm.RequestGenerateSheet += codes => { using (var form = new InvoiceSheetForm(codes)) form.ShowDialog(); };

            RebuildSortByOptions();
            _vm.LoadInvoices();
        }

        // A DataGridTemplateColumn CheckBox's TwoWay IsChecked binding does not reliably commit
        // back to the row when clicked (see InvoicePageView.xaml CellTemplate comment) — read the
        // toggled state explicitly here and write it back to the row manually.
        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is InvoiceRow row)
                row.Selected = cb.IsChecked == true;
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void MoreDocumentDates_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dlg = new Yakult.Inventory.App.WPF.Shared.Views.DocumentDateOverflowWindow(_vm.DocumentDateFilterRows, _vm.AddDocumentDateRowCommand);
            var owner = GetOwner();
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(dlg).Owner = owner.Handle;
            dlg.ShowDialog();
        }

        private void OnRequestOpenInvoice(InvoiceRow row)
        {
            if (row == null) return;
            using (var detail = new ViewInvoiceDetailPage(row.SetId))
            {
                detail.ShowDialog(GetOwner());
            }
        }

        private void OnRequestEditInvoice(InvoiceRow row)
        {
            if (row == null) return;
            using (var dlg = new SoftwareServiceSetDialog(row.SetId))
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.LoadInvoices();
            }
        }

        private void OnRequestDeleteInvoices(List<InvoiceRow> rows)
        {
            var labels = rows.Select(r =>
                $"{r.DocumentNumber ?? r.SetCode} — {r.Company} (Ref: {r.ReferenceNumber})").ToList();

            using (var dlg = new DeleteInvoicesDialog(labels))
            {
                if (dlg.ShowDialog(GetOwner()) != WinForms.DialogResult.OK) return;
                _vm.CommitDeleteInvoices(rows, dlg.PermanentlyDelete, dlg.Reason);
            }
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(InvoicesGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(InvoicesGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void InvoicesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key);

            foreach (var col in InvoicesGrid.Columns) col.SortDirection = null;
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
            foreach (var col in InvoicesGrid.Columns) col.SortDirection = null;
            var target = System.Linq.Enumerable.FirstOrDefault(InvoicesGrid.Columns, c => ListSortHelper.SortKey(c) == columnKey);
            if (target != null)
                target.SortDirection = _vm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending;
        }
    }

    /// <summary>Maps DaysLeft (nullable int) to a bold text color — same semantic thresholds as the
    /// original DgvInvoices_CellFormatting pill colors, matching this page's legend row.</summary>
    internal sealed class DaysLeftToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int daysLeft))
                return new SolidColorBrush(Color.FromRgb(120, 120, 120));   // N/A — gray

            if (daysLeft < 0) return new SolidColorBrush(Color.FromRgb(192, 57, 43));    // Expired — red
            if (daysLeft <= 30) return new SolidColorBrush(Color.FromRgb(211, 84, 0));    // Critical — dark orange
            if (daysLeft <= 90) return new SolidColorBrush(Color.FromRgb(183, 149, 11));  // Warning — amber
            return new SolidColorBrush(Color.FromRgb(39, 174, 96));                       // Active — green
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
