using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.InvoicePreparation.ViewModels
{
    /// <summary>
    /// Business logic for the Invoice Sub Groups page — one row per Sub-Type Group.
    /// Groups are created from the Items Page's "Add to Contract/Subscription/License/
    /// Service Group" bulk actions; this page only reviews and invoices them. Groups are
    /// split across a "Not Completed" tab (Draft/Cancelled) and a "Completed" tab, but
    /// both partitions share the same underlying row objects, so a checkbox toggled in
    /// either tab is reflected correctly by <see cref="Groups"/> for Preview/Create Invoice.
    /// </summary>
    public sealed class InvoicePreparationListViewModel : ViewModelBase
    {
        private readonly InvoicePreparationRepository _repository = new InvoicePreparationRepository();

        public ObservableCollection<InvoicePreparationSummaryRow> Groups { get; } = new ObservableCollection<InvoicePreparationSummaryRow>();
        public ObservableCollection<InvoicePreparationSummaryRow> NotCompletedGroups { get; } = new ObservableCollection<InvoicePreparationSummaryRow>();
        public ObservableCollection<InvoicePreparationSummaryRow> CompletedGroups { get; } = new ObservableCollection<InvoicePreparationSummaryRow>();

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;

        /// <summary>Raised with a PreparationId so the View can open the read-only
        /// Preview Contents dialog.</summary>
        public event Action<int> RequestPreview;

        /// <summary>Raised with the checked groups' ids so the View can pre-fill and open
        /// SoftwareServiceSetDialog, then call <see cref="MarkGroupsInvoiced"/> on success.</summary>
        public event Action<List<int>> RequestCreateInvoice;

        private InvoicePreparationSummaryRow _selectedGroup;
        public InvoicePreparationSummaryRow SelectedGroup
        {
            get => _selectedGroup;
            set => SetField(ref _selectedGroup, value);
        }

        private bool? _notCompletedSelectAllState = false;
        public bool? NotCompletedSelectAllState
        {
            get => _notCompletedSelectAllState;
            private set => SetField(ref _notCompletedSelectAllState, value);
        }

        private bool? _completedSelectAllState = false;
        public bool? CompletedSelectAllState
        {
            get => _completedSelectAllState;
            private set => SetField(ref _completedSelectAllState, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand PreviewCommand { get; }
        public RelayCommand CreateInvoiceCommand { get; }

        // ── Sub-Type multi-select filter (Contract / Subscription / License / Services) ──
        public ObservableCollection<CategoryFilterOption> SubTypeFilterOptions { get; } = new ObservableCollection<CategoryFilterOption>();

        private string _subTypeFilterSummary = "All Sub-Types";
        public string SubTypeFilterSummary
        {
            get => _subTypeFilterSummary;
            private set => SetField(ref _subTypeFilterSummary, value);
        }

        public RelayCommand ClearSubTypeFilterCommand { get; }

        private bool _suppressSubTypeFilterChange;

        public InvoicePreparationListViewModel()
        {
            RefreshCommand = new RelayCommand(LoadGroups);
            PreviewCommand = new RelayCommand(PreviewSelected);
            CreateInvoiceCommand = new RelayCommand(CreateInvoiceFromChecked);
            ClearSubTypeFilterCommand = new RelayCommand(ClearSubTypeFilter);

            foreach (var subType in ItemSubTypeCatalog.ValidSubTypes)
            {
                var option = new CategoryFilterOption(subType);
                option.CheckedChanged += OnSubTypeFilterOptionChanged;
                SubTypeFilterOptions.Add(option);
            }
        }

        private void OnSubTypeFilterOptionChanged()
        {
            if (_suppressSubTypeFilterChange) return;
            UpdateSubTypeFilterSummary();
            RefreshPartitions();
        }

        private void ClearSubTypeFilter()
        {
            _suppressSubTypeFilterChange = true;
            try
            {
                foreach (var option in SubTypeFilterOptions) option.IsChecked = false;
            }
            finally
            {
                _suppressSubTypeFilterChange = false;
            }

            UpdateSubTypeFilterSummary();
            RefreshPartitions();
        }

        private void UpdateSubTypeFilterSummary()
        {
            var checkedNames = SubTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            SubTypeFilterSummary = checkedNames.Count == 0
                ? "All Sub-Types"
                : checkedNames.Count == 1
                    ? checkedNames[0]
                    : $"{checkedNames.Count} sub-types selected";
        }

        private bool MatchesSubTypeFilter(InvoicePreparationSummaryRow group)
        {
            var selected = SubTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            return selected.Count == 0 || selected.Contains(group.SubType, StringComparer.OrdinalIgnoreCase);
        }

        public void LoadGroups()
        {
            try
            {
                var rows = _repository.GetAllGroups();
                Groups.Clear();
                foreach (var row in rows) Groups.Add(row);
                RefreshPartitions();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load Sub-Type Groups: {ex.Message}");
            }
        }

        /// <summary>Checks/unchecks every Not Completed group and refreshes both grids —
        /// bound to that tab's checkbox column header.</summary>
        public void ToggleSelectAllNotCompleted(bool selected)
        {
            foreach (var group in NotCompletedGroups) group.Selected = selected;
            RefreshPartitions();
        }

        /// <summary>Checks/unchecks every Completed group and refreshes both grids —
        /// bound to that tab's checkbox column header.</summary>
        public void ToggleSelectAllCompleted(bool selected)
        {
            foreach (var group in CompletedGroups) group.Selected = selected;
            RefreshPartitions();
        }

        /// <summary>Called by the View after a single row's checkbox is toggled, so both
        /// tabs' header tri-state checkboxes stay in sync.</summary>
        public void NotifySelectionChanged() => SyncSelectAllStates();

        /// <summary>Unchecks every group in both tabs — called by the View when the user
        /// switches tabs, so a checkbox checked on one tab doesn't silently carry over and
        /// stay checked (invisibly) when the other tab is shown.</summary>
        public void ClearAllChecked()
        {
            foreach (var group in Groups) group.Selected = false;
            RefreshPartitions();
        }

        private static bool IsCompleted(InvoicePreparationSummaryRow group) =>
            string.Equals(group.Status, "Completed", StringComparison.OrdinalIgnoreCase);

        private void SyncSelectAllStates()
        {
            NotCompletedSelectAllState = ComputeSelectAllState(NotCompletedGroups.ToList());
            CompletedSelectAllState = ComputeSelectAllState(CompletedGroups.ToList());
        }

        private static bool? ComputeSelectAllState(List<InvoicePreparationSummaryRow> groups)
        {
            if (groups.Count == 0) return false;
            int selectedCount = groups.Count(g => g.Selected);
            return selectedCount == 0 ? (bool?)false
                : selectedCount == groups.Count ? (bool?)true
                : null;
        }

        // Re-splits Groups into NotCompletedGroups/CompletedGroups and re-adds every row
        // in each so the DataGrids re-pull the checkbox cell's OneWay binding after
        // Selected changed programmatically (InvoicePreparationSummaryRow has no
        // INotifyPropertyChanged, so the grid otherwise won't notice the mutation).
        private void RefreshPartitions()
        {
            NotCompletedGroups.Clear();
            foreach (var group in Groups.Where(g => !IsCompleted(g) && MatchesSubTypeFilter(g))) NotCompletedGroups.Add(group);

            CompletedGroups.Clear();
            foreach (var group in Groups.Where(g => IsCompleted(g) && MatchesSubTypeFilter(g))) CompletedGroups.Add(group);

            SyncSelectAllStates();
        }

        private void PreviewSelected()
        {
            var checkedGroups = Groups.Where(g => g.Selected).ToList();
            if (checkedGroups.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please check a Sub-Type Group to preview.");
                return;
            }
            if (checkedGroups.Count > 1)
            {
                RequestWarning?.Invoke("Multiple Selected", "Only one Sub-Type Group can be previewed at a time. Please check exactly one.");
                return;
            }

            RequestPreview?.Invoke(checkedGroups[0].PreparationId);
        }

        public void PreviewGroup(int preparationId) => RequestPreview?.Invoke(preparationId);

        private void CreateInvoiceFromChecked()
        {
            var checkedIds = Groups.Where(g => g.Selected).Select(g => g.PreparationId).ToList();
            if (checkedIds.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please check at least one Sub-Type Group to create an invoice from.");
                return;
            }

            RequestCreateInvoice?.Invoke(checkedIds);
        }

        /// <summary>Called by the View after SoftwareServiceSetDialog returns OK with the
        /// new invoice's SetId.</summary>
        public void MarkGroupsInvoiced(List<int> preparationIds, int setId)
        {
            try
            {
                _repository.MarkGroupsCompleted(preparationIds, setId, Session.AppSession.CurrentUserId);
                LoadGroups();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to update the Sub-Type Group(s): {ex.Message}");
            }
        }
    }
}
