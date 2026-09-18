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

/**
 * Invoices are rows in dbo.[Set] too (IsInvoice = 1), but identified by DocumentNumber rather
 * than the QR-scannable SetCode Sets use, so the "which target" prompt needs to ask for a
 * different field label and pass a different query param depending on which entry point
 * (Set vs Invoice) the user tapped.
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0012\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\u0011\b\u0002\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007j\u0002\b\bj\u0002\b\t\u00a8\u0006\n"}, d2 = {"Lcom/example/yakultscanner/DocIdentifierType;", "", "label", "", "<init>", "(Ljava/lang/String;ILjava/lang/String;)V", "getLabel", "()Ljava/lang/String;", "SetCode", "DocumentNumber", "app_prodDebug"})
enum DocIdentifierType {
    /*public static final*/ SetCode /* = new SetCode(null) */,
    /*public static final*/ DocumentNumber /* = new DocumentNumber(null) */;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String label = null;
    
    DocIdentifierType(java.lang.String label) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.DocIdentifierType> getEntries() {
        return null;
    }
}