package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.SerialFromMobileRequest;
import com.example.yakultscanner.data.db.LocalScanDao;
import com.example.yakultscanner.data.db.LocalScanItemEntity;
import com.example.yakultscanner.data.db.LocalScanSessionEntity;
import com.example.yakultscanner.data.db.LocalScanSessionStatus;
import com.example.yakultscanner.data.db.LocalScanSessionWithCount;
import kotlinx.coroutines.flow.Flow;
import javax.inject.Inject;
import javax.inject.Singleton;

@javax.inject.Singleton()
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000@\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0002\b\t\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\b\u0007\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\u0012\u0010\u0006\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\t0\b0\u0007J\u001a\u0010\n\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u000b0\b0\u00072\u0006\u0010\f\u001a\u00020\rJ\u0018\u0010\u000e\u001a\u0004\u0018\u00010\u000f2\u0006\u0010\f\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0010J\u001c\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u000b0\b2\u0006\u0010\f\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0010JB\u0010\u0012\u001a\u00020\r2\b\u0010\f\u001a\u0004\u0018\u00010\r2\u0006\u0010\u0013\u001a\u00020\r2\b\u0010\u0014\u001a\u0004\u0018\u00010\r2\b\u0010\u0015\u001a\u0004\u0018\u00010\r2\f\u0010\u0016\u001a\b\u0012\u0004\u0012\u00020\u000b0\bH\u0086@\u00a2\u0006\u0002\u0010\u0017J\u0016\u0010\u0018\u001a\u00020\u00192\u0006\u0010\f\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0010J\u0016\u0010\u001a\u001a\u00020\u001b2\u0006\u0010\f\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0010R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u001c"}, d2 = {"Lcom/example/yakultscanner/data/repository/LocalScanRepository;", "", "dao", "Lcom/example/yakultscanner/data/db/LocalScanDao;", "<init>", "(Lcom/example/yakultscanner/data/db/LocalScanDao;)V", "observeSessions", "Lkotlinx/coroutines/flow/Flow;", "", "Lcom/example/yakultscanner/data/db/LocalScanSessionWithCount;", "observeItems", "Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "sessionId", "", "getSession", "Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getItems", "saveSession", "title", "createdBy", "notes", "rows", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/util/List;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "deleteSession", "", "sendSession", "Lcom/example/yakultscanner/data/repository/LocalScanSendResult;", "app_devDebug"})
public final class LocalScanRepository {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.db.LocalScanDao dao = null;
    
    @javax.inject.Inject()
    public LocalScanRepository(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.LocalScanDao dao) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.Flow<java.util.List<com.example.yakultscanner.data.db.LocalScanSessionWithCount>> observeSessions() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.Flow<java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity>> observeItems(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getSession(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.data.db.LocalScanSessionEntity> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getItems(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object saveSession(@org.jetbrains.annotations.Nullable()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    java.lang.String title, @org.jetbrains.annotations.Nullable()
    java.lang.String createdBy, @org.jetbrains.annotations.Nullable()
    java.lang.String notes, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> rows, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.String> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object deleteSession(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object sendSession(@org.jetbrains.annotations.NotNull()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super com.example.yakultscanner.data.repository.LocalScanSendResult> $completion) {
        return null;
    }
}