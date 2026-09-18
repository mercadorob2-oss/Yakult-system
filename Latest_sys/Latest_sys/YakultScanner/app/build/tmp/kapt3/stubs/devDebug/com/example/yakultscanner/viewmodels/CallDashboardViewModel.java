package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.api.CallDashboardResponse;
import com.example.yakultscanner.data.repository.CallMonitoringRepository;
import dagger.hilt.android.lifecycle.HiltViewModel;
import kotlinx.coroutines.flow.StateFlow;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00004\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\u0002\n\u0002\b\u0002\b\u0007\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\u0010\u0010\u0010\u001a\u00020\u00112\b\b\u0002\u0010\u0012\u001a\u00020\u000eR\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0014\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\t\u001a\b\u0012\u0004\u0012\u00020\b0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000b\u0010\fR\u0014\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000e0\u0007X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u0017\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u000e0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000f\u0010\f\u00a8\u0006\u0013"}, d2 = {"Lcom/example/yakultscanner/viewmodels/CallDashboardViewModel;", "Landroidx/lifecycle/ViewModel;", "repository", "Lcom/example/yakultscanner/data/repository/CallMonitoringRepository;", "<init>", "(Lcom/example/yakultscanner/data/repository/CallMonitoringRepository;)V", "_uiState", "Lkotlinx/coroutines/flow/MutableStateFlow;", "Lcom/example/yakultscanner/viewmodels/CallDashboardUiState;", "uiState", "Lkotlinx/coroutines/flow/StateFlow;", "getUiState", "()Lkotlinx/coroutines/flow/StateFlow;", "_isRefreshing", "", "isRefreshing", "loadDashboard", "", "refresh", "app_devDebug"})
@dagger.hilt.android.lifecycle.HiltViewModel()
public final class CallDashboardViewModel extends androidx.lifecycle.ViewModel {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.data.repository.CallMonitoringRepository repository = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<com.example.yakultscanner.viewmodels.CallDashboardUiState> _uiState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.CallDashboardUiState> uiState = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.MutableStateFlow<java.lang.Boolean> _isRefreshing = null;
    @org.jetbrains.annotations.NotNull()
    private final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isRefreshing = null;
    
    @javax.inject.Inject()
    public CallDashboardViewModel(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.repository.CallMonitoringRepository repository) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<com.example.yakultscanner.viewmodels.CallDashboardUiState> getUiState() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final kotlinx.coroutines.flow.StateFlow<java.lang.Boolean> isRefreshing() {
        return null;
    }
    
    public final void loadDashboard(boolean refresh) {
    }
}