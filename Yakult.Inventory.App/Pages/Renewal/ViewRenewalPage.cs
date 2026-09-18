using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Renewal.ViewRenewals.Views;

namespace Yakult.Inventory.App.Pages.Renewal
{
    public partial class ViewRenewalPage : UserControl
    {
        private ElementHost _host;
        private RenewalPageView _view;

        public ViewRenewalPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;
            _view = new RenewalPageView();
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        public void ApplyInitialSearch(string query) => _view?.ApplyInitialSearch(query?.Trim());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _host?.Dispose(); } catch { }
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
