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

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\u0094\u0001\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\b\n\u0000\n\u0002\u0010$\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\t\n\u0002\u0018\u0002\n\u0002\b\t\n\u0002\u0018\u0002\n\u0002\b\b\n\u0002\u0018\u0002\n\u0002\b\u000e\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\u001a8\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u00072\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00052\b\b\u0002\u0010\t\u001a\u00020\nH\u0007\u001a*\u0010\u000b\u001a\u00020\u00012\u0006\u0010\f\u001a\u00020\r2\u0006\u0010\u000e\u001a\u00020\u000f2\b\u0010\u0010\u001a\u0004\u0018\u00010\u00112\b\u0010\u0012\u001a\u0004\u0018\u00010\u0011\u001a\u00a4\u0002\u0010\u0013\u001a\u00020\u00012\u0006\u0010\u0014\u001a\u00020\u000f2\f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00170\u00162\u0012\u0010\u0018\u001a\u000e\u0012\u0004\u0012\u00020\u0017\u0012\u0004\u0012\u00020\u00010\u00192\b\b\u0002\u0010\u001a\u001a\u00020\u001b2\u0012\u0010\u001c\u001a\u000e\u0012\u0004\u0012\u00020\u0011\u0012\u0004\u0012\u00020\u00010\u00192\u0012\u0010\u001d\u001a\u000e\u0012\u0004\u0012\u00020\u0011\u0012\u0004\u0012\u00020\u00010\u00192\u0012\u0010\u001e\u001a\u000e\u0012\u0004\u0012\u00020\u000f\u0012\u0004\u0012\u00020\u00010\u00192\f\u0010\u001f\u001a\b\u0012\u0004\u0012\u00020\u00010 2\f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00010 2\f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\u00010 2\f\u0010#\u001a\b\u0012\u0004\u0012\u00020\u00010 2\u0006\u0010$\u001a\u00020\u00072\u0006\u0010%\u001a\u00020\u00072\u0006\u0010&\u001a\u00020\u00072\u0006\u0010\'\u001a\u00020(2\u0006\u0010\u0006\u001a\u00020\u00072\u0012\u0010)\u001a\u000e\u0012\u0004\u0012\u00020\u0005\u0012\u0004\u0012\u00020+0*2\b\u0010\b\u001a\u0004\u0018\u00010\u00052\u0014\u0010,\u001a\u0010\u0012\u0004\u0012\u00020\u0005\u0012\u0004\u0012\u00020\u0005\u0018\u00010-2\f\u0010.\u001a\b\u0012\u0004\u0012\u00020\u00010 2\b\b\u0002\u0010/\u001a\u00020\u0007H\u0007\u001a8\u00100\u001a\u00020\u00012\u0006\u0010\u000e\u001a\u00020\u000f2\u0006\u00101\u001a\u00020(2\u0006\u00102\u001a\u00020(2\u0006\u0010\'\u001a\u00020(2\u0006\u0010\u0006\u001a\u00020\u00072\u0006\u0010/\u001a\u00020\u0007H\u0003\u001a\"\u00103\u001a\u00020\u00012\u0006\u00104\u001a\u00020\u00052\u0006\u00105\u001a\u00020\u00052\b\b\u0002\u0010\u001a\u001a\u00020\u001bH\u0003\u001a\u0017\u00106\u001a\u0002072\b\u00108\u001a\u0004\u0018\u00010\u0005H\u0002\u00a2\u0006\u0002\u00109\u001a\u0010\u0010:\u001a\u00020\u00012\u0006\u0010\u000e\u001a\u00020\u000fH\u0007\u001a\u0016\u0010;\u001a\u00020\u0005*\u0004\u0018\u00010\u00052\u0006\u0010<\u001a\u00020\u0005H\u0002\u001a\"\u0010=\u001a\u00020\u00012\u0006\u00104\u001a\u00020\u00052\u0006\u00105\u001a\u00020\u00052\b\b\u0002\u0010\u001a\u001a\u00020\u001bH\u0007\u001a\u0018\u0010>\u001a\u00020\u00012\u0006\u00104\u001a\u00020\u00052\u0006\u00105\u001a\u00020\u0005H\u0003\u001aB\u0010?\u001a\u00020\u00012\u0006\u0010@\u001a\u00020A2\u0006\u0010B\u001a\u00020\u00172\u0012\u0010C\u001a\u000e\u0012\u0004\u0012\u00020\u0017\u0012\u0004\u0012\u00020\u00010\u00192\b\b\u0002\u0010\u0006\u001a\u00020\u00072\n\b\u0002\u0010D\u001a\u0004\u0018\u00010+H\u0007\u001a*\u0010E\u001a\u00020\u00012\u0006\u0010@\u001a\u00020A2\u0006\u0010B\u001a\u00020\u00172\b\u0010D\u001a\u0004\u0018\u00010+2\u0006\u0010F\u001a\u00020\u0007H\u0003\u001a\b\u0010G\u001a\u00020\u0001H\u0003\u001a9\u0010H\u001a\u00020\u00012\u0006\u00104\u001a\u00020\u00052\b\u0010I\u001a\u0004\u0018\u00010J2\u0006\u0010K\u001a\u0002072\u0006\u0010L\u001a\u0002072\u0006\u0010M\u001a\u000207H\u0003\u00a2\u0006\u0004\bN\u0010O\u001a\u0014\u0010P\u001a\u0004\u0018\u00010\u00052\b\u0010Q\u001a\u0004\u0018\u00010\u0005H\u0002\u001a\u0012\u0010R\u001a\u00020\u00072\b\u0010Q\u001a\u0004\u0018\u00010\u0005H\u0002\u001a(\u0010S\u001a\u00020\u00012\b\u0010\b\u001a\u0004\u0018\u00010\u00052\u0006\u0010T\u001a\u00020\u00052\f\u0010U\u001a\b\u0012\u0004\u0012\u00020\u00010 H\u0003\u001a\u0010\u0010V\u001a\u00020\u00052\u0006\u0010W\u001a\u00020\u0005H\u0002\u001a.\u0010X\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020Z0\u00160Y2\b\u0010\b\u001a\u0004\u0018\u00010\u00052\b\u0010T\u001a\u0004\u0018\u00010\u0005H\u0082@\u00a2\u0006\u0002\u0010[\u00a8\u0006\\"}, d2 = {"DispatchDetailsScreen", "", "navController", "Landroidx/navigation/NavController;", "scannedString", "", "readOnly", "", "token", "viewModel", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsViewModel;", "handlePdfGeneration", "context", "Landroid/content/Context;", "set", "Lcom/example/yakultscanner/data/model/DispatchSet;", "dispatcherView", "Lcom/example/yakultscanner/ui/components/SignatureView;", "requesterView", "DispatchDetailsContent", "dispatchSet", "itemEdits", "", "Lcom/example/yakultscanner/data/model/ItemEditState;", "onItemEditChange", "Lkotlin/Function1;", "modifier", "Landroidx/compose/ui/Modifier;", "registerDispatcherSignatureView", "registerRequesterSignatureView", "onGeneratePdf", "onUpload", "Lkotlin/Function0;", "onViewUploads", "onDeploy", "onSyncNow", "canDeploy", "isUploading", "isSyncing", "pendingCount", "", "updatesBySerial", "", "Lcom/example/yakultscanner/api/SetItemUpdateDto;", "actionErrorUi", "Lkotlin/Pair;", "onRetryLastAction", "deployed", "DispatchSummaryHero", "activeItemCount", "sparedItemCount", "DispatchMetricChip", "label", "value", "dispatchStatusColor", "Landroidx/compose/ui/graphics/Color;", "status", "(Ljava/lang/String;)J", "SetInformationCard", "displayOr", "fallback", "InfoRow", "InfoField", "ItemCard", "item", "Lcom/example/yakultscanner/data/model/DispatchItem;", "editState", "onReportIssueClick", "processedUpdate", "ItemCardContent", "showReportIssueHint", "ReportIssueHintBanner", "StatusPill", "icon", "Landroidx/compose/ui/graphics/vector/ImageVector;", "containerColor", "borderColor", "contentColor", "StatusPill-FLEW7EY", "(Ljava/lang/String;Landroidx/compose/ui/graphics/vector/ImageVector;JJJ)V", "normalizeRepairActionLabel", "raw", "isSpareToInventoryAction", "ViewImagesDialog", "setCode", "onDismiss", "formatDate", "dateString", "fetchSetImages", "Lcom/example/yakultscanner/api/ApiResult;", "Lcom/example/yakultscanner/api/SetImageDto;", "(Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "app_devDebug"})
public final class DispatchDetailsScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void DispatchDetailsScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    java.lang.String scannedString, boolean readOnly, @org.jetbrains.annotations.Nullable()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.DispatchDetailsViewModel viewModel) {
    }
    
    public static final void handlePdfGeneration(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.DispatchSet set, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.ui.components.SignatureView dispatcherView, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.ui.components.SignatureView requesterView) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void DispatchDetailsContent(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.DispatchSet dispatchSet, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.model.ItemEditState> itemEdits, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.data.model.ItemEditState, kotlin.Unit> onItemEditChange, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.ui.components.SignatureView, kotlin.Unit> registerDispatcherSignatureView, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.ui.components.SignatureView, kotlin.Unit> registerRequesterSignatureView, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.data.model.DispatchSet, kotlin.Unit> onGeneratePdf, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onUpload, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onViewUploads, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDeploy, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onSyncNow, boolean canDeploy, boolean isUploading, boolean isSyncing, int pendingCount, boolean readOnly, @org.jetbrains.annotations.NotNull()
    java.util.Map<java.lang.String, com.example.yakultscanner.api.SetItemUpdateDto> updatesBySerial, @org.jetbrains.annotations.Nullable()
    java.lang.String token, @org.jetbrains.annotations.Nullable()
    kotlin.Pair<java.lang.String, java.lang.String> actionErrorUi, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onRetryLastAction, boolean deployed) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DispatchSummaryHero(com.example.yakultscanner.data.model.DispatchSet set, int activeItemCount, int sparedItemCount, int pendingCount, boolean readOnly, boolean deployed) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DispatchMetricChip(java.lang.String label, java.lang.String value, androidx.compose.ui.Modifier modifier) {
    }
    
    private static final long dispatchStatusColor(java.lang.String status) {
        return 0L;
    }
    
    @androidx.compose.runtime.Composable()
    public static final void SetInformationCard(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.DispatchSet set) {
    }
    
    private static final java.lang.String displayOr(java.lang.String $this$displayOr, java.lang.String fallback) {
        return null;
    }
    
    @androidx.compose.runtime.Composable()
    public static final void InfoRow(@org.jetbrains.annotations.NotNull()
    java.lang.String label, @org.jetbrains.annotations.NotNull()
    java.lang.String value, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void InfoField(java.lang.String label, java.lang.String value) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void ItemCard(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.DispatchItem item, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.ItemEditState editState, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.data.model.ItemEditState, kotlin.Unit> onReportIssueClick, boolean readOnly, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.SetItemUpdateDto processedUpdate) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItemCardContent(com.example.yakultscanner.data.model.DispatchItem item, com.example.yakultscanner.data.model.ItemEditState editState, com.example.yakultscanner.api.SetItemUpdateDto processedUpdate, boolean showReportIssueHint) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ReportIssueHintBanner() {
    }
    
    private static final java.lang.String normalizeRepairActionLabel(java.lang.String raw) {
        return null;
    }
    
    private static final boolean isSpareToInventoryAction(java.lang.String raw) {
        return false;
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ViewImagesDialog(java.lang.String token, java.lang.String setCode, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss) {
    }
    
    private static final java.lang.String formatDate(java.lang.String dateString) {
        return null;
    }
    
    private static final java.lang.Object fetchSetImages(java.lang.String token, java.lang.String setCode, kotlin.coroutines.Continuation<? super com.example.yakultscanner.api.ApiResult<? extends java.util.List<com.example.yakultscanner.api.SetImageDto>>> $completion) {
        return null;
    }
}