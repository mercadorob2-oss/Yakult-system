using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;

namespace Yakult.SystemsPortal.Controllers;

[Authorize(Policy="ContentEditor")]
[Route("/Admin/LearningContent")]
public sealed class ContentAdminController : Controller
{
    private const long MaxMediaBytes = 500L * 1024 * 1024;

    private readonly IPortalContentRepository _content;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public ContentAdminController(IPortalContentRepository content, IWebHostEnvironment environment, IHttpClientFactory http, IConfiguration config)
    {
        _content = content;
        _environment = environment;
        _http = http;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(new ContentEditorViewModel { Items = await _content.GetManagedAsync(), Categories = await _content.GetCategoriesAsync(), CanPublish = User.HasClaim("IsDeveloper", "true") });

    [HttpGet("Stats/{contentId:int}")]
    public async Task<IActionResult> Stats(int contentId, int page = 1, int pageSize = 50)
    {
        try
        {
            var stats = await _content.GetCourseCompletionStatsAsync(contentId, page, pageSize);
            return Json(new
            {
                success = true,
                started = stats.StartedCount,
                completed = stats.CompletedCount,
                page = stats.Page,
                pageSize = stats.PageSize,
                totalPages = stats.TotalPages,
                employees = stats.Completed.Select(x => new { x.UserId, x.Name, completedAtUtc = x.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("Save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SavePortalContentRequest request)
    {
        try { ValidateExistingMediaPath(request.ThumbnailUrl, "thumbnail"); ValidateExistingMediaPath(request.MediaUrl, "media"); var saved = await _content.SaveDraftAsync(request, UserId(), User.Identity?.Name); return Json(new { success = true, item = saved }); }
        catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("Upload"), ValidateAntiForgeryToken, RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string target = "media")
    {
        try
        {
            if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "Choose a file to upload." });
            var plan = ResolveUploadPlan(file, target);
            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var folder = Path.Combine(webRoot, "media", "learning", plan.Folder);
            Directory.CreateDirectory(folder);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var baseName = Path.GetFileNameWithoutExtension(file.FileName);
            baseName = string.Concat(baseName.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')).Trim('-');
            if (string.IsNullOrWhiteSpace(baseName)) baseName = plan.Kind.ToLowerInvariant();
            var safeName = baseName[..Math.Min(baseName.Length, 80)];
            var storedFileName = $"{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension}";
            var physicalPath = Path.Combine(folder, storedFileName);

            await using (var stream = System.IO.File.Create(physicalPath))
                await file.CopyToAsync(stream);

            var url = $"/media/learning/{plan.Folder}/{storedFileName}";
            return Json(new { success = true, url, fileName = file.FileName, mediaKind = plan.Kind });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }


    [HttpPost("ImportUrl"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportUrl([FromBody] ImportUrlRequest request)
    {
        try
        {
            if (request.Url is null || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return BadRequest(new { success = false, message = "Enter a valid http:// or https:// URL." });

            var storedFileName = IsYouTubeUri(uri)
                ? await ImportWithYtDlpAsync(uri, HttpContext.RequestAborted)
                : await ImportFromDirectUrlAsync(uri, HttpContext.RequestAborted);

            var url = $"/media/learning/videos/{storedFileName}";
            return Json(new { success = true, url, fileName = storedFileName, mediaKind = "Video" });
        }
        catch (OperationCanceledException)
        {
            return BadRequest(new { success = false, message = "The download was cancelled." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"Download failed: {ex.Message}" });
        }
    }

    private static bool IsYouTubeUri(Uri uri) =>
        uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);

    private string VideoFolder()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var folder = Path.Combine(webRoot, "media", "learning", "videos");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string SanitizeBaseName(string name, string fallback)
    {
        var baseName = string.Concat((name ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(baseName)) baseName = fallback;
        return baseName[..Math.Min(baseName.Length, 80)];
    }

    private static string NextStoredFileName(string baseName, string fallback, string extension)
    {
        return $"{SanitizeBaseName(baseName, fallback)}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension}";
    }

    private static void RejectOversize(string physicalPath)
    {
        if (new FileInfo(physicalPath).Length > MaxMediaBytes)
        {
            System.IO.File.Delete(physicalPath);
            throw new InvalidOperationException("The video is larger than the 500 MB limit.");
        }
    }

    private async Task<string> ImportFromDirectUrlAsync(Uri uri, CancellationToken ct)
    {
        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (extension != ".mp4" && extension != ".webm")
            throw new InvalidOperationException("Use a direct MP4 or WebM file URL, or a YouTube link.");

        using var response = await _http.CreateClient("MediaImport").GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The remote server returned {(int)response.StatusCode} ({response.ReasonPhrase}).");

        if (response.Content.Headers.ContentLength is > MaxMediaBytes)
            throw new InvalidOperationException("The remote video is larger than the 500 MB limit.");

        var storedFileName = NextStoredFileName(Path.GetFileNameWithoutExtension(uri.AbsolutePath), "imported", extension);
        var physicalPath = Path.Combine(VideoFolder(), storedFileName);

        await using (var stream = System.IO.File.Create(physicalPath))
            await response.Content.CopyToAsync(stream, ct);

        RejectOversize(physicalPath);
        return storedFileName;
    }

    private async Task<string> ImportWithYtDlpAsync(Uri uri, CancellationToken ct)
    {
        var exe = _config["MediaImport:YtDlpPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(exe)) exe = Path.Combine(_environment.ContentRootPath, "tools", "yt-dlp.exe");
        if (!System.IO.File.Exists(exe))
            throw new InvalidOperationException("yt-dlp is not installed on the server. Place yt-dlp.exe in the tools folder or set MediaImport:YtDlpPath in appsettings.");

        var tempDir = Path.Combine(Path.GetTempPath(), "yakult-media-import");
        Directory.CreateDirectory(tempDir);
        foreach (var stale in Directory.GetFiles(tempDir, "import-*"))
            System.IO.File.Delete(stale);

        var stem = $"import-{Guid.NewGuid().ToString("N")[..8]}";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(20));

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("best[ext=mp4]/best");
        psi.ArgumentList.Add("--no-playlist");
        psi.ArgumentList.Add("--no-warnings");
        psi.ArgumentList.Add("--no-progress");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(Path.Combine(tempDir, $"{stem}-%(title)s.%(ext)s"));
        psi.ArgumentList.Add(uri.AbsoluteUri);

        string stderr;
        using (var process = Process.Start(psi))
        {
            if (process is null) throw new InvalidOperationException("Unable to start yt-dlp.");
            var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            await Task.WhenAll(stdout, stderrTask);
            stderr = stderrTask.Result;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(SanitizeYtDlpError(stderr));
        }

        var files = Directory.GetFiles(tempDir, $"{stem}-*");
        if (files.Length == 0)
            throw new InvalidOperationException("yt-dlp finished but produced no media file.");

        var produced = files.OrderByDescending(f => new FileInfo(f).Length).First();
        var extension = Path.GetExtension(produced).ToLowerInvariant();
        if (extension != ".mp4" && extension != ".webm")
        {
            foreach (var f in files) System.IO.File.Delete(f);
            throw new InvalidOperationException($"yt-dlp produced an unsupported format ({extension}); only MP4 and WebM play in the portal.");
        }
        RejectOversize(produced);

        var rawTitle = Path.GetFileNameWithoutExtension(produced);
        if (rawTitle.StartsWith(stem + "-", StringComparison.OrdinalIgnoreCase)) rawTitle = rawTitle[(stem.Length + 1)..];
        var storedFileName = NextStoredFileName(rawTitle, "youtube-video", extension);
        System.IO.File.Move(produced, Path.Combine(VideoFolder(), storedFileName));
        foreach (var f in files) { if (System.IO.File.Exists(f)) System.IO.File.Delete(f); }
        return storedFileName;
    }

    private static string SanitizeYtDlpError(string stderr)
    {
        var cleaned = Regex.Replace(stderr ?? string.Empty, @"\u001b\[[0-9;]*m", string.Empty);
        cleaned = Regex.Replace(cleaned, @"https?://\S+", "[link]");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (cleaned.Length > 300) cleaned = cleaned[..300];
        return string.IsNullOrWhiteSpace(cleaned) ? "yt-dlp could not download the video." : cleaned;
    }


    [HttpPost("Publish"), ValidateAntiForgeryToken, Authorize(Policy = "ContentPublisher")]
    public async Task<IActionResult> Publish([FromBody] ContentWorkflowRequest request)
    { try { await _content.ChangeStatusAsync(request.ContentId, new[] { "Draft", "PendingReview", "Rejected" }, "Published", UserId(), User.Identity?.Name, request.Remarks, request.PublishStartUtc); return Json(new { success = true }); } catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); } }


    [HttpPost("Archive"), ValidateAntiForgeryToken, Authorize(Policy = "ContentPublisher")]
    public async Task<IActionResult> Archive([FromBody] ContentWorkflowRequest request)
    { try { await _content.ChangeStatusAsync(request.ContentId, new[] { "Published" }, "Archived", UserId(), User.Identity?.Name, request.Remarks); return Json(new { success = true }); } catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); } }

    [HttpPost("Restore"), ValidateAntiForgeryToken, Authorize(Policy = "ContentPublisher")]
    public async Task<IActionResult> Restore([FromBody] ContentWorkflowRequest request)
    { try { await _content.ChangeStatusAsync(request.ContentId, new[] { "Archived" }, "Draft", UserId(), User.Identity?.Name, request.Remarks); return Json(new { success = true }); } catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); } }

    private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new InvalidOperationException("Authenticated user ID is unavailable.");


    private void ValidateExistingMediaPath(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)) return;
        if (value.Contains("..", StringComparison.Ordinal) || value.Contains('\\')) throw new InvalidOperationException($"The {label} path is not valid.");

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var relative = value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, relative));
        var mediaRoot = Path.GetFullPath(Path.Combine(webRoot, "media"));
        if (!fullPath.StartsWith(mediaRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            throw new InvalidOperationException($"The {label} file was not found under wwwroot/media.");
    }
    private static UploadPlan ResolveUploadPlan(IFormFile file, string target)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension)) throw new InvalidOperationException("Uploaded file must have an extension.");

        var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };
        var documentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx" };

        var normalizedTarget = (target ?? "media").Trim().ToLowerInvariant();
        if (normalizedTarget == "thumbnail")
        {
            if (!imageExtensions.Contains(extension)) throw new InvalidOperationException("Thumbnail uploads must be JPG, PNG, WebP, or GIF.");
            if (file.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Thumbnail uploads must not exceed 5 MB.");
            return new UploadPlan("thumbnails", "Thumbnail");
        }

        if (videoExtensions.Contains(extension))
        {
            if (file.Length > 500L * 1024 * 1024) throw new InvalidOperationException("Video uploads must not exceed 500 MB.");
            return new UploadPlan("videos", "Video");
        }

        if (documentExtensions.Contains(extension))
        {
            if (file.Length > 25L * 1024 * 1024) throw new InvalidOperationException("Document uploads must not exceed 25 MB.");
            return new UploadPlan("documents", "Document");
        }

        if (imageExtensions.Contains(extension))
        {
            if (file.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Image uploads must not exceed 5 MB.");
            return new UploadPlan("thumbnails", "Thumbnail");
        }

        throw new InvalidOperationException("Unsupported file type. Use JPG, PNG, WebP, GIF, MP4, WebM, PDF, or Office documents.");
    }

    private sealed record UploadPlan(string Folder, string Kind);
}
