using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

public interface IPortalContentRepository
{
    Task<IReadOnlyList<PortalContentItem>> GetPublishedAsync(string? contentType = null, string? categorySlug = null, string? query = null, int take = 50);
    Task<PortalContentItem?> GetPublishedBySlugAsync(string slug);
    Task<IReadOnlyList<PortalContentItem>> GetManagedAsync();
    Task<IReadOnlyList<PortalContentCategory>> GetCategoriesAsync();
    Task<PortalContentItem> SaveDraftAsync(SavePortalContentRequest request, int userId, string? userName);
    Task ChangeStatusAsync(int contentId, string[] expectedStatuses, string newStatus, int userId, string? userName, string? remarks, DateTime? publishStartUtc = null);
    Task TrackAsync(string eventType, string targetType, string targetKey);
    Task<PortalCourseProgress?> GetCourseProgressAsync(int userId, int contentId);
    Task<PortalCourseProgress> SaveCourseProgressAsync(int userId, int contentId, int positionSec, int durationSec);
    Task<IReadOnlyDictionary<int, PortalCourseProgress>> GetCourseProgressBatchAsync(int userId, IReadOnlyCollection<int> contentIds);
    Task<IReadOnlyList<MyLearningItem>> GetUserCourseProgressAsync(int userId);
    Task<CourseCompletionStats> GetCourseCompletionStatsAsync(int contentId, int page = 1, int pageSize = 50);
}
