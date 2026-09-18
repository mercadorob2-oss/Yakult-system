using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

public interface IPortalCardRepository
{
    Task<IReadOnlyList<PortalSystemLink>> GetVisibleSystemCardsAsync();

    Task<IReadOnlyList<AdminPortalCardViewModel>> GetAdminCardsAsync();

    Task<AdminPortalCardViewModel> SaveCardAsync(
        SavePortalCardRequest request,
        int? userId,
        string? userName,
        string? ipAddress);

    Task ArchiveCardAsync(
        ArchivePortalCardRequest request,
        int? userId,
        string? userName,
        string? ipAddress);

    Task UpdateHealthStatusAsync(string cardKey, string status, DateTime checkedAtUtc);

    Task LogPortalEventAsync(
        string action,
        string entityType,
        int? entityId,
        int? userId,
        string? userName,
        string? notes,
        string? ipAddress);

    Task<IReadOnlyList<PortalAuditLogItem>> GetRecentAuditAsync(int take = 100);

    Task<bool> IsPortalSettingsAvailableAsync();

    Task<PortalNoticeSettings> GetNoticeSettingsAsync();

    Task SaveNoticeSettingsAsync(SavePortalNoticeRequest request, int? userId);
}
