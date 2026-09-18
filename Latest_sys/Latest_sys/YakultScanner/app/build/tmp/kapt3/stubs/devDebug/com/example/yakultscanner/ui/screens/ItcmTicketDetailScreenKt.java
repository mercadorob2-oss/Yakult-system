package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.CallTicketDetail;
import com.example.yakultscanner.api.CallTicketHistoryDto;
import com.example.yakultscanner.api.CallTicketNoteDto;
import com.example.yakultscanner.ui.components.ItcmUi;
import com.example.yakultscanner.viewmodels.ActionUiState;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.TicketDetailUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000h\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\r\n\u0002\u0018\u0002\n\u0002\b\r\n\u0002\u0018\u0002\n\u0002\b\u0006\n\u0002\u0010 \n\u0000\u001a\"\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u0007H\u0007\u001a4\u0010\b\u001a\u00020\u00012\u0006\u0010\t\u001a\u00020\n2\f\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u00010\f2\u0006\u0010\u000e\u001a\u00020\u000fH\u0003\u001a\u0012\u0010\u0010\u001a\u00020\u00012\b\u0010\u0011\u001a\u0004\u0018\u00010\u0012H\u0003\u001a\u0012\u0010\u0013\u001a\u00020\u00012\b\u0010\u0011\u001a\u0004\u0018\u00010\u0012H\u0003\u001a\u0012\u0010\u0014\u001a\u00020\u00012\b\u0010\u0011\u001a\u0004\u0018\u00010\u0012H\u0003\u001a\u0018\u0010\u0015\u001a\u00020\u00012\u0006\u0010\u0016\u001a\u00020\n2\u0006\u0010\u0017\u001a\u00020\u0018H\u0003\u001a\u0010\u0010\u0019\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\u001bH\u0003\u001a\u0010\u0010\u001c\u001a\u00020\u00012\u0006\u0010\u001d\u001a\u00020\u001eH\u0003\u001aV\u0010\u001f\u001a\u00020\u00012\u0006\u0010\u0011\u001a\u00020\u00122\f\u0010 \u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010#\u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010$\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001a&\u0010%\u001a\u00020\u00012\u0006\u0010\u0016\u001a\u00020\n2\u0006\u0010&\u001a\u00020\n2\f\u0010\'\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001aH\u0010(\u001a\u00020\u00012\u0006\u0010)\u001a\u00020\n2\u0006\u0010*\u001a\u00020\u000f2\u0012\u0010+\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010,2\f\u0010 \u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010-\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001aP\u0010.\u001a\u00020\u00012\u0006\u0010/\u001a\u00020\n2\u0006\u00100\u001a\u00020\n2\u0006\u0010*\u001a\u00020\u000f2\u0012\u00101\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010,2\f\u0010 \u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010-\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001aH\u00102\u001a\u00020\u00012\u0006\u00103\u001a\u00020\n2\u0006\u0010*\u001a\u00020\u000f2\u0012\u00101\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010,2\f\u0010 \u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u0010-\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001aH\u00104\u001a\u00020\u00012\u0006\u00105\u001a\u00020\n2\u0006\u0010*\u001a\u00020\u000f2\u0012\u00106\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010,2\f\u0010 \u001a\b\u0012\u0004\u0012\u00020\u00010\f2\f\u00107\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001a\u0010\u00108\u001a\u00020\u00012\u0006\u00109\u001a\u00020:H\u0003\u001a&\u0010;\u001a\u00020\u00012\u0006\u00109\u001a\u00020:2\u0006\u0010<\u001a\u00020\n2\f\u0010=\u001a\b\u0012\u0004\u0012\u00020\u00010\fH\u0003\u001a\u0012\u0010>\u001a\u00020\u000f2\b\u0010?\u001a\u0004\u0018\u00010\nH\u0002\u001a\u0016\u0010@\u001a\b\u0012\u0004\u0012\u00020\n0A2\u0006\u0010/\u001a\u00020\nH\u0002\u00a8\u0006B"}, d2 = {"ItcmTicketDetailScreen", "", "navController", "Landroidx/navigation/NavController;", "ticketId", "", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "ItcmTicketActionDock", "primaryLabel", "", "onPrimary", "Lkotlin/Function0;", "onMore", "enabled", "", "ItcmTicketHero", "ticket", "Lcom/example/yakultscanner/api/CallTicketDetail;", "ItcmIssueCard", "ItcmTicketFacts", "ItcmTimelineHeader", "title", "icon", "Landroidx/compose/ui/graphics/vector/ImageVector;", "ItcmNoteTimelineItem", "note", "Lcom/example/yakultscanner/api/CallTicketNoteDto;", "ItcmHistoryTimelineItem", "entry", "Lcom/example/yakultscanner/api/CallTicketHistoryDto;", "ItcmMoreActionsSheet", "onDismiss", "onAddNote", "onUpdateStatus", "onUpdatePriority", "onExport", "ItcmActionRow", "description", "onClick", "ItcmAddNoteSheet", "noteText", "loading", "onNoteChange", "Lkotlin/Function1;", "onSave", "ItcmStatusSheet", "currentStatus", "selectedStatus", "onSelected", "ItcmPrioritySheet", "selectedPriority", "ItcmReopenSheet", "reason", "onReasonChange", "onReopen", "ItcmDetailLoading", "modifier", "Landroidx/compose/ui/Modifier;", "ItcmDetailError", "message", "onRetry", "isFinalTicketStatus", "status", "itcmAllowedStatuses", "", "app_devDebug"})
public final class ItcmTicketDetailScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void ItcmTicketDetailScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, int ticketId, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmTicketActionDock(java.lang.String primaryLabel, kotlin.jvm.functions.Function0<kotlin.Unit> onPrimary, kotlin.jvm.functions.Function0<kotlin.Unit> onMore, boolean enabled) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmTicketHero(com.example.yakultscanner.api.CallTicketDetail ticket) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmIssueCard(com.example.yakultscanner.api.CallTicketDetail ticket) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmTicketFacts(com.example.yakultscanner.api.CallTicketDetail ticket) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmTimelineHeader(java.lang.String title, androidx.compose.ui.graphics.vector.ImageVector icon) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmNoteTimelineItem(com.example.yakultscanner.api.CallTicketNoteDto note) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmHistoryTimelineItem(com.example.yakultscanner.api.CallTicketHistoryDto entry) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItcmMoreActionsSheet(com.example.yakultscanner.api.CallTicketDetail ticket, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function0<kotlin.Unit> onAddNote, kotlin.jvm.functions.Function0<kotlin.Unit> onUpdateStatus, kotlin.jvm.functions.Function0<kotlin.Unit> onUpdatePriority, kotlin.jvm.functions.Function0<kotlin.Unit> onExport) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmActionRow(java.lang.String title, java.lang.String description, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItcmAddNoteSheet(java.lang.String noteText, boolean loading, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onNoteChange, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function0<kotlin.Unit> onSave) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItcmStatusSheet(java.lang.String currentStatus, java.lang.String selectedStatus, boolean loading, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onSelected, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function0<kotlin.Unit> onSave) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItcmPrioritySheet(java.lang.String selectedPriority, boolean loading, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onSelected, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function0<kotlin.Unit> onSave) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItcmReopenSheet(java.lang.String reason, boolean loading, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onReasonChange, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function0<kotlin.Unit> onReopen) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmDetailLoading(androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmDetailError(androidx.compose.ui.Modifier modifier, java.lang.String message, kotlin.jvm.functions.Function0<kotlin.Unit> onRetry) {
    }
    
    private static final boolean isFinalTicketStatus(java.lang.String status) {
        return false;
    }
    
    private static final java.util.List<java.lang.String> itcmAllowedStatuses(java.lang.String currentStatus) {
        return null;
    }
}