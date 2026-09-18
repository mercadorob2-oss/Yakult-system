package com.example.yakultscanner.data.db;

import androidx.room.Entity;
import androidx.room.PrimaryKey;
import androidx.room.ColumnInfo;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00002\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\n\n\u0002\u0010\t\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b1\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0087\b\u0018\u00002\u00020\u0001B\u00bf\u0001\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0003\u0012\u0006\u0010\t\u001a\u00020\u0003\u0012\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u0003\u0012\u0006\u0010\f\u001a\u00020\u0003\u0012\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u0003\u0012\b\b\u0002\u0010\u0010\u001a\u00020\u0011\u0012\b\b\u0002\u0010\u0012\u001a\u00020\u0006\u0012\b\b\u0002\u0010\u0013\u001a\u00020\u0014\u0012\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u0011\u00a2\u0006\u0004\b\u0017\u0010\u0018J\t\u00102\u001a\u00020\u0003H\u00c6\u0003J\t\u00103\u001a\u00020\u0003H\u00c6\u0003J\u0010\u00104\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003\u00a2\u0006\u0002\u0010\u001dJ\u0010\u00105\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003\u00a2\u0006\u0002\u0010\u001dJ\u000b\u00106\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\t\u00107\u001a\u00020\u0003H\u00c6\u0003J\u000b\u00108\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u00109\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\t\u0010:\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010;\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010<\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010=\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\t\u0010>\u001a\u00020\u0011H\u00c6\u0003J\t\u0010?\u001a\u00020\u0006H\u00c6\u0003J\t\u0010@\u001a\u00020\u0014H\u00c6\u0003J\u000b\u0010A\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u0010\u0010B\u001a\u0004\u0018\u00010\u0011H\u00c6\u0003\u00a2\u0006\u0002\u00100J\u00cc\u0001\u0010C\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\t\u001a\u00020\u00032\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\f\u001a\u00020\u00032\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\u0010\u001a\u00020\u00112\b\b\u0002\u0010\u0012\u001a\u00020\u00062\b\b\u0002\u0010\u0013\u001a\u00020\u00142\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u0011H\u00c6\u0001\u00a2\u0006\u0002\u0010DJ\u0013\u0010E\u001a\u00020F2\b\u0010G\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010H\u001a\u00020\u0006H\u00d6\u0001J\t\u0010I\u001a\u00020\u0003H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u001aR\u0016\u0010\u0004\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u001aR\u001a\u0010\u0005\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u001e\u001a\u0004\b\u001c\u0010\u001dR\u001a\u0010\u0007\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u001e\u001a\u0004\b\u001f\u0010\u001dR\u0018\u0010\b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b \u0010\u001aR\u0016\u0010\t\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b!\u0010\u001aR\u0018\u0010\n\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\"\u0010\u001aR\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010\u001aR\u0016\u0010\f\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b$\u0010\u001aR\u0018\u0010\r\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b%\u0010\u001aR\u0018\u0010\u000e\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b&\u0010\u001aR\u0018\u0010\u000f\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\'\u0010\u001aR\u0016\u0010\u0010\u001a\u00020\u00118\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b(\u0010)R\u0016\u0010\u0012\u001a\u00020\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b*\u0010+R\u0016\u0010\u0013\u001a\u00020\u00148\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b,\u0010-R\u0018\u0010\u0015\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b.\u0010\u001aR\u001a\u0010\u0016\u001a\u0004\u0018\u00010\u00118\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u00101\u001a\u0004\b/\u00100\u00a8\u0006J"}, d2 = {"Lcom/example/yakultscanner/data/db/PendingUpdateEntity;", "", "id", "", "setCode", "setId", "", "itemId", "itemType", "serialNumber", "modelNumber", "previousStatus", "newStatus", "remark", "updatedByUserId", "updatedByName", "createdAt", "", "retryCount", "syncStatus", "Lcom/example/yakultscanner/data/db/SyncStatus;", "lastError", "syncedAt", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JILcom/example/yakultscanner/data/db/SyncStatus;Ljava/lang/String;Ljava/lang/Long;)V", "getId", "()Ljava/lang/String;", "getSetCode", "getSetId", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getItemId", "getItemType", "getSerialNumber", "getModelNumber", "getPreviousStatus", "getNewStatus", "getRemark", "getUpdatedByUserId", "getUpdatedByName", "getCreatedAt", "()J", "getRetryCount", "()I", "getSyncStatus", "()Lcom/example/yakultscanner/data/db/SyncStatus;", "getLastError", "getSyncedAt", "()Ljava/lang/Long;", "Ljava/lang/Long;", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "component13", "component14", "component15", "component16", "component17", "copy", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JILcom/example/yakultscanner/data/db/SyncStatus;Ljava/lang/String;Ljava/lang/Long;)Lcom/example/yakultscanner/data/db/PendingUpdateEntity;", "equals", "", "other", "hashCode", "toString", "app_prodDebug"})
@androidx.room.Entity(tableName = "pending_updates")
public final class PendingUpdateEntity {
    @androidx.room.PrimaryKey()
    @androidx.room.ColumnInfo(name = "id")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String id = null;
    @androidx.room.ColumnInfo(name = "set_code")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String setCode = null;
    @androidx.room.ColumnInfo(name = "set_id")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer setId = null;
    @androidx.room.ColumnInfo(name = "item_id")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer itemId = null;
    @androidx.room.ColumnInfo(name = "item_type")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String itemType = null;
    @androidx.room.ColumnInfo(name = "serial_number")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String serialNumber = null;
    @androidx.room.ColumnInfo(name = "model_number")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String modelNumber = null;
    @androidx.room.ColumnInfo(name = "previous_status")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String previousStatus = null;
    @androidx.room.ColumnInfo(name = "new_status")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String newStatus = null;
    @androidx.room.ColumnInfo(name = "remark")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String remark = null;
    @androidx.room.ColumnInfo(name = "updated_by_user_id")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String updatedByUserId = null;
    @androidx.room.ColumnInfo(name = "updated_by_name")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String updatedByName = null;
    @androidx.room.ColumnInfo(name = "created_at")
    private final long createdAt = 0L;
    @androidx.room.ColumnInfo(name = "retry_count")
    private final int retryCount = 0;
    @androidx.room.ColumnInfo(name = "sync_status")
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.db.SyncStatus syncStatus = null;
    @androidx.room.ColumnInfo(name = "last_error")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String lastError = null;
    @androidx.room.ColumnInfo(name = "synced_at")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Long syncedAt = null;
    
    public PendingUpdateEntity(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.Integer setId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer itemId, @org.jetbrains.annotations.Nullable()
    java.lang.String itemType, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String modelNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String previousStatus, @org.jetbrains.annotations.NotNull()
    java.lang.String newStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String remark, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByName, long createdAt, int retryCount, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.SyncStatus syncStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String lastError, @org.jetbrains.annotations.Nullable()
    java.lang.Long syncedAt) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getId() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSetCode() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getSetId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getItemId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getItemType() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getModelNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getPreviousStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getNewStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getRemark() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUpdatedByUserId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUpdatedByName() {
        return null;
    }
    
    public final long getCreatedAt() {
        return 0L;
    }
    
    public final int getRetryCount() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.SyncStatus getSyncStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLastError() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long getSyncedAt() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component10() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component11() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component12() {
        return null;
    }
    
    public final long component13() {
        return 0L;
    }
    
    public final int component14() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.SyncStatus component15() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component16() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long component17() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component5() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.PendingUpdateEntity copy(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.Integer setId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer itemId, @org.jetbrains.annotations.Nullable()
    java.lang.String itemType, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String modelNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String previousStatus, @org.jetbrains.annotations.NotNull()
    java.lang.String newStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String remark, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByName, long createdAt, int retryCount, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.SyncStatus syncStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String lastError, @org.jetbrains.annotations.Nullable()
    java.lang.Long syncedAt) {
        return null;
    }
    
    @java.lang.Override()
    public boolean equals(@org.jetbrains.annotations.Nullable()
    java.lang.Object other) {
        return false;
    }
    
    @java.lang.Override()
    public int hashCode() {
        return 0;
    }
    
    @java.lang.Override()
    @org.jetbrains.annotations.NotNull()
    public java.lang.String toString() {
        return null;
    }
}