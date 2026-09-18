using System.Collections.ObjectModel;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Attendance.ViewModels
{
    /// <summary>Backs RepairAttendanceHistoryWindow — the full Time In/Out session history for the
    /// currently logged-in technician, most recent first. Read-only; loads once on construction.</summary>
    public sealed class RepairAttendanceHistoryViewModel : ViewModelBase
    {
        private readonly IRepairTicketRepository _repository;

        public ObservableCollection<RepairTechnicianAttendanceStatus> Sessions { get; } = new ObservableCollection<RepairTechnicianAttendanceStatus>();

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        private bool _isEmpty;
        public bool IsEmpty { get => _isEmpty; private set => SetField(ref _isEmpty, value); }

        public event System.Action<string, string> RequestError;

        public RelayCommand RefreshCommand { get; }

        public RepairAttendanceHistoryViewModel(IRepairTicketRepository repository)
        {
            _repository = repository ?? new RepairTicketRepository();

            RefreshCommand = new RelayCommand(async () => await LoadAsync());

            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (!AppSession.CurrentEmployeeId.HasValue || AppSession.CurrentEmployeeId.Value <= 0)
            {
                IsEmpty = true;
                return;
            }

            IsBusy = true;
            try
            {
                var history = await _repository.GetAttendanceHistoryAsync(AppSession.CurrentEmployeeId.Value);
                Sessions.Clear();
                foreach (var s in history)
                    Sessions.Add(s);
                IsEmpty = Sessions.Count == 0;
            }
            catch (System.Exception ex)
            {
                RequestError?.Invoke("Load Failed", "Failed to load Time In/Out history: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
