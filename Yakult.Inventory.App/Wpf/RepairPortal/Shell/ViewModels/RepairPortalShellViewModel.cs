using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels
{
    public enum RepairPortalViewMode
    {
        Gallery,
        Table,
        Kanban
    }

    /// <summary>
    /// Owns the shared ticket collection and attendance/summary state for the Repair Technician
    /// Portal shell. All three views (Gallery/Table/Kanban) bind to the same
    /// ObservableCollection&lt;RepairTicketListItem&gt; instance — no per-view querying.
    /// </summary>
    public sealed partial class RepairPortalShellViewModel : ViewModelBase
    {
        internal readonly IRepairTicketRepository _repository;

        /// <summary>Exposes the shared repository instance so the Shell's code-behind can pass it
        /// to the New Ticket dialog and Detail window without constructing a second instance.</summary>
        public IRepairTicketRepository RepositoryForDialogs => _repository;

        public ObservableCollection<RepairTicketListItem> Tickets { get; } = new ObservableCollection<RepairTicketListItem>();

        private RepairPortalSummaryCounts _summary = new RepairPortalSummaryCounts();
        public RepairPortalSummaryCounts Summary { get => _summary; private set => SetField(ref _summary, value); }

        private RepairPortalViewMode _selectedViewMode = RepairPortalViewMode.Gallery;
        public RepairPortalViewMode SelectedViewMode
        {
            get => _selectedViewMode;
            set { if (SetField(ref _selectedViewMode, value)) { OnPropertyChanged(nameof(IsGalleryMode)); OnPropertyChanged(nameof(IsTableMode)); OnPropertyChanged(nameof(IsKanbanMode)); OnPropertyChanged(nameof(ShowTableSelectionActions)); ScheduleSaveState(); } }
        }

        public bool IsGalleryMode => SelectedViewMode == RepairPortalViewMode.Gallery;
        public bool IsTableMode => SelectedViewMode == RepairPortalViewMode.Table;
        public bool IsKanbanMode => SelectedViewMode == RepairPortalViewMode.Kanban;

        private RepairTechnicianAttendanceStatus _attendance = new RepairTechnicianAttendanceStatus();
        public RepairTechnicianAttendanceStatus Attendance { get => _attendance; private set => SetField(ref _attendance, value); }

        public bool IsTimedIn => Attendance?.IsTimedIn == true;

        /// <summary>False until the Shell's first attendance query resolves. The gate/workspace
        /// bindings key off this too (not just IsTimedIn) so the "You're not timed in" overlay
        /// can't flash on screen for an already-timed-in technician while Attendance still holds
        /// its optimistic not-timed-in default during that first query.</summary>
        private bool _isAttendanceLoaded;
        public bool IsAttendanceLoaded { get => _isAttendanceLoaded; private set => SetField(ref _isAttendanceLoaded, value); }

        public bool ShowTimeInGate => IsAttendanceLoaded && !IsTimedIn;
        public bool IsWorkspaceUnlocked => !IsAttendanceLoaded || IsTimedIn;

        private DateTime? _lastTimeOut;
        /// <summary>Most recent TimeOut on record for this technician, regardless of which day it
        /// happened — shown even before today's first Time In so there's always a reference point
        /// ("last time you clocked out"), unlike Attendance which only tracks today's row.</summary>
        public DateTime? LastTimeOut { get => _lastTimeOut; private set => SetField(ref _lastTimeOut, value); }

        private string _elapsedDisplay = "00:00:00";
        public string ElapsedDisplay { get => _elapsedDisplay; private set => SetField(ref _elapsedDisplay, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        public string CurrentDateDisplay => DateTime.Now.ToString("dddd, MMMM d, yyyy");
        public string CurrentTechnicianName => string.IsNullOrWhiteSpace(AppSession.CurrentEmployeeName) ? AppSession.CurrentUserName : AppSession.CurrentEmployeeName;

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<RepairTicketListItem> RequestOpenDetail;
        public event Action RequestNewTicketDialog;
        public event Func<string, string, bool> RequestConfirm;

        private readonly DispatcherTimer _elapsedTimer;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _stateSaveTimer;
        private bool _restoringState;

        /// <summary>The Shell's initial (fire-and-forget) load, including the first attendance
        /// status query. TimeInAsync/TimeOutAsync await this before doing anything — otherwise a
        /// technician clicking "Time In" quickly after the window opens (the gate is clickable
        /// immediately, showing its optimistic default "not timed in" state) could race with this
        /// still-in-flight initial query: if it resolves AFTER the manual Time In call, it would
        /// silently overwrite the just-set correct Attendance with its own now-stale result,
        /// making the UI flip back to "not timed in" and the elapsed timer stop.</summary>
        private Task _initializeTask;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand TimeInCommand { get; }
        public RelayCommand TimeOutCommand { get; }
        public RelayCommand NewTicketCommand { get; }
        public RelayCommand<string> SetViewModeCommand { get; }
        public RelayCommand<RepairTicketListItem> OpenTicketCommand { get; }
        public RelayCommand<RepairTicketListItem> DeleteTicketCommand { get; }
        public RelayCommand<System.Collections.Generic.IEnumerable<RepairTicketListItem>> DeleteTicketsCommand { get; }

        public RepairPortalShellViewModel() : this(new RepairTicketRepository())
        {
        }

        public RepairPortalShellViewModel(IRepairTicketRepository repository)
        {
            _repository = repository ?? new RepairTicketRepository();

            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            TimeInCommand = new RelayCommand(async () => await TimeInAsync());
            TimeOutCommand = new RelayCommand(async () => await TimeOutAsync());
            NewTicketCommand = new RelayCommand(() => RequestNewTicketDialog?.Invoke());
            SetViewModeCommand = new RelayCommand<string>(mode =>
            {
                if (Enum.TryParse<RepairPortalViewMode>(mode, out var parsed))
                    SelectedViewMode = parsed;
            });
            OpenTicketCommand = new RelayCommand<RepairTicketListItem>(t => { if (t != null) RequestOpenDetail?.Invoke(t); });
            DeleteTicketCommand = new RelayCommand<RepairTicketListItem>(async t => await DeleteTicketAsync(t));
            DeleteTicketsCommand = new RelayCommand<System.Collections.Generic.IEnumerable<RepairTicketListItem>>(async tickets => await DeleteTicketsAsync(tickets));

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (s, e) => UpdateElapsedDisplay();

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); _ = LoadTicketsAsync(); };

            _stateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _stateSaveTimer.Tick += (s, e) => { _stateSaveTimer.Stop(); SaveUiState(); };

            InitFilters();
            InitTableSelection();
            InitMaintenance();
            RestoreUiState();

            _initializeTask = InitializeAsync();
        }

        private void UpdateElapsedDisplay()
        {
            OnPropertyChanged(nameof(Attendance));
            ElapsedDisplay = Attendance?.ElapsedDisplay ?? "00:00:00";
        }

        /// <summary>Raises every property derived from Attendance's timed-in state at once —
        /// IsTimedIn, and the gate/workspace flags that also fold in IsAttendanceLoaded.</summary>
        private void RaiseAttendanceDerivedProps()
        {
            OnPropertyChanged(nameof(IsTimedIn));
            OnPropertyChanged(nameof(ShowTimeInGate));
            OnPropertyChanged(nameof(IsWorkspaceUnlocked));
        }

        public bool IsDisposed { get; private set; }
        public void MarkDisposed()
        {
            IsDisposed = true;
            _elapsedTimer.Stop();
            _searchDebounceTimer.Stop();
            _stateSaveTimer.Stop();
        }

        private void SetBusy(bool busy) => IsBusy = busy;
    }
}
