package com.example.yakultscanner.data.db;

import androidx.room.ColumnInfo;
import androidx.room.Entity;
import androidx.room.ForeignKey;
import androidx.room.Index;
import androidx.room.PrimaryKey;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000(\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0006\n\u0002\u0010\t\n\u0000\n\u0002\u0010\u000b\n\u0002\b)\b\u0087\b\u0018\u00002\u00020\u0001B\u0083\u0001\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u0012\u0006\u0010\u0005\u001a\u00020\u0006\u0012\u0006\u0010\u0007\u001a\u00020\u0003\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u0003\u0012\b\b\u0002\u0010\u000b\u001a\u00020\u0003\u0012\b\b\u0002\u0010\f\u001a\u00020\r\u0012\b\b\u0002\u0010\u000e\u001a\u00020\u000f\u0012\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\r\u0012\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\u0004\b\u0012\u0010\u0013J\t\u0010&\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\'\u001a\u00020\u0003H\u00c6\u0003J\t\u0010(\u001a\u00020\u0006H\u00c6\u0003J\t\u0010)\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010*\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010+\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010,\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\t\u0010-\u001a\u00020\u0003H\u00c6\u0003J\t\u0010.\u001a\u00020\rH\u00c6\u0003J\t\u0010/\u001a\u00020\u000fH\u00c6\u0003J\u0010\u00100\u001a\u0004\u0018\u00010\rH\u00c6\u0003\u00a2\u0006\u0002\u0010#J\u000b\u00101\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u0090\u0001\u00102\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00062\b\b\u0002\u0010\u0007\u001a\u00020\u00032\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\u000b\u001a\u00020\u00032\b\b\u0002\u0010\f\u001a\u00020\r2\b\b\u0002\u0010\u000e\u001a\u00020\u000f2\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\r2\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u0003H\u00c6\u0001\u00a2\u0006\u0002\u00103J\u0013\u00104\u001a\u00020\u000f2\b\u00105\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u00106\u001a\u00020\u0006H\u00d6\u0001J\t\u00107\u001a\u00020\u0003H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0015R\u0016\u0010\u0004\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0015R\u0016\u0010\u0005\u001a\u00020\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0018R\u0016\u0010\u0007\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0015R\u0018\u0010\b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0015R\u0018\u0010\t\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u0015R\u0018\u0010\n\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u0015R\u0016\u0010\u000b\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u0015R\u0016\u0010\f\u001a\u00020\r8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001e\u0010\u001fR\u0016\u0010\u000e\u001a\u00020\u000f8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b \u0010!R\u001a\u0010\u0010\u001a\u0004\u0018\u00010\r8\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010$\u001a\u0004\b\"\u0010#R\u0018\u0010\u0011\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b%\u0010\u0015\u00a8\u00068"}, d2 = {"Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "", "id", "", "sessionId", "rowNumber", "", "serialNumber", "cellPhoneNumber", "imei1", "imei2", "source", "createdAt", "", "sent", "", "sentAt", "lastError", "<init>", "(Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JZLjava/lang/Long;Ljava/lang/String;)V", "getId", "()Ljava/lang/String;", "getSessionId", "getRowNumber", "()I", "getSerialNumber", "getCellPhoneNumber", "getImei1", "getImei2", "getSource", "getCreatedAt", "()J", "getSent", "()Z", "getSentAt", "()Ljava/lang/Long;", "Ljava/lang/Long;", "getLastError", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "copy", "(Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JZLjava/lang/Long;Ljava/lang/String;)Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "equals", "other", "hashCode", "toString", "app_prodDebug"})
@androidx.room.Entity(tableName = "local_scan_items", foreignKeys = {@androidx.room.ForeignKey(entity = com.example.yakultscanner.data.db.LocalScanSessionEntity.class, parentColumns = {"id"}, childColumns = {"session_id"}, onDelete = 5)}, indices = {@androidx.room.Index(value = {"session_id"})})
public final class LocalScanItemEntity {
    @androidx.room.PrimaryKey()
    @androidx.room.ColumnInfo(name = "id")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String id = null;
    @androidx.room.ColumnInfo(name = "session_id")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String sessionId = null;
    @androidx.room.ColumnInfo(name = "row_number")
    private final int rowNumber = 0;
    @androidx.room.ColumnInfo(name = "serial_number")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String serialNumber = null;
    @androidx.room.ColumnInfo(name = "cell_phone_number")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String cellPhoneNumber = null;
    @androidx.room.ColumnInfo(name = "imei1")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String imei1 = null;
    @androidx.room.ColumnInfo(name = "imei2")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String imei2 = null;
    @androidx.room.ColumnInfo(name = "source")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String source = null;
    @androidx.room.ColumnInfo(name = "created_at")
    private final long createdAt = 0L;
    @androidx.room.ColumnInfo(name = "sent")
    private final boolean sent = false;
    @androidx.room.ColumnInfo(name = "sent_at")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Long sentAt = null;
    @androidx.room.ColumnInfo(name = "last_error")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String lastError = null;
    
    public LocalScanItemEntity(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, int rowNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String cellPhoneNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String imei1, @org.jetbrains.annotations.Nullable()
    java.lang.String imei2, @org.jetbrains.annotations.NotNull()
    java.lang.String source, long createdAt, boolean sent, @org.jetbrains.annotations.Nullable()
    java.lang.Long sentAt, @org.jetbrains.annotations.Nullable()
    java.lang.String lastError) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getId() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSessionId() {
        return null;
    }
    
    public final int getRowNumber() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCellPhoneNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getImei1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getImei2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSource() {
        return null;
    }
    
    public final long getCreatedAt() {
        return 0L;
    }
    
    public final boolean getSent() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long getSentAt() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLastError() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    public final boolean component10() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long component11() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component12() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component2() {
        return null;
    }
    
    public final int component3() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component8() {
        return null;
    }
    
    public final long component9() {
        return 0L;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.LocalScanItemEntity copy(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, int rowNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String cellPhoneNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String imei1, @org.jetbrains.annotations.Nullable()
    java.lang.String imei2, @org.jetbrains.annotations.NotNull()
    java.lang.String source, long createdAt, boolean sent, @org.jetbrains.annotations.Nullable()
    java.lang.Long sentAt, @org.jetbrains.annotations.Nullable()
    java.lang.String lastError) {
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