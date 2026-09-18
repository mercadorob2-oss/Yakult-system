using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Asset.Views;

namespace Yakult.Inventory.App.Pages.Asset
{
    public class ViewAssetPage : UserControl
    {
        private ElementHost _host;
        private AssetPageView _view;

        public ViewAssetPage()
        {
            Dock = DockStyle.Fill;

            _view = new AssetPageView();

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
