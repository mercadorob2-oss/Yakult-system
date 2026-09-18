using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Table.Views
{
    /// <summary>Wraps a RepairTicketListItem with a checkbox-selection flag — the plain model has
    /// no INotifyPropertyChanged of its own, so the DataGrid's checkbox column needs this thin
    /// selectable wrapper to react live as the user ticks/unticks rows. Same pattern as
    /// RepairReportRow (Wpf\RepairPortal\Reports\ViewModels), kept Table-view-local so this
    /// selection state never leaks into the shared Tickets collection Gallery/Kanban also bind to.</summary>
    public sealed class RepairTicketTableRow : ViewModelBase
    {
        public RepairTicketListItem Ticket { get; }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public RepairTicketTableRow(RepairTicketListItem ticket)
        {
            Ticket = ticket;
        }
    }
}
