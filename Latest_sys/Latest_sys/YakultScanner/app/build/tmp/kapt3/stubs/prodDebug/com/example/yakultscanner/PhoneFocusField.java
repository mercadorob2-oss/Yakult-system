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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\u0007\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005j\u0002\b\u0006j\u0002\b\u0007\u00a8\u0006\b"}, d2 = {"Lcom/example/yakultscanner/PhoneFocusField;", "", "<init>", "(Ljava/lang/String;I)V", "CellPhone", "Serial", "Imei1", "Imei2", "app_prodDebug"})
enum PhoneFocusField {
    /*public static final*/ CellPhone /* = new CellPhone() */,
    /*public static final*/ Serial /* = new Serial() */,
    /*public static final*/ Imei1 /* = new Imei1() */,
    /*public static final*/ Imei2 /* = new Imei2() */;
    
    PhoneFocusField() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.PhoneFocusField> getEntries() {
        return null;
    }
}