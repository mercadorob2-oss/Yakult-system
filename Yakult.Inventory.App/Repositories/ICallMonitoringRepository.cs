using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Defines all data access operations for the Call Monitoring module.
    /// Enables unit testing, mocking, and safer refactoring by decoupling
    /// consumers from the concrete <see cref="CallMonitoringRepository"/>.
    /// </summary>
    public interface ICallMonitoringRepository
    {
        // --- Properties ---

        string ConnectionString { get; }

        // --- Schema / Feature Flags ---

        Task<bool> CallSchemaExistsAsync();
        Task<bool> EmployeeAssignmentEnabledAsync();
        Task<bool> AssignmentEligibilitySettingsEnabledAsync();
        Task<bool> EscalationSettingsEnabledAsync();
        Task<bool> EscalationOverridesEnabledAsync();
        Task<bool> EmailNotificationSchemaExistsAsync();
        Task<bool> DepartmentSmtpProfileSchemaExistsAsync();
        Task<bool> EmailLogSchemaExistsAsync();
        Task<bool> AutoEscalationAssigneeSettingsEnabledAsync();
        Task<bool> DepartmentSmtpProfilesOptionBEnabledAsync();
        Task<bool> BranchSmtpProfilesOptionBEnabledAsync();
        Task<bool> BranchSmtpProfileLinkIsConfiguredAsync(int branchId);
        Task<bool> DepartmentSmtpProfileLinkIsConfiguredAsync(int deptId);
        Task<bool> DepartmentLegacySmtpProfileIsConfiguredAsync(int deptId);
        Task<bool> DepartmentNotificationRecipientsSchemaExistsAsync();
        Task<bool> BranchNotificationRecipientsSchemaExistsAsync();

        // --- Dashboard ---

        Task<CallDashboardMetrics> GetDashboardMetricsAsync();
        Task<List<CallTicketVolumePoint>> GetTicketVolumeByDayAsync(DateTime fromUtc, DateTime toUtcExclusive);
        Task<List<CallTicketVolumePoint>> GetTicketVolumeLast7DaysAsync();
        Task<List<CallIssueTypePoint>> GetIssueTypeDistributionAsync();
        Task<List<CallTicketListItem>> GetRequiresAttentionAsync(int maxRows = 100);
        Task<List<CallTicketListItem>> GetOpenTicketsListAsync(int maxRows = 100);
        Task<List<CallTicketListItem>> GetCriticalTicketsListAsync(int maxRows = 100);
        Task<List<CallTicketListItem>> GetTodaysTicketsListAsync(int maxRows = 100);
        Task<List<CallTicketListItem>> GetRecentlySolvedAsync(int maxRows = 50);
        Task<int> GetOverdueDaysAsync(int defaultDays = 3);

        // --- Tickets (Core) ---

        Task<CallTicketListItem> CreateTicketAsync(
            int? comId,
            int? deptId,
            int? branchId,
            string callerName,
            string issue,
            string providedSolution,
            string issueType,
            string priority,
            int? assignedToEmpId,
            int? createdByUserId,
            DateTime? createdAtUtc = null);

        Task<CallTicketListItem> GetTicketByIdAsync(int ticketId);
        Task<CallTicketNotificationData> GetTicketNotificationDataAsync(int ticketId);
        Task<List<CallTicketListItem>> GetTicketsAsync(string statusFilter, string searchText, int maxRows = 500);
        Task<List<CallTicketListItem>> GetTicketsByActiveStatusBucketAsync(string bucket, int maxRows = 500);
        Task<List<CallTicketListItem>> GetResolvedTicketsByResolutionBucketAsync(
            DateTime fromUtc,
            DateTime toUtc,
            string bucket,
            int maxRows = 250);

        // --- Incoming Portal Tickets ---

        Task<CallMonitoringRepository.CallTicketPageResult> GetIncomingPortalTicketsPageResultAsync(
            string searchText,
            string statusFilter,
            string priorityFilter,
            bool unassignedOnly,
            int minAgeDays,
            int pageIndex,
            int pageSize);

        Task<CallMonitoringRepository.IncomingTicketsSummary> GetIncomingPortalTicketsSummaryAsync();

        // --- Tickets (Paging) ---

        Task<List<CallTicketListItem>> GetPendingTicketsPageAsync(
            string statusFilter,
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int overdueDays,
            int pageIndex,
            int pageSize);

        Task<CallMonitoringRepository.CallTicketPageResult> GetPendingTicketsPageResultAsync(
            string statusFilter,
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int overdueDays,
            int pageIndex,
            int pageSize);

        Task<int> GetPendingTicketsCountAsync(
            string statusFilter,
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int overdueDays);

        Task<List<CallTicketListItem>> GetRecentlyResolvedTicketsPageAsync(
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int pageIndex,
            int pageSize);

        Task<CallMonitoringRepository.CallTicketPageResult> GetRecentlyResolvedTicketsPageResultAsync(
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int pageIndex,
            int pageSize);

        Task<int> GetRecentlyResolvedTicketsCountAsync(
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly);

        // --- Tickets (Workflow) ---

        Task SetTicketStatusAsync(int ticketId, string newStatus, int? changedByUserId, string note = null);
        Task SetTicketPriorityAsync(int ticketId, string newPriority, int? changedByUserId);
        Task AddTicketNoteAsync(int ticketId, string noteType, string noteText, int? createdByUserId);
        Task AssignTicketEmployeeAsync(int ticketId, int? assignedToEmpId, int? changedByUserId);
        Task<int?> GetTicketAssignedEmployeeIdAsync(int ticketId);

        Task<bool> DeleteTicketAsync(int ticketId, int? deletedByUserId);
        Task LogTicketResolutionAsync(int ticketId, string resolutionType, string remarks, int? changedByUserId);
        Task ApplyTicketReplacementAsync(
            int ticketId,
            int oldItemId,
            int newItemId,
            int quantity,
            string remarks,
            int? changedByUserId,
            int oldItemConditionId,
            string oldItemConditionRemarks,
            string oldItemRepairAction);

        Task<(int NewItemId, int Quantity, string NewItemText, bool AlreadyReturned)> GetTemporaryReplacementReturnPreviewAsync(int ticketId);
        Task ReturnTemporaryReplacementAsync(int ticketId, int? changedByUserId);
        Task<bool> SyncSetToLatestReplacementAsync(int ticketId, int? changedByUserId);
        Task<CallMonitoringRepository.SetSwapSyncResult> SyncSetToLatestReplacementWithResultAsync(int ticketId, int? changedByUserId);

        // --- Tickets (History / Notes) ---

        Task<List<CallTicketNoteItem>> GetTicketNotesAsync(int ticketId, int maxRows = 200);
        Task<List<CallTicketHistoryItem>> GetTicketHistoryAsync(int ticketId, int maxRows = 200);

        // --- Reminders & Escalation ---

        Task<List<int>> GetTicketIdsNeedingReminderAsync(int reminderDays, int maxRows = 25);
        Task MarkReminderSentAsync(int ticketId);
        Task<List<int>> GetTicketIdsNeedingAutoEscalationAsync(int maxRows = 25);
        Task<CallEscalationSettingsItem> GetEscalationSettingsAsync();
        Task SaveEscalationSettingsAsync(CallEscalationSettingsItem settings);
        Task<CallTicketEscalationOverrideItem> GetTicketEscalationOverrideAsync(int ticketId);
        Task<Dictionary<int, CallTicketEscalationOverrideItem>> GetEscalationOverridesForTicketsAsync(IEnumerable<int> ticketIds);
        Task SetTicketEscalationOverrideAsync(
            int ticketId,
            int? daysToSupervisor,
            int? daysToManager,
            string reason,
            int? changedByUserId,
            bool clear);
        Task SaveAutoEscalationAssigneeEmpIdAsync(int? assigneeEmpId, int? updatedByUserId);
        Task<int?> GetAutoEscalationAssigneeEmpIdAsync();

        // --- Lookups ---

        Task<string> GetEmployeePrimaryEmailAsync(int empId);
        Task<CallTicketContactEmailResolution> ResolveTicketContactEmailAsync(int? callerEmpId, int? comId, int? deptId, int? branchId);
        Task<bool> CanStoreTicketContactEmailAsync();
        Task SetTicketContactEmailAsync(int ticketId, string contactEmail);
        Task<string> GetEmployeeEmailBindingAsync(int empId);
        Task SaveEmployeeEmailBindingAsync(int empId, string email, int? changedByUserId);
        Task ClearEmployeeEmailBindingAsync(int empId);

        Task<List<LookupItem>> GetCompaniesAsync();
        Task<List<LookupItem>> GetDepartmentsAsync();
        Task<List<LookupItem>> GetDepartmentsAsync(int? comId);
        Task<List<LookupItem>> GetBranchesAsync(int? comId = null, int? deptId = null);
        Task<List<LookupItem>> GetEmployeesByDeptAndBranchAsync(int? comId, int? deptId, int? branchId);
        Task<string> GetBranchEmailAsync(int branchId);
        Task<List<LookupItem>> GetItEmployeesByDepartmentNameAsync(string itDepartmentName);
        Task<List<LookupItem>> GetCallAssignmentCandidatesByDepartmentNameAsync(string itDepartmentName);
        Task<Dictionary<int, int>> GetOpenTicketCountsByAssigneeAsync(IEnumerable<int> empIds);
        Task<List<CallAssignmentEligibilityItem>> GetCallAssignmentEligibilityAsync(string itDepartmentName);
        Task SaveCallAssignmentEligibilityAsync(IEnumerable<CallAssignmentEligibilityItem> items, int? updatedByUserId);
        Task<List<CallEmployeeProfileLookup>> GetItEmployeeProfilesAsync(string itDepartmentName);
        Task<CallEmployeeProfileLookup> GetEmployeeProfileLookupByEmpIdAsync(int empId);
        Task<CallEmployeeOrgInfo> GetEmployeeOrgInfoByEmpIdAsync(int empId);
        Task<List<LookupItem>> SearchEmployeesByNameAsync(string name, int maxResults = 20);

        // --- Reports ---

        Task<List<CallResolutionReportRow>> GetResolutionReportAsync(DateTime fromUtc, DateTime toUtc, string resolutionType, int maxRows = 2000);
        Task<List<CallSlaComplianceReportRow>> GetSlaComplianceReportAsync(DateTime fromUtc, DateTime toUtc, int maxRows = 2000);
        Task<List<CallSolvedSummaryReportRow>> GetSolvedSummaryReportAsync(DateTime fromUtc, DateTime toUtc, int maxRows = 2000);
        Task<List<CallFieldVisitReportRow>> GetFieldWorkReportAsync(DateTime fromUtc, DateTime toUtc, string statusFilter, int? technicianEmpId, int maxRows = 2000);
        Task<List<CallEmployeeReplacementItemRow>> GetEmployeeReplacementItemsAsync(
            int empId,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows = 2000);

        // --- Tech Profile / Dashboard ---

        Task<List<CallMonitoringRepository.CallTechResolutionSampleRow>> GetTechResolutionSamplesAsync(
            int empId,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows = 5000);

        Task<CallTechProfileSummary> GetTechProfileSummaryAsync(int empId, DateTime fromUtc, DateTime toUtc);
        Task<List<CallTechHandledTicketRow>> GetTechHandledTicketsAsync(
            int empId,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows = 200);

        Task<List<CallTechOpenTicketRow>> GetOpenTicketsAssignedToEmployeeAsync(int empId, int maxRows = 300);
        Task<List<(string Bucket, int TicketCount)>> GetResolutionBucketDistributionAsync(DateTime fromUtc, DateTime toUtc);
        Task<List<(string Bucket, int TicketCount)>> GetActiveTicketStatusDistributionAsync();

        // --- Email Settings & Templates ---

        Task<CallEmailSettingsItem> GetEmailSettingsAsync();
        Task SaveEmailSettingsAsync(CallEmailSettingsItem settings);
        Task<CallEmailTemplateItem> GetEmailTemplateByTypeAsync(string templateType);
        Task SaveEmailTemplateAsync(CallEmailTemplateItem template);
        Task<CallNotificationRulesItem> GetNotificationRulesAsync();
        Task SaveNotificationRulesAsync(CallNotificationRulesItem rules);

        // --- Email Log ---

        Task<List<CallEmailLogItem>> GetEmailLogAsync(int take = 250);
        Task<List<CallEmailLogItem>> GetEmailLogPageAsync(string searchText, string emailTypeFilter, int pageIndex, int pageSize);
        Task<int> GetEmailLogCountAsync(string searchText, string emailTypeFilter);
        Task<CallEmailLogItem> GetLastEmailLogForTicketAsync(int ticketId);
        Task LogEmailAsync(
            int? ticketId,
            string emailType,
            string recipient,
            string subject,
            string status,
            string errorMessage,
            int? createdByUserId);

        // --- SMTP Profiles ---

        Task<List<CallDepartmentSmtpProfileItem>> GetDepartmentSmtpProfilesAsync();
        Task<CallDepartmentSmtpProfileItem> GetDepartmentSmtpProfileByIdAsync(int profileId);
        Task SaveDepartmentSmtpProfileAsync(CallDepartmentSmtpProfileItem profile);
        Task<SmtpSenderConfig> GetSmtpSenderForDepartmentAsync(int? deptId);
        Task<CallMonitoringRepository.SmtpSenderResolution> GetSmtpSenderResolutionForDepartmentAsync(int? deptId);
        Task<SmtpSenderConfig> GetSmtpSenderForTicketAsync(int? branchId, int? deptId);
        Task<CallMonitoringRepository.SmtpSenderResolution> GetSmtpSenderResolutionForTicketAsync(int? branchId, int? deptId);
        Task<List<CallSmtpProfileItem>> GetSmtpProfilesAsync(bool activeOnly = true);
        Task<CallSmtpProfileItem> GetCallSmtpProfileByIdAsync(int profileId);
        Task SetCallSmtpProfileIsActiveAsync(int profileId, bool isActive, int? updatedByUserId);
        Task<CallMonitoringRepository.CallSmtpProfileUsage> GetCallSmtpProfileUsageAsync(int profileId);
        Task<int> UpsertSmtpProfileAsync(CallSmtpProfileItem profile);
        Task UpsertDepartmentSmtpProfileLinkAsync(int deptId, int profileId, int? updatedByUserId);
        Task UpsertBranchSmtpProfileLinkAsync(int branchId, int profileId, int? updatedByUserId);
        Task<List<CallDepartmentSmtpProfileRow>> GetDepartmentSmtpProfilesOptionBAsync();
        Task<List<CallBranchSmtpProfileRow>> GetBranchSmtpProfilesOptionBAsync();
        Task<List<CallDepartmentSmtpProfileRow>> GetDepartmentSmtpProfilesLegacyRowsAsync();

        // --- Notification Recipients ---

        Task<CallDepartmentNotificationRecipientItem> GetDepartmentNotificationRecipientAsync(int deptId);
        Task UpsertDepartmentNotificationRecipientAsync(
            int deptId,
            string recipientEmails,
            string escalationEmails,
            int? updatedByUserId);

        Task<CallBranchNotificationRecipientItem> GetBranchNotificationRecipientAsync(int branchId);
        Task UpsertBranchNotificationRecipientAsync(
            int branchId,
            string recipientEmails,
            string escalationEmails,
            int? updatedByUserId);

        // --- Diagnostics ---

        Task<CallMonitoringRepository.ItcmBackgroundLockProbe> ProbeItcmBackgroundLockAsync();
        Task<CallMonitoringRepository.ItcmDiagnosticsSnapshot> GetDiagnosticsSnapshotAsync();
        Task<List<CallMonitoringRepository.ItcmDiagnosticsTrendPoint>> LoadSnapshotTrendsAsync(CallMonitoringRepository.ItcmDiagnosticsSnapshot snapshot);
        Task<List<CallClientPresenceItem>> GetConnectedClientsAsync(int onlineMinutes = 15);

        // --- Field Work (one visit per ticket, no GPS) ---

        Task<bool> FieldWorkSchemaExistsAsync();
        Task<List<CallFieldVisitItem>> GetFieldVisitsAsync(int ticketId);
        Task<CallFieldVisitItem> GetFieldVisitAsync(int fieldVisitId);
        Task<CallFieldVisitItem> ScheduleFieldVisitAsync(int ticketId, int? technicianEmpId, DateTime? scheduledAt, string notes, int? createdByUserId);
        Task<CallFieldVisitItem> SetFieldVisitStatusAsync(int fieldVisitId, string newStatus, int? changedByUserId, string notes = null, int? technicianEmpId = null, DateTime? scheduledAt = null, DateTime? completedAt = null);
        Task<List<CallFieldVisitAttachmentItem>> GetFieldVisitAttachmentsAsync(int ticketId);
        Task<List<CallFieldVisitAttachmentItem>> GetFieldVisitAttachmentsByVisitAsync(int fieldVisitId);
        Task<int> UploadFieldVisitPhotoAsync(int fieldVisitId, string fileName, string mimeType, byte[] fileBytes, int? uploadedByUserId);
        Task SaveFieldVisitSignatureAsync(int fieldVisitId, byte[] signaturePngBytes, int? changedByUserId);
        Task<byte[]> GetFieldVisitAttachmentBytesAsync(int attachmentId);
        Task<byte[]> GetFieldVisitAttachmentThumbnailBytesAsync(int attachmentId);
        Task<byte[]> GetFieldVisitSignatureBytesAsync(int fieldVisitId);
        Task RecordTicketSignOffAsync(int ticketId, int fieldVisitId, string reportHash, string customerName, string techName, int? changedByUserId);
        Task<string> ValidateTicketSignOffAsync(int ticketId, int fieldVisitId);

        // --- Synthetic Tests ---

        Task<int?> CreateSyntheticTestTicketAsync(int? createdByUserId);
        Task<int> CleanupSyntheticTestTicketsAsync();
        Task<bool> AnySyntheticTestTicketsAsync();
    }
}
