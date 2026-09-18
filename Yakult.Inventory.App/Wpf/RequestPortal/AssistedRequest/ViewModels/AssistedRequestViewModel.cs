using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Yakult.Inventory.App.Models.ViewModels;
using ApproverViewModel   = Yakult.Inventory.App.Models.ApproverViewModel;
using ITAuthorizationInfo = Yakult.Inventory.App.Models.ITAuthorizationInfo;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.RequestPortal.NewRequest.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.RequestPortal.AssistedRequest.ViewModels
{
    public class AssistedRequestViewModel : ViewModelBase
    {
        private readonly RequesterPortalService _service;

        // (createdCount, targetDescription, decision, isCartridgeOnly, setId)
        public event Action<int, string, string, bool, int?> RequestSubmitted;

        public ObservableCollection<EmployeeViewModel> AllEmployees { get; }
            = new ObservableCollection<EmployeeViewModel>();

        public ObservableCollection<CartridgeModelAvailabilityViewModel> CartridgeModels { get; }
            = new ObservableCollection<CartridgeModelAvailabilityViewModel>();

        // Category selector — sourced from Models.ConsumableCategories, the single place a new
        // consumable category needs to be added for it to show up here (and be recognized by
        // RequesterPortalService's auto-grouping guard). CartridgeModels above is repopulated
        // per-category (from dbo.CartridgeModel for "Cartridge", dbo.ConsumableModel for the
        // others) so the existing model dropdown binding is reused unchanged.
        public ObservableCollection<string> CategoryOptions { get; }
            = new ObservableCollection<string>(Yakult.Inventory.App.Models.ConsumableCategories.All);

        // Request scope — Option 1 (cartridge only) vs Option 2 (cartridge + other consumables).
        // Option 1 restricts the category picker to "Cartridge" and drives the post-submit
        // redirect offer to the Cartridge Management page.
        private bool _isCartridgeOnlyScope = true;
        public bool IsCartridgeOnlyScope
        {
            get => _isCartridgeOnlyScope;
            set
            {
                if (!SetField(ref _isCartridgeOnlyScope, value)) return;
                OnPropertyChanged(nameof(IsMixedConsumableScope));
                OnPropertyChanged(nameof(AvailableCategoryOptions));
                if (value)
                {
                    var nonCartridge = RequestItems.Where(r => r.Category != "Cartridge").ToList();
                    foreach (var item in nonCartridge) RequestItems.Remove(item);
                    if (SelectedCategory != "Cartridge") SelectedCategory = "Cartridge";
                }
            }
        }

        public bool IsMixedConsumableScope
        {
            get => !_isCartridgeOnlyScope;
            set => IsCartridgeOnlyScope = !value;
        }

        public IEnumerable<string> AvailableCategoryOptions =>
            _isCartridgeOnlyScope ? new[] { "Cartridge" } : (IEnumerable<string>)CategoryOptions;

        private string _selectedCategory = "Cartridge";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetField(ref _selectedCategory, value)) return;
                OnPropertyChanged(nameof(IsCartridgeCategory));
                SelectedModel   = null;
                GoodEmptyQty    = 0;
                DamagedEmptyQty = 0;
                _ = LoadModelsForCategoryAsync(value);
            }
        }

        public bool IsCartridgeCategory => string.IsNullOrWhiteSpace(_selectedCategory) || _selectedCategory == "Cartridge";

        public ObservableCollection<CompanyDto> Companies { get; }
            = new ObservableCollection<CompanyDto>();

        public ObservableCollection<BranchDto> Branches { get; }
            = new ObservableCollection<BranchDto>();

        public ObservableCollection<DepartmentDto> Departments { get; }
            = new ObservableCollection<DepartmentDto>();

        // ── IT Manual Authorization ──────────────────────────────────────────────
        public ObservableCollection<ApproverViewModel> Approvers { get; }
            = new ObservableCollection<ApproverViewModel>();

        private string _approverSearchText = string.Empty;
        public string ApproverSearchText
        {
            get => _approverSearchText;
            set { if (!SetField(ref _approverSearchText, value)) return; _approversView?.Refresh(); }
        }

        private readonly ListCollectionView _approversView;
        public ICollectionView FilteredApprovers => _approversView;

        private bool FilterApprover(object o)
        {
            if (!(o is ApproverViewModel a)) return false;
            var terms = SearchTextHelper.SplitTerms(_approverSearchText);
            return terms.Length == 0 || SearchTextHelper.MatchesAllTerms(terms,
                new[] { a.DisplayName, a.Position, a.DepartmentName, a.BranchName });
        }

        // ── Employee filter ──────────────────────────────────────────────────────
        public ObservableCollection<BranchDto> EmpFilterBranches { get; }
            = new ObservableCollection<BranchDto>();
        public ObservableCollection<DepartmentDto> EmpFilterDepartments { get; }
            = new ObservableCollection<DepartmentDto>();

        // ── Received By filter ───────────────────────────────────────────────────
        public ObservableCollection<BranchDto> RbFilterBranches { get; }
            = new ObservableCollection<BranchDto>();
        public ObservableCollection<DepartmentDto> RbFilterDepartments { get; }
            = new ObservableCollection<DepartmentDto>();

        public ObservableCollection<RequestItemRowViewModel> RequestItems { get; }
            = new ObservableCollection<RequestItemRowViewModel>();

        private bool _isDeptLevel;
        public bool IsDeptLevel
        {
            get => _isDeptLevel;
            set
            {
                if (!SetField(ref _isDeptLevel, value)) return;
                OnPropertyChanged(nameof(ShowTargetEmployee));
                if (value)
                {
                    SelectedEmployee = null;
                    ExecuteClearEmpFilters();
                }
            }
        }

        public bool ShowTargetEmployee => !_isDeptLevel;

        private EmployeeViewModel _selectedEmployee;
        public EmployeeViewModel SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (!SetField(ref _selectedEmployee, value)) return;
                OnPropertyChanged(nameof(EmployeeInfoText));
                if (value != null)
                    ApplyEmployeeOrgToSelections(value);
            }
        }

        public string EmployeeInfoText
        {
            get
            {
                if (_selectedEmployee == null) return "— Select an employee above —";
                return $"{_selectedEmployee.Name}   |   {_selectedEmployee.Position ?? "—"}   |   " +
                       $"{_selectedEmployee.CompanyName ?? "—"}   |   {_selectedEmployee.BranchName ?? "—"}   |   " +
                       $"{_selectedEmployee.DepartmentName ?? "—"}";
            }
        }

        private CompanyDto _selectedCompany;
        public CompanyDto SelectedCompany
        {
            get => _selectedCompany;
            set => SetField(ref _selectedCompany, value);
        }

        private BranchDto _selectedBranch;
        public BranchDto SelectedBranch
        {
            get => _selectedBranch;
            set => SetField(ref _selectedBranch, value);
        }

        private DepartmentDto _selectedDepartment;
        public DepartmentDto SelectedDepartment
        {
            get => _selectedDepartment;
            set => SetField(ref _selectedDepartment, value);
        }

        // ── Employee filter state ────────────────────────────────────────────────
        private CompanyDto _selectedEmpFilterCompany;
        public CompanyDto SelectedEmpFilterCompany
        {
            get => _selectedEmpFilterCompany;
            set
            {
                if (!SetField(ref _selectedEmpFilterCompany, value)) return;
                _ = ReloadEmpFilterBranchesDepartmentsAsync(value?.ComId);
            }
        }

        private BranchDto _selectedEmpFilterBranch;
        public BranchDto SelectedEmpFilterBranch
        {
            get => _selectedEmpFilterBranch;
            set { if (!SetField(ref _selectedEmpFilterBranch, value)) return; _employeesView?.Refresh(); }
        }

        private DepartmentDto _selectedEmpFilterDepartment;
        public DepartmentDto SelectedEmpFilterDepartment
        {
            get => _selectedEmpFilterDepartment;
            set { if (!SetField(ref _selectedEmpFilterDepartment, value)) return; _employeesView?.Refresh(); }
        }

        private string _employeeSearchText = string.Empty;
        public string EmployeeSearchText
        {
            get => _employeeSearchText;
            set { if (!SetField(ref _employeeSearchText, value)) return; _employeesView?.Refresh(); }
        }

        private readonly ListCollectionView _employeesView;
        public ICollectionView FilteredEmployees => _employeesView;

        private bool FilterEmployeeForSelect(object o)
        {
            if (!(o is EmployeeViewModel e)) return false;
            if (_selectedEmpFilterCompany    != null && e.ComId    != _selectedEmpFilterCompany.ComId)    return false;
            if (_selectedEmpFilterBranch     != null && e.BranchId != _selectedEmpFilterBranch.BranchId)  return false;
            if (_selectedEmpFilterDepartment != null && e.DeptId   != _selectedEmpFilterDepartment.DeptId) return false;

            var terms = SearchTextHelper.SplitTerms(_employeeSearchText);
            return terms.Length == 0 || SearchTextHelper.MatchesAllTerms(terms,
                new[] { e.Name, e.EmployeeNumber, e.Position, e.DepartmentName });
        }

        // ── Received By filter state ─────────────────────────────────────────────
        private CompanyDto _selectedRbFilterCompany;
        public CompanyDto SelectedRbFilterCompany
        {
            get => _selectedRbFilterCompany;
            set
            {
                if (!SetField(ref _selectedRbFilterCompany, value)) return;
                _ = ReloadRbFilterBranchesDepartmentsAsync(value?.ComId);
            }
        }

        private BranchDto _selectedRbFilterBranch;
        public BranchDto SelectedRbFilterBranch
        {
            get => _selectedRbFilterBranch;
            set { if (!SetField(ref _selectedRbFilterBranch, value)) return; _receivedByView?.Refresh(); }
        }

        private DepartmentDto _selectedRbFilterDepartment;
        public DepartmentDto SelectedRbFilterDepartment
        {
            get => _selectedRbFilterDepartment;
            set { if (!SetField(ref _selectedRbFilterDepartment, value)) return; _receivedByView?.Refresh(); }
        }

        private string _receivedBySearchText = string.Empty;
        public string ReceivedBySearchText
        {
            get => _receivedBySearchText;
            set { if (!SetField(ref _receivedBySearchText, value)) return; _receivedByView?.Refresh(); }
        }

        private readonly ListCollectionView _receivedByView;
        public ICollectionView FilteredReceivedByEmployees => _receivedByView;

        private bool FilterEmployeeForReceivedBy(object o)
        {
            if (!(o is EmployeeViewModel e)) return false;
            if (_selectedRbFilterCompany    != null && e.ComId    != _selectedRbFilterCompany.ComId)    return false;
            if (_selectedRbFilterBranch     != null && e.BranchId != _selectedRbFilterBranch.BranchId)  return false;
            if (_selectedRbFilterDepartment != null && e.DeptId   != _selectedRbFilterDepartment.DeptId) return false;

            var terms = SearchTextHelper.SplitTerms(_receivedBySearchText);
            return terms.Length == 0 || SearchTextHelper.MatchesAllTerms(terms,
                new[] { e.Name, e.EmployeeNumber, e.Position, e.DepartmentName });
        }

        private CartridgeModelAvailabilityViewModel _selectedModel;
        public CartridgeModelAvailabilityViewModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (!SetField(ref _selectedModel, value)) return;
                OnPropertyChanged(nameof(ShowNoStockWarning));
                Quantity        = 1;
                GoodEmptyQty    = 0;
                DamagedEmptyQty = 0;
            }
        }

        public bool ShowNoStockWarning => false;

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                int clamped = Math.Max(1, value);
                if (!SetField(ref _quantity, clamped)) return;
                if (_goodEmptyQty + _damagedEmptyQty > clamped)
                {
                    _goodEmptyQty    = 0;
                    _damagedEmptyQty = 0;
                    OnPropertyChanged(nameof(GoodEmptyQty));
                    OnPropertyChanged(nameof(DamagedEmptyQty));
                }
                OnPropertyChanged(nameof(MaxGoodQty));
                OnPropertyChanged(nameof(MaxDamagedQty));
            }
        }

        private int _goodEmptyQty;
        public int GoodEmptyQty
        {
            get => _goodEmptyQty;
            set
            {
                int clamped = Math.Max(0, Math.Min(MaxGoodQty, value));
                if (!SetField(ref _goodEmptyQty, clamped)) return;
                OnPropertyChanged(nameof(MaxDamagedQty));
            }
        }

        private int _damagedEmptyQty;
        public int DamagedEmptyQty
        {
            get => _damagedEmptyQty;
            set
            {
                int clamped = Math.Max(0, Math.Min(MaxDamagedQty, value));
                if (!SetField(ref _damagedEmptyQty, clamped)) return;
                OnPropertyChanged(nameof(MaxGoodQty));
            }
        }

        public int MaxGoodQty    => Math.Max(0, _quantity - _damagedEmptyQty);
        public int MaxDamagedQty => Math.Max(0, _quantity - _goodEmptyQty);

        private RequestItemRowViewModel _selectedRequestItem;
        public RequestItemRowViewModel SelectedRequestItem
        {
            get => _selectedRequestItem;
            set => SetField(ref _selectedRequestItem, value);
        }

        public bool HasItems => RequestItems.Count > 0;

        private bool _isPickup = true;
        public bool IsPickup
        {
            get => _isPickup;
            set
            {
                if (!SetField(ref _isPickup, value)) return;
                OnPropertyChanged(nameof(IsDelivery));
                OnPropertyChanged(nameof(ShowReceivedBy));
            }
        }

        public bool IsDelivery
        {
            get => !_isPickup;
            set => IsPickup = !value;
        }

        public bool ShowReceivedBy => _isPickup;

        private EmployeeViewModel _selectedReceivedBy;
        public EmployeeViewModel SelectedReceivedBy
        {
            get => _selectedReceivedBy;
            set => SetField(ref _selectedReceivedBy, value);
        }

        private string _remarks = string.Empty;
        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        // ── IT Manual Authorization ──────────────────────────────────────────────
        private ApproverViewModel _selectedApprover;
        public ApproverViewModel SelectedApprover
        {
            get => _selectedApprover;
            set => SetField(ref _selectedApprover, value);
        }

        private bool _isManualAuthEnabled;
        /// <summary>Mirrors the Manual Authorization toggle. When true, IT records an offline decision.</summary>
        public bool IsManualAuthEnabled
        {
            get => _isManualAuthEnabled;
            set
            {
                if (!SetField(ref _isManualAuthEnabled, value)) return;
                OnPropertyChanged(nameof(ShowManualAuth));
            }
        }

        private bool _isQuickAutoApproved;
        /// <summary>
        /// Shortcut: marks the row as auto-approved without showing the full manual auth form.
        /// Independent of IsManualAuthEnabled — submit treats it as an offline auto-approved decision.
        /// </summary>
        public bool IsQuickAutoApproved
        {
            get => _isQuickAutoApproved;
            set
            {
                if (!SetField(ref _isQuickAutoApproved, value)) return;
                OnPropertyChanged(nameof(ShowManualAuth));
            }
        }

        /// <summary>True when the Manual Authorization section should be visible.</summary>
        public bool ShowManualAuth => _isManualAuthEnabled && !_isQuickAutoApproved;

        private bool _itDecisionAutoApproved = true;
        public bool ITDecisionAutoApproved
        {
            get => _itDecisionAutoApproved;
            set
            {
                if (!SetField(ref _itDecisionAutoApproved, value)) return;
                if (value) { _itDecisionApproved = false; _itDecisionRejected = false; }
                OnPropertyChanged(nameof(ITDecisionApproved));
                OnPropertyChanged(nameof(ITDecisionRejected));
                OnPropertyChanged(nameof(ShowAuthorizedByPicker));
                OnPropertyChanged(nameof(ITRemarksRequired));
            }
        }

        private bool _itDecisionApproved;
        public bool ITDecisionApproved
        {
            get => _itDecisionApproved;
            set
            {
                if (!SetField(ref _itDecisionApproved, value)) return;
                if (value) { _itDecisionAutoApproved = false; _itDecisionRejected = false; }
                OnPropertyChanged(nameof(ITDecisionAutoApproved));
                OnPropertyChanged(nameof(ITDecisionRejected));
                OnPropertyChanged(nameof(ShowAuthorizedByPicker));
                OnPropertyChanged(nameof(ITRemarksRequired));
            }
        }

        private bool _itDecisionRejected;
        public bool ITDecisionRejected
        {
            get => _itDecisionRejected;
            set
            {
                if (!SetField(ref _itDecisionRejected, value)) return;
                if (value) { _itDecisionAutoApproved = false; _itDecisionApproved = false; }
                OnPropertyChanged(nameof(ITDecisionAutoApproved));
                OnPropertyChanged(nameof(ITDecisionApproved));
                OnPropertyChanged(nameof(ShowAuthorizedByPicker));
                OnPropertyChanged(nameof(ITRemarksRequired));
            }
        }

        public bool ShowAuthorizedByPicker => !_itDecisionAutoApproved;
        public bool ITRemarksRequired => _itDecisionRejected;

        private string _itAuthRemarks = string.Empty;
        public string ITAuthRemarks
        {
            get => _itAuthRemarks;
            set => SetField(ref _itAuthRemarks, value);
        }
        // ────────────────────────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        private string _validationError = string.Empty;
        public string ValidationError
        {
            get => _validationError;
            set
            {
                if (!SetField(ref _validationError, value)) return;
                OnPropertyChanged(nameof(HasValidationError));
            }
        }

        public bool HasValidationError => !string.IsNullOrWhiteSpace(_validationError);

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public ICommand AddItemCommand            { get; }
        public ICommand RemoveItemCommand         { get; }
        public ICommand SubmitCommand             { get; }
        public ICommand ClearCommand              { get; }
        public ICommand IncrementQuantityCommand  { get; }
        public ICommand DecrementQuantityCommand  { get; }
        public ICommand IncrementGoodCommand      { get; }
        public ICommand DecrementGoodCommand      { get; }
        public ICommand IncrementDamagedCommand   { get; }
        public ICommand DecrementDamagedCommand   { get; }
        public ICommand ClearEmpFiltersCommand    { get; }
        public ICommand ClearRbFiltersCommand     { get; }

        public AssistedRequestViewModel()
        {
            _service = new RequesterPortalService();

            AddItemCommand           = new RelayCommand(ExecuteAddItem,     () => !_isLoading);
            RemoveItemCommand        = new RelayCommand<RequestItemRowViewModel>(ExecuteRemoveItem);
            SubmitCommand            = new RelayCommand(async () => await SubmitAsync(), () => !_isLoading);
            ClearCommand             = new RelayCommand(ExecuteClear,       () => !_isLoading);
            IncrementQuantityCommand = new RelayCommand(() => Quantity++,        () => true);
            DecrementQuantityCommand = new RelayCommand(() => Quantity--,        () => _quantity > 1);
            IncrementGoodCommand     = new RelayCommand(() => GoodEmptyQty++,    () => _goodEmptyQty < MaxGoodQty);
            DecrementGoodCommand     = new RelayCommand(() => GoodEmptyQty--,    () => _goodEmptyQty > 0);
            IncrementDamagedCommand  = new RelayCommand(() => DamagedEmptyQty++, () => _damagedEmptyQty < MaxDamagedQty);
            DecrementDamagedCommand  = new RelayCommand(() => DamagedEmptyQty--, () => _damagedEmptyQty > 0);
            ClearEmpFiltersCommand   = new RelayCommand(ExecuteClearEmpFilters);
            ClearRbFiltersCommand    = new RelayCommand(ExecuteClearRbFilters);

            RequestItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasItems));

            // Independent ListCollectionView instances (not CollectionViewSource.GetDefaultView,
            // which would return the same shared view for both employee comboboxes since they
            // wrap the same AllEmployees source) so Select Employee and Received By can filter
            // independently. Refresh() is called from the search/filter setters above instead of
            // rebinding a brand-new IEnumerable per keystroke — keeps typing responsive.
            _employeesView  = new ListCollectionView(AllEmployees) { Filter = FilterEmployeeForSelect };
            _receivedByView = new ListCollectionView(AllEmployees) { Filter = FilterEmployeeForReceivedBy };
            _approversView  = new ListCollectionView(Approvers)    { Filter = FilterApprover };
        }

        public async Task LoadDataAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            try
            {
                var modelsTask     = Task.Run(() => _service.GetCartridgeModelsWithAvailability());
                var employeesTask  = Task.Run(() => _service.GetActiveEmployees());
                var companiesTask  = Task.Run(() => _service.GetActiveCompanies());
                var branchesTask   = Task.Run(() => _service.GetAllActiveBranches());
                var deptsTask      = Task.Run(() => _service.GetAllActiveDepartments());

                await Task.WhenAll(modelsTask, employeesTask, companiesTask, branchesTask, deptsTask);

                CartridgeModels.Clear();
                foreach (var m in modelsTask.Result) CartridgeModels.Add(m);

                AllEmployees.Clear();
                foreach (var e in employeesTask.Result) AllEmployees.Add(e);
                // _employeesView / _receivedByView wrap AllEmployees directly and stay in sync
                // via CollectionChanged — no manual refresh needed here.

                Companies.Clear();
                foreach (var c in companiesTask.Result) Companies.Add(c);

                Branches.Clear();
                foreach (var b in branchesTask.Result) Branches.Add(b);

                Departments.Clear();
                foreach (var d in deptsTask.Result) Departments.Add(d);

                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void RefreshModels(IEnumerable<CartridgeModelAvailabilityViewModel> models)
        {
            if (models == null) return;
            var currentKey = _selectedModel?.ModelKey;
            CartridgeModels.Clear();
            foreach (var m in models) CartridgeModels.Add(m);
            if (currentKey != null)
            {
                SelectedModel = CartridgeModels.FirstOrDefault(m =>
                    string.Equals(m.ModelKey, currentKey, StringComparison.OrdinalIgnoreCase));
            }
        }

        private async Task LoadModelsForCategoryAsync(string category)
        {
            IsLoading = true;
            try
            {
                var models = await Task.Run(() =>
                    string.IsNullOrWhiteSpace(category) || category == "Cartridge"
                        ? _service.GetCartridgeModelsWithAvailability()
                        : _service.GetConsumableModelsWithAvailability(category));

                CartridgeModels.Clear();
                foreach (var m in models) CartridgeModels.Add(m);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading {category} models: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReloadEmpFilterBranchesDepartmentsAsync(int? companyId)
        {
            EmpFilterBranches.Clear();
            EmpFilterDepartments.Clear();
            _selectedEmpFilterBranch     = null;
            _selectedEmpFilterDepartment = null;
            OnPropertyChanged(nameof(SelectedEmpFilterBranch));
            OnPropertyChanged(nameof(SelectedEmpFilterDepartment));
            _employeesView?.Refresh();

            if (!companyId.HasValue || companyId.Value <= 0) return;

            try
            {
                var branchesTask = Task.Run(() => _service.GetBranchesByCompany(companyId.Value));
                var deptsTask    = Task.Run(() => _service.GetDepartmentsByCompany(companyId.Value));
                await Task.WhenAll(branchesTask, deptsTask);
                foreach (var b in branchesTask.Result) EmpFilterBranches.Add(b);
                foreach (var d in deptsTask.Result)    EmpFilterDepartments.Add(d);
            }
            catch { }
        }

        private async Task ReloadRbFilterBranchesDepartmentsAsync(int? companyId)
        {
            RbFilterBranches.Clear();
            RbFilterDepartments.Clear();
            _selectedRbFilterBranch     = null;
            _selectedRbFilterDepartment = null;
            OnPropertyChanged(nameof(SelectedRbFilterBranch));
            OnPropertyChanged(nameof(SelectedRbFilterDepartment));
            _receivedByView?.Refresh();

            if (!companyId.HasValue || companyId.Value <= 0) return;

            try
            {
                var branchesTask = Task.Run(() => _service.GetBranchesByCompany(companyId.Value));
                var deptsTask    = Task.Run(() => _service.GetDepartmentsByCompany(companyId.Value));
                await Task.WhenAll(branchesTask, deptsTask);
                foreach (var b in branchesTask.Result) RbFilterBranches.Add(b);
                foreach (var d in deptsTask.Result)    RbFilterDepartments.Add(d);
            }
            catch { }
        }

        private async void ApplyEmployeeOrgToSelections(EmployeeViewModel emp)
        {
            _selectedCompany    = Companies.FirstOrDefault(c => c.ComId    == emp.ComId);
            _selectedBranch     = Branches.FirstOrDefault(b  => b.BranchId == emp.BranchId);
            _selectedDepartment = Departments.FirstOrDefault(d => d.DeptId  == emp.DeptId);
            OnPropertyChanged(nameof(SelectedCompany));
            OnPropertyChanged(nameof(SelectedBranch));
            OnPropertyChanged(nameof(SelectedDepartment));

            if (emp.ComId > 0 && emp.BranchId > 0 && emp.DeptId > 0)
                await LoadApproversAsync(emp.ComId, emp.BranchId, emp.DeptId);
        }

        private string _approverDebugMessage;
        public string ApproverDebugMessage
        {
            get => _approverDebugMessage;
            set => SetField(ref _approverDebugMessage, value);
        }

        private async Task LoadApproversAsync(int comId, int branchId, int deptId)
        {
            Approvers.Clear();
            SelectedApprover  = null;
            ApproverSearchText = string.Empty;
            ApproverDebugMessage = $"DEBUG: Loading approvers for ComId={comId} BranchId={branchId} DeptId={deptId}…";
            try
            {
                var list = await Task.Run(() => _service.GetApproversByScope(comId, branchId, deptId));
                foreach (var a in list) Approvers.Add(a);
                ApproverDebugMessage = list.Count > 0
                    ? $"DEBUG: {list.Count} approver(s) — {string.Join(", ", list.Select(a => a.DisplayName + " (" + a.ApprovalRole + ")"))}"
                    : $"DEBUG: 0 approvers returned for ComId={comId} BranchId={branchId} DeptId={deptId}";
            }
            catch (Exception ex)
            {
                ApproverDebugMessage = $"DEBUG ERROR: {ex.GetType().Name}: {ex.Message}";
            }
        }
        private void ExecuteAddItem()
        {
            ValidationError = string.Empty;

            if (_selectedModel == null)
            {
                ValidationError = IsCartridgeCategory ? "Please select a cartridge model." : $"Please select a {_selectedCategory} model.";
                return;
            }

            if (_quantity <= 0)
            {
                ValidationError = "Quantity must be at least 1.";
                return;
            }

            // Good/Damaged empty-cartridge return tracking only applies to the Cartridge category.
            bool returningEmpties = _goodEmptyQty > 0 || _damagedEmptyQty > 0;
            if (IsCartridgeCategory && returningEmpties && _goodEmptyQty + _damagedEmptyQty != _quantity)
            {
                ValidationError = $"Good ({_goodEmptyQty}) + Damaged ({_damagedEmptyQty}) must equal the requested quantity ({_quantity}) when returning empties.";
                return;
            }

            var existing = RequestItems.FirstOrDefault(x =>
                string.Equals(x.ModelKey, _selectedModel.ModelKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Quantity = existing.Quantity + _quantity;
                StatusMessage = $"Updated '{existing.CartridgeModel}' quantity to {existing.Quantity}.";
                return;
            }

            RequestItems.Add(new RequestItemRowViewModel
            {
                Category        = _selectedCategory,
                CartridgeModel  = _selectedModel.ModelNumber ?? _selectedModel.ItemName,
                ModelKey        = _selectedModel.ModelKey,
                Quantity        = _quantity,
                GoodEmptyQty    = IsCartridgeCategory ? _goodEmptyQty    : 0,
                DamagedEmptyQty = IsCartridgeCategory ? _damagedEmptyQty : 0
            });

            Quantity        = 1;
            GoodEmptyQty    = 0;
            DamagedEmptyQty = 0;
            StatusMessage   = $"Added '{_selectedModel.ModelNumber ?? _selectedModel.ItemName}'.";
        }

        private void ExecuteRemoveItem(RequestItemRowViewModel item)
        {
            if (item != null) RequestItems.Remove(item);
        }

        private void ExecuteClear()
        {
            IsCartridgeOnlyScope      = true;
            IsDeptLevel               = false;
            SelectedEmployee          = null;
            EmployeeSearchText        = string.Empty;
            SelectedModel             = null;
            Quantity                  = 1;
            GoodEmptyQty              = 0;
            DamagedEmptyQty           = 0;
            RequestItems.Clear();
            _selectedCompany          = null;
            _selectedBranch           = null;
            _selectedDepartment       = null;
            OnPropertyChanged(nameof(SelectedCompany));
            OnPropertyChanged(nameof(SelectedBranch));
            OnPropertyChanged(nameof(SelectedDepartment));
            IsPickup                  = true;
            SelectedReceivedBy        = null;
            ReceivedBySearchText      = string.Empty;
            Remarks                   = string.Empty;
            Approvers.Clear();
            SelectedApprover          = null;
            ApproverSearchText        = string.Empty;
            IsManualAuthEnabled       = false;
            IsQuickAutoApproved       = false;
            ITDecisionAutoApproved    = true;
            ITAuthRemarks             = string.Empty;
            ApproverDebugMessage      = string.Empty;
            ValidationError           = string.Empty;
            StatusMessage             = string.Empty;
            ExecuteClearEmpFilters();
            ExecuteClearRbFilters();
        }

        private void ExecuteClearEmpFilters()
        {
            SelectedEmpFilterCompany = null;
        }

        private void ExecuteClearRbFilters()
        {
            SelectedRbFilterCompany = null;
        }

        // Describes who/what the request was submitted for, for use in the post-submit
        // success message. For dept-level requests (no employee) this surfaces the
        // Company/Branch/Department values instead of just "(No employee)".
        private string BuildTargetDescription()
        {
            if (_selectedEmployee != null) return _selectedEmployee.Name;

            var parts = new List<string>();
            if (_selectedCompany != null) parts.Add($"Company: {_selectedCompany.Name}");
            if (_selectedBranch != null) parts.Add($"Branch: {_selectedBranch.Name}");
            if (_selectedDepartment != null) parts.Add($"Department: {_selectedDepartment.Name}");

            return parts.Count > 0
                ? $"(No employee) — {string.Join(", ", parts)}"
                : "(No employee)";
        }

        private async Task SubmitAsync()
        {
            ValidationError = string.Empty;

            if (RequestItems.Count == 0)
            {
                ValidationError = "Please add at least one cartridge model to the request.";
                return;
            }

            int destCompanyId    = _selectedCompany?.ComId    ?? _selectedEmployee?.ComId    ?? 0;
            int destBranchId     = _selectedBranch?.BranchId  ?? _selectedEmployee?.BranchId ?? 0;
            int destDepartmentId = _selectedDepartment?.DeptId ?? _selectedEmployee?.DeptId   ?? 0;

            // When Manual Authorization is ON and not Auto-Approved, approver is required
            if (_isManualAuthEnabled && !_itDecisionAutoApproved)
            {
                if (_selectedApprover == null && _itDecisionRejected)
                {
                    ValidationError = "Please select the authorizing approver when rejecting.";
                    return;
                }
                if (_itDecisionRejected && string.IsNullOrWhiteSpace(_itAuthRemarks))
                {
                    ValidationError = "Remarks are required when the authorization decision is Rejected.";
                    return;
                }
            }

            IsLoading     = true;
            StatusMessage = "Submitting...";

            try
            {
                var requestViewModel = new CartridgeRequestViewModel
                {
                    EmployeeName            = _selectedEmployee?.Name ?? string.Empty,
                    EmployeePosition        = _selectedEmployee?.Position ?? string.Empty,
                    DestinationCompanyId    = destCompanyId,
                    DestinationBranchId     = destBranchId,
                    DestinationDepartmentId = destDepartmentId,
                    FulfillmentMethod       = IsPickup ? "PICKUP" : "DELIVERY",
                    AdditionalRemarks       = _remarks?.Trim() ?? string.Empty,
                    ReceivedById            = IsPickup && _selectedReceivedBy != null
                                              ? _selectedReceivedBy.EmpId : (int?)null
                };

                bool useManualAuth = _isManualAuthEnabled || _isQuickAutoApproved;
                string manualDecision = _isQuickAutoApproved ? "Approved"
                                      : _itDecisionAutoApproved ? "Approved"
                                      : _itDecisionRejected     ? "Rejected"
                                      : "Approved";
                var itAuth = new ITAuthorizationInfo
                {
                    AuthorizedByEmpId = useManualAuth && !_itDecisionAutoApproved && !_isQuickAutoApproved
                                        ? (_selectedApprover?.EmpId ?? 0) : 0,
                    UsePortalFlow     = !useManualAuth,
                    Decision          = useManualAuth ? manualDecision : null,
                    Remarks           = useManualAuth ? _itAuthRemarks?.Trim() : null
                };

                var serviceItems  = RequestItems.Select(r => r.ToServiceModel()).ToList();
                int currentUserId = AppSession.CurrentUserId;
                int targetEmpId   = _selectedEmployee?.EmpId ?? 0;

                StatusMessage = "Submitting...";

                var (createdIds, wasAutoApproved, authId, setId) = await Task.Run(() =>
                    _service.CreateCartridgeRequestByModel(
                        requestViewModel, serviceItems, targetEmpId, currentUserId,
                        isAssisted: true, itAuth: itAuth));

                string empName = _selectedEmployee?.Name ?? "(No employee)";
                string targetDescription = BuildTargetDescription();
                bool   wasCartridgeOnly  = _isCartridgeOnlyScope;
                int    count   = createdIds?.Count ?? 0;
                string outcome = itAuth.UsePortalFlow
                    ? "submitted for approval"
                    : $"{itAuth.Decision!.ToLower()} and submitted";

                ExecuteClear();

                var updated = await Task.Run(() =>
                    IsCartridgeCategory
                        ? _service.GetCartridgeModelsWithAvailability()
                        : _service.GetConsumableModelsWithAvailability(_selectedCategory));
                RefreshModels(updated);

                StatusMessage = $"Request {outcome} for {empName}.";
                RequestSubmitted?.Invoke(count, targetDescription, itAuth.UsePortalFlow ? "Pending" : itAuth.Decision, wasCartridgeOnly, setId);
            }
            catch (Exception ex)
            {
                ValidationError = $"Error submitting request: {ex.Message}";
                StatusMessage   = string.Empty;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
