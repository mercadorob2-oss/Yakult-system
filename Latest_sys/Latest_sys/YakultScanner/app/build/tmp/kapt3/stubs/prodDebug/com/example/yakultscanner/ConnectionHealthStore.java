package com.example.yakultscanner;

import kotlinx.coroutines.flow.StateFlow;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00004\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0010\b\n\u0002\b\u0005\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u000e\u0010\u000b\u001a\u00020\f2\u0006\u0010\r\u001a\u00020\u000eJ\u001f\u0010\u000f\u001a\u00020\f2\b\u0010\u0010\u001a\u0004\u0018\u00010\u000e2\b\u0010\u0011\u001a\u0004\u0018\u00010\u0012\u00a2\u0006\u0002\u0010\u0013J)\u0010\u0014\u001a\u00020\f2\b\u0010\u0010\u001a\u0004\u0018\u00010\u000e2\b\u0010\u0011\u001a\u0004\u0018\u00010\u00122\b\u0010\u0015\u001a\u0004\u0018\u00010\u000e\u00a2\u0006\u0002\u0010\u0016R\u0014\u0010\u0004\u001a\b\u0012\u0004\u0012\u00020\u00060\u0005X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\u00060\b\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\n\u00a8\u0006\u0017"}, d2 = {"Lcom/example/yakultscanner/ConnectionHealthStore;", "", "<init>", "()V", "_state", "Lkotlinx/coroutines/flow/MutableStateFlow;", "Lcom/example/yakultscanner/ConnectionHealthSnapshot;", "state", "Lkotlinx/coroutines/flow/StateFlow;", "getState", "()Lkotlinx/coroutines/flow/StateFlow;", "updateBaseUrl", "", "baseUrl", "", "recordSuccess", "endpoint", "httpCode", "", "(Ljava/lang/String;Ljava/lang/Integer;)V", "recordFailure", "message", "(Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;)V", "app_prodDebug"})
public final class ConnectionHealthStore {
    @org.jetbrains.annotations.NotNull()
    private static final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.ConnectionHealthSnapshot> _state = null;
    @org.jetbrains.annotations.NotNull()
    private static final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.ConnectionHealthSnapshot> state = null;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.ConnectionHealthStore INSTANCE = null;
    
    private ConnectionHealthStore() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.ConnectionHealthSnapshot> getState() {
        return null;
    }
    
    public final void updateBaseUrl(@org.jetbrains.annotations.NotNull()
    java.lang.String baseUrl) {
    }
    
    public final void recordSuccess(@org.jetbrains.annotations.Nullable()
    java.lang.String endpoint, @org.jetbrains.annotations.Nullable()
    java.lang.Integer httpCode) {
    }
    
    public final void recordFailure(@org.jetbrains.annotations.Nullable()
    java.lang.String endpoint, @org.jetbrains.annotations.Nullable()
    java.lang.Integer httpCode, @org.jetbrains.annotations.Nullable()
    java.lang.String message) {
    }
}