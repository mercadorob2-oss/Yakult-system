package com.example.yakultscanner.data.repository

import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.SerialFromMobileRequest
import com.example.yakultscanner.data.db.LocalScanDao
import com.example.yakultscanner.data.db.LocalScanItemEntity
import com.example.yakultscanner.data.db.LocalScanSessionEntity
import com.example.yakultscanner.data.db.LocalScanSessionStatus
import com.example.yakultscanner.data.db.LocalScanSessionWithCount
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

data class LocalScanSendResult(
    val successCount: Int,
    val failedCount: Int
)

@Singleton
class LocalScanRepository @Inject constructor(
    private val dao: LocalScanDao
) {
    fun observeSessions(): Flow<List<LocalScanSessionWithCount>> = dao.observeSessions()

    fun observeItems(sessionId: String): Flow<List<LocalScanItemEntity>> = dao.observeItems(sessionId)

    suspend fun getSession(sessionId: String): LocalScanSessionEntity? = dao.getSession(sessionId)

    suspend fun getItems(sessionId: String): List<LocalScanItemEntity> = dao.getItems(sessionId)

    suspend fun saveSession(
        sessionId: String?,
        title: String,
        createdBy: String?,
        notes: String?,
        rows: List<LocalScanItemEntity>
    ): String {
        val now = System.currentTimeMillis()
        val id = sessionId ?: java.util.UUID.randomUUID().toString()
        val existing = sessionId?.let { dao.getSession(it) }
        val session = LocalScanSessionEntity(
            id = id,
            title = title.ifBlank { "Local Scan ${java.text.SimpleDateFormat("yyyy-MM-dd HH:mm", java.util.Locale.getDefault()).format(java.util.Date(now))}" },
            createdBy = createdBy,
            createdAt = existing?.createdAt ?: now,
            updatedAt = now,
            notes = notes?.ifBlank { null },
            status = existing?.status ?: LocalScanSessionStatus.DRAFT,
            sentAt = existing?.sentAt
        )
        dao.replaceSession(
            session,
            rows.mapIndexed { index, row ->
                row.copy(
                    id = if (row.id.isBlank()) java.util.UUID.randomUUID().toString() else row.id,
                    sessionId = id,
                    rowNumber = index + 1
                )
            }
        )
        return id
    }

    suspend fun deleteSession(sessionId: String) {
        dao.deleteSession(sessionId)
    }

    suspend fun sendSession(sessionId: String): LocalScanSendResult {
        val items = dao.getItems(sessionId).filter { !it.sent && it.serialNumber.isNotBlank() }
        var success = 0
        var failed = 0
        val sentAt = System.currentTimeMillis()

        for (item in items) {
            try {
                val response = ApiClient.service.sendSerialToWindows(
                    SerialFromMobileRequest(
                        serialNumber = item.serialNumber,
                        cellPhoneNumber = item.cellPhoneNumber?.ifBlank { null },
                        imei1 = item.imei1?.ifBlank { null },
                        imei2 = item.imei2?.ifBlank { null }
                    )
                )
                if (response.isSuccessful && response.body()?.success == true) {
                    dao.markItemSent(item.id, sentAt)
                    success++
                } else {
                    dao.markItemFailed(item.id, response.body()?.message ?: "Send failed")
                    failed++
                }
            } catch (ex: Exception) {
                dao.markItemFailed(item.id, ex.message ?: "Network error")
                failed++
            }
        }

        val status = when {
            success > 0 && failed == 0 -> LocalScanSessionStatus.SENT
            success > 0 -> LocalScanSessionStatus.PARTIAL_SENT
            else -> LocalScanSessionStatus.DRAFT
        }
        dao.updateSessionSendStatus(
            sessionId = sessionId,
            status = status,
            sentAt = if (success > 0) sentAt else null,
            updatedAt = System.currentTimeMillis()
        )
        return LocalScanSendResult(success, failed)
    }
}
