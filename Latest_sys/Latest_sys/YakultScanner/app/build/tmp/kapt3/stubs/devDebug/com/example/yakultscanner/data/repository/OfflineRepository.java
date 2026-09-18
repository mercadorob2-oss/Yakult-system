package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.data.db.PendingUpdateDao;
import com.example.yakultscanner.data.db.PendingUpdateEntity;
import com.example.yakultscanner.data.db.SyncStatus;
import com.example.yakultscanner.api.SetItemUpdateEntry;
import kotlinx.coroutines.flow.Flow;
import javax.inject.Inject;
import javax.inject.Singleton;

@javax.inject.Singleton()
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000F\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010 \n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0010\u0002\n\u0002\b\u000b\b\u0007\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J6\u0010\u0006\u001a\u00020\u00072\u0006\u0010\b\u001a\u00020\u00072\u0006\u0010\t\u001a\u00020\n2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00072\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u0007H\u0086@\u00a2\u0006\u0002\u0010\rJB\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\u00070\u000f2\u0006\u0010\b\u001a\u00020\u00072\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\n0\u000f2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00072\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u0007H\u0086@\u00a2\u0006\u0002\u0010\u0011J\u0014\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u00130\u000fH\u0086@\u00a2\u0006\u0002\u0010\u0014J\u001c\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00130\u000f2\u0006\u0010\b\u001a\u00020\u0007H\u0086@\u00a2\u0006\u0002\u0010\u0016J\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00190\u0018J\u000e\u0010\u001a\u001a\u00020\u0019H\u0086@\u00a2\u0006\u0002\u0010\u0014J\u0016\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u0007H\u0086@\u00a2\u0006\u0002\u0010\u0016J\u0016\u0010\u001e\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u0007H\u0086@\u00a2\u0006\u0002\u0010\u0016J \u0010\u001f\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u00072\b\u0010 \u001a\u0004\u0018\u00010\u0007H\u0086@\u00a2\u0006\u0002\u0010!J\u0016\u0010\"\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u0007H\u0086@\u00a2\u0006\u0002\u0010\u0016J\u000e\u0010#\u001a\u00020\u001cH\u0086@\u00a2\u0006\u0002\u0010\u0014J\u0016\u0010$\u001a\u00020\n2\u0006\u0010%\u001a\u00020\u0013H\u0086@\u00a2\u0006\u0002\u0010&R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000\u00a8\u0006\'"}, d2 = {"Lcom/example/yakultscanner/data/repository/OfflineRepository;", "", "pendingUpdateDao", "Lcom/example/yakultscanner/data/db/PendingUpdateDao;", "<init>", "(Lcom/example/yakultscanner/data/db/PendingUpdateDao;)V", "savePendingUpdate", "", "setCode", "entry", "Lcom/example/yakultscanner/api/SetItemUpdateEntry;", "updatedByUser", "updatedByName", "(Ljava/lang/String;Lcom/example/yakultscanner/api/SetItemUpdateEntry;Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "savePendingUpdates", "", "entries", "(Ljava/lang/String;Ljava/util/List;Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getPendingUpdates", "Lcom/example/yakultscanner/data/db/PendingUpdateEntity;", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getPendingUpdatesBySetCode", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getPendingCountFlow", "Lkotlinx/coroutines/flow/Flow;", "", "getPendingCount", "markAsSyncing", "", "id", "markAsSynced", "markAsFailed", "error", "(Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "deletePendingUpdate", "clearSynced", "convertToApiEntry", "entity", "(Lcom/example/yakultscanner/data/db/PendingUpdateEntity;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "app_devDebug"})
public final class OfflineRepository {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.db.PendingUpdateDao pendingUpdateDao = null;
    
    @javax.inject.Inject()
    public OfflineRepository(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.PendingUpdateDao pendingUpdateDao) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object savePendingUpdate(@org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetItemUpdateEntry entry, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByUser, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByName, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.String> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object savePendingUpdates(@org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.SetItemUpdateEntry> entries, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByUser, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByName, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<java.lang.String>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getPendingUpdates(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getPendingUpdatesBySetCode(@org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.Flow<java.lang.Integer> getPendingCountFlow() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getPendingCount(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.Integer> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object markAsSyncing(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object markAsSynced(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object markAsFailed(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.Nullable()
    java.lang.String error, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object deletePendingUpdate(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object clearSynced(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object convertToApiEntry(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.PendingUpdateEntity entity, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.api.SetItemUpdateEntry> $completion) {
        return null;
    }
}