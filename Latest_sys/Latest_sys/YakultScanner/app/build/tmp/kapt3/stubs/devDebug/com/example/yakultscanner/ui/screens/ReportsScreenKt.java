package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.graphics.vector.ImageVector;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.unit.Dp;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.ReportKpiDto;
import com.example.yakultscanner.api.ReportListRowDto;
import com.example.yakultscanner.api.ReportModuleDetailResponse;
import com.example.yakultscanner.api.ReportTrendPointDto;
import com.example.yakultscanner.api.ReportsSummaryResponse;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\u0088\u0001\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0006\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\b\t\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\n\u001a\u0018\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u0003H\u0003\u001a\u0010\u0010\u0005\u001a\u00020\u00012\u0006\u0010\u0006\u001a\u00020\u0007H\u0007\u001a \u0010\b\u001a\u00020\u00012\u0006\u0010\u0006\u001a\u00020\u00072\u0006\u0010\t\u001a\u00020\u00032\u0006\u0010\n\u001a\u00020\u0003H\u0007\u001a\u0010\u0010\u000b\u001a\u00020\u00032\u0006\u0010\f\u001a\u00020\u0003H\u0002\u001a!\u0010\r\u001a\u00020\u000e2\b\u0010\u000f\u001a\u0004\u0018\u00010\u00032\u0006\u0010\u0010\u001a\u00020\u000eH\u0002\u00a2\u0006\u0004\b\u0011\u0010\u0012\u001a\u0014\u0010\u0013\u001a\u00020\u0014*\u00020\u00152\u0006\u0010\f\u001a\u00020\u0003H\u0002\u001a\u0014\u0010\u0016\u001a\u00020\u0014*\u00020\u00152\u0006\u0010\f\u001a\u00020\u0003H\u0002\u001a\u001e\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00190\u00182\u0006\u0010\u001a\u001a\u00020\u001b2\u0006\u0010\f\u001a\u00020\u0003H\u0002\u001a\u0014\u0010\u001c\u001a\u00020\u001d*\u00020\u001d2\u0006\u0010\u001e\u001a\u00020\u0014H\u0002\u001a\f\u0010\u001f\u001a\u00020 *\u00020!H\u0002\u001a\u0012\u0010\"\u001a\u00020 2\b\u0010\t\u001a\u0004\u0018\u00010\u0003H\u0002\u001a\f\u0010#\u001a\u00020$*\u00020%H\u0002\u001a\f\u0010&\u001a\u00020\u0019*\u00020\'H\u0002\u001a\u001b\u0010(\u001a\u00020)*\u00020*2\u0006\u0010\u0010\u001a\u00020\u000eH\u0002\u00a2\u0006\u0004\b+\u0010,\u001a\'\u0010-\u001a\u00020.2\u0006\u0010/\u001a\u00020.2\u0006\u00100\u001a\u00020\u001b2\u0006\u00101\u001a\u00020.H\u0002\u00a2\u0006\u0004\b2\u00103\u001a\u001a\u00104\u001a\u00020\u00012\b\b\u0002\u00105\u001a\u0002062\u0006\u00107\u001a\u00020$H\u0003\u001a\u0016\u00108\u001a\u00020\u00012\f\u00109\u001a\b\u0012\u0004\u0012\u00020\u00190\u0018H\u0003\u001a.\u0010:\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010;\u001a\u00020\u00032\u0006\u0010<\u001a\u00020\u00032\f\u0010=\u001a\b\u0012\u0004\u0012\u00020\u00030\u0018H\u0003\u001a+\u0010>\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010;\u001a\u00020\u00032\u0011\u0010?\u001a\r\u0012\u0004\u0012\u00020\u00010@\u00a2\u0006\u0002\bAH\u0003\u001a\u0010\u0010B\u001a\u00020\u00012\u0006\u0010C\u001a\u00020)H\u0003\u001a\u0010\u0010D\u001a\u00020\u00012\u0006\u0010C\u001a\u00020)H\u0003\u001a\u0010\u0010E\u001a\u00020\u00012\u0006\u0010C\u001a\u00020)H\u0003\u001a\u001e\u0010F\u001a\u00020\u00012\u0006\u0010G\u001a\u00020\u001d2\f\u0010H\u001a\b\u0012\u0004\u0012\u00020\u00010@H\u0003\u001a\u001e\u0010I\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\f\u0010J\u001a\b\u0012\u0004\u0012\u00020\u00030\u0018H\u0003\u00a8\u0006K"}, d2 = {"ReportSupportDetails", "", "title", "", "details", "ReportsScreen", "navController", "Landroidx/navigation/NavController;", "ReportModuleDetailScreen", "moduleId", "range", "toApiRange", "selectedRange", "parseTone", "Landroidx/compose/ui/graphics/Color;", "raw", "fallback", "parseTone-4WTKRHQ", "(Ljava/lang/String;J)J", "toLocalBundle", "Lcom/example/yakultscanner/ui/screens/ReportsBundle;", "Lcom/example/yakultscanner/api/ReportsSummaryResponse;", "toFallbackBundle", "legacyTrendPoints", "", "Lcom/example/yakultscanner/ui/screens/ReportTrendPoint;", "totalSets", "", "withStats", "Lcom/example/yakultscanner/ui/screens/ReportModulePreview;", "bundle", "toLocalDetail", "Lcom/example/yakultscanner/ui/screens/ReportModuleDetailBundle;", "Lcom/example/yakultscanner/api/ReportModuleDetailResponse;", "fallbackModuleDetail", "toLocalKpi", "Lcom/example/yakultscanner/ui/screens/ReportKpi;", "Lcom/example/yakultscanner/api/ReportKpiDto;", "toLocalTrend", "Lcom/example/yakultscanner/api/ReportTrendPointDto;", "toLocalRow", "Lcom/example/yakultscanner/ui/screens/ReportListRow;", "Lcom/example/yakultscanner/api/ReportListRowDto;", "toLocalRow-4WTKRHQ", "(Lcom/example/yakultscanner/api/ReportListRowDto;J)Lcom/example/yakultscanner/ui/screens/ReportListRow;", "reportCardWidth", "Landroidx/compose/ui/unit/Dp;", "totalWidth", "itemCount", "gap", "reportCardWidth-De6cCo0", "(FIF)F", "ReportsKpiCard", "modifier", "Landroidx/compose/ui/Modifier;", "item", "DispatchTrendCard", "points", "ReportsMiniModuleCard", "subtitle", "summary", "highlights", "DetailSectionCard", "content", "Lkotlin/Function0;", "Landroidx/compose/runtime/Composable;", "DetailMetricRow", "row", "ActionMetricRow", "TimelineMetricRow", "ReportModuleCard", "module", "onClick", "RankedInsightCard", "items", "app_devDebug"})
public final class ReportsScreenKt {
    
    @androidx.compose.runtime.Composable()
    private static final void ReportSupportDetails(java.lang.String title, java.lang.String details) {
    }
    
    @androidx.compose.material3.ExperimentalMaterial3Api()
    @androidx.compose.runtime.Composable()
    public static final void ReportsScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.material3.ExperimentalMaterial3Api()
    @androidx.compose.runtime.Composable()
    public static final void ReportModuleDetailScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    java.lang.String moduleId, @org.jetbrains.annotations.NotNull()
    java.lang.String range) {
    }
    
    private static final java.lang.String toApiRange(java.lang.String selectedRange) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportsBundle toLocalBundle(com.example.yakultscanner.api.ReportsSummaryResponse $this$toLocalBundle, java.lang.String selectedRange) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportsBundle toFallbackBundle(com.example.yakultscanner.api.ReportsSummaryResponse $this$toFallbackBundle, java.lang.String selectedRange) {
        return null;
    }
    
    private static final java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> legacyTrendPoints(int totalSets, java.lang.String selectedRange) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportModulePreview withStats(com.example.yakultscanner.ui.screens.ReportModulePreview $this$withStats, com.example.yakultscanner.ui.screens.ReportsBundle bundle) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportModuleDetailBundle toLocalDetail(com.example.yakultscanner.api.ReportModuleDetailResponse $this$toLocalDetail) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportModuleDetailBundle fallbackModuleDetail(java.lang.String moduleId) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportKpi toLocalKpi(com.example.yakultscanner.api.ReportKpiDto $this$toLocalKpi) {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.ReportTrendPoint toLocalTrend(com.example.yakultscanner.api.ReportTrendPointDto $this$toLocalTrend) {
        return null;
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ReportsKpiCard(androidx.compose.ui.Modifier modifier, com.example.yakultscanner.ui.screens.ReportKpi item) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DispatchTrendCard(java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> points) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ReportsMiniModuleCard(java.lang.String title, java.lang.String subtitle, java.lang.String summary, java.util.List<java.lang.String> highlights) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DetailSectionCard(java.lang.String title, java.lang.String subtitle, androidx.compose.runtime.internal.ComposableFunction0<kotlin.Unit> content) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DetailMetricRow(com.example.yakultscanner.ui.screens.ReportListRow row) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ActionMetricRow(com.example.yakultscanner.ui.screens.ReportListRow row) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void TimelineMetricRow(com.example.yakultscanner.ui.screens.ReportListRow row) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ReportModuleCard(com.example.yakultscanner.ui.screens.ReportModulePreview module, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void RankedInsightCard(java.lang.String title, java.util.List<java.lang.String> items) {
    }
}