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
import com.example.yakultscanner.api.DeploymentHistoryDto;
import com.example.yakultscanner.api.ItemMovementEntryDto;
import com.example.yakultscanner.api.ItemMovementResponse;
import com.example.yakultscanner.api.SerialLookupItemDto;
import com.example.yakultscanner.api.SerialLookupSetDto;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000X\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0000\u001a\u0010\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u0003H\u0007\u001a\b\u0010\u0004\u001a\u00020\u0001H\u0003\u001a\b\u0010\u0005\u001a\u00020\u0001H\u0003\u001a\u0010\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\bH\u0003\u001a\u0010\u0010\t\u001a\u00020\u00012\u0006\u0010\n\u001a\u00020\bH\u0003\u001a\u0010\u0010\u000b\u001a\u00020\u00012\u0006\u0010\f\u001a\u00020\rH\u0003\u001a2\u0010\u000e\u001a\u00020\u00012\u0006\u0010\f\u001a\u00020\r2\f\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00110\u00102\u0012\u0010\u0012\u001a\u000e\u0012\u0004\u0012\u00020\b\u0012\u0004\u0012\u00020\u00010\u0013H\u0003\u001a\u0010\u0010\u0014\u001a\u00020\u00012\u0006\u0010\f\u001a\u00020\rH\u0003\u001a,\u0010\u0015\u001a\u00020\u00012\u0006\u0010\u0016\u001a\u00020\u00112\u0006\u0010\u0017\u001a\u00020\u00182\u0012\u0010\u0012\u001a\u000e\u0012\u0004\u0012\u00020\b\u0012\u0004\u0012\u00020\u00010\u0013H\u0003\u001a\u0010\u0010\u0019\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\u001bH\u0003\u001a\u0010\u0010\u001c\u001a\u00020\u00012\u0006\u0010\u001d\u001a\u00020\bH\u0003\u001a\u0018\u0010\u001e\u001a\u00020\u00012\u0006\u0010\u001f\u001a\u00020 2\u0006\u0010!\u001a\u00020\u0018H\u0003\u001a\u0018\u0010\"\u001a\u00020\u00012\u0006\u0010\u001f\u001a\u00020#2\u0006\u0010!\u001a\u00020\u0018H\u0003\u001a\"\u0010$\u001a\u00020\u00012\u0006\u0010%\u001a\u00020\b2\u0006\u0010&\u001a\u00020\b2\b\b\u0002\u0010\'\u001a\u00020(H\u0003\u00a8\u0006)"}, d2 = {"SerialSetIdentifierScreen", "", "navController", "Landroidx/navigation/NavController;", "IdleHintCard", "LoadingCard", "NotFoundCard", "serial", "", "ErrorCard", "message", "FoundNoSetCard", "item", "Lcom/example/yakultscanner/api/SerialLookupItemDto;", "FoundInSetCard", "sets", "", "Lcom/example/yakultscanner/api/SerialLookupSetDto;", "onViewDetails", "Lkotlin/Function1;", "ItemDetailCard", "SetResultCard", "set", "isLatest", "", "ItemMovementSection", "itemId", "", "SetDeploymentHistorySection", "qrToken", "MovementTimelineItem", "entry", "Lcom/example/yakultscanner/api/ItemMovementEntryDto;", "isLast", "DeploymentTimelineItem", "Lcom/example/yakultscanner/api/DeploymentHistoryDto;", "LabeledValue", "label", "value", "modifier", "Landroidx/compose/ui/Modifier;", "app_prodDebug"})
public final class SerialSetIdentifierScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void SerialSetIdentifierScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void IdleHintCard() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void LoadingCard() {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void NotFoundCard(java.lang.String serial) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ErrorCard(java.lang.String message) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void FoundNoSetCard(com.example.yakultscanner.api.SerialLookupItemDto item) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void FoundInSetCard(com.example.yakultscanner.api.SerialLookupItemDto item, java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onViewDetails) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItemDetailCard(com.example.yakultscanner.api.SerialLookupItemDto item) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SetResultCard(com.example.yakultscanner.api.SerialLookupSetDto set, boolean isLatest, kotlin.jvm.functions.Function1<? super java.lang.String, kotlin.Unit> onViewDetails) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ItemMovementSection(int itemId) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void SetDeploymentHistorySection(java.lang.String qrToken) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void MovementTimelineItem(com.example.yakultscanner.api.ItemMovementEntryDto entry, boolean isLast) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void DeploymentTimelineItem(com.example.yakultscanner.api.DeploymentHistoryDto entry, boolean isLast) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void LabeledValue(java.lang.String label, java.lang.String value, androidx.compose.ui.Modifier modifier) {
    }
}