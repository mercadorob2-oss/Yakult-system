using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Branch.Views;

namespace Yakult.Inventory.App.Pages.Branch
{
    public partial class ViewBranchPage : UserControl
    {
        private ElementHost _host;
        private BranchPageView _view;

        public ViewBranchPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;

            _view = new BranchPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            components.Add(_host);

            Controls.Add(_host);
        }
    }
}
