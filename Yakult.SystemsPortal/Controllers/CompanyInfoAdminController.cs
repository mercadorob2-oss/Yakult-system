using System.Security.Claims;
using System.Security.Cryptography;
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
    private readonly IWebHostEnvironment _environment;

    public CompanyInfoAdminController(ICompanyInfoRepository companyInfo, IWebHostEnvironment environment)
    {
        _companyInfo = companyInfo;
        _environment = environment;
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

    [HttpGet("Policy/Assets")]
    public IActionResult PolicyAssets()
    {
        var folder = PolicyAssetFolder();
        if (!Directory.Exists(folder)) return Json(Array.Empty<object>());
        var assets = Directory.EnumerateFiles(folder)
            .Where(IsSupportedPolicyImage)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => new { name = Path.GetFileName(path), url = "/media/company-policies/" + Path.GetFileName(path) })
            .ToList();
        return Json(assets);
    }

    [HttpPost("Policy/Assets"), ValidateAntiForgeryToken, RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadPolicyAsset(IFormFile? file)
    {
        if (file is null || file.Length is <= 0 or > 10_485_760)
            return BadRequest(new { success = false, message = "Choose an image smaller than 10 MB." });
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Use a PNG, JPG, or WebP diagram." });

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, HttpContext.RequestAborted);
        var bytes = buffer.ToArray();
        if (bytes.Length != file.Length || !HasSupportedImageSignature(bytes, extension))
            return BadRequest(new { success = false, message = "The selected file is not a valid PNG, JPG, or WebP image." });

        var name = $"policy-diagram-{DateTime.UtcNow:yyyyMMddHHmmss}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}{extension}";
        var folder = PolicyAssetFolder();
        Directory.CreateDirectory(folder);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, name), bytes, HttpContext.RequestAborted);
        return Json(new { success = true, name, url = "/media/company-policies/" + name });
    }

    [HttpPost("Policy"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PolicySave(SaveCompanyPolicyRequest request)
    {
        if (request.Body?.StartsWith(CompanyPolicyDocument.Prefix, StringComparison.Ordinal) == true)
        {
            var document = CompanyPolicyDocument.Parse(request.Body);
            if (document is null) ModelState.AddModelError(nameof(request.Body), "The structured policy data could not be read. Review the sections and try again.");
            else
            {
                try { document.Validate(); }
                catch (ArgumentException ex) { ModelState.AddModelError(nameof(request.Body), ex.Message); }
            }
        }
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

    private string PolicyAssetFolder()
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "CompanyPolicyMedia");
    }

    private static bool IsSupportedPolicyImage(string path) => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static bool HasSupportedImageSignature(byte[] bytes, string extension)
    {
        if (extension == ".png") return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (extension is ".jpg" or ".jpeg") return bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
        return extension == ".webp" && bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
    }
}
