package com.example.yakultscanner.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.yakultscanner.api.CallLookupItem
import com.example.yakultscanner.api.CallTicketActionResponse
import com.example.yakultscanner.api.CallTicketDetailResponse
import com.example.yakultscanner.api.CallTicketListItem
import com.example.yakultscanner.api.CallTicketListResponse
import com.example.yakultscanner.api.CreateTicketRequest
import com.example.yakultscanner.api.EscalationSettingsResponse
import com.example.yakultscanner.api.SetEscalationOverrideRequest
import com.example.yakultscanner.api.CallItemLookupDto
import com.example.yakultscanner.api.CallItemLookupResponse
import com.example.yakultscanner.api.CallConditionDto
import com.example.yakultscanner.api.CallConditionResponse
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.ResolutionRequest
import com.example.yakultscanner.api.RepairForwardCreateResponse
import com.example.yakultscanner.api.RepairForwardPreviewResponse
import com.example.yakultscanner.api.RepairForwardRequest
import com.example.yakultscanner.data.repository.CallMonitoringRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import org.json.JSONObject
import retrofit2.Response
import javax.inject.Inject

sealed class CallTicketsUiState {
    object Loading : CallTicketsUiState()
    data class Success(val response: CallTicketListResponse) : CallTicketsUiState()
    data class Error(val message: String) : CallTicketsUiState()
}

sealed class TicketDetailUiState {
    object Loading : TicketDetailUiState()
    data class Success(val response: CallTicketDetailResponse) : TicketDetailUiState()
    data class Error(val message: String) : TicketDetailUiState()
}

sealed class SignOffExportUiState {
    object Idle : SignOffExportUiState()
    object Loading : SignOffExportUiState()
    data class Ready(val bundle: com.example.yakultscanner.utils.TicketSignOffBundle) : SignOffExportUiState()
    data class Error(val message: String) : SignOffExportUiState()
}

sealed class ActionUiState {
    object Idle : ActionUiState()
    object Loading : ActionUiState()
    data class Success(val message: String) : ActionUiState()
    data class Error(val message: String) : ActionUiState()
}

sealed class ResolutionUiState {
    object Idle : ResolutionUiState()
    object Loading : ResolutionUiState()
    data class Success(val message: String) : ResolutionUiState()
    data class Error(val message: String) : ResolutionUiState()
}

sealed class RepairForwardUiState {
    object Idle : RepairForwardUiState()
    object PreviewLoading : RepairForwardUiState()
    data class Preview(val response: RepairForwardPreviewResponse) : RepairForwardUiState()
    object CreateLoading : RepairForwardUiState()
    data class Created(val response: RepairForwardCreateResponse) : RepairForwardUiState()
    data class Error(val message: String) : RepairForwardUiState()
}

sealed class CreateTicketUiState {
    object Idle : CreateTicketUiState()
    object Loading : CreateTicketUiState()
    data class Success(val ticket: CallTicketListItem?, val escalationWarning: String? = null) : CreateTicketUiState()
    data class Error(val message: String) : CreateTicketUiState()
}

sealed class FieldVisitsUiState {
    object Idle : FieldVisitsUiState()
    object Loading : FieldVisitsUiState()
    data class Success(val visits: List<com.example.yakultscanner.api.CallFieldVisitDto>, val attachments: List<com.example.yakultscanner.api.CallFieldVisitAttachmentDto> = emptyList()) : FieldVisitsUiState()
    data class Error(val message: String) : FieldVisitsUiState()
}

sealed class FieldVisitActionUiState {
    object Idle : FieldVisitActionUiState()
    object Loading : FieldVisitActionUiState()
    data class Success(val message: String) : FieldVisitActionUiState()
    data class Error(val message: String) : FieldVisitActionUiState()
}

@HiltViewModel
class CallMonitoringViewModel @Inject constructor(
    private val repository: CallMonitoringRepository
) : ViewModel() {

    private val _ticketsState = MutableStateFlow<CallTicketsUiState>(CallTicketsUiState.Loading)
    val ticketsState: StateFlow<CallTicketsUiState> = _ticketsState.asStateFlow()

    private val _detailState = MutableStateFlow<TicketDetailUiState>(TicketDetailUiState.Loading)
    val detailState: StateFlow<TicketDetailUiState> = _detailState.asStateFlow()

    private val _actionState = MutableStateFlow<ActionUiState>(ActionUiState.Idle)
    val actionState: StateFlow<ActionUiState> = _actionState.asStateFlow()

    private val _createState = MutableStateFlow<CreateTicketUiState>(CreateTicketUiState.Idle)
    val createState: StateFlow<CreateTicketUiState> = _createState.asStateFlow()

    private var currentPage = 1
    private val pageSize = 25

    private val _fieldVisitsState = MutableStateFlow<FieldVisitsUiState>(FieldVisitsUiState.Idle)
    val fieldVisitsState: StateFlow<FieldVisitsUiState> = _fieldVisitsState.asStateFlow()

    private val _fieldVisitActionState = MutableStateFlow<FieldVisitActionUiState>(FieldVisitActionUiState.Idle)
    val fieldVisitActionState: StateFlow<FieldVisitActionUiState> = _fieldVisitActionState.asStateFlow()

    private val _pendingFieldActions = MutableStateFlow<List<String>>(emptyList())
    val pendingFieldActions: StateFlow<List<String>> = _pendingFieldActions.asStateFlow()

    private val _fieldPhotoBase64 = MutableStateFlow<String?>(null)
    val fieldPhotoBase64: StateFlow<String?> = _fieldPhotoBase64.asStateFlow()

    private val _fieldSignatureBase64 = MutableStateFlow<String?>(null)
    val fieldSignatureBase64: StateFlow<String?> = _fieldSignatureBase64.asStateFlow()

    // ── Lookups ──
    private val _companies = MutableStateFlow<List<CallLookupItem>>(emptyList())
    val companies: StateFlow<List<CallLookupItem>> = _companies.asStateFlow()

    private val _departments = MutableStateFlow<List<CallLookupItem>>(emptyList())
    val departments: StateFlow<List<CallLookupItem>> = _departments.asStateFlow()

    private val _branches = MutableStateFlow<List<CallLookupItem>>(emptyList())
    val branches: StateFlow<List<CallLookupItem>> = _branches.asStateFlow()

    private val _callerEmployees = MutableStateFlow<List<CallLookupItem>>(emptyList())
    val callerEmployees: StateFlow<List<CallLookupItem>> = _callerEmployees.asStateFlow()

    private val _employeeSearchResults = MutableStateFlow<List<CallLookupItem>>(emptyList())
    val employeeSearchResults: StateFlow<List<CallLookupItem>> = _employeeSearchResults.asStateFlow()
    private val _employeeSearchLoading = MutableStateFlow(false)
    val employeeSearchLoading: StateFlow<Boolean> = _employeeSearchLoading.asStateFlow()
    private var employeeSearchJob: kotlinx.coroutines.Job? = null

    private val _itEmployees = MutableStateFlow<List<CallLookupItem>>(emptyList())
    val itEmployees: StateFlow<List<CallLookupItem>> = _itEmployees.asStateFlow()

    private val _escalationSettings = MutableStateFlow<EscalationSettingsResponse?>(null)
    val escalationSettings: StateFlow<EscalationSettingsResponse?> = _escalationSettings.asStateFlow()

    private val _resolutionState = MutableStateFlow<ResolutionUiState>(ResolutionUiState.Idle)
    val resolutionState: StateFlow<ResolutionUiState> = _resolutionState.asStateFlow()

    private val _repairForwardState = MutableStateFlow<RepairForwardUiState>(RepairForwardUiState.Idle)
    val repairForwardState: StateFlow<RepairForwardUiState> = _repairForwardState.asStateFlow()

    private val _resolutionOutItems = MutableStateFlow<List<CallItemLookupDto>>(emptyList())
    val resolutionOutItems: StateFlow<List<CallItemLookupDto>> = _resolutionOutItems.asStateFlow()

    private val _resolutionStockItems = MutableStateFlow<List<CallItemLookupDto>>(emptyList())
    val resolutionStockItems: StateFlow<List<CallItemLookupDto>> = _resolutionStockItems.asStateFlow()

    private val _resolutionConditions = MutableStateFlow<List<CallConditionDto>>(emptyList())
    val resolutionConditions: StateFlow<List<CallConditionDto>> = _resolutionConditions.asStateFlow()

    private val _resolutionCategories = MutableStateFlow<List<ItemCategoryDto>>(emptyList())
    val resolutionCategories: StateFlow<List<ItemCategoryDto>> = _resolutionCategories.asStateFlow()

    private val _resolutionLoadingLookups = MutableStateFlow(false)
    val resolutionLoadingLookups: StateFlow<Boolean> = _resolutionLoadingLookups.asStateFlow()

    fun loadTickets(
        status: String? = null,
        scope: String? = null,
        search: String? = null,
        priority: String? = null,
        issueType: String? = null,
        page: Int = 1,
        refresh: Boolean = false
    ) {
        viewModelScope.launch {
            if (refresh || page == 1) _ticketsState.value = CallTicketsUiState.Loading
            currentPage = page
            try {
                val response = repository.getCallTickets(
                    status = status,
                    scope = scope,
                    search = search,
                    priority = priority,
                    issueType = issueType,
                    page = page,
                    pageSize = pageSize
                )
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null) {
                        _ticketsState.value = CallTicketsUiState.Success(body)
                    } else {
                        _ticketsState.value = CallTicketsUiState.Error("Empty response from server")
                    }
                } else {
                    _ticketsState.value = CallTicketsUiState.Error(friendlyHttpMessage(response.code()))
                }
            } catch (e: Exception) {
                _ticketsState.value = CallTicketsUiState.Error("Network error: ${e.message}")
            }
        }
    }

    private var lastTicketDetailId: Int? = null
    private var lastTicketDetailMs: Long = 0L
    fun loadTicketDetail(ticketId: Int, force: Boolean = false) {
        val now = System.currentTimeMillis()
        if (!force && ticketId == lastTicketDetailId && now - lastTicketDetailMs < 2000 && _detailState.value is TicketDetailUiState.Success) return
        lastTicketDetailId = ticketId
        lastTicketDetailMs = now
        viewModelScope.launch {
            _detailState.value = TicketDetailUiState.Loading
            try {
                val response = repository.getTicketDetail(ticketId)
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null && body.success) {
                        _detailState.value = TicketDetailUiState.Success(body)
                    } else {
                        _detailState.value = TicketDetailUiState.Error("Failed to load ticket details")
                    }
                } else {
                    _detailState.value = TicketDetailUiState.Error(friendlyHttpMessage(response.code()))
                }
            } catch (e: Exception) {
                _detailState.value = TicketDetailUiState.Error("Network error: ${e.message}")
            }
        }
    }

    private val _signOffExportState = MutableStateFlow<SignOffExportUiState>(SignOffExportUiState.Idle)
    val signOffExportState: StateFlow<SignOffExportUiState> = _signOffExportState.asStateFlow()

    /** Loads the field-work bundle for a 1:1 Sign-Off ticket PDF. Never throws. */
    fun exportSignOffBundle(ticketId: Int) {
        viewModelScope.launch {
            _signOffExportState.value = SignOffExportUiState.Loading
            try {
                _signOffExportState.value =
                    SignOffExportUiState.Ready(repository.loadSignOffBundle(ticketId))
            } catch (e: Exception) {
                _signOffExportState.value =
                    SignOffExportUiState.Error("Could not load sign-off data: ${e.message}")
            }
        }
    }

    fun resetSignOffExportState() {
        _signOffExportState.value = SignOffExportUiState.Idle
    }

    /** Direct bundle fetch for callers with their own loading UI. Never throws. */
    suspend fun loadSignOffBundleNow(ticketId: Int): com.example.yakultscanner.utils.TicketSignOffBundle? {
        return try {
            repository.loadSignOffBundle(ticketId)
        } catch (_: Exception) {
            null
        }
    }

    fun addNote(ticketId: Int, noteText: String, noteType: String = "Internal", userId: Int? = null) {
        viewModelScope.launch {
            _actionState.value = ActionUiState.Loading
            try {
                val response = repository.addNote(ticketId, noteText, noteType, userId)
                handleActionResponse(response)
            } catch (e: Exception) {
                _actionState.value = ActionUiState.Error("Network error: ${e.message}")
            }
        }
    }

    fun updateStatus(ticketId: Int, newStatus: String, note: String? = null, userId: Int? = null) {
        viewModelScope.launch {
            _actionState.value = ActionUiState.Loading
            try {
                val response = repository.updateStatus(ticketId, newStatus, note, userId)
                handleActionResponse(response)
            } catch (e: Exception) {
                _actionState.value = ActionUiState.Error("Network error: ${e.message}")
            }
        }
    }

    fun updatePriority(ticketId: Int, newPriority: String, userId: Int? = null) {
        viewModelScope.launch {
            _actionState.value = ActionUiState.Loading
            try {
                val response = repository.updatePriority(ticketId, newPriority, userId)
                handleActionResponse(response)
            } catch (e: Exception) {
                _actionState.value = ActionUiState.Error("Network error: ${e.message}")
            }
        }
    }

    fun createTicket(request: CreateTicketRequest) {
        viewModelScope.launch {
            _createState.value = CreateTicketUiState.Loading
            try {
                val response = repository.createTicket(request)
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null && body.success) {
                        _createState.value = CreateTicketUiState.Success(body.ticket)
                    } else {
                        _createState.value = CreateTicketUiState.Error("Failed to create ticket")
                    }
                } else {
                    _createState.value = CreateTicketUiState.Error(friendlyHttpMessage(response.code()))
                }
            } catch (e: Exception) {
                _createState.value = CreateTicketUiState.Error("Network error: ${e.message}")
            }
        }
    }

    fun loadCompanies() {
        viewModelScope.launch {
            try {
                val r = repository.getCompanies()
                if (r.isSuccessful) _companies.value = r.body()?.items ?: emptyList()
            } catch (_: Exception) {}
        }
    }

    fun loadDepartments(comId: Int? = null) {
        viewModelScope.launch {
            try {
                val r = repository.getDepartments(comId)
                if (r.isSuccessful) _departments.value = r.body()?.items ?: emptyList()
            } catch (_: Exception) {}
        }
    }

    fun loadBranches(comId: Int? = null, deptId: Int? = null) {
        viewModelScope.launch {
            try {
                val r = repository.getBranches(comId, deptId)
                if (r.isSuccessful) _branches.value = r.body()?.items ?: emptyList()
            } catch (_: Exception) {}
        }
    }

    fun loadCallerEmployees(deptId: Int, comId: Int? = null, branchId: Int? = null) {
        viewModelScope.launch {
            try {
                val r = repository.getEmployees(deptId, comId, branchId)
                if (r.isSuccessful) _callerEmployees.value = r.body()?.items ?: emptyList()
                else _callerEmployees.value = emptyList()
            } catch (_: Exception) { _callerEmployees.value = emptyList() }
        }
    }

    private var lastItEmployeesMs: Long = 0L
    fun loadItEmployees(force: Boolean = false) {
        val now = System.currentTimeMillis()
        if (!force && now - lastItEmployeesMs < 5000 && _itEmployees.value.isNotEmpty()) return
        lastItEmployeesMs = now
        viewModelScope.launch {
            try {
                val r = repository.getItEmployees()
                if (r.isSuccessful) _itEmployees.value = r.body()?.items ?: emptyList()
            } catch (_: Exception) {}
        }
    }

    fun loadEscalationSettings() {
        viewModelScope.launch {
            try {
                val r = repository.getEscalationSettings()
                if (r.isSuccessful) _escalationSettings.value = r.body()
            } catch (_: Exception) {}
        }
    }

    fun loadResolutionLookups() {
        viewModelScope.launch {
            _resolutionLoadingLookups.value = true
            try {
                val outResp = repository.getCallItemsLookup("out")
                if (outResp.isSuccessful) _resolutionOutItems.value = outResp.body()?.items ?: emptyList()

                val stockResp = repository.getCallItemsLookup("stock")
                if (stockResp.isSuccessful) _resolutionStockItems.value = stockResp.body()?.items ?: emptyList()

                val condResp = repository.getCallConditions()
                if (condResp.isSuccessful) _resolutionConditions.value = condResp.body()?.conditions ?: emptyList()

                val catResp = repository.getItemCategories()
                if (catResp.isSuccessful) _resolutionCategories.value = catResp.body() ?: emptyList()
            } catch (_: Exception) {}
            _resolutionLoadingLookups.value = false
        }
    }

    fun applyResolution(request: ResolutionRequest) {
        viewModelScope.launch {
            _resolutionState.value = ResolutionUiState.Loading
            try {
                val response = repository.applyResolution(request)
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null && body.success) {
                        _resolutionState.value = ResolutionUiState.Success(body.message ?: "Ticket resolved")
                    } else {
                        _resolutionState.value = ResolutionUiState.Error(body?.message ?: "Resolution failed")
                    }
                } else {
                    _resolutionState.value = ResolutionUiState.Error(serverErrorMessage(response) ?: friendlyHttpMessage(response.code()))
                }
            } catch (e: Exception) {
                _resolutionState.value = ResolutionUiState.Error("Network error: ${e.message}")
            }
        }
    }

    fun resetResolutionState() {
        _resolutionState.value = ResolutionUiState.Idle
    }

    fun previewRepairForward(request: RepairForwardRequest) {
        viewModelScope.launch {
            _repairForwardState.value = RepairForwardUiState.PreviewLoading
            try {
                val response = repository.previewRepairForward(request)
                val body = response.body()
                _repairForwardState.value = if (response.isSuccessful && body?.success == true && body.preview != null) {
                    RepairForwardUiState.Preview(body)
                } else {
                    RepairForwardUiState.Error(serverErrorMessage(response) ?: body?.message ?: friendlyHttpMessage(response.code()))
                }
            } catch (_: Exception) {
                _repairForwardState.value = RepairForwardUiState.Error("Network error while preparing the Repair Ticket")
            }
        }
    }

    fun createRepairForward(request: RepairForwardRequest) {
        viewModelScope.launch {
            _repairForwardState.value = RepairForwardUiState.CreateLoading
            try {
                val response = repository.createRepairForward(request)
                val body = response.body()
                _repairForwardState.value = if (response.isSuccessful && body?.success == true && body.ticket != null) {
                    RepairForwardUiState.Created(body)
                } else {
                    RepairForwardUiState.Error(serverErrorMessage(response) ?: body?.message ?: friendlyHttpMessage(response.code()))
                }
            } catch (_: Exception) {
                _repairForwardState.value = RepairForwardUiState.Error("Network error while creating the linked Repair Ticket")
            }
        }
    }

    fun resetRepairForwardState() {
        _repairForwardState.value = RepairForwardUiState.Idle
    }

    fun createTicketWithEscalation(
        request: CreateTicketRequest,
        daysToSupervisor: Int? = null,
        daysToManager: Int? = null,
        reason: String? = null
    ) {
        val hasEscalation = daysToSupervisor != null && daysToManager != null && !reason.isNullOrBlank()
        viewModelScope.launch {
            _createState.value = CreateTicketUiState.Loading
            try {
                val response = repository.createTicket(request)
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null && body.success) {
                        val ticketId = body.ticket?.ticketId ?: 0
                        var escalationWarning: String? = null
                        if (hasEscalation && ticketId > 0) {
                            try {
                                val overrideReq = SetEscalationOverrideRequest(
                                    ticketId = ticketId,
                                    daysToSupervisor = daysToSupervisor,
                                    daysToManager = daysToManager,
                                    reason = reason!!,
                                    changedByUserId = request.createdByUserId
                                )
                                val overrideResp = repository.setEscalationOverride(overrideReq)
                                if (!overrideResp.isSuccessful || overrideResp.body()?.success != true) {
                                    escalationWarning = "Ticket created, but escalation override could not be saved."
                                }
                            } catch (e: Exception) {
                                escalationWarning = "Ticket created, but escalation override could not be saved. Try updating it from the ticket details screen."
                            }
                        }
                        _createState.value = CreateTicketUiState.Success(body.ticket, escalationWarning)
                    } else {
                        _createState.value = CreateTicketUiState.Error("Failed to create ticket")
                    }
                } else {
                    _createState.value = CreateTicketUiState.Error(friendlyHttpMessage(response.code()))
                }
            } catch (e: Exception) {
                _createState.value = CreateTicketUiState.Error("Network error: ${e.message}")
            }
        }
    }

    fun clearCallerEmployees() { _callerEmployees.value = emptyList() }

    fun clearEmployeeSearchResults() { _employeeSearchResults.value = emptyList(); _employeeSearchLoading.value = false; employeeSearchJob?.cancel() }

    fun searchEmployeesByName(query: String) {
        val q = query.trim()
        employeeSearchJob?.cancel()
        if (q.length < 2) { _employeeSearchResults.value = emptyList(); _employeeSearchLoading.value = false; return }
        employeeSearchJob = viewModelScope.launch {
            kotlinx.coroutines.delay(350)
            _employeeSearchLoading.value = true
            try {
                val r = repository.searchEmployees(q)
                if (r.isSuccessful) _employeeSearchResults.value = r.body()?.items ?: emptyList()
                else _employeeSearchResults.value = emptyList()
            } catch (_: Exception) { _employeeSearchResults.value = emptyList() }
            finally { _employeeSearchLoading.value = false }
        }
    }

    suspend fun loadEmployeeOrg(empId: Int): com.example.yakultscanner.api.EmployeeOrgResponse? {
        return try {
            val r = repository.getEmployeeOrg(empId)
            if (r.isSuccessful) r.body() else null
        } catch (_: Exception) { null }
    }

    fun resetActionState() {
        _actionState.value = ActionUiState.Idle
    }

    fun resetCreateState() {
        _createState.value = CreateTicketUiState.Idle
    }

    fun resetFieldVisitActionState() { _fieldVisitActionState.value = FieldVisitActionUiState.Idle }

    // ── Field Work (one per ticket, no GPS, queue on offline) ──
    private var lastFieldVisitTicketId: Int? = null
    private var lastFieldVisitMs: Long = 0L
    fun loadFieldVisits(ticketId: Int, force: Boolean = false) {
        val now = System.currentTimeMillis()
        if (!force && ticketId == lastFieldVisitTicketId && now - lastFieldVisitMs < 2000 && _fieldVisitsState.value is FieldVisitsUiState.Success) return
        lastFieldVisitTicketId = ticketId
        lastFieldVisitMs = now
        viewModelScope.launch {
            _fieldVisitsState.value = FieldVisitsUiState.Loading
            try {
                val resp = repository.getFieldVisits(ticketId)
                val body = resp.body()
                _fieldVisitsState.value = when {
                    resp.isSuccessful && body?.success == true -> FieldVisitsUiState.Success(body.visits, body.attachments)
                    else -> FieldVisitsUiState.Error(serverErrorMessage(resp) ?: friendlyHttpMessage(resp.code()))
                }
            } catch (e: Exception) {
                // Queue for offline retry (4.b)
                _pendingFieldActions.value = _pendingFieldActions.value + "load:$ticketId"
                _fieldVisitsState.value = FieldVisitsUiState.Error("No internet — field visits will sync when online. ${e.message ?: ""}")
            }
        }
    }

    fun scheduleFieldVisit(ticketId: Int, technicianEmpId: Int? = null, notes: String? = null) {
        if (_fieldVisitActionState.value is FieldVisitActionUiState.Loading || _fieldVisitsState.value is FieldVisitsUiState.Loading) return
        viewModelScope.launch {
            _fieldVisitActionState.value = FieldVisitActionUiState.Loading
            try {
                val scheduledAt = java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ssXXX", java.util.Locale.US).apply { timeZone = java.util.TimeZone.getDefault() }.format(java.util.Date())
                val resp = repository.scheduleFieldVisit(com.example.yakultscanner.api.ScheduleFieldVisitRequest(ticketId, technicianEmpId, scheduledAt, notes))
                val body = resp.body()
                if (resp.isSuccessful && body?.success == true) {
                    _fieldVisitActionState.value = FieldVisitActionUiState.Success("Field visit scheduled")
                    loadFieldVisits(ticketId, force = true)
                    loadTicketDetail(ticketId, force = true)
                } else {
                    val msg = serverErrorMessage(resp) ?: friendlyHttpMessage(resp.code())
                    if (resp.code() == 503 || msg.contains("unavailable", true)) {
                        _pendingFieldActions.value = _pendingFieldActions.value + "schedule:$ticketId"
                    }
                    _fieldVisitActionState.value = FieldVisitActionUiState.Error(msg)
                }
            } catch (e: Exception) {
                _pendingFieldActions.value = _pendingFieldActions.value + "schedule:$ticketId"
                _fieldVisitActionState.value = FieldVisitActionUiState.Error("No internet — queued for sync. ${e.message ?: ""}")
            }
        }
    }

    fun fieldVisitAction(fieldVisitId: Int, ticketId: Int, action: String, notes: String? = null, signatureBase64: String? = null, technicianEmpId: Int? = null, scheduledAt: String? = null) {
        if (_fieldVisitActionState.value is FieldVisitActionUiState.Loading) return
        viewModelScope.launch {
            _fieldVisitActionState.value = FieldVisitActionUiState.Loading
            try {
                val effectiveScheduledAt = if (action.equals("Scheduled", true) && scheduledAt == null)
                    java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ssXXX", java.util.Locale.US).apply { timeZone = java.util.TimeZone.getDefault() }.format(java.util.Date())
                else scheduledAt
                val resp = repository.fieldVisitAction(com.example.yakultscanner.api.FieldVisitActionRequest(fieldVisitId, action, notes, signatureBase64, technicianEmpId, effectiveScheduledAt))
                val body = resp.body()
                if (resp.isSuccessful && body?.success == true) {
                    _fieldVisitActionState.value = FieldVisitActionUiState.Success(body.message ?: "Field work updated: $action")
                    loadFieldVisits(ticketId, force = true)
                    loadTicketDetail(ticketId, force = true)
                } else {
                    val msg = serverErrorMessage(resp) ?: friendlyHttpMessage(resp.code())
                    if (msg.contains("unavailable", true)) _pendingFieldActions.value = _pendingFieldActions.value + "$action:$fieldVisitId"
                    _fieldVisitActionState.value = FieldVisitActionUiState.Error(msg)
                }
            } catch (e: Exception) {
                _pendingFieldActions.value = _pendingFieldActions.value + "$action:$fieldVisitId"
                _fieldVisitActionState.value = FieldVisitActionUiState.Error("No internet — queued. ${e.message ?: ""}")
            }
        }
    }

    fun uploadFieldVisitPhoto(fieldVisitId: Int, ticketId: Int, fileName: String, mimeType: String, base64: String) {
        viewModelScope.launch {
            _fieldVisitActionState.value = FieldVisitActionUiState.Loading
            try {
                val resp = repository.uploadFieldVisitPhoto(com.example.yakultscanner.api.FieldVisitPhotoUploadRequest(fieldVisitId, fileName, mimeType, base64))
                if (resp.isSuccessful && resp.body()?.success == true) {
                    _fieldVisitActionState.value = FieldVisitActionUiState.Success("Photo uploaded")
                    loadFieldVisits(ticketId, force = true)
                } else {
                    _fieldVisitActionState.value = FieldVisitActionUiState.Error(serverErrorMessage(resp) ?: friendlyHttpMessage(resp.code()))
                }
            } catch (e: Exception) {
                _pendingFieldActions.value = _pendingFieldActions.value + "photo:$fieldVisitId"
                _fieldVisitActionState.value = FieldVisitActionUiState.Error("No internet — queued. ${e.message ?: ""}")
            }
        }
    }

    fun retryPendingFieldWork(ticketId: Int) {
        if (_pendingFieldActions.value.isEmpty()) return
        _pendingFieldActions.value = emptyList()
        loadFieldVisits(ticketId)
    }

    fun loadFieldVisitPhoto(attachmentId: Int) {
        viewModelScope.launch {
            _fieldPhotoBase64.value = null
            try {
                val resp = repository.getFieldVisitPhoto(attachmentId, true)
                if (resp.isSuccessful) {
                    _fieldPhotoBase64.value = resp.body()?.base64
                } else {
                    _fieldPhotoBase64.value = null
                }
            } catch (_: Exception) {
                _fieldPhotoBase64.value = null
            }
        }
    }

    fun clearFieldPhoto() { _fieldPhotoBase64.value = null }

    fun loadFieldVisitSignature(fieldVisitId: Int) {
        viewModelScope.launch {
            _fieldSignatureBase64.value = null
            try {
                val resp = repository.getFieldVisitSignature(fieldVisitId)
                if (resp.isSuccessful) {
                    _fieldSignatureBase64.value = resp.body()?.base64
                } else {
                    _fieldSignatureBase64.value = null
                }
            } catch (_: Exception) { _fieldSignatureBase64.value = null }
        }
    }
    fun clearFieldSignature() { _fieldSignatureBase64.value = null }

    private fun handleActionResponse(response: Response<CallTicketActionResponse>) {
        if (response.isSuccessful) {
            val body = response.body()
            if (body != null && body.success) {
                _actionState.value = ActionUiState.Success(body.message ?: "Success")
            } else {
                _actionState.value = ActionUiState.Error(body?.message ?: "Action failed")
            }
        } else {
            _actionState.value = ActionUiState.Error(friendlyHttpMessage(response.code()))
        }
    }
}


private fun serverErrorMessage(response: Response<*>): String? {
    return try {
        val raw = response.errorBody()?.string()?.trim().orEmpty()
        if (raw.isBlank()) null else {
            val obj = JSONObject(raw)
            val msg = obj.optString("message").trim().takeIf { it.isNotBlank() }
                ?: obj.optString("error").trim().takeIf { it.isNotBlank() }
            msg
        }
    } catch (_: Exception) {
        null
    }
}

private fun friendlyHttpMessage(code: Int): String = when (code) {
    401 -> "Your ITCM session has expired. Please sign in again."
    403 -> "Your account is not authorized for mobile IT Call Monitoring."
    404 -> "The ticket could not be found. Refresh and try again."
    409 -> "This action is not allowed for the ticket's current status or assignment."
    503 -> "ITCM is temporarily unavailable. Try again in a moment."
    else -> "ITCM could not complete the request. Please try again."
}
