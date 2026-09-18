using System;
using System.Windows;
using Yakult.Inventory.App.WPF.InvoicePreparation.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.InvoicePreparation.Views
{
    /// <summary>Read-only "Preview Contents" dialog for one Sub-Type Group — header
    /// fields and its member items. No editing here; membership and category are set
    /// from the Items Page.</summary>
    public partial class InvoicePreparationDetailWindow : Window, IDisposable
    {
        private readonly InvoicePreparationDetailViewModel _vm;

        public InvoicePreparationDetailWindow(int preparationId)
        {
            InitializeComponent();

            _vm = new InvoicePreparationDetailViewModel(preparationId);
            DataContext = _vm;

            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);

            Loaded += (s, e) => _vm.Load();
        }

        // WinForms-compatible ShowDialog overloads (same shape as SoftwareServiceSetDialog).
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return result == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public void Dispose() { }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
