package com.example.yakultscanner.data.repository

import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.CallLookupResponse
import com.example.yakultscanner.api.EscalationSettingsResponse
import com.example.yakultscanner.api.SetEscalationOverrideRequest
import com.example.yakultscanner.api.SetEscalationOverrideResponse
import com.example.yakultscanner.api.CallTicketActionRequest
import com.example.yakultscanner.api.CallTicketActionResponse
import com.example.yakultscanner.api.CallTicketDetailResponse
import com.example.yakultscanner.api.CallTicketListItem
import com.example.yakultscanner.api.CallTicketListResponse
import com.example.yakultscanner.api.CallTicketCreateResponse
import com.example.yakultscanner.api.CreateTicketRequest
import com.example.yakultscanner.api.CallDashboardResponse
import com.example.yakultscanner.api.CallItemLookupResponse
import com.example.yakultscanner.api.CallConditionResponse
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.ResolutionRequest
import com.example.yakultscanner.api.RepairForwardCreateResponse
import com.example.yakultscanner.api.RepairForwardPreviewResponse
import com.example.yakultscanner.api.RepairForwardRequest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import retrofit2.Response
import javax.inject.Singleton

@Singleton
class CallMonitoringRepository {

    private val apiService
        get() = ApiClient.service

    suspend fun getCallTickets(
        status: String? = null,
        scope: String? = null,
        search: String? = null,
        priority: String? = null,
        issueType: String? = null,
        page: Int = 1,
        pageSize: Int = 25
    ): Response<CallTicketListResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallTickets(
                status = status,
                scope = scope,
                search = search,
                priority = priority,
                issueType = issueType,
                page = page,
                pageSize = pageSize
            )
        }
    }

    suspend fun getTicketDetail(ticketId: Int): Response<CallTicketDetailResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallTicketDetail(ticketId)
        }
    }

    suspend fun createTicket(request: CreateTicketRequest): Response<CallTicketCreateResponse> {
        return withContext(Dispatchers.IO) {
            apiService.createCallTicket(request)
        }
    }

    suspend fun addNote(
        ticketId: Int,
        noteText: String,
        noteType: String = "Internal",
        userId: Int? = null
    ): Response<CallTicketActionResponse> {
        val request = CallTicketActionRequest(
            action = "note",
            ticketId = ticketId,
            noteText = noteText,
            noteType = noteType,
            userId = userId
        )
        return withContext(Dispatchers.IO) {
            apiService.callTicketAction(request)
        }
    }

    suspend fun updateStatus(
        ticketId: Int,
        newStatus: String,
        note: String? = null,
        userId: Int? = null
    ): Response<CallTicketActionResponse> {
        val request = CallTicketActionRequest(
            action = "status",
            ticketId = ticketId,
            newStatus = newStatus,
            note = note,
            userId = userId
        )
        return withContext(Dispatchers.IO) {
            apiService.callTicketAction(request)
        }
    }

    suspend fun updatePriority(
        ticketId: Int,
        newPriority: String,
        userId: Int? = null
    ): Response<CallTicketActionResponse> {
        val request = CallTicketActionRequest(
            action = "priority",
            ticketId = ticketId,
            newPriority = newPriority,
            userId = userId
        )
        return withContext(Dispatchers.IO) {
            apiService.callTicketAction(request)
        }
    }

    /**
     * Assign (or unassign when [assignedToEmpId] is null) a ticket.
     * Mirrors desktop AssignTicketAndNotifyAsync; the server validates that a
     * non-null id references an active IT employee and blocks final tickets.
     */
    suspend fun assignTicket(
        ticketId: Int,
        assignedToEmpId: Int? = null,
        userId: Int? = null
    ): Response<CallTicketActionResponse> {
        val request = CallTicketActionRequest(
            action = "assign",
            ticketId = ticketId,
            assignedToEmpId = assignedToEmpId,
            userId = userId
        )
        return withContext(Dispatchers.IO) {
            apiService.callTicketAction(request)
        }
    }

    suspend fun getCompanies(): Response<CallLookupResponse> =
        withContext(Dispatchers.IO) { apiService.getCallCompanies() }

    suspend fun getDepartments(comId: Int? = null): Response<CallLookupResponse> =
        withContext(Dispatchers.IO) { apiService.getCallDepartments(comId) }

    suspend fun getBranches(comId: Int? = null, deptId: Int? = null): Response<CallLookupResponse> =
        withContext(Dispatchers.IO) { apiService.getCallBranches(comId, deptId) }

    suspend fun getEmployees(deptId: Int, comId: Int? = null, branchId: Int? = null): Response<CallLookupResponse> =
        withContext(Dispatchers.IO) { apiService.getCallEmployees(deptId, comId, branchId) }

    suspend fun searchEmployees(query: String, maxResults: Int = 20): Response<CallLookupResponse> =
        withContext(Dispatchers.IO) { apiService.searchCallEmployees(query.trim(), maxResults) }

    suspend fun getEmployeeOrg(empId: Int): Response<com.example.yakultscanner.api.EmployeeOrgResponse> =
        withContext(Dispatchers.IO) { apiService.getCallEmployeeOrg(empId) }

    suspend fun getItEmployees(): Response<CallLookupResponse> =
        withContext(Dispatchers.IO) { apiService.getCallItEmployees() }

    suspend fun getEscalationSettings(): Response<EscalationSettingsResponse> =
        withContext(Dispatchers.IO) { apiService.getEscalationSettings() }

    suspend fun setEscalationOverride(request: SetEscalationOverrideRequest): Response<SetEscalationOverrideResponse> =
        withContext(Dispatchers.IO) { apiService.setEscalationOverride(request) }

    suspend fun applyResolution(request: ResolutionRequest): Response<CallTicketActionResponse> {
        return withContext(Dispatchers.IO) {
            apiService.callTicketResolution(request)
        }
    }

    suspend fun previewRepairForward(request: RepairForwardRequest): Response<RepairForwardPreviewResponse> {
        return withContext(Dispatchers.IO) { apiService.previewRepairForward(request) }
    }

    suspend fun createRepairForward(request: RepairForwardRequest): Response<RepairForwardCreateResponse> {
        return withContext(Dispatchers.IO) { apiService.createRepairForward(request) }
    }

    suspend fun getCallItemsLookup(type: String): Response<CallItemLookupResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallItemsLookup(type)
        }
    }

    suspend fun getCallConditions(): Response<CallConditionResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallConditions()
        }
    }

    suspend fun getDashboard(): Response<CallDashboardResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallDashboard()
        }
    }

    suspend fun getItemCategories(): Response<List<ItemCategoryDto>> {
        return withContext(Dispatchers.IO) {
            apiService.getItemCategories()
        }
    }

    // ── Field Work (one per ticket, no GPS) ──
    suspend fun getFieldVisits(ticketId: Int): Response<com.example.yakultscanner.api.CallFieldVisitsResponse> = withContext(Dispatchers.IO) {
        apiService.getCallFieldVisits(ticketId)
    }

    suspend fun scheduleFieldVisit(request: com.example.yakultscanner.api.ScheduleFieldVisitRequest): Response<com.example.yakultscanner.api.CallFieldVisitsResponse> = withContext(Dispatchers.IO) {
        apiService.scheduleCallFieldVisit(request)
    }

    suspend fun fieldVisitAction(request: com.example.yakultscanner.api.FieldVisitActionRequest): Response<com.example.yakultscanner.api.CallFieldVisitsResponse> = withContext(Dispatchers.IO) {
        apiService.fieldVisitAction(request)
    }

    suspend fun uploadFieldVisitPhoto(request: com.example.yakultscanner.api.FieldVisitPhotoUploadRequest): Response<com.example.yakultscanner.api.CallFieldVisitsResponse> = withContext(Dispatchers.IO) {
        apiService.uploadFieldVisitPhoto(request)
    }

    suspend fun getFieldVisitPhoto(attachmentId: Int, thumb: Boolean = true): Response<com.example.yakultscanner.api.CallFieldVisitPhotoResponse> = withContext(Dispatchers.IO) {
        apiService.getFieldVisitPhoto(attachmentId, if (thumb) 1 else null)
    }

    suspend fun getFieldVisitSignature(fieldVisitId: Int): Response<com.example.yakultscanner.api.CallFieldVisitSignatureResponse> = withContext(Dispatchers.IO) {
        apiService.getFieldVisitSignature(fieldVisitId)
    }

    // ── Sign-Off bundle (ticket PDF mirrors the desktop Sign-Off report) ──
    // Thumbnails preferred with full-size fallback, capped so one export
    // never fans out into dozens of image downloads. Best-effort throughout:
    // a missing photo/signature degrades that section, never the whole bundle.
    suspend fun loadSignOffBundle(
        ticketId: Int,
        maxThumbs: Int = com.example.yakultscanner.utils.SignOffParity.BUNDLE_MAX_THUMBS
    ): com.example.yakultscanner.utils.TicketSignOffBundle = withContext(Dispatchers.IO) {
        var visits: List<com.example.yakultscanner.api.CallFieldVisitDto> = emptyList()
        var attachments: List<com.example.yakultscanner.api.CallFieldVisitAttachmentDto> = emptyList()
        try {
            val resp = apiService.getCallFieldVisits(ticketId)
            if (resp.isSuccessful) {
                visits = resp.body()?.visits.orEmpty()
                attachments = resp.body()?.attachments.orEmpty()
            }
        } catch (_: Exception) {
        }
        val thumbs = mutableListOf<com.example.yakultscanner.utils.SignOffPhotoBytes>()
        for (a in attachments.take(maxThumbs)) {
            try {
                val b64 = apiService.getFieldVisitPhoto(a.attachmentId, 1).body()?.base64
                    ?: apiService.getFieldVisitPhoto(a.attachmentId, null).body()?.base64
                if (!b64.isNullOrBlank()) {
                    thumbs += com.example.yakultscanner.utils.SignOffPhotoBytes(
                        a.fileName, a.uploadedByName, a.uploadedAt, b64
                    )
                }
            } catch (_: Exception) {
            }
        }
        val current = visits.firstOrNull()
        var signature: String? = null
        if (current != null && current.hasSignature) {
            try {
                signature = apiService.getFieldVisitSignature(current.fieldVisitId)
                    .body()?.base64?.takeIf { it.isNotBlank() }
            } catch (_: Exception) {
            }
        }
        com.example.yakultscanner.utils.TicketSignOffBundle(
            visits = visits,
            totalPhotoCount = attachments.size,
            thumbs = thumbs,
            signatureBytes = signature,
            currentVisit = current
        )
    }
}
