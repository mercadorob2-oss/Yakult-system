using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Inventory.Views;

namespace Yakult.Inventory.App.Pages.Inventory
{
    public partial class ViewInventoryPage : UserControl
    {
        private ElementHost _host;
        private InventoryPageView _view;

        public ViewInventoryPage()
        {
            InitializeComponent();

            _view = new InventoryPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        /// <summary>
        /// Pre-fills the search box, switches to the Inventory tab, and applies the filter.
        /// Called from the home page search to land the user on the right page with results pre-filtered.
        /// </summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            _view?.ApplyInitialSearch(query.Trim());
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
