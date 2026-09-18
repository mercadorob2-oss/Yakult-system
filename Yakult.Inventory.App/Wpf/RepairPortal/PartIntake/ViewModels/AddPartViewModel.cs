using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.PartIntake.ViewModels
{
    /// <summary>Backs AddPartDialog: mirrors NewRepairTicketViewModel's shape. A new Part always
    /// starts at Status = WaitingDiagnosis server-side (sp_RepairPortal_CreatePart does not accept
    /// an initial status) — the dialog shows this as read-only context rather than an editable
    /// combo.</summary>
    public sealed class AddPartViewModel : ViewModelBase
    {
        private readonly IRepairTicketRepository _repository;
        public int RepairTicketId { get; }

        public ObservableCollection<string> SeverityOptions { get; } = new ObservableCollection<string> { "Low", "Medium", "High", "Critical" };

        public event Action<string, string> RequestWarning;
        public event Action<RepairPart> PartCreated;
        public event Action<bool> RequestClose;

        private string _customLabel = string.Empty;
        public string CustomLabel { get => _customLabel; set => SetField(ref _customLabel, value); }

        private string _problemDescription = string.Empty;
        public string ProblemDescription { get => _problemDescription; set => SetField(ref _problemDescription, value); }

        private string _selectedSeverity = "Medium";
        public string SelectedSeverity { get => _selectedSeverity; set => SetField(ref _selectedSeverity, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public AddPartViewModel(int repairTicketId, IRepairTicketRepository repository)
        {
            RepairTicketId = repairTicketId;
            _repository = repository ?? new RepairTicketRepository();

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        private async Task SaveAsync()
        {
            IsBusy = true;
            try
            {
                var request = new NewPartRequest
                {
                    RepairTicketId = RepairTicketId,
                    CustomLabel = string.IsNullOrWhiteSpace(CustomLabel) ? null : CustomLabel.Trim(),
                    ProblemDescription = string.IsNullOrWhiteSpace(ProblemDescription) ? null : ProblemDescription.Trim(),
                    Severity = SelectedSeverity
                };

                var created = await _repository.CreatePartAsync(request, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null);
                PartCreated?.Invoke(created);
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                RequestWarning?.Invoke("Save Failed", "Failed to create part: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
