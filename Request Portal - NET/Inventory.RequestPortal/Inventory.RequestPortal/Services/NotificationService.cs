using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Repositories;

namespace Inventory.RequestPortal.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(NotificationCreateDto dto);
        Task NotifyAuthorizationStatusChangedAsync(int authorizationId, string notificationType, string title, string message);
        Task NotifyApprovalStatusChangedAsync(int approvalId, string notificationType, string title, string message);
        Task<int?> GetRecentPortalReqIdByApprovalIdAsync(int approvalId);
        Task<string?> GetSetCodeByReqIdAsync(int reqId);
        Task<string?> GetRecentPortalSetCodeByApprovalIdAsync(int approvalId);
        Task<string?> GetSetCodeByAuthorizationIdAsync(int authorizationId);
        Task<List<NotificationDto>> GetNotificationsForUserAsync(int userId, int limit = 50);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
        Task ClearAllAsync(int userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repo;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            INotificationRepository repo,
            ILogger<NotificationService> logger)
        {
            _repo   = repo;
            _logger = logger;
        }

        public Task CreateNotificationAsync(NotificationCreateDto dto)
            => _repo.CreateAsync(dto);

        public async Task NotifyAuthorizationStatusChangedAsync(
            int authorizationId, string notificationType, string title, string message)
        {
            _logger.LogDebug("[NotificationService] NotifyAuthorizationStatusChangedAsync: AuthId={AuthId}, Type={Type}", authorizationId, notificationType);

            var userId = await _repo.GetUserIdByAuthorizationIdAsync(authorizationId);
            if (userId == null)
            {
                _logger.LogWarning("[NotificationService] Authorization Status notification NOT sent: could not resolve UserId for AuthorizationId={AuthorizationId} (Type={Type})", authorizationId, notificationType);
                return;
            }

            await _repo.CreateAsync(new NotificationCreateDto
            {
                UserId           = userId.Value,
                Title            = title,
                Message          = message,
                NotificationType = notificationType,
                ReferenceId      = authorizationId,
            });

            _logger.LogInformation("[NotificationService] Authorization Status notification sent: UserId={UserId}, AuthId={AuthId}, Type={Type}, Title={Title}",
                userId.Value, authorizationId, notificationType, title);
        }

        public async Task NotifyApprovalStatusChangedAsync(
            int approvalId, string notificationType, string title, string message)
        {
            _logger.LogDebug("[NotificationService] NotifyApprovalStatusChangedAsync: ApprovalId={ApprovalId}, Type={Type}", approvalId, notificationType);

            var userId = await _repo.GetUserIdByApprovalIdAsync(approvalId);
            if (userId == null)
            {
                _logger.LogWarning("[NotificationService] Request Status notification NOT sent: could not resolve UserId for ApprovalId={ApprovalId} (Type={Type}) — employee may not have a linked User account with EmpId set", approvalId, notificationType);
                return;
            }

            await _repo.CreateAsync(new NotificationCreateDto
            {
                UserId           = userId.Value,
                Title            = title,
                Message          = message,
                NotificationType = notificationType,
                ReferenceId      = approvalId,
            });

            _logger.LogInformation("[NotificationService] Request Status notification sent: UserId={UserId}, ApprovalId={ApprovalId}, Type={Type}, Title={Title}",
                userId.Value, approvalId, notificationType, title);
        }

        public Task<int?> GetRecentPortalReqIdByApprovalIdAsync(int approvalId)
            => _repo.GetRecentPortalReqIdByApprovalIdAsync(approvalId);

        public Task<string?> GetSetCodeByReqIdAsync(int reqId)
            => _repo.GetSetCodeByReqIdAsync(reqId);

        public Task<string?> GetRecentPortalSetCodeByApprovalIdAsync(int approvalId)
            => _repo.GetRecentPortalSetCodeByApprovalIdAsync(approvalId);

        public Task<string?> GetSetCodeByAuthorizationIdAsync(int authorizationId)
            => _repo.GetSetCodeByAuthorizationIdAsync(authorizationId);

        public Task<List<NotificationDto>> GetNotificationsForUserAsync(int userId, int limit = 50)
            => _repo.GetByUserAsync(userId, limit);

        public Task<int> GetUnreadCountAsync(int userId)
            => _repo.GetUnreadCountAsync(userId);

        public Task MarkAsReadAsync(int notificationId, int userId)
            => _repo.MarkAsReadAsync(notificationId, userId);

        public Task MarkAllAsReadAsync(int userId)
            => _repo.MarkAllAsReadAsync(userId);

        public Task ClearAllAsync(int userId)
            => _repo.ClearAllAsync(userId);
    }
}
