using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.InvoicePreparation.Views;

namespace Yakult.Inventory.App.Pages.InvoicePreparation
{
    public class ViewInvoicePreparationPage : UserControl
    {
        private ElementHost _host;
        private InvoicePreparationListView _view;

        public ViewInvoicePreparationPage()
        {
            Dock = DockStyle.Fill;
            _view = new InvoicePreparationListView();
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _host?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
