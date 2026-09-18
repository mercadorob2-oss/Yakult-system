using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Controllers;

[Authorize]
public sealed class AdminController : Controller
{
    private readonly PortalOptions _options;
    private readonly IPortalCardRepository _portalCardRepository;
    private readonly IAccountAdministrationRepository _accountAdministrationRepository;
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly IConnectionPageAccessGate _connectionPageAccessGate;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IOptions<PortalOptions> options,
        IPortalCardRepository portalCardRepository,
        IAccountAdministrationRepository accountAdministrationRepository,
        IConnectionStringProvider connectionStringProvider,
        IConnectionPageAccessGate connectionPageAccessGate,
        IWebHostEnvironment environment,
        ILogger<AdminController> logger)
    {
        _options = options.Value;
        _portalCardRepository = portalCardRepository;
        _accountAdministrationRepository = accountAdministrationRepository;
        _connectionStringProvider = connectionStringProvider;
        _connectionPageAccessGate = connectionPageAccessGate;
        _environment = environment;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var cardsTask = _portalCardRepository.GetAdminCardsAsync();
        var auditTask = _portalCardRepository.GetRecentAuditAsync(8);
        var noticeTask = _portalCardRepository.GetNoticeSettingsAsync();

        await Task.WhenAll(cardsTask, auditTask, noticeTask);

        return View(new AdminDashboardViewModel
        {
            Cards = cardsTask.Result,
            RecentEvents = auditTask.Result,
            Notice = noticeTask.Result
        });
    }

    public async Task<IActionResult> Cards()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var cards = await _portalCardRepository.GetAdminCardsAsync();

        return View(new AdminCardsViewModel { Cards = cards });
    }

    public async Task<IActionResult> Audit()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var items = await _portalCardRepository.GetRecentAuditAsync(150);
        return View(new AdminAuditViewModel { Items = items });
    }

    public async Task<IActionResult> Health()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var cards = await _portalCardRepository.GetAdminCardsAsync();
        return View(new AdminCardsViewModel { Cards = cards });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckHealth()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var cards = await _portalCardRepository.GetAdminCardsAsync();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        var results = new List<object>();

        foreach (var card in cards.Where(c => !string.IsNullOrWhiteSpace(c.HealthCheckUrl)))
        {
            var started = DateTime.UtcNow;
            try
            {
                using var response = await http.GetAsync(card.HealthCheckUrl);
                results.Add(new
                {
                    cardKey = card.CardKey,
                    status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                    detail = $"{(int)response.StatusCode} {response.ReasonPhrase}",
                    checkedAtUtc = started
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    cardKey = card.CardKey,
                    status = "Unreachable",
                    detail = ex.Message,
                    checkedAtUtc = started
                });
            }
        }

        return Json(new { success = true, results });
    }

    public async Task<IActionResult> Settings()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var cards = await _portalCardRepository.GetAdminCardsAsync();
        return View(new PortalSettingsViewModel
        {
            Notice = await _portalCardRepository.GetNoticeSettingsAsync(),
            CardsJson = JsonSerializer.Serialize(cards, new JsonSerializerOptions { WriteIndented = true }),
            DatabaseSettingsAvailable = await _portalCardRepository.IsPortalSettingsAvailableAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotice([FromBody] SavePortalNoticeRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            await _portalCardRepository.SaveNoticeSettingsAsync(request, userId);
            var savedNotice = new PortalNoticeSettings
            {
                Enabled = request.Enabled,
                Level = request.Level,
                Title = request.Title,
                Message = request.Message,
                StartUtc = request.StartUtc,
                EndUtc = request.EndUtc
            };
            return Json(new
            {
                success = true,
                activeNow = savedNotice.IsActive(DateTime.UtcNow),
                maintenanceMode = savedNotice.IsMaintenanceMode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save portal notice.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> ConnectionProperties()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        if (!IsConnectionPageUnlocked())
        {
            return RedirectToAction(nameof(UnlockConnectionPage));
        }

        return View(new AdminConnectionEditorViewModel
        {
            Properties = _connectionStringProvider.GetConnectionProperties()
        });
    }

    [HttpGet]
    public async Task<IActionResult> UnlockConnectionPage()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        if (IsConnectionPageUnlocked())
        {
            return RedirectToAction(nameof(ConnectionProperties));
        }

        var state = await _connectionPageAccessGate.GetStateAsync();
        return View(new ConnectionPageUnlockViewModel
        {
            IsConfigured = state.IsConfigured,
            IsStorageAvailable = state.IsStorageAvailable,
            StorageMessage = state.StorageMessage
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockConnectionPage(ConnectionPageUnlockViewModel model)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var state = await _connectionPageAccessGate.GetStateAsync();
        model.IsConfigured = state.IsConfigured;
        model.IsStorageAvailable = state.IsStorageAvailable;
        model.StorageMessage = state.StorageMessage;

        if (!state.IsStorageAvailable)
        {
            model.ErrorMessage = state.StorageMessage ?? "Secure passphrase storage is unavailable.";
            return View(model);
        }

        if (!state.IsConfigured)
        {
            if (string.IsNullOrWhiteSpace(model.Passphrase) || model.Passphrase.Length < 12)
            {
                model.ErrorMessage = "Use a passphrase with at least 12 characters.";
                return View(model);
            }

            if (!string.Equals(model.Passphrase, model.ConfirmPassphrase, StringComparison.Ordinal))
            {
                model.ErrorMessage = "The passphrase confirmation does not match.";
                return View(model);
            }

            try
            {
                var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                    ? parsedUserId
                    : (int?)null;
                await _connectionPageAccessGate.ConfigureAsync(model.Passphrase, userId);
                HttpContext.Session.SetString(ConnectionPageSessionKey, "1");
                _logger.LogInformation("Database Connection passphrase configured and page unlocked by {User}.", User.Identity?.Name);
                TempData["ConnectionPageStatus"] = "Passphrase secured. Database Connection is now unlocked for this session.";
                return RedirectToAction(nameof(ConnectionProperties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure Database Connection passphrase.");
                model.ErrorMessage = "The passphrase could not be saved. Please try again.";
                return View(model);
            }
        }

        if (string.IsNullOrWhiteSpace(model.Passphrase) || !await _connectionPageAccessGate.VerifyAsync(model.Passphrase))
        {
            _logger.LogWarning("Failed Database Connection page unlock attempt by {User} from {Ip}.",
                User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            model.ErrorMessage = "Incorrect passphrase.";
            return View(model);
        }

        HttpContext.Session.SetString(ConnectionPageSessionKey, "1");
        _logger.LogInformation("Database Connection page unlocked by {User}.", User.Identity?.Name);
        return RedirectToAction(nameof(ConnectionProperties));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection([FromBody] ConnectionProfileRequest? request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        if (!IsConnectionPageUnlocked())
        {
            return Forbid();
        }

        var result = await _connectionStringProvider.TestConnectionAsync(
            string.IsNullOrWhiteSpace(request?.ConnectionString) ? null : request.ConnectionString);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyConnection([FromBody] ConnectionProfileRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        if (!IsConnectionPageUnlocked())
        {
            return Forbid();
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ConnectionString))
            return BadRequest(new { success = false, message = "Enter a connection string before applying." });

        var test = await _connectionStringProvider.TestConnectionAsync(request.ConnectionString);
        if (!test.Success)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = $"Connection was not changed. Test failed: {test.Message}" });
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            _connectionStringProvider.ApplyConnectionString(request.ConnectionString, userId);
            _logger.LogWarning("Database connection profile changed by {User} from {Ip}.",
                User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Json(new
            {
                success = true,
                message = "Connection tested successfully and applied. New database connections will use this profile immediately; restart the portal if required by the host.",
                properties = _connectionStringProvider.GetConnectionProperties()
            });
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist the new database connection profile.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "The connection tested successfully but could not be persisted." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollbackConnection()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        if (!IsConnectionPageUnlocked())
        {
            return Forbid();
        }

        var test = await _connectionStringProvider.TestRollbackConnectionAsync();
        if (!test.Success)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = $"Rollback was not applied. Previous profile test failed: {test.Message}" });
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            _connectionStringProvider.RollbackConnection(userId);
            _logger.LogWarning("Database connection profile rolled back by {User} from {Ip}.",
                User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Json(new
            {
                success = true,
                message = "The previous tested connection profile has been restored.",
                properties = _connectionStringProvider.GetConnectionProperties()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to roll back the database connection profile.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "The rollback could not be completed." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LockConnectionPage()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        HttpContext.Session.Remove(ConnectionPageSessionKey);
        return RedirectToAction(nameof(UnlockConnectionPage));
    }

    private const string ConnectionPageSessionKey = "AdminConnectionPageUnlocked";

    private bool IsConnectionPageUnlocked()
    {
        return HttpContext.Session.GetString(ConnectionPageSessionKey) == "1";
    }

    public async Task<IActionResult> Export()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var payload = new
        {
            exportedAtUtc = DateTime.UtcNow,
            cards = await _portalCardRepository.GetAdminCardsAsync()
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "yakult-portal-export.json");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCard([FromBody] SavePortalCardRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            var saved = await _portalCardRepository.SaveCardAsync(
                request,
                userId,
                User.Identity?.Name,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Json(new { success = true, card = saved });
        }
        catch (DBConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Portal card concurrency conflict for {CardKey}", request.CardKey);
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save portal card {CardKey}", request.CardKey);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to save the card. Please refresh and try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> UploadInstaller(IFormFile? file, [FromForm] string? cardKey)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Choose an installer file to upload." });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".apk",
                ".exe",
                ".msi",
                ".zip"
            };
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { success = false, message = "Installer uploads must be APK, EXE, MSI, or ZIP files." });

            var safeCardKey = Regex.Replace((cardKey ?? "desktop-app").Trim().ToLowerInvariant(), @"[^a-z0-9_-]+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(safeCardKey))
                safeCardKey = "desktop-app";

            var originalBaseName = Path.GetFileNameWithoutExtension(file.FileName);
            var safeBaseName = Regex.Replace(originalBaseName.ToLowerInvariant(), @"[^a-z0-9._-]+", "-").Trim('-', '.', '_');
            if (string.IsNullOrWhiteSpace(safeBaseName))
                safeBaseName = "installer";
            if (safeBaseName.Length > 80)
                safeBaseName = safeBaseName[..80];

            var storedFileName = $"{safeCardKey}-{safeBaseName}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
            var installerRoot = Path.Combine(_environment.ContentRootPath, "installer", "uploads");
            Directory.CreateDirectory(installerRoot);

            var physicalPath = Path.GetFullPath(Path.Combine(installerRoot, storedFileName));
            var root = Path.GetFullPath(installerRoot);
            if (!physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Installer file name is not valid." });

            await using (var stream = System.IO.File.Create(physicalPath))
            {
                await file.CopyToAsync(stream);
            }

            var installerPath = Path.Combine("installer", "uploads", storedFileName);
            return Json(new
            {
                success = true,
                fileName = file.FileName,
                installerPath,
                size = file.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload portal installer for {CardKey}.", cardKey);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to upload the installer. Please try again." });
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveCard([FromBody] ArchivePortalCardRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }
        

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            await _portalCardRepository.ArchiveCardAsync(
                request,
                userId,
                User.Identity?.Name,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Json(new { success = true });
        }
        catch (DBConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Portal card archive concurrency conflict for {CardKey}", request.CardKey);
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive portal card {CardKey}", request.CardKey);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to archive the card. Please refresh and try again." });
        }
    }

    public async Task<IActionResult> AccountRequests()
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        var model = await _accountAdministrationRepository.GetAccountAdministrationDataAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAccountRequest([FromBody] ReviewAccountRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            await _accountAdministrationRepository.ApproveAccountRequestAsync(request.AccountRequestId, userId, request.Remarks);
            return Json(new { success = true, message = "Account request approved." });
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve account request {AccountRequestId}", request.AccountRequestId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to approve the account request. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAccountRequest([FromBody] ReviewAccountRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Remarks))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = "A rejection reason is required." });
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            await _accountAdministrationRepository.RejectAccountRequestAsync(request.AccountRequestId, userId, request.Remarks!);
            return Json(new { success = true, message = "Account request rejected." });
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject account request {AccountRequestId}", request.AccountRequestId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to reject the account request. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateManagedUser([FromBody] UpdateManagedUserRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        try
        {
            await _accountAdministrationRepository.UpdateManagedUserAsync(request);
            return Json(new { success = true, message = "Account access updated." });
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update managed UserId {UserId}", request.UserId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to update the account. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetManagedUserPassword([FromBody] ResetManagedUserPasswordRequest request)
    {
        if (!User.HasClaim("IsDeveloper", "true"))
        {
            return Forbid();
        }

        try
        {
            var temporaryPassword = await _accountAdministrationRepository.ResetManagedUserPasswordAsync(request);
            return Json(new
            {
                success = true,
                message = temporaryPassword != null ? "Temporary password generated." : "Password updated.",
                temporaryPassword
            });
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset password for managed UserId {UserId}", request.UserId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to reset the password. Please try again." });
        }
    }
}


