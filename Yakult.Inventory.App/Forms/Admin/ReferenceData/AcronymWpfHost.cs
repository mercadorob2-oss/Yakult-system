using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels;
using Yakult.Inventory.App.WPF.Admin.ReferenceData.Views;

namespace Yakult.Inventory.App.Forms.Admin.ReferenceData
{
    /// <summary>Hosts the WPF Branch Acronyms / Dept Acronyms page (Admin Portal > Reference Data).</summary>
    public class AcronymWpfHost : UserControl
    {
        private ElementHost     _host;
        private AcronymListView _view;
        private bool            _disposed;

        public AcronymWpfHost(AcronymKind kind)
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new AcronymListView(kind);
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
