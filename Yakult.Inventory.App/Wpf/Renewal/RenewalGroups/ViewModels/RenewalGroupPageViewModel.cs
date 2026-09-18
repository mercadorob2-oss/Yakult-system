using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels
{
    /// <summary>
    /// Business logic for the Renewals (Grouped) page, ported from
    /// Pages\Renewal\ViewRenewalGroupPage.cs. This page groups renewal chains by root set into
    /// expandable cards rather than a flat grid — see RenewalGroupRow for the card data shape.
    /// The commented-out "Chain Order" sort combo in the original was dead (never wired to any
    /// live control) and was not ported; chain rows are always ordered latest-first, matching
    /// the original's default (_orderLatestFirst = true, unreachable toggle).
    /// </summary>
    public sealed partial class RenewalGroupPageViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repository = new RenewalRepository();
        private readonly SetRepository _setRepository = new SetRepository();

        private List<RenewalDto> _allRenewals = new List<RenewalDto>();
        private List<RenewalGroupRow> _groups = new List<RenewalGroupRow>();
        private List<RenewalGroupRow> _filteredGroups = new List<RenewalGroupRow>();
        private Dictionary<int, List<string>> _itemNamesBySetId = new Dictionary<int, List<string>>();

        public RenewalGroupPageViewModel()
        {
            StatusOptions = new ObservableCollection<string> { "All", "Expired", "Expiring Soon", "Warning", "Active", "No Expiry Date" };
            TypeOptions = new ObservableCollection<string> { "All", "Software/License", "Services" };
            Rows = new ObservableCollection<RenewalGroupRow>();

            _selectedStatus = "All";
            _selectedType = "All";
            PageSize = 20;

            RefreshCommand = new RelayCommand(() => { _columnFilters.Clear(); _sortColumnKey = null; LoadGroups(); });
            ToggleExpandCommand = new RelayCommand<RenewalGroupRow>(row => { if (row != null) row.IsExpanded = !row.IsExpanded; });
            ViewDetailsCommand = new RelayCommand<RenewalChainRowVm>(ViewDetails);
            ManageItemsCommand = new RelayCommand<RenewalChainRowVm>(ManageItems);
            HistoryCommand = new RelayCommand<RenewalGroupRow>(row => RequestShowHistory?.Invoke(row));
            GenerateReportCommand = new RelayCommand(() => RequestShowReportOptionsDialog?.Invoke());
            ClearSubTypeFilterCommand = new RelayCommand(ClearSubTypeFilter);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            InitDocumentDateFilterRows();

            TotalCardCommand = new RelayCommand(() => SelectedStatus = "All");
            ExpiredCardCommand = new RelayCommand(() => ToggleStatusCard("Expired"));
            ExpiringCardCommand = new RelayCommand(() => ToggleStatusCard("Expiring Soon"));
            WarningCardCommand = new RelayCommand(() => ToggleStatusCard("Warning"));
            ActiveCardCommand = new RelayCommand(() => ToggleStatusCard("Active"));

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; RenderPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); RenderPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; RenderPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); RenderPage(); });

            _setFilterDebounceTimer.Tick += async (s, e) => { _setFilterDebounceTimer.Stop(); await LoadGroupsAsync(); };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilters(); };
        }

        // ── Events (WinForms/WPF-window interop seam — wired by the View's code-behind) ──
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;

        /// <summary>View opens RenewalDetailWindow(setId) and always reloads afterward.</summary>
        public event Action<int> RequestViewRenewalDetail;

        /// <summary>View opens ViewRenewalDetailPage(setId) (the WinForms page with the
        /// "Renew Items" bulk-renewal action) and always reloads afterward.</summary>
        public event Action<int> RequestManageRenewalItems;

        /// <summary>View opens the RenewalHistoryWindow for this group.</summary>
        public event Action<RenewalGroupRow> RequestShowHistory;

        /// <summary>View opens RenewalReportOptionsDialog; on OK calls CommitGenerateReport(includeOriginal).</summary>
        public event Action RequestShowReportOptionsDialog;

        /// <summary>Raised with root SetIds (checked groups, or null when none checked), the
        /// current status filter text (null when "All"), the include-original-sets flag, and
        /// any specific SetIds the user unchecked within an otherwise-selected chain (or null
        /// when nothing was excluded) so they can be left out of the generated report.</summary>
        public event Action<List<int>, string, bool, List<int>> RequestGenerateReport;

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand<RenewalGroupRow> ToggleExpandCommand { get; }
        public RelayCommand<RenewalChainRowVm> ViewDetailsCommand { get; }
        public RelayCommand<RenewalChainRowVm> ManageItemsCommand { get; }
        public RelayCommand<RenewalGroupRow> HistoryCommand { get; }
        public RelayCommand GenerateReportCommand { get; }
        public RelayCommand ClearSubTypeFilterCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand TotalCardCommand { get; }
        public RelayCommand ExpiredCardCommand { get; }
        public RelayCommand ExpiringCardCommand { get; }
        public RelayCommand WarningCardCommand { get; }
        public RelayCommand ActiveCardCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data (current page only) ───────────────────────────────────
        public ObservableCollection<RenewalGroupRow> Rows { get; }

        // ── "N Selected" badge — cross-page, matches CommitGenerateReport's scope ──
        private int _selectedCount;
        public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
        public bool HasSelection => SelectedCount > 0;

        public List<RenewalGroupRow> GetSelectedGroups() => (_filteredGroups ?? _groups).Where(g => g.Selected).ToList();
        public void ClearSelection() { foreach (var g in (_filteredGroups ?? _groups)) g.Selected = false; }

        public void CommitGenerateReport(bool includeOriginal)
        {
            var list = _filteredGroups ?? _groups;
            var selectedGroups = list?.Where(g => g != null && g.Selected).ToList() ?? new List<RenewalGroupRow>();
            var selected = selectedGroups.Select(g => g.RootSetId).ToList();
            var excluded = selectedGroups.SelectMany(g => g.GetExcludedSetIds()).ToList();
            string filter = SelectedStatus == "All" ? null : SelectedStatus;
            RequestGenerateReport?.Invoke(
                selected.Count > 0 ? selected : null,
                filter,
                includeOriginal,
                excluded.Count > 0 ? excluded : null);
        }

        private void ViewDetails(RenewalChainRowVm row)
        {
            if (row == null) return;
            try
            {
                int targetSetId = _repository.GetLatestSetIdInChain(row.Dto.SetId);
                if (targetSetId <= 0) targetSetId = row.Dto.SetId;
                RequestViewRenewalDetail?.Invoke(targetSetId);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to open renewal details: {ex.Message}");
            }
        }

        private void ManageItems(RenewalChainRowVm row)
        {
            if (row == null) return;
            try
            {
                int targetSetId = _repository.GetLatestSetIdInChain(row.Dto.SetId);
                if (targetSetId <= 0) targetSetId = row.Dto.SetId;
                RequestManageRenewalItems?.Invoke(targetSetId);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to open renewal items: {ex.Message}");
            }
        }

        /// <summary>Item names for the History window — populated once at load time.</summary>
        public List<string> GetItemNames(int setId)
            => _itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(setId, out var names) ? names : new List<string>();
    }
}
