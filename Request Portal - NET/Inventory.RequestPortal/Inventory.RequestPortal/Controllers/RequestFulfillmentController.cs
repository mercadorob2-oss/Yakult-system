using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Fulfillment loop for Ink / Toner / Print Head requests. Grouping/sibling logic mirrors
    /// desktop's UnfulfilledRequestsViewModel.GroupIntoSessions / OnFulfillSelected.
    /// </summary>
    [RequireITDepartment]
    public class RequestFulfillmentController : Controller
    {
        private readonly IRequestFulfillmentRepository _repository;
        private readonly IActivityLogRepository _activityLog;

        public RequestFulfillmentController(
            IRequestFulfillmentRepository repository,
            IActivityLogRepository activityLog)
        {
            _repository  = repository;
            _activityLog = activityLog;
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

        private static List<RequestSessionGroupViewModel> GroupIntoSessions(List<RequestDto> rows)
        {
            var result     = new List<RequestSessionGroupViewModel>();
            var setMap     = new Dictionary<int, RequestSessionGroupViewModel>();
            var sessionMap = new Dictionary<Guid, RequestSessionGroupViewModel>();

            foreach (var row in rows)
            {
                RequestSessionGroupViewModel grp;

                if (row.SetId.HasValue && row.SetId.Value > 0)
                {
                    if (!setMap.TryGetValue(row.SetId.Value, out grp!))
                    {
                        grp = new RequestSessionGroupViewModel
                        {
                            SetId = row.SetId,
                            SubmissionSessionId = row.SubmissionSessionId,
                            EmployeeName = row.EmployeeName,
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
                        grp = new RequestSessionGroupViewModel
                        {
                            SubmissionSessionId = row.SubmissionSessionId,
                            EmployeeName = row.EmployeeName,
                            BranchName = row.BranchName,
                            DepartmentName = row.DepartmentName
                        };
                        sessionMap[row.SubmissionSessionId.Value] = grp;
                        result.Add(grp);
                    }
                }
                else
                {
                    grp = new RequestSessionGroupViewModel
                    {
                        EmployeeName = row.EmployeeName,
                        BranchName = row.BranchName,
                        DepartmentName = row.DepartmentName
                    };
                    result.Add(grp);
                }

                grp.Rows.Add(row);
            }

            return result;
        }

        [HttpGet]
        public async Task<IActionResult> Unfulfilled()
        {
            var rows = await _repository.GetUnfulfilledRequestsFullSetAsync();
            var groups = GroupIntoSessions(rows);

            // Mirrors desktop's UnfulfilledRequestsViewModel.UpdateStats() — computed from the
            // loaded dataset itself (no separate always-pending query, unlike cartridge exchange).
            ViewBag.PendingRecords = rows.Count;
            ViewBag.UnfulfilledQty = rows.Sum(r => Math.Max(0, r.Quantity - r.IssuedQty));
            var items = rows.Select(r => r.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToList();
            ViewBag.ByItemText = items.Count == 0 ? "—" : string.Join(", ", items);

            SetCommonViewBag(GetCurrentUser());
            ViewBag.PageTitle = "Unfulfilled Requests";
            ViewBag.StatsMode = "Unfulfilled";
            return View("RequestGroupList", groups);
        }

        [HttpGet]
        public async Task<IActionResult> PartiallyFulfilled()
        {
            var rows = await _repository.GetPartiallyFulfilledRequestsFullSetAsync();
            var groups = GroupIntoSessions(rows);

            // Mirrors desktop's PartiallyFulfilledRequestsViewModel.UpdateStats().
            ViewBag.SessionCount = groups.Count;
            ViewBag.TotalIssued  = groups.Sum(g => g.TotalIssued);
            ViewBag.TotalPending = groups.Sum(g => g.TotalPending);

            SetCommonViewBag(GetCurrentUser());
            ViewBag.PageTitle = "Partially Fulfilled Requests";
            ViewBag.StatsMode = "PartiallyFulfilled";
            return View("RequestGroupList", groups);
        }

        [HttpGet]
        public async Task<IActionResult> Fulfill(int id)
        {
            var unfulfilled = await _repository.GetUnfulfilledRequestsFullSetAsync();
            var partial     = await _repository.GetPartiallyFulfilledRequestsFullSetAsync();
            var all         = unfulfilled.Concat(partial).ToList();

            var target = all.FirstOrDefault(r => r.ReqId == id);
            if (target == null)
            {
                TempData["ErrorMessage"] = "Request not found, or it has already been fully fulfilled.";
                return RedirectToAction("Unfulfilled");
            }

            List<RequestDto> siblings = target.SetId.HasValue && target.SetId.Value > 0
                ? all.Where(r => r.SetId == target.SetId && r.IssuedQty < r.Quantity).ToList()
                : new List<RequestDto> { target };

            var lines = new List<FulfillRequestLineViewModel>();
            foreach (var row in siblings)
            {
                int available = await _repository.GetItemStockOnHandAsync(row.ItemId);
                lines.Add(new FulfillRequestLineViewModel
                {
                    ReqId          = row.ReqId,
                    ItemId         = row.ItemId,
                    ItemName       = row.ItemName,
                    Quantity       = row.Quantity,
                    IssuedQty      = row.IssuedQty,
                    AvailableStock = available
                });
            }

            var form = new FulfillRequestFormViewModel
            {
                SetId          = target.SetId,
                EmployeeName   = target.EmployeeName,
                BranchName     = target.BranchName,
                DepartmentName = target.DepartmentName,
                Lines          = lines
            };

            SetCommonViewBag(GetCurrentUser());
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fulfill(FulfillRequestFormViewModel model)
        {
            var currentUser = GetCurrentUser();
            int userId = currentUser?.UserId ?? 0;

            // Recompute against current DB state so a stale/tampered form can't over-issue.
            var unfulfilled = await _repository.GetUnfulfilledRequestsFullSetAsync();
            var partial     = await _repository.GetPartiallyFulfilledRequestsFullSetAsync();
            var freshById   = unfulfilled.Concat(partial).ToDictionary(r => r.ReqId);

            int fulfilledCount = 0;
            foreach (var line in model.Lines)
            {
                if (line.IssueQty <= 0) continue;
                if (!freshById.TryGetValue(line.ReqId, out var fresh)) continue;

                int pendingQty  = Math.Max(0, fresh.Quantity - fresh.IssuedQty);
                int available   = await _repository.GetItemStockOnHandAsync(fresh.ItemId);
                int maxIssuable = Math.Min(available, pendingQty);
                int issueQty    = Math.Min(line.IssueQty, maxIssuable);
                if (issueQty <= 0) continue;

                string? remarks = string.IsNullOrWhiteSpace(line.Remarks) ? null : line.Remarks.Trim();
                await _repository.FulfillRequestAsync(fresh.ReqId, issueQty, userId, remarks);
                _activityLog.LogActivity(userId, "Update", "Request", fresh.ReqId,
                    $"[Portal] Issued {issueQty} more unit(s) for Request #{fresh.ReqId}");
                fulfilledCount++;
            }

            TempData["SuccessMessage"] = fulfilledCount > 0
                ? $"Fulfillment recorded for {fulfilledCount} line(s)."
                : "No quantities were issued.";

            return RedirectToAction("Unfulfilled");
        }
    }
}
