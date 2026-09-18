using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Vendor.Views;

namespace Yakult.Inventory.App.Pages.Vendor
{
    public class ViewVendorsPage : UserControl
    {
        private ElementHost _host;
        private VendorPageView _view;

        public ViewVendorsPage()
        {
            Dock = DockStyle.Fill;

            _view = new VendorPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

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
