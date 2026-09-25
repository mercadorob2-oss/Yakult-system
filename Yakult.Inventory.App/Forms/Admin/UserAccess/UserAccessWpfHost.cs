using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.UserAccess.Views;

namespace Yakult.Inventory.App.Forms.Admin.UserAccess
{
    /// <summary>Hosts the WPF User Access page (Admin Portal > Developer Tools > User Portal Access).</summary>
    public class UserAccessWpfHost : UserControl
    {
        private ElementHost    _host;
        private UserAccessView _view;
        private bool           _disposed;

        public UserAccessWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new UserAccessView();
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
