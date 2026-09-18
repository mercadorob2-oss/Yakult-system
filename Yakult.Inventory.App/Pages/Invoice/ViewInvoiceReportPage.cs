using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Invoice.Views;

namespace Yakult.Inventory.App.Pages.Invoice
{
    public class ViewInvoiceReportPage : UserControl
    {
        private ElementHost _host;
        private InvoicePageView _view;

        public ViewInvoiceReportPage()
        {
            Dock = DockStyle.Fill;
            _view = new InvoicePageView();
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        public void ApplyInitialSearch(string query) => _view?.ApplyInitialSearch(query?.Trim());

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
