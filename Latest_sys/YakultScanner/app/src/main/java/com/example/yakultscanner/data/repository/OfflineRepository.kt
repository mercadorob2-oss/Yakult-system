package com.example.yakultscanner.data.repository

import com.example.yakultscanner.data.db.PendingUpdateDao
import com.example.yakultscanner.data.db.PendingUpdateEntity
import com.example.yakultscanner.data.db.SyncStatus
import com.example.yakultscanner.api.SetItemUpdateEntry
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class OfflineRepository @Inject constructor(
    private val pendingUpdateDao: PendingUpdateDao
) {
    suspend fun savePendingUpdate(
        setCode: String,
        entry: SetItemUpdateEntry,
        updatedByUser: String? = null,
        updatedByName: String? = null
    ): String {
        val normalizedNewStatus = entry.newStatus ?: entry.previousStatus.orEmpty()
        val entity = PendingUpdateEntity(
            setCode = setCode,
            itemType = entry.itemType,
            serialNumber = entry.itemSerialNumber,
            modelNumber = entry.itemModelNumber,
            previousStatus = entry.previousStatus,
            newStatus = normalizedNewStatus,
            remark = entry.remark,
            updatedByUserId = updatedByUser,
            updatedByName = updatedByName,
            syncStatus = SyncStatus.PENDING
        )
        pendingUpdateDao.insert(entity)
        return entity.id
    }

    suspend fun savePendingUpdates(
        setCode: String,
        entries: List<SetItemUpdateEntry>,
        updatedByUser: String? = null,
        updatedByName: String? = null
    ): List<String> {
        val entities = entries.map { entry ->
            val normalizedNewStatus = entry.newStatus ?: entry.previousStatus.orEmpty()
            PendingUpdateEntity(
                setCode = setCode,
                itemType = entry.itemType,
                serialNumber = entry.itemSerialNumber,
                modelNumber = entry.itemModelNumber,
                previousStatus = entry.previousStatus,
                newStatus = normalizedNewStatus,
                remark = entry.remark,
                updatedByUserId = updatedByUser,
                updatedByName = updatedByName,
                syncStatus = SyncStatus.PENDING
            )
        }
        pendingUpdateDao.insertAll(entities)
        return entities.map { it.id }
    }

    suspend fun getPendingUpdates(): List<PendingUpdateEntity> {
        return pendingUpdateDao.getPendingOrFailed()
    }

    suspend fun getPendingUpdatesBySetCode(setCode: String): List<PendingUpdateEntity> {
        return pendingUpdateDao.getBySetCode(setCode)
    }

    fun getPendingCountFlow(): Flow<Int> {
        return pendingUpdateDao.getPendingCountFlow()
    }

    suspend fun getPendingCount(): Int {
        return pendingUpdateDao.getPendingCount()
    }

    suspend fun markAsSyncing(id: String) {
        pendingUpdateDao.markSyncing(id)
    }

    suspend fun markAsSynced(id: String) {
        pendingUpdateDao.markSynced(id, System.currentTimeMillis())
    }

    suspend fun markAsFailed(id: String, error: String?) {
        pendingUpdateDao.markFailed(id, error, SyncStatus.FAILED)
    }

    suspend fun deletePendingUpdate(id: String) {
        pendingUpdateDao.deleteById(id)
    }

    suspend fun clearSynced() {
        pendingUpdateDao.deleteSynced()
    }

    suspend fun convertToApiEntry(entity: PendingUpdateEntity): SetItemUpdateEntry {
        return SetItemUpdateEntry(
            itemSerialNumber = entity.serialNumber,
            itemModelNumber = entity.modelNumber,
            itemType = entity.itemType,
            previousStatus = entity.previousStatus,
            newStatus = entity.newStatus,
            remark = entity.remark
        )
    }
}
