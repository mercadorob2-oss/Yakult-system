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
import androidx.compose.ui.text.font.FontWeight;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.BorrowHomeSummaryResponse;
import com.example.yakultscanner.api.BorrowLogDto;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000d\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0004\n\u0002\u0010\u000b\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0014\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0002\u001a\u0010\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\u0010\u0010\u0004\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u0006H\u0003\u001a0\u0010\u0007\u001a\u00020\u00012\u0006\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\t2\u0006\u0010\u000b\u001a\u00020\t2\u0006\u0010\f\u001a\u00020\u00062\u0006\u0010\r\u001a\u00020\u000eH\u0003\u001a\b\u0010\u000f\u001a\u00020\u0001H\u0003\u001aY\u0010\u0010\u001a\u00020\u00012\u0006\u0010\u0011\u001a\u00020\u00062\u0006\u0010\u0005\u001a\u00020\u00062\u0006\u0010\u0012\u001a\u00020\u00132\u0006\u0010\u0014\u001a\u00020\u00132\u0016\b\u0002\u0010\u0015\u001a\u0010\u0012\u0004\u0012\u00020\u0006\u0012\u0004\u0012\u00020\u0006\u0018\u00010\u00162\u0010\b\u0002\u0010\u0017\u001a\n\u0012\u0004\u0012\u00020\u0001\u0018\u00010\u0018H\u0003\u00a2\u0006\u0004\b\u0019\u0010\u001a\u001a\u0010\u0010\u001b\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u0006H\u0003\u001aE\u0010\u001c\u001a\u00020\u00012\u0006\u0010\u0011\u001a\u00020\u00062\u0006\u0010\u001d\u001a\u00020\u00062\u0006\u0010\u001e\u001a\u00020\u00132\u0006\u0010\u001f\u001a\u00020\u00132\u0006\u0010 \u001a\u00020\u00062\f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00010\u0018H\u0003\u00a2\u0006\u0004\b\"\u0010#\u001a=\u0010$\u001a\u00020\u00012\u0006\u0010\u0011\u001a\u00020\u00062\u0006\u0010\u001d\u001a\u00020\u00062\u0006\u0010\u001e\u001a\u00020\u00132\u0006\u0010\u001f\u001a\u00020\u00132\f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00010\u0018H\u0003\u00a2\u0006\u0004\b%\u0010&\u001a9\u0010\'\u001a\u00020\u00012\u0006\u0010(\u001a\u00020\u00062\u0006\u0010)\u001a\u00020\u00062\u0006\u0010*\u001a\u00020\u00062\u0006\u0010+\u001a\u00020\u00132\b\b\u0002\u0010,\u001a\u00020-H\u0003\u00a2\u0006\u0004\b.\u0010/\u001a\u0010\u00100\u001a\u00020\u00012\u0006\u00101\u001a\u000202H\u0003\u001a\u000e\u00103\u001a\b\u0012\u0004\u0012\u00020204H\u0002\u001a\u0010\u00105\u001a\u0002022\u0006\u00106\u001a\u000207H\u0002\u001a\u0010\u00108\u001a\u00020\u00062\u0006\u00109\u001a\u00020\u0006H\u0002\u001a\u0010\u0010:\u001a\u00020\u00062\u0006\u0010;\u001a\u00020<H\u0002\u001a\u0012\u0010=\u001a\u0004\u0018\u00010<2\u0006\u00109\u001a\u00020\u0006H\u0002\u00a8\u0006>"}, d2 = {"BorrowHomeScreen", "", "navController", "Landroidx/navigation/NavController;", "PreviewBanner", "text", "", "WorkspaceHeroCard", "openCount", "", "overdueCount", "returnedTodayCount", "oldestOpenText", "usingPreview", "", "LoadingInfoCard", "HomeInfoCard", "title", "accentColor", "Landroidx/compose/ui/graphics/Color;", "contentColor", "supportDetails", "Lkotlin/Pair;", "onRetry", "Lkotlin/Function0;", "HomeInfoCard-9z6LAg8", "(Ljava/lang/String;Ljava/lang/String;JJLkotlin/Pair;Lkotlin/jvm/functions/Function0;)V", "SectionTitle", "PrimaryBorrowActionCard", "description", "accentBackground", "iconTint", "badgeText", "onClick", "PrimaryBorrowActionCard-9z6LAg8", "(Ljava/lang/String;Ljava/lang/String;JJLjava/lang/String;Lkotlin/jvm/functions/Function0;)V", "SecondaryBorrowActionCard", "SecondaryBorrowActionCard-OoHUuok", "(Ljava/lang/String;Ljava/lang/String;JJLkotlin/jvm/functions/Function0;)V", "BorrowMetricCard", "value", "label", "helper", "accent", "modifier", "Landroidx/compose/ui/Modifier;", "BorrowMetricCard-42QJj7c", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;JLandroidx/compose/ui/Modifier;)V", "RecentBorrowPreviewCard", "item", "Lcom/example/yakultscanner/ui/screens/BorrowHomePreview;", "previewBorrowHomeRows", "", "mapBorrowHomePreview", "row", "Lcom/example/yakultscanner/api/BorrowLogDto;", "formatBorrowHomeElapsed", "raw", "formatRelativeInstant", "instant", "Ljava/time/Instant;", "parseApiInstant", "app_prodDebug"})
public final class BorrowHomeScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void BorrowHomeScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void PreviewBanner(java.lang.String text) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void WorkspaceHeroCard(int openCount, int overdueCount, int returnedTodayCount, java.lang.String oldestOpenText, boolean usingPreview) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void LoadingInfoCard() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SectionTitle(java.lang.String text) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void RecentBorrowPreviewCard(com.example.yakultscanner.ui.screens.BorrowHomePreview item) {
    }
    
    private static final java.util.List<com.example.yakultscanner.ui.screens.BorrowHomePreview> previewBorrowHomeRows() {
        return null;
    }
    
    private static final com.example.yakultscanner.ui.screens.BorrowHomePreview mapBorrowHomePreview(com.example.yakultscanner.api.BorrowLogDto row) {
        return null;
    }
    
    private static final java.lang.String formatBorrowHomeElapsed(java.lang.String raw) {
        return null;
    }
    
    private static final java.lang.String formatRelativeInstant(java.time.Instant instant) {
        return null;
    }
    
    private static final java.time.Instant parseApiInstant(java.lang.String raw) {
        return null;
    }
}