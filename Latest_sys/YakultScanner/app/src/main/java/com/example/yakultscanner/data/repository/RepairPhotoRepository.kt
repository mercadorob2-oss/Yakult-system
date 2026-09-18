package com.example.yakultscanner.data.repository

import com.example.yakultscanner.api.RepairActionResponse
import com.example.yakultscanner.api.RepairPartPhotoUploadRequest
import com.example.yakultscanner.api.RepairPartPhotoUploadResponse
import com.example.yakultscanner.api.RepairTicketActionRequest
import com.example.yakultscanner.api.RepairTicketLookupResponse
import com.example.yakultscanner.api.YakultApiService
import retrofit2.Response
import javax.inject.Inject

/**
 * Repository for the mobile Repair Part photo upload flow — resolving a scanned/typed Repair
 * Ticket to its Parts list, then uploading evidence photos straight from the phone instead of
 * copying files to a PC first.
 */
class RepairPhotoRepository @Inject constructor(
    private val apiService: YakultApiService
) {

    suspend fun lookupRepairTicket(ticketCode: String?, token: String?): Response<RepairTicketLookupResponse> {
        return apiService.lookupRepairTicket(ticketCode, token)
    }

    suspend fun uploadRepairPartPhoto(request: RepairPartPhotoUploadRequest): Response<RepairPartPhotoUploadResponse> {
        return apiService.uploadRepairPartPhoto(request)
    }

    suspend fun deleteRepairPartPhoto(ticketId: Int, partAttachmentId: Int): Response<RepairActionResponse> {
        return apiService.repairTicketAction(
            RepairTicketActionRequest(
                action = "deletePartAttachment",
                ticketId = ticketId,
                partAttachmentId = partAttachmentId
            )
        )
    }
}
