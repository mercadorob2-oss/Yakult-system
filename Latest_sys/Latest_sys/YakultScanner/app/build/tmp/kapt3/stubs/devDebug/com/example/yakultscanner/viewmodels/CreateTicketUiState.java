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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u001e\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b6\u0018\u00002\u00020\u0001:\u0004\u0004\u0005\u0006\u0007B\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0004\b\t\n\u000b\u00a8\u0006\f"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CreateTicketUiState;", "", "<init>", "()V", "Idle", "Loading", "Success", "Error", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Error;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Idle;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Loading;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Success;", "app_devDebug"})
public abstract class CreateTicketUiState {
    
    private CreateTicketUiState() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Error;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState;", "message", "", "<init>", "(Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Error extends com.example.yakultscanner.viewmodels.CreateTicketUiState {
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
        public final com.example.yakultscanner.viewmodels.CreateTicketUiState.Error copy(@org.jetbrains.annotations.NotNull()
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Idle;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState;", "<init>", "()V", "app_devDebug"})
    public static final class Idle extends com.example.yakultscanner.viewmodels.CreateTicketUiState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.viewmodels.CreateTicketUiState.Idle INSTANCE = null;
        
        private Idle() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Loading;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState;", "<init>", "()V", "app_devDebug"})
    public static final class Loading extends com.example.yakultscanner.viewmodels.CreateTicketUiState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.viewmodels.CreateTicketUiState.Loading INSTANCE = null;
        
        private Loading() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\n\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u001d\u0012\b\u0010\u0002\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\u0004\b\u0006\u0010\u0007J\u000b\u0010\f\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\r\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J!\u0010\u000e\u001a\u00020\u00002\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005H\u00c6\u0001J\u0013\u0010\u000f\u001a\u00020\u00102\b\u0010\u0011\u001a\u0004\u0018\u00010\u0012H\u00d6\u0003J\t\u0010\u0013\u001a\u00020\u0014H\u00d6\u0001J\t\u0010\u0015\u001a\u00020\u0005H\u00d6\u0001R\u0013\u0010\u0002\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\b\u0010\tR\u0013\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\b\n\u0000\u001a\u0004\b\n\u0010\u000b\u00a8\u0006\u0016"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CreateTicketUiState$Success;", "Lcom/example/yakultscanner/viewmodels/CreateTicketUiState;", "ticket", "Lcom/example/yakultscanner/api/CallTicketListItem;", "escalationWarning", "", "<init>", "(Lcom/example/yakultscanner/api/CallTicketListItem;Ljava/lang/String;)V", "getTicket", "()Lcom/example/yakultscanner/api/CallTicketListItem;", "getEscalationWarning", "()Ljava/lang/String;", "component1", "component2", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Success extends com.example.yakultscanner.viewmodels.CreateTicketUiState {
        @org.jetbrains.annotations.Nullable()
        private final com.example.yakultscanner.api.CallTicketListItem ticket = null;
        @org.jetbrains.annotations.Nullable()
        private final java.lang.String escalationWarning = null;
        
        public Success(@org.jetbrains.annotations.Nullable()
        com.example.yakultscanner.api.CallTicketListItem ticket, @org.jetbrains.annotations.Nullable()
        java.lang.String escalationWarning) {
        }
        
        @org.jetbrains.annotations.Nullable()
        public final com.example.yakultscanner.api.CallTicketListItem getTicket() {
            return null;
        }
        
        @org.jetbrains.annotations.Nullable()
        public final java.lang.String getEscalationWarning() {
            return null;
        }
        
        @org.jetbrains.annotations.Nullable()
        public final com.example.yakultscanner.api.CallTicketListItem component1() {
            return null;
        }
        
        @org.jetbrains.annotations.Nullable()
        public final java.lang.String component2() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.viewmodels.CreateTicketUiState.Success copy(@org.jetbrains.annotations.Nullable()
        com.example.yakultscanner.api.CallTicketListItem ticket, @org.jetbrains.annotations.Nullable()
        java.lang.String escalationWarning) {
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