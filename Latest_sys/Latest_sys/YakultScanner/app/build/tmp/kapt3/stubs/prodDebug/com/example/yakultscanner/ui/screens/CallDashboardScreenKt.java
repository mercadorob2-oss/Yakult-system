package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.graphics.StrokeCap;
import androidx.compose.ui.graphics.drawscope.Stroke;
import androidx.compose.ui.graphics.vector.ImageVector;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.compose.ui.unit.Dp;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.CallDashboardResponse;
import com.example.yakultscanner.api.CallIssueTypeDto;
import com.example.yakultscanner.api.CallVolumePointDto;
import com.example.yakultscanner.viewmodels.CallDashboardUiState;
import com.example.yakultscanner.viewmodels.CallDashboardViewModel;
import java.time.Instant;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000z\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0005\n\u0002\u0010\u0000\n\u0002\b\u0005\u001a\u001a\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\u000b2\b\b\u0002\u0010\f\u001a\u00020\rH\u0007\u001a\u0010\u0010\u000e\u001a\u00020\t2\u0006\u0010\u000f\u001a\u00020\u0010H\u0003\u001a\b\u0010\u0011\u001a\u00020\tH\u0003\u001a\u001e\u0010\u0012\u001a\u00020\t2\u0006\u0010\u0013\u001a\u00020\u00142\f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\t0\u0016H\u0003\u001a\u0018\u0010\u0017\u001a\u00020\t2\u0006\u0010\u0018\u001a\u00020\u00192\u0006\u0010\n\u001a\u00020\u000bH\u0003\u001a\u001a\u0010\u001a\u001a\u00020\t2\b\b\u0002\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u001eH\u0003\u001a\u0016\u0010\u001f\u001a\u00020\t2\f\u0010 \u001a\b\u0012\u0004\u0012\u00020\"0!H\u0003\u001a\u0016\u0010#\u001a\u00020\t2\f\u0010$\u001a\b\u0012\u0004\u0012\u00020%0!H\u0003\u001a\'\u0010&\u001a\u00020\'2\u0006\u0010(\u001a\u00020\'2\u0006\u0010)\u001a\u00020*2\u0006\u0010+\u001a\u00020\'H\u0002\u00a2\u0006\u0004\b,\u0010-\u001a\u0012\u0010.\u001a\u00020\u00142\b\u0010/\u001a\u0004\u0018\u000100H\u0002\u001a\u0012\u00101\u001a\u00020\u00142\b\u00102\u001a\u0004\u0018\u00010\u0014H\u0002\u001a\u0012\u00103\u001a\u00020\u00142\b\u00104\u001a\u0004\u0018\u00010\u0014H\u0002\"\u000e\u0010\u0000\u001a\u00020\u0001X\u0082\u0004\u00a2\u0006\u0002\n\u0000\"\u0010\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0004\"\u0010\u0010\u0005\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0004\"\u0010\u0010\u0006\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0004\"\u0010\u0010\u0007\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0004\n\u0002\u0010\u0004\u00a8\u00065"}, d2 = {"HeaderBrush", "Landroidx/compose/ui/graphics/Brush;", "OpenTone", "Landroidx/compose/ui/graphics/Color;", "J", "CriticalTone", "VolumeTone", "ResolutionTone", "CallDashboardScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallDashboardViewModel;", "DashboardHeaderBlock", "uiState", "Lcom/example/yakultscanner/viewmodels/CallDashboardUiState;", "DashboardHeaderLoading", "DashboardHeaderError", "message", "", "onRetry", "Lkotlin/Function0;", "DashboardSections", "dashboard", "Lcom/example/yakultscanner/api/CallDashboardResponse;", "DashboardKpiCard", "modifier", "Landroidx/compose/ui/Modifier;", "item", "Lcom/example/yakultscanner/ui/screens/DashboardKpi;", "VolumeTrendChart", "volume", "", "Lcom/example/yakultscanner/api/CallVolumePointDto;", "IssueTypeDonut", "issueTypes", "Lcom/example/yakultscanner/api/CallIssueTypeDto;", "reportCardWidth", "Landroidx/compose/ui/unit/Dp;", "totalWidth", "itemCount", "", "gap", "reportCardWidth-De6cCo0", "(FIF)F", "formatKpiValue", "value", "", "dayLabel", "day", "updatedAgo", "generatedUtc", "app_prodDebug"})
public final class CallDashboardScreenKt {
    @org.jetbrains.annotations.NotNull()
    private static final androidx.compose.ui.graphics.Brush HeaderBrush = null;
    private static final long OpenTone = 0L;
    private static final long CriticalTone = 0L;
    private static final long VolumeTone = 0L;
    private static final long ResolutionTone = 0L;
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void CallDashboardScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallDashboardViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DashboardHeaderBlock(com.example.yakultscanner.viewmodels.CallDashboardUiState uiState) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DashboardHeaderLoading() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DashboardHeaderError(java.lang.String message, kotlin.jvm.functions.Function0<kotlin.Unit> onRetry) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DashboardSections(com.example.yakultscanner.api.CallDashboardResponse dashboard, androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DashboardKpiCard(androidx.compose.ui.Modifier modifier, com.example.yakultscanner.ui.screens.DashboardKpi item) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void VolumeTrendChart(java.util.List<com.example.yakultscanner.api.CallVolumePointDto> volume) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void IssueTypeDonut(java.util.List<com.example.yakultscanner.api.CallIssueTypeDto> issueTypes) {
    }
    
    private static final java.lang.String formatKpiValue(java.lang.Object value) {
        return null;
    }
    
    private static final java.lang.String dayLabel(java.lang.String day) {
        return null;
    }
    
    private static final java.lang.String updatedAgo(java.lang.String generatedUtc) {
        return null;
    }
}