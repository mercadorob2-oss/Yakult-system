package com.example.yakultscanner;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.util.Base64;
import android.widget.Toast;
import androidx.activity.result.IntentSenderRequest;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.compose.foundation.layout.*;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.*;
import androidx.compose.runtime.*;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.layout.ContentScale;
import androidx.compose.ui.focus.FocusRequester;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.compose.ui.window.DialogProperties;
import androidx.navigation.NavController;
import kotlinx.coroutines.Dispatchers;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.SerialFromMobileRequest;
import com.example.yakultscanner.api.SetImageUploadRequest;
import com.example.yakultscanner.api.ReceiptImageUploadRequest;
import com.google.mlkit.vision.documentscanner.GmsDocumentScannerOptions;
import com.google.mlkit.vision.documentscanner.GmsDocumentScanning;
import com.google.mlkit.vision.documentscanner.GmsDocumentScanningResult;
import java.io.ByteArrayOutputStream;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000Z\n\u0000\n\u0002\u0010\u0012\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010\u000b\n\u0002\b\u0004\u001a\u0016\u0010\u0000\u001a\u00020\u00012\f\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003H\u0002\u001a>\u0010\u0005\u001a\u00020\u00062\f\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\b0\u00032\f\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u00060\n2\u0018\u0010\u000b\u001a\u0014\u0012\n\u0012\b\u0012\u0004\u0012\u00020\b0\u0003\u0012\u0004\u0012\u00020\u00060\fH\u0003\u001a\u0010\u0010\r\u001a\u00020\u00062\u0006\u0010\u000e\u001a\u00020\u000fH\u0007\u001a\"\u0010\u0010\u001a\u00020\u00062\u0006\u0010\u0011\u001a\u00020\b2\u0006\u0010\u0012\u001a\u00020\b2\b\b\u0002\u0010\u0013\u001a\u00020\u0014H\u0003\u001ah\u0010\u0015\u001a\u00020\u00062\u0006\u0010\u0016\u001a\u00020\u00172\u0006\u0010\u0018\u001a\u00020\u00192\u0006\u0010\u001a\u001a\u00020\b2\b\u0010\u001b\u001a\u0004\u0018\u00010\u001c2\u0012\u0010\u001d\u001a\u000e\u0012\u0004\u0012\u00020\u001c\u0012\u0004\u0012\u00020\u00060\f2\u0012\u0010\u001e\u001a\u000e\u0012\u0004\u0012\u00020\u0019\u0012\u0004\u0012\u00020\u00060\f2\f\u0010\u001f\u001a\b\u0012\u0004\u0012\u00020\u00060\n2\u0006\u0010 \u001a\u00020!H\u0003\u001a&\u0010\"\u001a\u00020\u00062\u0006\u0010#\u001a\u00020\b2\u0006\u0010$\u001a\u00020\u00172\f\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u00060\nH\u0007\u00a8\u0006%"}, d2 = {"bitmapsToPdfBytes", "", "bitmaps", "", "Landroid/graphics/Bitmap;", "SendToWindowsConfirmDialog", "", "serials", "", "onDismiss", "Lkotlin/Function0;", "onConfirm", "Lkotlin/Function1;", "DirectSerialScanScreen", "navController", "Landroidx/navigation/NavController;", "SessionMetric", "label", "value", "modifier", "Landroidx/compose/ui/Modifier;", "DirectPhoneEntryCard", "index", "", "row", "Lcom/example/yakultscanner/DirectPhoneEntry;", "status", "activeFocusField", "Lcom/example/yakultscanner/PhoneFocusField;", "onFocusedField", "onChange", "onRemove", "canRemove", "", "SwipeToDismissItem", "item", "indexLabel", "app_prodDebug"})
public final class DirectSerialScanScreenKt {
    
    /**
     * Combines scanned pages into a single multi-page PDF using the platform PdfDocument API (no
     * extra library needed). Each page is re-encoded through JPEG at the same quality as standalone
     * Image-mode uploads first, rather than drawing the raw full-resolution bitmap straight into the
     * PDF -- multi-page PDFs from full-res camera captures were large enough to fail uploads even
     * after raising the server's request-size limit.
     */
    private static final byte[] bitmapsToPdfBytes(java.util.List<android.graphics.Bitmap> bitmaps) {
        return null;
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SendToWindowsConfirmDialog(java.util.List<java.lang.String> serials, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function1<? super java.util.List<java.lang.String>, kotlin.Unit> onConfirm) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void DirectSerialScanScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SessionMetric(java.lang.String label, java.lang.String value, androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DirectPhoneEntryCard(int index, com.example.yakultscanner.DirectPhoneEntry row, java.lang.String status, com.example.yakultscanner.PhoneFocusField activeFocusField, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.PhoneFocusField, kotlin.Unit> onFocusedField, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.DirectPhoneEntry, kotlin.Unit> onChange, kotlin.jvm.functions.Function0<kotlin.Unit> onRemove, boolean canRemove) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void SwipeToDismissItem(@org.jetbrains.annotations.NotNull()
    java.lang.String item, int indexLabel, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss) {
    }
}