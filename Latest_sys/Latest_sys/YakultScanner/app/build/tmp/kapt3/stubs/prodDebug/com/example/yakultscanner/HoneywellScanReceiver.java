package com.example.yakultscanner;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\u0018\u0000 \n2\u00020\u0001:\u0001\nB\u0007\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0018\u0010\u0004\u001a\u00020\u00052\u0006\u0010\u0006\u001a\u00020\u00072\u0006\u0010\b\u001a\u00020\tH\u0016\u00a8\u0006\u000b"}, d2 = {"Lcom/example/yakultscanner/HoneywellScanReceiver;", "Landroid/content/BroadcastReceiver;", "<init>", "()V", "onReceive", "", "context", "Landroid/content/Context;", "intent", "Landroid/content/Intent;", "Companion", "app_prodDebug"})
public final class HoneywellScanReceiver extends android.content.BroadcastReceiver {
    private static long lastScanAtMs = 0L;
    private static final int MAX_PAYLOAD_CHARS = 20000;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.HoneywellScanReceiver.Companion Companion = null;
    
    public HoneywellScanReceiver() {
        super();
    }
    
    @java.lang.Override()
    public void onReceive(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    android.content.Intent intent) {
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\t\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0004\b\u0086\u0003\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0012\u0010\b\u001a\u0004\u0018\u00010\t2\u0006\u0010\n\u001a\u00020\tH\u0002J\u0010\u0010\u000b\u001a\u00020\t2\u0006\u0010\f\u001a\u00020\tH\u0002R\u000e\u0010\u0004\u001a\u00020\u0005X\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0007X\u0082T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\r"}, d2 = {"Lcom/example/yakultscanner/HoneywellScanReceiver$Companion;", "", "<init>", "()V", "lastScanAtMs", "", "MAX_PAYLOAD_CHARS", "", "sanitizePayload", "", "raw", "previewForLog", "value", "app_prodDebug"})
    public static final class Companion {
        
        private Companion() {
            super();
        }
        
        private final java.lang.String sanitizePayload(java.lang.String raw) {
            return null;
        }
        
        private final java.lang.String previewForLog(java.lang.String value) {
            return null;
        }
    }
}