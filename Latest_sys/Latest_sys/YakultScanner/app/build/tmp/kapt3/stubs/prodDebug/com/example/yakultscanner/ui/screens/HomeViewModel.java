package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.ButtonDefaults;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.lifecycle.ViewModel;
import androidx.navigation.NavController;
import com.example.yakultscanner.PinnedSetsStore;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.ScanHistoryEntry;
import com.example.yakultscanner.settings.ApiSettings;
import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.network.ConnectivityMonitor;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.StateFlow;
import javax.inject.Inject;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u0007\u0018\u00002\u00020\u0001B\u0019\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\u0004\b\u0006\u0010\u0007R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0011\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\b\n\u0000\u001a\u0004\b\b\u0010\tR\u0014\u0010\n\u001a\b\u0012\u0004\u0012\u00020\f0\u000bX\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\r\u001a\b\u0012\u0004\u0012\u00020\f0\u000e\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000f\u0010\u0010\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/ui/screens/HomeViewModel;", "Landroidx/lifecycle/ViewModel;", "offlineRepository", "Lcom/example/yakultscanner/data/repository/OfflineRepository;", "connectivityMonitor", "Lcom/example/yakultscanner/network/ConnectivityMonitor;", "<init>", "(Lcom/example/yakultscanner/data/repository/OfflineRepository;Lcom/example/yakultscanner/network/ConnectivityMonitor;)V", "getConnectivityMonitor", "()Lcom/example/yakultscanner/network/ConnectivityMonitor;", "_pendingSyncCount", "Lkotlinx/coroutines/flow/MutableStateFlow;", "", "pendingSyncCount", "Lkotlinx/coroutines/flow/StateFlow;", "getPendingSyncCount", "()Lkotlinx/coroutines/flow/StateFlow;", "app_prodDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class HomeViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.OfflineRepository offlineRepository = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.network.ConnectivityMonitor connectivityMonitor = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Integer> _pendingSyncCount = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Integer> pendingSyncCount = null;
    
    @javax.inject.Inject()
    public HomeViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.OfflineRepository offlineRepository, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.network.ConnectivityMonitor connectivityMonitor) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.network.ConnectivityMonitor getConnectivityMonitor() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Integer> getPendingSyncCount() {
        return null;
    }
}