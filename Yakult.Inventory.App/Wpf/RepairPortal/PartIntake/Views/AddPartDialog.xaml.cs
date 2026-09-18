using System;
using System.Windows;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.RepairPortal.PartIntake.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.RepairPortal.PartIntake.Views
{
    public partial class AddPartDialog : Window
    {
        private readonly AddPartViewModel _vm;

        public event Action<RepairPart> PartCreated;

        public AddPartDialog(int repairTicketId, IRepairTicketRepository repository)
        {
            InitializeComponent();

            _vm = new AddPartViewModel(repairTicketId, repository);
            DataContext = _vm;

            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.PartCreated += part => PartCreated?.Invoke(part);
            _vm.RequestClose += result => { DialogResult = result; Close(); };
        }
    }
}
