package com.example.yakultscanner;

import android.content.Context;
import com.example.yakultscanner.api.LoginResponse;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00008\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\t\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\b\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0007\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0006\u0010\u0019\u001a\u00020\u001aJ\u000e\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u000fJ\u0016\u0010\u001e\u001a\u00020\u001c2\u0006\u0010\u001f\u001a\u00020\u00052\u0006\u0010 \u001a\u00020!J\u0016\u0010\"\u001a\u00020\u001c2\u0006\u0010#\u001a\u00020\u00052\u0006\u0010$\u001a\u00020\u0005J\u0016\u0010%\u001a\u00020\u001a2\u0006\u0010#\u001a\u00020\u00052\u0006\u0010$\u001a\u00020\u0005J\u0006\u0010&\u001a\u00020\u001cJ\u0010\u0010\'\u001a\u00020\u00052\u0006\u0010$\u001a\u00020\u0005H\u0002R\u000e\u0010\u0004\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0007\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\b\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\t\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\n\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u000b\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\f\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\r\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u0010\u0010\u000e\u001a\u0004\u0018\u00010\u000fX\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u001c\u0010\u0010\u001a\u0004\u0018\u00010\u0011X\u0086\u000e\u00a2\u0006\u000e\n\u0000\u001a\u0004\b\u0012\u0010\u0013\"\u0004\b\u0014\u0010\u0015R\u0013\u0010\u0016\u001a\u0004\u0018\u00010\u00058F\u00a2\u0006\u0006\u001a\u0004\b\u0017\u0010\u0018\u00a8\u0006("}, d2 = {"Lcom/example/yakultscanner/UserSession;", "", "<init>", "()V", "PREFS_NAME", "", "KEY_USER_ID", "KEY_USERNAME", "KEY_DISPLAY_NAME", "KEY_EMAIL", "KEY_TOKEN", "KEY_EXPIRES_UTC", "KEY_CACHED_USERNAME", "KEY_CACHED_PASSWORD_HASH", "appContext", "Landroid/content/Context;", "currentUser", "Lcom/example/yakultscanner/LoggedInUser;", "getCurrentUser", "()Lcom/example/yakultscanner/LoggedInUser;", "setCurrentUser", "(Lcom/example/yakultscanner/LoggedInUser;)V", "authToken", "getAuthToken", "()Ljava/lang/String;", "isLoggedIn", "", "init", "", "context", "loginFromApi", "loginUsername", "response", "Lcom/example/yakultscanner/api/LoginResponse;", "cacheCredentials", "username", "password", "attemptOfflineLogin", "logout", "hashPassword", "app_prodDebug"})
public final class UserSession {
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String PREFS_NAME = "yakult_scanner_session";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_USER_ID = "user_id";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_USERNAME = "username";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_DISPLAY_NAME = "display_name";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_EMAIL = "email";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_TOKEN = "token";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_EXPIRES_UTC = "expires_utc";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_CACHED_USERNAME = "cached_username";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_CACHED_PASSWORD_HASH = "cached_password_hash";
    @org.jetbrains.annotations.Nullable()
    private static android.content.Context appContext;
    @org.jetbrains.annotations.Nullable()
    private static com.example.yakultscanner.LoggedInUser currentUser;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.UserSession INSTANCE = null;
    
    private UserSession() {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.LoggedInUser getCurrentUser() {
        return null;
    }
    
    public final void setCurrentUser(@org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.LoggedInUser p0) {
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getAuthToken() {
        return null;
    }
    
    public final boolean isLoggedIn() {
        return false;
    }
    
    public final void init(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
    }
    
    public final void loginFromApi(@org.jetbrains.annotations.NotNull()
    java.lang.String loginUsername, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.LoginResponse response) {
    }
    
    public final void cacheCredentials(@org.jetbrains.annotations.NotNull()
    java.lang.String username, @org.jetbrains.annotations.NotNull()
    java.lang.String password) {
    }
    
    public final boolean attemptOfflineLogin(@org.jetbrains.annotations.NotNull()
    java.lang.String username, @org.jetbrains.annotations.NotNull()
    java.lang.String password) {
        return false;
    }
    
    public final void logout() {
    }
    
    private final java.lang.String hashPassword(java.lang.String password) {
        return null;
    }
}