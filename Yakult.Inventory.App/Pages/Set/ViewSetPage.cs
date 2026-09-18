using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Set.Views;

namespace Yakult.Inventory.App.Pages.Set
{
    public partial class ViewSetPage : UserControl
    {
        private ElementHost _host;
        private SetPageView _view;

        public ViewSetPage(bool restrictToConsumablesByDefault = false)
        {
            InitializeComponent();

            Dock = DockStyle.Fill;
            _view = new SetPageView(restrictToConsumablesByDefault);
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        public void ApplyInitialSearch(string query) => _view?.ApplyInitialSearch(query?.Trim());

        public void HighlightSet(int setId) => _view?.HighlightSet(setId);

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
