package com.example.yakultscanner.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.SCANNER_GENERIC_MESSAGE
import com.example.yakultscanner.api.SCANNER_NETWORK_MESSAGE
import com.example.yakultscanner.api.SCANNER_UNSUPPORTED_QR_MESSAGE
import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SetItemUpdateDto
import com.example.yakultscanner.api.SetItemUpdateEntry
import com.example.yakultscanner.api.SetUpdatesRequest
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.data.repository.OfflineRepository
import com.example.yakultscanner.data.repository.SyncRepository
import com.example.yakultscanner.data.repository.SyncResult
import com.example.yakultscanner.data.model.DispatchSet
import com.example.yakultscanner.data.model.ItemEditState
import com.google.gson.Gson
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.net.URLDecoder
import java.nio.charset.StandardCharsets
import com.example.yakultscanner.network.ConnectivityMonitor
import com.example.yakultscanner.utils.normalizeSerial

import com.example.yakultscanner.api.YakultApiService
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

@HiltViewModel
class DispatchDetailsViewModel @Inject constructor(
    private val apiService: YakultApiService,
    private val offlineRepository: OfflineRepository,
    private val syncRepository: SyncRepository,
    val connectivityMonitor: ConnectivityMonitor
) : ViewModel() {

    private val _uiState = MutableStateFlow<DispatchDetailsUiState>(DispatchDetailsUiState.Loading)
    val uiState: StateFlow<DispatchDetailsUiState> = _uiState.asStateFlow()

    private val _itemEdits = MutableStateFlow<List<ItemEditState>>(emptyList())
    val itemEdits: StateFlow<List<ItemEditState>> = _itemEdits.asStateFlow()

    private val _updatesBySerial = MutableStateFlow<Map<String, SetItemUpdateDto>>(emptyMap())
    val updatesBySerial: StateFlow<Map<String, SetItemUpdateDto>> = _updatesBySerial.asStateFlow()

    private val _uploadEvent = MutableStateFlow<UploadEvent?>(null)
    val uploadEvent: StateFlow<UploadEvent?> = _uploadEvent.asStateFlow()
    
    private val _isUploading = MutableStateFlow(false)
    val isUploading: StateFlow<Boolean> = _isUploading.asStateFlow()

    val isSyncing: StateFlow<Boolean> = syncRepository.isSyncing
    private val _pendingCount = MutableStateFlow(0)
    val pendingCount: StateFlow<Int> = _pendingCount.asStateFlow()
    val lastSyncResult: StateFlow<SyncResult?> = syncRepository.lastSyncResult

    init {
        viewModelScope.launch {
            offlineRepository.getPendingCountFlow().collect { count ->
                _pendingCount.value = count
            }
        }
    }
    
    fun loadDispatchSet(scannedString: String) {
        viewModelScope.launch {
            try {
                if (scannedString.isBlank()) {
                    _uiState.value = DispatchDetailsUiState.Error("No QR code data was received. Please scan again.")
                    return@launch
                }

                val decodedValue = URLDecoder.decode(scannedString, StandardCharsets.UTF_8.toString()).trim()
                val dispatchSet = parseDispatchSet(decodedValue)
                val token = extractSetToken(decodedValue)
                val setCode = dispatchSet?.setCode?.trim()

                when {
                    !setCode.isNullOrBlank() -> {
                        fetchDispatchSetFromApi(setCode = setCode, fallback = dispatchSet)
                    }
                    !token.isNullOrBlank() -> {
                        fetchDispatchSetFromApi(token = token, fallback = dispatchSet)
                    }
                    else -> {
                        fetchDispatchSetFromApi(setCode = decodedValue)
                    }
                }
            } catch (e: Exception) {
                _uiState.value = DispatchDetailsUiState.Error(SCANNER_GENERIC_MESSAGE)
            }
        }
    }

    private fun fetchDispatchSetFromApi(
        setCode: String? = null,
        token: String? = null,
        fallback: DispatchSet? = null
    ) {
        viewModelScope.launch {
            when (val result = safeApiCall { apiService.getDispatchSet(setCode = setCode, token = token) }) {
                is ApiResult.Success -> {
                    val data = result.data
                    if (data.success && data.set != null && !data.set.setCode.isNullOrBlank()) {
                        showDispatchSet(data.set)
                    } else {
                        showFallbackOrError(fallback, "Set not found on server.")
                    }
                }
                is ApiResult.HttpError -> showFallbackOrError(
                    fallback,
                    if (result.code == 404) "Set not found." else result.message ?: SCANNER_GENERIC_MESSAGE
                )
                is ApiResult.NetworkError -> showFallbackOrError(fallback, SCANNER_NETWORK_MESSAGE)
                is ApiResult.UnknownError -> showFallbackOrError(fallback, SCANNER_GENERIC_MESSAGE)
            }
        }
    }

    private fun parseDispatchSet(value: String): DispatchSet? {
        return try {
            Gson().fromJson(value, DispatchSet::class.java)
        } catch (_: Exception) {
            null
        }
    }

    private fun extractSetToken(value: String): String? {
        val trimmed = value.trim()
        val prefix = "yakult:set:v1:"
        val token = if (trimmed.startsWith(prefix, ignoreCase = true)) {
            trimmed.substring(prefix.length).trim()
        } else {
            trimmed
        }
        val guidPattern = Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
        return token.takeIf { guidPattern.matches(it) }
    }

    private fun showDispatchSet(dispatchSet: DispatchSet) {
        _uiState.value = DispatchDetailsUiState.Success(dispatchSet)
        initializeEdits(dispatchSet)
        dispatchSet.setCode?.takeIf { it.isNotBlank() }?.let { loadPendingUpdatesForSet(it) }
    }

    private fun showFallbackOrError(fallback: DispatchSet?, message: String) {
        if (fallback != null && !fallback.setCode.isNullOrBlank()) {
            showDispatchSet(fallback)
        } else {
            _uiState.value = DispatchDetailsUiState.Error(message)
        }
    }

    private fun initializeEdits(dispatchSet: DispatchSet) {
        val edits = dispatchSet.items?.map { item ->
            ItemEditState(
                id = item.id,
                base = item,
                statusInput = item.status ?: "",
                remarkInput = "",
                isEditing = false
            )
        } ?: emptyList()
        _itemEdits.value = edits
    }

    private fun fetchUpdates(setCode: String?) {
        if (setCode.isNullOrBlank()) return

        viewModelScope.launch {
            when (val result = safeApiCall { apiService.getSetUpdates(setCode) }) {
                is ApiResult.Success -> {
                    val map = result.data
                        .mapNotNull { dto ->
                            val key = normalizeSerial(dto.serialNumber)
                            if (key.isEmpty()) null else key to dto
                        }
                        .groupBy({ it.first }, { it.second })
                        .mapValues { (_, list) -> list.first() }
                    _updatesBySerial.value = map
                }
                is ApiResult.HttpError -> {
                    // We don't block the UI, just log or maybe show a subtle error
                    // For now, we just don't update the map
                }
                else -> { /* Ignore network errors for background check */ }
            }
        }
    }

    private fun loadPendingUpdatesForSet(setCode: String) {
        viewModelScope.launch {
            val pending = offlineRepository.getPendingUpdatesBySetCode(setCode)
            // Could update UI to show which items have pending local changes
        }
    }

    fun updateItemEdit(updated: ItemEditState) {
        _itemEdits.value = _itemEdits.value.map { existing ->
            if (existing.id == updated.id) updated else existing
        }
    }

    fun uploadChanges(dispatchSet: DispatchSet) {
        val user = UserSession.currentUser
        if (user == null) {
            _uploadEvent.value = UploadEvent.Error(
                message = "Please log in before uploading.",
                details = "Type: Not logged in"
            )
            return
        }

        val itemsToUpload = _itemEdits.value.filter { edit ->
            val serialKey = normalizeSerial(edit.base.serialNumber)
            val processed = serialKey.isNotEmpty() && _updatesBySerial.value[serialKey]?.processed == true
            !processed && (edit.remarkInput.isNotBlank() || edit.statusInput != (edit.base.status ?: ""))
        }

        if (itemsToUpload.isEmpty()) {
            _uploadEvent.value = UploadEvent.Error(
                message = "No item changes to upload.",
                details = "Nothing to upload for this set."
            )
            return
        }

        val setCode = dispatchSet.setCode
        if (setCode.isNullOrBlank()) {
            _uploadEvent.value = UploadEvent.Error(
                message = "Set code is missing.",
                details = "Set code was blank in the scanned payload."
            )
            return
        }

        viewModelScope.launch {
            _isUploading.value = true

            val entries = itemsToUpload.map { edit ->
                SetItemUpdateEntry(
                    itemSerialNumber = edit.base.serialNumber ?: "",
                    itemModelNumber = edit.base.modelNumber,
                    previousStatus = edit.base.status,
                    newStatus = edit.statusInput.ifBlank { edit.base.status ?: "" },
                    remark = edit.remarkInput.ifBlank { null },
                    itemType = edit.base.type ?: edit.base.description
                )
            }

            // Try online first
            val request = SetUpdatesRequest(
                setCode = setCode,
                updatedByUser = user.username,
                updates = entries
            )

            when (val result = safeApiCall { apiService.uploadSetUpdates(request) }) {
                is ApiResult.Success -> {
                    if (result.data.success) {
                        _uploadEvent.value = UploadEvent.Success(
                            count = result.data.insertedCount ?: 0,
                            savedLocally = false
                        )
                        syncLocalStateAfterUpload(itemsToUpload)
                        fetchUpdates(setCode)
                    } else {
                        // API rejected, save locally for retry
                        savePendingAndNotify(setCode, entries, user.username, user.displayName)
                    }
                }
                is ApiResult.NetworkError -> {
                    // No network, save locally
                    savePendingAndNotify(setCode, entries, user.username, user.displayName)
                }
                is ApiResult.HttpError, is ApiResult.UnknownError -> {
                    // Server error, save locally
                    savePendingAndNotify(setCode, entries, user.username, user.displayName)
                }
            }
            _isUploading.value = false
        }
    }

    private suspend fun savePendingAndNotify(
        setCode: String,
        entries: List<SetItemUpdateEntry>,
        userId: String?,
        userName: String?
    ) {
        offlineRepository.savePendingUpdates(setCode, entries, userId, userName)
        _uploadEvent.value = UploadEvent.Success(
            count = entries.size,
            savedLocally = true
        )
    }

    private fun syncLocalStateAfterUpload(itemsToUpload: List<ItemEditState>) {
        _itemEdits.value = _itemEdits.value.map { current ->
            val matchingUpload = itemsToUpload.firstOrNull { it.id == current.id }
            if (matchingUpload != null) {
                val effectiveNewStatus = matchingUpload.statusInput.ifBlank { matchingUpload.base.status ?: "" }
                current.copy(
                    base = current.base.copy(status = effectiveNewStatus),
                    statusInput = effectiveNewStatus,
                    remarkInput = "",
                    isEditing = false
                )
            } else {
                current
            }
        }
    }

    fun syncNow() {
        viewModelScope.launch {
            val user = UserSession.currentUser?.username
            syncRepository.syncPendingUpdates(user)
        }
    }
    
    fun clearUploadEvent() {
        _uploadEvent.value = null
        syncRepository.clearLastResult()
    }
}

sealed class DispatchDetailsUiState {
    data object Loading : DispatchDetailsUiState()
    data class Success(val dispatchSet: DispatchSet) : DispatchDetailsUiState()
    data class Error(val message: String) : DispatchDetailsUiState()
}

sealed class UploadEvent {
    data class Success(val count: Int, val savedLocally: Boolean = false) : UploadEvent()
    data class Error(val message: String, val details: String) : UploadEvent()
}
