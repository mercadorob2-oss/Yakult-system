using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Export.Sets.Views;

namespace Yakult.Inventory.App.Pages.Export
{
    public class ExportSets : UserControl
    {
        private ElementHost _host;
        private SetsExportView _view;

        public ExportSets()
        {
            Dock = DockStyle.Fill;

            _view = new SetsExportView();
            _view.BackRequested += () =>
            {
                var p = Parent;
                if (p != null) { p.Controls.Clear(); p.Controls.Add(new ReportPickerPage()); }
            };

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _host?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
