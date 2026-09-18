using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.Views;

namespace Yakult.Inventory.App.Forms.Admin.EmailManagement
{
    /// <summary>
    /// Hosts the WPF Email Logs view inside the WinForms admin portal content panel.
    /// </summary>
    public class EmailLogsWpfHost : UserControl
    {
        private ElementHost _host;
        private EmailLogsView _view;
        private bool _disposed;

        public EmailLogsWpfHost()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new EmailLogsView();
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
