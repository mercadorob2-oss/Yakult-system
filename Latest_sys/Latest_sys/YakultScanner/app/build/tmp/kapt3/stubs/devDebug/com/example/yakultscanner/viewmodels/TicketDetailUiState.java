package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.api.CallLookupItem;
import com.example.yakultscanner.api.CallTicketActionResponse;
import com.example.yakultscanner.api.CallTicketDetailResponse;
import com.example.yakultscanner.api.CallTicketListItem;
import com.example.yakultscanner.api.CallTicketListResponse;
import com.example.yakultscanner.api.CreateTicketRequest;
import com.example.yakultscanner.api.EscalationSettingsResponse;
import com.example.yakultscanner.api.SetEscalationOverrideRequest;
import com.example.yakultscanner.api.CallItemLookupDto;
import com.example.yakultscanner.api.CallItemLookupResponse;
import com.example.yakultscanner.api.CallConditionDto;
import com.example.yakultscanner.api.CallConditionResponse;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.ResolutionRequest;
import com.example.yakultscanner.data.repository.CallMonitoringRepository;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.StateFlow;
import org.json.JSONObject;
import retrofit2.Response;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u001a\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b6\u0018\u00002\u00020\u0001:\u0003\u0004\u0005\u0006B\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0003\u0007\b\t\u00a8\u0006\n"}, d2 = {"Lcom/example/yakultscanner/viewmodels/TicketDetailUiState;", "", "<init>", "()V", "Loading", "Success", "Error", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState$Error;", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState$Loading;", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState$Success;", "app_devDebug"})
public abstract class TicketDetailUiState {
    
    private TicketDetailUiState() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/viewmodels/TicketDetailUiState$Error;", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState;", "message", "", "<init>", "(Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Error extends com.example.yakultscanner.viewmodels.TicketDetailUiState {
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
        public final com.example.yakultscanner.viewmodels.TicketDetailUiState.Error copy(@org.jetbrains.annotations.NotNull()
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/viewmodels/TicketDetailUiState$Loading;", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState;", "<init>", "()V", "app_devDebug"})
    public static final class Loading extends com.example.yakultscanner.viewmodels.TicketDetailUiState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.viewmodels.TicketDetailUiState.Loading INSTANCE = null;
        
        private Loading() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0011H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/viewmodels/TicketDetailUiState$Success;", "Lcom/example/yakultscanner/viewmodels/TicketDetailUiState;", "response", "Lcom/example/yakultscanner/api/CallTicketDetailResponse;", "<init>", "(Lcom/example/yakultscanner/api/CallTicketDetailResponse;)V", "getResponse", "()Lcom/example/yakultscanner/api/CallTicketDetailResponse;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "", "app_devDebug"})
    public static final class Success extends com.example.yakultscanner.viewmodels.TicketDetailUiState {
        @org.jetbrains.annotations.NotNull()
        private final com.example.yakultscanner.api.CallTicketDetailResponse response = null;
        
        public Success(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.api.CallTicketDetailResponse response) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.api.CallTicketDetailResponse getResponse() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.api.CallTicketDetailResponse component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.viewmodels.TicketDetailUiState.Success copy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.api.CallTicketDetailResponse response) {
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