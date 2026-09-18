package com.example.yakultscanner.ui.screens;

import android.widget.Toast;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.input.ImeAction;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.data.model.TransmittalItemDraft;
import com.example.yakultscanner.data.model.TransmittalItemMode;
import com.example.yakultscanner.data.model.TransmittalReport;
import com.example.yakultscanner.data.model.TransmittalScanTarget;
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u00006\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0000\u001a\u0010\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\nH\u0007\u001a\u0010\u0010\u000b\u001a\u00020\b2\u0006\u0010\f\u001a\u00020\rH\u0003\u001a\u0018\u0010\u000e\u001a\u00020\b2\u0006\u0010\f\u001a\u00020\r2\u0006\u0010\u000f\u001a\u00020\u0010H\u0003\"\u000e\u0010\u0000\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\" \u0010\u0002\u001a\u0014\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u00050\u0004\u0012\u0004\u0012\u00020\u00060\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u0011"}, d2 = {"MAX_TRANSMITTAL_ITEMS", "", "transmittalItemSaver", "Landroidx/compose/runtime/saveable/Saver;", "", "Lcom/example/yakultscanner/data/model/TransmittalItemDraft;", "", "TransmittalScanScreen", "", "navController", "Landroidx/navigation/NavController;", "TransmittalSheetPreview", "report", "Lcom/example/yakultscanner/data/model/TransmittalReport;", "TransmittalFormPreview", "title", "", "app_devDebug"})
public final class TransmittalScanScreenKt {
    private static final int MAX_TRANSMITTAL_ITEMS = 500;
    @org.jetbrains.annotations.NotNull()
    private static final androidx.compose.runtime.saveable.Saver<java.util.List<com.example.yakultscanner.data.model.TransmittalItemDraft>, java.lang.Object> transmittalItemSaver = null;
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void TransmittalScanScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void TransmittalSheetPreview(com.example.yakultscanner.data.model.TransmittalReport report) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void TransmittalFormPreview(com.example.yakultscanner.data.model.TransmittalReport report, java.lang.String title) {
    }
}