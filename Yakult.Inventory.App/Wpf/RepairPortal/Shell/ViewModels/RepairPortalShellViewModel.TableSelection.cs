using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels
{
    /// <summary>Table View's checkbox multi-select state, surfaced here so the Open/Delete actions
    /// can sit in the Shell's fixed summary-cards row instead of a bar that appears/disappears
    /// directly above the DataGrid (which shifted the table up and down on every selection change).
    /// RepairTicketTableView's code-behind pushes its selection here via SetTableSelection; the
    /// actual Open/Delete work still goes through the same OpenTicketCommand/DeleteTicketsCommand
    /// Gallery/Kanban already use — these are just parameterless wrappers for the summary row's
    /// buttons to bind to.</summary>
    public sealed partial class RepairPortalShellViewModel
    {
        private List<RepairTicketListItem> _tableSelectedTickets = new List<RepairTicketListItem>();

        private int _tableSelectedCount;
        public int TableSelectedCount { get => _tableSelectedCount; private set => SetField(ref _tableSelectedCount, value); }

        public bool TableHasSelection => TableSelectedCount > 0;

        /// <summary>Only true while Table View is the active view AND at least one row is checked —
        /// Gallery/Kanban have their own card-level actions and never show these buttons.</summary>
        public bool ShowTableSelectionActions => IsTableMode && TableHasSelection;

        private string _tableSelectionCountText = string.Empty;
        public string TableSelectionCountText { get => _tableSelectionCountText; private set => SetField(ref _tableSelectionCountText, value); }

        public RelayCommand TableOpenCommand { get; private set; }
        public RelayCommand TableDeleteCommand { get; private set; }
        public RelayCommand ReviewTableSelectionCommand { get; private set; }

        /// <summary>RepairTicketTableView subscribes to this to open the review-and-unselect popup
        /// (RepairTicketSelectionReviewDialog) — the checkbox state itself lives there, not here
        /// (see SetTableSelection's summary), so this VM only asks for it to be shown.</summary>
        public event System.Action TableSelectionReviewRequested;

        private void InitTableSelection()
        {
            ReviewTableSelectionCommand = new RelayCommand(() => TableSelectionReviewRequested?.Invoke());

            TableOpenCommand = new RelayCommand(() =>
            {
                if (_tableSelectedTickets.Count > 1)
                {
                    RequestInfo?.Invoke("Select One Ticket", "Open only works on a single ticket at a time. Uncheck rows until just one remains, then try again.");
                    return;
                }

                var ticket = _tableSelectedTickets.FirstOrDefault();
                if (ticket != null && OpenTicketCommand.CanExecute(ticket))
                    OpenTicketCommand.Execute(ticket);
            });

            TableDeleteCommand = new RelayCommand(() =>
            {
                if (_tableSelectedTickets.Count == 0) return;
                if (DeleteTicketsCommand.CanExecute(_tableSelectedTickets))
                    DeleteTicketsCommand.Execute(_tableSelectedTickets);
            });
        }

        /// <summary>Called by RepairTicketTableView whenever its checkbox selection changes (ticked,
        /// unticked, page change, or the underlying ticket list changes).</summary>
        public void SetTableSelection(List<RepairTicketListItem> tickets)
        {
            _tableSelectedTickets = tickets ?? new List<RepairTicketListItem>();
            TableSelectedCount = _tableSelectedTickets.Count;
            TableSelectionCountText = TableSelectedCount == 1 ? "1 selected" : $"{TableSelectedCount} selected";
            OnPropertyChanged(nameof(TableHasSelection));
            OnPropertyChanged(nameof(ShowTableSelectionActions));
        }
    }
}
