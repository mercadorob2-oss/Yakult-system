package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.data.db.LocalScanItemEntity;
import com.example.yakultscanner.data.db.LocalScanSessionEntity;
import com.example.yakultscanner.data.db.LocalScanSessionWithCount;
import com.example.yakultscanner.data.repository.LocalScanRepository;
import com.example.yakultscanner.data.repository.LocalScanSendResult;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.SharingStarted;
import kotlinx.coroutines.flow.StateFlow;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000F\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\b\u0007\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J8\u0010\f\u001a\u00020\r2\b\u0010\u000e\u001a\u0004\u0018\u00010\r2\u0006\u0010\u000f\u001a\u00020\r2\b\u0010\u0010\u001a\u0004\u0018\u00010\r2\f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00120\bH\u0086@\u00a2\u0006\u0002\u0010\u0013J\u0018\u0010\u0014\u001a\u0004\u0018\u00010\u00152\u0006\u0010\u000e\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0016J\u001c\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00120\b2\u0006\u0010\u000e\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0016J\u0016\u0010\u0018\u001a\u00020\u00192\u0006\u0010\u000e\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0016J\u0016\u0010\u001a\u001a\u00020\u001b2\u0006\u0010\u000e\u001a\u00020\rH\u0086@\u00a2\u0006\u0002\u0010\u0016R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010\u0006\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\t0\b0\u0007\u00a2\u0006\b\n\u0000\u001a\u0004\b\n\u0010\u000b\u00a8\u0006\u001c"}, d2 = {"Lcom/example/yakultscanner/viewmodels/LocalSerialScanViewModel;", "Landroidx/lifecycle/ViewModel;", "repository", "Lcom/example/yakultscanner/data/repository/LocalScanRepository;", "<init>", "(Lcom/example/yakultscanner/data/repository/LocalScanRepository;)V", "sessions", "Lkotlinx/coroutines/flow/StateFlow;", "", "Lcom/example/yakultscanner/data/db/LocalScanSessionWithCount;", "getSessions", "()Lkotlinx/coroutines/flow/StateFlow;", "saveSession", "", "sessionId", "title", "notes", "rows", "Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/util/List;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getSession", "Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getItems", "deleteSession", "", "sendSession", "Lcom/example/yakultscanner/data/repository/LocalScanSendResult;", "app_devDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class LocalSerialScanViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.LocalScanRepository repository = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.data.db.LocalScanSessionWithCount>> sessions = null;
    
    @javax.inject.Inject()
    public LocalSerialScanViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.LocalScanRepository repository) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.data.db.LocalScanSessionWithCount>> getSessions() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object saveSession(@org.jetbrains.annotations.Nullable()
    java.lang.String sessionId, @org.jetbrains.annotations.NotNull()
    java.lang.String title, @org.jetbrains.annotations.Nullable()
    java.lang.String notes, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> rows, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super java.lang.String> $completion) {
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