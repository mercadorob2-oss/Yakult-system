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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\b\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\b2\u0018\u00002\u00020\u0001:\u0006\u0004\u0005\u0006\u0007\b\tB\t\b\u0004\u00a2\u0006\u0004\b\u0002\u0010\u0003\u0082\u0001\u0006\n\u000b\f\r\u000e\u000f\u00a8\u0006\u0010"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState;", "", "<init>", "()V", "Idle", "Loading", "NotFound", "Error", "FoundNoSet", "FoundInSet", "Lcom/example/yakultscanner/ui/screens/LookupState$Error;", "Lcom/example/yakultscanner/ui/screens/LookupState$FoundInSet;", "Lcom/example/yakultscanner/ui/screens/LookupState$FoundNoSet;", "Lcom/example/yakultscanner/ui/screens/LookupState$Idle;", "Lcom/example/yakultscanner/ui/screens/LookupState$Loading;", "Lcom/example/yakultscanner/ui/screens/LookupState$NotFound;", "app_prodDebug"})
abstract class LookupState {
    
    private LookupState() {
        super();
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000&\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0011"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState$Error;", "Lcom/example/yakultscanner/ui/screens/LookupState;", "message", "", "<init>", "(Ljava/lang/String;)V", "getMessage", "()Ljava/lang/String;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "app_prodDebug"})
    public static final class Error extends com.example.yakultscanner.ui.screens.LookupState {
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
        public final com.example.yakultscanner.ui.screens.LookupState.Error copy(@org.jetbrains.annotations.NotNull()
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00004\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\n\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u001d\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\f\u0010\u0004\u001a\b\u0012\u0004\u0012\u00020\u00060\u0005\u00a2\u0006\u0004\b\u0007\u0010\bJ\t\u0010\r\u001a\u00020\u0003H\u00c6\u0003J\u000f\u0010\u000e\u001a\b\u0012\u0004\u0012\u00020\u00060\u0005H\u00c6\u0003J#\u0010\u000f\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\u000e\b\u0002\u0010\u0004\u001a\b\u0012\u0004\u0012\u00020\u00060\u0005H\u00c6\u0001J\u0013\u0010\u0010\u001a\u00020\u00112\b\u0010\u0012\u001a\u0004\u0018\u00010\u0013H\u00d6\u0003J\t\u0010\u0014\u001a\u00020\u0015H\u00d6\u0001J\t\u0010\u0016\u001a\u00020\u0017H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\nR\u0017\u0010\u0004\u001a\b\u0012\u0004\u0012\u00020\u00060\u0005\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000b\u0010\f\u00a8\u0006\u0018"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState$FoundInSet;", "Lcom/example/yakultscanner/ui/screens/LookupState;", "item", "Lcom/example/yakultscanner/api/SerialLookupItemDto;", "sets", "", "Lcom/example/yakultscanner/api/SerialLookupSetDto;", "<init>", "(Lcom/example/yakultscanner/api/SerialLookupItemDto;Ljava/util/List;)V", "getItem", "()Lcom/example/yakultscanner/api/SerialLookupItemDto;", "getSets", "()Ljava/util/List;", "component1", "component2", "copy", "equals", "", "other", "", "hashCode", "", "toString", "", "app_prodDebug"})
    public static final class FoundInSet extends com.example.yakultscanner.ui.screens.LookupState {
        @org.jetbrains.annotations.NotNull()
        private final com.example.yakultscanner.api.SerialLookupItemDto item = null;
        @org.jetbrains.annotations.NotNull()
        private final java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets = null;
        
        public FoundInSet(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.api.SerialLookupItemDto item, @org.jetbrains.annotations.NotNull()
        java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.api.SerialLookupItemDto getItem() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> getSets() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.api.SerialLookupItemDto component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> component2() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.ui.screens.LookupState.FoundInSet copy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.api.SerialLookupItemDto item, @org.jetbrains.annotations.NotNull()
        java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets) {
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000*\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0007\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u000f\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\t\u0010\b\u001a\u00020\u0003H\u00c6\u0003J\u0013\u0010\t\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\n\u001a\u00020\u000b2\b\u0010\f\u001a\u0004\u0018\u00010\rH\u00d6\u0003J\t\u0010\u000e\u001a\u00020\u000fH\u00d6\u0001J\t\u0010\u0010\u001a\u00020\u0011H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState$FoundNoSet;", "Lcom/example/yakultscanner/ui/screens/LookupState;", "item", "Lcom/example/yakultscanner/api/SerialLookupItemDto;", "<init>", "(Lcom/example/yakultscanner/api/SerialLookupItemDto;)V", "getItem", "()Lcom/example/yakultscanner/api/SerialLookupItemDto;", "component1", "copy", "equals", "", "other", "", "hashCode", "", "toString", "", "app_prodDebug"})
    public static final class FoundNoSet extends com.example.yakultscanner.ui.screens.LookupState {
        @org.jetbrains.annotations.NotNull()
        private final com.example.yakultscanner.api.SerialLookupItemDto item = null;
        
        public FoundNoSet(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.api.SerialLookupItemDto item) {
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.api.SerialLookupItemDto getItem() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.api.SerialLookupItemDto component1() {
            return null;
        }
        
        @org.jetbrains.annotations.NotNull()
        public final com.example.yakultscanner.ui.screens.LookupState.FoundNoSet copy(@org.jetbrains.annotations.NotNull()
        com.example.yakultscanner.api.SerialLookupItemDto item) {
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
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState$Idle;", "Lcom/example/yakultscanner/ui/screens/LookupState;", "<init>", "()V", "app_prodDebug"})
    public static final class Idle extends com.example.yakultscanner.ui.screens.LookupState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.screens.LookupState.Idle INSTANCE = null;
        
        private Idle() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState$Loading;", "Lcom/example/yakultscanner/ui/screens/LookupState;", "<init>", "()V", "app_prodDebug"})
    public static final class Loading extends com.example.yakultscanner.ui.screens.LookupState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.screens.LookupState.Loading INSTANCE = null;
        
        private Loading() {
        }
    }
    
    @kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003\u00a8\u0006\u0004"}, d2 = {"Lcom/example/yakultscanner/ui/screens/LookupState$NotFound;", "Lcom/example/yakultscanner/ui/screens/LookupState;", "<init>", "()V", "app_prodDebug"})
    public static final class NotFound extends com.example.yakultscanner.ui.screens.LookupState {
        @org.jetbrains.annotations.NotNull()
        public static final com.example.yakultscanner.ui.screens.LookupState.NotFound INSTANCE = null;
        
        private NotFound() {
        }
    }
}