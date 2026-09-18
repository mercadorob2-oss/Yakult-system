package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.SetItemUpdateDto;
import com.example.yakultscanner.api.SetItemUpdateEntry;
import com.example.yakultscanner.api.SetUpdatesRequest;
import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.data.repository.SyncRepository;
import com.example.yakultscanner.data.repository.SyncResult;
import com.example.yakultscanner.data.model.DispatchSet;
import com.example.yakultscanner.data.model.ItemEditState;
import com.google.gson.Gson;
import kotlinx.coroutines.flow.StateFlow;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import com.example.yakultscanner.network.ConnectivityMonitor;
import com.example.yakultscanner.api.YakultApiService;
import dagger.hilt.android.lifecycle.HiltViewModel;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0016\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b6\u0018\u00002\u00020\u0001:\u0002\u0004\u0005B\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0002\u0006\u0007\u00a8\u0006\b"}, d2 = {"Lcom/example/yakultscanner/viewmodels/UploadEvent;", "", "<init>", "()V", "Success", "Error", "Lcom/example/yakultscanner/viewmodels/UploadEvent$Error;", "Lcom/example/yakultscanner/viewmodels/UploadEvent$Success;", "app_devDebug"})
public abstract class UploadEvent {
    
    private UploadEvent() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\n\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u0017\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0005\u0010\u0006J\t\u0010\n\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u000b\u001a\u00020\u0003H\u00c6\u0003J\u001d\u0010\f\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\r\u001a\u00020\u000e2\b\u0010\u000f\u001a\u0004\u0018\u00010\u0010H\u00d6\u0003J\t\u0010\u0011\u001a\u00020\u0012H\u00d6\u0001J\t\u0010\u0013\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0007\u0010\bR\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\b\u00a8\u0006\u0014"}, d2 = {"Lcom/example/yakultscanner/viewmodels/UploadEvent$Error;", "Lcom/example/yakultscanner/viewmodels/UploadEvent;", "message", "", "details", "<init>", "(Ljava/lang/String;Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "getDetails", "component1", "component2", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Error extends com.example.yakultscanner.viewmodels.UploadEvent {
        @org.jetbrains.annotations.NotNull()
        private final java.lang.String message = null;
        @org.jetbrains.annotations.NotNull()
        private final java.lang.String details = null;
        
        public Error(@org.jetbrains.annotations.NotNull()
        java.lang.String message, @org.jetbrains.annotations.NotNull()
        java.lang.String details) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String getMessage() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String getDetails() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String component2() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.viewmodels.UploadEvent.Error copy(@org.jetbrains.annotations.NotNull()
        java.lang.String message, @org.jetbrains.annotations.NotNull()
        java.lang.String details) {
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u000b\n\u0002\u0010\u0000\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u0019\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\u0004\b\u0006\u0010\u0007J\t\u0010\f\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\r\u001a\u00020\u0005H\u00c6\u0003J\u001d\u0010\u000e\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u00c6\u0001J\u0013\u0010\u000f\u001a\u00020\u00052\b\u0010\u0010\u001a\u0004\u0018\u00010\u0011H\u00d6\u0003J\t\u0010\u0012\u001a\u00020\u0003H\u00d6\u0001J\t\u0010\u0013\u001a\u00020\u0014H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\b\u0010\tR\u0011\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\b\n\u0000\u001a\u0004\b\n\u0010\u000b\u00a8\u0006\u0015"}, d2 = {"Lcom/example/yakultscanner/viewmodels/UploadEvent$Success;", "Lcom/example/yakultscanner/viewmodels/UploadEvent;", "count", "", "savedLocally", "", "<init>", "(IZ)V", "getCount", "()I", "getSavedLocally", "()Z", "component1", "component2", "copy", "equals", "other", "", "hashCode", "toString", "", "app_devDebug"})
    public static final class Success extends com.example.yakultscanner.viewmodels.UploadEvent {
        private final int count = 0;
        private final boolean savedLocally = false;
        
        public Success(int count, boolean savedLocally) {
        }
        
        public final int getCount() {
            return 0;
        }
        
        public final boolean getSavedLocally() {
            return false;
        }
        
        public final int component1() {
            return 0;
        }
        
        public final boolean component2() {
            return false;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.viewmodels.UploadEvent.Success copy(int count, boolean savedLocally) {
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