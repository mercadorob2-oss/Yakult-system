using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.ItemMovementAudit.Views;

namespace Yakult.Inventory.App.Pages.ItemAudit
{
    public partial class ViewItemMovementAuditPage : UserControl
    {
        private ElementHost _host;
        private ItemMovementAuditPageView _view;

        public ViewItemMovementAuditPage(bool autoLoad = true)
        {
            InitializeComponent();

            _view = new ItemMovementAuditPageView(autoLoad);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        public async System.Threading.Tasks.Task LoadTimelineForSerialAsync(string serial)
        {
            if (_view != null)
                await _view.LoadTimelineForSerialAsync(serial);
        }

        public void NotifyClosed()
        {
            _view?.NotifyClosed();
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
