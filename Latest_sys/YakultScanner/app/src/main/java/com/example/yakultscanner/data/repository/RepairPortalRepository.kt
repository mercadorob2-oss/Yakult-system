package com.example.yakultscanner.data.repository

import com.example.yakultscanner.api.CreateRepairTicketRequest
import com.example.yakultscanner.api.CreateRepairTicketResponse
import com.example.yakultscanner.api.RepairActionResponse
import com.example.yakultscanner.api.RepairAttendanceResponse
import com.example.yakultscanner.api.RepairForwardCreateResponse
import com.example.yakultscanner.api.RepairForwardItemsResponse
import com.example.yakultscanner.api.RepairForwardPreviewResponse
import com.example.yakultscanner.api.RepairForwardRequest
import com.example.yakultscanner.api.RepairLookupResponse
import com.example.yakultscanner.api.RepairTicketActionRequest
import com.example.yakultscanner.api.RepairTicketDetailResponse
import com.example.yakultscanner.api.RepairTicketEvidenceUploadRequest
import com.example.yakultscanner.api.RepairTicketEvidenceUploadResponse
import com.example.yakultscanner.api.RepairTicketListResponse
import com.example.yakultscanner.api.RepairTicketQrResponse
import com.example.yakultscanner.api.YakultApiService
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.ResponseBody
import retrofit2.Response
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class RepairPortalRepository @Inject constructor(
    private val api: YakultApiService
) {
    suspend fun tickets(
        scope: String,
        status: String? = null,
        priority: String? = null,
        search: String? = null,
        page: Int = 1
    ): Response<RepairTicketListResponse> = withContext(Dispatchers.IO) {
        api.getRepairTickets(scope, status, priority, search, page)
    }

    suspend fun create(request: CreateRepairTicketRequest): Response<CreateRepairTicketResponse> = withContext(Dispatchers.IO) {
        api.createRepairTicket(request)
    }

    suspend fun detail(ticketId: Int): Response<RepairTicketDetailResponse> = withContext(Dispatchers.IO) {
        api.getRepairTicketDetail(ticketId)
    }

    suspend fun action(request: RepairTicketActionRequest): Response<RepairActionResponse> = withContext(Dispatchers.IO) {
        api.repairTicketAction(request)
    }

    suspend fun lookup(
        kind: String,
        query: String? = null,
        companyId: Int? = null,
        departmentId: Int? = null,
        branchId: Int? = null
    ): Response<RepairLookupResponse> = withContext(Dispatchers.IO) {
        api.getRepairLookups(kind, query, companyId, departmentId, branchId)
    }

    suspend fun forwardItems(ticketId: Int, query: String? = null): Response<RepairForwardItemsResponse> = withContext(Dispatchers.IO) {
        api.getRepairForwardItems(ticketId, query)
    }

    suspend fun previewForward(request: RepairForwardRequest): Response<RepairForwardPreviewResponse> = withContext(Dispatchers.IO) {
        api.previewRepairForward(request)
    }

    suspend fun createForward(request: RepairForwardRequest): Response<RepairForwardCreateResponse> = withContext(Dispatchers.IO) {
        api.createRepairForward(request)
    }

    suspend fun uploadEvidence(request: RepairTicketEvidenceUploadRequest): Response<RepairTicketEvidenceUploadResponse> = withContext(Dispatchers.IO) {
        api.uploadRepairTicketEvidence(request)
    }

    suspend fun attendance(): Response<RepairAttendanceResponse> = withContext(Dispatchers.IO) {
        api.getRepairAttendance()
    }

    suspend fun reports(pageSize: Int = 20): Response<com.example.yakultscanner.api.RepairReportsResponse> = withContext(Dispatchers.IO) {
        api.getRepairReports(pageSize)
    }

    suspend fun qr(ticketId: Int): Response<RepairTicketQrResponse> = withContext(Dispatchers.IO) {
        api.getRepairTicketQr(ticketId)
    }

    suspend fun reportSignatures(ticketId: Int): Response<com.example.yakultscanner.api.RepairReportSignaturesResponse> = withContext(Dispatchers.IO) {
        api.getRepairReportSignatures(ticketId)
    }

    suspend fun postReportSignature(request: com.example.yakultscanner.api.RepairReportSignatureRequest): Response<com.example.yakultscanner.api.RepairReportSignaturesResponse> = withContext(Dispatchers.IO) {
        api.postRepairReportSignature(request)
    }

    suspend fun reportPreview(ticketIds: List<Int>, includeAttachments: Boolean = true): Response<com.example.yakultscanner.api.RepairReportPreviewResponse> = withContext(Dispatchers.IO) {
        api.getRepairReportPreview(ticketIds.joinToString(","), includeAttachments)
    }

    suspend fun reportPreviewWithSignatures(request: com.example.yakultscanner.api.RepairReportPreviewRequest): Response<com.example.yakultscanner.api.RepairReportPreviewResponse> = withContext(Dispatchers.IO) {
        api.postRepairReportPreview(request)
    }

    suspend fun downloadEvidence(attachmentId: Int? = null, partAttachmentId: Int? = null): Response<ResponseBody> = withContext(Dispatchers.IO) {
        api.downloadRepairEvidence(attachmentId, partAttachmentId)
    }
}
