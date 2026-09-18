using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace Yakult.Inventory.App.Pages.Reports
{
    public partial class EDocumentsPage : UserControl
    {
        private ElementHost _host;
        private WPF.EDocs.Views.EDocsDashboardView _view;

        public EDocumentsPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;
            _view = new WPF.EDocs.Views.EDocsDashboardView();
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _host?.Dispose(); } catch { }
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
