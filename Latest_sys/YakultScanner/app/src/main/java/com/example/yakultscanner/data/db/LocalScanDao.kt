package com.example.yakultscanner.data.db

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import kotlinx.coroutines.flow.Flow

@Dao
interface LocalScanDao {
    @Query(
        """
        SELECT s.*, COUNT(i.id) AS item_count,
               COALESCE(SUM(CASE WHEN i.sent = 1 THEN 1 ELSE 0 END), 0) AS sent_count
        FROM local_scan_sessions s
        LEFT JOIN local_scan_items i ON i.session_id = s.id
        GROUP BY s.id
        ORDER BY s.updated_at DESC
        """
    )
    fun observeSessions(): Flow<List<LocalScanSessionWithCount>>

    @Query("SELECT * FROM local_scan_sessions WHERE id = :sessionId LIMIT 1")
    suspend fun getSession(sessionId: String): LocalScanSessionEntity?

    @Query("SELECT * FROM local_scan_items WHERE session_id = :sessionId ORDER BY row_number ASC, created_at ASC")
    suspend fun getItems(sessionId: String): List<LocalScanItemEntity>

    @Query("SELECT * FROM local_scan_items WHERE session_id = :sessionId ORDER BY row_number ASC, created_at ASC")
    fun observeItems(sessionId: String): Flow<List<LocalScanItemEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertSession(session: LocalScanSessionEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertItems(items: List<LocalScanItemEntity>)

    @Transaction
    suspend fun replaceSession(session: LocalScanSessionEntity, items: List<LocalScanItemEntity>) {
        insertSession(session)
        deleteItems(session.id)
        insertItems(items)
    }

    @Query("DELETE FROM local_scan_items WHERE session_id = :sessionId")
    suspend fun deleteItems(sessionId: String)

    @Query("DELETE FROM local_scan_sessions WHERE id = :sessionId")
    suspend fun deleteSession(sessionId: String)

    @Query("UPDATE local_scan_items SET sent = 1, sent_at = :sentAt, last_error = NULL WHERE id = :itemId")
    suspend fun markItemSent(itemId: String, sentAt: Long)

    @Query("UPDATE local_scan_items SET last_error = :error WHERE id = :itemId")
    suspend fun markItemFailed(itemId: String, error: String?)

    @Query("UPDATE local_scan_sessions SET status = :status, sent_at = :sentAt, updated_at = :updatedAt WHERE id = :sessionId")
    suspend fun updateSessionSendStatus(sessionId: String, status: LocalScanSessionStatus, sentAt: Long?, updatedAt: Long)
}

