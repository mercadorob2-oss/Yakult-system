package com.example.yakultscanner;

import kotlinx.coroutines.flow.StateFlow;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000(\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010\t\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0018\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001BM\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\b\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\u0004\b\u000b\u0010\fJ\t\u0010\u0018\u001a\u00020\u0003H\u00c6\u0003J\u0010\u0010\u0019\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0010J\u0010\u0010\u001a\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0010J\u0010\u0010\u001b\u001a\u0004\u0018\u00010\bH\u00c6\u0003\u00a2\u0006\u0002\u0010\u0014J\u000b\u0010\u001c\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001d\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003JT\u0010\u001e\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\b2\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u0003H\u00c6\u0001\u00a2\u0006\u0002\u0010\u001fJ\u0013\u0010 \u001a\u00020!2\b\u0010\"\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010#\u001a\u00020\bH\u00d6\u0001J\t\u0010$\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\r\u0010\u000eR\u0015\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\n\n\u0002\u0010\u0011\u001a\u0004\b\u000f\u0010\u0010R\u0015\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\n\n\u0002\u0010\u0011\u001a\u0004\b\u0012\u0010\u0010R\u0015\u0010\u0007\u001a\u0004\u0018\u00010\b\u00a2\u0006\n\n\u0002\u0010\u0015\u001a\u0004\b\u0013\u0010\u0014R\u0013\u0010\t\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u000eR\u0013\u0010\n\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u000e\u00a8\u0006%"}, d2 = {"Lcom/example/yakultscanner/ConnectionHealthSnapshot;", "", "baseUrl", "", "lastSuccessAtMs", "", "lastFailureAtMs", "lastHttpCode", "", "lastEndpoint", "lastErrorMessage", "<init>", "(Ljava/lang/String;Ljava/lang/Long;Ljava/lang/Long;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;)V", "getBaseUrl", "()Ljava/lang/String;", "getLastSuccessAtMs", "()Ljava/lang/Long;", "Ljava/lang/Long;", "getLastFailureAtMs", "getLastHttpCode", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getLastEndpoint", "getLastErrorMessage", "component1", "component2", "component3", "component4", "component5", "component6", "copy", "(Ljava/lang/String;Ljava/lang/Long;Ljava/lang/Long;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;)Lcom/example/yakultscanner/ConnectionHealthSnapshot;", "equals", "", "other", "hashCode", "toString", "app_prodDebug"})
public final class ConnectionHealthSnapshot {
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String baseUrl = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Long lastSuccessAtMs = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Long lastFailureAtMs = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer lastHttpCode = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String lastEndpoint = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String lastErrorMessage = null;
    
    public ConnectionHealthSnapshot(@org.jetbrains.annotations.NotNull()
    java.lang.String baseUrl, @org.jetbrains.annotations.Nullable()
    java.lang.Long lastSuccessAtMs, @org.jetbrains.annotations.Nullable()
    java.lang.Long lastFailureAtMs, @org.jetbrains.annotations.Nullable()
    java.lang.Integer lastHttpCode, @org.jetbrains.annotations.Nullable()
    java.lang.String lastEndpoint, @org.jetbrains.annotations.Nullable()
    java.lang.String lastErrorMessage) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getBaseUrl() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long getLastSuccessAtMs() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long getLastFailureAtMs() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getLastHttpCode() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLastEndpoint() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLastErrorMessage() {
        return null;
    }
    
    public ConnectionHealthSnapshot() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Long component3() {
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
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.ConnectionHealthSnapshot copy(@org.jetbrains.annotations.NotNull()
    java.lang.String baseUrl, @org.jetbrains.annotations.Nullable()
    java.lang.Long lastSuccessAtMs, @org.jetbrains.annotations.Nullable()
    java.lang.Long lastFailureAtMs, @org.jetbrains.annotations.Nullable()
    java.lang.Integer lastHttpCode, @org.jetbrains.annotations.Nullable()
    java.lang.String lastEndpoint, @org.jetbrains.annotations.Nullable()
    java.lang.String lastErrorMessage) {
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