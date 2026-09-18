package com.example.yakultscanner;

import android.os.Build;
import android.provider.Settings;
import android.widget.Toast;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.layout.ColumnScope;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import com.example.yakultscanner.settings.ApiSettings;
import com.example.yakultscanner.UserSession;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000<\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\u001a\u0016\u0010\u0000\u001a\u00020\u00012\f\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00010\u0003H\u0007\u001a\u0010\u0010\u0004\u001a\u00020\u00012\u0006\u0010\u0005\u001a\u00020\u0006H\u0003\u001a,\u0010\u0007\u001a\u00020\u00012\f\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00010\u00032\u0006\u0010\b\u001a\u00020\t2\f\u0010\n\u001a\b\u0012\u0004\u0012\u00020\u00010\u0003H\u0007\u001a\u0018\u0010\u000b\u001a\u00020\u00012\u0006\u0010\f\u001a\u00020\r2\u0006\u0010\u000e\u001a\u00020\rH\u0003\u001a.\u0010\u000f\u001a\u00020\u00012\u0006\u0010\u0010\u001a\u00020\r2\u001c\u0010\u0011\u001a\u0018\u0012\u0004\u0012\u00020\u0013\u0012\u0004\u0012\u00020\u00010\u0012\u00a2\u0006\u0002\b\u0014\u00a2\u0006\u0002\b\u0015H\u0003\u001a\u0018\u0010\u0016\u001a\u00020\u00012\u0006\u0010\u0010\u001a\u00020\r2\u0006\u0010\u0017\u001a\u00020\rH\u0003\u00a8\u0006\u0018"}, d2 = {"HowToUseScreen", "", "onNavigateBack", "Lkotlin/Function0;", "HowToUseContent", "paddingValues", "Landroidx/compose/foundation/layout/PaddingValues;", "AppInfoScreen", "isDarkMode", "", "onThemeToggle", "InfoRow", "label", "", "value", "InfoSectionCard", "title", "content", "Lkotlin/Function1;", "Landroidx/compose/foundation/layout/ColumnScope;", "Landroidx/compose/runtime/Composable;", "Lkotlin/ExtensionFunctionType;", "HelpStep", "body", "app_prodDebug"})
public final class HelpAndInfoScreensKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void HowToUseScreen(@org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onNavigateBack) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HowToUseContent(androidx.compose.foundation.layout.PaddingValues paddingValues) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void AppInfoScreen(@org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onNavigateBack, boolean isDarkMode, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onThemeToggle) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void InfoRow(java.lang.String label, java.lang.String value) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void InfoSectionCard(java.lang.String title, androidx.compose.runtime.internal.ComposableFunction1<? super androidx.compose.foundation.layout.ColumnScope, kotlin.Unit> content) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void HelpStep(java.lang.String title, java.lang.String body) {
    }
}