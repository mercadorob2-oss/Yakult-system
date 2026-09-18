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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\b\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b6\u0018\u00002\u00020\u0001:\u0006\u0004\u0005\u0006\u0007\b\tB\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0006\n\u000b\f\r\u000e\u000f\u00a8\u0006\u0010"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "", "<init>", "()V", "Review", "Signature", "Photo", "Confirming", "Success", "Error", "Lcom/example/yakultscanner/ui/components/ConfirmationStep$Confirming;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep$Error;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep$Photo;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep$Review;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep$Signature;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep$Success;", "app_devDebug"})
public abstract class ConfirmationStep {
    
    private ConfirmationStep() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep$Confirming;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "<init>", "()V", "app_devDebug"})
    public static final class Confirming extends com.example.yakultscanner.ui.components.ConfirmationStep {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.components.ConfirmationStep.Confirming INSTANCE = null;
        
        private Confirming() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep$Error;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "message", "", "<init>", "(Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Error extends com.example.yakultscanner.ui.components.ConfirmationStep {
        @org.jetbrains.annotations.NotNull()
        private final java.lang.String message = null;
        
        public Error(@org.jetbrains.annotations.NotNull()
        java.lang.String message) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String getMessage() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.ui.components.ConfirmationStep.Error copy(@org.jetbrains.annotations.NotNull()
        java.lang.String message) {
            return null;
        }
        
        @java.lang.Override()
        public boolean equals(@org.jetbrains.annotations.Nullable()
        java.lang.Object other) {
            return false;
        }
        
        @java.lang.Override()
        public int hashCode() {
            return 0;
        }
        
        @java.lang.Override()
        @org.jetbrains.annotations.NotNull()
        public java.lang.String toString() {
            return null;
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep$Photo;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "<init>", "()V", "app_devDebug"})
    public static final class Photo extends com.example.yakultscanner.ui.components.ConfirmationStep {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.components.ConfirmationStep.Photo INSTANCE = null;
        
        private Photo() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep$Review;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "<init>", "()V", "app_devDebug"})
    public static final class Review extends com.example.yakultscanner.ui.components.ConfirmationStep {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.components.ConfirmationStep.Review INSTANCE = null;
        
        private Review() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep$Signature;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "<init>", "()V", "app_devDebug"})
    public static final class Signature extends com.example.yakultscanner.ui.components.ConfirmationStep {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.components.ConfirmationStep.Signature INSTANCE = null;
        
        private Signature() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/ui/components/ConfirmationStep$Success;", "Lcom/example/yakultscanner/ui/components/ConfirmationStep;", "confirmationId", "", "<init>", "(Ljava/lang/String;)V", "getConfirmationId", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_devDebug"})
    public static final class Success extends com.example.yakultscanner.ui.components.ConfirmationStep {
        @org.jetbrains.annotations.NotNull()
        private final java.lang.String confirmationId = null;
        
        public Success(@org.jetbrains.annotations.NotNull()
        java.lang.String confirmationId) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String getConfirmationId() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.lang.String component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.ui.components.ConfirmationStep.Success copy(@org.jetbrains.annotations.NotNull()
        java.lang.String confirmationId) {
            return null;
        }
        
        @java.lang.Override()
        public boolean equals(@org.jetbrains.annotations.Nullable()
        java.lang.Object other) {
            return false;
        }
        
        @java.lang.Override()
        public int hashCode() {
            return 0;
        }
        
        @java.lang.Override()
        @org.jetbrains.annotations.NotNull()
        public java.lang.String toString() {
            return null;
        }
    }
}