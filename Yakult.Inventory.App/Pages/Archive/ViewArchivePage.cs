using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Archive.Views;

namespace Yakult.Inventory.App.Pages.Archive
{
    public partial class ViewArchivePage : UserControl
    {
        private ElementHost     _host;
        private ArchivePageView _view;

        public ViewArchivePage()
        {
            InitializeComponent();

            _view = new ArchivePageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            _view?.ApplyInitialSearch(query.Trim());
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
