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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000:\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u001f\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0002\b\u0082\b\u0018\u00002\u00020\u0001B\u008d\u0001\u0012\f\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003\u0012\u0006\u0010\u0005\u001a\u00020\u0006\u0012\f\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\u00060\u0003\u0012\f\u0010\b\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u0012\f\u0010\n\u001a\b\u0012\u0004\u0012\u00020\u000b0\u0003\u0012\f\u0010\f\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u0012\f\u0010\r\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u0012\f\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u0012\f\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u0012\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\u0004\b\u0011\u0010\u0012J\u000f\u0010\u001f\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003H\u00c6\u0003J\t\u0010 \u001a\u00020\u0006H\u00c6\u0003J\u000f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00060\u0003H\u00c6\u0003J\u000f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0003J\u000f\u0010#\u001a\b\u0012\u0004\u0012\u00020\u000b0\u0003H\u00c6\u0003J\u000f\u0010$\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0003J\u000f\u0010%\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0003J\u000f\u0010&\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0003J\u000f\u0010\'\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0003J\u000f\u0010(\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0003J\u00a3\u0001\u0010)\u001a\u00020\u00002\u000e\b\u0002\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00062\u000e\b\u0002\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\u00060\u00032\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\t0\u00032\u000e\b\u0002\u0010\n\u001a\b\u0012\u0004\u0012\u00020\u000b0\u00032\u000e\b\u0002\u0010\f\u001a\b\u0012\u0004\u0012\u00020\t0\u00032\u000e\b\u0002\u0010\r\u001a\b\u0012\u0004\u0012\u00020\t0\u00032\u000e\b\u0002\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\t0\u00032\u000e\b\u0002\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\t0\u00032\u000e\b\u0002\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\t0\u0003H\u00c6\u0001J\u0013\u0010*\u001a\u00020+2\b\u0010,\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010-\u001a\u00020.H\u00d6\u0001J\t\u0010/\u001a\u00020\u0006H\u00d6\u0001R\u0017\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0014R\u0011\u0010\u0005\u001a\u00020\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0016R\u0017\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\u00060\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0014R\u0017\u0010\b\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0014R\u0017\u0010\n\u001a\b\u0012\u0004\u0012\u00020\u000b0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0014R\u0017\u0010\f\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0014R\u0017\u0010\r\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u0014R\u0017\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u0014R\u0017\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u0014R\u0017\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\t0\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001e\u0010\u0014\u00a8\u00060"}, d2 = {"Lcom/example/yakultscanner/ui/screens/ReportsBundle;", "", "kpis", "", "Lcom/example/yakultscanner/ui/screens/ReportKpi;", "executiveSummary", "", "highlights", "healthSignals", "Lcom/example/yakultscanner/ui/screens/ReportListRow;", "dispatchTrend", "Lcom/example/yakultscanner/ui/screens/ReportTrendPoint;", "issueRows", "borrowRows", "activityRows", "actionRows", "timelineRows", "<init>", "(Ljava/util/List;Ljava/lang/String;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;Ljava/util/List;)V", "getKpis", "()Ljava/util/List;", "getExecutiveSummary", "()Ljava/lang/String;", "getHighlights", "getHealthSignals", "getDispatchTrend", "getIssueRows", "getBorrowRows", "getActivityRows", "getActionRows", "getTimelineRows", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "copy", "equals", "", "other", "hashCode", "", "toString", "app_devDebug"})
final class ReportsBundle {
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportKpi> kpis = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String executiveSummary = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<java.lang.String> highlights = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> healthSignals = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> dispatchTrend = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> issueRows = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> borrowRows = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> activityRows = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> actionRows = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> timelineRows = null;
    
    public ReportsBundle(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportKpi> kpis, @org.jetbrains.annotations.NotNull()
    java.lang.String executiveSummary, @org.jetbrains.annotations.NotNull()
    java.util.List<java.lang.String> highlights, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> healthSignals, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> dispatchTrend, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> issueRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> borrowRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> activityRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> actionRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> timelineRows) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportKpi> getKpis() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getExecutiveSummary() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> getHighlights() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getHealthSignals() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> getDispatchTrend() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getIssueRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getBorrowRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getActivityRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getActionRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getTimelineRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportKpi> component1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> component10() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<java.lang.String> component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> component5() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.ui.screens.ReportsBundle copy(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportKpi> kpis, @org.jetbrains.annotations.NotNull()
    java.lang.String executiveSummary, @org.jetbrains.annotations.NotNull()
    java.util.List<java.lang.String> highlights, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> healthSignals, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportTrendPoint> dispatchTrend, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> issueRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> borrowRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> activityRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> actionRows, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> timelineRows) {
        return null;
    }
    
    @java.lang.Override()
    public boolean equals(@org.jetbrains.annotations.Nullable()
    java.lang.Object other) {
        return false;
    }
    
    @java.lang.Override()
    public int hashCode() {
        return 0;
    }
    
    @java.lang.Override()
    @org.jetbrains.annotations.NotNull()
    public java.lang.String toString() {
        return null;
    }
}