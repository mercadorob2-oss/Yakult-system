package com.example.yakultscanner.ui.screens;

import android.app.DatePickerDialog;
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
import com.example.yakultscanner.api.BorrowAccessDto;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.BorrowDeleteRequest;
import com.example.yakultscanner.api.BorrowLogDto;
import com.example.yakultscanner.api.BorrowLogPageResponse;
import com.example.yakultscanner.utils.TextExportUtils;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.time.temporal.ChronoUnit;
import java.util.Calendar;
import java.util.Locale;
import java.util.UUID;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000p\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\t\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\t\n\u0002\b\u000e\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\b\r\n\u0002\u0010 \n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\n\u001a\u0010\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u0003H\u0007\u001a$\u0010\u0004\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u00062\u0012\u0010\u0007\u001a\u000e\u0012\u0004\u0012\u00020\u0006\u0012\u0004\u0012\u00020\u00010\bH\u0003\u001a8\u0010\t\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\u000b2\u0006\u0010\r\u001a\u00020\u000e2\b\b\u0002\u0010\u000f\u001a\u00020\u00102\f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a \u0010\u0013\u001a\u00020\u00012\u0006\u0010\u0014\u001a\u00020\u00062\u0006\u0010\u0015\u001a\u00020\u00062\u0006\u0010\u0016\u001a\u00020\u000bH\u0003\u001a\"\u0010\u0017\u001a\u00020\u00012\u0006\u0010\u0018\u001a\u00020\u000b2\u0006\u0010\u0019\u001a\u00020\u000b2\b\b\u0002\u0010\u000f\u001a\u00020\u0010H\u0003\u001aP\u0010\u001a\u001a\u00020\u00012\u0006\u0010\u001b\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u001e2\u0006\u0010\u001f\u001a\u00020\u001e2\u0012\u0010 \u001a\u000e\u0012\u0004\u0012\u00020\u001c\u0012\u0004\u0012\u00020\u00010\b2\f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00010\u00122\f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001aL\u0010#\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u00062\u0006\u0010$\u001a\u00020\u000e2\u0006\u0010%\u001a\u00020\u000e2\u0006\u0010&\u001a\u00020\u000e2\u0006\u0010\'\u001a\u00020\u000e2\f\u0010(\u001a\b\u0012\u0004\u0012\u00020\u00010\u00122\f\u0010)\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a:\u0010*\u001a\u00020\u00012\u0006\u0010+\u001a\u00020\u000b2\u0016\b\u0002\u0010,\u001a\u0010\u0012\u0004\u0012\u00020\u000b\u0012\u0004\u0012\u00020\u000b\u0018\u00010-2\u0010\b\u0002\u0010.\u001a\n\u0012\u0004\u0012\u00020\u0001\u0018\u00010\u0012H\u0003\u001a\u0010\u0010/\u001a\u00020\u00012\u0006\u0010+\u001a\u00020\u000bH\u0003\u001a\b\u00100\u001a\u00020\u0001H\u0003\u001a\u0010\u00101\u001a\u00020\u00012\u0006\u00102\u001a\u00020\u000eH\u0003\u001a.\u00103\u001a\u00020\u00012\u0006\u00104\u001a\u0002052\u0006\u00102\u001a\u00020\u000e2\u0006\u00106\u001a\u00020\u000e2\f\u00107\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a\"\u00108\u001a\u00020\u00012\u0006\u00109\u001a\u00020\u000b2\u0006\u00102\u001a\u00020\u000e2\b\b\u0002\u0010\u000f\u001a\u00020\u0010H\u0003\u001a\u0010\u0010:\u001a\u00020\u00012\u0006\u00104\u001a\u000205H\u0003\u001a#\u0010;\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\u000b2\u0011\u0010<\u001a\r\u0012\u0004\u0012\u00020\u00010\u0012\u00a2\u0006\u0002\b=H\u0003\u001a\u0018\u0010>\u001a\u00020\u00012\u0006\u0010\u0018\u001a\u00020\u000b2\u0006\u0010\u0019\u001a\u00020\u000bH\u0003\u001a<\u0010?\u001a\u00020\u00012\u0006\u0010@\u001a\u00020\u00062\u0006\u0010A\u001a\u00020\u00062\u0006\u0010B\u001a\u00020\u00062\f\u0010C\u001a\b\u0012\u0004\u0012\u00020\u00010\u00122\f\u0010D\u001a\b\u0012\u0004\u0012\u00020\u00010\u0012H\u0003\u001a\u001a\u0010E\u001a\u0004\u0018\u00010\u000b2\u0006\u0010F\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u001eH\u0002\u001a\"\u0010G\u001a\u0004\u0018\u00010\u000b2\u0006\u0010F\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u001e2\u0006\u0010\u001f\u001a\u00020\u001eH\u0002\u001a(\u0010H\u001a\u00020\u000e2\u0006\u00104\u001a\u0002052\u0006\u0010F\u001a\u00020\u001c2\u0006\u0010\u001d\u001a\u00020\u001e2\u0006\u0010\u001f\u001a\u00020\u001eH\u0002\u001a\u001e\u0010I\u001a\u00020\u000b2\f\u0010J\u001a\b\u0012\u0004\u0012\u0002050K2\u0006\u00102\u001a\u00020\u000eH\u0002\u001a\u0012\u0010L\u001a\u00020\u000b2\b\u0010\u0019\u001a\u0004\u0018\u00010\u000bH\u0002\u001a,\u0010M\u001a\u00020\u00012\u0006\u0010N\u001a\u00020O2\u0006\u0010P\u001a\u00020\u001e2\u0012\u0010Q\u001a\u000e\u0012\u0004\u0012\u00020\u001e\u0012\u0004\u0012\u00020\u00010\bH\u0002\u001a\u0010\u0010R\u001a\u00020\u001e2\u0006\u0010P\u001a\u00020\u001eH\u0002\u001a\u0010\u0010S\u001a\u00020\u000b2\u0006\u0010T\u001a\u00020\u001eH\u0002\u001a\u0012\u0010U\u001a\u00020\u000b2\b\u0010V\u001a\u0004\u0018\u00010\u000bH\u0002\u001a\u0012\u0010W\u001a\u00020\u000b2\b\u0010V\u001a\u0004\u0018\u00010\u000bH\u0002\u001a\u0012\u0010X\u001a\u00020\u000b2\b\u0010V\u001a\u0004\u0018\u00010\u000bH\u0002\u00a8\u0006Y"}, d2 = {"BorrowRecordsScreen", "", "navController", "Landroidx/navigation/NavController;", "RecordModeTabs", "selectedTab", "", "onTabSelected", "Lkotlin/Function1;", "RecordModeTabCard", "title", "", "subtitle", "selected", "", "modifier", "Landroidx/compose/ui/Modifier;", "onClick", "Lkotlin/Function0;", "SummaryStrip", "openCount", "historyCount", "oldestOpenText", "SummaryMetric", "label", "value", "HistoryRangeCard", "selectedRange", "Lcom/example/yakultscanner/ui/screens/BorrowHistoryRange;", "customFromMillis", "", "customToMillis", "onRangeSelected", "onPickFrom", "onPickTo", "RecordManagementCard", "canExportCsv", "canDeleteOpenBorrow", "loading", "exporting", "onRefresh", "onExport", "ErrorCard", "message", "supportDetails", "Lkotlin/Pair;", "onRetry", "NoticeCard", "LoadingCard", "EmptyRecordsCard", "isHistory", "BorrowRecordRow", "record", "Lcom/example/yakultscanner/api/BorrowLogDto;", "showDeleteAction", "onOpenDetails", "StatusBadge", "text", "RecordDetailHeroCard", "DetailSectionCard", "content", "Landroidx/compose/runtime/Composable;", "DetailLine", "PaginationCard", "pageIndex", "totalCount", "pageSize", "onPrevious", "onNext", "buildHistoryFromUtc", "range", "buildHistoryToUtcExclusive", "isHistoryRecordVisible", "buildBorrowCsv", "rows", "", "csvCell", "showRecordsDatePicker", "context", "Landroid/content/Context;", "currentMillis", "onSelected", "startOfDay", "formatShortDate", "millis", "formatElapsed", "raw", "formatRecordTimestamp", "formatCompactTimestamp", "app_prodDebug"})
public final class BorrowRecordsScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void BorrowRecordsScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void RecordModeTabs(int selectedTab, kotlin.jvm.functions.Function1<? super java.lang.Integer, kotlin.Unit> onTabSelected) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void RecordModeTabCard(java.lang.String title, java.lang.String subtitle, boolean selected, androidx.compose.ui.Modifier modifier, kotlin.jvm.functions.Function0<kotlin.Unit> onClick) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SummaryStrip(int openCount, int historyCount, java.lang.String oldestOpenText) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SummaryMetric(java.lang.String label, java.lang.String value, androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HistoryRangeCard(com.example.yakultscanner.ui.screens.BorrowHistoryRange selectedRange, long customFromMillis, long customToMillis, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.ui.screens.BorrowHistoryRange, kotlin.Unit> onRangeSelected, kotlin.jvm.functions.Function0<kotlin.Unit> onPickFrom, kotlin.jvm.functions.Function0<kotlin.Unit> onPickTo) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void RecordManagementCard(int selectedTab, boolean canExportCsv, boolean canDeleteOpenBorrow, boolean loading, boolean exporting, kotlin.jvm.functions.Function0<kotlin.Unit> onRefresh, kotlin.jvm.functions.Function0<kotlin.Unit> onExport) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ErrorCard(java.lang.String message, kotlin.Pair<java.lang.String, java.lang.String> supportDetails, kotlin.jvm.functions.Function0<kotlin.Unit> onRetry) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void NoticeCard(java.lang.String message) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void LoadingCard() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void EmptyRecordsCard(boolean isHistory) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void BorrowRecordRow(com.example.yakultscanner.api.BorrowLogDto record, boolean isHistory, boolean showDeleteAction, kotlin.jvm.functions.Function0<kotlin.Unit> onOpenDetails) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void StatusBadge(java.lang.String text, boolean isHistory, androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void RecordDetailHeroCard(com.example.yakultscanner.api.BorrowLogDto record) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DetailSectionCard(java.lang.String title, androidx.compose.runtime.internal.ComposableFunction0<kotlin.Unit> content) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DetailLine(java.lang.String label, java.lang.String value) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void PaginationCard(int pageIndex, int totalCount, int pageSize, kotlin.jvm.functions.Function0<kotlin.Unit> onPrevious, kotlin.jvm.functions.Function0<kotlin.Unit> onNext) {
    }
    
    private static final java.lang.String buildHistoryFromUtc(com.example.yakultscanner.ui.screens.BorrowHistoryRange range, long customFromMillis) {
        return null;
    }
    
    private static final java.lang.String buildHistoryToUtcExclusive(com.example.yakultscanner.ui.screens.BorrowHistoryRange range, long customFromMillis, long customToMillis) {
        return null;
    }
    
    private static final boolean isHistoryRecordVisible(com.example.yakultscanner.api.BorrowLogDto record, com.example.yakultscanner.ui.screens.BorrowHistoryRange range, long customFromMillis, long customToMillis) {
        return false;
    }
    
    private static final java.lang.String buildBorrowCsv(java.util.List<com.example.yakultscanner.api.BorrowLogDto> rows, boolean isHistory) {
        return null;
    }
    
    private static final java.lang.String csvCell(java.lang.String value) {
        return null;
    }
    
    private static final void showRecordsDatePicker(android.content.Context context, long currentMillis, kotlin.jvm.functions.Function1<? super java.lang.Long, kotlin.Unit> onSelected) {
    }
    
    private static final long startOfDay(long currentMillis) {
        return 0L;
    }
    
    private static final java.lang.String formatShortDate(long millis) {
        return null;
    }
    
    private static final java.lang.String formatElapsed(java.lang.String raw) {
        return null;
    }
    
    private static final java.lang.String formatRecordTimestamp(java.lang.String raw) {
        return null;
    }
    
    private static final java.lang.String formatCompactTimestamp(java.lang.String raw) {
        return null;
    }
}