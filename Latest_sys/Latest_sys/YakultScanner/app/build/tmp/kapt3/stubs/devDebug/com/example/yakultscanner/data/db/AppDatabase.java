package com.example.yakultscanner.data.db;

import androidx.room.Database;
import androidx.room.RoomDatabase;
import androidx.room.TypeConverter;
import androidx.room.TypeConverters;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0018\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\b\'\u0018\u00002\u00020\u0001B\u0007\u00a2\u0006\u0004\b\u0002\u0010\u0003J\b\u0010\u0004\u001a\u00020\u0005H&J\b\u0010\u0006\u001a\u00020\u0007H&\u00a8\u0006\b"}, d2 = {"Lcom/example/yakultscanner/data/db/AppDatabase;", "Landroidx/room/RoomDatabase;", "<init>", "()V", "pendingUpdateDao", "Lcom/example/yakultscanner/data/db/PendingUpdateDao;", "localScanDao", "Lcom/example/yakultscanner/data/db/LocalScanDao;", "app_devDebug"})
@androidx.room.Database(entities = {com.example.yakultscanner.data.db.PendingUpdateEntity.class, com.example.yakultscanner.data.db.LocalScanSessionEntity.class, com.example.yakultscanner.data.db.LocalScanItemEntity.class}, version = 2, exportSchema = true)
@androidx.room.TypeConverters(value = {com.example.yakultscanner.data.db.Converters.class})
public abstract class AppDatabase extends androidx.room.RoomDatabase {
    
    public AppDatabase() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public abstract com.example.yakultscanner.data.db.PendingUpdateDao pendingUpdateDao();
    
    @org.jetbrains.annotations.NotNull()
    public abstract com.example.yakultscanner.data.db.LocalScanDao localScanDao();
}