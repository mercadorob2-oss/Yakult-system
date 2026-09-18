package com.example.yakultscanner;

import android.content.Context;
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
import java.io.ByteArrayOutputStream;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.compose.ui.text.style.TextAlign;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.SetItemUpdateDto;
import com.example.yakultscanner.data.model.DispatchItem;
import com.example.yakultscanner.settings.ApiSettings;
import coil.request.ImageRequest;
import com.example.yakultscanner.data.model.DispatchSet;
import com.example.yakultscanner.data.model.ItemEditState;
import com.example.yakultscanner.ui.components.SignatureView;
import com.example.yakultscanner.api.SetConfirmationRequest;
import com.example.yakultscanner.api.SetImageDto;
import com.example.yakultscanner.api.SetImageUploadRequest;
import com.google.mlkit.vision.documentscanner.GmsDocumentScannerOptions;
import com.google.mlkit.vision.documentscanner.GmsDocumentScanning;
import com.google.mlkit.vision.documentscanner.GmsDocumentScanningResult;
import com.example.yakultscanner.network.ConnectivityMonitor;
import com.example.yakultscanner.viewmodels.DispatchDetailsUiState;
import com.example.yakultscanner.viewmodels.DispatchDetailsViewModel;
import com.example.yakultscanner.viewmodels.UploadEvent;
import kotlinx.coroutines.Dispatchers;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\u0005\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005\u00a8\u0006\u0006"}, d2 = {"Lcom/example/yakultscanner/FailedAction;", "", "<init>", "(Ljava/lang/String;I)V", "Deploy", "Upload", "app_devDebug"})
enum FailedAction {
    /*public static final*/ Deploy /* = new Deploy() */,
    /*public static final*/ Upload /* = new Upload() */;
    
    FailedAction() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.FailedAction> getEntries() {
        return null;
    }
}