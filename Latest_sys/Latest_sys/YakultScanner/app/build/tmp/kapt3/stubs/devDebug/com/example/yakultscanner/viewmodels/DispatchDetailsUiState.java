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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u001a\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b6\u0018\u00002\u00020\u0001:\u0003\u0004\u0005\u0006B\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0003\u0007\b\t\u00a8\u0006\n"}, d2 = {"Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState;", "", "<init>", "()V", "Loading", "Success", "Error", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState$Error;", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState$Loading;", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState$Success;", "app_devDebug"})
public abstract class DispatchDetailsUiState {
    
    private DispatchDetailsUiState() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState$Error;", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState;", "message", "", "<init>", "(Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Error extends com.example.yakultscanner.viewmodels.DispatchDetailsUiState {
        @org.jetbrains.annotations.NotNull()
        private final java.lang.String message = null;
        
        public Error(@org.jetbrains.annotations.NotNull()
        java.lang.String message) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String getMessage() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.viewmodels.DispatchDetailsUiState.Error copy(@org.jetbrains.annotations.NotNull()
        java.lang.String message) {
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000$\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u00c6\n\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0013\u0010\u0004\u001a\u00020\u00052\b\u0010\u0006\u001a\u0004\u0018\u00010\u0007H\u00d6\u0003J\t\u0010\b\u001a\u00020\tH\u00d6\u0001J\t\u0010\n\u001a\u00020\u000bH\u00d6\u0001\u00a8\u0006\f"}, d2 = {"Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState$Loading;", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState;", "<init>", "()V", "equals", "", "other", "", "hashCode", "", "toString", "", "app_devDebug"})
    public static final class Loading extends com.example.yakultscanner.viewmodels.DispatchDetailsUiState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.viewmodels.DispatchDetailsUiState.Loading INSTANCE = null;
        
        private Loading() {
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0011H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState$Success;", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState;", "dispatchSet", "Lcom/example/yakultscanner/data/model/DispatchSet;", "<init>", "(Lcom/example/yakultscanner/data/model/DispatchSet;)V", "getDispatchSet", "()Lcom/example/yakultscanner/data/model/DispatchSet;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "", "app_devDebug"})
    public static final class Success extends com.example.yakultscanner.viewmodels.DispatchDetailsUiState {
        @org.jetbrains.annotations.NotNull()
        private final com.example.yakultscanner.data.model.DispatchSet dispatchSet = null;
        
        public Success(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.model.DispatchSet dispatchSet) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.model.DispatchSet getDispatchSet() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.data.model.DispatchSet component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.viewmodels.DispatchDetailsUiState.Success copy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.data.model.DispatchSet dispatchSet) {
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