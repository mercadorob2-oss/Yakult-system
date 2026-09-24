using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.RequestManagement.Views;

namespace Yakult.Inventory.App.Forms.CartridgeManagement
{
    /// <summary>
    /// WinForms UserControl that hosts the Mixed Request Exchange page (MixedRequestExchangeView)
    /// via ElementHost. Shared by the Cartridge Management and Request &amp; Set Management portals.
    /// </summary>
    public class MixedRequestExchangeWpfHost : UserControl
    {
        private ElementHost              _host;
        private MixedRequestExchangeView _view;
        private bool                     _disposed;

        public MixedRequestExchangeWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new MixedRequestExchangeView();

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
