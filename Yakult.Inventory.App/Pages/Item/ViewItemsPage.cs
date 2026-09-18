using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Items.Views;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class ViewItemsPage : UserControl
    {
        private ElementHost   _host;
        private ItemsPageView _view;

        public ViewItemsPage()
        {
            InitializeComponent();

            _view = new ItemsPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        /// <summary>
        /// Pre-fills the search box and applies the filter.
        /// Called from the home page search to land the user on this page with results pre-filtered.
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
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
