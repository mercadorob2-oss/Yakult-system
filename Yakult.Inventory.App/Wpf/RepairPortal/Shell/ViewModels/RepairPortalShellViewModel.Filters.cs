using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels
{
    /// <summary>Search/filter/sort state — the WHERE/ORDER BY is composed server-side by the
    /// repository (RepairTicketFilterCriteria), unlike RepairedItems' client-side LINQ filtering,
    /// because attachment thumbnails (VARBINARY) are joined in-query.</summary>
    public sealed partial class RepairPortalShellViewModel
    {
        public ObservableCollection<OrgLookupOption> CompanyOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> BranchOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> DepartmentOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> TechnicianOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<string> PriorityOptions { get; } = new ObservableCollection<string> { "Low", "Medium", "High", "Critical" };

        /// <summary>Multi-select checkbox list, populated from GetDistinctTicketCategoriesAsync —
        /// only categories that actually have at least one ticket right now show up here, not the
        /// full system-wide category catalog.</summary>
        public ObservableCollection<CategoryCheckItem> CategoryOptions { get; } = new ObservableCollection<CategoryCheckItem>();
        public ObservableCollection<QuickFilterOption> QuickFilterOptions { get; } = new ObservableCollection<QuickFilterOption>
        {
            new QuickFilterOption("All", string.Empty),
            new QuickFilterOption("Waiting", "Waiting"),
            new QuickFilterOption("Diagnosing", "Diagnosing"),
            new QuickFilterOption("Repairing", "Repairing"),
            new QuickFilterOption("Needs Parts", "NeedsParts"),
            new QuickFilterOption("Completed", "Completed"),
            new QuickFilterOption("Unrepairable", "Unrepairable"),
            new QuickFilterOption("High Priority", "HighPriority")
        };

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    ScheduleSaveState();
                    if (IsDisposed) return;
                    _searchDebounceTimer.Stop();
                    _searchDebounceTimer.Start();
                }
            }
        }

        private OrgLookupOption _selectedCompany;
        public OrgLookupOption SelectedCompany
        {
            get => _selectedCompany;
            set { if (SetField(ref _selectedCompany, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        private OrgLookupOption _selectedBranch;
        public OrgLookupOption SelectedBranch
        {
            get => _selectedBranch;
            set { if (SetField(ref _selectedBranch, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        private OrgLookupOption _selectedDepartment;
        public OrgLookupOption SelectedDepartment
        {
            get => _selectedDepartment;
            set { if (SetField(ref _selectedDepartment, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        private OrgLookupOption _selectedTechnician;
        public OrgLookupOption SelectedTechnician
        {
            get => _selectedTechnician;
            set { if (SetField(ref _selectedTechnician, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        private string _selectedPriority;
        public string SelectedPriority
        {
            get => _selectedPriority;
            set { if (SetField(ref _selectedPriority, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        private string _quickFilterChip = string.Empty;
        public string QuickFilterChip
        {
            get => _quickFilterChip;
            set
            {
                if (SetField(ref _quickFilterChip, value))
                {
                    ScheduleSaveState();
                    _ = LoadTicketsAsync();
                    OnPropertyChanged(nameof(IsAllCardActive));
                    OnPropertyChanged(nameof(IsWaitingCardActive));
                    OnPropertyChanged(nameof(IsDiagnosingCardActive));
                    OnPropertyChanged(nameof(IsRepairingCardActive));
                    OnPropertyChanged(nameof(IsAwaitingPartsCardActive));
                    OnPropertyChanged(nameof(IsCompletedCardActive));
                    OnPropertyChanged(nameof(IsUnrepairableCardActive));
                }
            }
        }

        // Summary-card highlight state — each card's border tints when it's the active Quick
        // Filter, same visual as WarrantyPageView's clickable summary cards.
        public bool IsAllCardActive => QuickFilterChip == string.Empty;
        public bool IsWaitingCardActive => QuickFilterChip == "Waiting";
        public bool IsDiagnosingCardActive => QuickFilterChip == "Diagnosing";
        public bool IsRepairingCardActive => QuickFilterChip == "Repairing";
        public bool IsAwaitingPartsCardActive => QuickFilterChip == "NeedsParts";
        public bool IsCompletedCardActive => QuickFilterChip == "Completed";
        public bool IsUnrepairableCardActive => QuickFilterChip == "Unrepairable";

        public RelayCommand<string> SetQuickFilterCommand { get; private set; }

        private bool _hideCompleted = true;
        /// <summary>Excludes Status = 'Completed' — on by default since completed tickets (including
        /// resolved-Unrepairable ones, which also become Completed) are clutter once resolved.</summary>
        public bool HideCompleted
        {
            get => _hideCompleted;
            set { if (SetField(ref _hideCompleted, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        private string _sortKey = "DateReceivedDesc";
        public string SortKey
        {
            get => _sortKey;
            set { if (SetField(ref _sortKey, value)) { ScheduleSaveState(); _ = LoadTicketsAsync(); } }
        }

        public RelayCommand ClearFiltersCommand { get; private set; }

        private void InitFilters()
        {
            // Toggling the same card's chip again clears it back to "All", same as re-clicking
            // an already-active Warranty summary card.
            SetQuickFilterCommand = new RelayCommand<string>(chip =>
            {
                QuickFilterChip = QuickFilterChip == chip ? string.Empty : chip;
            });

            ClearFiltersCommand = new RelayCommand(() =>
            {
                _searchText = string.Empty; OnPropertyChanged(nameof(SearchText));
                _selectedCompany = null; OnPropertyChanged(nameof(SelectedCompany));
                _selectedBranch = null; OnPropertyChanged(nameof(SelectedBranch));
                _selectedDepartment = null; OnPropertyChanged(nameof(SelectedDepartment));
                _selectedTechnician = null; OnPropertyChanged(nameof(SelectedTechnician));
                _selectedPriority = null; OnPropertyChanged(nameof(SelectedPriority));
                _quickFilterChip = string.Empty; OnPropertyChanged(nameof(QuickFilterChip));
                foreach (var c in CategoryOptions) c.IsSelected = false;
                _ = LoadTicketsAsync();
                ScheduleSaveState();
            });
        }

        private async System.Threading.Tasks.Task LoadLookupsAsync()
        {
            try
            {
                var companies = await _repository.GetCompanyOptionsAsync();
                var branches = await _repository.GetBranchOptionsAsync();
                var departments = await _repository.GetDepartmentOptionsAsync();
                var technicians = await _repository.GetTechnicianOptionsAsync();
                var categories = await _repository.GetDistinctTicketCategoriesAsync();

                CompanyOptions.Clear();
                foreach (var c in companies) CompanyOptions.Add(c);

                BranchOptions.Clear();
                foreach (var b in branches) BranchOptions.Add(b);

                DepartmentOptions.Clear();
                foreach (var d in departments) DepartmentOptions.Add(d);

                TechnicianOptions.Clear();
                foreach (var t in technicians) TechnicianOptions.Add(t);

                // Preserve any currently-checked categories (e.g. restored from saved state)
                // rather than blowing away the selection every time this refreshes.
                var previouslySelected = new HashSet<string>(CategoryOptions.Where(c => c.IsSelected).Select(c => c.Name));
                foreach (var c in CategoryOptions) c.PropertyChanged -= CategoryOption_PropertyChanged;
                CategoryOptions.Clear();
                foreach (var name in categories)
                {
                    var item = new CategoryCheckItem(name) { IsSelected = previouslySelected.Contains(name) };
                    item.PropertyChanged += CategoryOption_PropertyChanged;
                    CategoryOptions.Add(item);
                }
            }
            catch
            {
                // Filter dropdowns are non-critical; leave them empty on failure.
            }
        }

        private void CategoryOption_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CategoryCheckItem.IsSelected)) return;
            ScheduleSaveState();
            _ = LoadTicketsAsync();
        }

        private RepairTicketFilterCriteria BuildCriteria()
        {
            var selectedCategories = CategoryOptions.Where(c => c.IsSelected).Select(c => c.Name).ToList();

            return new RepairTicketFilterCriteria
            {
                SearchText = SearchText,
                ComId = SelectedCompany?.Id,
                BranchId = SelectedBranch?.Id,
                DeptId = SelectedDepartment?.Id,
                Priority = SelectedPriority,
                AssignedTechEmpId = SelectedTechnician?.Id,
                QuickFilterChip = QuickFilterChip,
                HideCompleted = HideCompleted,
                Categories = selectedCategories.Count > 0 ? selectedCategories : null,
                SortKey = string.IsNullOrWhiteSpace(SortKey) ? "DateReceivedDesc" : SortKey
            };
        }
    }

    /// <summary>Display label / filter value pair for the Quick Filter dropdown.</summary>
    public sealed class QuickFilterOption
    {
        public string Label { get; }
        public string Value { get; }

        public QuickFilterOption(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    /// <summary>Checkbox item for the multi-select Category filter.</summary>
    public sealed class CategoryCheckItem : INotifyPropertyChanged
    {
        public string Name { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public CategoryCheckItem(string name)
        {
            Name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
