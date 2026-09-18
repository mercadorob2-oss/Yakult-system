package com.example.yakultscanner.ui.screens;

import androidx.compose.animation.core.RepeatMode;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.graphics.vector.ImageVector;
import androidx.compose.ui.text.font.FontWeight;
import androidx.navigation.NavController;
import com.example.yakultscanner.ConnectionStatus;
import com.example.yakultscanner.PinnedSetsStore;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000T\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u0007\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0011\n\u0002\u0018\u0002\n\u0002\b\f\n\u0002\u0018\u0002\n\u0002\b\u0004\u001a\u0010\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u0003H\u0007\u001a*\u0010\u0004\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u00062\b\u0010\u0007\u001a\u0004\u0018\u00010\u00062\u0006\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\u000bH\u0003\u001a\b\u0010\f\u001a\u00020\u0001H\u0003\u001a6\u0010\r\u001a\u00020\u00012\u0006\u0010\u000e\u001a\u00020\u000b2\u0006\u0010\u000f\u001a\u00020\u00102\u0006\u0010\u0011\u001a\u00020\u00122\u0006\u0010\u0013\u001a\u00020\u00122\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u001a\"\u0010\u0016\u001a\u00020\u00012\u0006\u0010\u0017\u001a\u00020\u00062\u0006\u0010\u0018\u001a\u00020\u00062\b\b\u0002\u0010\u0019\u001a\u00020\u001aH\u0003\u001a\u0080\u0001\u0010\u001b\u001a\u00020\u00012\u0006\u0010\u001c\u001a\u00020\u000b2\u0006\u0010\u001d\u001a\u00020\u00102\u0006\u0010\u001e\u001a\u00020\u000b2\u0006\u0010\u001f\u001a\u00020\u00102\u0006\u0010 \u001a\u00020\u000b2\u0006\u0010!\u001a\u00020\u00102\u0006\u0010\"\u001a\u00020\u000b2\u0006\u0010#\u001a\u00020\u00102\f\u0010$\u001a\b\u0012\u0004\u0012\u00020\u00010\u00152\f\u0010%\u001a\b\u0012\u0004\u0012\u00020\u00010\u00152\f\u0010&\u001a\b\u0012\u0004\u0012\u00020\u00010\u00152\f\u0010\'\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u001aH\u0010(\u001a\u00020\u00012\b\b\u0002\u0010\u0019\u001a\u00020\u001a2\u0006\u0010)\u001a\u00020\u00062\u0006\u0010*\u001a\u00020\u00062\u0006\u0010+\u001a\u00020,2\u0006\u0010\u000e\u001a\u00020\u000b2\u0006\u0010\u000f\u001a\u00020\u00102\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u001a[\u0010-\u001a\u00020\u00012\u0006\u0010.\u001a\u00020\u00122\u0006\u0010\u0013\u001a\u00020\u00122\b\u0010/\u001a\u0004\u0018\u00010\u00122\b\u00100\u001a\u0004\u0018\u00010\u00122\f\u00101\u001a\b\u0012\u0004\u0012\u00020\u00010\u00152\f\u00102\u001a\b\u0012\u0004\u0012\u00020\u00010\u00152\f\u0010&\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u00a2\u0006\u0002\u00103\u001a>\u00104\u001a\u00020\u00012\u0006\u0010)\u001a\u00020\u00062\u0006\u00105\u001a\u00020\u00062\u0006\u0010+\u001a\u00020,2\u0006\u0010\u000e\u001a\u00020\u000b2\u0006\u0010\u000f\u001a\u00020\u00102\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u001aH\u00106\u001a\u00020\u00012\b\b\u0002\u0010\u0019\u001a\u00020\u001a2\u0006\u0010)\u001a\u00020\u00062\u0006\u00105\u001a\u00020\u00062\u0006\u0010+\u001a\u00020,2\u0006\u0010\u000e\u001a\u00020\u000b2\u0006\u0010\u000f\u001a\u00020\u00102\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u001aG\u00107\u001a\u00020\u00012\b\b\u0002\u0010\u0019\u001a\u00020\u001a2\u0006\u0010\u0017\u001a\u00020\u00062\u0006\u0010\u0018\u001a\u00020\u00062\u0006\u00108\u001a\u0002092\u0006\u0010:\u001a\u0002092\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00010\u0015H\u0003\u00a2\u0006\u0004\b;\u0010<\u00a8\u0006="}, d2 = {"RootHomeScreen", "", "navController", "Landroidx/navigation/NavController;", "HomeCommandHero", "greetingName", "", "lastUpdatedText", "connectionStatus", "Lcom/example/yakultscanner/ConnectionStatus;", "dotAlpha", "", "ConnectionNoticeCard", "ScannerCommandCard", "scale", "interactionSource", "Landroidx/compose/foundation/interaction/MutableInteractionSource;", "localScanCount", "", "pendingIssueCount", "onClick", "Lkotlin/Function0;", "CommandStatChip", "label", "value", "modifier", "Landroidx/compose/ui/Modifier;", "OperationsGridCard", "borrowScale", "borrowInteractionSource", "toolsScale", "toolsInteractionSource", "reportsScale", "reportsInteractionSource", "callMonitoringScale", "callMonitoringInteractionSource", "onBorrowClick", "onToolsClick", "onReportsClick", "onCallMonitoringClick", "OperationTile", "title", "subtitle", "icon", "Landroidx/compose/ui/graphics/vector/ImageVector;", "DashboardOverviewCard", "processedCount", "dispatchCount", "openBorrowCount", "onProcessedClick", "onPendingClick", "(IILjava/lang/Integer;Ljava/lang/Integer;Lkotlin/jvm/functions/Function0;Lkotlin/jvm/functions/Function0;Lkotlin/jvm/functions/Function0;)V", "PrimaryModuleCard", "description", "SecondaryModuleCard", "QuickMetricCard", "containerColor", "Landroidx/compose/ui/graphics/Color;", "contentColor", "QuickMetricCard-BQnUqu0", "(Landroidx/compose/ui/Modifier;Ljava/lang/String;Ljava/lang/String;JJLkotlin/jvm/functions/Function0;)V", "app_prodDebug"})
public final class RootHomeScreenKt {
    
    @androidx.compose.material3.ExperimentalMaterial3Api()
    @androidx.compose.runtime.Composable()
    public static final void RootHomeScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HomeCommandHero(java.lang.String greetingName, java.lang.String lastUpdatedText, com.example.yakultscanner.ConnectionStatus connectionStatus, float dotAlpha) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ConnectionNoticeCard() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ScannerCommandCard(float scale, androidx.compose.foundation.interaction.MutableInteractionSource interactionSource, int localScanCount, int pendingIssueCount, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void CommandStatChip(java.lang.String label, java.lang.String value, androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void OperationsGridCard(float borrowScale, androidx.compose.foundation.interaction.MutableInteractionSource borrowInteractionSource, float toolsScale, androidx.compose.foundation.interaction.MutableInteractionSource toolsInteractionSource, float reportsScale, androidx.compose.foundation.interaction.MutableInteractionSource reportsInteractionSource, float callMonitoringScale, androidx.compose.foundation.interaction.MutableInteractionSource callMonitoringInteractionSource, kotlin.jvm.functions.Function0<kotlin.Unit> onBorrowClick, kotlin.jvm.functions.Function0<kotlin.Unit> onToolsClick, kotlin.jvm.functions.Function0<kotlin.Unit> onReportsClick, kotlin.jvm.functions.Function0<kotlin.Unit> onCallMonitoringClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void OperationTile(androidx.compose.ui.Modifier modifier, java.lang.String title, java.lang.String subtitle, androidx.compose.ui.graphics.vector.ImageVector icon, float scale, androidx.compose.foundation.interaction.MutableInteractionSource interactionSource, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DashboardOverviewCard(int processedCount, int pendingIssueCount, java.lang.Integer dispatchCount, java.lang.Integer openBorrowCount, kotlin.jvm.functions.Function0<kotlin.Unit> onProcessedClick, kotlin.jvm.functions.Function0<kotlin.Unit> onPendingClick, kotlin.jvm.functions.Function0<kotlin.Unit> onReportsClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void PrimaryModuleCard(java.lang.String title, java.lang.String description, androidx.compose.ui.graphics.vector.ImageVector icon, float scale, androidx.compose.foundation.interaction.MutableInteractionSource interactionSource, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SecondaryModuleCard(androidx.compose.ui.Modifier modifier, java.lang.String title, java.lang.String description, androidx.compose.ui.graphics.vector.ImageVector icon, float scale, androidx.compose.foundation.interaction.MutableInteractionSource interactionSource, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
}