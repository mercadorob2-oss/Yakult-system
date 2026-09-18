package com.example.yakultscanner.data.db;

import androidx.room.Dao;
import androidx.room.Insert;
import androidx.room.OnConflictStrategy;
import androidx.room.Query;
import androidx.room.Transaction;
import kotlinx.coroutines.flow.Flow;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000F\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u0002\n\u0002\b\f\n\u0002\u0010\t\n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0002\b\u0003\bg\u0018\u00002\u00020\u0001J\u0014\u0010\u0002\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00050\u00040\u0003H\'J\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00072\u0006\u0010\b\u001a\u00020\tH\u00a7@\u00a2\u0006\u0002\u0010\nJ\u001c\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\f0\u00042\u0006\u0010\b\u001a\u00020\tH\u00a7@\u00a2\u0006\u0002\u0010\nJ\u001c\u0010\r\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\f0\u00040\u00032\u0006\u0010\b\u001a\u00020\tH\'J\u0016\u0010\u000e\u001a\u00020\u000f2\u0006\u0010\u0010\u001a\u00020\u0007H\u00a7@\u00a2\u0006\u0002\u0010\u0011J\u001c\u0010\u0012\u001a\u00020\u000f2\f\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\f0\u0004H\u00a7@\u00a2\u0006\u0002\u0010\u0014J$\u0010\u0015\u001a\u00020\u000f2\u0006\u0010\u0010\u001a\u00020\u00072\f\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\f0\u0004H\u0097@\u00a2\u0006\u0002\u0010\u0016J\u0016\u0010\u0017\u001a\u00020\u000f2\u0006\u0010\b\u001a\u00020\tH\u00a7@\u00a2\u0006\u0002\u0010\nJ\u0016\u0010\u0018\u001a\u00020\u000f2\u0006\u0010\b\u001a\u00020\tH\u00a7@\u00a2\u0006\u0002\u0010\nJ\u001e\u0010\u0019\u001a\u00020\u000f2\u0006\u0010\u001a\u001a\u00020\t2\u0006\u0010\u001b\u001a\u00020\u001cH\u00a7@\u00a2\u0006\u0002\u0010\u001dJ \u0010\u001e\u001a\u00020\u000f2\u0006\u0010\u001a\u001a\u00020\t2\b\u0010\u001f\u001a\u0004\u0018\u00010\tH\u00a7@\u00a2\u0006\u0002\u0010 J0\u0010!\u001a\u00020\u000f2\u0006\u0010\b\u001a\u00020\t2\u0006\u0010\"\u001a\u00020#2\b\u0010\u001b\u001a\u0004\u0018\u00010\u001c2\u0006\u0010$\u001a\u00020\u001cH\u00a7@\u00a2\u0006\u0002\u0010%\u00a8\u0006&\u00c0\u0006\u0003"}, d2 = {"Lcom/example/yakultscanner/data/db/LocalScanDao;", "", "observeSessions", "Lkotlinx/coroutines/flow/Flow;", "", "Lcom/example/yakultscanner/data/db/LocalScanSessionWithCount;", "getSession", "Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;", "sessionId", "", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getItems", "Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "observeItems", "insertSession", "", "session", "(Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "insertItems", "items", "(Ljava/util/List;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "replaceSession", "(Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;Ljava/util/List;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "deleteItems", "deleteSession", "markItemSent", "itemId", "sentAt", "", "(Ljava/lang/String;JLkotlin/coroutines/Continuation;)Ljava/lang/Object;", "markItemFailed", "error", "(Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "updateSessionSendStatus", "status", "Lcom/example/yakultscanner/data/db/LocalScanSessionStatus;", "updatedAt", "(Ljava/lang/String;Lcom/example/yakultscanner/data/db/LocalScanSessionStatus;Ljava/lang/Long;JLkotlin/coroutines/Continuation;)Ljava/lang/Object;", "app_devDebug"})
@androidx.room.Dao()
public abstract interface LocalScanDao {
    
    @androidx.room.Query(value = "\n        SELECT s.*, COUNT(i.id) AS item_count,\n               COALESCE(SUM(CASE WHEN i.sent = 1 THEN 1 ELSE 0 END), 0) AS sent_count\n        FROM local_scan_sessions s\n        LEFT JOIN local_scan_items i ON i.session_id = s.id\n        GROUP BY s.id\n        ORDER BY s.updated_at DESC\n        ")
    @org.jetbrains.annotations.NotNull()
    public abstract kotlinx.coroutines.flow.Flow<java.util.List<com.example.yakultscanner.data.db.LocalScanSessionWithCount>> observeSessions();
    
    @androidx.room.Query(value = "SELECT * FROM local_scan_sessions WHERE id = :sessionId LIMIT 1")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getSession(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.data.db.LocalScanSessionEntity> $completion);
    
    @androidx.room.Query(value = "SELECT * FROM local_scan_items WHERE session_id = :sessionId ORDER BY row_number ASC, created_at ASC")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getItems(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity>> $completion);
    
    @androidx.room.Query(value = "SELECT * FROM local_scan_items WHERE session_id = :sessionId ORDER BY row_number ASC, created_at ASC")
    @org.jetbrains.annotations.NotNull()
    public abstract kotlinx.coroutines.flow.Flow<java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity>> observeItems(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId);
    
    @androidx.room.Insert(onConflict = 1)
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object insertSession(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.LocalScanSessionEntity session, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Insert(onConflict = 1)
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object insertItems(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> items, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Transaction()
    @org.jetbrains.annotations.Nullable()
    public default java.lang.Object replaceSession(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.LocalScanSessionEntity session, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> items, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @androidx.room.Query(value = "DELETE FROM local_scan_items WHERE session_id = :sessionId")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object deleteItems(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "DELETE FROM local_scan_sessions WHERE id = :sessionId")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object deleteSession(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "UPDATE local_scan_items SET sent = 1, sent_at = :sentAt, last_error = NULL WHERE id = :itemId")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object markItemSent(@org.jetbrains.annotations.NotNull()
    java.lang.String itemId, long sentAt, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "UPDATE local_scan_items SET last_error = :error WHERE id = :itemId")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object markItemFailed(@org.jetbrains.annotations.NotNull()
    java.lang.String itemId, @org.jetbrains.annotations.Nullable()
    java.lang.String error, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "UPDATE local_scan_sessions SET status = :status, sent_at = :sentAt, updated_at = :updatedAt WHERE id = :sessionId")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object updateSessionSendStatus(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.LocalScanSessionStatus status, @org.jetbrains.annotations.Nullable()
    java.lang.Long sentAt, long updatedAt, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 3, xi = 48)
    public static final class DefaultImpls {
        
        @androidx.room.Transaction()
        @org.jetbrains.annotations.Nullable()
        @java.lang.Deprecated()
        public static java.lang.Object replaceSession(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.db.LocalScanDao $this, @org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.db.LocalScanSessionEntity session, @org.jetbrains.annotations.NotNull()
        java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> items, @org.jetbrains.annotations.NotNull()
        kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
            return null;
        }
    }
}