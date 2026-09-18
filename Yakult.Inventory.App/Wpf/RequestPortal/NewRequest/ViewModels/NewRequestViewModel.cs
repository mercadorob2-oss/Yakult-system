using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Models.ViewModels;
using CompanyDto    = Yakult.Inventory.App.Services.CompanyDto;
using BranchDto     = Yakult.Inventory.App.Services.BranchDto;
using DepartmentDto = Yakult.Inventory.App.Services.DepartmentDto;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestPortal.NewRequest.ViewModels
{
    public class NewRequestViewModel : ViewModelBase
    {
        private readonly RequesterPortalService _service;
        private bool _isDataLoaded;

        public event Action<List<int>, bool, int> RequestSubmitted;

        public ObservableCollection<CartridgeModelAvailabilityViewModel> CartridgeModels { get; }
            = new ObservableCollection<CartridgeModelAvailabilityViewModel>();

        // Category selector — sourced from Models.ConsumableCategories, the single place a new
        // consumable category needs to be added for it to show up here (and be recognized by
        // RequesterPortalService's auto-grouping guard). CartridgeModels above is repopulated
        // per-category (from dbo.CartridgeModel for "Cartridge", dbo.ConsumableModel for the
        // others) so the existing model dropdown binding is reused unchanged.
        public ObservableCollection<string> CategoryOptions { get; }
            = new ObservableCollection<string>(Yakult.Inventory.App.Models.ConsumableCategories.All);

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

        public ObservableCollection<EmployeeViewModel> AllEmployees { get; }
            = new ObservableCollection<EmployeeViewModel>();

        public ObservableCollection<EmployeeViewModel> DeptEmployees { get; }
            = new ObservableCollection<EmployeeViewModel>();

        // ── Received By org filters ───────────────────────────────────────────
        public ObservableCollection<CompanyDto> RbCompanies { get; }
            = new ObservableCollection<CompanyDto>();

        public ObservableCollection<BranchDto> RbBranches { get; }
            = new ObservableCollection<BranchDto>();

        public ObservableCollection<DepartmentDto> RbDepartments { get; }
            = new ObservableCollection<DepartmentDto>();

        private bool _rbListCollapsed;
        private bool _syncingRbFilters;

        private void ExpandRbList()
        {
            if (_syncingRbFilters) return;
            _rbListCollapsed = false;
            OnPropertyChanged(nameof(ShowRbList));
        }

        private CompanyDto _selectedRbCompany;
        public CompanyDto SelectedRbCompany
        {
            get => _selectedRbCompany;
            set { if (!SetField(ref _selectedRbCompany, value)) return; OnPropertyChanged(nameof(FilteredReceivedByEmployees)); ExpandRbList(); }
        }

        private BranchDto _selectedRbBranch;
        public BranchDto SelectedRbBranch
        {
            get => _selectedRbBranch;
            set { if (!SetField(ref _selectedRbBranch, value)) return; OnPropertyChanged(nameof(FilteredReceivedByEmployees)); ExpandRbList(); }
        }

        private DepartmentDto _selectedRbDepartment;
        public DepartmentDto SelectedRbDepartment
        {
            get => _selectedRbDepartment;
            set { if (!SetField(ref _selectedRbDepartment, value)) return; OnPropertyChanged(nameof(FilteredReceivedByEmployees)); ExpandRbList(); }
        }

        private string _rbSearchText = string.Empty;
        public string RbSearchText
        {
            get => _rbSearchText;
            set { if (!SetField(ref _rbSearchText, value)) return; OnPropertyChanged(nameof(FilteredReceivedByEmployees)); ExpandRbList(); }
        }

        public bool ShowRbList =>
            !_rbListCollapsed &&
            (_selectedRbCompany != null || _selectedRbBranch != null ||
             _selectedRbDepartment != null || !string.IsNullOrWhiteSpace(_rbSearchText));

        public IEnumerable<EmployeeViewModel> FilteredReceivedByEmployees
        {
            get
            {
                var result = AllEmployees.AsEnumerable();
                if (_selectedRbCompany    != null) result = result.Where(e => e.ComId    == _selectedRbCompany.ComId);
                if (_selectedRbBranch     != null) result = result.Where(e => e.BranchId == _selectedRbBranch.BranchId);
                if (_selectedRbDepartment != null) result = result.Where(e => e.DeptId   == _selectedRbDepartment.DeptId);
                if (!string.IsNullOrWhiteSpace(_rbSearchText))
                    result = result.Where(e => e.Name != null &&
                        e.Name.IndexOf(_rbSearchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
                return result.OrderBy(e => e.Name);
            }
        }

        public ICommand ClearRbFiltersCommand { get; }

        public ObservableCollection<RequestItemRowViewModel> RequestItems { get; }
            = new ObservableCollection<RequestItemRowViewModel>();

        private bool _isDeptAccountMode;
        public bool IsDeptAccountMode
        {
            get => _isDeptAccountMode;
            private set => SetField(ref _isDeptAccountMode, value);
        }

        private string _requesterDisplay = "Loading...";
        public string RequesterDisplay
        {
            get => _requesterDisplay;
            private set => SetField(ref _requesterDisplay, value);
        }

        private string _requesterOrgDisplay = string.Empty;
        public string RequesterOrgDisplay
        {
            get => _requesterOrgDisplay;
            private set => SetField(ref _requesterOrgDisplay, value);
        }

        private bool _hasLinkedEmployee = true;
        public bool HasLinkedEmployee
        {
            get => _hasLinkedEmployee;
            private set => SetField(ref _hasLinkedEmployee, value);
        }

        private EmployeeViewModel _selectedDeptEmployee;
        public EmployeeViewModel SelectedDeptEmployee
        {
            get => _selectedDeptEmployee;
            set
            {
                if (!SetField(ref _selectedDeptEmployee, value)) return;
                OnPropertyChanged(nameof(DeptEmployeeInfoText));
            }
        }

        public string DeptEmployeeInfoText
        {
            get
            {
                if (_selectedDeptEmployee == null) return "— Select an employee above —";
                return $"{_selectedDeptEmployee.Name}   |   {_selectedDeptEmployee.Position ?? "—"}   |   " +
                       $"{_selectedDeptEmployee.CompanyName ?? "—"}   |   {_selectedDeptEmployee.BranchName ?? "—"}   |   " +
                       $"{_selectedDeptEmployee.DepartmentName ?? "—"}";
            }
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
            set
            {
                if (!SetField(ref _selectedReceivedBy, value)) return;
                if (value != null)
                {
                    _rbListCollapsed = true;
                    OnPropertyChanged(nameof(ShowRbList));
                    _ = SyncRbFiltersToEmployeeAsync(value);
                }
            }
        }

        private string _remarks = string.Empty;
        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

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

        public NewRequestViewModel()
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
            ClearRbFiltersCommand    = new RelayCommand(ExecuteClearRbFilters);

            RequestItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasItems));

            IsDeptAccountMode = AppSession.IsDepartmentAccountSession;
            InitializeIdentityDisplay();
        }

        private void InitializeIdentityDisplay()
        {
            if (IsDeptAccountMode) return;

            string empName  = AppSession.CurrentEmployeeName     ?? AppSession.CurrentUserName ?? "(Unknown)";
            string empPos   = AppSession.CurrentEmployeePosition ?? "—";
            string compName = AppSession.CurrentCompanyName      ?? "—";
            string branch   = AppSession.CurrentBranchName       ?? "—";
            string dept     = AppSession.CurrentDepartmentName   ?? "—";

            RequesterDisplay    = $"Requesting for: {empName}   |   {empPos}";
            RequesterOrgDisplay = $"{compName}   |   {branch}   |   {dept}";
            HasLinkedEmployee   = AppSession.CurrentEmployeeId.HasValue && AppSession.CurrentEmployeeId.Value > 0;
        }

        public async Task LoadDataAsync()
        {
            if (_isDataLoaded) return;
            IsLoading     = true;
            StatusMessage = "Loading...";
            try
            {
                var modelsTask    = Task.Run(() => _service.GetCartridgeModelsWithAvailability());
                var employeesTask = Task.Run(() => _service.GetActiveEmployees());
                var companiesTask = Task.Run(() => _service.GetActiveCompanies());

                await Task.WhenAll(modelsTask, employeesTask, companiesTask);

                var models    = modelsTask.Result;
                var employees = employeesTask.Result;
                var companies = companiesTask.Result;

                CartridgeModels.Clear();
                foreach (var m in models) CartridgeModels.Add(m);

                AllEmployees.Clear();
                foreach (var e in employees) AllEmployees.Add(e);
                OnPropertyChanged(nameof(FilteredReceivedByEmployees));

                RbCompanies.Clear();
                foreach (var c in companies) RbCompanies.Add(c);

                // Pre-populate branch/dept lists from employees so they show before a company is chosen
                RbBranches.Clear();
                foreach (var b in employees
                    .Where(e => e.BranchId > 0 && !string.IsNullOrEmpty(e.BranchName))
                    .Select(e => new BranchDto { BranchId = e.BranchId, Name = e.BranchName, ComId = e.ComId })
                    .GroupBy(b => b.BranchId).Select(g => g.First())
                    .OrderBy(b => b.Name))
                    RbBranches.Add(b);

                RbDepartments.Clear();
                foreach (var d in employees
                    .Where(e => e.DeptId > 0 && !string.IsNullOrEmpty(e.DepartmentName))
                    .Select(e => new DepartmentDto { DeptId = e.DeptId, Name = e.DepartmentName, ComId = e.ComId })
                    .GroupBy(d => d.DeptId).Select(g => g.First())
                    .OrderBy(d => d.Name))
                    RbDepartments.Add(d);

                if (IsDeptAccountMode)
                {
                    if (AppSession.DepartmentAccountCompanyId.HasValue
                        && AppSession.DepartmentAccountDepartmentId.HasValue
                        && AppSession.DepartmentAccountBranchId.HasValue)
                    {
                        var compId   = AppSession.DepartmentAccountCompanyId.Value;
                        var deptId   = AppSession.DepartmentAccountDepartmentId.Value;
                        var branchId = AppSession.DepartmentAccountBranchId.Value;
                        var deptEmps = employees.Where(e =>
                            e.ComId == compId && e.DeptId == deptId && e.BranchId == branchId).ToList();
                        DeptEmployees.Clear();
                        foreach (var e in deptEmps) DeptEmployees.Add(e);
                    }
                }

                _isDataLoaded = true;
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

        private Task SyncRbFiltersToEmployeeAsync(EmployeeViewModel emp)
        {
            _syncingRbFilters = true;
            try
            {
                var company = RbCompanies.FirstOrDefault(c => c.ComId == emp.ComId);
                if (company != null)
                    SetField(ref _selectedRbCompany, company, nameof(SelectedRbCompany));

                var branch = RbBranches.FirstOrDefault(b => b.BranchId == emp.BranchId);
                if (branch != null)
                    SetField(ref _selectedRbBranch, branch, nameof(SelectedRbBranch));

                var dept = RbDepartments.FirstOrDefault(d => d.DeptId == emp.DeptId);
                if (dept != null)
                    SetField(ref _selectedRbDepartment, dept, nameof(SelectedRbDepartment));

                OnPropertyChanged(nameof(FilteredReceivedByEmployees));
            }
            finally
            {
                _syncingRbFilters = false;
            }
            return Task.CompletedTask;
        }

        private async Task ReloadRbBranchesDepartmentsAsync(int? companyId)
        {
            RbBranches.Clear();
            RbDepartments.Clear();
            _selectedRbBranch     = null;
            _selectedRbDepartment = null;
            OnPropertyChanged(nameof(SelectedRbBranch));
            OnPropertyChanged(nameof(SelectedRbDepartment));
            OnPropertyChanged(nameof(FilteredReceivedByEmployees));

            if (companyId.HasValue)
            {
                // Narrow to the selected company via DB query
                var branchesTask = Task.Run(() => _service.GetBranchesByCompany(companyId.Value));
                var deptsTask    = Task.Run(() => _service.GetDepartmentsByCompany(companyId.Value));
                await Task.WhenAll(branchesTask, deptsTask);
                foreach (var b in branchesTask.Result) RbBranches.Add(b);
                foreach (var d in deptsTask.Result)    RbDepartments.Add(d);
            }
            else
            {
                // No company selected — restore full lists derived from employees
                foreach (var b in AllEmployees
                    .Where(e => e.BranchId > 0 && !string.IsNullOrEmpty(e.BranchName))
                    .Select(e => new BranchDto { BranchId = e.BranchId, Name = e.BranchName, ComId = e.ComId })
                    .GroupBy(b => b.BranchId).Select(g => g.First())
                    .OrderBy(b => b.Name))
                    RbBranches.Add(b);

                foreach (var d in AllEmployees
                    .Where(e => e.DeptId > 0 && !string.IsNullOrEmpty(e.DepartmentName))
                    .Select(e => new DepartmentDto { DeptId = e.DeptId, Name = e.DepartmentName, ComId = e.ComId })
                    .GroupBy(d => d.DeptId).Select(g => g.First())
                    .OrderBy(d => d.Name))
                    RbDepartments.Add(d);
            }

            OnPropertyChanged(nameof(FilteredReceivedByEmployees));
        }

        private void ExecuteClearRbFilters()
        {
            _rbListCollapsed   = false;
            SelectedRbCompany  = null;
            SelectedRbBranch   = null;
            SelectedRbDepartment = null;
            RbSearchText       = string.Empty;
            SelectedReceivedBy = null;
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
            if (IsCartridgeCategory && _goodEmptyQty + _damagedEmptyQty != _quantity)
            {
                ValidationError = $"Please specify the returned empty cartridges. Good + Damaged ({_goodEmptyQty + _damagedEmptyQty}) must equal the quantity requested ({_quantity}).";
                return;
            }

            var existing = RequestItems.FirstOrDefault(x =>
                string.Equals(x.ModelKey, _selectedModel.ModelKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                int merged = existing.Quantity + _quantity;
                existing.Quantity = merged;
                StatusMessage = $"Updated '{existing.CartridgeModel}' quantity to {merged}.";
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
            if (item != null)
                RequestItems.Remove(item);
        }

        private void ExecuteClear()
        {
            SelectedModel      = null;
            Quantity           = 1;
            GoodEmptyQty       = 0;
            DamagedEmptyQty    = 0;
            RequestItems.Clear();
            IsPickup           = true;
            ExecuteClearRbFilters();
            Remarks            = string.Empty;
            ValidationError    = string.Empty;
            StatusMessage      = string.Empty;
            if (IsDeptAccountMode) SelectedDeptEmployee = null;
        }

        // ── Tour dummy data ───────────────────────────────────────────────────

        private bool _tourDummyInjected;

        public bool AddTourDummyItems()
        {
            if (RequestItems.Count > 0 || _tourDummyInjected) return false;
            RequestItems.Add(new RequestItemRowViewModel
            {
                CartridgeModel  = "HP CF280A",
                ModelKey        = "__tour_demo_1__",
                Quantity        = 2,
                GoodEmptyQty    = 1,
                DamagedEmptyQty = 0
            });
            RequestItems.Add(new RequestItemRowViewModel
            {
                CartridgeModel  = "HP CE285A",
                ModelKey        = "__tour_demo_2__",
                Quantity        = 1,
                GoodEmptyQty    = 1,
                DamagedEmptyQty = 0
            });
            _tourDummyInjected = true;
            return true;
        }

        public void RemoveTourDummyItems()
        {
            if (!_tourDummyInjected) return;
            var dummies = RequestItems
                .Where(r => r.ModelKey == "__tour_demo_1__" || r.ModelKey == "__tour_demo_2__")
                .ToList();
            foreach (var d in dummies) RequestItems.Remove(d);
            _tourDummyInjected = false;
        }

        private async Task SubmitAsync()
        {
            ValidationError = string.Empty;

            int submittingEmployeeId;
            string employeeName, employeePosition;
            int destinationCompanyId, destinationBranchId, destinationDepartmentId;

            if (IsDeptAccountMode)
            {
                if (_selectedDeptEmployee == null)
                {
                    ValidationError = "Please select an employee before submitting.";
                    return;
                }
                submittingEmployeeId    = _selectedDeptEmployee.EmpId;
                employeeName            = _selectedDeptEmployee.Name;
                employeePosition        = _selectedDeptEmployee.Position ?? string.Empty;
                destinationCompanyId    = _selectedDeptEmployee.ComId;
                destinationBranchId     = _selectedDeptEmployee.BranchId;
                destinationDepartmentId = _selectedDeptEmployee.DeptId;
            }
            else
            {
                if (!AppSession.CurrentEmployeeId.HasValue || AppSession.CurrentEmployeeId.Value <= 0)
                {
                    ValidationError = "Your account does not have a linked employee profile. Contact IT to link your account.";
                    return;
                }
                if (!AppSession.CurrentCompanyId.HasValue    || AppSession.CurrentCompanyId.Value    <= 0 ||
                    !AppSession.CurrentBranchId.HasValue     || AppSession.CurrentBranchId.Value     <= 0 ||
                    !AppSession.CurrentDepartmentId.HasValue || AppSession.CurrentDepartmentId.Value <= 0)
                {
                    ValidationError = "Your employee profile is missing company, branch, or department information. Contact IT.";
                    return;
                }
                submittingEmployeeId    = AppSession.CurrentEmployeeId.Value;
                employeeName            = AppSession.CurrentEmployeeName ?? AppSession.CurrentUserName ?? "Unknown";
                employeePosition        = AppSession.CurrentEmployeePosition ?? string.Empty;
                destinationCompanyId    = AppSession.CurrentCompanyId.Value;
                destinationBranchId     = AppSession.CurrentBranchId.Value;
                destinationDepartmentId = AppSession.CurrentDepartmentId.Value;
            }

            if (RequestItems.Count == 0)
            {
                ValidationError = "Please add at least one cartridge model to the request.";
                return;
            }

            if (IsPickup && _selectedReceivedBy == null)
            {
                ValidationError = "Please select who will receive the cartridges (Received By).";
                return;
            }

            IsLoading     = true;
            StatusMessage = "Submitting...";

            try
            {
                var requestViewModel = new CartridgeRequestViewModel
                {
                    EmployeeName            = employeeName,
                    EmployeePosition        = employeePosition,
                    DestinationCompanyId    = destinationCompanyId,
                    DestinationBranchId     = destinationBranchId,
                    DestinationDepartmentId = destinationDepartmentId,
                    FulfillmentMethod       = IsPickup ? "PICKUP" : "DELIVERY",
                    AdditionalRemarks       = _remarks?.Trim() ?? string.Empty,
                    ReceivedById            = IsPickup && _selectedReceivedBy != null
                                              ? _selectedReceivedBy.EmpId : (int?)null
                };

                var serviceItems  = RequestItems.Select(r => r.ToServiceModel()).ToList();
                int currentUserId = AppSession.CurrentUserId;

                StatusMessage = "Submitting...";

                var (createdIds, wasAutoApproved, authId, _) = await Task.Run(() =>
                    _service.CreateCartridgeRequestByModel(
                        requestViewModel, serviceItems, submittingEmployeeId, currentUserId, isAssisted: false));

                ExecuteClear();

                var updated = await Task.Run(() =>
                    IsCartridgeCategory
                        ? _service.GetCartridgeModelsWithAvailability()
                        : _service.GetConsumableModelsWithAvailability(_selectedCategory));
                RefreshModels(updated);

                StatusMessage = "Request submitted successfully.";
                RequestSubmitted?.Invoke(createdIds, wasAutoApproved, authId);
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
