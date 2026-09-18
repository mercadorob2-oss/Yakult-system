using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.ConsumableManagement.Views;

namespace Yakult.Inventory.App.Forms.Admin
{
    /// <summary>
    /// WinForms host for the WPF <see cref="ConsumableStockMonitorView"/>, shown as a page
    /// in the Admin Portal side menu.
    /// </summary>
    public class ConsumableStockMonitorWpfHost : UserControl
    {
        private ElementHost                _host;
        private ConsumableStockMonitorView _view;
        private bool                       _disposed;

        public ConsumableStockMonitorWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new ConsumableStockMonitorView();
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
