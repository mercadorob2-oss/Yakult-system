package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.lazy.LazyListScope;
import androidx.compose.foundation.layout.RowScope;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.graphics.vector.ImageVector;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.api.SetItemUpdateDto;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000h\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0005\u001a\u00d4\u0001\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\u0006\u0010\u0006\u001a\u00020\u00052\u0006\u0010\u0007\u001a\u00020\u00052\u0006\u0010\b\u001a\u00020\u00052\u0006\u0010\t\u001a\u00020\n2\u0006\u0010\u000b\u001a\u00020\f2\u0006\u0010\r\u001a\u00020\f2\u0006\u0010\u000e\u001a\u00020\u000f2\u0006\u0010\u0010\u001a\u00020\u00112\u0014\u0010\u0012\u001a\u0010\u0012\u0004\u0012\u00020\u0005\u0012\u0004\u0012\u00020\u0005\u0018\u00010\u00132\u0006\u0010\u0014\u001a\u00020\u00112\u0006\u0010\u0015\u001a\u00020\u00052\u0006\u0010\u0016\u001a\u00020\u00052\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00010\u00182\u001e\b\u0002\u0010\u0019\u001a\u0018\u0012\u0004\u0012\u00020\u001b\u0012\u0004\u0012\u00020\u00010\u001a\u00a2\u0006\u0002\b\u001c\u00a2\u0006\u0002\b\u001d2\u0017\u0010\u001e\u001a\u0013\u0012\u0004\u0012\u00020\u001f\u0012\u0004\u0012\u00020\u00010\u001a\u00a2\u0006\u0002\b\u001dH\u0007\u00a2\u0006\u0004\b \u0010!\u001aI\u0010\"\u001a\u00020\u00012\u0006\u0010#\u001a\u00020$2\u0006\u0010%\u001a\u00020\u00052\u0006\u0010&\u001a\u00020\u00052\u0006\u0010\t\u001a\u00020\n2\u0006\u0010\u000b\u001a\u00020\f2\u0006\u0010\r\u001a\u00020\f2\b\b\u0002\u0010\'\u001a\u00020(H\u0007\u00a2\u0006\u0004\b)\u0010*\u001a\u0018\u0010+\u001a\u00020\u00012\u0006\u0010\u0004\u001a\u00020\u00052\u0006\u0010,\u001a\u00020\u0005H\u0003\u00a8\u0006-"}, d2 = {"UpdatesListScreen", "", "navController", "Landroidx/navigation/NavController;", "title", "", "summaryTitle", "summaryText", "countLabel", "icon", "Landroidx/compose/ui/graphics/vector/ImageVector;", "iconContainerColor", "Landroidx/compose/ui/graphics/Color;", "iconTint", "headerBrush", "Landroidx/compose/ui/graphics/Brush;", "isLoading", "", "errorUi", "Lkotlin/Pair;", "isEmpty", "emptyTitle", "emptySubtitle", "onRetry", "Lkotlin/Function0;", "topBarActions", "Lkotlin/Function1;", "Landroidx/compose/foundation/layout/RowScope;", "Landroidx/compose/runtime/Composable;", "Lkotlin/ExtensionFunctionType;", "content", "Landroidx/compose/foundation/lazy/LazyListScope;", "UpdatesListScreen-zgQg56Y", "(Landroidx/navigation/NavController;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Landroidx/compose/ui/graphics/vector/ImageVector;JJLandroidx/compose/ui/graphics/Brush;ZLkotlin/Pair;ZLjava/lang/String;Ljava/lang/String;Lkotlin/jvm/functions/Function0;Landroidx/compose/runtime/internal/ComposableFunction1;Lkotlin/jvm/functions/Function1;)V", "UpdateRecordCard", "update", "Lcom/example/yakultscanner/api/SetItemUpdateDto;", "modeLabel", "timestamp", "modifier", "Landroidx/compose/ui/Modifier;", "UpdateRecordCard-ulV41r8", "(Lcom/example/yakultscanner/api/SetItemUpdateDto;Ljava/lang/String;Ljava/lang/String;Landroidx/compose/ui/graphics/vector/ImageVector;JJLandroidx/compose/ui/Modifier;)V", "EmptyUpdatesState", "subtitle", "app_prodDebug"})
public final class UpdatesUiKitKt {
    
    @androidx.compose.runtime.Composable()
    private static final void EmptyUpdatesState(java.lang.String title, java.lang.String subtitle) {
    }
}