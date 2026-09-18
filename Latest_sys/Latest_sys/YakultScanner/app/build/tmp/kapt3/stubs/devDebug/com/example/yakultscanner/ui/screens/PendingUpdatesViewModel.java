package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.text.font.FontWeight;
import androidx.lifecycle.ViewModel;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.data.db.PendingUpdateEntity;
import com.example.yakultscanner.data.db.SyncStatus;
import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.data.repository.SyncRepository;
import com.example.yakultscanner.data.repository.SyncResult;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.StateFlow;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000N\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0010\u0002\n\u0002\b\u0005\b\u0007\u0018\u00002\u00020\u0001B\u0019\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\u0004\b\u0006\u0010\u0007J\u0006\u0010\u001d\u001a\u00020\u001eJ\u0006\u0010\u001f\u001a\u00020\u001eJ\u0006\u0010 \u001a\u00020\u001eJ\u000e\u0010!\u001a\u00020\u001e2\u0006\u0010\"\u001a\u00020\u001aR\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0004\u001a\u00020\u0005X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001a\u0010\b\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u000b0\n0\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u001d\u0010\f\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u000b0\n0\r\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000e\u0010\u000fR\u0014\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00110\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u00110\r\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u000fR\u0014\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\u00110\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00110\r\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u000fR\u0016\u0010\u0015\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010\u00160\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0019\u0010\u0017\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010\u00160\r\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u000fR\u0016\u0010\u0019\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010\u001a0\tX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0019\u0010\u001b\u001a\n\u0012\u0006\u0012\u0004\u0018\u00010\u001a0\r\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u000f\u00a8\u0006#"}, d2 = {"Lcom/example/yakultscanner/ui/screens/PendingUpdatesViewModel;", "Landroidx/lifecycle/ViewModel;", "offlineRepository", "Lcom/example/yakultscanner/data/repository/OfflineRepository;", "syncRepository", "Lcom/example/yakultscanner/data/repository/SyncRepository;", "<init>", "(Lcom/example/yakultscanner/data/repository/OfflineRepository;Lcom/example/yakultscanner/data/repository/SyncRepository;)V", "_pendingUpdates", "Lkotlinx/coroutines/flow/MutableStateFlow;", "", "Lcom/example/yakultscanner/data/db/PendingUpdateEntity;", "pendingUpdates", "Lkotlinx/coroutines/flow/StateFlow;", "getPendingUpdates", "()Lkotlinx/coroutines/flow/StateFlow;", "_isLoading", "", "isLoading", "_isSyncing", "isSyncing", "_syncResult", "Lcom/example/yakultscanner/data/repository/SyncResult;", "syncResult", "getSyncResult", "_errorMessage", "", "errorMessage", "getErrorMessage", "loadPendingUpdates", "", "syncNow", "clearSyncResult", "deletePendingUpdate", "id", "app_devDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class PendingUpdatesViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.OfflineRepository offlineRepository = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.SyncRepository syncRepository = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> _pendingUpdates = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> pendingUpdates = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Boolean> _isLoading = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isLoading = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Boolean> _isSyncing = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isSyncing = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.data.repository.SyncResult> _syncResult = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.data.repository.SyncResult> syncResult = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.String> _errorMessage = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.String> errorMessage = null;
    
    @javax.inject.Inject()
    public PendingUpdatesViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.OfflineRepository offlineRepository, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.SyncRepository syncRepository) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity>> getPendingUpdates() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isLoading() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isSyncing() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.data.repository.SyncResult> getSyncResult() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.String> getErrorMessage() {
        return null;
    }
    
    public final void loadPendingUpdates() {
    }
    
    public final void syncNow() {
    }
    
    public final void clearSyncResult() {
    }
    
    public final void deletePendingUpdate(@org.jetbrains.annotations.NotNull()
    java.lang.String id) {
    }
}