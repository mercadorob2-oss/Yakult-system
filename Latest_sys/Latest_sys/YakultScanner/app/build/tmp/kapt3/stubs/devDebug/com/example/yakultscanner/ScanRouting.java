package com.example.yakultscanner;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010\u000e\n\u0002\b\u0005\n\u0002\u0010\u000b\n\u0002\b\u000e\n\u0002\u0010\u0002\n\u0000\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0014\u0010\u0017\u001a\u0004\u0018\u00010\u000b2\b\u0010\u0018\u001a\u0004\u0018\u00010\u000bH\u0002J!\u0010\u0019\u001a\u00020\u00052\b\u0010\u001a\u001a\u0004\u0018\u00010\u000b2\b\u0010\u001b\u001a\u0004\u0018\u00010\u000bH\u0000\u00a2\u0006\u0002\b\u001cJ\u0017\u0010\u001d\u001a\u00020\u00112\b\u0010\u001a\u001a\u0004\u0018\u00010\u000bH\u0000\u00a2\u0006\u0002\b\u001eJ\u001a\u0010\u001f\u001a\u00020 2\b\u0010\u001a\u001a\u0004\u0018\u00010\u000b2\b\u0010\u001b\u001a\u0004\u0018\u00010\u000bR$\u0010\u0006\u001a\u00020\u00052\u0006\u0010\u0004\u001a\u00020\u0005@@X\u0086\u000e\u00a2\u0006\u000e\n\u0000\u001a\u0004\b\u0007\u0010\b\"\u0004\b\t\u0010\nR(\u0010\f\u001a\u0004\u0018\u00010\u000b2\b\u0010\u0004\u001a\u0004\u0018\u00010\u000b@@X\u0086\u000e\u00a2\u0006\u000e\n\u0000\u001a\u0004\b\r\u0010\u000e\"\u0004\b\u000f\u0010\u0010R$\u0010\u0012\u001a\u00020\u00112\u0006\u0010\u0004\u001a\u00020\u0011@@X\u0086\u000e\u00a2\u0006\u000e\n\u0000\u001a\u0004\b\u0013\u0010\u0014\"\u0004\b\u0015\u0010\u0016\u00a8\u0006!"}, d2 = {"Lcom/example/yakultscanner/ScanRouting;", "", "<init>", "()V", "value", "Lcom/example/yakultscanner/ScanMode;", "mode", "getMode", "()Lcom/example/yakultscanner/ScanMode;", "setMode$app_devDebug", "(Lcom/example/yakultscanner/ScanMode;)V", "", "currentRoute", "getCurrentRoute", "()Ljava/lang/String;", "setCurrentRoute$app_devDebug", "(Ljava/lang/String;)V", "", "honeywellArmed", "getHoneywellArmed", "()Z", "setHoneywellArmed$app_devDebug", "(Z)V", "baseRoute", "route", "computeMode", "current", "previous", "computeMode$app_devDebug", "computeHoneywellArmed", "computeHoneywellArmed$app_devDebug", "updateFromRoutes", "", "app_devDebug"})
public final class ScanRouting {
    @kotlin.jvm.Volatile()
    @org.jetbrains.annotations.NotNull()
    private static volatile com.example.yakultscanner.ScanMode mode = com.example.yakultscanner.ScanMode.None;
    @kotlin.jvm.Volatile()
    @org.jetbrains.annotations.Nullable()
    private static volatile java.lang.String currentRoute;
    @kotlin.jvm.Volatile()
    private static volatile boolean honeywellArmed = false;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.ScanRouting INSTANCE = null;
    
    private ScanRouting() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.ScanMode getMode() {
        return null;
    }
    
    public final void setMode$app_devDebug(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.ScanMode p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCurrentRoute() {
        return null;
    }
    
    public final void setCurrentRoute$app_devDebug(@org.jetbrains.annotations.Nullable()
    java.lang.String p0) {
    }
    
    public final boolean getHoneywellArmed() {
        return false;
    }
    
    public final void setHoneywellArmed$app_devDebug(boolean p0) {
    }
    
    private final java.lang.String baseRoute(java.lang.String route) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.ScanMode computeMode$app_devDebug(@org.jetbrains.annotations.Nullable()
    java.lang.String current, @org.jetbrains.annotations.Nullable()
    java.lang.String previous) {
        return null;
    }
    
    public final boolean computeHoneywellArmed$app_devDebug(@org.jetbrains.annotations.Nullable()
    java.lang.String current) {
        return false;
    }
    
    public final void updateFromRoutes(@org.jetbrains.annotations.Nullable()
    java.lang.String current, @org.jetbrains.annotations.Nullable()
    java.lang.String previous) {
    }
}