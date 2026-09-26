using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Services;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Repositories;
using System.Linq;

namespace Inventory.RequestPortal.Controllers
{
    // IMPORTANT:
    // Users select cartridges by MODEL and QUANTITY for usability.
    // Internally, the system always resolves requests to specific
    // cartridges using ItemId / SerialNumber.
    // Model-based auto-matching is forbidden and must never be reintroduced.

    /// <summary>
    /// Controller for Request Portal functionality.
    /// Users select by Model + Quantity (they don't know serial numbers).
    /// System resolves to specific ItemIds at submission time.
    /// </summary>
    [RequireLogin]
    public class RequestController : Controller
    {
        private readonly IRequesterPortalService _portalService;
        private readonly ILogger<RequestController> _logger;
        private readonly IActivityLogRepository _activityLog;
        private readonly ICartridgeAuthorizationWebRepository _authRepo;
        private readonly IWebHostEnvironment _environment;

        public RequestController(IRequesterPortalService portalService,
                                 ILogger<RequestController> logger,
                                 IActivityLogRepository activityLog,
                                 ICartridgeAuthorizationWebRepository authRepo,
                                 IWebHostEnvironment environment)
        {
            _portalService = portalService;
            _logger        = logger;
            _activityLog   = activityLog;
            _authRepo      = authRepo;
            _environment   = environment;
        }

        private static readonly HashSet<string> ApproverPositions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ACCOUNT COORDINATOR",
                "ACTING ACCOUNT COORDINATOR",
                "ASST. COORDINATOR",
                "LADY COORDINATOR",
                "ACTING JR. ASST. MANAGER",
                "ASST. MANAGER",
                "JR. ASST. MANAGER",
                "SUPERVISOR",
                "MANAGER",
                "COORDINATOR"
            };

        private static readonly string[] ConsumableCategories = { "Ink", "Printerhead", "Toner" };

        /// <summary>
        /// Loads the Ink/Printhead/Toner Cartridge model dropdowns, keyed by category,
        /// for the category selector alongside the existing Cartridge dropdown.
        /// </summary>
        private Dictionary<string, List<CartridgeModelAvailabilityViewModel>> LoadConsumableModelsByCategory()
        {
            var result = new Dictionary<string, List<CartridgeModelAvailabilityViewModel>>();
            foreach (var category in ConsumableCategories)
                result[category] = _portalService.GetConsumableModelsWithAvailability(category);
            return result;
        }

        /// <summary>
        /// Gets the current logged-in user from session.
        /// </summary>
        private UserSessionModel? GetCurrentUser()
        {
            return HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
        }

        private static string AggregateGroupStatus(List<string> statuses)
        {
            if (statuses.Count == 0) return string.Empty;
            var upper = statuses.Select(s => s?.ToUpperInvariant() ?? "").ToList();
            bool anyUnfulfilled   = upper.Any(s => s == "UNFULFILLED" || s.Contains("PARTIALLY"));
            bool anyFulfilled     = upper.Any(s => s == "FULFILLED" || s == "COMPLETED" || s == "REPLACED");
            bool anyActive        = upper.Any(s => s == "UNDER REVIEW" || s == "PROCESSING");
            if (anyUnfulfilled && (anyFulfilled || anyActive)) return "Partially Fulfilled";
            if (anyUnfulfilled) return "Unfulfilled";
            if (anyFulfilled)   return "Fulfilled";
            return statuses[0];
        }

        private List<EmployeeViewModel> ReloadEmployeesForUser(UserSessionModel? user)
        {
            return _portalService.GetActiveEmployees();
        }

        private void SetCommonViewBag(UserSessionModel? user)
        {
            ViewBag.CurrentUserName = user?.UserName;
            ViewBag.IsLoggedIn      = user?.IsLoggedIn ?? false;
            ViewBag.IsApprover      = user != null
                && !string.IsNullOrWhiteSpace(user.EmployeePosition)
                && ApproverPositions.Contains(user.EmployeePosition.Trim());
            ViewBag.IsDeveloper     = user?.IsDeveloper ?? false;
            ViewBag.IsITStaff       = user?.IsITStaff ?? false;
            ViewBag.IsITDepartment  = user?.IsITDepartment ?? false;
        }

        /// <summary>
        /// Displays the New Request form with Model + Quantity selection.
        /// Redirects to Authorization/EmployeeStatus if the employee has no valid authorization.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = GetCurrentUser();

            // No pre-authorization gate — employees submit freely.
            // The request is created as 'Awaiting Authorization' and becomes visible
            // on the WinForms fulfillment page only after the supervisor signs.

            bool isDeptAccount = currentUser?.IsDepartmentAccountSession == true;

            List<EmployeeViewModel> deptAccountEmployees = new();
            if (isDeptAccount)
            {
                // Load employees scoped to the dept account's company+dept+branch
                int comId  = currentUser!.DepartmentAccountCompanyId    ?? 0;
                int deptId = currentUser!.DepartmentAccountDepartmentId ?? 0;
                int brId   = currentUser!.DepartmentAccountBranchId     ?? 0;
                if (comId > 0 && deptId > 0 && brId > 0)
                    deptAccountEmployees = _portalService.GetEmployeesByAccount(comId, deptId, brId);
            }

            var model = new NewRequestPageViewModel
            {
                Request = new CartridgeRequestViewModel
                {
                    // For normal employees: auto-fill from session
                    // For dept accounts: left blank — user picks from employee picker
                    EmployeeName     = isDeptAccount ? string.Empty : (currentUser?.EmployeeName ?? currentUser?.UserName ?? string.Empty),
                    EmployeePosition = isDeptAccount ? string.Empty : (currentUser?.EmployeePosition ?? string.Empty),
                    DestinationCompanyId    = isDeptAccount ? 0 : (currentUser?.CompanyId    ?? 0),
                    DestinationBranchId     = isDeptAccount ? 0 : (currentUser?.BranchId     ?? 0),
                    DestinationDepartmentId = isDeptAccount ? 0 : (currentUser?.DepartmentId ?? 0)
                },
                RequestItems = new List<CartridgeRequestItemViewModel> { new CartridgeRequestItemViewModel() },
                CartridgeModels        = _portalService.GetCartridgeModelsWithAvailability(),
                ConsumableModelsByCategory = LoadConsumableModelsByCategory(),
                Companies              = _portalService.GetActiveCompanies(),
                Branches               = _portalService.GetAllBranches(),
                Departments            = _portalService.GetAllDepartments(),
                Employees              = _portalService.GetActiveEmployees(),
                DeptAccountEmployees   = deptAccountEmployees
            };

            SetCommonViewBag(currentUser);
            ViewBag.IsDeptAccount  = isDeptAccount;
            ViewBag.CompanyName    = isDeptAccount ? (currentUser?.DepartmentAccountCompanyName    ?? "N/A") : (currentUser?.CompanyName    ?? "N/A");
            ViewBag.BranchName     = isDeptAccount ? (currentUser?.DepartmentAccountBranchName     ?? "N/A") : (currentUser?.BranchName     ?? "N/A");
            ViewBag.DepartmentName = isDeptAccount ? (currentUser?.DepartmentAccountDepartmentName ?? "N/A") : (currentUser?.DepartmentName ?? "N/A");

            return View(model);
        }

        /// <summary>
        /// Submits a new cartridge request using Model + Quantity selection.
        /// System resolves Model → ItemIds at submission time (FIFO order).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NewRequestPageViewModel model)
        {
            // Strip completely blank rows (e.g. user accidentally clicked "Add another model").
            // After stripping, clear all RequestItems ModelState keys — indices shift and stale
            // errors from the removed rows would otherwise keep ModelState invalid.
            if (model.RequestItems != null)
            {
                model.RequestItems = model.RequestItems
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.CartridgeModel))
                    .ToList();

                var staleItemKeys = ModelState.Keys
                    .Where(k => k.StartsWith("RequestItems["))
                    .ToList();
                foreach (var k in staleItemKeys)
                    ModelState.Remove(k);
            }

            bool hasRequestItems = model.RequestItems != null
                && model.RequestItems.Any(x => x != null && !string.IsNullOrWhiteSpace(x.CartridgeModel));

            if (hasRequestItems)
            {
                ModelState.Remove("Request.TypedModelNumber");
            }

            // For dept accounts, employee data is resolved server-side from SelectedEmployeeId
            // so the hidden form fields may be empty. Remove their validation errors.
            var currentUserCheck = GetCurrentUser();
            if (currentUserCheck?.IsDepartmentAccountSession == true)
            {
                ModelState.Remove("Request.EmployeeName");
                ModelState.Remove("Request.EmployeePosition");
                ModelState.Remove("Request.DestinationCompanyId");
                ModelState.Remove("Request.DestinationBranchId");
                ModelState.Remove("Request.DestinationDepartmentId");
            }

            // ReceivedById is only required for PICKUP; skip validation for DELIVERY.
            // CartridgeRequestViewModel.ReceivedById has no [Required] attribute (it's an int?
            // with no server-side annotation), so without this explicit check a PICKUP request
            // whose "Received By" combo was typed into but never actually clicked/committed
            // (the hidden field stays empty) silently saves with ReceivedById = NULL instead of
            // failing validation — exactly what let ReqId 1488 through with no receiver recorded.
            if (model.Request?.DistributionMethod == "DELIVERY")
            {
                ModelState.Remove("Request.ReceivedById");
            }
            else if (model.Request?.DistributionMethod == "PICKUP" &&
                     (!model.Request.ReceivedById.HasValue || model.Request.ReceivedById.Value <= 0))
            {
                ModelState.AddModelError("Request.ReceivedById", "Please select who will receive the cartridges (Received By).");
            }

            if (!ModelState.IsValid)
            {
                if (model.RequestItems == null || model.RequestItems.Count == 0)
                {
                    model.RequestItems = new List<CartridgeRequestItemViewModel>
                    {
                        new CartridgeRequestItemViewModel()
                    };
                }

                // Reload dropdown data
                model.CartridgeModels = _portalService.GetCartridgeModelsWithAvailability();
                model.ConsumableModelsByCategory = LoadConsumableModelsByCategory();
                model.Companies = _portalService.GetActiveCompanies();
                model.Branches = _portalService.GetAllBranches();
                model.Departments = _portalService.GetAllDepartments();
                var currentUserForReload = GetCurrentUser();
                bool isDeptAccReload = currentUserForReload?.IsDepartmentAccountSession == true;
                if (isDeptAccReload)
                {
                    int comId  = currentUserForReload!.DepartmentAccountCompanyId    ?? 0;
                    int deptId = currentUserForReload!.DepartmentAccountDepartmentId ?? 0;
                    int brId   = currentUserForReload!.DepartmentAccountBranchId     ?? 0;
                    var scopedEmps = (comId > 0 && deptId > 0 && brId > 0)
                        ? _portalService.GetEmployeesByAccount(comId, deptId, brId)
                        : new List<EmployeeViewModel>();
                    model.Employees            = scopedEmps;
                    model.DeptAccountEmployees = scopedEmps;
                }
                else
                {
                    model.Employees = _portalService.GetActiveEmployees();
                }
                SetCommonViewBag(currentUserForReload);
                ViewBag.IsDeptAccount  = isDeptAccReload;
                ViewBag.CompanyName    = isDeptAccReload ? (currentUserForReload?.DepartmentAccountCompanyName    ?? "N/A") : (currentUserForReload?.CompanyName    ?? "N/A");
                ViewBag.BranchName     = isDeptAccReload ? (currentUserForReload?.DepartmentAccountBranchName     ?? "N/A") : (currentUserForReload?.BranchName     ?? "N/A");
                ViewBag.DepartmentName = isDeptAccReload ? (currentUserForReload?.DepartmentAccountDepartmentName ?? "N/A") : (currentUserForReload?.DepartmentName ?? "N/A");
                return View("Index", model);
            }

            try
            {
                var currentUser = GetCurrentUser();

                // Dept account path: resolve selected employee
                EmployeeViewModel? deptAccountEmployee = null;
                if (currentUser?.IsDepartmentAccountSession == true)
                {
                    if (!model.SelectedEmployeeId.HasValue || model.SelectedEmployeeId.Value <= 0)
                    {
                        TempData["ErrorMessage"] = "Please select an employee to submit the request on behalf of.";
                        return RedirectToAction("Index");
                    }

                    var employees = _portalService.GetActiveEmployees();
                    deptAccountEmployee = employees.FirstOrDefault(e => e.EmpId == model.SelectedEmployeeId.Value);
                    if (deptAccountEmployee == null)
                    {
                        TempData["ErrorMessage"] = "Selected employee not found. Please try again.";
                        return RedirectToAction("Index");
                    }
                }
                else if (currentUser == null || !currentUser.EmployeeId.HasValue || currentUser.EmployeeId.Value <= 0)
                {
                    // HARD REQUIREMENT for non-dept-account: Employee ID must exist in session
                    TempData["ErrorMessage"] = "Your account is not linked to an employee. Please contact an administrator to complete your account setup.";
                    return RedirectToAction("Index");
                }

                // Set audit fields from session
                model.Request.CreatedByUserId = currentUser!.UserId;
                model.Request.DateRequested = DateTime.Now;

                // CRITICAL: Override destination and requester fields
                // For dept accounts: use selected employee's data as source of truth
                // For normal employees: use session employee data
                if (deptAccountEmployee != null)
                {
                    model.Request.EmployeeName           = deptAccountEmployee.Name;
                    model.Request.EmployeePosition       = deptAccountEmployee.Position;
                    model.Request.DestinationCompanyId   = deptAccountEmployee.ComId;
                    model.Request.DestinationBranchId    = deptAccountEmployee.BranchId;
                    model.Request.DestinationDepartmentId = deptAccountEmployee.DeptId;
                    model.Request.DestinationCompanyName  = deptAccountEmployee.CompanyName;
                    model.Request.DestinationBranchName   = deptAccountEmployee.BranchName;
                    model.Request.DestinationDepartmentName = deptAccountEmployee.DepartmentName;
                }
                else
                {
                    model.Request.DestinationCompanyId      = currentUser.CompanyId    ?? 0;
                    model.Request.DestinationBranchId       = currentUser.BranchId     ?? 0;
                    model.Request.DestinationDepartmentId   = currentUser.DepartmentId ?? 0;
                    model.Request.DestinationCompanyName    = currentUser.CompanyName;
                    model.Request.DestinationBranchName     = currentUser.BranchName;
                    model.Request.DestinationDepartmentName = currentUser.DepartmentName;
                }

                // Resolve ReceivedByName for snapshot display (lookup from employee list)
                if (model.Request.DistributionMethod == "PICKUP" && model.Request.ReceivedById.HasValue)
                {
                    var employees = ReloadEmployeesForUser(currentUser);
                    var receiver = employees.FirstOrDefault(e => e.EmpId == model.Request.ReceivedById.Value);
                    model.Request.ReceivedByName = receiver?.Name;
                }

                // Determine the employee ID and department ID to use for the request
                int submittingEmpId  = deptAccountEmployee != null ? deptAccountEmployee.EmpId  : currentUser!.EmployeeId!.Value;
                int submittingDeptId = deptAccountEmployee != null ? deptAccountEmployee.DeptId  : (currentUser!.DepartmentId ?? 0);

                // Submit request — created as 'Awaiting Authorization' (not yet visible on fulfillment)
                List<int> requestIds = _portalService.CreateCartridgeRequestByModel(
                    model.Request,
                    model.RequestItems ?? new List<CartridgeRequestItemViewModel>(),
                    submittingEmpId,
                    out Guid submissionSessionId);

                // Create authorization linked to this submission.
                // IsDeveloper / LevelRank >= 999 accounts are auto-approved.
                // All other employees (including higher-up positions) get Pending so they can self-sign.
                bool wasAutoApproved = false;
                int newAuthId = 0;
                if (submittingDeptId > 0)
                {
                    string modelsForAuth = model.RequestItems != null && model.RequestItems.Any()
                        ? System.Text.Json.JsonSerializer.Serialize(
                            model.RequestItems
                                .Where(x => !string.IsNullOrWhiteSpace(x.CartridgeModel))
                                .Select(x => new { model = x.CartridgeModel, qty = x.Quantity, good = x.GoodEmptyQty, damaged = x.DamagedEmptyQty })
                                .ToList())
                        : model.Request.TypedModelNumber ?? model.Request.ModelNumber ?? string.Empty;

                    (wasAutoApproved, newAuthId) = await _authRepo.CreateAutoOrPendingAsync(
                        submittingEmpId,
                        submittingDeptId,
                        submissionSessionId,
                        modelsForAuth,
                        currentUser!.UserId);
                }

                _logger.LogInformation("User {UserId} submitted {Count} request(s): {RequestIds}",
                    currentUser!.UserId, requestIds.Count, string.Join(", ", requestIds));

                // Log each created request to dbo.UserActivityLog so portal users appear in the audit trail.
                string submitterName = deptAccountEmployee != null
                    ? $"{currentUser.UserName} (on behalf of {deptAccountEmployee.Name})"
                    : (currentUser.EmployeeName ?? currentUser.UserName);
                foreach (int reqId in requestIds)
                {
                    _activityLog.LogActivity(
                        currentUser.UserId,
                        "Create",
                        "Request",
                        reqId,
                        $"[Portal] Request #{reqId} submitted by {submitterName}");
                }

                // Create submission snapshot to show in My Requests (Google Forms-style confirmation)
                // Captures EXACTLY what the user entered, not queried from unreliable historical data
                var snapshot = new RequestSubmissionSnapshotViewModel
                {
                    CartridgeModel = model.RequestItems != null && model.RequestItems.Count > 0
                        ? model.RequestItems[0].CartridgeModel
                        : (!string.IsNullOrWhiteSpace(model.Request.TypedModelNumber)
                            ? model.Request.TypedModelNumber
                            : model.Request.ModelNumber ?? string.Empty),
                    Quantity = model.RequestItems != null && model.RequestItems.Count > 0
                        ? model.RequestItems.Sum(x => x.Quantity)
                        : model.Request.Quantity,
                    GoodEmptyQty = model.RequestItems != null && model.RequestItems.Count > 0
                        ? model.RequestItems.Sum(x => x.GoodEmptyQty)
                        : model.Request.GoodEmptyQty,
                    DamagedEmptyQty = model.RequestItems != null && model.RequestItems.Count > 0
                        ? model.RequestItems.Sum(x => x.DamagedEmptyQty)
                        : model.Request.DamagedEmptyQty,
                    Items = model.RequestItems != null
                        ? model.RequestItems
                            .Where(x => x != null)
                            .Select(x => new RequestSubmissionItemSnapshotViewModel
                            {
                                CartridgeModel = x.CartridgeModel,
                                Quantity = x.Quantity,
                                GoodEmptyQty = x.GoodEmptyQty,
                                DamagedEmptyQty = x.DamagedEmptyQty
                            })
                            .ToList()
                        : new List<RequestSubmissionItemSnapshotViewModel>(),
                    RequesterName = model.Request.EmployeeName,
                    RequesterPosition = model.Request.EmployeePosition,
                    Company    = model.Request.DestinationCompanyName    ?? "N/A",
                    Branch     = model.Request.DestinationBranchName     ?? "N/A",
                    Department = model.Request.DestinationDepartmentName ?? "N/A",
                    DistributionMethod = model.Request.DistributionMethod,
                    AdditionalRemarks = model.Request.AdditionalRemarks,
                    SubmissionDate = DateTime.Now,
                    RequestIds = requestIds,
                    ReceivedByName = model.Request.ReceivedByName
                };

                // Store snapshot in session for My Requests view (lightweight, session-scoped)
                HttpContext.Session.SetObject("LastSubmissionSnapshot", snapshot);

                // Personal-account higher-ups sign their own request immediately after submission.
                // Dept account submissions for higher-ups still go through supervisor queue.
                bool isSelfSign = deptAccountEmployee == null
                    && !wasAutoApproved
                    && newAuthId > 0
                    && !string.IsNullOrWhiteSpace(currentUser!.EmployeePosition)
                    && ApproverPositions.Contains(currentUser.EmployeePosition.Trim());

                if (isSelfSign)
                    return RedirectToAction("SupervisorQueue", "Authorization", new { selectedId = newAuthId });

                int requestedModels = model.RequestItems?.Count ?? 0;
                string message = requestedModels <= 1
                    ? "Request submitted successfully!"
                    : $"Request submitted successfully! {requestedModels} model(s) requested.";

                TempData["SuccessMessage"] = message + (wasAutoApproved
                    ? " You can view your submission in the 'My Submitted Requests' tab."
                    : " Your request is awaiting supervisor authorization before it will be processed.");

                return RedirectToAction("MyRequests");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting request");
                ModelState.AddModelError("", ex.ToUserMessage(_environment, "Error submitting request"));

                // Reload dropdown data
                model.CartridgeModels = _portalService.GetCartridgeModelsWithAvailability();
                model.ConsumableModelsByCategory = LoadConsumableModelsByCategory();
                model.Companies = _portalService.GetActiveCompanies();
                model.Branches = _portalService.GetAllBranches();
                model.Departments = _portalService.GetAllDepartments();
                var errorUser = GetCurrentUser();
                model.Employees = ReloadEmployeesForUser(errorUser);
                bool isDeptAccError = errorUser?.IsDepartmentAccountSession == true;
                if (isDeptAccError)
                {
                    int comId  = errorUser!.DepartmentAccountCompanyId    ?? 0;
                    int deptId = errorUser!.DepartmentAccountDepartmentId ?? 0;
                    int brId   = errorUser!.DepartmentAccountBranchId     ?? 0;
                    if (comId > 0 && deptId > 0 && brId > 0)
                        model.DeptAccountEmployees = _portalService.GetEmployeesByAccount(comId, deptId, brId);
                }
                SetCommonViewBag(errorUser);
                ViewBag.IsDeptAccount  = isDeptAccError;
                ViewBag.CompanyName    = isDeptAccError ? (errorUser?.DepartmentAccountCompanyName    ?? "N/A") : (errorUser?.CompanyName    ?? "N/A");
                ViewBag.BranchName     = isDeptAccError ? (errorUser?.DepartmentAccountBranchName     ?? "N/A") : (errorUser?.BranchName     ?? "N/A");
                ViewBag.DepartmentName = isDeptAccError ? (errorUser?.DepartmentAccountDepartmentName ?? "N/A") : (errorUser?.DepartmentName ?? "N/A");
                return View("Index", model);
            }
        }

        [HttpGet]
        public IActionResult MyRequests(string? search, string? status) =>
            RequestHistoryPage(search, status, deptLevelView: false);

        /// <summary>
        /// "My Department History": every portal request for the user's department (all its
        /// employees' requests and the Dept. Level ones), plus the user's own submissions, for
        /// employee and department accounts alike. Same view as MyRequests.
        /// MATCHES: Yakult.Inventory.App Request Portal "My Department History" tab.
        /// </summary>
        [HttpGet]
        public IActionResult DeptRequests(string? search, string? status) =>
            RequestHistoryPage(search, status, deptLevelView: true);

        private IActionResult RequestHistoryPage(string? search, string? status, bool deptLevelView)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var model = BuildMyRequestsPageModel(currentUser, search, status, deptLevelView);
                SetCommonViewBag(currentUser);
                return View("MyRequests", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading request history");
                TempData["ErrorMessage"] = ex.ToUserMessage(_environment, "Error loading request history");
                return View("MyRequests", new MyRequestsPageViewModel { IsDeptLevelView = deptLevelView });
            }
        }

        /// <summary>
        /// AJAX partner to MyRequests — returns just the _MyRequestsHistoryCard partial (search
        /// box, table, pagination) so the page can swap it in without a full navigation when the
        /// user searches, changes the status filter, clears, or refreshes.
        /// </summary>
        [HttpGet]
        public IActionResult MyRequestsHistoryPartial(string? search, string? status) =>
            PartialView("_MyRequestsHistoryCard", BuildMyRequestsPageModel(GetCurrentUser(), search, status, deptLevelView: false));

        /// <summary>AJAX partner to DeptRequests (see MyRequestsHistoryPartial).</summary>
        [HttpGet]
        public IActionResult DeptRequestsHistoryPartial(string? search, string? status) =>
            PartialView("_MyRequestsHistoryCard", BuildMyRequestsPageModel(GetCurrentUser(), search, status, deptLevelView: true));

        private MyRequestsPageViewModel BuildMyRequestsPageModel(UserSessionModel? currentUser, string? search, string? status, bool deptLevelView)
        {
            var fullHistory = currentUser != null ? BuildMyRequestsHistory(currentUser, deptLevelView) : new List<RequestHistoryRowViewModel>();

            ViewBag.StatusOptions = fullHistory.Select(h => h.Status).Distinct().OrderBy(s => s).ToList();
            ViewBag.SearchText    = search ?? string.Empty;
            ViewBag.StatusFilter  = status ?? string.Empty;

            return new MyRequestsPageViewModel
            {
                History         = FilterMyRequestsHistory(fullHistory, search, status),
                IsDeptLevelView = deptLevelView
            };
        }

        /// <summary>
        /// Downloads the same rows shown on MyRequests as a CSV — one row per submission
        /// session, matching what's on screen (not the raw per-Request rows underneath),
        /// with the same search/status filter applied so the export matches the current view.
        /// </summary>
        [HttpGet]
        public IActionResult ExportMyRequestsCsv(string? search, string? status) =>
            ExportRequestHistoryCsv(search, status, deptLevelView: false);

        /// <summary>CSV of the My Department History page (see ExportMyRequestsCsv).</summary>
        [HttpGet]
        public IActionResult ExportDeptRequestsCsv(string? search, string? status) =>
            ExportRequestHistoryCsv(search, status, deptLevelView: true);

        private IActionResult ExportRequestHistoryCsv(string? search, string? status, bool deptLevelView)
        {
            var currentUser = GetCurrentUser();
            var fullHistory = currentUser != null ? BuildMyRequestsHistory(currentUser, deptLevelView) : new List<RequestHistoryRowViewModel>();
            var history = FilterMyRequestsHistory(fullHistory, search, status);

            var headers = new[]
            {
                "Set Code", "Date Requested", "Cartridge/Item", "Total Qty", "Returned (Good/Damaged)",
                "Fulfillment Method", "Destination Branch", "Status", "Employee", "Department", "Company", "Remarks"
            };
            var rows = history.Select(h => new object?[]
            {
                h.SetCode, h.DateRequested?.ToString("yyyy-MM-dd HH:mm"), h.CartridgeDisplay, h.TotalQty,
                h.ReturnInfo, h.FulfillmentMethod, h.DestinationBranch, h.Status, h.EmployeeName,
                h.Department, h.Company, h.Remarks
            });

            var csv = CsvExportExtensions.ToCsvBytes(headers, rows);
            string prefix = deptLevelView ? "my-department-history" : "my-requests";
            return File(csv, "text/csv", $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        /// <summary>
        /// Builds the grouped (one row per submission session) history shown on MyRequests.
        /// Shared by the page itself and ExportMyRequestsCsv so the CSV always matches what's
        /// on screen.
        /// </summary>
        private List<RequestHistoryRowViewModel> BuildMyRequestsHistory(UserSessionModel currentUser, bool deptLevelView)
        {
            var history = new List<RequestHistoryRowViewModel>();
            // MATCHES: Yakult.Inventory.App RequestHistoryViewModel.LoadAsync.
            // The user's department: the department account's, or the employee's own.
            bool isDeptAccount = currentUser.IsDepartmentAccountSession;
            int? comId    = isDeptAccount ? currentUser.DepartmentAccountCompanyId    : currentUser.CompanyId;
            int? branchId = isDeptAccount ? currentUser.DepartmentAccountBranchId     : currentUser.BranchId;
            int? deptId   = isDeptAccount ? currentUser.DepartmentAccountDepartmentId : currentUser.DepartmentId;

            List<PortalRequestStatusViewModel> raw;
            if (deptLevelView)
                // My Department History page: every portal request for the user's department
                // (every employee's, including the user's own, and the Dept. Level ones,
                // highlighted), plus anything the user submitted themselves.
                raw = _portalService.GetPortalRequestsByUser(currentUser.UserId, comId, branchId, deptId);
            else if (isDeptAccount)
                // A Department Account's history: every portal request for its department
                // (Dept. Level ones and its employees'); Dept. Level rows are highlighted.
                raw = _portalService.GetPortalRequestsByUser(currentUser.UserId, comId, branchId, deptId);
            else
                // An employee's history: their own submissions only. The department's Dept.
                // Level requests are on the My Department History page.
                raw = _portalService.GetPortalRequestsByUser(currentUser.UserId);

            // Group by SubmissionSessionId (or ReqId if session not set)
            var grouped = raw
                .GroupBy(x => x.SubmissionSessionId.HasValue
                    ? x.SubmissionSessionId.Value.ToString()
                    : x.ReqId.ToString())
                .OrderByDescending(g => g.Max(x => x.DateCreated));

            foreach (var g in grouped)
            {
                var items = g.ToList();
                var first = items[0];

                string cartridgeDisplay = items.Count == 1
                    ? (first.CartridgeName ?? first.ItemName ?? "—")
                    : string.Join(", ", items
                        .Select(x => x.CartridgeName ?? x.ItemName ?? "—")
                        .Distinct());

                int totalGood    = items.Sum(x => x.GoodEmptyQty);
                int totalDamaged = items.Sum(x => x.DamagedEmptyQty);
                string returnInfo = (totalGood > 0 || totalDamaged > 0)
                    ? $"G:{totalGood} D:{totalDamaged}"
                    : string.Empty;

                string status = AggregateGroupStatus(items.Select(x => x.Status).ToList());

                history.Add(new RequestHistoryRowViewModel
                {
                    SetCode           = !string.IsNullOrWhiteSpace(first.SetCode) ? first.SetCode : "—",
                    DateRequested     = first.DateRequested,
                    CartridgeDisplay  = cartridgeDisplay,
                    TotalQty          = items.Sum(x => x.Quantity),
                    ReturnInfo        = returnInfo,
                    FulfillmentMethod = first.DistributionMethod ?? "—",
                    DestinationBranch = first.DestinationBranch ?? "—",
                    Status            = status,
                    // No employee on the Request row = a Dept. Level request.
                    EmployeeName      = string.IsNullOrWhiteSpace(first.DestinationEmployeeName) || first.DestinationEmployeeName == "—"
                                        ? "Dept. Level"
                                        : first.DestinationEmployeeName,
                    Department        = first.DestinationDepartment ?? "—",
                    Company           = first.DestinationCompany ?? "—",
                    Remarks           = items.Select(x => {
                                            var full = x.FullRemarks;
                                            if (string.IsNullOrWhiteSpace(full)) return null;
                                            var sep = full.IndexOf(" | ", StringComparison.Ordinal);
                                            return sep >= 0 ? full.Substring(sep + 3).Trim() : null;
                                        }).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)) ?? string.Empty,
                    ReqIds            = items.Select(x => x.ReqId).ToList(),
                    Items             = items.Select(x => new RequestDetailItemViewModel
                    {
                        CartridgeModel = x.CartridgeName ?? x.ItemName ?? "—",
                        Qty            = x.Quantity,
                        GoodQty        = x.GoodEmptyQty,
                        DamagedQty     = x.DamagedEmptyQty
                    }).ToList()
                });
            }

            return history;
        }

        /// <summary>
        /// Filters an already-built history list in memory — the full result set is loaded
        /// from SQL either way (BuildMyRequestsHistory has no filter params), so this is a
        /// display-layer filter, not a query optimization. Search matches Set Code, cartridge/
        /// item display, employee, department, company, and remarks.
        /// </summary>
        private static List<RequestHistoryRowViewModel> FilterMyRequestsHistory(
            List<RequestHistoryRowViewModel> history, string? search, string? status)
        {
            IEnumerable<RequestHistoryRowViewModel> filtered = history;

            if (!string.IsNullOrWhiteSpace(status))
                filtered = filtered.Where(h => string.Equals(h.Status, status, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim();
                filtered = filtered.Where(h =>
                    Contains(h.SetCode, q) || Contains(h.CartridgeDisplay, q) || Contains(h.EmployeeName, q) ||
                    Contains(h.Department, q) || Contains(h.Company, q) || Contains(h.Remarks, q));
            }

            return filtered.ToList();
        }

        private static bool Contains(string? haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the details of a specific request.
        /// </summary>
        [HttpGet]
        public IActionResult Details(int id)
        {
            var request = _portalService.GetRequestStatus(id);
            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        /// <summary>
        /// AJAX endpoint to get branches by company.
        /// NOTE: Not used in current implementation (dropdowns are independent)
        /// but kept for future use if filtering is needed.
        /// </summary>
        [HttpGet]
        public IActionResult GetBranchesByCompany(int companyId)
        {
            var branches = _portalService.GetBranchesByCompany(companyId);
            return Json(branches);
        }

        /// <summary>
        /// AJAX endpoint to get departments by company.
        /// NOTE: Not used in current implementation (dropdowns are independent)
        /// but kept for future use if filtering is needed.
        /// </summary>
        [HttpGet]
        public IActionResult GetDepartmentsByCompany(int companyId)
        {
            var departments = _portalService.GetDepartmentsByCompany(companyId);
            return Json(departments);
        }

        /// <summary>
        /// AJAX endpoint to get cartridge by model number (for auto-select).
        /// TRANSLATED FROM: RequesterPortalForm.TryAutoSelectCartridgeByModel()
        /// </summary>
        [HttpGet]
        public IActionResult FindCartridgeByModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return Json(new { found = false });
            }

            var cartridges = _portalService.GetCartridgeItems();
            var q = model.Trim().ToUpperInvariant();

            // Exact match first
            var match = cartridges.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.ModelNumber) &&
                c.ModelNumber.Trim().ToUpperInvariant() == q);

            // Partial match if no exact match
            if (match == null)
            {
                match = cartridges.FirstOrDefault(c =>
                    (!string.IsNullOrWhiteSpace(c.ModelNumber) && c.ModelNumber.ToUpperInvariant().Contains(q)) ||
                    (!string.IsNullOrWhiteSpace(c.ItemName) && c.ItemName.ToUpperInvariant().Contains(q)));
            }

            if (match != null)
            {
                return Json(new { found = true, itemId = match.ItemId, displayName = match.DisplayName });
            }

            return Json(new { found = false });
        }

        [HttpGet]
        public IActionResult AssistedRequest()
        {
            var currentUser = GetCurrentUser();
            if (currentUser?.IsITStaff != true)
                return RedirectToAction("Index");

            var model = new NewRequestPageViewModel
            {
                Request = new CartridgeRequestViewModel(),
                RequestItems = new List<CartridgeRequestItemViewModel> { new CartridgeRequestItemViewModel() },
                CartridgeModels      = _portalService.GetCartridgeModelsWithAvailability(),
                ConsumableModelsByCategory = LoadConsumableModelsByCategory(),
                Companies            = _portalService.GetActiveCompanies(),
                Branches             = _portalService.GetAllBranches(),
                Departments          = _portalService.GetAllDepartments(),
                Employees            = _portalService.GetActiveEmployees()
            };

            SetCommonViewBag(currentUser);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAssisted(NewRequestPageViewModel model)
        {
            var currentUser = GetCurrentUser();
            if (currentUser?.IsITStaff != true)
                return RedirectToAction("Index");

            // Strip blank model rows
            if (model.RequestItems != null)
            {
                model.RequestItems = model.RequestItems
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.CartridgeModel))
                    .ToList();

                var staleKeys = ModelState.Keys
                    .Where(k => k.StartsWith("RequestItems["))
                    .ToList();
                foreach (var k in staleKeys)
                    ModelState.Remove(k);
            }

            bool hasRequestItems = model.RequestItems != null
                && model.RequestItems.Any(x => !string.IsNullOrWhiteSpace(x.CartridgeModel));

            if (hasRequestItems)
                ModelState.Remove("Request.TypedModelNumber");

            // Employee is resolved server-side from SelectedEmployeeId
            ModelState.Remove("Request.EmployeeName");
            ModelState.Remove("Request.EmployeePosition");
            ModelState.Remove("Request.DestinationCompanyId");
            ModelState.Remove("Request.DestinationBranchId");
            ModelState.Remove("Request.DestinationDepartmentId");

            // See the same check in Create() above — ReceivedById has no [Required] attribute,
            // so this explicit guard is what actually stops a PICKUP request from silently
            // saving with no receiver when the combo's selection never committed.
            if (model.Request?.DistributionMethod == "DELIVERY")
            {
                ModelState.Remove("Request.ReceivedById");
            }
            else if (model.Request?.DistributionMethod == "PICKUP" &&
                     (!model.Request.ReceivedById.HasValue || model.Request.ReceivedById.Value <= 0))
            {
                ModelState.AddModelError("Request.ReceivedById", "Please select who will receive the cartridges (Received By).");
            }

            if (!ModelState.IsValid)
            {
                model.CartridgeModels = _portalService.GetCartridgeModelsWithAvailability();
                model.ConsumableModelsByCategory = LoadConsumableModelsByCategory();
                model.Companies       = _portalService.GetActiveCompanies();
                model.Branches        = _portalService.GetAllBranches();
                model.Departments     = _portalService.GetAllDepartments();
                model.Employees       = _portalService.GetActiveEmployees();
                if (model.RequestItems == null || model.RequestItems.Count == 0)
                    model.RequestItems = new List<CartridgeRequestItemViewModel> { new CartridgeRequestItemViewModel() };
                SetCommonViewBag(currentUser);
                return View("AssistedRequest", model);
            }

            try
            {
                var allEmployees = _portalService.GetActiveEmployees();
                EmployeeViewModel? targetEmployee = null;

                if (model.Request.IsDeptLevel)
                {
                    // Dept-level: no target employee. Company/Branch/Department are all optional
                    // ("Fill in only what you have") and are already bound directly onto
                    // model.Request.DestinationCompanyId/BranchId/DepartmentId from the form —
                    // Request.EmpId stays NULL and those three go straight onto the Request row.
                    model.Request.EmployeeName     = string.Empty;
                    model.Request.EmployeePosition = string.Empty;

                    var companies   = _portalService.GetActiveCompanies();
                    var branches    = _portalService.GetAllBranches();
                    var departments = _portalService.GetAllDepartments();
                    model.Request.DestinationCompanyName    = companies.FirstOrDefault(c => c.ComId == model.Request.DestinationCompanyId)?.Name;
                    model.Request.DestinationBranchName     = branches.FirstOrDefault(b => b.BranchId == model.Request.DestinationBranchId)?.Name;
                    model.Request.DestinationDepartmentName = departments.FirstOrDefault(d => d.DeptId == model.Request.DestinationDepartmentId)?.Name;
                }
                else
                {
                    if (!model.SelectedEmployeeId.HasValue || model.SelectedEmployeeId.Value <= 0)
                    {
                        TempData["ErrorMessage"] = "Please select the employee you are submitting on behalf of.";
                        return RedirectToAction("AssistedRequest");
                    }

                    targetEmployee = allEmployees.FirstOrDefault(e => e.EmpId == model.SelectedEmployeeId.Value);
                    if (targetEmployee == null)
                    {
                        TempData["ErrorMessage"] = "Selected employee not found. Please try again.";
                        return RedirectToAction("AssistedRequest");
                    }

                    model.Request.EmployeeName               = targetEmployee.Name;
                    model.Request.EmployeePosition           = targetEmployee.Position;
                    model.Request.DestinationCompanyId       = targetEmployee.ComId;
                    model.Request.DestinationBranchId        = targetEmployee.BranchId;
                    model.Request.DestinationDepartmentId    = targetEmployee.DeptId;
                    model.Request.DestinationCompanyName     = targetEmployee.CompanyName;
                    model.Request.DestinationBranchName      = targetEmployee.BranchName;
                    model.Request.DestinationDepartmentName  = targetEmployee.DepartmentName;
                }

                model.Request.CreatedByUserId = currentUser!.UserId;
                model.Request.DateRequested   = model.Request.DateRequested ?? DateTime.Now;

                if (model.Request.DistributionMethod == "PICKUP" && model.Request.ReceivedById.HasValue)
                {
                    var receiver = allEmployees.FirstOrDefault(e => e.EmpId == model.Request.ReceivedById.Value);
                    model.Request.ReceivedByName = receiver?.Name;
                }

                int? targetEmpId = targetEmployee?.EmpId;

                List<int> requestIds = _portalService.CreateAssistedCartridgeRequestByModel(
                    model.Request,
                    model.RequestItems ?? new List<CartridgeRequestItemViewModel>(),
                    targetEmpId,
                    out Guid submissionSessionId);

                string modelsForAuth = model.RequestItems != null && model.RequestItems.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(
                        model.RequestItems
                            .Where(x => !string.IsNullOrWhiteSpace(x.CartridgeModel))
                            .Select(x => new { model = x.CartridgeModel, qty = x.Quantity, good = x.GoodEmptyQty, damaged = x.DamagedEmptyQty })
                            .ToList())
                    : model.Request.TypedModelNumber ?? model.Request.ModelNumber ?? string.Empty;

                int count = model.RequestItems?.Count ?? 1;

                // Display label for messages/logs — falls back to the department name (or a
                // generic label) when this is a dept-level request with no target employee.
                string targetLabel = targetEmployee?.Name
                    ?? model.Request.DestinationDepartmentName
                    ?? "the department";
                int targetDeptId = targetEmployee?.DeptId ?? model.Request.DestinationDepartmentId;

                if (model.ITQuickAutoApproved)
                {
                    // "Auto-Approved" checkbox: record an offline Approved decision with no
                    // approver / remarks (mirrors the desktop AssistedRequest quick auto-approve).
                    // Takes precedence over the Manual Authorization toggle.
                    await _authRepo.CreateITAssistedAsync(
                        targetEmpId ?? 0,
                        targetDeptId,
                        submissionSessionId,
                        modelsForAuth,
                        currentUser!.UserId,
                        authorizedByEmpId: 0,
                        decision: "Approved",
                        remarks: null);

                    _logger.LogInformation(
                        "IT-assisted (Auto-Approved): User {UserId} submitted {Count} request(s) for target {TargetLabel}: {RequestIds}",
                        currentUser!.UserId, requestIds.Count, targetLabel, string.Join(", ", requestIds));

                    foreach (int reqId in requestIds)
                        _activityLog.LogActivity(currentUser.UserId, "Create", "Request", reqId,
                            $"[Portal Assisted — Auto-Approved] Request #{reqId} submitted by {currentUser.UserName} on behalf of {targetLabel}");

                    TempData["SuccessMessage"] = count <= 1
                        ? $"Assisted request submitted for {targetLabel}. Authorization recorded as auto-approved."
                        : $"Assisted request submitted for {targetLabel} ({count} model(s)). Authorization recorded as auto-approved.";
                }
                else if (model.ITUseManualAuth)
                {
                    // Manual Authorization toggle is ON: IT records an offline/verbal decision
                    if (!model.ITAuthorizedByEmpId.HasValue || model.ITAuthorizedByEmpId.Value <= 0)
                    {
                        TempData["ErrorMessage"] = "Please select the authorizing approver.";
                        return RedirectToAction("AssistedRequest");
                    }

                    string itDecision = (model.ITDecision ?? string.Empty).Trim();
                    if (itDecision != "Approved" && itDecision != "Rejected")
                    {
                        TempData["ErrorMessage"] = "Please select a valid authorization decision (Approved or Rejected).";
                        return RedirectToAction("AssistedRequest");
                    }
                    if (itDecision == "Rejected" && string.IsNullOrWhiteSpace(model.ITRemarks))
                    {
                        TempData["ErrorMessage"] = "Remarks are required when the authorization decision is Rejected.";
                        return RedirectToAction("AssistedRequest");
                    }

                    await _authRepo.CreateITAssistedAsync(
                        targetEmpId ?? 0,
                        targetDeptId,
                        submissionSessionId,
                        modelsForAuth,
                        currentUser!.UserId,
                        model.ITAuthorizedByEmpId.Value,
                        itDecision,
                        model.ITRemarks?.Trim());

                    string decisionLabel = itDecision == "Approved" ? "approved" : "rejected";
                    _logger.LogInformation(
                        "IT-assisted (Manual Auth, {Decision}): User {UserId} submitted {Count} request(s) for target {TargetLabel}: {RequestIds}",
                        itDecision, currentUser!.UserId, requestIds.Count, targetLabel, string.Join(", ", requestIds));

                    foreach (int reqId in requestIds)
                        _activityLog.LogActivity(currentUser.UserId, "Create", "Request", reqId,
                            $"[Portal Assisted — Manual Auth] Request #{reqId} submitted by {currentUser.UserName} on behalf of {targetLabel} — Authorization: {itDecision}");

                    TempData["SuccessMessage"] = count <= 1
                        ? $"Assisted request submitted for {targetLabel}. Authorization recorded as {decisionLabel}."
                        : $"Assisted request submitted for {targetLabel} ({count} model(s)). Authorization recorded as {decisionLabel}.";
                }
                else
                {
                    // Manual Authorization toggle is OFF: create Pending auth for digital approval
                    await _authRepo.CreateITPendingAsync(
                        targetEmpId ?? 0,
                        targetDeptId,
                        submissionSessionId,
                        modelsForAuth,
                        currentUser!.UserId);

                    _logger.LogInformation(
                        "IT-assisted (Pending): User {UserId} submitted {Count} request(s) for target {TargetLabel} → Pending digital approval: {RequestIds}",
                        currentUser!.UserId, requestIds.Count, targetLabel, string.Join(", ", requestIds));

                    foreach (int reqId in requestIds)
                        _activityLog.LogActivity(currentUser.UserId, "Create", "Request", reqId,
                            $"[Portal Assisted] Request #{reqId} submitted by {currentUser.UserName} on behalf of {targetLabel} — Pending digital approval");

                    TempData["SuccessMessage"] = count <= 1
                        ? $"Assisted request submitted for {targetLabel}. Awaiting digital approval."
                        : $"Assisted request submitted for {targetLabel} ({count} model(s)). Awaiting digital approval.";
                }

                return RedirectToAction("MyRequests");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting assisted request");
                ModelState.AddModelError("", ex.ToUserMessage(_environment, "Error submitting request"));
                model.CartridgeModels = _portalService.GetCartridgeModelsWithAvailability();
                model.ConsumableModelsByCategory = LoadConsumableModelsByCategory();
                model.Companies       = _portalService.GetActiveCompanies();
                model.Branches        = _portalService.GetAllBranches();
                model.Departments     = _portalService.GetAllDepartments();
                model.Employees       = _portalService.GetActiveEmployees();
                SetCommonViewBag(currentUser);
                return View("AssistedRequest", model);
            }
        }

        /// <summary>
        /// AJAX: Returns approvers scoped to the target employee's branch and department.
        /// Called when the IT user selects an employee on the Assisted Request form.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetApproversForEmployee(int employeeId)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (currentUser == null)
                    return Json(new { error = "DEBUG: GetCurrentUser() returned null — session may have expired." });

                // IT Assisted Request is used by IT users (developers), not regular approvers
                if (!currentUser.IsDeveloper)
                    return Json(new { error = $"DEBUG: User '{currentUser.UserName}' IsDeveloper=false — access denied." });

                var allEmployees = _portalService.GetActiveEmployees();
                var emp = allEmployees.FirstOrDefault(e => e.EmpId == employeeId);
                if (emp == null)
                    return Json(new { error = $"DEBUG: Employee with EmpId={employeeId} not found in GetActiveEmployees()." });

                // Log scope values so we can verify they're populated
                var scope = new { emp.EmpId, emp.ComId, emp.BranchId, emp.DeptId, emp.Name };

                var approvers = await _portalService.GetApproversByScope(emp.ComId, emp.BranchId, emp.DeptId);
                return Json(new
                {
                    debug_scope = scope,
                    debug_count = approvers.Count,
                    approvers   = approvers.Select(a => new { a.EmpId, a.DisplayName, a.Position, a.ApprovalRole })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving approvers for employee");
                return Json(new { error = ex.ToUserMessage(_environment, "Error resolving approvers") });
            }
        }

        /// <summary>
        /// AJAX: Returns approvers scoped directly to a Company/Branch/Department, for the
        /// Dept-Level (no employee) mode of IT Assisted Request — there is no target employee
        /// to derive scope from, so the raw ids selected in the Dept Level Target section are
        /// used instead.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetApproversForScope(int comId, int branchId, int deptId)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (currentUser == null)
                    return Json(new { error = "Session may have expired." });

                if (!currentUser.IsDeveloper)
                    return Json(new { error = $"User '{currentUser.UserName}' is not authorized for this lookup." });

                if (comId <= 0 || branchId <= 0 || deptId <= 0)
                    return Json(new { approvers = Array.Empty<object>() });

                var approvers = await _portalService.GetApproversByScope(comId, branchId, deptId);
                return Json(new
                {
                    approvers = approvers.Select(a => new { a.EmpId, a.DisplayName, a.Position, a.ApprovalRole })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving approvers for scope");
                return Json(new { error = ex.ToUserMessage(_environment, "Error resolving approvers") });
            }
        }

    }
}
