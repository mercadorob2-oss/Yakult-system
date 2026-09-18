package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.data.db.PendingUpdateEntity;
import com.example.yakultscanner.data.db.SyncStatus;
import com.example.yakultscanner.data.repository.SyncResult;
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0012\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0000\n\u0002\u0010\u000e\n\u0002\b\t\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\u0011\b\u0002\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007j\u0002\b\bj\u0002\b\tj\u0002\b\nj\u0002\b\u000b\u00a8\u0006\f"}, d2 = {"Lcom/example/yakultscanner/ui/screens/SyncCenterFilter;", "", "label", "", "<init>", "(Ljava/lang/String;ILjava/lang/String;)V", "getLabel", "()Ljava/lang/String;", "All", "NeedsAttention", "Failed", "Pending", "app_devDebug"})
enum SyncCenterFilter {
    /*public static final*/ All /* = new All(null) */,
    /*public static final*/ NeedsAttention /* = new NeedsAttention(null) */,
    /*public static final*/ Failed /* = new Failed(null) */,
    /*public static final*/ Pending /* = new Pending(null) */;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String label = null;
    
    SyncCenterFilter(java.lang.String label) {
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.ui.screens.SyncCenterFilter> getEntries() {
        return null;
    }
}