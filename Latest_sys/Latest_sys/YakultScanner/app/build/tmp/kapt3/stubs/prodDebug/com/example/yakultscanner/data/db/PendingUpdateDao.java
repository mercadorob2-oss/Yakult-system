package com.example.yakultscanner.data.db;

import androidx.room.*;
import kotlinx.coroutines.flow.Flow;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000@\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0010\t\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0002\b\u0010\bg\u0018\u00002\u00020\u0001J\u001c\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u00032\u0006\u0010\u0005\u001a\u00020\u0006H\u00a7@\u00a2\u0006\u0002\u0010\u0007J\u0014\u0010\b\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003H\u00a7@\u00a2\u0006\u0002\u0010\tJ\u001c\u0010\n\u001a\b\u0012\u0004\u0012\u00020\u00040\u00032\u0006\u0010\u000b\u001a\u00020\fH\u00a7@\u00a2\u0006\u0002\u0010\rJ\u000e\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\u00100\u000fH\'J\u000e\u0010\u0011\u001a\u00020\u0010H\u00a7@\u00a2\u0006\u0002\u0010\tJ\u0016\u0010\u0012\u001a\u00020\u00132\u0006\u0010\u0014\u001a\u00020\u0004H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u001c\u0010\u0016\u001a\u00020\u00172\f\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003H\u00a7@\u00a2\u0006\u0002\u0010\u0019J\u0016\u0010\u001a\u001a\u00020\u00172\u0006\u0010\u0014\u001a\u00020\u0004H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u0016\u0010\u001b\u001a\u00020\u00172\u0006\u0010\u0014\u001a\u00020\u0004H\u00a7@\u00a2\u0006\u0002\u0010\u0015J\u0016\u0010\u001c\u001a\u00020\u00172\u0006\u0010\u001d\u001a\u00020\fH\u00a7@\u00a2\u0006\u0002\u0010\rJ\u000e\u0010\u001e\u001a\u00020\u0017H\u00a7@\u00a2\u0006\u0002\u0010\tJ\u0016\u0010\u001f\u001a\u00020\u00172\u0006\u0010\u001d\u001a\u00020\fH\u00a7@\u00a2\u0006\u0002\u0010\rJ(\u0010 \u001a\u00020\u00172\u0006\u0010\u001d\u001a\u00020\f2\b\u0010!\u001a\u0004\u0018\u00010\f2\u0006\u0010\u0005\u001a\u00020\u0006H\u00a7@\u00a2\u0006\u0002\u0010\"J\u001e\u0010#\u001a\u00020\u00172\u0006\u0010\u001d\u001a\u00020\f2\u0006\u0010$\u001a\u00020\u0013H\u00a7@\u00a2\u0006\u0002\u0010%J\u0014\u0010&\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003H\u00a7@\u00a2\u0006\u0002\u0010\t\u00a8\u0006\'\u00c0\u0006\u0003"}, d2 = {"Lcom/example/yakultscanner/data/db/PendingUpdateDao;", "", "getByStatus", "", "Lcom/example/yakultscanner/data/db/PendingUpdateEntity;", "status", "Lcom/example/yakultscanner/data/db/SyncStatus;", "(Lcom/example/yakultscanner/data/db/SyncStatus;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getPendingOrFailed", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getBySetCode", "setCode", "", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getPendingCountFlow", "Lkotlinx/coroutines/flow/Flow;", "", "getPendingCount", "insert", "", "entity", "(Lcom/example/yakultscanner/data/db/PendingUpdateEntity;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "insertAll", "", "entities", "(Ljava/util/List;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "update", "delete", "deleteById", "id", "deleteSynced", "markSyncing", "markFailed", "error", "(Ljava/lang/String;Ljava/lang/String;Lcom/example/yakultscanner/data/db/SyncStatus;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "markSynced", "timestamp", "(Ljava/lang/String;JLkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getAll", "app_prodDebug"})
@androidx.room.Dao()
public abstract interface PendingUpdateDao {
    
    @androidx.room.Query(value = "SELECT * FROM pending_updates WHERE sync_status = :status ORDER BY created_at ASC")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getByStatus(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.SyncStatus status, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> $completion);
    
    @androidx.room.Query(value = "SELECT * FROM pending_updates WHERE sync_status IN (\'PENDING\', \'SYNCING\', \'FAILED\') ORDER BY created_at ASC")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getPendingOrFailed(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> $completion);
    
    @androidx.room.Query(value = "SELECT * FROM pending_updates WHERE set_code = :setCode ORDER BY created_at DESC")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getBySetCode(@org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> $completion);
    
    @androidx.room.Query(value = "SELECT COUNT(*) FROM pending_updates WHERE sync_status IN (\'PENDING\', \'SYNCING\', \'FAILED\')")
    @org.jetbrains.annotations.NotNull()
    public abstract kotlinx.coroutines.flow.Flow<java.lang.Integer> getPendingCountFlow();
    
    @androidx.room.Query(value = "SELECT COUNT(*) FROM pending_updates WHERE sync_status IN (\'PENDING\', \'SYNCING\', \'FAILED\')")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getPendingCount(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.Integer> $completion);
    
    @androidx.room.Insert()
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object insert(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.PendingUpdateEntity entity, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.Long> $completion);
    
    @androidx.room.Insert()
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object insertAll(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity> entities, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Update()
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object update(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.PendingUpdateEntity entity, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Delete()
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object delete(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.PendingUpdateEntity entity, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "DELETE FROM pending_updates WHERE id = :id")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object deleteById(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "DELETE FROM pending_updates WHERE sync_status = \'SYNCED\'")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object deleteSynced(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "UPDATE pending_updates SET sync_status = \'SYNCING\' WHERE id = :id")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object markSyncing(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "UPDATE pending_updates SET retry_count = retry_count + 1, last_error = :error, sync_status = :status WHERE id = :id")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object markFailed(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.Nullable()
    java.lang.String error, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.SyncStatus status, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "UPDATE pending_updates SET sync_status = \'SYNCED\', synced_at = :timestamp WHERE id = :id")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object markSynced(@org.jetbrains.annotations.NotNull()
    java.lang.String id, long timestamp, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion);
    
    @androidx.room.Query(value = "SELECT * FROM pending_updates")
    @org.jetbrains.annotations.Nullable()
    public abstract java.lang.Object getAll(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> $completion);
}