package com.example.yakultscanner.data.repository

import com.example.yakultscanner.api.ApiResult
import com.example.yakultscanner.api.SetUpdatesRequest
import com.example.yakultscanner.api.YakultApiService
import com.example.yakultscanner.api.safeApiCall
import com.example.yakultscanner.data.db.PendingUpdateEntity
import com.example.yakultscanner.data.db.SyncStatus
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject
import javax.inject.Singleton

sealed class SyncResult {
    data class Success(val uploadedCount: Int, val failedCount: Int) : SyncResult()
    data class PartialSuccess(val uploadedCount: Int, val failedCount: Int, val errors: List<String>) : SyncResult()
    data class Error(val message: String) : SyncResult()
    object NoPendingUpdates : SyncResult()
    object NetworkUnavailable : SyncResult()
}

@Singleton
class SyncRepository @Inject constructor(
    private val offlineRepository: OfflineRepository,
    private val apiService: YakultApiService
) {
    private val _isSyncing = MutableStateFlow(false)
    val isSyncing: StateFlow<Boolean> = _isSyncing.asStateFlow()

    private val _lastSyncResult = MutableStateFlow<SyncResult?>(null)
    val lastSyncResult: StateFlow<SyncResult?> = _lastSyncResult.asStateFlow()

    val pendingCountFlow: Flow<Int> = offlineRepository.getPendingCountFlow()

    suspend fun syncPendingUpdates(user: String?): SyncResult {
        val pending = offlineRepository.getPendingUpdates()
        
        if (pending.isEmpty()) {
            return SyncResult.NoPendingUpdates
        }

        _isSyncing.value = true
        var uploadedCount = 0
        var failedCount = 0
        val errors = mutableListOf<String>()

        try {
            // Group by setCode to batch uploads
            val groupedBySet = pending.groupBy { it.setCode }

            groupedBySet.forEach { (setCode, updates) ->
                val entries = updates.map { entity ->
                    offlineRepository.convertToApiEntry(entity)
                }

                val request = SetUpdatesRequest(
                    setCode = setCode,
                    updatedByUser = user ?: "",
                    updates = entries
                )

                // Mark all entities in this group as SYNCING so the UI can
                // show an in-flight state, and so a crash mid-upload can be
                // detected on next launch (SYNCING is included in getPendingOrFailed).
                updates.forEach { entity ->
                    offlineRepository.markAsSyncing(entity.id)
                }

                when (val result = safeApiCall { apiService.uploadSetUpdates(request) }) {
                    is ApiResult.Success -> {
                        if (result.data.success) {
                            // Mark all as synced
                            updates.forEach { entity ->
                                offlineRepository.markAsSynced(entity.id)
                            }
                            uploadedCount += updates.size
                        } else {
                            // API returned error
                            updates.forEach { entity ->
                                offlineRepository.markAsFailed(entity.id, result.data.message)
                            }
                            failedCount += updates.size
                            errors.add("Set $setCode: ${result.data.message}")
                        }
                    }
                    is ApiResult.NetworkError -> {
                        _isSyncing.value = false
                        return SyncResult.NetworkUnavailable
                    }
                    is ApiResult.HttpError -> {
                        updates.forEach { entity ->
                            offlineRepository.markAsFailed(entity.id, "HTTP ${result.code}: ${result.message}")
                        }
                        failedCount += updates.size
                        errors.add("Set $setCode: HTTP ${result.code}")
                    }
                    is ApiResult.UnknownError -> {
                        updates.forEach { entity ->
                            offlineRepository.markAsFailed(entity.id, result.message)
                        }
                        failedCount += updates.size
                        errors.add("Set $setCode: ${result.message}")
                    }
                }
            }

            val result = when {
                failedCount == 0 -> SyncResult.Success(uploadedCount, 0)
                uploadedCount > 0 -> SyncResult.PartialSuccess(uploadedCount, failedCount, errors)
                else -> SyncResult.Error("All uploads failed: ${errors.firstOrNull()}")
            }

            _lastSyncResult.value = result
            return result

        } finally {
            _isSyncing.value = false
        }
    }

    suspend fun retryFailedUpdates(user: String?): SyncResult {
        val failed = offlineRepository.getPendingUpdates()
            .filter { it.retryCount > 0 || it.syncStatus == SyncStatus.FAILED }
        
        if (failed.isEmpty()) {
            return SyncResult.NoPendingUpdates
        }

        return syncPendingUpdates(user)
    }

    suspend fun getPendingCount(): Int {
        return offlineRepository.getPendingCount()
    }

    fun clearLastResult() {
        _lastSyncResult.value = null
    }
}
