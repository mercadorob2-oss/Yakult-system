package com.example.yakultscanner.ui.screens;

import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.foundation.text.KeyboardOptions;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.ExposedDropdownMenuDefaults;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.input.KeyboardType;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.CallConditionDto;
import com.example.yakultscanner.api.CallItemLookupDto;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.ResolutionRequest;
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel;
import com.example.yakultscanner.viewmodels.ResolutionUiState;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000X\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010 \n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0010\u001a\"\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u0007H\u0007\u001a:\u0010\b\u001a\u00020\u00012\u0006\u0010\t\u001a\u00020\n2\f\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\n0\f2\u0006\u0010\r\u001a\u00020\n2\u0012\u0010\u000e\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010\u000fH\u0003\u001a<\u0010\u0010\u001a\u00020\u00012\u0006\u0010\t\u001a\u00020\n2\f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00120\f2\b\u0010\r\u001a\u0004\u0018\u00010\u00122\u0012\u0010\u000e\u001a\u000e\u0012\u0004\u0012\u00020\u0012\u0012\u0004\u0012\u00020\u00010\u000fH\u0003\u001a4\u0010\u0013\u001a\u00020\u00012\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00150\f2\b\u0010\r\u001a\u0004\u0018\u00010\u00152\u0012\u0010\u000e\u001a\u000e\u0012\u0004\u0012\u00020\u0015\u0012\u0004\u0012\u00020\u00010\u000fH\u0003\u001a$\u0010\u0016\u001a\u00020\u00012\u0006\u0010\u0017\u001a\u00020\n2\u0012\u0010\u0018\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010\u000fH\u0003\u001a4\u0010\u0019\u001a\u00020\u00012\f\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\u001a0\f2\b\u0010\r\u001a\u0004\u0018\u00010\u001a2\u0012\u0010\u000e\u001a\u000e\u0012\u0004\u0012\u00020\u001a\u0012\u0004\u0012\u00020\u00010\u000fH\u0003\u001a\u0010\u0010\u001b\u001a\u00020\u00012\u0006\u0010\u001c\u001a\u00020\nH\u0003\u001a\u0010\u0010\u001d\u001a\u00020\u00012\u0006\u0010\u001c\u001a\u00020\nH\u0003\u001a\u0098\u0001\u0010\u001e\u001a\u00020\u001f2\u0006\u0010\u0004\u001a\u00020\u00052\u0006\u0010 \u001a\u00020!2\u0006\u0010\"\u001a\u00020!2\b\u0010#\u001a\u0004\u0018\u00010\u00122\u0006\u0010$\u001a\u00020\n2\u0006\u0010%\u001a\u00020\n2\u0006\u0010&\u001a\u00020\n2\u0006\u0010\'\u001a\u00020\n2\u0006\u0010(\u001a\u00020\n2\b\u0010)\u001a\u0004\u0018\u00010\u001a2\b\u0010*\u001a\u0004\u0018\u00010\u00152\u0006\u0010+\u001a\u00020\n2\u0006\u0010,\u001a\u00020\n2\b\u0010-\u001a\u0004\u0018\u00010\u00122\u0006\u0010.\u001a\u00020\u00052\u0006\u0010/\u001a\u00020!2\u0006\u00100\u001a\u00020\nH\u0002\u00a8\u00061"}, d2 = {"CallMarkAsResolvedScreen", "", "navController", "Landroidx/navigation/NavController;", "ticketId", "", "viewModel", "Lcom/example/yakultscanner/viewmodels/CallMonitoringViewModel;", "CategoryDropdown", "label", "", "categories", "", "selected", "onSelected", "Lkotlin/Function1;", "ItemDropdown", "items", "Lcom/example/yakultscanner/api/CallItemLookupDto;", "ConditionDropdown", "conditions", "Lcom/example/yakultscanner/api/CallConditionDto;", "UnitDropdown", "value", "onValueChange", "ItemCategoryDropdown", "Lcom/example/yakultscanner/api/ItemCategoryDto;", "GroupLabel", "text", "DetailLine", "buildResolutionRequest", "Lcom/example/yakultscanner/api/ResolutionRequest;", "isReplacement", "", "useUnlisted", "selectedOldItem", "unlistedName", "unlistedModel", "unlistedSerial", "unlistedUnit", "unlistedDesc", "unlistedCategory", "selectedCondition", "conditionRemarks", "repairAction", "selectedNewItem", "quantity", "isTemporary", "remarks", "app_devDebug"})
public final class CallMarkAsResolvedScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void CallMarkAsResolvedScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, int ticketId, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.CallMonitoringViewModel viewModel) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void CategoryDropdown(java.lang.String label, java.util.List<java.lang.String> categories, java.lang.String selected, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onSelected) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItemDropdown(java.lang.String label, java.util.List<com.example.yakultscanner.api.CallItemLookupDto> items, com.example.yakultscanner.api.CallItemLookupDto selected, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.api.CallItemLookupDto, kotlin.Unit> onSelected) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ConditionDropdown(java.util.List<com.example.yakultscanner.api.CallConditionDto> conditions, com.example.yakultscanner.api.CallConditionDto selected, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.api.CallConditionDto, kotlin.Unit> onSelected) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void UnitDropdown(java.lang.String value, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onValueChange) {
    }
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    private static final void ItemCategoryDropdown(java.util.List<com.example.yakultscanner.api.ItemCategoryDto> categories, com.example.yakultscanner.api.ItemCategoryDto selected, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.api.ItemCategoryDto, kotlin.Unit> onSelected) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void GroupLabel(java.lang.String text) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DetailLine(java.lang.String text) {
    }
    
    private static final com.example.yakultscanner.api.ResolutionRequest buildResolutionRequest(int ticketId, boolean isReplacement, boolean useUnlisted, com.example.yakultscanner.api.CallItemLookupDto selectedOldItem, java.lang.String unlistedName, java.lang.String unlistedModel, java.lang.String unlistedSerial, java.lang.String unlistedUnit, java.lang.String unlistedDesc, com.example.yakultscanner.api.ItemCategoryDto unlistedCategory, com.example.yakultscanner.api.CallConditionDto selectedCondition, java.lang.String conditionRemarks, java.lang.String repairAction, com.example.yakultscanner.api.CallItemLookupDto selectedNewItem, int quantity, boolean isTemporary, java.lang.String remarks) {
        return null;
    }
}