package com.example.yakultscanner.settings;

import android.content.Context;
import android.net.Uri;
import com.example.yakultscanner.ConnectionHealthStore;
import com.example.yakultscanner.BuildConfig;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00000\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u0002\n\u0002\b\u0005\n\u0002\u0010\u000b\n\u0002\b\n\n\u0002\u0010\b\n\u0000\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u000e\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\tJ\u000e\u0010\u0016\u001a\u00020\u000b2\u0006\u0010\u0017\u001a\u00020\u0005J\u0010\u0010\u0018\u001a\u00020\u00052\u0006\u0010\u0019\u001a\u00020\u0005H\u0002J\u000e\u0010\u001a\u001a\u00020\u00052\u0006\u0010\u001b\u001a\u00020\u001cR\u000e\u0010\u0004\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0007\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u0010\u0010\b\u001a\u0004\u0018\u00010\tX\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u0011\u0010\r\u001a\u00020\u00058F\u00a2\u0006\u0006\u001a\u0004\b\u000e\u0010\u000fR$\u0010\u0012\u001a\u00020\u00112\u0006\u0010\u0010\u001a\u00020\u00118F@FX\u0086\u000e\u00a2\u0006\f\u001a\u0004\b\u0012\u0010\u0013\"\u0004\b\u0014\u0010\u0015\u00a8\u0006\u001d"}, d2 = {"Lcom/example/yakultscanner/settings/ApiSettings;", "", "<init>", "()V", "PREFS_NAME", "", "KEY_BASE_URL", "KEY_OFFLINE_MODE", "appContext", "Landroid/content/Context;", "init", "", "context", "apiBaseUrl", "getApiBaseUrl", "()Ljava/lang/String;", "value", "", "isOfflineMode", "()Z", "setOfflineMode", "(Z)V", "setBaseUrl", "rawUrl", "normalizeBaseUrl", "url", "buildUrlWithPort", "port", "", "app_prodDebug"})
public final class ApiSettings {
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String PREFS_NAME = "yakult_api_settings";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_BASE_URL = "api_base_url";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_OFFLINE_MODE = "offline_mode";
    @org.jetbrains.annotations.Nullable()
    private static android.content.Context appContext;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.settings.ApiSettings INSTANCE = null;
    
    private ApiSettings() {
        super();
    }
    
    public final void init(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getApiBaseUrl() {
        return null;
    }
    
    public final boolean isOfflineMode() {
        return false;
    }
    
    public final void setOfflineMode(boolean value) {
    }
    
    public final void setBaseUrl(@org.jetbrains.annotations.NotNull()
    java.lang.String rawUrl) {
    }
    
    private final java.lang.String normalizeBaseUrl(java.lang.String url) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String buildUrlWithPort(int port) {
        return null;
    }
}