using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.ReferenceData.Views;

namespace Yakult.Inventory.App.Forms.Admin.ReferenceData
{
    /// <summary>Hosts the WPF Company Holidays page (Admin Portal > Reference Data > Holidays).</summary>
    public class HolidayManagementWpfHost : UserControl
    {
        private ElementHost           _host;
        private HolidayManagementView _view;
        private bool                  _disposed;

        public HolidayManagementWpfHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 247, 250);

            _view = new HolidayManagementView();
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
