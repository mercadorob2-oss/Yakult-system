package com.example.yakultscanner;

import android.content.Context;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00000\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\"\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0002\b\u0002\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0014\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\u00050\b2\u0006\u0010\t\u001a\u00020\nJ\u0016\u0010\u000b\u001a\u00020\f2\u0006\u0010\t\u001a\u00020\n2\u0006\u0010\r\u001a\u00020\u0005J\u001c\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\u00050\b2\u0006\u0010\t\u001a\u00020\n2\u0006\u0010\r\u001a\u00020\u0005J\u000e\u0010\u000f\u001a\u00020\u00102\u0006\u0010\t\u001a\u00020\nJ\u0010\u0010\u0011\u001a\u00020\u00052\u0006\u0010\r\u001a\u00020\u0005H\u0002R\u000e\u0010\u0004\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0005X\u0082T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/PinnedSetsStore;", "", "<init>", "()V", "PREFS_NAME", "", "KEY_PINNED", "load", "", "context", "Landroid/content/Context;", "isPinned", "", "setCode", "toggle", "clear", "", "normalize", "app_prodDebug"})
public final class PinnedSetsStore {
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String PREFS_NAME = "yakult_scanner_pins";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String KEY_PINNED = "pinned_set_codes";
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.PinnedSetsStore INSTANCE = null;
    
    private PinnedSetsStore() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.Set<java.lang.String> load(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
        return null;
    }
    
    public final boolean isPinned(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.lang.String setCode) {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.Set<java.lang.String> toggle(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.lang.String setCode) {
        return null;
    }
    
    public final void clear(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
    }
    
    private final java.lang.String normalize(java.lang.String setCode) {
        return null;
    }
}