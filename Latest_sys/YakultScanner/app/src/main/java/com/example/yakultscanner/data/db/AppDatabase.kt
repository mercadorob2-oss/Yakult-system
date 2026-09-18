package com.example.yakultscanner.data.db

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverter
import androidx.room.TypeConverters

class Converters {
    companion object {
        @JvmStatic
        @TypeConverter
        fun fromSyncStatus(status: SyncStatus): String = status.name

        @JvmStatic
        @TypeConverter
        fun toSyncStatus(status: String): SyncStatus = SyncStatus.valueOf(status)

        @JvmStatic
        @TypeConverter
        fun fromLocalScanSessionStatus(status: LocalScanSessionStatus): String = status.name

        @JvmStatic
        @TypeConverter
        fun toLocalScanSessionStatus(status: String): LocalScanSessionStatus = LocalScanSessionStatus.valueOf(status)
    }
}

@Database(
    entities = [PendingUpdateEntity::class, LocalScanSessionEntity::class, LocalScanItemEntity::class],
    version = 2,
    exportSchema = true   // Generates schemas/<version>.json — commit these files to track schema history.
)
@TypeConverters(Converters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun pendingUpdateDao(): PendingUpdateDao
    abstract fun localScanDao(): LocalScanDao
}


