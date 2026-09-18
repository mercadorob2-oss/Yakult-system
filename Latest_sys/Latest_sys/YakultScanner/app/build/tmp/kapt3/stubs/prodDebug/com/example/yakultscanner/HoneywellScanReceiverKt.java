package com.example.yakultscanner;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\n\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0003\"\u000e\u0010\u0000\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0002\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0003\u001a\u00020\u0001X\u0086T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u0004"}, d2 = {"HONEYWELL_SCAN_ACTION", "", "HONEYWELL_DEFAULT_ACTION", "HONEYWELL_SCAN_DATA_EXTRA", "app_prodDebug"})
public final class HoneywellScanReceiverKt {
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String HONEYWELL_SCAN_ACTION = "com.example.yakultscanner.SCAN";
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String HONEYWELL_DEFAULT_ACTION = "com.honeywell.decode.intent.action.DECODE_DATA";
    @org.jetbrains.annotations.NotNull()
    public static final java.lang.String HONEYWELL_SCAN_DATA_EXTRA = "data";
}