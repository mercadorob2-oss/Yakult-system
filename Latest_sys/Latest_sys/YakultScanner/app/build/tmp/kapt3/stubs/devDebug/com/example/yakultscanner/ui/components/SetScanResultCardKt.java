package com.example.yakultscanner.ui.components;

import androidx.compose.foundation.layout.*;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.*;
import androidx.compose.runtime.*;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import com.example.yakultscanner.api.DetailedDispatchItemDto;
import com.example.yakultscanner.api.SetMetadataDto;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000@\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u000b\u001a\u0084\u0001\u0010\u0006\u001a\u00020\u00072\u0006\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\t2\u0006\u0010\u000b\u001a\u00020\t2\u0006\u0010\f\u001a\u00020\t2\u0006\u0010\r\u001a\u00020\t2\u0006\u0010\u000e\u001a\u00020\t2\b\u0010\u000f\u001a\u0004\u0018\u00010\u00102\f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00130\u00122\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00070\u00152\f\u0010\u0016\u001a\b\u0012\u0004\u0012\u00020\u00070\u00152\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00070\u00152\b\b\u0002\u0010\u0018\u001a\u00020\u0019H\u0007\u001a\u0018\u0010\u001a\u001a\u00020\u00072\u0006\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\tH\u0003\u001a/\u0010\u001e\u001a\u00020\u00072\u0006\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001f\u001a\u00020\t2\u0006\u0010 \u001a\u00020\t2\u0006\u0010!\u001a\u00020\u0001H\u0003\u00a2\u0006\u0004\b\"\u0010#\u001a\u0010\u0010$\u001a\u00020\u00072\u0006\u0010%\u001a\u00020\u0013H\u0003\u001a\u001a\u0010&\u001a\u00020\u00072\u0006\u0010\u001f\u001a\u00020\t2\b\u0010 \u001a\u0004\u0018\u00010\tH\u0003\"\u0010\u0010\u0000\u001a\u00020\u0001X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0002\"\u0010\u0010\u0003\u001a\u00020\u0001X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0002\"\u0010\u0010\u0004\u001a\u00020\u0001X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0002\"\u0010\u0010\u0005\u001a\u00020\u0001X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0002\u00a8\u0006\'"}, d2 = {"StatusPending", "Landroidx/compose/ui/graphics/Color;", "J", "StatusReady", "StatusDeployed", "StatusIssue", "SetScanResultCard", "", "setCode", "", "employeeName", "department", "branch", "company", "status", "metadata", "Lcom/example/yakultscanner/api/SetMetadataDto;", "items", "", "Lcom/example/yakultscanner/api/DetailedDispatchItemDto;", "onDeployClick", "Lkotlin/Function0;", "onViewHistoryClick", "onTrackClick", "modifier", "Landroidx/compose/ui/Modifier;", "InfoRow", "icon", "Landroidx/compose/ui/graphics/vector/ImageVector;", "text", "MetadataChip", "label", "value", "color", "MetadataChip-g2O1Hgs", "(Landroidx/compose/ui/graphics/vector/ImageVector;Ljava/lang/String;Ljava/lang/String;J)V", "CompactItemRow", "item", "DetailText", "app_devDebug"})
public final class SetScanResultCardKt {
    private static final long StatusPending = 0L;
    private static final long StatusReady = 0L;
    private static final long StatusDeployed = 0L;
    private static final long StatusIssue = 0L;
    
    @androidx.compose.runtime.Composable()
    public static final void SetScanResultCard(@org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    java.lang.String employeeName, @org.jetbrains.annotations.NotNull()
    java.lang.String department, @org.jetbrains.annotations.NotNull()
    java.lang.String branch, @org.jetbrains.annotations.NotNull()
    java.lang.String company, @org.jetbrains.annotations.NotNull()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.SetMetadataDto metadata, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.DetailedDispatchItemDto> items, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDeployClick, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onViewHistoryClick, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onTrackClick, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void InfoRow(androidx.compose.ui.graphics.vector.ImageVector icon, java.lang.String text) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void CompactItemRow(com.example.yakultscanner.api.DetailedDispatchItemDto item) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DetailText(java.lang.String label, java.lang.String value) {
    }
}