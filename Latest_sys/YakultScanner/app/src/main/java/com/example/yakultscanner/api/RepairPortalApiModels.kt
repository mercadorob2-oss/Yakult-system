package com.example.yakultscanner.api

import com.google.gson.annotations.SerializedName

/** Typed contracts for the JWT-protected standalone mobile Repair Portal handlers. */

data class RepairTicketListResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("scope") val scope: String? = null,
    @SerializedName("tickets") val tickets: List<RepairTicketSummaryDto> = emptyList(),
    @SerializedName("totalCount") val totalCount: Int = 0,
    @SerializedName("page") val page: Int = 1,
    @SerializedName("pageSize") val pageSize: Int = 25,
    @SerializedName("message") val message: String? = null
)

data class CreateRepairTicketRequest(
    @SerializedName("itemId") val itemId: Int,
    @SerializedName("problem") val problem: String,
    @SerializedName("priority") val priority: String = "Medium",
    @SerializedName("requestedByType") val requestedByType: String? = null,
    @SerializedName("requestedByEmployeeId") val requestedByEmployeeId: Int? = null,
    @SerializedName("requestedByDepartmentId") val requestedByDepartmentId: Int? = null,
    @SerializedName("requestedByCompanyId") val requestedByCompanyId: Int? = null,
    @SerializedName("requestedByBranchId") val requestedByBranchId: Int? = null,
    @SerializedName("dateReceived") val dateReceived: String? = null
)

data class CreateRepairTicketResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("ticket") val ticket: RepairTicketCreatedDto? = null,
    @SerializedName("message") val message: String? = null
)

data class RepairTicketCreatedDto(
    @SerializedName("repairTicketId") val repairTicketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null
)

data class RepairTicketDetailResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("isTechnician") val isTechnician: Boolean = false,
    @SerializedName("ticket") val ticket: RepairTicketDetailDto? = null,
    @SerializedName("notes") val notes: List<RepairTicketNoteDto> = emptyList(),
    @SerializedName("history") val history: List<RepairTicketHistoryDto> = emptyList(),
    @SerializedName("observations") val observations: List<RepairObservationDto> = emptyList(),
    @SerializedName("attachments") val attachments: List<RepairAttachmentDto> = emptyList(),
    @SerializedName("parts") val parts: List<RepairPartDto> = emptyList(),
    @SerializedName("partNotes") val partNotes: List<RepairPartNoteDto> = emptyList(),
    @SerializedName("partHistory") val partHistory: List<RepairPartHistoryDto> = emptyList(),
    @SerializedName("partAttachments") val partAttachments: List<RepairPartAttachmentDto> = emptyList(),
    @SerializedName("conclusion") val conclusion: RepairConclusionDto? = null,
    @SerializedName("linkedCall") val linkedCall: LinkedCallTicketDto? = null,
    @SerializedName("activeSpare") val activeSpare: RepairSpareDto? = null,
    @SerializedName("message") val message: String? = null
)

data class RepairTicketDetailDto(
    @SerializedName("repairTicketId") val repairTicketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("itemName") val itemName: String? = null,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("setCode") val setCode: String? = null,
    @SerializedName("problem") val problem: String? = null,
    @SerializedName("diagnosis") val diagnosis: String? = null,
    @SerializedName("resolution") val resolution: String? = null,
    @SerializedName("partsUsed") val partsUsed: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("dateReceived") val dateReceived: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null,
    @SerializedName("submittedByUserId") val submittedByUserId: Int? = null,
    @SerializedName("submittedByEmployeeId") val submittedByEmployeeId: Int? = null,
    @SerializedName("submittedByName") val submittedByName: String? = null,
    @SerializedName("assignedTechnicianId") val assignedTechnicianId: Int? = null,
    @SerializedName("assignedTechnicianName") val assignedTechnicianName: String? = null,
    @SerializedName("requestedByType") val requestedByType: String? = null,
    @SerializedName("requestedByEmployeeId") val requestedByEmployeeId: Int? = null,
    @SerializedName("requestedByEmployeeName") val requestedByEmployeeName: String? = null,
    @SerializedName("requestedByDepartmentId") val requestedByDepartmentId: Int? = null,
    @SerializedName("requestedByDepartmentName") val requestedByDepartmentName: String? = null,
    @SerializedName("requestedByCompanyId") val requestedByCompanyId: Int? = null,
    @SerializedName("requestedByCompanyName") val requestedByCompanyName: String? = null,
    @SerializedName("requestedByBranchId") val requestedByBranchId: Int? = null,
    @SerializedName("requestedByBranchName") val requestedByBranchName: String? = null,
    @SerializedName("linkedCallTicketId") val linkedCallTicketId: Int? = null
)

data class RepairTicketNoteDto(
    @SerializedName("noteId") val noteId: Long = 0,
    @SerializedName("noteType") val noteType: String? = null,
    @SerializedName("noteText") val noteText: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("createdByName") val createdByName: String? = null
)

data class RepairTicketHistoryDto(
    @SerializedName("historyId") val historyId: Long = 0,
    @SerializedName("changedAt") val changedAt: String? = null,
    @SerializedName("fieldName") val fieldName: String? = null,
    @SerializedName("oldValue") val oldValue: String? = null,
    @SerializedName("newValue") val newValue: String? = null,
    @SerializedName("note") val note: String? = null,
    @SerializedName("changedByName") val changedByName: String? = null
)

data class RepairObservationDto(
    @SerializedName("observationId") val observationId: Int = 0,
    @SerializedName("sortOrder") val sortOrder: Int = 0,
    @SerializedName("text") val text: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null,
    @SerializedName("createdByName") val createdByName: String? = null
)

data class RepairAttachmentDto(
    @SerializedName("attachmentId") val attachmentId: Int = 0,
    @SerializedName("attachmentType") val attachmentType: String? = null,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("fileSizeBytes") val fileSizeBytes: Int? = null,
    @SerializedName("sortOrder") val sortOrder: Int = 0,
    @SerializedName("uploadedAt") val uploadedAt: String? = null,
    @SerializedName("uploadedByName") val uploadedByName: String? = null
)

data class RepairPartDto(
    @SerializedName("repairPartId") val repairPartId: Int = 0,
    @SerializedName("partNumber") val partNumber: Int = 0,
    @SerializedName("customLabel") val customLabel: String? = null,
    @SerializedName("displayName") val displayName: String? = null,
    @SerializedName("problemDescription") val problemDescription: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("severity") val severity: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null,
    @SerializedName("noteCount") val noteCount: Int = 0,
    @SerializedName("attachmentCount") val attachmentCount: Int = 0
)

data class RepairPartNoteDto(
    @SerializedName("partNoteId") val partNoteId: Long = 0,
    @SerializedName("repairPartId") val repairPartId: Int = 0,
    @SerializedName("noteType") val noteType: String? = null,
    @SerializedName("noteText") val noteText: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("createdByName") val createdByName: String? = null
)

data class RepairPartHistoryDto(
    @SerializedName("partHistoryId") val partHistoryId: Long = 0,
    @SerializedName("repairPartId") val repairPartId: Int = 0,
    @SerializedName("changedAt") val changedAt: String? = null,
    @SerializedName("fieldName") val fieldName: String? = null,
    @SerializedName("oldValue") val oldValue: String? = null,
    @SerializedName("newValue") val newValue: String? = null,
    @SerializedName("note") val note: String? = null,
    @SerializedName("changedByName") val changedByName: String? = null
)

data class RepairPartAttachmentDto(
    @SerializedName("partAttachmentId") val partAttachmentId: Int = 0,
    @SerializedName("repairPartId") val repairPartId: Int = 0,
    @SerializedName("attachmentType") val attachmentType: String? = null,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("fileSizeBytes") val fileSizeBytes: Int? = null,
    @SerializedName("sortOrder") val sortOrder: Int = 0,
    @SerializedName("uploadedAt") val uploadedAt: String? = null,
    @SerializedName("uploadedByName") val uploadedByName: String? = null
)

data class RepairConclusionDto(
    @SerializedName("rootCause") val rootCause: String? = null,
    @SerializedName("workPerformed") val workPerformed: String? = null,
    @SerializedName("finalOutcome") val finalOutcome: String? = null,
    @SerializedName("recommendations") val recommendations: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null,
    @SerializedName("completedByName") val completedByName: String? = null,
    @SerializedName("handedOverToVendorId") val handedOverToVendorId: Int? = null,
    @SerializedName("handedOverToVendorName") val handedOverToVendorName: String? = null,
    @SerializedName("disposition") val disposition: String? = null,
    @SerializedName("replacementItemId") val replacementItemId: Int? = null,
    @SerializedName("repairedBy") val repairedBy: List<RepairLookupItemDto> = emptyList()
)

data class LinkedCallTicketDto(
    @SerializedName("ticketId") val ticketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("issue") val issue: String? = null,
    @SerializedName("callerName") val callerName: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("isForwardedToRepair") val isForwardedToRepair: Boolean = false
)

data class RepairSpareDto(
    @SerializedName("borrowId") val borrowId: Int = 0,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("itemName") val itemName: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("borrowedAt") val borrowedAt: String? = null,
    @SerializedName("borrowerName") val borrowerName: String? = null
)

data class RepairTicketActionRequest(
    @SerializedName("action") val action: String,
    @SerializedName("ticketId") val ticketId: Int? = null,
    @SerializedName("noteText") val noteText: String? = null,
    @SerializedName("noteType") val noteType: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("note") val note: String? = null,
    @SerializedName("text") val text: String? = null,
    @SerializedName("repairPartId") val repairPartId: Int? = null,
    @SerializedName("label") val label: String? = null,
    @SerializedName("problemDescription") val problemDescription: String? = null,
    @SerializedName("severity") val severity: String? = null,
    @SerializedName("rootCause") val rootCause: String? = null,
    @SerializedName("workPerformed") val workPerformed: String? = null,
    @SerializedName("finalOutcome") val finalOutcome: String? = null,
    @SerializedName("recommendations") val recommendations: String? = null,
    @SerializedName("handedOverToVendorId") val handedOverToVendorId: Int? = null,
    @SerializedName("repairedByEmployeeIds") val repairedByEmployeeIds: List<Int>? = null,
    @SerializedName("requestedByType") val requestedByType: String? = null,
    @SerializedName("requestedByEmployeeId") val requestedByEmployeeId: Int? = null,
    @SerializedName("requestedByDepartmentId") val requestedByDepartmentId: Int? = null,
    @SerializedName("requestedByCompanyId") val requestedByCompanyId: Int? = null,
    @SerializedName("requestedByBranchId") val requestedByBranchId: Int? = null,
    @SerializedName("observationId") val observationId: Int? = null,
    @SerializedName("orderedObservationIds") val orderedObservationIds: List<Int>? = null,
    @SerializedName("attachmentId") val attachmentId: Int? = null,
    @SerializedName("partAttachmentId") val partAttachmentId: Int? = null,
    @SerializedName("disposition") val disposition: String? = null,
    @SerializedName("replacementItemId") val replacementItemId: Int? = null,
    @SerializedName("spareItemId") val spareItemId: Int? = null
)

data class RepairActionResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("message") val message: String? = null
)

data class RepairLookupItemDto(
    @SerializedName("id") val id: Int = 0,
    @SerializedName("employeeId") val employeeId: Int? = null,
    @SerializedName("name") val name: String = "",
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("stockOnHand") val stockOnHand: Int? = null,
    @SerializedName("conditionId") val conditionId: Int? = null
) {
    val displayText: String
        get() = listOfNotNull(name.takeIf { it.isNotBlank() }, modelNumber?.takeIf { it.isNotBlank() }, serialNumber?.takeIf { it.isNotBlank() }?.let { "S/N $it" }).joinToString(" • ")
}

data class RepairLookupResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("items") val items: List<RepairLookupItemDto> = emptyList(),
    @SerializedName("message") val message: String? = null
)

data class RepairForwardItemDto(
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("displayText") val displayText: String? = null,
    @SerializedName("name") val name: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("conditionId") val conditionId: Int? = null,
    @SerializedName("stockOnHand") val stockOnHand: Int? = null
)

data class RepairForwardLinkedTicketDto(
    @SerializedName("repairTicketId") val repairTicketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("status") val status: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null
)

data class RepairForwardItemsResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("items") val items: List<RepairForwardItemDto> = emptyList(),
    @SerializedName("existingRepairTicket") val existingRepairTicket: RepairForwardLinkedTicketDto? = null,
    @SerializedName("message") val message: String? = null
)

data class RepairForwardRequest(
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("resolutionType") val resolutionType: String,
    @SerializedName("remarks") val remarks: String? = null,
    @SerializedName("repairItemId") val repairItemId: Int? = null,
    @SerializedName("oldItemId") val oldItemId: Int? = null,
    @SerializedName("useUnlistedOldItem") val useUnlistedOldItem: Boolean = false,
    @SerializedName("unlistedOldItemName") val unlistedOldItemName: String? = null,
    @SerializedName("unlistedOldItemDescription") val unlistedOldItemDescription: String? = null,
    @SerializedName("unlistedOldItemCategoryId") val unlistedOldItemCategoryId: Int? = null,
    @SerializedName("unlistedOldItemCategoryName") val unlistedOldItemCategoryName: String? = null,
    @SerializedName("unlistedOldItemSerialNumber") val unlistedOldItemSerialNumber: String? = null,
    @SerializedName("unlistedOldItemModelNumber") val unlistedOldItemModelNumber: String? = null,
    @SerializedName("unlistedOldItemUnitOfMeasure") val unlistedOldItemUnitOfMeasure: String? = null,
    @SerializedName("oldItemConditionId") val oldItemConditionId: Int? = null,
    @SerializedName("oldItemConditionRemarks") val oldItemConditionRemarks: String? = null
)

data class RepairForwardPreviewDto(
    @SerializedName("callTicketId") val callTicketId: Int = 0,
    @SerializedName("callTicketCode") val callTicketCode: String? = null,
    @SerializedName("repairItem") val repairItem: RepairForwardPreviewItemDto? = null,
    @SerializedName("problem") val problem: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("submittedByEmployeeId") val submittedByEmployeeId: Int? = null,
    @SerializedName("requestedByType") val requestedByType: String? = null,
    @SerializedName("requestedByEmployeeId") val requestedByEmployeeId: Int? = null,
    @SerializedName("requestedByDepartmentId") val requestedByDepartmentId: Int? = null,
    @SerializedName("requestedByCompanyId") val requestedByCompanyId: Int? = null,
    @SerializedName("requestedByBranchId") val requestedByBranchId: Int? = null,
    @SerializedName("forwardedAfter") val forwardedAfter: String? = null,
    @SerializedName("resolutionRemarks") val resolutionRemarks: String? = null
)

data class RepairForwardPreviewItemDto(
    @SerializedName("itemId") val itemId: Int? = null,
    @SerializedName("name") val name: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("isUnlisted") val isUnlisted: Boolean = false
)

data class RepairForwardPreviewResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("existingRepairTicket") val existingRepairTicket: RepairForwardLinkedTicketDto? = null,
    @SerializedName("preview") val preview: RepairForwardPreviewDto? = null,
    @SerializedName("message") val message: String? = null
)

data class RepairForwardCreateResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("created") val created: Boolean = false,
    @SerializedName("ticket") val ticket: RepairForwardLinkedTicketDto? = null,
    @SerializedName("oldItemId") val oldItemId: Int? = null,
    @SerializedName("parentOutcomeRequired") val parentOutcomeRequired: Boolean = false,
    @SerializedName("message") val message: String? = null
)

data class RepairTicketEvidenceUploadRequest(
    @SerializedName("repairTicketId") val repairTicketId: Int,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("mimeType") val mimeType: String? = null,
    @SerializedName("fileBase64") val fileBase64: String
)

data class RepairTicketEvidenceUploadResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("attachmentId") val attachmentId: Int? = null,
    @SerializedName("evidenceBytesUsed") val evidenceBytesUsed: Long? = null,
    @SerializedName("evidenceBytesLimit") val evidenceBytesLimit: Long? = null,
    @SerializedName("message") val message: String? = null
)


data class RepairAttendanceDto(
    @SerializedName("attendanceId") val attendanceId: Int? = null,
    @SerializedName("employeeId") val employeeId: Int? = null,
    @SerializedName("workDate") val workDate: String? = null,
    @SerializedName("timeIn") val timeIn: String? = null,
    @SerializedName("timeOut") val timeOut: String? = null
)

data class RepairAttendanceResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("today") val today: RepairAttendanceDto? = null,
    @SerializedName("history") val history: List<RepairAttendanceDto> = emptyList(),
    @SerializedName("message") val message: String? = null
)

data class RepairTicketQrResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("ticketId") val ticketId: Int = 0,
    @SerializedName("qrToken") val qrToken: String? = null,
    @SerializedName("qrData") val qrData: String? = null,
    @SerializedName("hasImage") val hasImage: Boolean = false,
    @SerializedName("imageBase64") val imageBase64: String? = null,
    @SerializedName("imageOmittedForSize") val imageOmittedForSize: Boolean = false,
    @SerializedName("message") val message: String? = null
)

// ── Repair Reports (Option B) — aggregated for Reports tab, all authenticated users ──

data class RepairReportsSummaryDto(
    @SerializedName("waiting") val waiting: Int = 0,
    @SerializedName("diagnosing") val diagnosing: Int = 0,
    @SerializedName("repairing") val repairing: Int = 0,
    @SerializedName("awaitingParts") val awaitingParts: Int = 0,
    @SerializedName("testing") val testing: Int = 0,
    @SerializedName("completed") val completed: Int = 0,
    @SerializedName("unrepairable") val unrepairable: Int = 0,
    @SerializedName("total") val total: Int = 0
)

data class RepairReportsResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("isTechnician") val isTechnician: Boolean = false,
    @SerializedName("today") val today: RepairAttendanceDto? = null,
    @SerializedName("history") val history: List<RepairAttendanceDto> = emptyList(),
    @SerializedName("summary") val summary: RepairReportsSummaryDto? = null,
    @SerializedName("mySummary") val mySummary: RepairReportsSummaryDto? = null,
    @SerializedName("recentTickets") val recentTickets: List<RepairTicketSummaryDto> = emptyList(),
    @SerializedName("recentCount") val recentCount: Int = 0,
    @SerializedName("message") val message: String? = null
)

// ── Report Signatures & Preview (mobile parity with desktop RepairSignatoryPicker) ──
data class RepairReportSignatureDto(
    @SerializedName("roleName") val roleName: String? = null,
    @SerializedName("employeeName") val employeeName: String? = null,
    @SerializedName("title") val title: String? = null,
    @SerializedName("signedDate") val signedDate: String? = null,
    @SerializedName("recordedAt") val recordedAt: String? = null,
    @SerializedName("recordedByUserId") val recordedByUserId: Int? = null
)

data class RepairReportSignaturesResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("ticketId") val ticketId: Int = 0,
    @SerializedName("signatures") val signatures: List<RepairReportSignatureDto> = emptyList(),
    @SerializedName("history") val history: List<RepairReportSignatureDto> = emptyList(),
    @SerializedName("message") val message: String? = null
)

data class RepairReportSignatureRequest(
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("roleName") val roleName: String,
    @SerializedName("employeeName") val employeeName: String,
    @SerializedName("title") val title: String? = null,
    @SerializedName("signedDate") val signedDate: String? = null
)

data class RepairReportPreviewAttachmentDto(
    @SerializedName("attachmentId") val attachmentId: Int = 0,
    @SerializedName("fileName") val fileName: String? = null,
    @SerializedName("attachmentType") val attachmentType: String? = null,
    @SerializedName("fileSizeBytes") val fileSizeBytes: Int? = null,
    @SerializedName("source") val source: String? = null,
    @SerializedName("hasImage") val hasImage: Boolean = false
)

data class RepairReportPreviewPartDto(
    @SerializedName("displayName") val displayName: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("severity") val severity: String? = null
)

data class RepairReportPreviewConclusionDto(
    @SerializedName("rootCause") val rootCause: String? = null,
    @SerializedName("workPerformed") val workPerformed: String? = null,
    @SerializedName("finalOutcome") val finalOutcome: String? = null,
    @SerializedName("recommendations") val recommendations: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null,
    @SerializedName("disposition") val disposition: String? = null,
    @SerializedName("replacementItemId") val replacementItemId: Int? = null,
    @SerializedName("handedOverToVendorId") val handedOverToVendorId: Int? = null
)

data class RepairReportPreviewTicketDto(
    @SerializedName("repairTicketId") val repairTicketId: Int = 0,
    @SerializedName("ticketCode") val ticketCode: String? = null,
    @SerializedName("itemId") val itemId: Int = 0,
    @SerializedName("itemName") val itemName: String? = null,
    @SerializedName("serialNumber") val serialNumber: String? = null,
    @SerializedName("modelNumber") val modelNumber: String? = null,
    @SerializedName("category") val category: String? = null,
    @SerializedName("setCode") val setCode: String? = null,
    @SerializedName("problem") val problem: String? = null,
    @SerializedName("priority") val priority: String? = null,
    @SerializedName("status") val status: String? = null,
    @SerializedName("dateReceived") val dateReceived: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("updatedAt") val updatedAt: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null,
    @SerializedName("assignedTechnicianName") val assignedTechnicianName: String? = null,
    @SerializedName("requestedByDepartmentName") val requestedByDepartmentName: String? = null,
    @SerializedName("requestedByCompanyName") val requestedByCompanyName: String? = null,
    @SerializedName("requestedByBranchName") val requestedByBranchName: String? = null,
    @SerializedName("requesterLabel") val requesterLabel: String? = null,
    @SerializedName("requestedByDeptId") val requestedByDeptId: Int? = null,
    @SerializedName("observations") val observations: List<String> = emptyList(),
    @SerializedName("problemText") val problemText: String? = null,
    @SerializedName("assetFields") val assetFields: List<String> = emptyList(),
    @SerializedName("parts") val parts: List<RepairReportPreviewPartDto> = emptyList(),
    @SerializedName("conclusion") val conclusion: RepairReportPreviewConclusionDto? = null,
    @SerializedName("attachments") val attachments: List<RepairReportPreviewAttachmentDto> = emptyList(),
    @SerializedName("hasReportedProblem") val hasReportedProblem: Boolean = false,
    @SerializedName("hasParts") val hasParts: Boolean = false,
    @SerializedName("hasDiagnosis") val hasDiagnosis: Boolean = false,
    @SerializedName("hasWorkPerformed") val hasWorkPerformed: Boolean = false,
    @SerializedName("hasRecommendations") val hasRecommendations: Boolean = false,
    @SerializedName("hasAttachments") val hasAttachments: Boolean = false
)

data class RepairReportGroupDto(
    @SerializedName("groupLabel") val groupLabel: String? = null,
    @SerializedName("ticketCount") val ticketCount: Int = 0,
    @SerializedName("ticketCodes") val ticketCodes: List<String> = emptyList(),
    @SerializedName("tickets") val tickets: List<RepairReportPreviewTicketDto> = emptyList()
)

data class RepairReportPreviewResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("isTechnician") val isTechnician: Boolean = false,
    @SerializedName("generatedByName") val generatedByName: String? = null,
    @SerializedName("generatedAt") val generatedAt: String? = null,
    @SerializedName("ticketCount") val ticketCount: Int = 0,
    @SerializedName("groupCount") val groupCount: Int = 0,
    @SerializedName("groups") val groups: List<RepairReportGroupDto> = emptyList(),
    @SerializedName("reports") val reports: List<RepairReportPreviewTicketDto> = emptyList(),
    @SerializedName("message") val message: String? = null
)

data class RepairReportPreviewRequest(
    @SerializedName("ticketIds") val ticketIds: List<Int>,
    @SerializedName("includeAttachments") val includeAttachments: Boolean = true,
    @SerializedName("recordSignatures") val recordSignatures: Boolean = true,
    @SerializedName("reviewedBy") val reviewedBy: RepairReportSignatureDto? = null,
    @SerializedName("receivedBy") val receivedBy: RepairReportSignatureDto? = null
)
