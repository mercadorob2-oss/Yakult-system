using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.Monitoring.Views;

namespace Yakult.Inventory.App.Forms.Admin.Monitoring
{
    /// <summary>Hosts the WPF Connected Devices page (Admin Portal).</summary>
    public class ConnectedDevicesWpfHost : UserControl
    {
        private ElementHost _host;
        private ConnectedDevicesView _view;
        private bool _disposed;

        public ConnectedDevicesWpfHost()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new ConnectedDevicesView();
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };

            Controls.Add(_host);
            HandleDestroyed += (s, e) => DisposeHost();
        }

        private void DisposeHost()
        {
            if (_disposed) return;
            _disposed = true;
            try { _host?.Dispose(); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeHost();
            base.Dispose(disposing);
        }
    }
}
