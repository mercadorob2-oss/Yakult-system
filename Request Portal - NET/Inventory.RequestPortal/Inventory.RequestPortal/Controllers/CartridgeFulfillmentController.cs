using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Fulfillment loop for the cartridge-exchange backlog (dbo.UnfulfilledCartridgeExchange).
    /// Only serves requests that already had a first-pass fulfillment attempt on the desktop
    /// Cartridge Management portal. Grouping/sibling logic mirrors desktop's
    /// UnfulfilledExchangesViewModel.OnFulfillSelected.
    /// </summary>
    [RequireITDepartment]
    public class CartridgeFulfillmentController : Controller
    {
        private readonly ICartridgeFulfillmentRepository _repository;
        private readonly IActivityLogRepository _activityLog;
        private readonly INotificationRepository _notifications;

        public CartridgeFulfillmentController(
            ICartridgeFulfillmentRepository repository,
            IActivityLogRepository activityLog,
            INotificationRepository notifications)
        {
            _repository    = repository;
            _activityLog   = activityLog;
            _notifications = notifications;
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

        private static List<CartridgeExchangeGroupViewModel> GroupIntoSessions(List<UnfulfilledCartridgeExchangeDto> rows)
        {
            var result     = new List<CartridgeExchangeGroupViewModel>();
            var setMap     = new Dictionary<int, CartridgeExchangeGroupViewModel>();
            var sessionMap = new Dictionary<Guid, CartridgeExchangeGroupViewModel>();

            foreach (var row in rows)
            {
                CartridgeExchangeGroupViewModel grp;

                if (row.SetId.HasValue && row.SetId.Value > 0)
                {
                    if (!setMap.TryGetValue(row.SetId.Value, out grp!))
                    {
                        grp = new CartridgeExchangeGroupViewModel
                        {
                            SetId = row.SetId,
                            SubmissionSessionId = row.SubmissionSessionId,
                            RequesterName = row.RequesterName,
                            BranchName = row.BranchName,
                            DepartmentName = row.DepartmentName
                        };
                        setMap[row.SetId.Value] = grp;
                        result.Add(grp);
                    }
                }
                else if (row.SubmissionSessionId.HasValue)
                {
                    if (!sessionMap.TryGetValue(row.SubmissionSessionId.Value, out grp!))
                    {
                        grp = new CartridgeExchangeGroupViewModel
                        {
                            SubmissionSessionId = row.SubmissionSessionId,
                            RequesterName = row.RequesterName,
                            BranchName = row.BranchName,
                            DepartmentName = row.DepartmentName
                        };
                        sessionMap[row.SubmissionSessionId.Value] = grp;
                        result.Add(grp);
                    }
                }
                else
                {
                    grp = new CartridgeExchangeGroupViewModel
                    {
                        RequesterName = row.RequesterName,
                        BranchName = row.BranchName,
                        DepartmentName = row.DepartmentName
                    };
                    result.Add(grp);
                }

                grp.Rows.Add(row);
            }

            return result;
        }

        // Stat cards are always scoped to Status='Pending' regardless of the active status
        // filter — mirrors desktop's UnfulfilledExchangesViewModel.LoadStats(), which queries
        // GetPendingSummary()/GetPendingSummaryByModel() independently of _statusFilter.
        private async Task SetStatsViewBagAsync()
        {
            var (pendingCount, totalQty) = await _repository.GetPendingSummaryAsync();
            ViewBag.PendingRecords = pendingCount;
            ViewBag.UnfulfilledCartridges = totalQty;

            var byModel = await _repository.GetPendingSummaryByModelAsync();
            var models = byModel
                .OrderByDescending(x => x.UnfulfilledQty)
                .Select(x => x.Model)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            ViewBag.ByModelText = models.Count == 0 ? "—" : string.Join(", ", models);
        }

        [HttpGet]
        public async Task<IActionResult> Unfulfilled(string status = "Pending")
        {
            List<UnfulfilledCartridgeExchangeDto> rows = status switch
            {
                "Fulfilled" => await _repository.GetFulfilledExchangesAsync(),
                "All"       => await _repository.GetAllExchangesAsync(),
                _           => await _repository.GetPurelyUnfulfilledExchangesAsync()
            };

            await SetStatsViewBagAsync();
            SetCommonViewBag(GetCurrentUser());
            ViewBag.PageTitle = "Unfulfilled Cartridge Exchanges";
            ViewBag.ShowStatusFilter = true;
            ViewBag.StatusFilter = status is "Fulfilled" or "All" ? status : "Pending";
            return View("CartridgeGroupList", GroupIntoSessions(rows));
        }

        [HttpGet]
        public async Task<IActionResult> PartiallyFulfilled()
        {
            var rows = await _repository.GetPartiallyFulfilledExchangesFullSetAsync();
            await SetStatsViewBagAsync();
            SetCommonViewBag(GetCurrentUser());
            ViewBag.PageTitle = "Partially Fulfilled Cartridge Exchanges";
            ViewBag.ShowStatusFilter = false;
            ViewBag.StatusFilter = "Pending";
            return View("CartridgeGroupList", GroupIntoSessions(rows));
        }

        [HttpGet]
        public async Task<IActionResult> Fulfill(int id)
        {
            var unfulfilled = await _repository.GetPurelyUnfulfilledExchangesAsync();
            var partial     = await _repository.GetPartiallyFulfilledExchangesFullSetAsync();
            var all         = unfulfilled.Concat(partial).ToList();

            var target = all.FirstOrDefault(r => r.UnfulfilledId == id);
            if (target == null)
            {
                TempData["ErrorMessage"] = "Exchange not found, or it has already been fully fulfilled.";
                return RedirectToAction("Unfulfilled");
            }

            List<UnfulfilledCartridgeExchangeDto> siblings = target.SetId.HasValue && target.SetId.Value > 0
                ? all.Where(r => r.SetId == target.SetId && r.Status == "Pending" && r.UnfulfilledQty > 0).ToList()
                : new List<UnfulfilledCartridgeExchangeDto> { target };

            var lines = new List<FulfillCartridgeLineViewModel>();
            foreach (var row in siblings)
            {
                int? modelId = await _repository.GetCartridgeModelIdByModelNumberAsync(row.CartridgeModel ?? string.Empty);
                int available = await _repository.GetAvailableIssuableStockAsync(modelId);
                int availBrandNew = await _repository.GetAvailableIssuableStockByConditionAsync(modelId, "Brand New");
                int availRefilled = await _repository.GetAvailableIssuableStockByConditionAsync(modelId, "Refilled");
                lines.Add(new FulfillCartridgeLineViewModel
                {
                    UnfulfilledId           = row.UnfulfilledId,
                    CartridgeModel          = row.CartridgeModel,
                    ReturnedEmptyQty        = row.ReturnedEmptyQty,
                    IssuedFullQty           = row.IssuedFullQty,
                    UnfulfilledQty          = row.UnfulfilledQty,
                    AvailableIssuableStock  = available,
                    AvailBrandNew           = availBrandNew,
                    AvailRefilled           = availRefilled
                });
            }

            var form = new FulfillCartridgeFormViewModel
            {
                SetId          = target.SetId,
                RequesterName  = target.RequesterName,
                BranchName     = target.BranchName,
                DepartmentName = target.DepartmentName,
                Lines          = lines
            };

            SetCommonViewBag(GetCurrentUser());
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fulfill(FulfillCartridgeFormViewModel model)
        {
            var currentUser = GetCurrentUser();
            int userId = currentUser?.UserId ?? 0;

            var unfulfilled = await _repository.GetPurelyUnfulfilledExchangesAsync();
            var partial     = await _repository.GetPartiallyFulfilledExchangesFullSetAsync();
            var freshById   = unfulfilled.Concat(partial).ToDictionary(r => r.UnfulfilledId);

            int fulfilledCount = 0;
            // Mirrors desktop's CommitFulfillment: tracks every sibling line in this batch
            // (not just the ones with a quantity entered) so the notification's totalIssued/
            // totalPending sums cover the whole Set, and anchors on the first line actually issued.
            var appliedLines = new List<(UnfulfilledCartridgeExchangeDto Fresh, int IssueQty)>();

            foreach (var line in model.Lines)
            {
                if (!freshById.TryGetValue(line.UnfulfilledId, out var fresh)) continue;

                int issueQty = 0;
                int requestedQty = line.BrandNewQty + line.RefilledQty;
                if (requestedQty > 0)
                {
                    int? modelId    = await _repository.GetCartridgeModelIdByModelNumberAsync(fresh.CartridgeModel ?? string.Empty);
                    int available   = await _repository.GetAvailableIssuableStockAsync(modelId);
                    int maxIssuable = Math.Min(available, fresh.UnfulfilledQty);
                    issueQty        = Math.Min(requestedQty, maxIssuable);

                    if (issueQty > 0)
                    {
                        string remarks = string.IsNullOrWhiteSpace(line.Remarks)
                            ? CartridgeExchangeRemarks.GenerateRemarks(fresh.IssuedFullQty + issueQty, fresh.ReturnedEmptyQty, fresh.CartridgeModel)
                            : line.Remarks.Trim();

                        await _repository.FulfillExchangeAsync(fresh.UnfulfilledId, issueQty, userId, remarks);
                        _activityLog.LogActivity(userId, "Update", "CartridgeExchange", fresh.UnfulfilledId,
                            $"[Portal] Issued {issueQty} more cartridge(s) for Exchange #{fresh.UnfulfilledId} (Req #{fresh.ReqId})");
                        fulfilledCount++;
                    }
                }

                appliedLines.Add((fresh, issueQty));
            }

            await SendFulfillmentNotificationAsync(appliedLines);

            TempData["SuccessMessage"] = fulfilledCount > 0
                ? $"Fulfillment recorded for {fulfilledCount} line(s)."
                : "No quantities were issued.";

            return RedirectToAction("Unfulfilled");
        }

        /// <summary>
        /// PORTED FROM: Yakult.Inventory.App/Wpf/CartridgeManagement/ViewModels/UnfulfilledExchangesViewModel.cs
        /// (CommitFulfillment). Best-effort — mirrors desktop's try/catch-and-swallow since a
        /// failed notification must never roll back the fulfillment itself.
        /// </summary>
        private async Task SendFulfillmentNotificationAsync(List<(UnfulfilledCartridgeExchangeDto Fresh, int IssueQty)> appliedLines)
        {
            var anchor = appliedLines.FirstOrDefault(l => l.IssueQty > 0);
            if (anchor.Fresh == null) return;

            try
            {
                int totalIssued  = appliedLines.Sum(l => l.IssueQty);
                int totalPending = appliedLines.Sum(l => l.Fresh.UnfulfilledQty);

                string notifType, notifTitle, notifMsg;
                if (totalIssued <= 0)
                {
                    notifType  = NotificationType.RequestUnfulfilled;
                    notifTitle = "Request Unfulfilled";
                    notifMsg   = $"Your cartridge request (Req #{anchor.Fresh.ReqId}) could not be fulfilled due to insufficient stock.";
                }
                else if (totalIssued < totalPending)
                {
                    notifType  = NotificationType.RequestPartiallyFulfilled;
                    notifTitle = "Request Partially Fulfilled";
                    notifMsg   = $"Your cartridge request (Req #{anchor.Fresh.ReqId}) was partially fulfilled: {totalIssued} of {totalPending} issued.";
                }
                else
                {
                    notifType  = NotificationType.RequestFulfilled;
                    notifTitle = "Request Fulfilled";
                    notifMsg   = $"Your cartridge request (Req #{anchor.Fresh.ReqId}) has been fulfilled in full.";
                }

                var (notifUserId, notifAuthId) = await _notifications.GetUserAndAuthIdForFulfillmentAsync(
                    anchor.Fresh.EmpId, anchor.Fresh.SubmissionSessionId);

                if (notifUserId.HasValue)
                {
                    await _notifications.CreateAsync(new NotificationCreateDto
                    {
                        UserId           = notifUserId.Value,
                        Title            = notifTitle,
                        Message          = notifMsg,
                        NotificationType = notifType,
                        ReferenceId      = notifAuthId
                    });
                }
            }
            catch
            {
                // notification is non-critical
            }
        }
    }
}
