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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u008a\u0001\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010$\n\u0002\u0010\u000e\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0003\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\b\u000f\n\u0002\u0018\u0002\n\u0002\b\b\b\u0007\u0018\u00002\u00020\u0001B)\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0005\u0012\u0006\u0010\u0006\u001a\u00020\u0007\u0012\u0006\u0010\b\u001a\u00020\t\u00a2\u0006\u0004\b\n\u0010\u000bJ\u000e\u0010/\u001a\u0002002\u0006\u00101\u001a\u00020\u001cJ,\u00102\u001a\u0002002\n\b\u0002\u00103\u001a\u0004\u0018\u00010\u001c2\n\b\u0002\u00104\u001a\u0004\u0018\u00010\u001c2\n\b\u0002\u00105\u001a\u0004\u0018\u000106H\u0002J\u0012\u00107\u001a\u0004\u0018\u0001062\u0006\u00108\u001a\u00020\u001cH\u0002J\u0012\u00109\u001a\u0004\u0018\u00010\u001c2\u0006\u00108\u001a\u00020\u001cH\u0002J\u0010\u0010:\u001a\u0002002\u0006\u0010;\u001a\u000206H\u0002J\u001a\u0010<\u001a\u0002002\b\u00105\u001a\u0004\u0018\u0001062\u0006\u0010=\u001a\u00020\u001cH\u0002J\u0010\u0010>\u001a\u0002002\u0006\u0010;\u001a\u000206H\u0002J\u0012\u0010?\u001a\u0002002\b\u00103\u001a\u0004\u0018\u00010\u001cH\u0002J\u0010\u0010@\u001a\u0002002\u0006\u00103\u001a\u00020\u001cH\u0002J\u000e\u0010A\u001a\u0002002\u0006\u0010B\u001a\u00020\u0017J\u000e\u0010C\u001a\u0002002\u0006\u0010;\u001a\u000206J8\u0010D\u001a\u0002002\u0006\u00103\u001a\u00020\u001c2\f\u0010E\u001a\b\u0012\u0004\u0012\u00020F0\u00162\b\u0010G\u001a\u0004\u0018\u00010\u001c2\b\u0010H\u001a\u0004\u0018\u00010\u001cH\u0082@\u00a2\u0006\u0002\u0010IJ\u0016\u0010J\u001a\u0002002\f\u0010K\u001a\b\u0012\u0004\u0012\u00020\u00170\u0016H\u0002J\u0006\u0010L\u001a\u000200J\u0006\u0010M\u001a\u000200R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0004\u001a\u00020\u0005X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0011\u0010\b\u001a\u00020\t\u00a2\u0006\b\n\u0000\u001a\u0004\b\f\u0010\rR\u0014\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\u00100\u000fX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00100\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0014R\u001a\u0010\u0015\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00170\u00160\u000fX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010\u0018\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00170\u00160\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0014R \u0010\u001a\u001a\u0014\u0012\u0010\u0012\u000e\u0012\u0004\u0012\u00020\u001c\u0012\u0004\u0012\u00020\u001d0\u001b0\u000fX\u0082\u0004\u00a2\u0006\u0002\n\u0000R#\u0010\u001e\u001a\u0014\u0012\u0010\u0012\u000e\u0012\u0004\u0012\u00020\u001c\u0012\u0004\u0012\u00020\u001d0\u001b0\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001f\u0010\u0014R\u0016\u0010 \u001a\n\u0012\u0006\u0012\u0004\u0018\u00010!0\u000fX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0019\u0010\"\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010!0\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010\u0014R\u0014\u0010$\u001a\b\u0012\u0004\u0012\u00020%0\u000fX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010&\u001a\b\u0012\u0004\u0012\u00020%0\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b&\u0010\u0014R\u0017\u0010\'\u001a\b\u0012\u0004\u0012\u00020%0\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b\'\u0010\u0014R\u0014\u0010(\u001a\b\u0012\u0004\u0012\u00020)0\u000fX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010*\u001a\b\u0012\u0004\u0012\u00020)0\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b+\u0010\u0014R\u0019\u0010,\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010-0\u0012\u00a2\u0006\b\n\u0000\u001a\u0004\b.\u0010\u0014\u00a8\u0006N"}, d2 = {"Lcom/example/yakultscanner/viewmodels/DispatchDetailsViewModel;", "Landroidx/lifecycle/ViewModel;", "apiService", "Lcom/example/yakultscanner/api/YakultApiService;", "offlineRepository", "Lcom/example/yakultscanner/data/repository/OfflineRepository;", "syncRepository", "Lcom/example/yakultscanner/data/repository/SyncRepository;", "connectivityMonitor", "Lcom/example/yakultscanner/network/ConnectivityMonitor;", "<init>", "(Lcom/example/yakultscanner/api/YakultApiService;Lcom/example/yakultscanner/data/repository/OfflineRepository;Lcom/example/yakultscanner/data/repository/SyncRepository;Lcom/example/yakultscanner/network/ConnectivityMonitor;)V", "getConnectivityMonitor", "()Lcom/example/yakultscanner/network/ConnectivityMonitor;", "_uiState", "Lkotlinx/coroutines/flow/MutableStateFlow;", "Lcom/example/yakultscanner/viewmodels/DispatchDetailsUiState;", "uiState", "Lkotlinx/coroutines/flow/StateFlow;", "getUiState", "()Lkotlinx/coroutines/flow/StateFlow;", "_itemEdits", "", "Lcom/example/yakultscanner/data/model/ItemEditState;", "itemEdits", "getItemEdits", "_updatesBySerial", "", "", "Lcom/example/yakultscanner/api/SetItemUpdateDto;", "updatesBySerial", "getUpdatesBySerial", "_uploadEvent", "Lcom/example/yakultscanner/viewmodels/UploadEvent;", "uploadEvent", "getUploadEvent", "_isUploading", "", "isUploading", "isSyncing", "_pendingCount", "", "pendingCount", "getPendingCount", "lastSyncResult", "Lcom/example/yakultscanner/data/repository/SyncResult;", "getLastSyncResult", "loadDispatchSet", "", "scannedString", "fetchDispatchSetFromApi", "setCode", "token", "fallback", "Lcom/example/yakultscanner/data/model/DispatchSet;", "parseDispatchSet", "value", "extractSetToken", "showDispatchSet", "dispatchSet", "showFallbackOrError", "message", "initializeEdits", "fetchUpdates", "loadPendingUpdatesForSet", "updateItemEdit", "updated", "uploadChanges", "savePendingAndNotify", "entries", "Lcom/example/yakultscanner/api/SetItemUpdateEntry;", "userId", "userName", "(Ljava/lang/String;Ljava/util/List;Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "syncLocalStateAfterUpload", "itemsToUpload", "syncNow", "clearUploadEvent", "app_prodDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class DispatchDetailsViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.api.YakultApiService apiService = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.OfflineRepository offlineRepository = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.SyncRepository syncRepository = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.network.ConnectivityMonitor connectivityMonitor = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.DispatchDetailsUiState> _uiState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.DispatchDetailsUiState> uiState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.data.model.ItemEditState>> _itemEdits = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.data.model.ItemEditState>> itemEdits = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.Map<java.lang.String, com.example.yakultscanner.api.SetItemUpdateDto>> _updatesBySerial = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.Map<java.lang.String, com.example.yakultscanner.api.SetItemUpdateDto>> updatesBySerial = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.UploadEvent> _uploadEvent = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.UploadEvent> uploadEvent = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Boolean> _isUploading = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isUploading = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isSyncing = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Integer> _pendingCount = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Integer> pendingCount = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.data.repository.SyncResult> lastSyncResult = null;
    
    @javax.inject.Inject()
    public DispatchDetailsViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.YakultApiService apiService, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.OfflineRepository offlineRepository, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.SyncRepository syncRepository, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.network.ConnectivityMonitor connectivityMonitor) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.network.ConnectivityMonitor getConnectivityMonitor() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.DispatchDetailsUiState> getUiState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.data.model.ItemEditState>> getItemEdits() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.Map<java.lang.String, com.example.yakultscanner.api.SetItemUpdateDto>> getUpdatesBySerial() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.UploadEvent> getUploadEvent() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isUploading() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isSyncing() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Integer> getPendingCount() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.data.repository.SyncResult> getLastSyncResult() {
        return null;
    }
    
    public final void loadDispatchSet(@org.jetbrains.annotations.NotNull()
    java.lang.String scannedString) {
    }
    
    private final void fetchDispatchSetFromApi(java.lang.String setCode, java.lang.String token, com.example.yakultscanner.data.model.DispatchSet fallback) {
    }
    
    private final com.example.yakultscanner.data.model.DispatchSet parseDispatchSet(java.lang.String value) {
        return null;
    }
    
    private final java.lang.String extractSetToken(java.lang.String value) {
        return null;
    }
    
    private final void showDispatchSet(com.example.yakultscanner.data.model.DispatchSet dispatchSet) {
    }
    
    private final void showFallbackOrError(com.example.yakultscanner.data.model.DispatchSet fallback, java.lang.String message) {
    }
    
    private final void initializeEdits(com.example.yakultscanner.data.model.DispatchSet dispatchSet) {
    }
    
    private final void fetchUpdates(java.lang.String setCode) {
    }
    
    private final void loadPendingUpdatesForSet(java.lang.String setCode) {
    }
    
    public final void updateItemEdit(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.ItemEditState updated) {
    }
    
    public final void uploadChanges(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.DispatchSet dispatchSet) {
    }
    
    private final java.lang.Object savePendingAndNotify(java.lang.String setCode, java.util.List<com.example.yakultscanner.api.SetItemUpdateEntry> entries, java.lang.String userId, java.lang.String userName, kotlin.coroutines.Continuation<? super kotlin.Unit> $completion) {
        return null;
    }
    
    private final void syncLocalStateAfterUpload(java.util.List<com.example.yakultscanner.data.model.ItemEditState> itemsToUpload) {
    }
    
    public final void syncNow() {
    }
    
    public final void clearUploadEvent() {
    }
}