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
    private readonly ILogger<CompanyInfoAdminController> _logger;

    public CompanyInfoAdminController(
        ICompanyInfoRepository companyInfo,
        IWebHostEnvironment environment,
        ILogger<CompanyInfoAdminController> logger)
    {
        _companyInfo = companyInfo;
        _environment = environment;
        _logger = logger;
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
    [HttpGet("Policy/Prepared/{id:int?}")]
    public async Task<IActionResult> PreparedPolicy(int? id)
    {
        Response.Headers.CacheControl = "no-store";
        var existing = id is > 0
            ? await _companyInfo.GetPolicyByIdAsync(id.Value)
            : (await _companyInfo.GetManagedPoliciesAsync()).FirstOrDefault(policy => policy.Slug == ItPolicyContent.Slug);
        if (id is > 0 && (existing is null || existing.Slug != ItPolicyContent.Slug)) return NotFound();
        if (existing is not null) return RedirectToAction(nameof(PolicyEdit), new { id = existing.PolicyId });
        if (!ItPolicyContent.TryLoad(_environment.ContentRootPath, out var prepared))
        {
            TempData["CompanyInfoError"] = "The IT policy template is unavailable. Create a policy using the normal editor.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.PreparedPolicy = true;
        return View("PolicyEdit", new SaveCompanyPolicyRequest {
            Title = prepared!.Title, Summary = prepared.Summary, Body = prepared.Body, Category = prepared.Category
        });
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
            ViewBag.IsPublished = item.IsPublished;
        }
        return View(model);
    }

    [HttpPost("Policy/Preview"), ValidateAntiForgeryToken, RequestSizeLimit(2_097_152)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult PolicyPreview(SaveCompanyPolicyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Complete the policy details to update the preview." });
        var document = CompanyPolicyDocument.Parse(request.Body);
        if (document is null && request.Body.StartsWith(CompanyPolicyDocument.Prefix, StringComparison.Ordinal))
            return BadRequest(new { message = "The structured policy data could not be read." });
        // Legacy Markdown also uses the shared reader without changing its saved format.
        document ??= new CompanyPolicyDocument { Sections = [new CompanyPolicySection {
            Id = "policy-content", Title = request.Title,
            Blocks = [new CompanyPolicyBlock { Type = "text", Text = request.Body }]
        }] };
        try { document.ToBody(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        return PartialView("_CompanyPolicyReader", new CompanyPolicyStructuredViewModel {
            Policy = new CompanyPolicyItem { Title = request.Title, Summary = request.Summary,
                Category = request.Category, EffectiveDate = request.EffectiveDate, Body = request.Body },
            Document = document
        });
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
        try
        {
            Directory.CreateDirectory(folder);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, name), bytes, HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save company policy image to {PolicyAssetFolder}.", folder);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The portal could not save the image. Check the portal server's write access to its CompanyPolicyMedia folder and review the server log."
            });
        }
        return Json(new { success = true, name, url = "/media/company-policies/" + name });
    }

    [HttpPost("Policy"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PolicySave(SaveCompanyPolicyRequest request)
    {
        if (request.Body?.Contains(ItPolicyContent.PendingTitle, StringComparison.Ordinal) == true)
            ModelState.AddModelError(nameof(request.Body), "Replace all approver title placeholders before saving this policy.");
        if (request.PolicyId is > 0)
            ViewBag.IsPublished = (await _companyInfo.GetPolicyByIdAsync(request.PolicyId.Value))?.IsPublished == true;
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
        var item = await _companyInfo.GetPolicyByIdAsync(id);
        if (item?.Body.Contains(ItPolicyContent.PendingTitle, StringComparison.Ordinal) == true)
        {
            TempData["CompanyInfoError"] = "Replace the approver title placeholders before publishing.";
            return RedirectToAction(nameof(Index));
        }
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
