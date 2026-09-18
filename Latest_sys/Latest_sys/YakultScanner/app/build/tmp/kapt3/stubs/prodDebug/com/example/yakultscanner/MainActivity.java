package com.example.yakultscanner;

import android.Manifest;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.widget.Toast;
import androidx.activity.ComponentActivity;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.core.content.ContextCompat;
import com.example.yakultscanner.settings.ThemePreferences;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.HoneywellScanReceiver;
import dagger.hilt.android.AndroidEntryPoint;

@dagger.hilt.android.AndroidEntryPoint(value = androidx.activity.ComponentActivity.class)
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u0007\u0018\u00002\u00020\u0001B\u0007\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0012\u0010\t\u001a\u00020\n2\b\u0010\u000b\u001a\u0004\u0018\u00010\fH\u0016J\b\u0010\r\u001a\u00020\nH\u0016J\b\u0010\u000e\u001a\u00020\nH\u0002R\u0010\u0010\u0004\u001a\u0004\u0018\u00010\u0005X\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u0010\u0010\u0006\u001a\u00020\u0007X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\b\u00a8\u0006\u000f"}, d2 = {"Lcom/example/yakultscanner/MainActivity;", "", "<init>", "()V", "honeywellReceiver", "Lcom/example/yakultscanner/HoneywellScanReceiver;", "cameraPermissionRequest", "error/NonExistentClass", "Lerror/NonExistentClass;", "onCreate", "", "savedInstanceState", "Landroid/os/Bundle;", "onDestroy", "requestCameraPermission", "app_prodDebug"})
@kotlin.Suppress(names = {"UnprotectedReceiver"})
public final class MainActivity extends Hilt_MainActivity {
    @org.jetbrains.annotations.Nullable()
    private com.example.yakultscanner.HoneywellScanReceiver honeywellReceiver;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.Object cameraPermissionRequest = null;
    
    public MainActivity() {
        super();
    }
    
    public void onCreate(@org.jetbrains.annotations.Nullable()
    android.os.Bundle savedInstanceState) {
    }
    
    public void onDestroy() {
    }
    
    private final void requestCameraPermission() {
    }
}