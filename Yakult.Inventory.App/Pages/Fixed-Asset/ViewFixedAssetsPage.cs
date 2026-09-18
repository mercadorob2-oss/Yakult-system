using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.FixedAssets.Views;

namespace Yakult.Inventory.App.Pages.FixedAsset
{
    public partial class ViewFixedAssetsPage : UserControl
    {
        private ElementHost _host;
        private FixedAssetsPageView _view;

        public ViewFixedAssetsPage()
        {
            InitializeComponent();

            _view = new FixedAssetsPageView();

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
