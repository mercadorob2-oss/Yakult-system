using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

public interface IEmployeeResourceRepository
{
    Task<IReadOnlyList<EmployeeResourceItem>> GetPublishedAsync(string? resourceType = null, string? categorySlug = null, string? query = null, int take = 100);
    Task<EmployeeResourceItem?> GetPublishedBySlugAsync(string slug);
    Task<IReadOnlyList<EmployeeResourceItem>> GetManagedAsync();
    Task<EmployeeResourceItem?> GetByIdAsync(int resourceId);
    Task<IReadOnlyList<EmployeeResourceOption>> GetCategoriesAsync();
    Task<IReadOnlyList<EmployeeResourceOption>> GetDepartmentsAsync();
    Task<EmployeeResourceItem> SaveDraftAsync(SaveEmployeeResourceRequest request, int userId, string? userName, string? ipAddress);
    Task ChangeStatusAsync(int resourceId, string[] expectedStatuses, string newStatus, int userId, string? userName, string? ipAddress, string? remarks, DateTime? publishStartUtc = null);
    Task<IReadOnlyList<string>> DeleteAsync(int resourceId, string rowVersion, int userId, string? userName, string? ipAddress);
    Task<EmployeeResourceAttachment> AddAttachmentAsync(int resourceId, string fileName, string storedPath, string extension, long sizeBytes, string mimeType, string? description, int userId, string? userName, string? ipAddress);
    Task<string?> DeleteAttachmentAsync(int attachmentId, int userId, string? userName, string? ipAddress);
    Task<EmployeeResourceDownload?> GetPublishedDownloadAsync(int attachmentId);
    Task<IReadOnlyList<EmployeeResourceRevision>> GetRevisionsAsync(int resourceId);
}
