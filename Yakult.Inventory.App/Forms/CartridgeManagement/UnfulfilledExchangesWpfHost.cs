using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.CartridgeManagement.Views;

namespace Yakult.Inventory.App.Forms.CartridgeManagement
{
    /// <summary>
    /// WinForms UserControl that hosts the WPF UnfulfilledExchangesView via ElementHost.
    /// Drop-in replacement for UnfulfilledCartridgeExchangesPage.
    /// </summary>
    public class UnfulfilledExchangesWpfHost : UserControl
    {
        private ElementHost              _host;
        private UnfulfilledExchangesView _view;
        private bool                     _disposed;

        public UnfulfilledExchangesWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new UnfulfilledExchangesView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

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
