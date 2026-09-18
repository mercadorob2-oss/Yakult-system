package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.AssistChipDefaults;
import androidx.compose.material3.CardDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.lifecycle.Lifecycle;
import androidx.lifecycle.LifecycleEventObserver;
import androidx.navigation.NavController;
import com.example.yakultscanner.PinnedSetsStore;
import com.example.yakultscanner.ScanHistoryEntry;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u00008\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\u001a\u0010\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\u0014\u0010\u0004\u001a\u0004\u0018\u00010\u00052\b\u0010\u0006\u001a\u0004\u0018\u00010\u0005H\u0002\u001a\u0012\u0010\u0007\u001a\u00020\b2\b\u0010\u0006\u001a\u0004\u0018\u00010\u0005H\u0002\u001a<\u0010\t\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\r2\u0006\u0010\u000e\u001a\u00020\u000f2\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00010\u00112\f\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u00010\u0011H\u0003\u00a8\u0006\u0013"}, d2 = {"HistoryScreen", "", "navController", "Landroidx/navigation/NavController;", "normalizeSetCode", "", "value", "toStatusFilter", "Lcom/example/yakultscanner/ui/screens/HistoryStatusFilter;", "HistoryEntryCard", "entry", "Lcom/example/yakultscanner/ScanHistoryEntry;", "pinned", "", "pendingIssueCount", "", "onTogglePin", "Lkotlin/Function0;", "onOpen", "app_prodDebug"})
public final class HistoryScreenKt {
    
    @androidx.compose.material3.ExperimentalMaterial3Api()
    @androidx.compose.runtime.Composable()
    public static final void HistoryScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    private static final java.lang.String normalizeSetCode(java.lang.String value) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.HistoryStatusFilter toStatusFilter(java.lang.String value) {
        return null;
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HistoryEntryCard(com.example.yakultscanner.ScanHistoryEntry entry, boolean pinned, int pendingIssueCount, kotlin.jvm.functions.Function0<kotlin.Unit> onTogglePin, kotlin.jvm.functions.Function0<kotlin.Unit> onOpen) {
    }
}