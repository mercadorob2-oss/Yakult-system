using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Kanban.Views
{
    /// <summary>
    /// Six static columns (Waiting/Diagnosing/Repairing/Testing/Completed/Unrepairable), each an
    /// ItemsControl filtered client-side from the Shell's shared Tickets collection by Status.
    /// Structural placeholder only — no drag-and-drop between columns (explicitly out of scope).
    /// </summary>
    public partial class RepairTicketKanbanView : UserControl
    {
        public RepairTicketKanbanView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => BindColumns();
        }

        private void BindColumns()
        {
            if (!(DataContext is RepairPortalShellViewModel vm)) return;

            BindColumn(WaitingItems, vm, "Waiting");
            BindColumn(DiagnosingItems, vm, "Diagnosing");
            BindColumn(RepairingItems, vm, "Repairing");
            BindColumn(TestingItems, vm, "Testing");
            BindColumn(CompletedItems, vm, "Completed");
            BindColumn(UnrepairableItems, vm, "Unrepairable");
        }

        private static void BindColumn(ItemsControl itemsControl, RepairPortalShellViewModel vm, string status)
        {
            var view = new CollectionViewSource { Source = vm.Tickets }.View;
            view.Filter = obj => obj is RepairTicketListItem t && string.Equals(t.Status, status, System.StringComparison.OrdinalIgnoreCase);
            itemsControl.ItemsSource = view;
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is RepairTicketListItem ticket
                && DataContext is RepairPortalShellViewModel vm
                && vm.OpenTicketCommand.CanExecute(ticket))
            {
                vm.OpenTicketCommand.Execute(ticket);
            }
        }
    }
}
