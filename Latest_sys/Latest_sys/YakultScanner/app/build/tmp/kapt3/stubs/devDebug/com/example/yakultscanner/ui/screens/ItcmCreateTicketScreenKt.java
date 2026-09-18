package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.text.KeyboardOptions;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.CallLookupItem;
import com.example.yakultscanner.api.CreateTicketRequest;
import com.example.yakultscanner.ui.components.ItcmUi;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.CreateTicketUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000D\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0006\n\u0002\u0010\b\n\u0002\b\u0004\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001af\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\b\u0010\t\u001a\u0004\u0018\u00010\n2\f\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\n0\f2\u0014\u0010\r\u001a\u0010\u0012\u0006\u0012\u0004\u0018\u00010\n\u0012\u0004\u0012\u00020\u00010\u000e2\b\b\u0002\u0010\u000f\u001a\u00020\u00102\b\b\u0002\u0010\u0011\u001a\u00020\u00122\b\b\u0002\u0010\u0013\u001a\u00020\b2\b\b\u0002\u0010\u0014\u001a\u00020\u0012H\u0003\u001aD\u0010\u0015\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\u0016\u001a\u00020\b2\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\b0\f2\u0012\u0010\r\u001a\u000e\u0012\u0004\u0012\u00020\b\u0012\u0004\u0012\u00020\u00010\u000e2\b\b\u0002\u0010\u000f\u001a\u00020\u0010H\u0003\u001a4\u0010\u0018\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\u0016\u001a\u00020\u00192\u0006\u0010\u001a\u001a\u00020\u00192\u0012\u0010\u001b\u001a\u000e\u0012\u0004\u0012\u00020\u0019\u0012\u0004\u0012\u00020\u00010\u000eH\u0003\u001a\u0018\u0010\u001c\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\u0016\u001a\u00020\bH\u0003\u00a8\u0006\u001d"}, d2 = {"ItcmCreateTicketScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "ItcmCreateLookupDropdown", "label", "", "selected", "Lcom/example/yakultscanner/api/CallLookupItem;", "items", "", "onSelected", "Lkotlin/Function1;", "modifier", "Landroidx/compose/ui/Modifier;", "enabled", "", "placeholder", "allowClear", "ItcmCreateStringDropdown", "value", "options", "ItcmDaysControl", "", "minimum", "onValueChange", "ItcmCreateReviewLine", "app_devDebug"})
public final class ItcmCreateTicketScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void ItcmCreateTicketScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmCreateLookupDropdown(java.lang.String label, com.example.yakultscanner.api.CallLookupItem selected, java.util.List<com.example.yakultscanner.api.CallLookupItem> items, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.api.CallLookupItem, kotlin.Unit> onSelected, androidx.compose.ui.Modifier modifier, boolean enabled, java.lang.String placeholder, boolean allowClear) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmCreateStringDropdown(java.lang.String label, java.lang.String value, java.util.List<java.lang.String> options, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onSelected, androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmDaysControl(java.lang.String label, int value, int minimum, kotlin.jvm.functions.Function1<? super java.lang.Integer, kotlin.Unit> onValueChange) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItcmCreateReviewLine(java.lang.String label, java.lang.String value) {
    }
}