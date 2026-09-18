using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// The "first-pass" cartridge exchange fulfillment queue (pending requests that have never
    /// been attempted) plus the read-only fulfilled-history view. Mirrors desktop's
    /// CartridgeManagementViewModel / CartridgeManagementView ("Cartridge Exchange" page).
    /// </summary>
    [RequireITDepartment]
    public class CartridgeExchangeController : Controller
    {
        private readonly ICartridgeExchangeRepository _repo;
        private readonly ICartridgeFulfillmentRepository _cartridgeFulfillmentRepo;
        private readonly IActivityLogRepository _activityLog;
        private readonly INotificationRepository _notificationRepo;

        public CartridgeExchangeController(
            ICartridgeExchangeRepository repo,
            ICartridgeFulfillmentRepository cartridgeFulfillmentRepo,
            IActivityLogRepository activityLog,
            INotificationRepository notificationRepo)
        {
            _repo = repo;
            _cartridgeFulfillmentRepo = cartridgeFulfillmentRepo;
            _activityLog = activityLog;
            _notificationRepo = notificationRepo;
        }

        private UserSessionModel? GetCurrentUser()
        {
            return HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
        }

        private void SetCommonViewBag(UserSessionModel? user)
        {
            ViewBag.CurrentUserName = user?.UserName;
            ViewBag.IsLoggedIn      = user?.IsLoggedIn ?? false;
            ViewBag.IsITDepartment  = user?.IsITDepartment ?? false;
        }

        private static List<PendingCartridgeGroupViewModel> GroupIntoSessions(List<CartridgeRequestDto> rows)
        {
            var result = new List<PendingCartridgeGroupViewModel>();
            var sessionMap = new Dictionary<Guid, PendingCartridgeGroupViewModel>();

            foreach (var row in rows)
            {
                PendingCartridgeGroupViewModel grp;

                if (row.SubmissionSessionId.HasValue)
                {
                    if (!sessionMap.TryGetValue(row.SubmissionSessionId.Value, out grp!))
                    {
                        grp = new PendingCartridgeGroupViewModel
                        {
                            SubmissionSessionId = row.SubmissionSessionId,
                            EmployeeName = row.EmployeeName,
                            CompanyName = row.CompanyName,
                            BranchName = row.BranchName,
                            DepartmentName = row.DepartmentName
                        };
                        sessionMap[row.SubmissionSessionId.Value] = grp;
                        result.Add(grp);
                    }
                }
                else
                {
                    grp = new PendingCartridgeGroupViewModel
                    {
                        EmployeeName = row.EmployeeName,
                        CompanyName = row.CompanyName,
                        BranchName = row.BranchName,
                        DepartmentName = row.DepartmentName
                    };
                    result.Add(grp);
                }

                grp.Rows.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Builds the fulfillment panel data for one request (and its multi-model siblings, if
        /// any), including a fresh live stock lookup — mirrors desktop's OnRequestSelectedAsync,
        /// which always re-queries stock on selection rather than reusing stale list data.
        /// Returns null (with an error message queued) if the request is no longer pending.
        /// </summary>
        private async Task<FulfillCartridgeExchangeFormViewModel?> BuildFormForRequestAsync(int reqId)
        {
            var rows = await _repo.GetPendingCartridgeRequestsAsync();
            var target = rows.FirstOrDefault(r => r.ReqId == reqId);
            if (target == null)
            {
                TempData["ErrorMessage"] = "Request not found, or it has already been attempted (check the Cartridge Fulfillment queues).";
                return null;
            }

            List<CartridgeRequestDto> siblings = target.SubmissionSessionId.HasValue
                ? rows.Where(r => r.SubmissionSessionId == target.SubmissionSessionId).ToList()
                : new List<CartridgeRequestDto> { target };

            var lines = new List<FulfillCartridgeExchangeLineViewModel>();
            foreach (var row in siblings)
            {
                int? modelId = row.CartridgeModelId ?? await _cartridgeFulfillmentRepo.GetCartridgeModelIdByModelNumberAsync(row.DisplayModel);
                int availBrandNew = await _repo.GetAvailableIssuableStockByConditionAsync(modelId, "Brand New");
                int availRefilled = await _repo.GetAvailableIssuableStockByConditionAsync(modelId, "Refilled");

                lines.Add(new FulfillCartridgeExchangeLineViewModel
                {
                    ReqId = row.ReqId,
                    RequestModelId = row.RequestModelId,
                    CartridgeModelId = modelId,
                    DisplayModel = row.DisplayModel,
                    ConditionType = row.ConditionType,
                    RequestedQty = row.Quantity,
                    GoodEmptyQty = row.GoodEmptyQty,
                    DamagedEmptyQty = row.DamagedEmptyQty,
                    AvailableBrandNewStock = availBrandNew,
                    AvailableRefilledStock = availRefilled
                });
            }

            return new FulfillCartridgeExchangeFormViewModel
            {
                SubmissionSessionId = target.SubmissionSessionId,
                EmployeeName = target.EmployeeName,
                CompanyName = target.CompanyName,
                BranchName = target.BranchName,
                DepartmentName = target.DepartmentName,
                ReceivedByName = target.ReceivedByName,
                AdditionalRemarks = target.AdditionalRemarks,
                DistributionMethod = target.DistributionMethod,
                Lines = lines
            };
        }

        /// <summary>
        /// Single two-pane workspace, mirroring desktop's Cartridge Exchange window: the pending
        /// list on the left is always shown; clicking a row fetches the panel via AJAX (see
        /// Panel action) instead of a full page navigation. This action still accepts ?id= and
        /// renders the selected panel server-side too, so the page is directly linkable/refreshable
        /// and works without JavaScript.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? id)
        {
            var rows = await _repo.GetPendingCartridgeRequestsAsync();
            var groups = GroupIntoSessions(rows);

            FulfillCartridgeExchangeFormViewModel? form = id.HasValue
                ? await BuildFormForRequestAsync(id.Value)
                : null;

            SetCommonViewBag(GetCurrentUser());
            return View(new CartridgeExchangeWorkspaceViewModel
            {
                Groups = groups,
                SelectedReqId = id,
                SelectedForm = form
            });
        }

        /// <summary>
        /// AJAX endpoint backing the left-list row click — returns just the fulfillment panel
        /// markup so selecting a request doesn't reload the whole page (the list, scroll
        /// position, and filters all stay put).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Panel(int id)
        {
            var form = await BuildFormForRequestAsync(id);
            SetCommonViewBag(GetCurrentUser());
            return PartialView("_FulfillPanel", new CartridgeExchangeWorkspaceViewModel
            {
                SelectedReqId = id,
                SelectedForm = form
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fulfill(FulfillCartridgeExchangeFormViewModel model)
        {
            var currentUser = GetCurrentUser();
            int userId = currentUser?.UserId ?? 0;

            // Fetch EmpId/CartridgeModelId BEFORE any commits — once a row is fulfilled it drops
            // out of GetPendingCartridgeRequestsAsync(), so this must happen up front.
            var pendingBefore = await _repo.GetPendingCartridgeRequestsAsync();
            var pendingByReqId = pendingBefore.ToDictionary(r => r.ReqId);
            int? empIdForNotification = model.Lines.Count > 0 && pendingByReqId.TryGetValue(model.Lines[0].ReqId, out var firstPending)
                ? firstPending.EmpId
                : null;

            var committedRows = new List<CartridgeRequestDto>();
            int totalIssued = 0, totalRequested = 0;

            foreach (var line in model.Lines)
            {
                totalRequested += line.RequestedQty;

                int issuedBn = Math.Max(0, line.IssuedBrandNewQty);
                int issuedRf = Math.Max(0, line.IssuedRefilledQty);
                int total = issuedBn + issuedRf;
                if (total > line.RequestedQty)
                {
                    // Defensive clamp against a tampered/stale form — cap proportionally simple: drop refilled first.
                    int over = total - line.RequestedQty;
                    issuedRf = Math.Max(0, issuedRf - over);
                    if (issuedBn + issuedRf > line.RequestedQty)
                        issuedBn = Math.Max(0, line.RequestedQty - issuedRf);
                }

                int? modelId = line.CartridgeModelId ?? await _cartridgeFulfillmentRepo.GetCartridgeModelIdByModelNumberAsync(line.DisplayModel);

                // Re-derive against live stock so a stale form can't over-issue.
                int availBn = await _repo.GetAvailableIssuableStockByConditionAsync(modelId, "Brand New");
                int availRf = await _repo.GetAvailableIssuableStockByConditionAsync(modelId, "Refilled");
                issuedBn = Math.Min(issuedBn, availBn);
                issuedRf = Math.Min(issuedRf, availRf);

                var bnIds = issuedBn > 0 ? await _repo.GetIssuableItemIdsByConditionAsync(modelId, "Brand New", issuedBn) : new List<int>();
                var rfIds = issuedRf > 0 ? await _repo.GetIssuableItemIdsByConditionAsync(modelId, "Refilled", issuedRf) : new List<int>();
                // Item selection can come back short of the requested qty if stock changed since the form loaded.
                issuedBn = bnIds.Count;
                issuedRf = rfIds.Count;

                string remarks = CartridgeExchangeRemarks.GenerateRemarks(issuedBn + issuedRf, line.RequestedQty, line.DisplayModel);

                await _repo.FulfillCartridgeExchangeByConditionAsync(
                    line.ReqId, line.RequestedQty, bnIds, rfIds, issuedBn, issuedRf,
                    userId, line.RequestModelId, remarks);

                _activityLog.LogActivity(userId, "Update", "CartridgeExchange", line.ReqId,
                    $"[Portal] Cartridge exchange fulfilled for Request #{line.ReqId}: {issuedBn} brand new, {issuedRf} refilled.");

                totalIssued += issuedBn + issuedRf;
                committedRows.Add(new CartridgeRequestDto
                {
                    ReqId = line.ReqId,
                    ModelNumber = line.DisplayModel,
                    Quantity = line.RequestedQty,
                    EmployeeName = model.EmployeeName ?? string.Empty,
                    CompanyName = model.CompanyName,
                    BranchName = model.BranchName,
                    DepartmentName = model.DepartmentName,
                    ReceivedByName = model.ReceivedByName,
                    AdditionalRemarks = model.AdditionalRemarks
                });
            }

            // Notify the portal requester of the overall outcome (non-critical, mirrors desktop).
            try
            {
                if (committedRows.Count > 0 && empIdForNotification.HasValue)
                {
                    string notifType, notifTitle, notifMsg;
                    if (totalIssued == 0)
                    {
                        notifType = NotificationType.RequestUnfulfilled;
                        notifTitle = "Request Unfulfilled";
                        notifMsg = "Your cartridge request could not be fulfilled due to insufficient stock.";
                    }
                    else if (totalIssued < totalRequested)
                    {
                        notifType = NotificationType.RequestPartiallyFulfilled;
                        notifTitle = "Request Partially Fulfilled";
                        notifMsg = $"Your cartridge request was partially fulfilled: {totalIssued} of {totalRequested} units issued across all models.";
                    }
                    else
                    {
                        notifType = NotificationType.RequestFulfilled;
                        notifTitle = "Request Fulfilled";
                        notifMsg = "Your cartridge request has been fulfilled in full.";
                    }

                    var (notifUserId, authId) = await _notificationRepo.GetUserAndAuthIdForFulfillmentAsync(empIdForNotification.Value, model.SubmissionSessionId);
                    if (notifUserId.HasValue)
                    {
                        await _notificationRepo.CreateAsync(new NotificationCreateDto
                        {
                            UserId = notifUserId.Value,
                            Title = notifTitle,
                            Message = notifMsg,
                            NotificationType = notifType,
                            ReferenceId = authId
                        });
                    }
                }
            }
            catch
            {
                // notification is non-critical
            }

            var transmittal = CartridgeTransmittalViewModel.FromSession(committedRows, currentUser?.UserName ?? "");

            SetCommonViewBag(currentUser);
            return View("Transmittal", transmittal);
        }

        /// <summary>
        /// Data-entry-error escape hatch — permanently deletes a single cartridge Request row,
        /// reversing issued stock/returned-empty bookkeeping. Mirrors desktop's OnForceDelete:
        /// operates on exactly the request the user had selected, not the whole multi-model
        /// session. The confirmation itself happens client-side (JS confirm()) before this posts.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDelete(int reqId)
        {
            var currentUser = GetCurrentUser();
            int userId = currentUser?.UserId ?? 0;

            var (success, message) = await _repo.ForceDeleteCartridgeRequestAsync(reqId, userId);

            if (success)
            {
                _activityLog.LogActivity(userId, "Delete", "CartridgeExchange", reqId, $"[Portal] Force deleted Request #{reqId}: {message}");
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var rows = await _repo.GetFulfilledCartridgeHistoryAsync();
            SetCommonViewBag(GetCurrentUser());
            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var detail = await _repo.GetFulfilledCartridgeDetailAsync(id);
            if (detail == null)
            {
                TempData["ErrorMessage"] = "Set not found.";
                return RedirectToAction("History");
            }

            SetCommonViewBag(GetCurrentUser());
            return View(detail);
        }

        /// <summary>
        /// Same data as <see cref="Detail"/> but as a bodies-only partial, for the History
        /// page's popup modal (fetched client-side instead of navigating to a separate page).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetailPartial(int id)
        {
            var detail = await _repo.GetFulfilledCartridgeDetailAsync(id);
            if (detail == null) return NotFound();
            return PartialView("_DetailBody", detail);
        }

        /// <summary>
        /// Reprints the transmittal for an already-fulfilled Set — mirrors desktop's
        /// FulfilledCartridgesView "Reprint" button (BtnReprint_Click), which calls
        /// TransmittalPrintService.ShowPrintDialog(CartridgeTransmittalViewModel.FromHistory(row))
        /// on the grid-selected row. Reuses the same history list the History page renders from,
        /// same as desktop reprinting off the row the user already has selected in PagedRows.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Reprint(int setId)
        {
            var rows = await _repo.GetFulfilledCartridgeHistoryAsync();
            var row = rows.FirstOrDefault(r => r.SetId == setId);
            if (row == null)
            {
                TempData["ErrorMessage"] = "Set not found, or it falls outside the 365-day history window.";
                return RedirectToAction("History");
            }

            var currentUser = GetCurrentUser();
            var transmittal = CartridgeTransmittalViewModel.FromHistory(row, currentUser?.UserName ?? "");

            SetCommonViewBag(currentUser);
            return View("Transmittal", transmittal);
        }
    }
}
