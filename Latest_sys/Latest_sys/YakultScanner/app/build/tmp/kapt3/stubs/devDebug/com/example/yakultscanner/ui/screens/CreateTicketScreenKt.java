package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.CallLookupItem;
import com.example.yakultscanner.api.CreateTicketRequest;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.CreateTicketUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000R\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0003\n\u0002\u0010\b\n\u0002\b\u0006\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a\u0018\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\bH\u0003\u001a-\u0010\n\u001a\u00020\u00012\u0006\u0010\u000b\u001a\u00020\b2\b\b\u0002\u0010\f\u001a\u00020\r2\u0011\u0010\u000e\u001a\r\u0012\u0004\u0012\u00020\u00010\u000f\u00a2\u0006\u0002\b\u0010H\u0003\u001ad\u0010\u0011\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\b\u0010\u0012\u001a\u0004\u0018\u00010\u00132\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00130\u00152\u0012\u0010\u0016\u001a\u000e\u0012\u0004\u0012\u00020\u0013\u0012\u0004\u0012\u00020\u00010\u00172\b\b\u0002\u0010\f\u001a\u00020\r2\b\b\u0002\u0010\u0018\u001a\u00020\u00192\b\b\u0002\u0010\u001a\u001a\u00020\b2\b\b\u0002\u0010\u001b\u001a\u00020\u0019H\u0003\u001a>\u0010\u001c\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\u001d2\u0006\u0010\u001e\u001a\u00020\u001d2\b\b\u0002\u0010\u001f\u001a\u00020\u001d2\u0012\u0010 \u001a\u000e\u0012\u0004\u0012\u00020\u001d\u0012\u0004\u0012\u00020\u00010\u0017H\u0003\u001aD\u0010!\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\b2\f\u0010\"\u001a\b\u0012\u0004\u0012\u00020\b0\u00152\u0012\u0010\u0016\u001a\u000e\u0012\u0004\u0012\u00020\b\u0012\u0004\u0012\u00020\u00010\u00172\b\b\u0002\u0010\f\u001a\u00020\rH\u0003\u00a8\u0006#"}, d2 = {"CreateTicketScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "ReviewLine", "label", "", "value", "FormSectionCard", "title", "modifier", "Landroidx/compose/ui/Modifier;", "content", "Lkotlin/Function0;", "Landroidx/compose/runtime/Composable;", "LookupDropdown", "selected", "Lcom/example/yakultscanner/api/CallLookupItem;", "items", "", "onSelect", "Lkotlin/Function1;", "enabled", "", "placeholder", "allowClear", "DaysStepper", "", "min", "max", "onValueChange", "StaticDropdown", "options", "app_devDebug"})
public final class CreateTicketScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void CreateTicketScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ReviewLine(java.lang.String label, java.lang.String value) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void FormSectionCard(java.lang.String title, androidx.compose.ui.Modifier modifier, androidx.compose.runtime.internal.ComposableFunction0<kotlin.Unit> content) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void LookupDropdown(java.lang.String label, com.example.yakultscanner.api.CallLookupItem selected, java.util.List<com.example.yakultscanner.api.CallLookupItem> items, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.api.CallLookupItem, kotlin.Unit> onSelect, androidx.compose.ui.Modifier modifier, boolean enabled, java.lang.String placeholder, boolean allowClear) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DaysStepper(java.lang.String label, int value, int min, int max, kotlin.jvm.functions.Function1<? super java.lang.Integer, kotlin.Unit> onValueChange) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void StaticDropdown(java.lang.String label, java.lang.String value, java.util.List<java.lang.String> options, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onSelect, androidx.compose.ui.Modifier modifier) {
    }
}