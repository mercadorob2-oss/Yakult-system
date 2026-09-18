using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Filters;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Cartridge Master Data — mirrors the desktop Cartridge Management portal's
    /// "Cartridge Master Data" side-menu group: Cartridge Models (full CRUD) and
    /// View Cartridges (read-only). PORTED FROM:
    /// Yakult.Inventory.App/Wpf/CartridgeManagement/Views/ViewCartridgeModelsView.xaml.cs
    /// and ViewCartridgesView.xaml.cs.
    /// </summary>
    [RequireITDepartment]
    public class CartridgeMasterDataController : Controller
    {
        private readonly ICartridgeMasterDataRepository _repository;

        public CartridgeMasterDataController(ICartridgeMasterDataRepository repository)
        {
            _repository = repository;
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

        // ─── Cartridge Models ───────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Models(string? search)
        {
            var models = await _repository.GetAllActiveModelsAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim().ToLowerInvariant();
                models = models.Where(m => (m.ModelNumber ?? "").ToLowerInvariant().Contains(q)).ToList();
            }

            ViewBag.SearchText = search ?? string.Empty;
            SetCommonViewBag(GetCurrentUser());
            return View(models);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModel(string modelNumber, bool isRequestable, bool isRefillable)
        {
            if (string.IsNullOrWhiteSpace(modelNumber))
            {
                TempData["ErrorMessage"] = "Please enter a model number.";
                return RedirectToAction("Models");
            }

            var currentUser = GetCurrentUser();

            var existing = await _repository.FindModelByModelNumberAsync(modelNumber);
            if (existing != null)
            {
                TempData["ErrorMessage"] = $"Model number '{modelNumber.Trim()}' already exists.";
                return RedirectToAction("Models");
            }

            await _repository.CreateModelAsync(new CartridgeModelDto
            {
                ModelNumber   = modelNumber.Trim(),
                IsRequestable = isRequestable,
                IsRefillable  = isRefillable,
                IsActive      = true,
                CreatedBy     = currentUser?.UserId ?? 0
            });

            TempData["SuccessMessage"] = "Cartridge model added successfully!";
            return RedirectToAction("Models");
        }

        [HttpGet]
        public async Task<IActionResult> EditModel(int id)
        {
            var model = await _repository.GetModelByIdAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Cartridge model not found.";
                return RedirectToAction("Models");
            }

            SetCommonViewBag(GetCurrentUser());
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModel(CartridgeModelDto model)
        {
            if (string.IsNullOrWhiteSpace(model.ModelNumber))
            {
                TempData["ErrorMessage"] = "Please enter a model number.";
                return RedirectToAction("EditModel", new { id = model.CartridgeModelId });
            }

            await _repository.UpdateModelAsync(model);
            TempData["SuccessMessage"] = "Cartridge model updated successfully!";
            return RedirectToAction("Models");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveModel(int id, string? reason, bool deactivate)
        {
            var currentUser = GetCurrentUser();
            string archivedBy = currentUser?.UserName ?? "System";
            string archiveReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided" : reason.Trim();

            await _repository.ArchiveModelAsync(id, archiveReason, deactivate, archivedBy);

            TempData["SuccessMessage"] = "Cartridge model archived successfully! You can view archived records in the Archive page.";
            return RedirectToAction("Models");
        }

        [HttpGet]
        public async Task<IActionResult> DeleteModel(int id)
        {
            var model = await _repository.GetModelByIdAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Cartridge model not found.";
                return RedirectToAction("Models");
            }

            var (hasItems, itemCount, emptyCartridgeCount, batchLineCount) = await _repository.CheckModelDependenciesAsync(id);

            if (emptyCartridgeCount > 0 || batchLineCount > 0)
            {
                TempData["ErrorMessage"] =
                    $"\"{model.ModelNumber}\" cannot be permanently deleted: {emptyCartridgeCount} EmptyCartridge " +
                    $"record(s) and {batchLineCount} vendor batch line record(s) still reference it. These represent " +
                    "physical inventory / audit history and cannot be auto-unlinked. Resolve those records first " +
                    "(dispose, sell, or return them), or mark the model as Inactive instead.";
                return RedirectToAction("Models");
            }

            ViewBag.HasItems  = hasItems;
            ViewBag.ItemCount = itemCount;
            SetCommonViewBag(GetCurrentUser());
            return View("ConfirmDeleteModel", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteModelConfirmed(int id, string mode)
        {
            var model = await _repository.GetModelByIdAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Cartridge model not found.";
                return RedirectToAction("Models");
            }

            if (mode == "inactive")
            {
                await _repository.SetModelActiveAsync(id, false);
                TempData["SuccessMessage"] = $"\"{model.ModelNumber}\" marked as inactive.";
                return RedirectToAction("Models");
            }

            try
            {
                await _repository.DeleteModelAsync(id);
                TempData["SuccessMessage"] = $"\"{model.ModelNumber}\" deleted.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction("Models");
        }

        // ─── View Cartridges (read-only) ───────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Items(string? search, bool includeInactive = false)
        {
            var items = await _repository.GetCartridgeItemsAsync(includeInactive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim().ToLowerInvariant();
                items = items.Where(i =>
                    (i.Name          ?? "").ToLowerInvariant().Contains(q) ||
                    (i.ModelNumber   ?? "").ToLowerInvariant().Contains(q) ||
                    (i.SerialNumber  ?? "").ToLowerInvariant().Contains(q) ||
                    (i.VendorName    ?? "").ToLowerInvariant().Contains(q) ||
                    (i.ConditionName ?? "").ToLowerInvariant().Contains(q) ||
                    (i.LatestStatus  ?? "").ToLowerInvariant().Contains(q)
                ).ToList();
            }

            ViewBag.SearchText       = search ?? string.Empty;
            ViewBag.IncludeInactive  = includeInactive;
            ViewBag.TotalRecords     = items.Count;
            ViewBag.TotalActive      = items.Count(i => i.Active);
            SetCommonViewBag(GetCurrentUser());
            return View(items);
        }
    }
}
