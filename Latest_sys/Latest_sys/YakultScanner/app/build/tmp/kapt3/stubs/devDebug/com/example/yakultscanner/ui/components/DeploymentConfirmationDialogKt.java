package com.example.yakultscanner.ui.components;

import android.graphics.Bitmap;
import android.util.Base64;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.compose.foundation.layout.*;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.*;
import androidx.compose.runtime.*;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.layout.ContentScale;
import androidx.compose.ui.text.font.FontWeight;
import com.example.yakultscanner.api.SetConfirmationRequest;
import java.io.ByteArrayOutputStream;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u00006\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\u001aD\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00032\f\u0010\u0005\u001a\b\u0012\u0004\u0012\u00020\u00010\u00062\u0012\u0010\u0007\u001a\u000e\u0012\u0004\u0012\u00020\t\u0012\u0004\u0012\u00020\u00010\b2\b\b\u0002\u0010\n\u001a\u00020\u000bH\u0007\u001a$\u0010\f\u001a\u00020\t2\b\u0010\r\u001a\u0004\u0018\u00010\u000e2\b\u0010\u000f\u001a\u0004\u0018\u00010\u00102\u0006\u0010\u0011\u001a\u00020\u0003H\u0002\u00a8\u0006\u0012"}, d2 = {"DeploymentConfirmationDialog", "", "setCode", "", "employeeName", "onDismiss", "Lkotlin/Function0;", "onConfirm", "Lkotlin/Function1;", "Lcom/example/yakultscanner/api/SetConfirmationRequest;", "modifier", "Landroidx/compose/ui/Modifier;", "createConfirmationRequest", "signatureView", "Lcom/example/yakultscanner/ui/components/SignatureView;", "photoBitmap", "Landroid/graphics/Bitmap;", "notes", "app_devDebug"})
public final class DeploymentConfirmationDialogKt {
    
    @androidx.compose.runtime.Composable()
    public static final void DeploymentConfirmationDialog(@org.jetbrains.annotations.NotNull()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    java.lang.String employeeName, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function0<kotlin.Unit> onDismiss, @org.jetbrains.annotations.NotNull()
    kotlin.jvm.functions.Function1<? super com.example.yakultscanner.api.SetConfirmationRequest, kotlin.Unit> onConfirm, @org.jetbrains.annotations.NotNull()
    androidx.compose.ui.Modifier modifier) {
    }
    
    private static final com.example.yakultscanner.api.SetConfirmationRequest createConfirmationRequest(com.example.yakultscanner.ui.components.SignatureView signatureView, android.graphics.Bitmap photoBitmap, java.lang.String notes) {
        return null;
    }
}