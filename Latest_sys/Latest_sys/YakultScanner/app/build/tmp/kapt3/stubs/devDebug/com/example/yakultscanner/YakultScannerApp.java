package com.example.yakultscanner;

import android.app.Application;
import com.example.yakultscanner.settings.ApiSettings;
import com.example.yakultscanner.worker.SyncWorkScheduler;
import dagger.hilt.android.HiltAndroidApp;
import javax.inject.Inject;

@dagger.hilt.android.HiltAndroidApp(value = android.app.Application.class)
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u001a\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010\u0002\n\u0000\b\u0007\u0018\u00002\u00020\u0001B\u0007\u00a2\u0006\u0004\b\u0002\u0010\u0003J\b\u0010\n\u001a\u00020\u000bH\u0016R\u001e\u0010\u0004\u001a\u00020\u00058\u0006@\u0006X\u0087.\u00a2\u0006\u000e\n\u0000\u001a\u0004\b\u0006\u0010\u0007\"\u0004\b\b\u0010\t\u00a8\u0006\f"}, d2 = {"Lcom/example/yakultscanner/YakultScannerApp;", "", "<init>", "()V", "syncScheduler", "Lcom/example/yakultscanner/worker/SyncWorkScheduler;", "getSyncScheduler", "()Lcom/example/yakultscanner/worker/SyncWorkScheduler;", "setSyncScheduler", "(Lcom/example/yakultscanner/worker/SyncWorkScheduler;)V", "onCreate", "", "app_devDebug"})
public final class YakultScannerApp extends Hilt_YakultScannerApp {
    @javax.inject.Inject()
    public com.example.yakultscanner.worker.SyncWorkScheduler syncScheduler;
    
    public YakultScannerApp() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.worker.SyncWorkScheduler getSyncScheduler() {
        return null;
    }
    
    public final void setSyncScheduler(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.worker.SyncWorkScheduler p0) {
    }
    
    public void onCreate() {
    }
}