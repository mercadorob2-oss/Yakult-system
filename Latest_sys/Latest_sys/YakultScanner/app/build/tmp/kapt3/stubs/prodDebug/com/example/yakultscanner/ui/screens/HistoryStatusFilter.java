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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\u0007\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005j\u0002\b\u0006j\u0002\b\u0007\u00a8\u0006\b"}, d2 = {"Lcom/example/yakultscanner/ui/screens/HistoryStatusFilter;", "", "<init>", "(Ljava/lang/String;I)V", "All", "Pending", "Dispatched", "Other", "app_prodDebug"})
enum HistoryStatusFilter {
    /*public static final*/ All /* = new All() */,
    /*public static final*/ Pending /* = new Pending() */,
    /*public static final*/ Dispatched /* = new Dispatched() */,
    /*public static final*/ Other /* = new Other() */;
    
    HistoryStatusFilter() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.ui.screens.HistoryStatusFilter> getEntries() {
        return null;
    }
}