using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.DepartmentRequestHistory.Views;

namespace Yakult.Inventory.App.Forms.Admin.AccountManagement
{
    /// <summary>Admin Portal → Account Management → Department Request History.</summary>
    public class DepartmentRequestHistoryWpfHost : UserControl
    {
        private ElementHost                 _host;
        private DepartmentRequestHistoryView _view;
        private bool                        _disposed;

        public DepartmentRequestHistoryWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new DepartmentRequestHistoryView();
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
