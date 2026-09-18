package com.example.yakultscanner.ui.screens;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.util.Base64;
import android.widget.MediaController;
import android.widget.VideoView;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.compose.foundation.ExperimentalFoundationApi;
import androidx.compose.foundation.layout.*;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.*;
import androidx.compose.runtime.*;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.window.DialogProperties;
import androidx.core.content.FileProvider;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.RepairPartPhotoUploadRequest;
import com.example.yakultscanner.api.RepairPartSummaryDto;
import com.example.yakultscanner.api.RepairTicketSummaryDto;
import com.example.yakultscanner.data.repository.RepairPhotoRepository;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.util.UUID;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000D\n\u0000\n\u0002\u0010\u000e\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\t\n\u0002\b\u0002\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0000\u001a\f\u0010\u0000\u001a\u00020\u0001*\u00020\u0002H\u0002\u001a\u0018\u0010\u0006\u001a\u00020\u00072\u0006\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\u0001H\u0007\u001aZ\u0010\u000b\u001a\u00020\u00072\u0012\u0010\f\u001a\u000e\u0012\u0004\u0012\u00020\u000e\u0012\u0004\u0012\u00020\u00010\r2\f\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00110\u00102\u0006\u0010\u0012\u001a\u00020\u00132\u0012\u0010\u0014\u001a\u000e\u0012\u0004\u0012\u00020\u0011\u0012\u0004\u0012\u00020\u00070\r2\u0012\u0010\u0015\u001a\u000e\u0012\u0004\u0012\u00020\u000e\u0012\u0004\u0012\u00020\u00070\rH\u0003\u001a,\u0010\u0016\u001a\u00020\u00072\f\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00110\u00102\u0006\u0010\u0017\u001a\u00020\u000e2\f\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00070\u0019H\u0003\"\u000e\u0010\u0003\u001a\u00020\u0004X\u0082T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0005\u001a\u00020\u0004X\u0082T\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u001a"}, d2 = {"label", "", "Lcom/example/yakultscanner/api/RepairPartSummaryDto;", "MAX_UPLOAD_BYTES", "", "MAX_UPLOAD_MB", "RepairPhotoUploadScreen", "", "navController", "Landroidx/navigation/NavController;", "ticketRef", "MediaGridSection", "title", "Lkotlin/Function1;", "", "items", "", "Lcom/example/yakultscanner/ui/screens/MediaItem;", "showRemove", "", "onRemove", "onTap", "MediaPreviewDialog", "startIndex", "onDismiss", "Lkotlin/Function0;", "app_devDebug"})
public final class RepairPhotoUploadScreenKt {
    private static final long MAX_UPLOAD_BYTES = 20971520L;
    private static final long MAX_UPLOAD_MB = 20L;
    
    private static final java.lang.String label(com.example.yakultscanner.api.RepairPartSummaryDto $this$label) {
        return null;
    }
    
    /**
     * Second step of the mobile Repair Part photo upload flow — resolves the scanned/typed ticket
     * reference (a plain Repair No., or a scanned "yakult:repair:v1:{guid}" QR token), lets the
     * technician tag photos/videos to one or more Parts, review/drop any before committing, then
     * uploads straight from the phone's camera or gallery instead of copying files to a PC.
     */
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void RepairPhotoUploadScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    java.lang.String ticketRef) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void MediaGridSection(kotlin.jvm.functions.Function1<? super java.lang.Integer, java.lang.String> title, java.util.List<com.example.yakultscanner.ui.screens.MediaItem> items, boolean showRemove, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.ui.screens.MediaItem, kotlin.Unit> onRemove, kotlin.jvm.functions.Function1<? super java.lang.Integer, kotlin.Unit> onTap) {
    }
    
    /**
     * Fullscreen preview — swipe left/right (HorizontalPager) or tap the < / > buttons to navigate.
     * Images render directly from the in-memory bitmap; videos play via a VideoView pointed at the
     * original content Uri (no extra player dependency needed for local playback). Shows which Part
     * each item belongs to, since the queue can span several Parts at once.
     */
    @kotlin.OptIn(markerClass = {androidx.compose.foundation.ExperimentalFoundationApi.class})
    @androidx.compose.runtime.Composable()
    private static final void MediaPreviewDialog(java.util.List<com.example.yakultscanner.ui.screens.MediaItem> items, int startIndex, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss) {
    }
}