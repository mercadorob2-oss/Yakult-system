package com.example.yakultscanner.ui.components;

import androidx.compose.foundation.layout.*;
import androidx.compose.material3.*;
import androidx.compose.runtime.*;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.text.font.FontWeight;
import androidx.compose.ui.text.style.TextOverflow;
import com.example.yakultscanner.data.model.ItemEditState;
import com.example.yakultscanner.api.SetItemUpdateDto;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000:\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0002\b\u0006\n\u0002\u0010\b\n\u0002\b\u0002\u001a\u001a\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0005H\u0007\u001a2\u0010\u0006\u001a\u00020\u00012\u0006\u0010\u0007\u001a\u00020\b2\f\u0010\t\u001a\b\u0012\u0004\u0012\u00020\u00010\n2\u0012\u0010\u000b\u001a\u000e\u0012\u0004\u0012\u00020\b\u0012\u0004\u0012\u00020\u00010\fH\u0007\u001a\u0090\u0001\u0010\r\u001a\u00020\u00012\f\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\u00010\n2\f\u0010\u000f\u001a\b\u0012\u0004\u0012\u00020\u00010\n2\f\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00010\n2\f\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00010\n2\f\u0010\u0012\u001a\b\u0012\u0004\u0012\u00020\u00010\n2\u0006\u0010\u0013\u001a\u00020\u00142\u0006\u0010\u0015\u001a\u00020\u00142\u0006\u0010\u0016\u001a\u00020\u00142\u0006\u0010\u0017\u001a\u00020\u00142\u0006\u0010\u0018\u001a\u00020\u00142\u0006\u0010\u0019\u001a\u00020\u00142\u0006\u0010\u001a\u001a\u00020\u001b2\b\b\u0002\u0010\u001c\u001a\u00020\u0014H\u0007\u00a8\u0006\u001d"}, d2 = {"ItemStatusChip", "", "status", "", "modifier", "Landroidx/compose/ui/Modifier;", "ReportIssueDialog", "editState", "Lcom/example/yakultscanner/data/model/ItemEditState;", "onDismiss", "Lkotlin/Function0;", "onSave", "Lkotlin/Function1;", "Footer", "onGeneratePdf", "onUpload", "onViewUploads", "onDeploy", "onSyncNow", "canUpload", "", "canViewUploads", "canDeploy", "canSync", "isUploading", "isSyncing", "pendingCount", "", "deployed", "app_prodDebug"})
public final class DispatchComponentsKt {
    
    @androidx.compose.runtime.Composable()
    public static final void ItemStatusChip(@org.jetbrains.annotations.NotNull()
    java.lang.String status, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void ReportIssueDialog(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.ItemEditState editState, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.data.model.ItemEditState, kotlin.Unit> onSave) {
    }
    
    @androidx.compose.runtime.Composable()
    public static final void Footer(@org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onGeneratePdf, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onUpload, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onViewUploads, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDeploy, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onSyncNow, boolean canUpload, boolean canViewUploads, boolean canDeploy, boolean canSync, boolean isUploading, boolean isSyncing, int pendingCount, boolean deployed) {
    }
}