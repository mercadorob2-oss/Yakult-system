using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Employee.Views;

namespace Yakult.Inventory.App.Pages.Employee
{
    public partial class ViewEmployeePage : UserControl
    {
        private ElementHost _host;
        private EmployeePageView _view;

        public ViewEmployeePage()
        {
            InitializeComponent();

            Dock = DockStyle.Fill;

            _view = new EmployeePageView();

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
