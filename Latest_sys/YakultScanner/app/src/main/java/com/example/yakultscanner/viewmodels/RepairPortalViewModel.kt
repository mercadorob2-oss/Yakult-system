package com.example.yakultscanner.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.yakultscanner.api.CreateRepairTicketRequest
import com.example.yakultscanner.api.RepairLookupItemDto
import com.example.yakultscanner.api.RepairAttendanceResponse
import com.example.yakultscanner.api.RepairTicketQrResponse
import com.example.yakultscanner.api.RepairTicketEvidenceUploadRequest
import com.example.yakultscanner.api.RepairTicketActionRequest
import com.example.yakultscanner.api.RepairTicketDetailResponse
import com.example.yakultscanner.api.RepairTicketListResponse
import com.example.yakultscanner.data.repository.RepairPortalRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import org.json.JSONObject
import retrofit2.Response
import java.io.File
import javax.inject.Inject

sealed class RepairTicketsUiState {
    object Idle : RepairTicketsUiState()
    object Loading : RepairTicketsUiState()
    data class Success(val response: RepairTicketListResponse) : RepairTicketsUiState()
    data class Error(val message: String) : RepairTicketsUiState()
}

sealed class RepairDetailUiState {
    object Idle : RepairDetailUiState()
    object Loading : RepairDetailUiState()
    data class Success(val response: RepairTicketDetailResponse) : RepairDetailUiState()
    data class Error(val message: String) : RepairDetailUiState()
}

sealed class RepairCreateUiState {
    object Idle : RepairCreateUiState()
    object Loading : RepairCreateUiState()
    data class Success(val ticketId: Int, val ticketCode: String?) : RepairCreateUiState()
    data class Error(val message: String) : RepairCreateUiState()
}

sealed class RepairActionUiState {
    object Idle : RepairActionUiState()
    object Loading : RepairActionUiState()
    data class Success(val message: String) : RepairActionUiState()
    data class Error(val message: String) : RepairActionUiState()
}

sealed class RepairAttendanceUiState {
    object Idle : RepairAttendanceUiState()
    object Loading : RepairAttendanceUiState()
    data class Success(val response: RepairAttendanceResponse) : RepairAttendanceUiState()
    data class Error(val message: String) : RepairAttendanceUiState()
}

sealed class RepairQrUiState {
    object Idle : RepairQrUiState()
    object Loading : RepairQrUiState()
    data class Success(val response: RepairTicketQrResponse) : RepairQrUiState()
    data class Error(val message: String) : RepairQrUiState()
}

sealed class RepairEvidenceUiState {
    object Idle : RepairEvidenceUiState()
    object Loading : RepairEvidenceUiState()
    data class Success(val message: String) : RepairEvidenceUiState()
    data class Error(val message: String) : RepairEvidenceUiState()
}

sealed class RepairDownloadUiState {
    object Idle : RepairDownloadUiState()
    object Loading : RepairDownloadUiState()
    data class Success(val file: File, val mimeType: String?) : RepairDownloadUiState()
    data class Error(val message: String) : RepairDownloadUiState()
}

sealed class RepairReportsUiState {
    object Idle : RepairReportsUiState()
    object Loading : RepairReportsUiState()
    data class Success(val response: com.example.yakultscanner.api.RepairReportsResponse) : RepairReportsUiState()
    data class Error(val message: String) : RepairReportsUiState()
}

sealed class RepairReportPreviewUiState {
    object Idle : RepairReportPreviewUiState()
    object Loading : RepairReportPreviewUiState()
    data class Success(val response: com.example.yakultscanner.api.RepairReportPreviewResponse) : RepairReportPreviewUiState()
    data class Error(val message: String) : RepairReportPreviewUiState()
}

sealed class RepairReportSignaturesUiState {
    object Idle : RepairReportSignaturesUiState()
    object Loading : RepairReportSignaturesUiState()
    data class Success(val response: com.example.yakultscanner.api.RepairReportSignaturesResponse) : RepairReportSignaturesUiState()
    data class Error(val message: String) : RepairReportSignaturesUiState()
}

sealed class RepairReportPdfUiState {
    object Idle : RepairReportPdfUiState()
    object Generating : RepairReportPdfUiState()
    data class Success(val file: File) : RepairReportPdfUiState()
    data class Error(val message: String) : RepairReportPdfUiState()
}

@HiltViewModel
class RepairPortalViewModel @Inject constructor(
    private val repository: RepairPortalRepository
) : ViewModel() {
    private val _ticketsState = MutableStateFlow<RepairTicketsUiState>(RepairTicketsUiState.Idle)
    val ticketsState: StateFlow<RepairTicketsUiState> = _ticketsState.asStateFlow()

    private val _detailState = MutableStateFlow<RepairDetailUiState>(RepairDetailUiState.Idle)
    val detailState: StateFlow<RepairDetailUiState> = _detailState.asStateFlow()

    private val _createState = MutableStateFlow<RepairCreateUiState>(RepairCreateUiState.Idle)
    val createState: StateFlow<RepairCreateUiState> = _createState.asStateFlow()

    private val _actionState = MutableStateFlow<RepairActionUiState>(RepairActionUiState.Idle)
    val actionState: StateFlow<RepairActionUiState> = _actionState.asStateFlow()

    private val _attendanceState = MutableStateFlow<RepairAttendanceUiState>(RepairAttendanceUiState.Idle)
    val attendanceState: StateFlow<RepairAttendanceUiState> = _attendanceState.asStateFlow()

    private val _qrState = MutableStateFlow<RepairQrUiState>(RepairQrUiState.Idle)
    val qrState: StateFlow<RepairQrUiState> = _qrState.asStateFlow()

    private val _evidenceState = MutableStateFlow<RepairEvidenceUiState>(RepairEvidenceUiState.Idle)
    val evidenceState: StateFlow<RepairEvidenceUiState> = _evidenceState.asStateFlow()

    private val _downloadState = MutableStateFlow<RepairDownloadUiState>(RepairDownloadUiState.Idle)
    val downloadState: StateFlow<RepairDownloadUiState> = _downloadState.asStateFlow()

    private val _reportsState = MutableStateFlow<RepairReportsUiState>(RepairReportsUiState.Idle)
    val reportsState: StateFlow<RepairReportsUiState> = _reportsState.asStateFlow()

    private val _reportPreviewState = MutableStateFlow<RepairReportPreviewUiState>(RepairReportPreviewUiState.Idle)
    val reportPreviewState: StateFlow<RepairReportPreviewUiState> = _reportPreviewState.asStateFlow()

    private val _reportSignaturesState = MutableStateFlow<RepairReportSignaturesUiState>(RepairReportSignaturesUiState.Idle)
    val reportSignaturesState: StateFlow<RepairReportSignaturesUiState> = _reportSignaturesState.asStateFlow()

    private val _reportPdfState = MutableStateFlow<RepairReportPdfUiState>(RepairReportPdfUiState.Idle)
    val reportPdfState: StateFlow<RepairReportPdfUiState> = _reportPdfState.asStateFlow()

    private val _selectedReportIds = MutableStateFlow<Set<Int>>(emptySet())
    val selectedReportIds: StateFlow<Set<Int>> = _selectedReportIds.asStateFlow()

    private val _reportPickerTicketsState = MutableStateFlow<RepairTicketsUiState>(RepairTicketsUiState.Idle)
    val reportPickerTicketsState: StateFlow<RepairTicketsUiState> = _reportPickerTicketsState.asStateFlow()

    private val _reportPickerQuery = MutableStateFlow("")
    val reportPickerQuery: StateFlow<String> = _reportPickerQuery.asStateFlow()

    private val _itemLookup = MutableStateFlow<List<RepairLookupItemDto>>(emptyList())
    val itemLookup: StateFlow<List<RepairLookupItemDto>> = _itemLookup.asStateFlow()

    private val _technicianLookup = MutableStateFlow<List<RepairLookupItemDto>>(emptyList())
    val technicianLookup: StateFlow<List<RepairLookupItemDto>> = _technicianLookup.asStateFlow()

    private val _technicianLookupKind = MutableStateFlow<String?>(null)
    val technicianLookupKind: StateFlow<String?> = _technicianLookupKind.asStateFlow()

    private val _lookupLoading = MutableStateFlow(false)
    val lookupLoading: StateFlow<Boolean> = _lookupLoading.asStateFlow()

    fun loadTickets(scope: String, status: String? = null, priority: String? = null, search: String? = null) {
        viewModelScope.launch {
            _ticketsState.value = RepairTicketsUiState.Loading
            try {
                val response = repository.tickets(scope, status, priority, search)
                val body = response.body()
                _ticketsState.value = when {
                    response.isSuccessful && body?.success == true -> RepairTicketsUiState.Success(body)
                    else -> RepairTicketsUiState.Error(readError(response, body?.message ?: "Repair tickets could not be loaded"))
                }
            } catch (_: Exception) {
                _ticketsState.value = RepairTicketsUiState.Error("Network error while loading repair tickets")
            }
        }
    }

    fun loadDetail(ticketId: Int, silent: Boolean = false) {
        viewModelScope.launch {
            if (!(silent && _detailState.value is RepairDetailUiState.Success)) {
                _detailState.value = RepairDetailUiState.Loading
            }
            try {
                val response = repository.detail(ticketId)
                val body = response.body()
                _detailState.value = when {
                    response.isSuccessful && body?.success == true -> RepairDetailUiState.Success(body)
                    else -> RepairDetailUiState.Error(readError(response, body?.message ?: "Repair ticket could not be loaded"))
                }
            } catch (_: Exception) {
                _detailState.value = RepairDetailUiState.Error("Network error while loading the repair ticket")
            }
        }
    }

    fun searchItems(query: String) {
        viewModelScope.launch {
            _lookupLoading.value = true
            try {
                val response = repository.lookup("items", query)
                _itemLookup.value = if (response.isSuccessful && response.body()?.success == true) response.body()?.items.orEmpty() else emptyList()
            } catch (_: Exception) {
                _itemLookup.value = emptyList()
            } finally {
                _lookupLoading.value = false
            }
        }
    }

    fun loadTechnicianLookup(
        kind: String,
        query: String? = null,
        companyId: Int? = null,
        departmentId: Int? = null,
        branchId: Int? = null
    ) {
        viewModelScope.launch {
            _lookupLoading.value = true
            _technicianLookupKind.value = kind
            try {
                val response = repository.lookup(kind, query, companyId, departmentId, branchId)
                _technicianLookup.value = if (response.isSuccessful && response.body()?.success == true) response.body()?.items.orEmpty() else emptyList()
            } catch (_: Exception) {
                _technicianLookup.value = emptyList()
            } finally {
                _lookupLoading.value = false
            }
        }
    }

    fun clearTechnicianLookup() {
        _technicianLookup.value = emptyList()
        _technicianLookupKind.value = null
    }

    fun createTicket(request: CreateRepairTicketRequest) {
        viewModelScope.launch {
            _createState.value = RepairCreateUiState.Loading
            try {
                val response = repository.create(request)
                val body = response.body()
                _createState.value = when {
                    response.isSuccessful && body?.success == true && body.ticket != null ->
                        RepairCreateUiState.Success(body.ticket.repairTicketId, body.ticket.ticketCode)
                    else -> RepairCreateUiState.Error(readError(response, body?.message ?: "Repair ticket could not be created"))
                }
            } catch (_: Exception) {
                _createState.value = RepairCreateUiState.Error("Network error while creating the repair ticket")
            }
        }
    }

    fun submitAction(request: RepairTicketActionRequest, refreshTicketId: Int? = request.ticketId) {
        viewModelScope.launch {
            _actionState.value = RepairActionUiState.Loading
            try {
                val response = repository.action(request)
                val body = response.body()
                if (response.isSuccessful && body?.success == true) {
                    _actionState.value = RepairActionUiState.Success(body.message ?: "Repair ticket updated")
                    if (refreshTicketId != null) loadDetail(refreshTicketId, silent = true)
                } else {
                    _actionState.value = RepairActionUiState.Error(readError(response, body?.message ?: "Repair action could not be completed"))
                }
            } catch (_: Exception) {
                _actionState.value = RepairActionUiState.Error("Network error while updating the repair ticket")
            }
        }
    }

    fun loadAttendance() {
        viewModelScope.launch {
            _attendanceState.value = RepairAttendanceUiState.Loading
            try {
                val response = repository.attendance()
                val body = response.body()
                _attendanceState.value = if (response.isSuccessful && body?.success == true) {
                    RepairAttendanceUiState.Success(body)
                } else {
                    RepairAttendanceUiState.Error(readError(response, body?.message ?: "Repair attendance could not be loaded"))
                }
            } catch (_: Exception) {
                _attendanceState.value = RepairAttendanceUiState.Error("Network error while loading repair attendance")
            }
        }
    }

    fun loadQr(ticketId: Int) {
        viewModelScope.launch {
            _qrState.value = RepairQrUiState.Loading
            try {
                val response = repository.qr(ticketId)
                val body = response.body()
                _qrState.value = if (response.isSuccessful && body?.success == true) {
                    RepairQrUiState.Success(body)
                } else {
                    RepairQrUiState.Error(readError(response, body?.message ?: "Repair QR data could not be loaded"))
                }
            } catch (_: Exception) {
                _qrState.value = RepairQrUiState.Error("Network error while loading repair QR data")
            }
        }
    }

    fun uploadEvidence(request: RepairTicketEvidenceUploadRequest) {
        viewModelScope.launch {
            _evidenceState.value = RepairEvidenceUiState.Loading
            try {
                val response = repository.uploadEvidence(request)
                val body = response.body()
                if (response.isSuccessful && body?.success == true) {
                    _evidenceState.value = RepairEvidenceUiState.Success(body.message ?: "Evidence uploaded")
                    loadDetail(request.repairTicketId)
                } else {
                    _evidenceState.value = RepairEvidenceUiState.Error(readError(response, body?.message ?: "Evidence could not be uploaded"))
                }
            } catch (_: Exception) {
                _evidenceState.value = RepairEvidenceUiState.Error("Network error while uploading evidence")
            }
        }
    }

    fun downloadEvidence(attachmentId: Int? = null, partAttachmentId: Int? = null, destination: File) {
        viewModelScope.launch {
            _downloadState.value = RepairDownloadUiState.Loading
            try {
                val response = repository.downloadEvidence(attachmentId, partAttachmentId)
                val body = response.body()
                if (!response.isSuccessful || body == null) {
                    _downloadState.value = RepairDownloadUiState.Error(readError(response, "Evidence could not be downloaded"))
                    return@launch
                }
                destination.parentFile?.mkdirs()
                body.byteStream().use { input ->
                    destination.outputStream().use { output -> input.copyTo(output) }
                }
                _downloadState.value = RepairDownloadUiState.Success(destination, body.contentType()?.toString())
            } catch (_: Exception) {
                _downloadState.value = RepairDownloadUiState.Error("Network error while downloading evidence")
            }
        }
    }

    fun resetQrState() { _qrState.value = RepairQrUiState.Idle }
    fun resetEvidenceState() { _evidenceState.value = RepairEvidenceUiState.Idle }
    fun resetDownloadState() { _downloadState.value = RepairDownloadUiState.Idle }
    fun resetCreateState() { _createState.value = RepairCreateUiState.Idle }
    fun resetActionState() { _actionState.value = RepairActionUiState.Idle }
    fun resetReportsState() { _reportsState.value = RepairReportsUiState.Idle }
    fun resetReportPreviewState() { _reportPreviewState.value = RepairReportPreviewUiState.Idle }
    fun resetReportSignaturesState() { _reportSignaturesState.value = RepairReportSignaturesUiState.Idle }
    fun resetReportPdfState() { _reportPdfState.value = RepairReportPdfUiState.Idle }

    // ── Report picker + preview + PDF generation ──

    fun toggleReportSelection(ticketId: Int) {
        val current = _selectedReportIds.value.toMutableSet()
        if (ticketId in current) current.remove(ticketId) else {
            if (current.size >= 20) return
            current.add(ticketId)
        }
        _selectedReportIds.value = current
    }

    fun setReportSelection(ids: Set<Int>) {
        _selectedReportIds.value = ids.take(20).toSet()
    }

    fun clearReportSelection() { _selectedReportIds.value = emptySet() }

    fun loadReportPickerTickets(scope: String = "mine", search: String? = null, status: String? = null, priority: String? = null) {
        viewModelScope.launch {
            _reportPickerTicketsState.value = RepairTicketsUiState.Loading
            if (search != null) _reportPickerQuery.value = search
            try {
                val response = repository.tickets(scope, status, priority, search, page = 1)
                val body = response.body()
                _reportPickerTicketsState.value = when {
                    response.isSuccessful && body?.success == true -> RepairTicketsUiState.Success(body)
                    else -> RepairTicketsUiState.Error(readError(response, body?.message ?: "Tickets could not be loaded"))
                }
            } catch (_: Exception) {
                _reportPickerTicketsState.value = RepairTicketsUiState.Error("Network error while loading tickets")
            }
        }
    }

    fun loadReportSignatures(ticketId: Int) {
        viewModelScope.launch {
            _reportSignaturesState.value = RepairReportSignaturesUiState.Loading
            try {
                val response = repository.reportSignatures(ticketId)
                val body = response.body()
                _reportSignaturesState.value = if (response.isSuccessful && body?.success == true) RepairReportSignaturesUiState.Success(body)
                else RepairReportSignaturesUiState.Error(readError(response, body?.message ?: "Signatures could not be loaded"))
            } catch (_: Exception) {
                _reportSignaturesState.value = RepairReportSignaturesUiState.Error("Network error while loading signatures")
            }
        }
    }

    fun loadReportPreview(ticketIds: List<Int>, includeAttachments: Boolean = true) {
        if (ticketIds.isEmpty()) {
            _reportPreviewState.value = RepairReportPreviewUiState.Error("Select at least one ticket")
            return
        }
        viewModelScope.launch {
            _reportPreviewState.value = RepairReportPreviewUiState.Loading
            try {
                val response = repository.reportPreview(ticketIds, includeAttachments)
                val body = response.body()
                _reportPreviewState.value = when {
                    response.isSuccessful && body?.success == true -> RepairReportPreviewUiState.Success(body)
                    else -> RepairReportPreviewUiState.Error(readError(response, body?.message ?: "Report preview could not be loaded"))
                }
            } catch (_: Exception) {
                _reportPreviewState.value = RepairReportPreviewUiState.Error("Network error while loading report preview")
            }
        }
    }

    fun generateReportWithSignatures(request: com.example.yakultscanner.api.RepairReportPreviewRequest) {
        viewModelScope.launch {
            _reportPreviewState.value = RepairReportPreviewUiState.Loading
            try {
                val response = repository.reportPreviewWithSignatures(request)
                val body = response.body()
                _reportPreviewState.value = when {
                    response.isSuccessful && body?.success == true -> RepairReportPreviewUiState.Success(body)
                    else -> RepairReportPreviewUiState.Error(readError(response, body?.message ?: "Report could not be generated"))
                }
            } catch (_: Exception) {
                _reportPreviewState.value = RepairReportPreviewUiState.Error("Network error while generating report")
            }
        }
    }

    /**
     * Generates a printable PDF on-device from the already-loaded preview response.
     * Uses Android PdfDocument (no extra dependency) — mirrors desktop ComputeItemFields layout
     * with data-driven section hiding, so a simple repair stays one page.
     */
    fun generateReportPdf(
        context: android.content.Context,
        preview: com.example.yakultscanner.api.RepairReportPreviewResponse,
        reviewedBy: com.example.yakultscanner.api.RepairReportSignatureDto? = null,
        receivedBy: com.example.yakultscanner.api.RepairReportSignatureDto? = null
    ) {
        viewModelScope.launch(kotlinx.coroutines.Dispatchers.IO) {
            _reportPdfState.value = RepairReportPdfUiState.Generating
            try {
                val file = com.example.yakultscanner.utils.RepairReportPdfGenerator.generate(
                    context = context,
                    preview = preview,
                    reviewedBy = reviewedBy,
                    receivedBy = receivedBy
                )
                _reportPdfState.value = RepairReportPdfUiState.Success(file)
            } catch (e: Exception) {
                _reportPdfState.value = RepairReportPdfUiState.Error(e.message ?: "PDF could not be generated")
            }
        }
    }

    fun loadReports(pageSize: Int = 20) {
        viewModelScope.launch {
            _reportsState.value = RepairReportsUiState.Loading
            try {
                val response = repository.reports(pageSize)
                val body = response.body()
                _reportsState.value = when {
                    response.isSuccessful && body?.success == true -> RepairReportsUiState.Success(body)
                    else -> RepairReportsUiState.Error(readError(response, body?.message ?: "Repair reports could not be loaded"))
                }
            } catch (_: Exception) {
                _reportsState.value = RepairReportsUiState.Error("Network error while loading repair reports")
            }
        }
    }

    private fun readError(response: Response<*>, fallback: String): String {
        return try {
            if (response.isSuccessful) fallback else {
                val raw = response.errorBody()?.string().orEmpty()
                JSONObject(raw).optString("message").takeIf { it.isNotBlank() } ?: when (response.code()) {
                    401 -> "Your session has expired. Please sign in again."
                    403 -> "Your account is not authorized for this repair action."
                    404 -> "The requested repair ticket was not found."
                    409 -> "This repair action conflicts with the current ticket state."
                    else -> fallback
                }
            }
        } catch (_: Exception) {
            fallback
        }
    }
}
