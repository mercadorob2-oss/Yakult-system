package com.example.yakultscanner.ui.components;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.layout.ColumnScope;
import androidx.compose.material3.CardDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000@\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010\u000b\n\u0002\b\u0015\n\u0002\u0010\b\n\u0000\u001a0\u0010\u0000\u001a\u00020\u00012\b\b\u0002\u0010\u0002\u001a\u00020\u00032\u001c\u0010\u0004\u001a\u0018\u0012\u0004\u0012\u00020\u0006\u0012\u0004\u0012\u00020\u00010\u0005\u00a2\u0006\u0002\b\u0007\u00a2\u0006\u0002\b\bH\u0007\u001a9\u0010\t\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\u000b2\b\b\u0002\u0010\u0002\u001a\u00020\u00032\u0015\b\u0002\u0010\r\u001a\u000f\u0012\u0004\u0012\u00020\u0001\u0018\u00010\u000e\u00a2\u0006\u0002\b\u0007H\u0007\u001a&\u0010\u000f\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\u000b2\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u000b2\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001aF\u0010\u0010\u001a\u00020\u00012\u0006\u0010\u0011\u001a\u00020\u000b2\u0006\u0010\u0012\u001a\u00020\u000b2\u0006\u0010\u0013\u001a\u00020\u00142\f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00010\u000e2\f\u0010\u0016\u001a\b\u0012\u0004\u0012\u00020\u00010\u000e2\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001a.\u0010\u0017\u001a\u00020\u00012\u0006\u0010\u0018\u001a\u00020\u000b2\u0006\u0010\u0019\u001a\u00020\u00142\f\u0010\u001a\u001a\b\u0012\u0004\u0012\u00020\u00010\u000e2\u0006\u0010\u0002\u001a\u00020\u0003H\u0003\u001a\"\u0010\u001b\u001a\u00020\u00012\u0006\u0010\u0018\u001a\u00020\u000b2\u0006\u0010\u001c\u001a\u00020\u000b2\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\u001a\u0010\u001d\u001a\u00020\u00012\u0006\u0010\u001e\u001a\u00020\u000b2\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001aP\u0010\u001f\u001a\u00020\u00012\u0006\u0010 \u001a\u00020\u000b2\f\u0010!\u001a\b\u0012\u0004\u0012\u00020\u00010\u000e2\n\b\u0002\u0010\"\u001a\u0004\u0018\u00010\u000b2\u0010\b\u0002\u0010#\u001a\n\u0012\u0004\u0012\u00020\u0001\u0018\u00010\u000e2\b\b\u0002\u0010$\u001a\u00020\u00142\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\"\u0010%\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\u000b2\u0006\u0010&\u001a\u00020\u000b2\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\u001a\u0010\'\u001a\u00020\u00012\u0006\u0010\u001c\u001a\u00020\u000b2\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\u0012\u0010(\u001a\u00020\u00012\b\b\u0002\u0010)\u001a\u00020*H\u0007\u00a8\u0006+"}, d2 = {"ScannerSurfaceCard", "", "modifier", "Landroidx/compose/ui/Modifier;", "content", "Lkotlin/Function1;", "Landroidx/compose/foundation/layout/ColumnScope;", "Landroidx/compose/runtime/Composable;", "Lkotlin/ExtensionFunctionType;", "ScannerWorkspaceHeader", "title", "", "subtitle", "trailing", "Lkotlin/Function0;", "ScannerSectionHeader", "ScannerModeSegment", "firstLabel", "secondLabel", "firstSelected", "", "onFirstSelected", "onSecondSelected", "ScannerModeChoice", "label", "selected", "onClick", "ScannerMetricChip", "value", "ScannerStatusChip", "status", "ScannerActionBar", "primaryLabel", "onPrimary", "secondaryLabel", "onSecondary", "primaryEnabled", "ScannerEmptyState", "message", "ScannerMonospaceValue", "ScannerSpacer", "height", "", "app_devDebug"})
public final class ScannerWorkspaceUiKitKt {
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerSurfaceCard(@org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier, @org.jetbrains.annotations.NotNull()
    androidx.compose.runtime.internal.ComposableFunction1<? super androidx.compose.foundation.layout.ColumnScope, kotlin.Unit> content) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerWorkspaceHeader(@org.jetbrains.annotations.NotNull()
    java.lang.String title, @org.jetbrains.annotations.NotNull()
    java.lang.String subtitle, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier, @org.jetbrains.annotations.Nullable()
    androidx.compose.runtime.internal.ComposableFunction0<kotlin.Unit> trailing) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerSectionHeader(@org.jetbrains.annotations.NotNull()
    java.lang.String title, @org.jetbrains.annotations.Nullable()
    java.lang.String subtitle, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerModeSegment(@org.jetbrains.annotations.NotNull()
    java.lang.String firstLabel, @org.jetbrains.annotations.NotNull()
    java.lang.String secondLabel, boolean firstSelected, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onFirstSelected, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onSecondSelected, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ScannerModeChoice(java.lang.String label, boolean selected, kotlin.jvm.functions.Function0<kotlin.Unit> onClick, androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerMetricChip(@org.jetbrains.annotations.NotNull()
    java.lang.String label, @org.jetbrains.annotations.NotNull()
    java.lang.String value, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerStatusChip(@org.jetbrains.annotations.NotNull()
    java.lang.String status, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerActionBar(@org.jetbrains.annotations.NotNull()
    java.lang.String primaryLabel, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onPrimary, @org.jetbrains.annotations.Nullable()
    java.lang.String secondaryLabel, @org.jetbrains.annotations.Nullable()
    kotlin.jvm.functions.Function0<kotlin.Unit> onSecondary, boolean primaryEnabled, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerEmptyState(@org.jetbrains.annotations.NotNull()
    java.lang.String title, @org.jetbrains.annotations.NotNull()
    java.lang.String message, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerMonospaceValue(@org.jetbrains.annotations.NotNull()
    java.lang.String value, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ScannerSpacer(int height) {
    }
}