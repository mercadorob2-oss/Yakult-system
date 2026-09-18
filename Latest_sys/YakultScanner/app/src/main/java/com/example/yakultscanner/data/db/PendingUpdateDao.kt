package com.example.yakultscanner.data.db

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Dao
interface PendingUpdateDao {
    @Query("SELECT * FROM pending_updates WHERE sync_status = :status ORDER BY created_at ASC")
    suspend fun getByStatus(status: SyncStatus): List<PendingUpdateEntity>

    @Query("SELECT * FROM pending_updates WHERE sync_status IN ('PENDING', 'SYNCING', 'FAILED') ORDER BY created_at ASC")
    suspend fun getPendingOrFailed(): List<PendingUpdateEntity>

    @Query("SELECT * FROM pending_updates WHERE set_code = :setCode ORDER BY created_at DESC")
    suspend fun getBySetCode(setCode: String): List<PendingUpdateEntity>

    @Query("SELECT COUNT(*) FROM pending_updates WHERE sync_status IN ('PENDING', 'SYNCING', 'FAILED')")
    fun getPendingCountFlow(): Flow<Int>

    @Query("SELECT COUNT(*) FROM pending_updates WHERE sync_status IN ('PENDING', 'SYNCING', 'FAILED')")
    suspend fun getPendingCount(): Int

    @Insert
    suspend fun insert(entity: PendingUpdateEntity): Long

    @Insert
    suspend fun insertAll(entities: List<PendingUpdateEntity>)

    @Update
    suspend fun update(entity: PendingUpdateEntity)

    @Delete
    suspend fun delete(entity: PendingUpdateEntity)

    @Query("DELETE FROM pending_updates WHERE id = :id")
    suspend fun deleteById(id: String)

    @Query("DELETE FROM pending_updates WHERE sync_status = 'SYNCED'")
    suspend fun deleteSynced()

    @Query("UPDATE pending_updates SET sync_status = 'SYNCING' WHERE id = :id")
    suspend fun markSyncing(id: String)

    @Query("UPDATE pending_updates SET retry_count = retry_count + 1, last_error = :error, sync_status = :status WHERE id = :id")
    suspend fun markFailed(id: String, error: String?, status: SyncStatus)

    @Query("UPDATE pending_updates SET sync_status = 'SYNCED', synced_at = :timestamp WHERE id = :id")
    suspend fun markSynced(id: String, timestamp: Long)

    @Query("SELECT * FROM pending_updates")
    suspend fun getAll(): List<PendingUpdateEntity>
}
