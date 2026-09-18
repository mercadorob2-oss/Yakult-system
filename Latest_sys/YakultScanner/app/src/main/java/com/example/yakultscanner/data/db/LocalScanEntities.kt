package com.example.yakultscanner.data.db

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

enum class LocalScanSessionStatus {
    DRAFT,
    SENT,
    PARTIAL_SENT
}

@Entity(tableName = "local_scan_sessions")
data class LocalScanSessionEntity(
    @PrimaryKey @ColumnInfo(name = "id") val id: String = java.util.UUID.randomUUID().toString(),
    @ColumnInfo(name = "title") val title: String,
    @ColumnInfo(name = "created_by") val createdBy: String? = null,
    @ColumnInfo(name = "created_at") val createdAt: Long = System.currentTimeMillis(),
    @ColumnInfo(name = "updated_at") val updatedAt: Long = System.currentTimeMillis(),
    @ColumnInfo(name = "notes") val notes: String? = null,
    @ColumnInfo(name = "status") val status: LocalScanSessionStatus = LocalScanSessionStatus.DRAFT,
    @ColumnInfo(name = "sent_at") val sentAt: Long? = null
)

@Entity(
    tableName = "local_scan_items",
    foreignKeys = [
        ForeignKey(
            entity = LocalScanSessionEntity::class,
            parentColumns = ["id"],
            childColumns = ["session_id"],
            onDelete = ForeignKey.CASCADE
        )
    ],
    indices = [Index("session_id")]
)
data class LocalScanItemEntity(
    @PrimaryKey @ColumnInfo(name = "id") val id: String = java.util.UUID.randomUUID().toString(),
    @ColumnInfo(name = "session_id") val sessionId: String,
    @ColumnInfo(name = "row_number") val rowNumber: Int,
    @ColumnInfo(name = "serial_number") val serialNumber: String,
    @ColumnInfo(name = "cell_phone_number") val cellPhoneNumber: String? = null,
    @ColumnInfo(name = "imei1") val imei1: String? = null,
    @ColumnInfo(name = "imei2") val imei2: String? = null,
    @ColumnInfo(name = "source") val source: String = "Manual",
    @ColumnInfo(name = "created_at") val createdAt: Long = System.currentTimeMillis(),
    @ColumnInfo(name = "sent") val sent: Boolean = false,
    @ColumnInfo(name = "sent_at") val sentAt: Long? = null,
    @ColumnInfo(name = "last_error") val lastError: String? = null
)

data class LocalScanSessionWithCount(
    @ColumnInfo(name = "id") val id: String,
    @ColumnInfo(name = "title") val title: String,
    @ColumnInfo(name = "created_by") val createdBy: String?,
    @ColumnInfo(name = "created_at") val createdAt: Long,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
    @ColumnInfo(name = "notes") val notes: String?,
    @ColumnInfo(name = "status") val status: LocalScanSessionStatus,
    @ColumnInfo(name = "sent_at") val sentAt: Long?,
    @ColumnInfo(name = "item_count") val itemCount: Int,
    @ColumnInfo(name = "sent_count") val sentCount: Int
)
