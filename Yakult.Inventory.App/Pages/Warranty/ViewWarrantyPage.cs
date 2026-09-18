using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Warranty.Views;

namespace Yakult.Inventory.App.Pages.Warranty
{
    public partial class ViewWarrantyPage : UserControl
    {
        private ElementHost _host;
        private WarrantyPageView _view;

        public ViewWarrantyPage()
        {
            InitializeComponent();

            _view = new WarrantyPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Name = "ViewWarrantyPage";
            Size = new Size(1200, 800);
            BackColor = Color.White;
            ResumeLayout(false);
        }

        /// <summary>
        /// Pre-fills the search box and applies the filter.
        /// Called from the home page search to land the user on this page with results pre-filtered.
        /// </summary>
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
            }
            base.Dispose(disposing);
        }
    }
}
