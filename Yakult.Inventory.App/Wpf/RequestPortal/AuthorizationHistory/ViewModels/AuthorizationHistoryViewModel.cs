using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestPortal.AuthorizationHistory.ViewModels
{
    public class AuthorizationHistoryViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private List<AuthHistoryRowViewModel> _allRows = new List<AuthHistoryRowViewModel>();
        private int _pendingHighlightId = -1;

        // ── My History tab ────────────────────────────────────────────────────
        public ObservableCollection<AuthHistoryRowViewModel> PagedRows { get; }
            = new ObservableCollection<AuthHistoryRowViewModel>();

        // ── Signed tab (approvers only) ───────────────────────────────────────
        private List<SignedRowViewModel> _allSignedRows = new List<SignedRowViewModel>();

        public ObservableCollection<SignedRowViewModel> PagedSignedRows { get; }
            = new ObservableCollection<SignedRowViewModel>();

        private int _signedTotalCount;
        public int SignedTotalCount
        {
            get => _signedTotalCount;
            set
            {
                if (!SetField(ref _signedTotalCount, value)) return;
                OnPropertyChanged(nameof(HasSignedRows));
            }
        }

        public bool HasSignedRows => _signedTotalCount > 0;

        private int _signedCurrentPage = 1;
        private int _signedTotalPages  = 1;

        public string SignedPageInfo => $"Page {_signedCurrentPage} of {_signedTotalPages}";
        public bool   CanSignedGoPrev => _signedCurrentPage > 1;
        public bool   CanSignedGoNext => _signedCurrentPage < _signedTotalPages;

        public ICommand SignedPreviousPageCommand { get; }
        public ICommand SignedNextPageCommand     { get; }

        // ── Active tab ────────────────────────────────────────────────────────
        private bool _showSignedTab;
        public bool ShowSignedTab
        {
            get => _showSignedTab;
            set
            {
                if (!SetField(ref _showSignedTab, value)) return;
                OnPropertyChanged(nameof(ShowMyHistoryTab));
            }
        }
        public bool ShowMyHistoryTab => !_showSignedTab;
        public bool IsApprover       => AppSession.IsApprover;

        public ICommand SwitchToMyHistoryCommand { get; }
        public ICommand SwitchToSignedCommand    { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (!SetField(ref _totalCount, value)) return;
                OnPropertyChanged(nameof(HasRows));
            }
        }

        public bool HasRows => _totalCount > 0;

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (!SetField(ref _currentPage, value)) return;
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(CanGoPrev));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (!SetField(ref _totalPages, value)) return;
                OnPropertyChanged(nameof(PageInfo));
            }
        }

        public string PageInfo => $"Page {_currentPage} of {_totalPages}";
        public bool CanGoPrev => _currentPage > 1;
        public bool CanGoNext => _currentPage < _totalPages;

        public ICommand RefreshCommand      { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand     { get; }

        /// <summary>
        /// Fired when the user clicks a row — the int is the AuthorizationId to show.
        /// </summary>
        public event Action<int> AuthorizationDetailRequested;

        public AuthorizationHistoryViewModel()
        {
            RefreshCommand      = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
            PreviousPageCommand = new RelayCommand(() => ApplyPage(_currentPage - 1), () => CanGoPrev);
            NextPageCommand     = new RelayCommand(() => ApplyPage(_currentPage + 1), () => CanGoNext);

            SignedPreviousPageCommand = new RelayCommand(() => ApplySignedPage(_signedCurrentPage - 1), () => CanSignedGoPrev);
            SignedNextPageCommand     = new RelayCommand(() => ApplySignedPage(_signedCurrentPage + 1), () => CanSignedGoNext);

            SwitchToMyHistoryCommand = new RelayCommand(() => ShowSignedTab = false);
            SwitchToSignedCommand    = new RelayCommand(() => ShowSignedTab = true);
        }

        /// <summary>
        /// Called by the code-behind when the user clicks a DataGrid row.
        /// Fires AuthorizationDetailRequested with the row's AuthorizationId.
        /// </summary>
        public void OnRowClicked(AuthHistoryRowViewModel row)
        {
            if (row == null) return;
            AuthorizationDetailRequested?.Invoke(row.AuthorizationId);
        }

        public void OnSignedRowClicked(SignedRowViewModel row)
        {
            if (row == null) return;
            AuthorizationDetailRequested?.Invoke(row.AuthorizationId);
        }

        /// <summary>
        /// Stores an ID to highlight once LoadAsync finishes. Use when data isn't loaded yet.
        /// </summary>
        public void SetPendingHighlight(int authorizationId)
        {
            _pendingHighlightId = authorizationId;
        }

        /// <summary>
        /// Navigates to the page containing authorizationId and highlights that row.
        /// Does NOT open any dialog — that is triggered only by a user row-click.
        /// </summary>
        public void NavigateToAndHighlight(int authorizationId)
        {
            // Clear any existing highlight
            foreach (var r in _allRows) r.IsHighlighted = false;

            int idx = _allRows.FindIndex(r => r.AuthorizationId == authorizationId);
            if (idx < 0) return;

            // Navigate to the correct page
            int targetPage = (idx / PageSize) + 1;
            ApplyPage(targetPage);

            // Set highlight on the paged row
            foreach (var r in PagedRows)
            {
                if (r.AuthorizationId == authorizationId)
                {
                    r.IsHighlighted = true;
                    break;
                }
            }
        }

        public async Task LoadAsync()
        {
            // Clear any previously injected demo rows before fetching fresh data
            RemoveTourDummyRows();

            IsLoading     = true;
            StatusMessage = "Loading...";
            try
            {
                int empId  = AppSession.CurrentEmployeeId ?? 0;
                int userId = AppSession.CurrentUserId;
                var repo   = new CartridgeAuthorizationRepository();

                var raw = await repo.GetHistoryByEmployeeAsync(empId);
                _allRows = raw
                    .OrderByDescending(x => x.CreatedDate)
                    .Select(Map)
                    .ToList();

                TotalCount = _allRows.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(_allRows.Count / (double)PageSize));
                ApplyPage(1);

                // Load approvals signed by this user (approvers only)
                if (AppSession.IsApprover && userId > 0)
                {
                    var signed = await repo.GetSignedByUserAsync(userId);
                    _allSignedRows = signed.Select(MapSigned).ToList();
                    SignedTotalCount = _allSignedRows.Count;
                    _signedTotalPages = Math.Max(1, (int)Math.Ceiling(_allSignedRows.Count / (double)PageSize));
                    ApplySignedPage(1);
                }

                StatusMessage = string.Empty;

                // Apply a pending highlight set before data was available
                if (_pendingHighlightId > 0)
                {
                    int id = _pendingHighlightId;
                    _pendingHighlightId = -1;
                    NavigateToAndHighlight(id);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading authorizations: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplySignedPage(int page)
        {
            if (page < 1 || page > _signedTotalPages) return;
            _signedCurrentPage = page;

            PagedSignedRows.Clear();
            foreach (var r in _allSignedRows.Skip((_signedCurrentPage - 1) * PageSize).Take(PageSize))
                PagedSignedRows.Add(r);

            OnPropertyChanged(nameof(SignedPageInfo));
            OnPropertyChanged(nameof(CanSignedGoPrev));
            OnPropertyChanged(nameof(CanSignedGoNext));
            ((RelayCommand)SignedPreviousPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SignedNextPageCommand).RaiseCanExecuteChanged();
        }

        private void ApplyPage(int page)
        {
            if (page < 1 || page > _totalPages) return;
            CurrentPage = page;

            PagedRows.Clear();
            foreach (var r in _allRows.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            ((RelayCommand)PreviousPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)NextPageCommand).RaiseCanExecuteChanged();
        }

        private static AuthHistoryRowViewModel Map(CartridgeAuthorizationModel m) =>
            new AuthHistoryRowViewModel
            {
                AuthorizationId  = m.AuthorizationId,
                Status           = m.Status,
                DepartmentName   = m.DepartmentName ?? "—",
                RequestedModels  = ParseModelNames(m.RequestedModels),
                SignedByName     = m.SignedByName ?? "—",
                SignedDate       = m.SignedDate.HasValue
                                   ? m.SignedDate.Value.ToString("MM/dd/yyyy")
                                   : "—",
                CreatedDate      = m.CreatedDate.ToString("MM/dd/yyyy")
            };

        private static SignedRowViewModel MapSigned(CartridgeAuthorizationModel m) =>
            new SignedRowViewModel
            {
                AuthorizationId = m.AuthorizationId,
                Status          = m.Status,
                EmployeeName    = m.EmployeeName   ?? "—",
                DepartmentName  = m.DepartmentName ?? "—",
                RequestedModels = ParseModelNames(m.RequestedModels),
                SignedDate      = m.SignedDate.HasValue
                                  ? m.SignedDate.Value.ToString("MM/dd/yyyy")
                                  : "—"
            };

        private static string ParseModelNames(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "—";
            try
            {
                using var doc = JsonDocument.Parse(json);
                var names = doc.RootElement.EnumerateArray()
                    .Select(el => el.TryGetProperty("model", out var p) ? p.GetString() : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();
                return names.Count > 0 ? string.Join(", ", names) : "—";
            }
            catch
            {
                return json;
            }
        }

        // ── Tour dummy data ───────────────────────────────────────────────────

        private bool _tourDummyInjected;
        public bool IsDemoDataActive => _tourDummyInjected;

        private bool _signedTourDummyInjected;
        public bool IsDemoSignedDataActive => _signedTourDummyInjected;

        public void AddTourDummyRows()
        {
            if (_totalCount > 0 || _tourDummyInjected) return;
            PagedRows.Add(new AuthHistoryRowViewModel
            {
                AuthorizationId = 1001,
                Status          = "Pending",           // amber — matches real DB casing
                DepartmentName  = "Information Technology",
                RequestedModels = "HP CF280A (x2), HP CE285A (x1)",
                SignedByName    = "—",
                SignedDate      = "—",
                CreatedDate     = DateTime.Today.ToString("MM/dd/yyyy")
            });
            PagedRows.Add(new AuthHistoryRowViewModel
            {
                AuthorizationId = 1002,
                Status          = "Approved",          // green — matches real DB casing
                DepartmentName  = "Information Technology",
                RequestedModels = "Canon 051H (x1)",
                SignedByName    = "Demo Manager",
                SignedDate      = DateTime.Today.AddDays(-7).ToString("MM/dd/yyyy"),
                CreatedDate     = DateTime.Today.AddDays(-7).ToString("MM/dd/yyyy")
            });
            TotalCount = 2;
            _tourDummyInjected = true;
            OnPropertyChanged(nameof(IsDemoDataActive));
        }

        public void RemoveTourDummyRows()
        {
            if (!_tourDummyInjected) return;
            PagedRows.Clear();
            TotalCount = 0;
            _tourDummyInjected = false;
            OnPropertyChanged(nameof(IsDemoDataActive));
        }

        public void AddTourDummySignedRows()
        {
            if (_signedTotalCount > 0 || _signedTourDummyInjected) return;
            PagedSignedRows.Add(new SignedRowViewModel
            {
                AuthorizationId = 2001,
                Status          = "Approved",
                EmployeeName    = "JUAN DELA CRUZ",
                DepartmentName  = "Sales",
                RequestedModels = "HP CF280A (x2)",
                SignedDate      = DateTime.Today.AddDays(-3).ToString("MM/dd/yyyy")
            });
            PagedSignedRows.Add(new SignedRowViewModel
            {
                AuthorizationId = 2002,
                Status          = "Rejected",
                EmployeeName    = "MARIA SANTOS",
                DepartmentName  = "Accounting",
                RequestedModels = "Canon 051H (x1)",
                SignedDate      = DateTime.Today.AddDays(-7).ToString("MM/dd/yyyy")
            });
            SignedTotalCount = 2;
            _signedTourDummyInjected = true;
            OnPropertyChanged(nameof(IsDemoSignedDataActive));
        }

        public void RemoveTourDummySignedRows()
        {
            if (!_signedTourDummyInjected) return;
            PagedSignedRows.Clear();
            SignedTotalCount = 0;
            _signedTourDummyInjected = false;
            OnPropertyChanged(nameof(IsDemoSignedDataActive));
        }
    }

    public class SignedRowViewModel : ViewModelBase
    {
        public int    AuthorizationId { get; set; }
        public string Status          { get; set; }
        public string EmployeeName    { get; set; }
        public string DepartmentName  { get; set; }
        public string RequestedModels { get; set; }
        public string SignedDate      { get; set; }

        public SolidColorBrush StatusColorBrush
        {
            get
            {
                switch (Status?.ToUpper() ?? "")
                {
                    case "APPROVED":
                        return new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x5E));
                    case "PENDING":
                    case "UNDER REVIEW":
                        return new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x00));
                    case "REJECTED":
                    case "CANCELLED":
                        return new SolidColorBrush(Color.FromRgb(0xE0, 0x3C, 0x31));
                    default:
                        return new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x7E));
                }
            }
        }
    }

    public class AuthHistoryRowViewModel : ViewModelBase
    {
        public int    AuthorizationId  { get; set; }
        public string Status           { get; set; }
        public string DepartmentName   { get; set; }
        public string RequestedModels  { get; set; }
        public string SignedByName     { get; set; }
        public string SignedDate       { get; set; }
        public string CreatedDate      { get; set; }

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetField(ref _isHighlighted, value);
        }

        public SolidColorBrush StatusColorBrush
        {
            get
            {
                string upper = Status?.ToUpper() ?? "";
                switch (upper)
                {
                    case "APPROVED":
                        return new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x5E));
                    case "PENDING":
                    case "UNDER REVIEW":
                        return new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x00));
                    case "REJECTED":
                    case "CANCELLED":
                        return new SolidColorBrush(Color.FromRgb(0xE0, 0x3C, 0x31));
                    default:
                        return new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x7E));
                }
            }
        }
    }
}
