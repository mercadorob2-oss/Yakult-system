using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Dashboard.Views;

namespace Yakult.Inventory.App.Forms.Dashboard
{
    /// <summary>
    /// WinForms UserControl that hosts the WPF AnalyticsDashboardView via ElementHost.
    /// Keeps the same public surface as the legacy WinForms DashboardView so the
    /// MainForm wiring stays unchanged.
    /// </summary>
    public class DashboardWpfHost : UserControl
    {
        private readonly ElementHost _host;
        private readonly AnalyticsDashboardView _view;
        private bool _disposed;

        public DashboardWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(246, 245, 242);

            _view = new AnalyticsDashboardView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);

            HandleDestroyed += (s, e) => DisposeHost();
        }

        public Action OnViewInventoryClicked
        {
            get => _view.OnViewInventoryClicked;
            set => _view.OnViewInventoryClicked = value;
        }

        public Action OnViewUpdatesClicked
        {
            get => _view.OnViewUpdatesClicked;
            set => _view.OnViewUpdatesClicked = value;
        }

        public Action OnRefreshClicked
        {
            get => _view.OnRefreshClicked;
            set => _view.OnRefreshClicked = value;
        }

        public void LoadDashboard(DashboardScope scope, DashboardFilter filter = null)
            => _view.LoadDashboard(scope, filter);

        public void SetMobileUpdatesPendingCount(int pendingCount)
            => _view.SetMobileUpdatesPendingCount(pendingCount);

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
