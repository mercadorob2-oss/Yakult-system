package com.example.yakultscanner.data.db;

import androidx.room.ColumnInfo;
import androidx.room.Entity;
import androidx.room.ForeignKey;
import androidx.room.Index;
import androidx.room.PrimaryKey;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\u0006\b\u0086\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005j\u0002\b\u0006\u00a8\u0006\u0007"}, d2 = {"Lcom/example/yakultscanner/data/db/LocalScanSessionStatus;", "", "<init>", "(Ljava/lang/String;I)V", "DRAFT", "SENT", "PARTIAL_SENT", "app_prodDebug"})
public enum LocalScanSessionStatus {
    /*public static final*/ DRAFT /* = new DRAFT() */,
    /*public static final*/ SENT /* = new SENT() */,
    /*public static final*/ PARTIAL_SENT /* = new PARTIAL_SENT() */;
    
    LocalScanSessionStatus() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.data.db.LocalScanSessionStatus> getEntries() {
        return null;
    }
}