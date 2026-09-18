using System;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels
{
    /// <summary>Per-observation card state for the Reported Problem section: inline edit
    /// (Save/Cancel), Delete, and Move Up/Down. Mutating commands are bridged back to the parent
    /// RepairTicketDetailViewModel via delegate callbacks (set by the parent after construction),
    /// matching the "delegate properties instead of events" pattern used elsewhere in this
    /// codebase — safe when the parent recreates/replaces the collection on reload.</summary>
    public sealed class ObservationCardViewModel : ViewModelBase
    {
        public RepairItemObservation Model { get; }

        public int ObservationId => Model.ObservationId;
        public int SortOrder => Model.SortOrder;
        public int DisplayNumber => Model.SortOrder + 1;
        public string ObservationText => Model.ObservationText;
        public DateTime CreatedAt => Model.CreatedAt;
        public string CreatedByName => Model.CreatedByName;

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; private set => SetField(ref _isEditing, value); }

        private string _editText = string.Empty;
        public string EditText { get => _editText; set => SetField(ref _editText, value); }

        public bool CanMoveUp { get; set; }
        public bool CanMoveDown { get; set; }

        public Func<ObservationCardViewModel, Task> OnSave;
        public Func<ObservationCardViewModel, Task> OnDelete;
        public Func<ObservationCardViewModel, Task> OnMoveUp;
        public Func<ObservationCardViewModel, Task> OnMoveDown;

        public RelayCommand EditCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand MoveUpCommand { get; }
        public RelayCommand MoveDownCommand { get; }

        public ObservationCardViewModel(RepairItemObservation model)
        {
            Model = model;

            EditCommand = new RelayCommand(() => { EditText = ObservationText; IsEditing = true; });
            CancelCommand = new RelayCommand(() => IsEditing = false);
            SaveCommand = new RelayCommand(async () =>
            {
                if (string.IsNullOrWhiteSpace(EditText)) return;
                if (OnSave != null) await OnSave(this);
                IsEditing = false;
            });
            DeleteCommand = new RelayCommand(async () => { if (OnDelete != null) await OnDelete(this); });
            MoveUpCommand = new RelayCommand(async () => { if (OnMoveUp != null) await OnMoveUp(this); }, () => CanMoveUp);
            MoveDownCommand = new RelayCommand(async () => { if (OnMoveDown != null) await OnMoveDown(this); }, () => CanMoveDown);
        }
    }
}
