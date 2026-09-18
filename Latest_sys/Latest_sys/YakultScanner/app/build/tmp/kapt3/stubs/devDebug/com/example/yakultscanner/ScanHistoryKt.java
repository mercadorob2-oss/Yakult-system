package com.example.yakultscanner;

import android.content.Context;
import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import com.example.yakultscanner.data.model.DispatchSet;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000B\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010!\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010 \n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010$\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0000\u001a\u0016\u0010\u0004\u001a\b\u0012\u0004\u0012\u00020\u00060\u00052\u0006\u0010\u0007\u001a\u00020\bH\u0002\u001a\u001e\u0010\t\u001a\u00020\n2\u0006\u0010\u0007\u001a\u00020\b2\f\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\u00060\fH\u0002\u001a\u0018\u0010\r\u001a\u00020\u00012\u0006\u0010\u000e\u001a\u00020\u00012\u0006\u0010\u000f\u001a\u00020\u0001H\u0002\u001a \u0010\u0010\u001a\u00020\n2\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\u000e\u001a\u00020\u00012\b\u0010\u0011\u001a\u0004\u0018\u00010\u0012\u001a \u0010\u0013\u001a\u00020\n2\u0006\u0010\u0007\u001a\u00020\b2\b\u0010\u0014\u001a\u0004\u0018\u00010\u00012\u0006\u0010\u000f\u001a\u00020\u0001\u001a\"\u0010\u0015\u001a\u00020\n2\u0006\u0010\u0007\u001a\u00020\b2\u0012\u0010\u0016\u001a\u000e\u0012\u0004\u0012\u00020\u0001\u0012\u0004\u0012\u00020\u00010\u0017\u001a\u0014\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00060\f2\u0006\u0010\u0007\u001a\u00020\b\u001a\u000e\u0010\u0019\u001a\u00020\u001a2\u0006\u0010\u0007\u001a\u00020\b\"\u000e\u0010\u0000\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0002\u001a\u00020\u0003X\u0082T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u001b"}, d2 = {"HISTORY_FILE_NAME", "", "HISTORY_MAX_SIZE", "", "readScanHistoryEntries", "", "Lcom/example/yakultscanner/ScanHistoryEntry;", "context", "Landroid/content/Context;", "writeScanHistoryEntries", "", "entries", "", "rewriteDispatchSetStatus", "rawJson", "newStatus", "addScanToHistory", "dispatchSet", "Lcom/example/yakultscanner/data/model/DispatchSet;", "updateScanHistoryStatus", "setCode", "updateScanHistoryStatuses", "statusesBySetCode", "", "loadScanHistory", "clearScanHistory", "", "app_devDebug"})
public final class ScanHistoryKt {
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String HISTORY_FILE_NAME = "scan_history.json";
    private static final int HISTORY_MAX_SIZE = 50;
    
    private static final java.util.List<com.example.yakultscanner.ScanHistoryEntry> readScanHistoryEntries(android.content.Context context) {
        return null;
    }
    
    private static final void writeScanHistoryEntries(android.content.Context context, java.util.List<com.example.yakultscanner.ScanHistoryEntry> entries) {
    }
    
    private static final java.lang.String rewriteDispatchSetStatus(java.lang.String rawJson, java.lang.String newStatus) {
        return null;
    }
    
    public static final void addScanToHistory(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.lang.String rawJson, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.data.model.DispatchSet dispatchSet) {
    }
    
    public static final void updateScanHistoryStatus(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    java.lang.String newStatus) {
    }
    
    public static final void updateScanHistoryStatuses(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.util.Map<java.lang.String, java.lang.String> statusesBySetCode) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static final java.util.List<com.example.yakultscanner.ScanHistoryEntry> loadScanHistory(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
        return null;
    }
    
    public static final boolean clearScanHistory(@org.jetbrains.annotations.NotNull()
    android.content.Context context) {
        return false;
    }
}