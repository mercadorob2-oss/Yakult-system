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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0016\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\br\u0018\u00002\u00020\u0001:\u0002\u0002\u0003\u0082\u0001\u0002\u0004\u0005\u00a8\u0006\u0006\u00c0\u0006\u0003"}, d2 = {"Lcom/example/yakultscanner/PendingConfirmation;", "", "Deploy", "Upload", "Lcom/example/yakultscanner/PendingConfirmation$Deploy;", "Lcom/example/yakultscanner/PendingConfirmation$Upload;", "app_devDebug"})
abstract interface PendingConfirmation {
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0011H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/PendingConfirmation$Deploy;", "Lcom/example/yakultscanner/PendingConfirmation;", "set", "Lcom/example/yakultscanner/data/model/DispatchSet;", "<init>", "(Lcom/example/yakultscanner/data/model/DispatchSet;)V", "getSet", "()Lcom/example/yakultscanner/data/model/DispatchSet;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "", "app_devDebug"})
    public static final class Deploy implements com.example.yakultscanner.PendingConfirmation {
        @org.jetbrains.annotations.NotNull()
        private final com.example.yakultscanner.data.model.DispatchSet set = null;
        
        public Deploy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.model.DispatchSet set) {
            super();
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.model.DispatchSet getSet() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.model.DispatchSet component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.PendingConfirmation.Deploy copy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.model.DispatchSet set) {
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0011H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/PendingConfirmation$Upload;", "Lcom/example/yakultscanner/PendingConfirmation;", "set", "Lcom/example/yakultscanner/data/model/DispatchSet;", "<init>", "(Lcom/example/yakultscanner/data/model/DispatchSet;)V", "getSet", "()Lcom/example/yakultscanner/data/model/DispatchSet;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "", "app_devDebug"})
    public static final class Upload implements com.example.yakultscanner.PendingConfirmation {
        @org.jetbrains.annotations.NotNull()
        private final com.example.yakultscanner.data.model.DispatchSet set = null;
        
        public Upload(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.model.DispatchSet set) {
            super();
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.model.DispatchSet getSet() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.model.DispatchSet component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.PendingConfirmation.Upload copy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.model.DispatchSet set) {
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
}