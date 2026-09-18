package com.example.yakultscanner;

import android.Manifest;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.provider.Settings;
import android.util.Log;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.camera.core.Camera;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.view.PreviewView;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.text.KeyboardOptions;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.IconButtonDefaults;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.material.icons.Icons;
import androidx.compose.ui.focus.FocusRequester;
import androidx.compose.ui.graphics.BlendMode;
import androidx.compose.ui.hapticfeedback.HapticFeedbackType;
import androidx.compose.ui.text.style.TextAlign;
import androidx.compose.ui.text.input.ImeAction;
import androidx.core.content.ContextCompat;
import androidx.lifecycle.Lifecycle;
import androidx.lifecycle.LifecycleEventObserver;
import androidx.navigation.NavController;
import com.google.mlkit.vision.barcode.common.Barcode;
import com.google.mlkit.vision.barcode.BarcodeScannerOptions;
import com.google.mlkit.vision.barcode.BarcodeScanning;
import com.google.mlkit.vision.common.InputImage;
import java.util.concurrent.Executors;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import com.google.gson.Gson;
import com.example.yakultscanner.data.model.DispatchSet;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import java.util.UUID;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000:\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\u001a\"\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u0007H\u0007\u001a$\u0010\b\u001a\u00020\u00012\u0006\u0010\t\u001a\u00020\n2\u0012\u0010\u000b\u001a\u000e\u0012\u0004\u0012\u00020\r\u0012\u0004\u0012\u00020\u00010\fH\u0002\u001a,\u0010\u000e\u001a\u00020\u00012\u0006\u0010\u000f\u001a\u00020\u00102\u0006\u0010\u0011\u001a\u00020\u00122\u0012\u0010\u000b\u001a\u000e\u0012\u0004\u0012\u00020\r\u0012\u0004\u0012\u00020\u00010\fH\u0002\u00a8\u0006\u0013"}, d2 = {"EnhancedScannerScreen", "", "navController", "Landroidx/navigation/NavController;", "useDeviceScannerMode", "", "returnRoute", "", "processImageProxy", "imageProxy", "Landroidx/camera/core/ImageProxy;", "onQrCodeScanned", "Lkotlin/Function1;", "Lcom/google/mlkit/vision/barcode/common/Barcode;", "processGalleryImage", "context", "Landroid/content/Context;", "imageUri", "Landroid/net/Uri;", "app_prodDebug"})
public final class EnhancedScannerScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void EnhancedScannerScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, boolean useDeviceScannerMode, @org.jetbrains.annotations.NotNull()
    java.lang.String returnRoute) {
    }
    
    private static final void processImageProxy(androidx.camera.core.ImageProxy imageProxy, kotlin.jvm.functions.Function1<? super com.google.mlkit.vision.barcode.common.Barcode, kotlin.Unit> onQrCodeScanned) {
    }
    
    private static final void processGalleryImage(android.content.Context context, android.net.Uri imageUri, kotlin.jvm.functions.Function1<? super com.google.mlkit.vision.barcode.common.Barcode, kotlin.Unit> onQrCodeScanned) {
    }
}