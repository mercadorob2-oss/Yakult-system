using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;

namespace Yakult.SystemsPortal.Controllers;

[AllowAnonymous]
public sealed class MaintenanceController : Controller
{
    private readonly IPortalCardRepository _portalCardRepository;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(
        IPortalCardRepository portalCardRepository,
        ILogger<MaintenanceController> logger)
    {
        _portalCardRepository = portalCardRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        PortalNoticeSettings notice;
        try
        {
            notice = await _portalCardRepository.GetNoticeSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Maintenance page could not read the portal notice.");
            return RedirectToAction("Index", "Public");
        }

        if (!notice.IsMaintenanceMode || !notice.IsActive(DateTime.UtcNow))
            return RedirectToAction("Index", "Public");

        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        ViewData["Title"] = string.IsNullOrWhiteSpace(notice.Title)
            ? "Portal maintenance"
            : notice.Title;
        return View(notice);
    }
}
