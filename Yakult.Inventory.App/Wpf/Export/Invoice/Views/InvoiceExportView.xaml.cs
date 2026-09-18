using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Pages.Export;
using Yakult.Inventory.App.WPF.Export.Invoice.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Export.Invoice.Views
{
    public partial class InvoiceExportView : UserControl
    {
        private readonly InvoiceExportViewModel _vm;

        public event System.Action BackRequested;

        public InvoiceExportView()
        {
            InitializeComponent();

            _vm = new InvoiceExportViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestExport += OnRequestExport;
            _vm.RequestBack += () => BackRequested?.Invoke();

            _ = _vm.LoadAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestExport(List<DataRow> headers, DataTable items)
        {
            using (var dlg = new ExportInvoicePreviewDialog(headers, items))
                dlg.ShowDialog(GetOwner());
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelection(newState);
        }

        private void RowCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _vm.NotifySelectionChanged();
        }

        private void InvoicesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(key)) return;

            var direction = _vm.SortByColumn(key);

            foreach (var col in InvoicesGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }
}
