using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.NewTicketIntake.ViewModels
{
    /// <summary>
    /// Backs NewRepairTicketDialog: search-as-you-type item picker (min 2 chars, 300ms debounce),
    /// problem/priority fields, and save via sp_RepairPortal_CreateTicket.
    /// </summary>
    public sealed class NewRepairTicketViewModel : ViewModelBase
    {
        private readonly IRepairTicketRepository _repository;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly System.Threading.Tasks.Task _requestedByOptionsLoadTask;
        private int? _linkedCallTicketId;
        private bool _suppressItemRequestedByAutoFill;

        public ObservableCollection<RepairableItemLookup> SearchResults { get; } = new ObservableCollection<RepairableItemLookup>();
        public ObservableCollection<string> PriorityOptions { get; } = new ObservableCollection<string> { "Low", "Medium", "High", "Critical" };

        public ObservableCollection<OrgLookupOption> DepartmentOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> EmployeeOptions { get; } = new ObservableCollection<OrgLookupOption>();

        /// <summary>Company -> Branch -> Department cascade for Department-mode requests (a bare
        /// Department is ambiguous across Companies/Branches). Follows the cascade concept from
        /// BatchAddRequestDialog's CmbCompany/CmbBranch/CmbDepartment, driven by
        /// dbo.BranchDepartmentCompany via GetBranchesForCompanyAsync/GetDepartmentsForCompanyBranchAsync
        /// instead of a preloaded/text-filtered list.</summary>
        public ObservableCollection<OrgLookupOption> RequestedCompanyOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> RequestedBranchOptions { get; } = new ObservableCollection<OrgLookupOption>();

        public event Action<string, string> RequestWarning;
        public event Action<RepairTicketListItem> TicketCreated;
        public event Action<bool> RequestClose;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    _searchDebounceTimer.Stop();
                    if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 2)
                        _searchDebounceTimer.Start();
                    else
                        _ = LoadCandidatesAsync();
                }
            }
        }

        // ── Item scope: three independent checkboxes, not mutually exclusive ─────────────────
        // "In a Lot (Set)" and "An Item (Inventory)" are the two real filters; "Both" is a
        // convenience checkbox that checks both of them at once. The effective query scope is
        // derived from whichever combination ends up checked (both, or neither, means no
        // restriction — same as explicitly checking "Both").
        private bool _scopeInLot = true;
        public bool ScopeInLot
        {
            get => _scopeInLot;
            set
            {
                if (SetField(ref _scopeInLot, value))
                {
                    SyncScopeBoth();
                    RefreshItems();
                }
            }
        }

        private bool _scopeAnItem = true;
        public bool ScopeAnItem
        {
            get => _scopeAnItem;
            set
            {
                if (SetField(ref _scopeAnItem, value))
                {
                    SyncScopeBoth();
                    RefreshItems();
                }
            }
        }

        private bool _scopeBoth = true;
        public bool ScopeBoth
        {
            get => _scopeBoth;
            set
            {
                if (SetField(ref _scopeBoth, value) && value)
                {
                    // Checking "Both" turns the other two on too — unchecking it alone doesn't
                    // force them off, since it's just re-derived from them on their own change.
                    ScopeInLot = true;
                    ScopeAnItem = true;
                }
            }
        }

        private void SyncScopeBoth() => SetField(ref _scopeBoth, ScopeInLot && ScopeAnItem, nameof(ScopeBoth));

        /// <summary>"Set", "Inventory", or "Both" for the repository — both checked (or neither
        /// checked, to avoid an empty/confusing result set) means no restriction.</summary>
        private string EffectiveItemScope
        {
            get
            {
                if (ScopeInLot && !ScopeAnItem) return "Set";
                if (ScopeAnItem && !ScopeInLot) return "Inventory";
                return "Both";
            }
        }

        private void RefreshItems()
        {
            _ = string.IsNullOrWhiteSpace(SearchText) || SearchText.Trim().Length < 2
                ? LoadCandidatesAsync() : RunSearchAsync();
        }

        private bool _isShowingCandidates;
        /// <summary>True when SearchResults holds the default "previously repaired / damaged"
        /// candidate list rather than live search results.</summary>
        public bool IsShowingCandidates { get => _isShowingCandidates; private set => SetField(ref _isShowingCandidates, value); }

        private bool _isRequestedByEditable = true;
        /// <summary>False once Requested By has been auto-filled from the selected item's existing
        /// Set (Company/Branch/Department/Employee all come from real dispatch data at that point) —
        /// the technician shouldn't be able to silently retarget who a repair is billed/reported to
        /// just by editing these dropdowns after picking an already-assigned item. True for items
        /// with no Set yet (e.g. freshly created via "+ Add Item"), where nothing to lock onto exists.</summary>
        public bool IsRequestedByEditable
        {
            get => _isRequestedByEditable;
            private set { if (SetField(ref _isRequestedByEditable, value)) OnPropertyChanged(nameof(CanEditRequestedByEmployee)); }
        }

        /// <summary>Employee dropdown's actual enabled state — combines the overall lock (item
        /// already has a Set) with the existing dept-level-vs-employee toggle.</summary>
        public bool CanEditRequestedByEmployee => IsRequestedByEditable && !IsDeptLevelRequest;

        private RepairableItemLookup _selectedItem;
        public RepairableItemLookup SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetField(ref _selectedItem, value) && !_suppressItemRequestedByAutoFill)
                    _ = ApplyRequestedByFromItemAsync(value);
            }
        }

        /// <summary>Called after the "+ Add Item" button (BatchAddItemDialog) creates a new item —
        /// drops it straight into the picker as the selection, so the technician doesn't have to
        /// search for the item they just typed in seconds earlier.</summary>
        public void SetSelectedItemFromCreation(RepairableItemLookup item)
        {
            if (item == null) return;
            SearchResults.Clear();
            SearchResults.Add(item);
            IsShowingCandidates = false;
            SelectedItem = item;
        }

        /// <summary>Initializes the normal intake controls for a forwarded IT Call. This performs
        /// no persistence; SaveAsync still requires the operator to select Create Ticket.</summary>
        public async System.Threading.Tasks.Task PrefillForCallTicketForwardingAsync(NewRepairTicketRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.CallTicketId.HasValue || request.CallTicketId.Value <= 0)
                throw new ArgumentException("A source IT Call is required for linked repair intake.", nameof(request));
            if (request.ItemId <= 0)
                throw new ArgumentException("A repair item is required for linked repair intake.", nameof(request));

            _linkedCallTicketId = request.CallTicketId;
            Problem = request.Problem ?? string.Empty;
            SelectedPriority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority;
            _selectedDateReceived = request.DateReceived ?? DateTime.Now;
            _dateReceivedManuallySet = request.DateReceived.HasValue;
            OnPropertyChanged(nameof(SelectedDateReceived));

            await _requestedByOptionsLoadTask;

            // The constructor begins a default candidate query. Invalidate it before assigning
            // the forwarded item, otherwise its delayed result replaces this instance in the
            // ListBox and removes the visible selected-row highlight.
            _searchDebounceTimer.Stop();
            ++_searchLoadToken;
            var item = await GetForwardedItemLookupAsync(request.ItemId);
            if (item == null)
                throw new InvalidOperationException("The item selected for repair could not be found.");

            SearchResults.Clear();
            SearchResults.Add(item);
            IsShowingCandidates = false;
            _suppressItemRequestedByAutoFill = true;
            try
            {
                SelectedItem = item;
            }
            finally
            {
                _suppressItemRequestedByAutoFill = false;
            }

            await ApplyRequestedByContextAsync(request, item);
        }

        /// <summary>Gets the forwarded item with its existing Set/Request context when possible.
        /// The direct ID lookup is retained as a reliable fallback for an item that does not appear
        /// in the normal searchable pool (for example, after an idempotent retry).</summary>
        private async System.Threading.Tasks.Task<RepairableItemLookup> GetForwardedItemLookupAsync(int itemId)
        {
            var directItem = await _repository.GetItemLookupByIdAsync(itemId);
            if (directItem == null)
                return null;

            var query = new[] { directItem.SerialNumber, directItem.ModelNumber, directItem.Name }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 2);
            if (string.IsNullOrWhiteSpace(query))
                return directItem;

            try
            {
                var matches = await _repository.SearchRepairableItemsAsync(query, "Both");
                return matches.FirstOrDefault(candidate => candidate.ItemId == itemId) ?? directItem;
            }
            catch
            {
                return directItem;
            }
        }

        /// <summary>Applies the IT Call's organization context directly, rather than allowing the
        /// selected item's historical Set assignment to replace the caller context of this ticket.</summary>
        private async System.Threading.Tasks.Task ApplyRequestedByContextAsync(
            NewRepairTicketRequest request,
            RepairableItemLookup forwardedItem)
        {
            // An IT Call is the preferred source. Some legacy calls contain only a caller name,
            // however, so fall back as one complete group to the physical item's active Set/Request
            // assignment instead of leaving the Requested By controls blank or mixing two contexts.
            var hasCallOrganization = request.RequestedByComId.HasValue && request.RequestedByDeptId.HasValue;
            var companyId = hasCallOrganization ? request.RequestedByComId : forwardedItem?.RequestedByComId;
            var branchId = hasCallOrganization ? request.RequestedByBranchId : forwardedItem?.RequestedByBranchId;
            var departmentId = hasCallOrganization ? request.RequestedByDeptId : forwardedItem?.RequestedByDeptId;
            var employeeId = hasCallOrganization ? request.RequestedByEmpId : forwardedItem?.RequestedByEmpId;

            if (!companyId.HasValue || !departmentId.HasValue)
                return;

            var company = RequestedCompanyOptions.FirstOrDefault(x => x.Id == companyId.Value);
            if (company == null)
                return;

            var branches = await _repository.GetBranchesForCompanyAsync(company.Id);
            RequestedBranchOptions.Clear();
            foreach (var branchOption in branches) RequestedBranchOptions.Add(branchOption);

            var branch = branchId.HasValue
                ? RequestedBranchOptions.FirstOrDefault(x => x.Id == branchId.Value)
                : null;

            var departments = await _repository.GetDepartmentsForCompanyBranchAsync(company.Id, branch?.Id);
            DepartmentOptions.Clear();
            foreach (var departmentOption in departments) DepartmentOptions.Add(departmentOption);

            var department = DepartmentOptions.FirstOrDefault(x => x.Id == departmentId.Value);
            if (department == null && branch != null)
            {
                departments = await _repository.GetDepartmentsForCompanyBranchAsync(company.Id, null);
                DepartmentOptions.Clear();
                foreach (var departmentOption in departments) DepartmentOptions.Add(departmentOption);
                department = DepartmentOptions.FirstOrDefault(x => x.Id == departmentId.Value);
            }
            if (department == null)
                return;

            _selectedRequestedCompany = company;
            OnPropertyChanged(nameof(SelectedRequestedCompany));
            _selectedRequestedBranch = branch;
            OnPropertyChanged(nameof(SelectedRequestedBranch));
            _selectedRequestedDept = department;
            OnPropertyChanged(nameof(SelectedRequestedDept));

            EmployeeOptions.Clear();
            var employees = await _repository.GetEmployeesForCompanyBranchDeptAsync(company.Id, branch?.Id, department.Id);
            foreach (var employeeOption in employees) EmployeeOptions.Add(employeeOption);

            var employee = employeeId.HasValue
                ? EmployeeOptions.FirstOrDefault(x => x.Id == employeeId.Value)
                : null;
            var isEmployeeRequest = employee != null
                && (!hasCallOrganization || string.Equals(request.RequestedByType, "Employee", StringComparison.OrdinalIgnoreCase));
            IsDeptLevelRequest = !isEmployeeRequest;
            _selectedRequestedEmployee = employee;
            OnPropertyChanged(nameof(SelectedRequestedEmployee));
            IsRequestedByEditable = false;
        }

        /// <summary>Prefills Requested By from the picked item's current Set — Company/Branch/
        /// Department come from the Set's own dispatch context, Employee from the Request row that
        /// put this item in that Set (NULL there means it was a department-level dispatch). Does
        /// nothing if the item has no Set (e.g. one just created via "+ Add Item"), leaving whatever
        /// the technician already entered untouched rather than clearing it.</summary>
        private async System.Threading.Tasks.Task ApplyRequestedByFromItemAsync(RepairableItemLookup item)
        {
            if (item?.RequestedByComId == null)
            {
                IsRequestedByEditable = true;
                return;
            }

            try
            {
                if (RequestedCompanyOptions.Count == 0)
                    await LoadRequestedByOptionsAsync();

                var company = RequestedCompanyOptions.FirstOrDefault(c => c.Id == item.RequestedByComId.Value);
                if (company == null) return;

                _selectedRequestedCompany = company;
                OnPropertyChanged(nameof(SelectedRequestedCompany));

                RequestedBranchOptions.Clear();
                DepartmentOptions.Clear();
                var branches = await _repository.GetBranchesForCompanyAsync(company.Id);
                foreach (var b in branches) RequestedBranchOptions.Add(b);
                var depts = await _repository.GetDepartmentsForCompanyBranchAsync(company.Id, item.RequestedByBranchId);
                foreach (var d in depts) DepartmentOptions.Add(d);

                var branch = item.RequestedByBranchId.HasValue
                    ? RequestedBranchOptions.FirstOrDefault(b => b.Id == item.RequestedByBranchId.Value)
                    : null;
                _selectedRequestedBranch = branch;
                OnPropertyChanged(nameof(SelectedRequestedBranch));

                var dept = item.RequestedByDeptId.HasValue
                    ? DepartmentOptions.FirstOrDefault(d => d.Id == item.RequestedByDeptId.Value)
                    : null;
                _selectedRequestedDept = dept;
                OnPropertyChanged(nameof(SelectedRequestedDept));

                EmployeeOptions.Clear();
                var emps = await _repository.GetEmployeesForCompanyBranchDeptAsync(company.Id, branch?.Id, dept?.Id);
                foreach (var e in emps) EmployeeOptions.Add(e);

                var employee = item.RequestedByEmpId.HasValue
                    ? EmployeeOptions.FirstOrDefault(e => e.Id == item.RequestedByEmpId.Value)
                    : null;
                _selectedRequestedEmployee = employee;
                OnPropertyChanged(nameof(SelectedRequestedEmployee));

                // Auto-choose department- vs employee-level to match what the Set actually shows.
                IsDeptLevelRequest = !item.RequestedByEmpId.HasValue;

                // Lock the fields now that they reflect the item's real, existing assignment.
                IsRequestedByEditable = false;
            }
            catch
            {
                // Non-critical — Requested By just stays whatever it already was if this fails.
            }
        }

        private string _problem = string.Empty;
        public string Problem { get => _problem; set => SetField(ref _problem, value); }

        private string _selectedPriority = "Medium";
        public string SelectedPriority { get => _selectedPriority; set => SetField(ref _selectedPriority, value); }

        /// <summary>Manually-editable intake date — when the item actually arrived, distinct from
        /// CreatedAt (when the ticket record itself is created). Defaults to "now" at the moment
        /// the dialog opens, but a technician can spend several minutes filling the rest of the
        /// form before saving — SaveAsync refreshes this to "now" again right before submit,
        /// UNLESS the technician has manually picked a date, tracked via _dateReceivedManuallySet.</summary>
        private DateTime _selectedDateReceived = DateTime.Now;
        private bool _dateReceivedManuallySet;
        public DateTime SelectedDateReceived
        {
            get => _selectedDateReceived;
            set { if (SetField(ref _selectedDateReceived, value)) _dateReceivedManuallySet = true; }
        }

        /// <summary>Company/Branch/Department/Employee all show together now — this just flags
        /// whether the request is at department level (no specific employee), in which case the
        /// Employee dropdown is disabled and cleared instead of switching the whole section's
        /// layout out from under the technician.</summary>
        private bool _isDeptLevelRequest = true;
        public bool IsDeptLevelRequest
        {
            get => _isDeptLevelRequest;
            set
            {
                if (SetField(ref _isDeptLevelRequest, value))
                {
                    OnPropertyChanged(nameof(CanEditRequestedByEmployee));
                    if (value) SelectedRequestedEmployee = null;
                }
            }
        }

        private OrgLookupOption _selectedRequestedCompany;
        public OrgLookupOption SelectedRequestedCompany
        {
            get => _selectedRequestedCompany;
            set
            {
                if (SetField(ref _selectedRequestedCompany, value))
                    _ = ReloadRequestedBranchAndDeptAsync();
            }
        }

        private OrgLookupOption _selectedRequestedBranch;
        public OrgLookupOption SelectedRequestedBranch
        {
            get => _selectedRequestedBranch;
            set
            {
                if (SetField(ref _selectedRequestedBranch, value))
                    _ = ReloadRequestedDeptAsync();
            }
        }

        private OrgLookupOption _selectedRequestedDept;
        public OrgLookupOption SelectedRequestedDept
        {
            get => _selectedRequestedDept;
            set
            {
                if (SetField(ref _selectedRequestedDept, value))
                    _ = ReloadRequestedEmployeesAsync();
            }
        }

        private OrgLookupOption _selectedRequestedEmployee;
        public OrgLookupOption SelectedRequestedEmployee { get => _selectedRequestedEmployee; set => SetField(ref _selectedRequestedEmployee, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public NewRepairTicketViewModel(IRepairTicketRepository repository)
        {
            _repository = repository ?? new RepairTicketRepository();

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += async (s, e) => { _searchDebounceTimer.Stop(); await RunSearchAsync(); };
            _requestedByOptionsLoadTask = LoadRequestedByOptionsAsync();

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));

            _ = LoadCandidatesAsync();
        }

        private async System.Threading.Tasks.Task LoadRequestedByOptionsAsync()
        {
            try
            {
                var companies = await _repository.GetCompanyOptionsAsync();
                RequestedCompanyOptions.Clear();
                foreach (var c in companies) RequestedCompanyOptions.Add(c);
            }
            catch
            {
                // Non-critical — the dropdown just stays empty if this fails.
            }
        }

        /// <summary>Company changed: reload Branch options for the new Company (cascade step 1) and
        /// Department options for the new Company with no Branch filter yet (cascade step 2's
        /// initial state), clearing any previously selected Branch/Department since they may no
        /// longer be valid under the new Company. Clearing Branch/Dept below cascades further via
        /// their own setters (ReloadRequestedDeptAsync -> ReloadRequestedEmployeesAsync), so
        /// Employee ends up refreshed too.</summary>
        private async System.Threading.Tasks.Task ReloadRequestedBranchAndDeptAsync()
        {
            SelectedRequestedBranch = null;
            SelectedRequestedDept = null;
            RequestedBranchOptions.Clear();
            DepartmentOptions.Clear();

            if (SelectedRequestedCompany == null) return;

            try
            {
                var branches = await _repository.GetBranchesForCompanyAsync(SelectedRequestedCompany.Id);
                foreach (var b in branches) RequestedBranchOptions.Add(b);

                var depts = await _repository.GetDepartmentsForCompanyBranchAsync(SelectedRequestedCompany.Id, null);
                foreach (var d in depts) DepartmentOptions.Add(d);
            }
            catch
            {
                // Non-critical — the dropdowns just stay empty if this fails.
            }
        }

        /// <summary>Branch changed: reload Department options narrowed to Company + Branch (cascade
        /// step 2), clearing any previously selected Department since it may no longer be valid
        /// under the new Branch (cascades to Employee via SelectedRequestedDept's setter).</summary>
        private async System.Threading.Tasks.Task ReloadRequestedDeptAsync()
        {
            SelectedRequestedDept = null;
            DepartmentOptions.Clear();

            if (SelectedRequestedCompany == null) return;

            try
            {
                var depts = await _repository.GetDepartmentsForCompanyBranchAsync(SelectedRequestedCompany.Id, SelectedRequestedBranch?.Id);
                foreach (var d in depts) DepartmentOptions.Add(d);
            }
            catch
            {
                // Non-critical — the dropdown just stays empty if this fails.
            }
        }

        /// <summary>Department changed (cascade step 3): reload Employee options narrowed to
        /// Company [+ Branch] [+ Department] — the Employee dropdown always mirrors whatever the
        /// Company/Branch/Department pickers currently resolve to, whether or not this is a
        /// department-level request.</summary>
        private async System.Threading.Tasks.Task ReloadRequestedEmployeesAsync()
        {
            SelectedRequestedEmployee = null;
            EmployeeOptions.Clear();

            if (SelectedRequestedCompany == null) return;

            try
            {
                var emps = await _repository.GetEmployeesForCompanyBranchDeptAsync(
                    SelectedRequestedCompany.Id, SelectedRequestedBranch?.Id, SelectedRequestedDept?.Id);
                foreach (var e in emps) EmployeeOptions.Add(e);
            }
            catch
            {
                // Non-critical — the dropdown just stays empty if this fails.
            }
        }

        /// <summary>Guards LoadCandidatesAsync/RunSearchAsync against out-of-order results — typing
        /// under 2 characters fires LoadCandidatesAsync immediately while 2+ characters fires
        /// RunSearchAsync 300ms later, so both can be in flight at once. Without this token, an
        /// earlier request finishing after a later one silently reverts SearchResults to stale
        /// content, making it look like the search box "ate" what was just typed.</summary>
        private int _searchLoadToken;

        /// <summary>
        /// Default picker contents before the technician types anything: items that were
        /// previously repaired or currently have Condition = Damaged — the pool most likely to
        /// need another ticket, instead of an empty list requiring a search first.
        /// </summary>
        private async System.Threading.Tasks.Task LoadCandidatesAsync()
        {
            var token = ++_searchLoadToken;
            IsShowingCandidates = true;
            try
            {
                var results = await _repository.GetCandidateRepairItemsAsync(EffectiveItemScope);
                if (token != _searchLoadToken) return;
                SearchResults.Clear();
                foreach (var r in results)
                    SearchResults.Add(r);
            }
            catch
            {
                if (token == _searchLoadToken) SearchResults.Clear();
            }
        }

        private async System.Threading.Tasks.Task RunSearchAsync()
        {
            var token = ++_searchLoadToken;
            IsShowingCandidates = false;
            try
            {
                var results = await _repository.SearchRepairableItemsAsync(SearchText, EffectiveItemScope);
                if (token != _searchLoadToken) return;
                SearchResults.Clear();
                foreach (var r in results)
                    SearchResults.Add(r);
            }
            catch
            {
                if (token == _searchLoadToken) SearchResults.Clear();
            }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (SelectedItem == null)
            {
                RequestWarning?.Invoke("No Item Selected", "Please search for and select the item being repaired.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Problem))
            {
                RequestWarning?.Invoke("Problem Required", "Please describe the problem.");
                return;
            }

            if (SelectedRequestedCompany == null)
            {
                RequestWarning?.Invoke("Requested By Required", "Please select the company requesting this repair.");
                return;
            }

            if (SelectedRequestedDept == null)
            {
                RequestWarning?.Invoke("Requested By Required", "Please select the department requesting this repair.");
                return;
            }

            if (!IsDeptLevelRequest && SelectedRequestedEmployee == null)
            {
                RequestWarning?.Invoke("Requested By Required", "Please select the employee requesting this repair, or check \"Department-level request\" if there isn't one specific person.");
                return;
            }

            // Refresh Date Received to the actual submit moment if the technician never manually
            // picked a date — keeps it from silently going stale while the rest of the form (item
            // search, Problem, Requested By) was being filled out. A manual pick is respected as-is.
            if (!_dateReceivedManuallySet)
            {
                _selectedDateReceived = DateTime.Now;
                OnPropertyChanged(nameof(SelectedDateReceived));
            }

            IsBusy = true;
            try
            {
                var request = new NewRepairTicketRequest
                {
                    ItemId = SelectedItem.ItemId,
                    CallTicketId = _linkedCallTicketId,
                    Problem = Problem.Trim(),
                    Priority = SelectedPriority,
                    SubmittedByEmpId = AppSession.CurrentEmployeeId,
                    DateReceived = SelectedDateReceived,
                    RequestedByType = IsDeptLevelRequest ? "Department" : "Employee",
                    RequestedByDeptId = SelectedRequestedDept?.Id,
                    RequestedByEmpId = IsDeptLevelRequest ? null : SelectedRequestedEmployee?.Id,
                    RequestedByComId = SelectedRequestedCompany?.Id,
                    RequestedByBranchId = SelectedRequestedBranch?.Id
                };

                var createdByUserId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null;
                var isLinkedCallForwarding = request.CallTicketId.HasValue;
                var created = isLinkedCallForwarding
                    ? await _repository.CreateTicketFromCallTicketAsync(request, createdByUserId)
                    : await _repository.CreateTicketAsync(request, createdByUserId);

                // The item had no Set at pick time (IsRequestedByEditable), so the Requested By
                // values just entered would otherwise only ever exist as flat columns on this
                // ticket. Register a real Request+Set+SetItem so the item's org placement is
                // discoverable everywhere else in the system (Sets pages, and this same picker
                // locking the fields next time this item comes in for repair) instead of being a
                // disconnected copy. Linked IT Call creation is idempotent, so it deliberately
                // skips this follow-up work: a retry may return an existing ticket.
                if (!isLinkedCallForwarding && IsRequestedByEditable && createdByUserId.HasValue)
                {
                    try
                    {
                        await _repository.LinkNewItemToRequestedBySetAsync(request, created.TicketCode, createdByUserId.Value);
                    }
                    catch (Exception linkEx)
                    {
                        Yakult.Inventory.App.Core.Logger.LogError("NewRepairTicketViewModel: failed to auto-create Set for new item", linkEx);
                    }
                }

                // The intake Problem text IS the reported problem — auto-create it as
                // Observation #1 so it shows up in the Detail page's Reported Problem section
                // immediately, instead of only living in the legacy flat Problem column. Linked
                // IT Calls skip this because an idempotent retry can return an existing ticket,
                // which must not receive a duplicate observation.
                if (!isLinkedCallForwarding)
                {
                    try
                    {
                        await _repository.AddObservationAsync(created.RepairTicketId, request.Problem, createdByUserId);
                    }
                    catch
                    {
                        // Non-critical — the ticket itself was created successfully; the legacy
                        // Problem column still holds the text as a fallback if this fails.
                    }
                }

                TicketCreated?.Invoke(created);
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                RequestWarning?.Invoke("Save Failed", "Failed to create repair ticket: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
