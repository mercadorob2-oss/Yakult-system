using System.Collections.Generic;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    public interface IRepairTicketRepository
    {
        // Tickets
        Task<List<RepairTicketListItem>> GetTicketsAsync(RepairTicketFilterCriteria criteria);
        Task<RepairPortalSummaryCounts> GetSummaryCountsAsync();
        Task<List<string>> GetDistinctTicketCategoriesAsync();
        Task<RepairTicketDetail> GetTicketDetailAsync(int repairTicketId);
        Task<RepairTicketAttachmentItem> GetAttachmentAsync(int attachmentId);
        Task<RepairTicketListItem> CreateTicketAsync(NewRepairTicketRequest request, int? createdByUserId);
        Task<RepairTicketListItem> CreateTicketFromCallTicketAsync(NewRepairTicketRequest request, int? createdByUserId);
        Task LinkNewItemToRequestedBySetAsync(NewRepairTicketRequest request, string ticketCode, int createdByUserId);
        Task SetStatusAsync(int repairTicketId, string newStatus, int? changedByUserId, string note, System.DateTime? completedAtOverride = null, bool skipConditionReset = false);
        Task DeleteTicketAsync(int repairTicketId);
        Task SetPriorityAsync(int repairTicketId, string newPriority, int? changedByUserId);
        Task AddNoteAsync(int repairTicketId, string noteText, string noteType, int? createdByUserId, int? createdByEmpId);
        Task<int> AddAttachmentAsync(int repairTicketId, string attachmentType, string fileName, string mimeType, byte[] bytes, int? uploadedByUserId);
        Task DeleteAttachmentAsync(int attachmentId, int? changedByUserId);
        Task<RepairReportData> GetReportDataAsync(int repairTicketId, string generatedByName);
        Task<byte[]> GetFullAttachmentBytesAsync(int attachmentId, bool isPartAttachment);
        Task UpgradeAttachmentForPrintAsync(RepairReportAttachmentRow row);
        Task<(int updated, int failed)> RegenerateAttachmentThumbnailsAsync(System.IProgress<string> progress = null);
        Task RecordReportSignatureAsync(int repairTicketId, string roleName, string employeeName, string title, System.DateTime? signedDate, int? recordedByUserId);
        Task<Dictionary<string, (string name, string title, System.DateTime? date)>> GetLatestReportSignaturesAsync(int repairTicketId);
        Task<List<RepairTicketHistoryItem>> GetHistoryAsync(int repairTicketId);
        Task UpdateRequestedByAsync(int repairTicketId, string requestedByType, int? deptId, int? empId, int? changedByUserId, int? comId = null, int? branchId = null);

        // Attendance
        Task<RepairTechnicianAttendanceStatus> GetTodayStatusAsync(int employeeId);
        Task<RepairTechnicianAttendanceStatus> TimeInAsync(int employeeId);
        Task<RepairTechnicianAttendanceStatus> TimeOutAsync(int employeeId);
        Task<System.DateTime?> GetLastTimeOutAsync(int employeeId);
        Task<List<RepairTechnicianAttendanceStatus>> GetAttendanceHistoryAsync(int employeeId);

        // Lookups
        Task<List<RepairableItemLookup>> SearchRepairableItemsAsync(string query, string scope = "Both");
        Task<List<RepairableItemLookup>> GetCandidateRepairItemsAsync(string scope = "Both", int top = 50);
        Task<List<OrgLookupOption>> GetCompanyOptionsAsync();
        Task<List<OrgLookupOption>> GetBranchOptionsAsync();
        Task<List<OrgLookupOption>> GetDepartmentOptionsAsync();
        Task<List<OrgLookupOption>> GetTechnicianOptionsAsync();
        Task<List<OrgLookupOption>> GetItDepartmentEmployeesAsync();
        Task<List<OrgLookupOption>> GetBranchesForCompanyAsync(int comId);
        Task<List<OrgLookupOption>> GetDepartmentsForCompanyBranchAsync(int comId, int? branchId);
        Task<List<OrgLookupOption>> GetEmployeesForCompanyBranchDeptAsync(int comId, int? branchId, int? deptId);
        Task<RepairableItemLookup> GetItemLookupByIdAsync(int itemId);
        Task<RepairableItemLookupPage> SearchReplacementCandidatesAsync(string query, int excludeItemId, int pageNumber = 1, int pageSize = 25);

        // Part-based repair workflow — Observations
        Task<List<RepairItemObservation>> GetObservationsAsync(int repairTicketId);
        Task AddObservationAsync(int repairTicketId, string text, int? createdByUserId);
        Task UpdateObservationAsync(int observationId, string text);
        Task DeleteObservationAsync(int observationId);
        Task ReorderObservationsAsync(int repairTicketId, List<int> orderedObservationIds);

        // Part-based repair workflow — Parts
        Task<List<RepairPart>> GetPartsAsync(int repairTicketId);
        Task<RepairPartDetail> GetPartDetailAsync(int repairPartId);
        Task<RepairPart> CreatePartAsync(NewPartRequest request, int? createdByUserId);
        Task SetPartStatusAsync(int repairPartId, string newStatus, int? changedByUserId, string note, System.DateTime? completedDate = null);
        Task UpdatePartLabelAsync(int repairPartId, string newLabel, int? changedByUserId);
        Task DeletePartAsync(int repairPartId, int? changedByUserId);
        Task AddPartNoteAsync(int repairPartId, string noteType, string noteText, int? createdByUserId, int? createdByEmpId);
        Task<int> AddPartAttachmentAsync(int repairPartId, string attachmentType, string fileName, string mimeType, byte[] bytes, int? uploadedByUserId);
        Task DeletePartAttachmentAsync(int partAttachmentId, int? changedByUserId);
        Task<RepairPartAttachmentItem> GetPartAttachmentAsync(int partAttachmentId);

        // Part-based repair workflow — Conclusion & aggregate gallery
        Task<RepairConclusion> GetConclusionAsync(int repairTicketId);
        Task SaveConclusionAsync(RepairConclusion conclusion);
        Task<List<AttachmentGalleryItem>> GetEvidenceGalleryAsync(int repairTicketId);
        Task<long> GetTotalEvidenceSizeBytesAsync(int repairTicketId);

        // Unrepairable disposition (Discard / Replace)
        Task SetDispositionAsync(int repairTicketId, string disposition, int? replacementItemId, int decidedByUserId, string decidedByDisplayName);
        Task ClearDispositionAsync(int repairTicketId, int decidedByUserId, string decidedByDisplayName);

        // Spare item loaners (Repairing status) — reuses dbo.BorrowLog via BorrowItemsRepository
        Task<Yakult.Inventory.App.Models.BorrowItems.BorrowLogRow> GetActiveSpareAsync(int repairTicketId);
        Task<Yakult.Inventory.App.Models.BorrowItems.BorrowLogRow> GetMostRecentSpareAsync(int repairTicketId);
        Task AssignSpareAsync(int repairTicketId, string serialNumber, int decidedByUserId);
        Task UnlinkSpareAsync(int repairTicketId, int decidedByUserId);
        Task AutoReturnSpareIfAnyAsync(int repairTicketId, int decidedByUserId);

        // QR Code (scan-to-lookup, mirrors dbo.[Set]'s QR feature)
        Task<(byte[] QRImageData, System.Guid QRToken)> GetQrInfoAsync(int repairTicketId);
        Task UpdateQRDataAsync(int repairTicketId, byte[] qrImageData, string qrDataJson);
    }
}
