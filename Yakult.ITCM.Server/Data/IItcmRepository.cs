using Microsoft.Data.SqlClient;
using Yakult.ITCM.Server.Models;

namespace Yakult.ITCM.Server.Data;

public interface IItcmRepository
{
    string ConnectionString { get; }

    Task<bool> AcquireGlobalLockAsync(SqlConnection connection, int timeoutMs = 0);
    Task ReleaseGlobalLockAsync(SqlConnection connection);

    Task<CallNotificationRulesItem?> GetNotificationRulesAsync();
    Task<CallEmailTemplateItem?> GetEmailTemplateByTypeAsync(string templateType);
    Task<CallTicketNotificationData?> GetTicketNotificationDataAsync(int ticketId);
    Task<string?> GetPortalTicketContactEmailAsync(int ticketId);
    Task<List<int>> GetTicketIdsNeedingReminderAsync(int reminderDays, int maxRows = 25);
    Task<string?> GetEmployeePrimaryEmailAsync(int empId);

    Task MarkReminderSentAsync(int ticketId);
    Task<List<int>> GetTicketIdsNeedingAutoEscalationAsync(int maxRows = 25);
    Task SetTicketStatusAsync(int ticketId, string newStatus, int? changedByUserId, string? note = null);
    Task AssignTicketEmployeeAsync(int ticketId, int? assignedToEmpId, int? changedByUserId);
    Task<bool> IsItEmployeeAsync(int empId);
    Task<List<ItEmployeeItem>> GetItEmployeesAsync();
    Task<int?> GetTicketIdByCodeAsync(string ticketCode);
    Task<int?> GetAutoEscalationAssigneeEmpIdAsync();
    Task<bool> EmployeeAssignmentEnabledAsync();
    Task<bool> BranchNotificationRecipientsSchemaExistsAsync();
    Task<bool> DepartmentNotificationRecipientsSchemaExistsAsync();
    Task<CallBranchNotificationRecipientItem?> GetBranchNotificationRecipientAsync(int branchId);
    Task<CallDepartmentNotificationRecipientItem?> GetDepartmentNotificationRecipientAsync(int deptId);
    Task<string?> GetBranchEmailAsync(int branchId);
    Task LogEmailAsync(int? ticketId, string emailType, string recipient, string? subject, string status, string? errorMessage, int? createdByUserId);

    Task<CallEscalationSettingsItem?> GetEscalationSettingsAsync();

    Task<SmtpSenderConfig?> GetSmtpSenderForTicketAsync(int? branchId, int? deptId);
    Task<bool> BranchSmtpProfilesOptionBEnabledAsync();
    Task<bool> DepartmentSmtpProfilesOptionBEnabledAsync();
    Task<CallSmtpProfileItem?> GetBranchSmtpProfileForTicketAsync(int branchId);
    Task<CallSmtpProfileItem?> GetDepartmentSmtpProfileLinkAsync(int deptId);
    Task<CallSmtpProfileItem?> GetDepartmentLegacySmtpProfileAsync(int deptId);
    Task<CallSmtpProfileItem?> GetBranchLegacySmtpProfileAsync(int branchId);
    Task<CallEmailSettingsItem?> GetGlobalEmailSettingsAsync();
    Task SaveEmailSettingsAsync(CallEmailSettingsItem settings);
    Task SaveNotificationRulesAsync(CallNotificationRulesItem rules);

    Task<long> WriteHeartbeatAsync(SchedulerHeartbeat heartbeat);
    Task<bool> UpsertClientPresenceAsync(string machineName, string userName, string? module, string? clientVersion);
    Task<ClientPresenceReport> GetClientPresenceAsync(int onlineMinutes = 15);
    Task<SchedulerHeartbeatSummary?> GetHeartbeatSummaryAsync();
    Task<SchedulerHeartbeatPage> GetHeartbeatPageAsync(int page, int pageSize);
    Task<MonitoringDashboard> GetMonitoringDashboardAsync(int maxRows = 20);

    Task<bool> TestConnectionAsync();
    Task<bool> TestConnectionAsync(string connectionString);
    void UpdateConnectionString(string connectionString);
    Task<List<int>> GetTicketIdsNeedingPortalSafeAutoEscalationAsync(int maxRows = 25);
    Task<bool> HasReplacementAllocationAsync(int ticketId);
}
