using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Controllers;

[Authorize(Policy = "EmployeeResourceEditor")]
[Route("/Admin/EmployeeResources")]
public sealed class EmployeeResourcesAdminController : Controller
{
    private const long MaxAttachmentBytes = 100L * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    private readonly IEmployeeResourceRepository _resources;
    private readonly IEmployeeResourceCatalog _demoCatalog;
    private readonly IDemoModeService _demoMode;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EmployeeResourcesAdminController> _logger;

    public EmployeeResourcesAdminController(
        IEmployeeResourceRepository resources,
        IEmployeeResourceCatalog demoCatalog,
        IDemoModeService demoMode,
        IWebHostEnvironment environment,
        ILogger<EmployeeResourcesAdminController> logger)
    {
        _resources = resources;
        _demoCatalog = demoCatalog;
        _demoMode = demoMode;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? id)
    {
        var demoModeEnabled = _demoMode.IsEnabled(HttpContext);
        try
        {
            var items = await _resources.GetManagedAsync();
            var categories = await _resources.GetCategoriesAsync();
            var departments = await _resources.GetDepartmentsAsync();
            var isDemoData = demoModeEnabled && items.Count == 0;
            if (isDemoData)
                items = _demoCatalog.GetPublished();
            var selected = id is > 0 ? items.FirstOrDefault(item => item.ResourceId == id.Value) : null;
            return View(BuildViewModel(items, selected, categories, departments, isDatabaseAvailable: true,
                setupMessage: isDemoData ? "The database is connected, but no managed Employee Resources have been published yet. The records below are read-only IT preview data; use New resource to create the first managed record." : null,
                selectedId: id,
                isDemoData: isDemoData));
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Employee Resources database is not available for the admin page.");
            var demoItems = demoModeEnabled ? _demoCatalog.GetPublished() : Array.Empty<EmployeeResourceItem>();
            var demoCategories = demoItems.Select((item, index) => new EmployeeResourceOption { Id = -(index + 1), Name = item.Category, Slug = item.Category.ToLowerInvariant().Replace(' ', '-') }).DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var demoDepartments = demoItems.Select((item, index) => new EmployeeResourceOption { Id = -(index + 1), Name = item.OwnerDepartment }).DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            return View(BuildViewModel(demoItems, null, demoCategories, demoDepartments, isDatabaseAvailable: false,
                setupMessage: "The Employee Resources database schema is not available. Run Migration_EmployeeResources_CreateTables.sql before creating or editing managed records.",
                selectedId: id,
                isDemoData: demoModeEnabled));
        }
    }

    [HttpPost("Save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaveEmployeeResourceRequest request)
    {
        try
        {
            if (!ModelState.IsValid) throw new ArgumentException(string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage).Where(message => !string.IsNullOrWhiteSpace(message))));
            var saved = await _resources.SaveDraftAsync(request, UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["EmployeeResourceNotice"] = $"Draft '{saved.Title}' saved.";
            return RedirectToAction(nameof(Index), new { id = saved.ResourceId });
        }
        catch (Exception ex)
        {
            TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The Employee Resource could not be saved.");
            return RedirectToAction(nameof(Index), new { id = request.ResourceId });
        }
    }

    [HttpPost("Publish/{id:int}"), ValidateAntiForgeryToken, Authorize(Policy = "EmployeeResourcePublisher")]
    public async Task<IActionResult> Publish(int id, EmployeeResourceWorkflowRequest request)
    {
        try
        {
            await _resources.ChangeStatusAsync(id, new[] { "Draft", "Rejected" }, "Published", UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString(), request.Remarks, request.PublishStartUtc);
            TempData["EmployeeResourceNotice"] = "Employee Resource published.";
        }
        catch (Exception ex) { TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The Employee Resource could not be published."); }
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost("Archive/{id:int}"), ValidateAntiForgeryToken, Authorize(Policy = "EmployeeResourcePublisher")]
    public async Task<IActionResult> Archive(int id, EmployeeResourceWorkflowRequest request)
    {
        try
        {
            await _resources.ChangeStatusAsync(id, new[] { "Published" }, "Archived", UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString(), request.Remarks);
            TempData["EmployeeResourceNotice"] = "Employee Resource archived.";
        }
        catch (Exception ex) { TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The Employee Resource could not be archived."); }
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost("Restore/{id:int}"), ValidateAntiForgeryToken, Authorize(Policy = "EmployeeResourcePublisher")]
    public async Task<IActionResult> Restore(int id, EmployeeResourceWorkflowRequest request)
    {
        try
        {
            await _resources.ChangeStatusAsync(id, new[] { "Archived" }, "Draft", UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString(), request.Remarks);
            TempData["EmployeeResourceNotice"] = "Employee Resource restored to draft.";
        }
        catch (Exception ex) { TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The Employee Resource could not be restored."); }
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost("Delete/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, EmployeeResourceDeleteRequest request)
    {
        try
        {
            var paths = await _resources.DeleteAsync(id, request.RowVersion, UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            foreach (var path in paths)
            {
                var physicalPath = ResolvePhysicalPath(path);
                if (physicalPath is not null && System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
            }
            TempData["EmployeeResourceNotice"] = "Draft Employee Resource deleted.";
        }
        catch (Exception ex) { TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The Employee Resource could not be deleted."); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Upload/{id:int}"), ValidateAntiForgeryToken, RequestSizeLimit(MaxAttachmentBytes)]
    public async Task<IActionResult> Upload(int id, IFormFile? file, string? description)
    {
        string? physicalPath = null;
        try
        {
            if (file is null || file.Length == 0) throw new ArgumentException("Choose a file to upload.");
            if (file.Length > MaxAttachmentBytes) throw new ArgumentException("Attachments cannot exceed 100 MB.");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.TryGetValue(extension, out var mimeType)) throw new ArgumentException("This attachment type is not allowed.");
            var webRoot = WebRoot();
            var folderName = DateTime.UtcNow.ToString("yyyy-MM");
            var folder = Path.Combine(webRoot, "media", "employee-resources", folderName);
            Directory.CreateDirectory(folder);
            var baseName = SanitizeBaseName(Path.GetFileNameWithoutExtension(file.FileName));
            var storedFileName = $"{baseName}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension}";
            physicalPath = Path.Combine(folder, storedFileName);
            await using (var stream = System.IO.File.Create(physicalPath)) await file.CopyToAsync(stream, HttpContext.RequestAborted);
            var storedPath = $"/media/employee-resources/{folderName}/{storedFileName}";
            await _resources.AddAttachmentAsync(id, Path.GetFileName(file.FileName), storedPath, extension, file.Length, mimeType, description, UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["EmployeeResourceNotice"] = "Attachment uploaded.";
        }
        catch (Exception ex)
        {
            if (physicalPath is not null && System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
            TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The attachment could not be uploaded.");
        }
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost("DeleteAttachment/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id, int resourceId)
    {
        try
        {
            var path = await _resources.DeleteAttachmentAsync(id, UserId(), User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            var physicalPath = path is null ? null : ResolvePhysicalPath(path);
            if (physicalPath is not null && System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
            TempData["EmployeeResourceNotice"] = "Attachment removed.";
        }
        catch (Exception ex) { TempData["EmployeeResourceError"] = UserFacingMessage(ex, "The attachment could not be removed."); }
        return RedirectToAction(nameof(Index), new { id = resourceId });
    }

    private EmployeeResourceAdminViewModel BuildViewModel(IReadOnlyList<EmployeeResourceItem> items, EmployeeResourceItem? selected, IReadOnlyList<EmployeeResourceOption> categories, IReadOnlyList<EmployeeResourceOption> departments, bool isDatabaseAvailable, string? setupMessage, int? selectedId, bool isDemoData = false) => new()
    {
        Items = items,
        SelectedItem = selected,
        Categories = categories,
        Departments = departments,
        IsDatabaseAvailable = isDatabaseAvailable,
        IsDemoData = isDemoData,
        SetupMessage = setupMessage,
        CanPublish = User.HasClaim("IsDeveloper", "true") || User.IsInRole("EmployeeResourcePublisher"),
        SelectedId = selectedId
    };

    private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : throw new InvalidOperationException("Authenticated user ID is unavailable.");

    private string WebRoot()
    {
        var root = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(root);
        return root;
    }

    private string? ResolvePhysicalPath(string storedPath)
    {
        if (!storedPath.StartsWith("/media/employee-resources/", StringComparison.OrdinalIgnoreCase) || storedPath.Contains("..", StringComparison.Ordinal) || storedPath.Contains('\\')) return null;
        var root = Path.GetFullPath(WebRoot());
        var relative = storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private static string SanitizeBaseName(string value)
    {
        var result = string.Concat((value ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "attachment" : result[..Math.Min(80, result.Length)];
    }

    private static string UserFacingMessage(Exception ex, string fallback) => ex is ArgumentException or InvalidOperationException or DBConcurrencyException ? ex.Message : fallback;
}
