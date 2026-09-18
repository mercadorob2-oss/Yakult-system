package com.example.yakultscanner.ui.screens;

import android.widget.Toast;
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
import androidx.compose.ui.text.input.ImeAction;
import androidx.compose.ui.text.style.TextOverflow;
import androidx.navigation.NavController;
import com.example.yakultscanner.data.db.LocalScanItemEntity;
import com.example.yakultscanner.data.db.LocalScanSessionEntity;
import com.example.yakultscanner.data.db.LocalScanSessionStatus;
import com.example.yakultscanner.data.db.LocalScanSessionWithCount;
import com.example.yakultscanner.data.repository.LocalScanSendResult;
import com.example.yakultscanner.data.repository.LocalScanRepository;
import com.example.yakultscanner.ui.components.ScannerWorkspaceUi;
import com.example.yakultscanner.utils.TextExportUtils;
import com.example.yakultscanner.viewmodels.LocalSerialScanViewModel;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000^\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0006\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\t\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001aB\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\u0006\u0010\t\u001a\u00020\n2\u0006\u0010\u000b\u001a\u00020\f2\u0012\u0010\r\u001a\u000e\u0012\u0004\u0012\u00020\n\u0012\u0004\u0012\u00020\u00010\u000e2\f\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00010\u0010H\u0003\u001aV\u0010\u0011\u001a\u00020\u00012\u0006\u0010\u0012\u001a\u00020\u00132\f\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00010\u00102\f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00010\u00102\f\u0010\u0016\u001a\b\u0012\u0004\u0012\u00020\u00010\u00102\f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\u00010\u00102\f\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00010\u0010H\u0003\u001a\b\u0010\u0019\u001a\u00020\u001aH\u0002\u001a\u0010\u0010\u001b\u001a\u00020\u001a2\u0006\u0010\u001c\u001a\u00020\u001dH\u0002\u001a\b\u0010\u001e\u001a\u00020\u001aH\u0002\u001a\u0012\u0010\u001f\u001a\u00020\u001a2\b\u0010\u001c\u001a\u0004\u0018\u00010\u001aH\u0002\u001a\u001e\u0010 \u001a\u00020\u001a2\u0006\u0010\u0012\u001a\u00020!2\f\u0010\"\u001a\b\u0012\u0004\u0012\u00020$0#H\u0002\u00a8\u0006%"}, d2 = {"ModernLocalSerialScanReportScreen", "", "navController", "Landroidx/navigation/NavController;", "viewModel", "Lcom/example/yakultscanner/viewmodels/LocalSerialScanViewModel;", "ModernPhoneRowCard", "index", "", "row", "Lcom/example/yakultscanner/ui/screens/ModernLocalPhoneRow;", "canRemove", "", "onChange", "Lkotlin/Function1;", "onRemove", "Lkotlin/Function0;", "ModernSavedSessionCard", "session", "Lcom/example/yakultscanner/data/db/LocalScanSessionWithCount;", "onOpen", "onCsv", "onPdf", "onSend", "onDelete", "modernDefaultTitle", "", "modernFormatMillis", "value", "", "modernFileStamp", "modernCsvCell", "modernLocalScanCsv", "Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;", "items", "", "Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "app_devDebug"})
public final class ModernLocalSerialScanReportScreenKt {
    
    @kotlin.OptIn(markerClass = {androidx.compose.material3.ExperimentalMaterial3Api.class})
    @androidx.compose.runtime.Composable()
    public static final void ModernLocalSerialScanReportScreen(@org.jetbrains.annotations.NotNull()
    androidx.navigation.NavController navController, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.viewmodels.LocalSerialScanViewModel viewModel) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ModernPhoneRowCard(int index, com.example.yakultscanner.ui.screens.ModernLocalPhoneRow row, boolean canRemove, kotlin.jvm.functions.Function1<? super com.example.yakultscanner.ui.screens.ModernLocalPhoneRow, kotlin.Unit> onChange, kotlin.jvm.functions.Function0<kotlin.Unit> onRemove) {
    }
    
    @androidx.compose.runtime.Composable()
    private static final void ModernSavedSessionCard(com.example.yakultscanner.data.db.LocalScanSessionWithCount session, kotlin.jvm.functions.Function0<kotlin.Unit> onOpen, kotlin.jvm.functions.Function0<kotlin.Unit> onCsv, kotlin.jvm.functions.Function0<kotlin.Unit> onPdf, kotlin.jvm.functions.Function0<kotlin.Unit> onSend, kotlin.jvm.functions.Function0<kotlin.Unit> onDelete) {
    }
    
    private static final java.lang.String modernDefaultTitle() {
        return null;
    }
    
    private static final java.lang.String modernFormatMillis(long value) {
        return null;
    }
    
    private static final java.lang.String modernFileStamp() {
        return null;
    }
    
    private static final java.lang.String modernCsvCell(java.lang.String value) {
        return null;
    }
    
    private static final java.lang.String modernLocalScanCsv(com.example.yakultscanner.data.db.LocalScanSessionEntity session, java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> items) {
        return null;
    }
}