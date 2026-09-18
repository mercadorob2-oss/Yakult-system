package com.example.yakultscanner.data.db;

import androidx.room.Entity;
import androidx.room.PrimaryKey;
import androidx.room.ColumnInfo;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\u0007\b\u0086\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005j\u0002\b\u0006j\u0002\b\u0007\u00a8\u0006\b"}, d2 = {"Lcom/example/yakultscanner/data/db/SyncStatus;", "", "<init>", "(Ljava/lang/String;I)V", "PENDING", "SYNCING", "SYNCED", "FAILED", "app_prodDebug"})
public enum SyncStatus {
    /*public static final*/ PENDING /* = new PENDING() */,
    /*public static final*/ SYNCING /* = new SYNCING() */,
    /*public static final*/ SYNCED /* = new SYNCED() */,
    /*public static final*/ FAILED /* = new FAILED() */;
    
    SyncStatus() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.data.db.SyncStatus> getEntries() {
        return null;
    }
}