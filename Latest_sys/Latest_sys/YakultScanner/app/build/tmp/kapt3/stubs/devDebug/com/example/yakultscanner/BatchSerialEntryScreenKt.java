package com.example.yakultscanner;

import android.widget.Toast;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.ButtonDefaults;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.layout.ContentScale;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextAlign;
import androidx.navigation.NavController;
import com.example.yakultscanner.R;
import com.example.yakultscanner.viewmodels.BatchEntryViewModel;
import com.example.yakultscanner.viewmodels.PhoneItemEntry;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000F\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0006\n\u0002\u0010 \n\u0002\b\u0003\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001aB\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\n2\u0012\u0010\u000b\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010\f2\f\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u00010\u000e2\u0006\u0010\u000f\u001a\u00020\u0010H\u0003\u001ab\u0010\u0011\u001a\u00020\u00012\u0006\u0010\u0012\u001a\u00020\u00132\u0006\u0010\u0014\u001a\u00020\u00132\u0006\u0010\u0015\u001a\u00020\u00132\u0006\u0010\u0016\u001a\u00020\u00132\u0006\u0010\u0017\u001a\u00020\u00132\u0006\u0010\u0018\u001a\u00020\u00132\f\u0010\u0019\u001a\b\u0012\u0004\u0012\u00020\u00130\u001a2\f\u0010\u001b\u001a\b\u0012\u0004\u0012\u00020\u00010\u000e2\f\u0010\u001c\u001a\b\u0012\u0004\u0012\u00020\u00010\u000eH\u0003\u00a8\u0006\u001d"}, d2 = {"BatchSerialEntryScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/BatchEntryViewModel;", "PhoneItemEntryCard", "index", "", "row", "Lcom/example/yakultscanner/viewmodels/PhoneItemEntry;", "onChange", "Lkotlin/Function1;", "onRemove", "Lkotlin/Function0;", "canRemove", "", "BatchSubmitConfirmDialog", "itemName", "", "itemType", "category", "condition", "modelNumber", "vendor", "serialNumbers", "", "onDismiss", "onConfirm", "app_devDebug"})
public final class BatchSerialEntryScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void BatchSerialEntryScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.BatchEntryViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void PhoneItemEntryCard(int index, com.example.yakultscanner.viewmodels.PhoneItemEntry row, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.viewmodels.PhoneItemEntry, kotlin.Unit> onChange, kotlin.jvm.functions.Function0<kotlin.Unit> onRemove, boolean canRemove) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void BatchSubmitConfirmDialog(java.lang.String itemName, java.lang.String itemType, java.lang.String category, java.lang.String condition, java.lang.String modelNumber, java.lang.String vendor, java.util.List<java.lang.String> serialNumbers, kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, kotlin.jvm.functions.Function0<kotlin.Unit> onConfirm) {
    }
}