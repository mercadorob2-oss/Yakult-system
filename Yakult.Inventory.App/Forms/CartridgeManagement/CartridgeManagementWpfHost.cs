using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.CartridgeManagement.Views;

namespace Yakult.Inventory.App.Forms.CartridgeManagement
{
    /// <summary>
    /// WinForms Form that hosts the WPF CartridgeManagementView via ElementHost.
    /// Drop-in replacement for CartridgeManagementForm — same Show/ShowDialog API.
    ///
    /// This thin wrapper handles:
    ///   - Creating the ElementHost + WPF UserControl
    ///   - Forwarding WinForms resize to the WPF layout engine
    ///   - Proper disposal of WPF resources on close
    /// </summary>
    public class CartridgeManagementWpfHost : Form
    {
        private ElementHost           _host;
        private CartridgeManagementView _view;

        public CartridgeManagementWpfHost()
        {
            Text             = "Cartridge Management — IT Fulfillment";
            MinimumSize      = new System.Drawing.Size(1200, 700);
            StartPosition    = FormStartPosition.CenterScreen;
            WindowState      = FormWindowState.Maximized;
            FormBorderStyle  = FormBorderStyle.Sizable;
            BackColor        = System.Drawing.Color.FromArgb(245, 247, 250);

            _view = new CartridgeManagementView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);

            FormClosed      += (s, e) => DisposeHost();
            HandleDestroyed += (s, e) => DisposeHost();
        }

        private bool _disposed;
        private void DisposeHost()
        {
            if (_disposed) return;
            _disposed = true;

            try { _host?.Dispose(); }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeHost();
            base.Dispose(disposing);
        }
    }
}
