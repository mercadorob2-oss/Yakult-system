using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Views;

namespace Yakult.Inventory.App.Forms.Admin.AccountManagement
{
    public class EmployeeManagementWpfHost : UserControl
    {
        private ElementHost             _host;
        private EmployeeManagementView  _view;
        private bool                    _disposed;

        public EmployeeManagementWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new EmployeeManagementView();
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
