using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Admin.UserActivityInsights.Views;

namespace Yakult.Inventory.App.Forms.Admin
{
    public class UserActivityInsightsWpfHost : UserControl
    {
        private ElementHost _host;
        private UserActivityInsightsView _view;
        private bool _disposed;

        public UserActivityInsightsWpfHost(UserActivityFilter initialFilter)
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 247, 250);

            _view = new UserActivityInsightsView(initialFilter);
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
