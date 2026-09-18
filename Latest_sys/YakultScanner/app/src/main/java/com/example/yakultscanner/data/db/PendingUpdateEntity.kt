package com.example.yakultscanner.data.db

import androidx.room.Entity
import androidx.room.PrimaryKey
import androidx.room.ColumnInfo

enum class SyncStatus {
    PENDING,
    SYNCING,
    SYNCED,
    FAILED
}

@Entity(tableName = "pending_updates")
data class PendingUpdateEntity(
    @PrimaryKey @ColumnInfo(name = "id") val id: String = java.util.UUID.randomUUID().toString(),
    @ColumnInfo(name = "set_code") val setCode: String,
    @ColumnInfo(name = "set_id") val setId: Int? = null,
    @ColumnInfo(name = "item_id") val itemId: Int? = null,
    @ColumnInfo(name = "item_type") val itemType: String? = null,
    @ColumnInfo(name = "serial_number") val serialNumber: String,
    @ColumnInfo(name = "model_number") val modelNumber: String? = null,
    @ColumnInfo(name = "previous_status") val previousStatus: String? = null,
    @ColumnInfo(name = "new_status") val newStatus: String,
    @ColumnInfo(name = "remark") val remark: String? = null,
    @ColumnInfo(name = "updated_by_user_id") val updatedByUserId: String? = null,
    @ColumnInfo(name = "updated_by_name") val updatedByName: String? = null,
    @ColumnInfo(name = "created_at") val createdAt: Long = System.currentTimeMillis(),
    @ColumnInfo(name = "retry_count") val retryCount: Int = 0,
    @ColumnInfo(name = "sync_status") val syncStatus: SyncStatus = SyncStatus.PENDING,
    @ColumnInfo(name = "last_error") val lastError: String? = null,
    @ColumnInfo(name = "synced_at") val syncedAt: Long? = null
)
