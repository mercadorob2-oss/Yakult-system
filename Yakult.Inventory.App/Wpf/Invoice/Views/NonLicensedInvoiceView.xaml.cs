using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.Invoice.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Invoice.Views
{
    public partial class NonLicensedInvoiceView : UserControl
    {
        private readonly NonLicensedInvoiceViewModel _vm;

        public NonLicensedInvoiceView()
        {
            InitializeComponent();

            _vm = new NonLicensedInvoiceViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestPreviewInvoice += OnRequestPreviewInvoice;

            Loaded += async (s, e) => await _vm.LoadAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestPreviewInvoice(NonLicensedInvoiceRow row)
        {
            if (row == null || row.SetId <= 0) return;
            using (var detail = new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceDetailPage(row.SetId))
            {
                detail.ShowDialog(GetOwner());
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            var row = ResultsGrid.SelectedItems.Cast<NonLicensedInvoiceRow>().FirstOrDefault();
            if (row == null)
            {
                WinForms.MessageBox.Show(GetOwner(), "Select an item first to preview its invoice.", "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }
            _vm.PreviewInvoice(row);
        }
    }
}
