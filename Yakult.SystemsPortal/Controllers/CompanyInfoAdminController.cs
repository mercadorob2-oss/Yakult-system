using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;

namespace Yakult.SystemsPortal.Controllers;

[Authorize(Policy = "ContentEditor")]
[Route("/Admin/CompanyInfo")]
public sealed class CompanyInfoAdminController : Controller
{
    private readonly ICompanyInfoRepository _companyInfo;

    public CompanyInfoAdminController(ICompanyInfoRepository companyInfo)
    {
        _companyInfo = companyInfo;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        IReadOnlyList<CompanyFaqItem> faqs = Array.Empty<CompanyFaqItem>();
        IReadOnlyList<CompanyPolicyItem> policies = Array.Empty<CompanyPolicyItem>();
        try { faqs = await _companyInfo.GetManagedFaqsAsync(); } catch { }
        try { policies = await _companyInfo.GetManagedPoliciesAsync(); } catch { }
        return View(new CompanyInfoAdminViewModel
        {
            Faqs = faqs,
            Policies = policies,
            CanPublish = User.HasClaim("IsDeveloper", "true")
        });
    }

    // ── FAQ ──────────────────────────────────────────────────────────────
    [HttpGet("Faq/{id:int?}")]
    public async Task<IActionResult> FaqEdit(int? id)
    {
        SaveCompanyFaqRequest model = new();
        if (id is > 0)
        {
            CompanyFaqItem? item;
            try { item = await _companyInfo.GetFaqByIdAsync(id.Value); }
            catch { item = null; }
            if (item is null) return NotFound();
            model = new SaveCompanyFaqRequest { FaqId = item.FaqId, Question = item.Question, Answer = item.Answer, Category = item.Category, SortOrder = item.SortOrder };
        }
        return View(model);
    }

    [HttpPost("Faq"), ValidateAntiForgeryToken]
    public async Task<IActionResult> FaqSave(SaveCompanyFaqRequest request)
    {
        if (!ModelState.IsValid) return View("FaqEdit", request);
        try
        {
            await _companyInfo.SaveFaqAsync(request, UserId());
            TempData["CompanyInfoMessage"] = "FAQ saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("FaqEdit", request);
        }
    }

    [HttpPost("Faq/Publish/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> FaqPublish(int id)
    {
        if (!User.HasClaim("IsDeveloper", "true")) return Forbid();
        try { await _companyInfo.SetFaqPublishedAsync(id, true, UserId()); TempData["CompanyInfoMessage"] = "FAQ published."; }
        catch (Exception ex) { TempData["CompanyInfoError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Faq/Unpublish/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> FaqUnpublish(int id)
    {
        if (!User.HasClaim("IsDeveloper", "true")) return Forbid();
        try { await _companyInfo.SetFaqPublishedAsync(id, false, UserId()); TempData["CompanyInfoMessage"] = "FAQ unpublished."; }
        catch (Exception ex) { TempData["CompanyInfoError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Faq/Delete/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> FaqDelete(int id)
    {
        if (!User.HasClaim("IsDeveloper", "true")) return Forbid();
        try { await _companyInfo.DeleteFaqAsync(id); TempData["CompanyInfoMessage"] = "FAQ deleted."; }
        catch (Exception ex) { TempData["CompanyInfoError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    // ── Policy ───────────────────────────────────────────────────────────
    [HttpGet("Policy/{id:int?}")]
    public async Task<IActionResult> PolicyEdit(int? id)
    {
        SaveCompanyPolicyRequest model = new();
        if (id is > 0)
        {
            CompanyPolicyItem? item;
            try { item = await _companyInfo.GetPolicyByIdAsync(id.Value); }
            catch { item = null; }
            if (item is null) return NotFound();
            model = new SaveCompanyPolicyRequest { PolicyId = item.PolicyId, Title = item.Title, Summary = item.Summary, Body = item.Body, Category = item.Category, EffectiveDate = item.EffectiveDate, SortOrder = item.SortOrder };
        }
        return View(model);
    }

    [HttpPost("Policy"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PolicySave(SaveCompanyPolicyRequest request)
    {
        if (!ModelState.IsValid) return View("PolicyEdit", request);
        try
        {
            await _companyInfo.SavePolicyAsync(request, UserId());
            TempData["CompanyInfoMessage"] = "Policy saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("PolicyEdit", request);
        }
    }

    [HttpPost("Policy/Publish/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PolicyPublish(int id)
    {
        if (!User.HasClaim("IsDeveloper", "true")) return Forbid();
        try { await _companyInfo.SetPolicyPublishedAsync(id, true, UserId()); TempData["CompanyInfoMessage"] = "Policy published."; }
        catch (Exception ex) { TempData["CompanyInfoError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Policy/Unpublish/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PolicyUnpublish(int id)
    {
        if (!User.HasClaim("IsDeveloper", "true")) return Forbid();
        try { await _companyInfo.SetPolicyPublishedAsync(id, false, UserId()); TempData["CompanyInfoMessage"] = "Policy unpublished."; }
        catch (Exception ex) { TempData["CompanyInfoError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Policy/Delete/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PolicyDelete(int id)
    {
        if (!User.HasClaim("IsDeveloper", "true")) return Forbid();
        try { await _companyInfo.DeletePolicyAsync(id); TempData["CompanyInfoMessage"] = "Policy deleted."; }
        catch (Exception ex) { TempData["CompanyInfoError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new InvalidOperationException("Authenticated user ID is unavailable.");
}
