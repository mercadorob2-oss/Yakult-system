package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.text.KeyboardOptions;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.input.ImeAction;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.CallTicketListItem;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.CallTicketsUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000H\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\b\u0003\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a4\u0010\u0006\u001a\u00020\u00012\f\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\t0\b2\b\u0010\n\u001a\u0004\u0018\u00010\t2\u0012\u0010\u000b\u001a\u000e\u0012\u0004\u0012\u00020\t\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001a\u001e\u0010\r\u001a\u00020\u00012\u0006\u0010\u000e\u001a\u00020\u000f2\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00010\u0011H\u0003\u001a<\u0010\u0012\u001a\u00020\u00012\u0006\u0010\u0013\u001a\u00020\u00142\u0006\u0010\u0015\u001a\u00020\u00142\u0006\u0010\u0016\u001a\u00020\u00142\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00010\u00112\f\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00010\u0011H\u0003\u001a\u001f\u0010\u0019\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\t2\u0006\u0010\u001b\u001a\u00020\u001cH\u0001\u00a2\u0006\u0004\b\u001d\u0010\u001e\u00a8\u0006\u001f"}, d2 = {"CallMonitoringScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "FilterChipRow", "items", "", "", "selected", "onSelect", "Lkotlin/Function1;", "TicketListCard", "ticket", "Lcom/example/yakultscanner/api/CallTicketListItem;", "onClick", "Lkotlin/Function0;", "TicketPaginationCard", "currentPage", "", "totalCount", "pageSize", "onPrevious", "onNext", "StatusBadge", "text", "color", "Landroidx/compose/ui/graphics/Color;", "StatusBadge-4WTKRHQ", "(Ljava/lang/String;J)V", "app_prodDebug"})
public final class CallMonitoringScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void CallMonitoringScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void FilterChipRow(java.util.List<java.lang.String> items, java.lang.String selected, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onSelect) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void TicketListCard(com.example.yakultscanner.api.CallTicketListItem ticket, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void TicketPaginationCard(int currentPage, int totalCount, int pageSize, kotlin.jvm.functions.Function0<kotlin.Unit> onPrevious, kotlin.jvm.functions.Function0<kotlin.Unit> onNext) {
    }
}