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
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.CallTicketDetailResponse;
import com.example.yakultscanner.api.CallTicketHistoryDto;
import com.example.yakultscanner.api.CallTicketNoteDto;
import com.example.yakultscanner.viewmodels.ActionUiState;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.TicketDetailUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000@\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0005\u001a\"\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u0007H\u0007\u001a\u001a\u0010\b\u001a\u00020\u00012\u0006\u0010\t\u001a\u00020\n2\b\u0010\u000b\u001a\u0004\u0018\u00010\nH\u0003\u001a\u0016\u0010\f\u001a\u00020\u00012\f\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000f0\u000eH\u0003\u001a\u0018\u0010\u0010\u001a\u00020\u00012\u0006\u0010\u0011\u001a\u00020\u000f2\u0006\u0010\u0012\u001a\u00020\u0013H\u0003\u001a\u0016\u0010\u0014\u001a\u00020\u00012\f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00160\u000eH\u0003\u001a\u0018\u0010\u0017\u001a\u00020\u00012\u0006\u0010\u0018\u001a\u00020\u00162\u0006\u0010\u0012\u001a\u00020\u0013H\u0003\u001a\u0018\u0010\u0019\u001a\b\u0012\u0004\u0012\u00020\n0\u000e2\b\u0010\u001a\u001a\u0004\u0018\u00010\nH\u0002\u00a8\u0006\u001b"}, d2 = {"TicketDetailScreen", "", "navController", "Landroidx/navigation/NavController;", "ticketId", "", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "InfoRow", "label", "", "value", "NotesTimelineSection", "notes", "", "Lcom/example/yakultscanner/api/CallTicketNoteDto;", "NoteTimelineItem", "note", "isLast", "", "HistoryTimelineSection", "history", "Lcom/example/yakultscanner/api/CallTicketHistoryDto;", "HistoryTimelineItem", "entry", "allowedTicketStatuses", "currentStatus", "app_prodDebug"})
public final class TicketDetailScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void TicketDetailScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, int ticketId, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void InfoRow(java.lang.String label, java.lang.String value) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void NotesTimelineSection(java.util.List<com.example.yakultscanner.api.CallTicketNoteDto> notes) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void NoteTimelineItem(com.example.yakultscanner.api.CallTicketNoteDto note, boolean isLast) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HistoryTimelineSection(java.util.List<com.example.yakultscanner.api.CallTicketHistoryDto> history) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HistoryTimelineItem(com.example.yakultscanner.api.CallTicketHistoryDto entry, boolean isLast) {
    }
    
    private static final java.util.List<java.lang.String> allowedTicketStatuses(java.lang.String currentStatus) {
        return null;
    }
}