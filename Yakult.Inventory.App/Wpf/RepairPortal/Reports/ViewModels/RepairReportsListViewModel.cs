using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Reports.ViewModels
{
    /// <summary>Wraps a RepairTicketListItem with a checkbox-selection flag — the plain model has
    /// no INotifyPropertyChanged of its own, so the DataGrid's checkbox column needs this thin
    /// selectable wrapper to react live as the user ticks/unticks rows.</summary>
    public sealed class RepairReportRow : ViewModelBase
    {
        public RepairTicketListItem Ticket { get; }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public RepairReportRow(RepairTicketListItem ticket)
        {
            Ticket = ticket;
        }
    }

    /// <summary>Simple ticket picker for the Repair Report — reuses the same
    /// GetTicketsAsync/RepairTicketFilterCriteria infrastructure that powers Gallery/Table/Kanban,
    /// but with just a Search filter (no Company/Branch/Department/Priority) since its only job is
    /// finding tickets to view/print reports for, not full triage. Supports opening one ticket's
    /// report via its row, or checking several rows and opening them all via "View Reports".</summary>
    public sealed class RepairReportsListViewModel : ViewModelBase
    {
        private readonly IRepairTicketRepository _repository;

        public ObservableCollection<RepairReportRow> Tickets { get; } = new ObservableCollection<RepairReportRow>();

        private string _searchText = string.Empty;
        public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        // Guards against a report/picker flow being started twice from one click. RelayCommand's
        // Execute is effectively "async void" (assigned to a plain Action), so WPF's Command
        // infrastructure can re-enter it — e.g. a still-bubbling click on the "View Reports"/row
        // button gets redelivered once the nested ShowDialog() message pump starts — without this
        // guard that reopens the signatory picker and the attachments prompt a second time.
        private bool _isGeneratingReport;

        public event System.Action<string, string> RequestError;
        public event System.Action<string, string> RequestInfo;

        public RelayCommand SearchCommand { get; }
        public RelayCommand<RepairReportRow> ViewReportCommand { get; }
        public RelayCommand ViewSelectedReportsCommand { get; }

        public RepairReportsListViewModel() : this(new RepairTicketRepository())
        {
        }

        public RepairReportsListViewModel(IRepairTicketRepository repository)
        {
            _repository = repository ?? new RepairTicketRepository();

            SearchCommand = new RelayCommand(async () => await LoadAsync());
            ViewReportCommand = new RelayCommand<RepairReportRow>(async row => await ViewReportAsync(row?.Ticket));
            ViewSelectedReportsCommand = new RelayCommand(async () => await ViewSelectedReportsAsync());

            _ = LoadAsync();
        }

        private async Task ViewReportAsync(RepairTicketListItem ticket)
        {
            if (ticket == null || _isGeneratingReport) return;
            _isGeneratingReport = true;
            try
            {
                await RepairReportBuilder.ShowRepairReportAsync(ticket.RepairTicketId);
            }
            catch (System.Exception ex)
            {
                RequestError?.Invoke("Report Failed", "Failed to generate report: " + ex.Message);
            }
            finally
            {
                _isGeneratingReport = false;
            }
        }

        /// <summary>Opens one combined, requester-grouped report for every checked ticket — tickets
        /// sharing a requester merge into one copy; different requesters each get their own,
        /// page-broken apart. A single checked ticket is equivalent to that row's own "View
        /// Report".</summary>
        private async Task ViewSelectedReportsAsync()
        {
            if (_isGeneratingReport) return;

            var selectedIds = Tickets.Where(t => t.IsSelected).Select(t => t.Ticket.RepairTicketId).ToList();
            if (selectedIds.Count == 0)
            {
                RequestInfo?.Invoke("No Tickets Selected", "Tick the checkbox next to one or more tickets first.");
                return;
            }

            _isGeneratingReport = true;
            try
            {
                await RepairReportBuilder.ShowRepairReportBatchAsync(selectedIds);
            }
            catch (System.Exception ex)
            {
                RequestError?.Invoke("Report Failed", "Failed to generate reports: " + ex.Message);
            }
            finally
            {
                _isGeneratingReport = false;
            }
        }

        private async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                var criteria = new RepairTicketFilterCriteria { SearchText = SearchText };
                var results = await _repository.GetTicketsAsync(criteria);

                Tickets.Clear();
                foreach (var t in results)
                    Tickets.Add(new RepairReportRow(t));
            }
            catch (System.Exception ex)
            {
                RequestError?.Invoke("Load Failed", "Failed to load tickets: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
