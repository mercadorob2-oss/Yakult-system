using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.ViewModels
{
    public class RequestHistoryViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly RequesterPortalService _service;
        private List<RequestHistoryRowViewModel> _allRows = new List<RequestHistoryRowViewModel>();
        private string _pendingHighlightSetCode;

        public event Action<List<PortalRequestStatusDto>> ShowDetailRequested;

        public ObservableCollection<RequestHistoryRowViewModel> PagedRows { get; }
            = new ObservableCollection<RequestHistoryRowViewModel>();

        private RequestHistoryRowViewModel _selectedRow;
        public RequestHistoryRowViewModel SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

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

        public ICommand LoadCommand        { get; }
        public ICommand RefreshCommand     { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand     { get; }
        public RelayCommand<RequestHistoryRowViewModel> ShowDetailCommand { get; }

        public RequestHistoryViewModel()
        {
            _service            = new RequesterPortalService();
            LoadCommand         = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
            RefreshCommand      = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
            PreviousPageCommand = new RelayCommand(() => ApplyPage(_currentPage - 1), () => CanGoPrev);
            NextPageCommand     = new RelayCommand(() => ApplyPage(_currentPage + 1), () => CanGoNext);
            ShowDetailCommand   = new RelayCommand<RequestHistoryRowViewModel>(row =>
            {
                if (row?.Items != null)
                    ShowDetailRequested?.Invoke(row.Items);
            });
        }

        public void SetPendingHighlight(string setCode)
        {
            _pendingHighlightSetCode = setCode;
        }

        public void NavigateToAndHighlight(string setCode)
        {
            foreach (var r in _allRows) r.IsHighlighted = false;

            int idx = _allRows.FindIndex(r => r.SetCode == setCode);
            if (idx < 0) return;

            int targetPage = (idx / PageSize) + 1;
            ApplyPage(targetPage);

            foreach (var r in PagedRows)
            {
                if (r.SetCode == setCode)
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
                var raw = await Task.Run(() => _service.GetPortalRequestsByUser(AppSession.CurrentUserId));

                _allRows = raw
                    .GroupBy(x => x.SubmissionSessionId.HasValue
                                  ? x.SubmissionSessionId.Value.ToString()
                                  : x.ReqId.ToString())
                    .Select(g => BuildRow(g.ToList()))
                    .OrderByDescending(r => r.DateRequested)
                    .ToList();

                TotalCount = _allRows.Count;
                TotalPages = Math.Max(1, (int)Math.Ceiling(_allRows.Count / (double)PageSize));
                ApplyPage(1);

                StatusMessage = string.Empty;

                if (!string.IsNullOrEmpty(_pendingHighlightSetCode))
                {
                    string code = _pendingHighlightSetCode;
                    _pendingHighlightSetCode = null;
                    NavigateToAndHighlight(code);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading history: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
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

        private static RequestHistoryRowViewModel BuildRow(List<PortalRequestStatusDto> items)
        {
            var first  = items[0];
            string status = AggregateGroupStatus(items);
            string cartridgeDisplay = items.Count == 1
                ? first.CartridgeName ?? first.ItemName ?? "—"
                : string.Join(", ", items.Select(x => x.CartridgeName ?? x.ItemName ?? "—").Distinct());

            string returnInfo = string.Empty;
            int totalGood    = items.Sum(x => x.GoodEmptyQty);
            int totalDamaged = items.Sum(x => x.DamagedEmptyQty);
            if (totalGood > 0 || totalDamaged > 0)
                returnInfo = $"G:{totalGood} D:{totalDamaged}";

            return new RequestHistoryRowViewModel
            {
                SetCode           = !string.IsNullOrWhiteSpace(first.SetCode) ? first.SetCode : "—",
                DateRequested     = (DateTime?)first.DateRequested,
                CartridgeDisplay  = cartridgeDisplay,
                TotalQty          = items.Sum(x => x.Quantity),
                ReturnInfo        = returnInfo,
                FulfillmentMethod = first.FulfillmentMethod ?? "—",
                DestinationBranch = first.DestinationBranch ?? "—",
                Status            = status,
                Items             = items
            };
        }

        private static string AggregateGroupStatus(List<PortalRequestStatusDto> items)
        {
            var upper = items.Select(x => x.Status?.ToUpperInvariant() ?? "").ToList();
            bool anyUnfulfilled = upper.Any(s => s == "UNFULFILLED" || s.Contains("PARTIALLY"));
            bool anyFulfilled   = upper.Any(s => s == "FULFILLED" || s == "COMPLETED" || s == "REPLACED");
            bool anyActive      = upper.Any(s => s == "UNDER REVIEW" || s == "PROCESSING");
            if (anyUnfulfilled && (anyFulfilled || anyActive)) return "Partially Fulfilled";
            if (anyUnfulfilled) return "Unfulfilled";
            if (anyFulfilled)   return "Fulfilled";
            return items[0].Status ?? string.Empty;
        }

        // ── Tour dummy data ───────────────────────────────────────────────────

        private bool _tourDummyInjected;
        public bool IsDemoDataActive => _tourDummyInjected;

        public void AddTourDummyRows()
        {
            if (_totalCount > 0 || _tourDummyInjected) return;
            PagedRows.Add(new RequestHistoryRowViewModel
            {
                SetCode           = "REQ-DEMO-001",
                DateRequested     = DateTime.Today,
                CartridgeDisplay  = "HP CF280A, HP CE285A",
                TotalQty          = 3,
                ReturnInfo        = "G:2 D:0",
                FulfillmentMethod = "PICKUP",
                DestinationBranch = "Manila Liaison Office",
                Status            = "Awaiting Authorization"    // gray — matches real DB casing
            });
            PagedRows.Add(new RequestHistoryRowViewModel
            {
                SetCode           = "REQ-DEMO-002",
                DateRequested     = DateTime.Today.AddDays(-7),
                CartridgeDisplay  = "Canon 051H",
                TotalQty          = 1,
                ReturnInfo        = "G:1 D:0",
                FulfillmentMethod = "DELIVERY",
                DestinationBranch = "Manila Liaison Office",
                Status            = "Fulfilled"        // green — matches real DB casing
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
    }

    public class RequestHistoryRowViewModel : ViewModelBase
    {
        public string SetCode           { get; set; }
        public DateTime? DateRequested  { get; set; }
        public string CartridgeDisplay  { get; set; }
        public int TotalQty             { get; set; }
        public string ReturnInfo        { get; set; }
        public string FulfillmentMethod { get; set; }
        public string DestinationBranch { get; set; }
        public string Status            { get; set; }
        public List<PortalRequestStatusDto> Items { get; set; }

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetField(ref _isHighlighted, value);
        }

        public string DateDisplay => DateRequested.HasValue
            ? DateRequested.Value.ToString("MM/dd/yyyy")
            : "—";

        public SolidColorBrush StatusColorBrush
        {
            get
            {
                string upper = Status?.ToUpperInvariant() ?? "";
                if (upper == "UNFULFILLED")
                    return new SolidColorBrush(Color.FromRgb(0xE0, 0x3C, 0x31)); // Red
                if (upper.Contains("PARTIALLY"))
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x00)); // Orange
                switch (upper)
                {
                    case "UNDER REVIEW":
                    case "PROCESSING":
                        return new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6)); // Blue
                    case "RECEIVED":
                        return new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x00)); // Amber
                    case "REPLACED":
                    case "COMPLETED":
                    case "FULFILLED":
                        return new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x5E)); // Green
                    case "CANCELLED":
                    case "REJECTED":
                        return new SolidColorBrush(Color.FromRgb(0xE0, 0x3C, 0x31)); // Red
                    default:
                        return new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x7E)); // Gray
                }
            }
        }
    }
}
