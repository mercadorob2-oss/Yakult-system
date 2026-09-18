package com.example.yakultscanner.ui.screens.fieldwork

import com.example.yakultscanner.api.CallFieldVisitAttachmentDto
import com.example.yakultscanner.api.CallFieldVisitDto
import com.example.yakultscanner.api.CallTicketHistoryDto

data class FieldWorkViewState(
    val visit: CallFieldVisitDto? = null,
    val attachments: List<CallFieldVisitAttachmentDto> = emptyList(),
    val fieldHistory: List<CallTicketHistoryDto> = emptyList(),
    val ticketHistory: List<CallTicketHistoryDto> = emptyList(),
    val isReschedule: Boolean = false,
    val hasSignature: Boolean = false,
    val photoCount: Int = 0
)
