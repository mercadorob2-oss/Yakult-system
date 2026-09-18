package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.CallTicketListItem;
import com.example.yakultscanner.ui.components.ItcmUi;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.CallTicketsUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000R\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010 \n\u0002\b\u0004\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\n\n\u0002\u0010\u000b\n\u0000\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a$\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0012\u0010\t\u001a\u000e\u0012\u0004\u0012\u00020\b\u0012\u0004\u0012\u00020\u00010\nH\u0003\u001a`\u0010\u000b\u001a\u00020\u00012\b\u0010\f\u001a\u0004\u0018\u00010\r2\b\u0010\u000e\u001a\u0004\u0018\u00010\r2\u0012\u0010\u000f\u001a\u000e\u0012\u0004\u0012\u00020\r\u0012\u0004\u0012\u00020\u00010\n2\u0012\u0010\u0010\u001a\u000e\u0012\u0004\u0012\u00020\r\u0012\u0004\u0012\u00020\u00010\n2\f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00010\u00122\f\u0010\u0013\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a<\u0010\u0014\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\r2\f\u0010\u0016\u001a\b\u0012\u0004\u0012\u00020\r0\u00172\b\u0010\u0007\u001a\u0004\u0018\u00010\r2\u0012\u0010\u0018\u001a\u000e\u0012\u0004\u0012\u00020\r\u0012\u0004\u0012\u00020\u00010\nH\u0003\u001a \u0010\u0019\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\b2\u0006\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u001cH\u0003\u001a\u001e\u0010\u001e\u001a\u00020\u00012\u0006\u0010\u001f\u001a\u00020 2\f\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a<\u0010!\u001a\u00020\u00012\u0006\u0010\u001d\u001a\u00020\u001c2\u0006\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\"\u001a\u00020\u001c2\f\u0010#\u001a\b\u0012\u0004\u0012\u00020\u00010\u00122\f\u0010$\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a\b\u0010%\u001a\u00020\u0001H\u0003\u001a\u001e\u0010&\u001a\u00020\u00012\u0006\u0010\'\u001a\u00020\r2\f\u0010(\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a\u0018\u0010)\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\b2\u0006\u0010*\u001a\u00020+H\u0003\u00a8\u0006,"}, d2 = {"ItcmQueueScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "ItcmWorkspaceTabs", "selected", "Lcom/example/yakultscanner/ui/screens/TicketWorkspace;", "onSelected", "Lkotlin/Function1;", "ItcmQueueFilterSheet", "selectedPriority", "", "selectedIssueType", "onPrioritySelected", "onIssueTypeSelected", "onClear", "Lkotlin/Function0;", "onDismiss", "FilterChoiceRow", "label", "values", "", "onClick", "ItcmQueueSummary", "workspace", "totalCount", "", "currentPage", "ItcmTicketQueueCard", "ticket", "Lcom/example/yakultscanner/api/CallTicketListItem;", "ItcmCompactPager", "pageSize", "onPrevious", "onNext", "ItcmQueueLoading", "ItcmQueueError", "message", "onRetry", "ItcmQueueEmpty", "hasSearchOrFilters", "", "app_devDebug"})
public final class ItcmQueueScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void ItcmQueueScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmWorkspaceTabs(com.example.yakultscanner.ui.screens.TicketWorkspace selected, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.ui.screens.TicketWorkspace, kotlin.Unit> onSelected) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItcmQueueFilterSheet(java.lang.String selectedPriority, java.lang.String selectedIssueType, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onPrioritySelected, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onIssueTypeSelected, kotlin.jvm.functions.Function0<kotlin.Unit> onClear, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void FilterChoiceRow(java.lang.String label, java.util.List<java.lang.String> values, java.lang.String selected, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmQueueSummary(com.example.yakultscanner.ui.screens.TicketWorkspace workspace, int totalCount, int currentPage) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmTicketQueueCard(com.example.yakultscanner.api.CallTicketListItem ticket, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmCompactPager(int currentPage, int totalCount, int pageSize, kotlin.jvm.functions.Function0<kotlin.Unit> onPrevious, kotlin.jvm.functions.Function0<kotlin.Unit> onNext) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmQueueLoading() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmQueueError(java.lang.String message, kotlin.jvm.functions.Function0<kotlin.Unit> onRetry) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmQueueEmpty(com.example.yakultscanner.ui.screens.TicketWorkspace workspace, boolean hasSearchOrFilters) {
    }
}