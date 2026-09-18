package com.example.yakultscanner.settings;

import android.content.Context;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000(\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u0002\n\u0002\b\u0002\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u000e\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\nJ\u0016\u0010\u000b\u001a\u00020\f2\u0006\u0010\t\u001a\u00020\n2\u0006\u0010\r\u001a\u00020\bR\u000e\u0010\u0004\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u000e"}, d2 = {"Lcom/example/yakultscanner/settings/ThemePreferences;", "", "<init>", "()V", "PREFS_NAME", "", "KEY_DARK_MODE", "isDarkModeEnabled", "", "context", "Landroid/content/Context;", "setDarkModeEnabled", "", "enabled", "app_prodDebug"})
public final class ThemePreferences {
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String PREFS_NAME = "yakult_scanner_preferences";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_DARK_MODE = "dark_mode_enabled";
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.settings.ThemePreferences INSTANCE = null;
    
    private ThemePreferences() {
        super();
    }
    
    public final boolean isDarkModeEnabled(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
        return false;
    }
    
    public final void setDarkModeEnabled(@org.jetbrains.annotations.NotNull()
    android.content.Context context, boolean enabled) {
    }
}