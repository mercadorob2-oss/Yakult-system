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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00004\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u001d\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0002\b\u0082\b\u0018\u00002\u00020\u0001BY\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u0012\u0006\u0010\u0005\u001a\u00020\u0003\u0012\u0006\u0010\u0006\u001a\u00020\u0003\u0012\u0006\u0010\u0007\u001a\u00020\b\u0012\f\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u0012\f\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u0012\f\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u00a2\u0006\u0004\b\u000e\u0010\u000fJ\t\u0010\u001c\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001d\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001e\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001f\u001a\u00020\u0003H\u00c6\u0003J\u0010\u0010 \u001a\u00020\bH\u00c6\u0003\u00a2\u0006\u0004\b!\u0010\u0016J\u000f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0003J\u000f\u0010#\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0003J\u000f\u0010$\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0003Jr\u0010%\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00032\b\b\u0002\u0010\u0006\u001a\u00020\u00032\b\b\u0002\u0010\u0007\u001a\u00020\b2\u000e\b\u0002\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u000b0\n2\u000e\b\u0002\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u000b0\n2\u000e\b\u0002\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000b0\nH\u00c6\u0001\u00a2\u0006\u0004\b&\u0010\'J\u0013\u0010(\u001a\u00020)2\b\u0010*\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010+\u001a\u00020,H\u00d6\u0001J\t\u0010-\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u0011R\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u0011R\u0011\u0010\u0005\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0011R\u0011\u0010\u0006\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0011R\u0013\u0010\u0007\u001a\u00020\b\u00a2\u0006\n\n\u0002\u0010\u0017\u001a\u0004\b\u0015\u0010\u0016R\u0017\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0019R\u0017\u0010\f\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0019R\u0017\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000b0\n\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u0019\u00a8\u0006."}, d2 = {"Lcom/example/yakultscanner/ui/screens/ReportModuleDetailBundle;", "", "title", "", "summary", "heroLabel", "heroValue", "heroTone", "Landroidx/compose/ui/graphics/Color;", "keyRows", "", "Lcom/example/yakultscanner/ui/screens/ReportListRow;", "leaderboardRows", "timelineRows", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JLjava/util/List;Ljava/util/List;Ljava/util/List;Lkotlin/jvm/internal/DefaultConstructorMarker;)V", "getTitle", "()Ljava/lang/String;", "getSummary", "getHeroLabel", "getHeroValue", "getHeroTone-0d7_KjU", "()J", "J", "getKeyRows", "()Ljava/util/List;", "getLeaderboardRows", "getTimelineRows", "component1", "component2", "component3", "component4", "component5", "component5-0d7_KjU", "component6", "component7", "component8", "copy", "copy-yrwZFoE", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JLjava/util/List;Ljava/util/List;Ljava/util/List;)Lcom/example/yakultscanner/ui/screens/ReportModuleDetailBundle;", "equals", "", "other", "hashCode", "", "toString", "app_prodDebug"})
final class ReportModuleDetailBundle {
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String title = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String summary = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String heroLabel = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String heroValue = null;
    private final long heroTone = 0L;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> keyRows = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> leaderboardRows = null;
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> timelineRows = null;
    
    private ReportModuleDetailBundle(java.lang.String title, java.lang.String summary, java.lang.String heroLabel, java.lang.String heroValue, long heroTone, java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> keyRows, java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> leaderboardRows, java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> timelineRows) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getTitle() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSummary() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getHeroLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getHeroValue() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getKeyRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getLeaderboardRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.ui.screens.ReportListRow> getTimelineRows() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component4() {
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