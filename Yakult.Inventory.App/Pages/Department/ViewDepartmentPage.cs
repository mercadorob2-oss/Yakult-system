using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Department.Views;

namespace Yakult.Inventory.App.Pages.Department
{
    public partial class ViewDepartmentPage : UserControl
    {
        private ElementHost _host;
        private DepartmentPageView _view;

        public ViewDepartmentPage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;

            _view = new DepartmentPageView();

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
