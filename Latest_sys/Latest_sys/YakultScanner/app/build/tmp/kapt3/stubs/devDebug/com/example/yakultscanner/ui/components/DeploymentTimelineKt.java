package com.example.yakultscanner.ui.components;

import androidx.compose.foundation.layout.*;
import androidx.compose.material.icons.Icons;
import androidx.compose.material.icons.filled.*;
import androidx.compose.material3.*;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import com.example.yakultscanner.api.DeploymentHistoryDto;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000*\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0002\u001a \u0010\u0000\u001a\u00020\u00012\f\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u0006H\u0007\u001a\u0018\u0010\u0007\u001a\u00020\u00012\u0006\u0010\b\u001a\u00020\u00042\u0006\u0010\t\u001a\u00020\nH\u0003\u001a\b\u0010\u000b\u001a\u00020\u0001H\u0003\u001a\u0012\u0010\f\u001a\u00020\r2\b\u0010\u000e\u001a\u0004\u0018\u00010\rH\u0002\u00a8\u0006\u000f"}, d2 = {"DeploymentTimeline", "", "history", "", "Lcom/example/yakultscanner/api/DeploymentHistoryDto;", "modifier", "Landroidx/compose/ui/Modifier;", "TimelineItem", "event", "isLast", "", "EmptyTimelineState", "formatTimestamp", "", "timestamp", "app_devDebug"})
public final class DeploymentTimelineKt {
    
    @androidx.compose.runtime.Composable()
    public static final void DeploymentTimeline(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.DeploymentHistoryDto> history, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void TimelineItem(com.example.yakultscanner.api.DeploymentHistoryDto event, boolean isLast) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void EmptyTimelineState() {
    }
    
    private static final java.lang.String formatTimestamp(java.lang.String timestamp) {
        return null;
    }
}