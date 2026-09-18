package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.SetUpdatesRequest;
import com.example.yakultscanner.api.YakultApiService;
import com.example.yakultscanner.data.db.PendingUpdateEntity;
import com.example.yakultscanner.data.db.SyncStatus;
import kotlinx.coroutines.flow.Flow;
import kotlinx.coroutines.flow.StateFlow;
import javax.inject.Inject;
import javax.inject.Singleton;

@javax.inject.Singleton()
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000L\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0010\u000b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0010\b\n\u0002\b\u0004\n\u0002\u0010\u000e\n\u0002\b\u0005\n\u0002\u0010\u0002\n\u0000\b\u0007\u0018\u00002\u00020\u0001B\u0019\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\u0004\b\u0006\u0010\u0007J\u0018\u0010\u0017\u001a\u00020\u000f2\b\u0010\u0018\u001a\u0004\u0018\u00010\u0019H\u0086@\u00a2\u0006\u0002\u0010\u001aJ\u0018\u0010\u001b\u001a\u00020\u000f2\b\u0010\u0018\u001a\u0004\u0018\u00010\u0019H\u0086@\u00a2\u0006\u0002\u0010\u001aJ\u000e\u0010\u001c\u001a\u00020\u0014H\u0086@\u00a2\u0006\u0002\u0010\u001dJ\u0006\u0010\u001e\u001a\u00020\u001fR\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0004\u001a\u00020\u0005X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0014\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\n0\f\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000b\u0010\rR\u0016\u0010\u000e\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010\u000f0\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0019\u0010\u0010\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010\u000f0\f\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\rR\u0017\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u00140\u0013\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0016\u00a8\u0006 "}, d2 = {"Lcom/example/yakultscanner/data/repository/SyncRepository;", "", "offlineRepository", "Lcom/example/yakultscanner/data/repository/OfflineRepository;", "apiService", "Lcom/example/yakultscanner/api/YakultApiService;", "<init>", "(Lcom/example/yakultscanner/data/repository/OfflineRepository;Lcom/example/yakultscanner/api/YakultApiService;)V", "_isSyncing", "Lkotlinx/coroutines/flow/MutableStateFlow;", "", "isSyncing", "Lkotlinx/coroutines/flow/StateFlow;", "()Lkotlinx/coroutines/flow/StateFlow;", "_lastSyncResult", "Lcom/example/yakultscanner/data/repository/SyncResult;", "lastSyncResult", "getLastSyncResult", "pendingCountFlow", "Lkotlinx/coroutines/flow/Flow;", "", "getPendingCountFlow", "()Lkotlinx/coroutines/flow/Flow;", "syncPendingUpdates", "user", "", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "retryFailedUpdates", "getPendingCount", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "clearLastResult", "", "app_prodDebug"})
public final class SyncRepository {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.OfflineRepository offlineRepository = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.api.YakultApiService apiService = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Boolean> _isSyncing = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isSyncing = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.data.repository.SyncResult> _lastSyncResult = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.data.repository.SyncResult> lastSyncResult = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.Flow<java.lang.Integer> pendingCountFlow = null;
    
    @javax.inject.Inject()
    public SyncRepository(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.OfflineRepository offlineRepository, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.YakultApiService apiService) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isSyncing() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.data.repository.SyncResult> getLastSyncResult() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.Flow<java.lang.Integer> getPendingCountFlow() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object syncPendingUpdates(@org.jetbrains.annotations.Nullable()
    java.lang.String user, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.data.repository.SyncResult> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object retryFailedUpdates(@org.jetbrains.annotations.Nullable()
    java.lang.String user, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.data.repository.SyncResult> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getPendingCount(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.Integer> $completion) {
        return null;
    }
    
    public final void clearLastResult() {
    }
}