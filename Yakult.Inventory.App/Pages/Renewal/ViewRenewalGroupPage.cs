using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Renewal.RenewalGroups.Views;

namespace Yakult.Inventory.App.Pages.Renewal
{
    public partial class ViewRenewalGroupPage : UserControl
    {
        private ElementHost _host;
        private RenewalGroupPageView _view;

        public ViewRenewalGroupPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;
            _view = new RenewalGroupPageView();
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
