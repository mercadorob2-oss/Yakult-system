using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Invoice.Views;

namespace Yakult.Inventory.App.Pages.Invoice
{
    public class ViewNonLicensedInvoicesPage : UserControl
    {
        private ElementHost _host;
        private NonLicensedInvoiceView _view;

        public ViewNonLicensedInvoicesPage()
        {
            Dock = DockStyle.Fill;
            _view = new NonLicensedInvoiceView();
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
