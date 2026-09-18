package com.example.yakultscanner.ui.components;

import androidx.compose.material.icons.Icons;
import androidx.compose.material3.*;
import androidx.compose.runtime.*;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import com.example.yakultscanner.R;
import java.text.SimpleDateFormat;
import java.util.*;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000:\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0005\n\u0002\u0010 \n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0002\b\u0007\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0010\t\n\u0000\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a\u009a\u0001\u0010\u0006\u001a\u00020\u0001\"\u0004\b\u0000\u0010\u00072\u0006\u0010\b\u001a\u00020\u00032\u0006\u0010\t\u001a\u00020\u00032\f\u0010\n\u001a\b\u0012\u0004\u0012\u0002H\u00070\u000b2\u0012\u0010\f\u001a\u000e\u0012\u0004\u0012\u0002H\u0007\u0012\u0004\u0012\u00020\u00030\r2\u0012\u0010\u000e\u001a\u000e\u0012\u0004\u0012\u0002H\u0007\u0012\u0004\u0012\u00020\u00010\r2\u0006\u0010\u000f\u001a\u00020\u00102\u0012\u0010\u0011\u001a\u000e\u0012\u0004\u0012\u00020\u0010\u0012\u0004\u0012\u00020\u00010\r2\b\b\u0002\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0012\u001a\u00020\u00032\b\b\u0002\u0010\u0013\u001a\u00020\u00102\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u0003H\u0007\u001a(\u0010\u0015\u001a\u00020\u00012\u0006\u0010\u0016\u001a\u00020\u00032\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00010\u00182\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a6\u0010\u0019\u001a\u00020\u00012\u0006\u0010\b\u001a\u00020\u00032\u0006\u0010\u001a\u001a\u00020\u00032\u0012\u0010\u001b\u001a\u000e\u0012\u0004\u0012\u00020\u001c\u0012\u0004\u0012\u00020\u00010\r2\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u00a8\u0006\u001d"}, d2 = {"BatchSectionHeader", "", "text", "", "modifier", "Landroidx/compose/ui/Modifier;", "BatchDropdownField", "T", "label", "selectedValue", "options", "", "optionLabel", "Lkotlin/Function1;", "onOptionSelected", "expanded", "", "onExpandedChange", "placeholder", "isError", "supportingText", "SerialItemCard", "serial", "onDelete", "Lkotlin/Function0;", "BatchDateField", "value", "onDateSelected", "", "app_devDebug"})
public final class BatchComponentsKt {
    
    @androidx.compose.runtime.Composable()
    public static final void BatchSectionHeader(@org.jetbrains.annotations.NotNull()
    java.lang.String text, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final <T extends java.lang.Object>void BatchDropdownField(@org.jetbrains.annotations.NotNull()
    java.lang.String label, @org.jetbrains.annotations.NotNull()
    java.lang.String selectedValue, @org.jetbrains.annotations.NotNull()
    java.util.List<? extends T> options, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super T, java.lang.String> optionLabel, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super T, kotlin.Unit> onOptionSelected, boolean expanded, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super java.lang.Boolean, kotlin.Unit> onExpandedChange, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier, @org.jetbrains.annotations.NotNull()
    java.lang.String placeholder, boolean isError, @org.jetbrains.annotations.Nullable()
    java.lang.String supportingText) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void SerialItemCard(@org.jetbrains.annotations.NotNull()
    java.lang.String serial, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDelete, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void BatchDateField(@org.jetbrains.annotations.NotNull()
    java.lang.String label, @org.jetbrains.annotations.NotNull()
    java.lang.String value, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super java.lang.Long, kotlin.Unit> onDateSelected, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
}