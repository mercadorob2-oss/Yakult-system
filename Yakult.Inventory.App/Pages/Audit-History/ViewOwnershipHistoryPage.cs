using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Wpf.OwnershipHistory.Views;

namespace Yakult.Inventory.App.Pages.AuditHistory
{
    public partial class ViewOwnershipHistoryPage : UserControl
    {
        private ElementHost _host;
        private OwnershipHistoryPageView _view;

        public ViewOwnershipHistoryPage(bool autoLoad = true)
        {
            InitializeComponent();

            _view = new OwnershipHistoryPageView(autoLoad);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
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
