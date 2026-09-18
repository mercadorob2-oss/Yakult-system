using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Forms.SystemSettings;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.SendNotifications.ViewModels
{
    public class EmployeeOption
    {
        public int    EmpId  { get; }
        public string Label  { get; }

        public EmployeeOption(int empId, string label)
        {
            EmpId = empId;
            Label = label;
        }

        public override string ToString() => Label;
    }

    public class SendNotificationsViewModel : ViewModelBase, IDisposable
    {
        private readonly CartridgeManagementRepository _repo = new CartridgeManagementRepository();

        // Full dataset loaded from DB
        private List<NotificationRowViewModel> _allRows = new List<NotificationRowViewModel>();

        // Filtered slice (post-search/filter, pre-page)
        private List<NotificationRowViewModel> _filteredRows = new List<NotificationRowViewModel>();

        // What the DataGrid actually binds to (current page only)
        public ObservableCollection<NotificationRowViewModel> PagedRows { get; }
            = new ObservableCollection<NotificationRowViewModel>();

        private const int PageSize = 25;

        private List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> _employees
            = new List<(int, string, string, string, string, string)>();

        // ── Selection ──────────────────────────────────────────────────────────
        private NotificationRowViewModel _selectedRow;
        public NotificationRowViewModel SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                {
                    RefreshDetailProperties();
                    RebuildReceiverOptions();
                    Notes         = string.Empty;
                    StatusMessage = string.Empty;
                }
            }
        }

        public bool HasSelection => _selectedRow != null;

        public bool IsPickup => _selectedRow != null &&
            string.Equals(_selectedRow.DistributionMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);

        // ── Detail card display ─────────────────────────────────────────────────
        public string DetailSetCode      => _selectedRow?.SetCode            ?? "—";
        public string DetailRequester    => _selectedRow?.RequesterName      ?? "—";
        public string DetailCompany      => _selectedRow?.CompanyName        ?? "—";
        public string DetailBranchDept   => _selectedRow?.BranchDept         ?? "—";
        public string DetailDistribution => _selectedRow?.DistributionMethod  ?? "—";
        public string DetailReceivedBy   => _selectedRow?.ReceivedByDisplay  ?? "—";
        public string DetailStatus       => _selectedRow?.FriendlyStatus     ?? "—";
        public string DetailStatusFg     => _selectedRow?.StatusFg           ?? "#5A6A7E";
        public string DetailStatusBg     => _selectedRow?.StatusBg           ?? "#F0F2F6";

        // ── Receiver combo (PICKUP only) ────────────────────────────────────────
        public ObservableCollection<EmployeeOption> ReceiverOptions { get; }
            = new ObservableCollection<EmployeeOption>();

        private EmployeeOption _selectedReceiver;
        public EmployeeOption SelectedReceiver
        {
            get => _selectedReceiver;
            set => SetField(ref _selectedReceiver, value);
        }

        // ── Notes ───────────────────────────────────────────────────────────────
        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetField(ref _notes, value);
        }

        // ── Status / feedback ───────────────────────────────────────────────────
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (SetField(ref _statusMessage, value))
                    OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
        public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

        private bool _statusIsSuccess;
        public bool StatusIsSuccess
        {
            get => _statusIsSuccess;
            set
            {
                if (SetField(ref _statusIsSuccess, value))
                    OnPropertyChanged(nameof(StatusMessageColor));
            }
        }
        public string StatusMessageColor => _statusIsSuccess ? "#1E9E5E" : "#C0392B";

        // ── Filters ─────────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                    ApplyFiltersAndPage();
            }
        }

        public List<string> StatusOptions { get; } = new List<string>
        {
            "All", "Fulfilled", "Partially Fulfilled", "Unfulfilled"
        };

        private string _selectedStatus = "All";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetField(ref _selectedStatus, value))
                    ApplyFiltersAndPage();
            }
        }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetField(ref _dateFrom, value))
                    ApplyFiltersAndPage();
            }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set
            {
                if (SetField(ref _dateTo, value))
                    ApplyFiltersAndPage();
            }
        }

        // ── Loading / sending ───────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set
            {
                if (SetField(ref _isSending, value))
                    OnPropertyChanged(nameof(CanSendNotification));
            }
        }

        public bool CanSendNotification => HasSelection && !IsSending;

        // ── Summary cards ───────────────────────────────────────────────────────
        private int _pendingCount;
        public int PendingCount
        {
            get => _pendingCount;
            set => SetField(ref _pendingCount, value);
        }

        private int _sentTodayCount;
        public int SentTodayCount
        {
            get => _sentTodayCount;
            set => SetField(ref _sentTodayCount, value);
        }

        private int _fulfilledCount;
        public int FulfilledCount
        {
            get => _fulfilledCount;
            set => SetField(ref _fulfilledCount, value);
        }

        private string _lastRefresh = "—";
        public string LastRefresh
        {
            get => _lastRefresh;
            set => SetField(ref _lastRefresh, value);
        }

        // ── Pagination ──────────────────────────────────────────────────────────
        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetField(ref _currentPage, value))
                    RefreshPaginationProperties();
            }
        }

        public int TotalPages =>
            Math.Max(1, (int)Math.Ceiling((double)_filteredRows.Count / PageSize));

        public string PageInfo
        {
            get
            {
                if (_filteredRows.Count == 0) return "No records";
                int start = (_currentPage - 1) * PageSize + 1;
                int end   = Math.Min(_currentPage * PageSize, _filteredRows.Count);
                return $"Page {_currentPage} of {TotalPages}  ·  Showing {start}–{end} of {_filteredRows.Count}";
            }
        }

        public bool CanGoFirst => _currentPage > 1;
        public bool CanGoPrev  => _currentPage > 1;
        public bool CanGoNext  => _currentPage < TotalPages;
        public bool CanGoLast  => _currentPage < TotalPages;

        // ── Commands ────────────────────────────────────────────────────────────
        public ICommand RefreshCommand          { get; }
        public ICommand SendNotificationCommand { get; }
        public ICommand PreviewEmailCommand     { get; }
        public ICommand SaveReceiverCommand     { get; }
        public ICommand ClearFiltersCommand     { get; }
        public ICommand FirstPageCommand        { get; }
        public ICommand PrevPageCommand         { get; }
        public ICommand NextPageCommand         { get; }
        public ICommand LastPageCommand         { get; }

        public SendNotificationsViewModel()
        {
            RefreshCommand          = new RelayCommand(async () => await LoadDataAsync());
            SendNotificationCommand = new RelayCommand(async () => await SendNotificationAsync(),
                () => CanSendNotification);
            PreviewEmailCommand     = new RelayCommand(async () => await PreviewEmailAsync(),
                () => HasSelection && !IsSending);
            SaveReceiverCommand     = new RelayCommand(SaveReceiver,
                () => HasSelection && IsPickup);
            ClearFiltersCommand     = new RelayCommand(ClearFilters);
            FirstPageCommand        = new RelayCommand(() => GoToPage(1),            () => CanGoFirst);
            PrevPageCommand         = new RelayCommand(() => GoToPage(_currentPage - 1), () => CanGoPrev);
            NextPageCommand         = new RelayCommand(() => GoToPage(_currentPage + 1), () => CanGoNext);
            LastPageCommand         = new RelayCommand(() => GoToPage(TotalPages),   () => CanGoLast);
        }

        public async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var sets      = default(List<FulfilledSetNotificationDto>);
                var employees = default(List<(int, string, string, string, string, string)>);

                await Task.Run(() =>
                {
                    sets      = _repo.GetFulfilledSetsForNotification();
                    employees = _repo.GetActiveEmployeesForReceiver();
                });

                _employees = employees;
                _allRows   = sets.Select(dto => new NotificationRowViewModel(dto)).ToList();

                UpdateSummaryCards();
                LastRefresh = DateTime.Now.ToString("HH:mm");
                SelectedRow = null;
                ApplyFiltersAndPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load notification data:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateSummaryCards()
        {
            var today = DateTime.Today;
            PendingCount   = _allRows.Count(r => r.SetStatus == "Pending");
            FulfilledCount = _allRows.Count(r => r.SetStatus == "Dispatched");
            SentTodayCount = _allRows.Count(r => r.Dto.CreatedAt.Date == today);
        }

        private void ApplyFiltersAndPage()
        {
            _filteredRows = _allRows.Where(MatchesFilter).ToList();
            _currentPage  = 1;
            RepopulatePagedRows();
            RefreshPaginationProperties();
        }

        private bool MatchesFilter(NotificationRowViewModel row)
        {
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var s = _searchText.Trim();
                bool hit =
                    row.SetCode.IndexOf(s, StringComparison.OrdinalIgnoreCase)       >= 0 ||
                    row.RequesterName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.CompanyName.IndexOf(s, StringComparison.OrdinalIgnoreCase)   >= 0 ||
                    row.BranchDept.IndexOf(s, StringComparison.OrdinalIgnoreCase)    >= 0;
                if (!hit) return false;
            }

            if (_selectedStatus != "All" && row.FriendlyStatus != _selectedStatus)
                return false;

            if (_dateFrom.HasValue && row.Dto.CreatedAt != DateTime.MinValue &&
                row.Dto.CreatedAt.Date < _dateFrom.Value.Date)
                return false;

            if (_dateTo.HasValue && row.Dto.CreatedAt != DateTime.MinValue &&
                row.Dto.CreatedAt.Date > _dateTo.Value.Date)
                return false;

            return true;
        }

        private void GoToPage(int page)
        {
            int clamped = Math.Max(1, Math.Min(page, TotalPages));
            if (clamped == _currentPage) return;
            _currentPage = clamped;
            RepopulatePagedRows();
            RefreshPaginationProperties();
        }

        private void RepopulatePagedRows()
        {
            PagedRows.Clear();
            foreach (var row in _filteredRows.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);
        }

        private void RefreshPaginationProperties()
        {
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(CanGoFirst));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoLast));
        }

        private void ClearFilters()
        {
            _searchText    = string.Empty;
            _selectedStatus = "All";
            _dateFrom      = null;
            _dateTo        = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatus));
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            ApplyFiltersAndPage();
        }

        private void RebuildReceiverOptions()
        {
            ReceiverOptions.Clear();
            ReceiverOptions.Add(new EmployeeOption(0, "— Select receiver —"));

            foreach (var emp in _employees)
            {
                var empNum   = !string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? $" ({emp.EmployeeNumber})" : "";
                var deptPart = !string.IsNullOrWhiteSpace(emp.DepartmentName) ? $" – {emp.DepartmentName}" : "";
                ReceiverOptions.Add(new EmployeeOption(emp.EmpId, $"{emp.Name}{empNum}{deptPart}"));
            }

            SelectedReceiver = ReceiverOptions[0];

            if (_selectedRow?.ReceivedById.HasValue == true && _selectedRow.ReceivedById.Value > 0)
            {
                foreach (var opt in ReceiverOptions)
                {
                    if (opt.EmpId == _selectedRow.ReceivedById.Value)
                    {
                        SelectedReceiver = opt;
                        break;
                    }
                }
            }
        }

        private void SaveReceiver()
        {
            if (_selectedRow == null) return;

            var chosen = _selectedReceiver;
            if (chosen == null || chosen.EmpId <= 0)
            {
                MessageBox.Show("Please select a receiver before saving.",
                    "Receiver Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

                if (_selectedRow.SubmissionSessionId.HasValue)
                    _repo.UpdateReceivedByForSession(_selectedRow.SubmissionSessionId.Value, chosen.EmpId, userId);
                else
                    _repo.UpdateReceivedByForRequest(_selectedRow.ReqId, chosen.EmpId, userId);

                _selectedRow.UpdateReceivedBy(chosen.EmpId, chosen.Label);
                OnPropertyChanged(nameof(DetailReceivedBy));
                SetStatus("Receiver saved.", success: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save receiver:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendNotificationAsync()
        {
            if (_selectedRow == null) return;

            if (IsPickup && (!_selectedRow.ReceivedById.HasValue || _selectedRow.ReceivedById.Value <= 0))
            {
                var chosen = _selectedReceiver;
                if (chosen != null && chosen.EmpId > 0)
                {
                    try
                    {
                        int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                        if (_selectedRow.SubmissionSessionId.HasValue)
                            _repo.UpdateReceivedByForSession(_selectedRow.SubmissionSessionId.Value, chosen.EmpId, userId);
                        else
                            _repo.UpdateReceivedByForRequest(_selectedRow.ReqId, chosen.EmpId, userId);

                        _selectedRow.UpdateReceivedBy(chosen.EmpId, chosen.Label);
                        OnPropertyChanged(nameof(DetailReceivedBy));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not save receiver before sending:\n\n{ex.Message}",
                            "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Please select and save the receiver before sending the notification.",
                        "Receiver Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            IsSending = true;
            SetStatus("Preparing email…", success: true);

            try
            {
                int    setId = _selectedRow.SetId;
                string notes = _notes;

                var preview = await Task.Run(() => CartridgeManagementForm.GenerateEmailContentForSet(setId));

                if (preview == null || preview.Error != null)
                {
                    SetStatus($"Not sent: {preview?.Error ?? "Could not generate email content."}", success: false);
                    return;
                }

                preview.Body = ApplyNotesRow(preview.Body, notes);
                SetStatus("Sending…", success: true);

                string errorMsg = await Task.Run(() => CartridgeManagementForm.SendFulfillmentEmailWithPreview(preview));

                if (string.IsNullOrEmpty(errorMsg))
                    SetStatus("Notification sent successfully.", success: true);
                else
                    SetStatus($"Not sent: {errorMsg}", success: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Send failed: {ex.Message}", success: false);
            }
            finally
            {
                IsSending = false;
            }
        }

        private async Task PreviewEmailAsync()
        {
            if (_selectedRow == null) return;

            IsSending = true;
            SetStatus("Building preview…", success: true);

            try
            {
                int    setId = _selectedRow.SetId;
                string notes = _notes;

                var preview = await Task.Run(() => CartridgeManagementForm.GenerateEmailContentForSet(setId));

                if (preview == null || preview.Error != null)
                {
                    SetStatus($"Preview failed: {preview?.Error ?? "Could not generate email content."}", success: false);
                    return;
                }

                var body = ApplyNotesRow(preview.Body, notes);
                SetStatus(string.Empty, success: true);

                using (var dlg = new EmailTemplateHtmlPreviewDialog(
                    _selectedRow.SetCode, preview.Subject, body, isHtml: true))
                {
                    dlg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Preview failed: {ex.Message}", success: false);
            }
            finally
            {
                IsSending = false;
            }
        }

        private static string ApplyNotesRow(string body, string notes)
        {
            string notesRow = string.Empty;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                string safeNotes = notes
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\r\n", "<br>")
                    .Replace("\n", "<br>");
                notesRow =
                    "<tr>" +
                    "<td style=\"padding:8px 0; color:#666;\"><strong>Notes:</strong></td>" +
                    $"<td style=\"padding:8px 0;\">{safeNotes}</td>" +
                    "</tr>";
            }
            return body.Replace("{NotesRow}", notesRow);
        }

        private void SetStatus(string message, bool success)
        {
            StatusIsSuccess = success;
            StatusMessage   = message;
        }

        private void RefreshDetailProperties()
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsPickup));
            OnPropertyChanged(nameof(CanSendNotification));
            OnPropertyChanged(nameof(DetailSetCode));
            OnPropertyChanged(nameof(DetailRequester));
            OnPropertyChanged(nameof(DetailCompany));
            OnPropertyChanged(nameof(DetailBranchDept));
            OnPropertyChanged(nameof(DetailDistribution));
            OnPropertyChanged(nameof(DetailReceivedBy));
            OnPropertyChanged(nameof(DetailStatus));
            OnPropertyChanged(nameof(DetailStatusFg));
            OnPropertyChanged(nameof(DetailStatusBg));
        }

        public void Dispose() { }
    }
}
