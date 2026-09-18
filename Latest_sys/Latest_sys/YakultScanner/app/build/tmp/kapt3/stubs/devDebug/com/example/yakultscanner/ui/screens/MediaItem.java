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

/**
 * One staged-or-uploaded photo/video, held in memory for the duration of this screen — tagged
 * with the Part it belongs to, since a technician can stage media for several Parts before
 * uploading them all together. Both the "ready to upload" review grid and the "uploaded this
 * session" grid use this same shape so the same preview pager works for either.
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\u001b\b\u0082\b\u0018\u00002\u00020\u0001BC\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0005\u0012\u0006\u0010\u0006\u001a\u00020\u0007\u0012\u0006\u0010\b\u001a\u00020\u0003\u0012\b\u0010\t\u001a\u0004\u0018\u00010\n\u0012\u0006\u0010\u000b\u001a\u00020\f\u0012\u0006\u0010\r\u001a\u00020\u0003\u00a2\u0006\u0004\b\u000e\u0010\u000fJ\t\u0010\u001b\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001c\u001a\u00020\u0005H\u00c6\u0003J\t\u0010\u001d\u001a\u00020\u0007H\u00c6\u0003J\t\u0010\u001e\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u001f\u001a\u0004\u0018\u00010\nH\u00c6\u0003J\t\u0010 \u001a\u00020\fH\u00c6\u0003J\t\u0010!\u001a\u00020\u0003H\u00c6\u0003JQ\u0010\"\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u00072\b\b\u0002\u0010\b\u001a\u00020\u00032\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n2\b\b\u0002\u0010\u000b\u001a\u00020\f2\b\b\u0002\u0010\r\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010#\u001a\u00020\u00072\b\u0010$\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010%\u001a\u00020\fH\u00d6\u0001J\t\u0010&\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u0011R\u0011\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u0013R\u0011\u0010\u0006\u001a\u00020\u0007\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0014R\u0011\u0010\b\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0011R\u0013\u0010\t\u001a\u0004\u0018\u00010\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0017R\u0011\u0010\u000b\u001a\u00020\f\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0019R\u0011\u0010\r\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0011\u00a8\u0006\'"}, d2 = {"Lcom/example/yakultscanner/ui/screens/MediaItem;", "", "id", "", "uri", "Landroid/net/Uri;", "isVideo", "", "mimeType", "previewBitmap", "Landroid/graphics/Bitmap;", "partId", "", "partLabel", "<init>", "(Ljava/lang/String;Landroid/net/Uri;ZLjava/lang/String;Landroid/graphics/Bitmap;ILjava/lang/String;)V", "getId", "()Ljava/lang/String;", "getUri", "()Landroid/net/Uri;", "()Z", "getMimeType", "getPreviewBitmap", "()Landroid/graphics/Bitmap;", "getPartId", "()I", "getPartLabel", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "copy", "equals", "other", "hashCode", "toString", "app_devDebug"})
final class MediaItem {
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String id = null;
    @org.jetbrains.annotations.NotNull()
    private final android.net.Uri uri = null;
    private final boolean isVideo = false;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String mimeType = null;
    @org.jetbrains.annotations.Nullable()
    private final android.graphics.Bitmap previewBitmap = null;
    private final int partId = 0;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String partLabel = null;
    
    public MediaItem(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    android.net.Uri uri, boolean isVideo, @org.jetbrains.annotations.NotNull()
    java.lang.String mimeType, @org.jetbrains.annotations.Nullable()
    android.graphics.Bitmap previewBitmap, int partId, @org.jetbrains.annotations.NotNull()
    java.lang.String partLabel) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getId() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final android.net.Uri getUri() {
        return null;
    }
    
    public final boolean isVideo() {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getMimeType() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final android.graphics.Bitmap getPreviewBitmap() {
        return null;
    }
    
    public final int getPartId() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getPartLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final android.net.Uri component2() {
        return null;
    }
    
    public final boolean component3() {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final android.graphics.Bitmap component5() {
        return null;
    }
    
    public final int component6() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.ui.screens.MediaItem copy(@org.jetbrains.annotations.NotNull()
    java.lang.String id, @org.jetbrains.annotations.NotNull()
    android.net.Uri uri, boolean isVideo, @org.jetbrains.annotations.NotNull()
    java.lang.String mimeType, @org.jetbrains.annotations.Nullable()
    android.graphics.Bitmap previewBitmap, int partId, @org.jetbrains.annotations.NotNull()
    java.lang.String partLabel) {
        return null;
    }
    
    @java.lang.Override()
    public boolean equals(@org.jetbrains.annotations.Nullable()
    java.lang.Object other) {
        return false;
    }
    
    @java.lang.Override()
    public int hashCode() {
        return 0;
    }
    
    @java.lang.Override()
    @org.jetbrains.annotations.NotNull()
    public java.lang.String toString() {
        return null;
    }
}