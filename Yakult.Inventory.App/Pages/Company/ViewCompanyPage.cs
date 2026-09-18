using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Company.Views;

namespace Yakult.Inventory.App.Pages.Company
{
    public partial class ViewCompanyPage : UserControl
    {
        private ElementHost _host;
        private CompanyPageView _view;

        public ViewCompanyPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;

            _view = new CompanyPageView();

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
