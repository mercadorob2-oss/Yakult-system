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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\b\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005j\u0002\b\u0006j\u0002\b\u0007j\u0002\b\b\u00a8\u0006\t"}, d2 = {"Lcom/example/yakultscanner/ui/screens/ItcmDetailSheet;", "", "<init>", "(Ljava/lang/String;I)V", "More", "AddNote", "UpdateStatus", "UpdatePriority", "Reopen", "app_devDebug"})
enum ItcmDetailSheet {
    /*public static final*/ More /* = new More() */,
    /*public static final*/ AddNote /* = new AddNote() */,
    /*public static final*/ UpdateStatus /* = new UpdateStatus() */,
    /*public static final*/ UpdatePriority /* = new UpdatePriority() */,
    /*public static final*/ Reopen /* = new Reopen() */;
    
    ItcmDetailSheet() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.ui.screens.ItcmDetailSheet> getEntries() {
        return null;
    }
}