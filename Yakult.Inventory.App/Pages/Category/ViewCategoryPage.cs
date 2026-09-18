using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Category.Views;

namespace Yakult.Inventory.App.Pages.Category
{
    public partial class ViewCategoryPage : UserControl
    {
        private ElementHost _host;
        private CategoryPageView _view;

        public ViewCategoryPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;

            _view = new CategoryPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            // Designer.cs (kept untouched) already provides a Dispose(bool) override that
            // disposes everything registered in `components` — register the host there
            // instead of adding a second Dispose(bool) override in this file.
            components.Add(_host);

            Controls.Add(_host);
        }
    }
}
