package com.example.yakultscanner.ui.screens;

import android.app.DatePickerDialog;
import android.app.TimePickerDialog;
import androidx.compose.foundation.layout.Arrangement;
import androidx.compose.material.icons.Icons;
import androidx.compose.material3.CardDefaults;
import androidx.compose.material3.ExperimentalMaterial3Api;
import androidx.compose.material3.SnackbarHostState;
import androidx.compose.material3.TopAppBarDefaults;
import androidx.compose.runtime.Composable;
import androidx.compose.ui.Alignment;
import androidx.compose.ui.Modifier;
import androidx.compose.ui.graphics.Brush;
import androidx.compose.ui.text.font.FontWeight;
import androidx.navigation.NavController;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.BorrowBranchDto;
import com.example.yakultscanner.api.BorrowCompanyDto;
import com.example.yakultscanner.api.BorrowCreateRequest;
import com.example.yakultscanner.api.BorrowDepartmentDto;
import com.example.yakultscanner.api.BorrowEmployeeDto;
import com.example.yakultscanner.api.BorrowEmployeeCreateRequest;
import com.example.yakultscanner.api.BorrowItemDto;
import com.example.yakultscanner.api.BorrowLogDto;
import com.example.yakultscanner.api.BorrowResolveResponse;
import com.example.yakultscanner.api.BorrowReturnRequest;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.Calendar;
import java.util.Locale;
import java.util.UUID;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b%\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0002\b\u0082\b\u0018\u00002\u00020\u0001Bc\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u0012\u0006\u0010\u0005\u001a\u00020\u0003\u0012\u0006\u0010\u0006\u001a\u00020\u0003\u0012\u0006\u0010\u0007\u001a\u00020\u0003\u0012\u0006\u0010\b\u001a\u00020\u0003\u0012\u0006\u0010\t\u001a\u00020\u0003\u0012\u0006\u0010\n\u001a\u00020\u0003\u0012\u0006\u0010\u000b\u001a\u00020\u0003\u0012\u0006\u0010\f\u001a\u00020\u0003\u0012\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\u0004\b\u000e\u0010\u000fJ\t\u0010\u001c\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001d\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001e\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001f\u001a\u00020\u0003H\u00c6\u0003J\t\u0010 \u001a\u00020\u0003H\u00c6\u0003J\t\u0010!\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\"\u001a\u00020\u0003H\u00c6\u0003J\t\u0010#\u001a\u00020\u0003H\u00c6\u0003J\t\u0010$\u001a\u00020\u0003H\u00c6\u0003J\t\u0010%\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010&\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003Jy\u0010\'\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00032\b\b\u0002\u0010\u0006\u001a\u00020\u00032\b\b\u0002\u0010\u0007\u001a\u00020\u00032\b\b\u0002\u0010\b\u001a\u00020\u00032\b\b\u0002\u0010\t\u001a\u00020\u00032\b\b\u0002\u0010\n\u001a\u00020\u00032\b\b\u0002\u0010\u000b\u001a\u00020\u00032\b\b\u0002\u0010\f\u001a\u00020\u00032\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u0003H\u00c6\u0001J\u0013\u0010(\u001a\u00020)2\b\u0010*\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010+\u001a\u00020,H\u00d6\u0001J\t\u0010-\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u0011R\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u0011R\u0011\u0010\u0005\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0011R\u0011\u0010\u0006\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0011R\u0011\u0010\u0007\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0011R\u0011\u0010\b\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0011R\u0011\u0010\t\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0011R\u0011\u0010\n\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0011R\u0011\u0010\u000b\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0011R\u0011\u0010\f\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0011R\u0013\u0010\r\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u0011\u00a8\u0006."}, d2 = {"Lcom/example/yakultscanner/ui/screens/BorrowConfirmationPreview;", "", "itemName", "", "itemDescription", "modelNumber", "serialNumber", "borrowerName", "companyName", "borrowerDepartment", "encodedBy", "timestampLabel", "borrowedAtLabel", "note", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", "getItemName", "()Ljava/lang/String;", "getItemDescription", "getModelNumber", "getSerialNumber", "getBorrowerName", "getCompanyName", "getBorrowerDepartment", "getEncodedBy", "getTimestampLabel", "getBorrowedAtLabel", "getNote", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "copy", "equals", "", "other", "hashCode", "", "toString", "app_prodDebug"})
final class BorrowConfirmationPreview {
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String itemName = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String itemDescription = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String modelNumber = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String serialNumber = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String borrowerName = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String companyName = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String borrowerDepartment = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String encodedBy = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String timestampLabel = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String borrowedAtLabel = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String note = null;
    
    public BorrowConfirmationPreview(@org.jetbrains.annotations.NotNull()
    java.lang.String itemName, @org.jetbrains.annotations.NotNull()
    java.lang.String itemDescription, @org.jetbrains.annotations.NotNull()
    java.lang.String modelNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String borrowerName, @org.jetbrains.annotations.NotNull()
    java.lang.String companyName, @org.jetbrains.annotations.NotNull()
    java.lang.String borrowerDepartment, @org.jetbrains.annotations.NotNull()
    java.lang.String encodedBy, @org.jetbrains.annotations.NotNull()
    java.lang.String timestampLabel, @org.jetbrains.annotations.NotNull()
    java.lang.String borrowedAtLabel, @org.jetbrains.annotations.Nullable()
    java.lang.String note) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getItemName() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getItemDescription() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getModelNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getBorrowerName() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getCompanyName() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getBorrowerDepartment() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getEncodedBy() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getTimestampLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getBorrowedAtLabel() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getNote() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component10() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component11() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component5() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.ui.screens.BorrowConfirmationPreview copy(@org.jetbrains.annotations.NotNull()
    java.lang.String itemName, @org.jetbrains.annotations.NotNull()
    java.lang.String itemDescription, @org.jetbrains.annotations.NotNull()
    java.lang.String modelNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String borrowerName, @org.jetbrains.annotations.NotNull()
    java.lang.String companyName, @org.jetbrains.annotations.NotNull()
    java.lang.String borrowerDepartment, @org.jetbrains.annotations.NotNull()
    java.lang.String encodedBy, @org.jetbrains.annotations.NotNull()
    java.lang.String timestampLabel, @org.jetbrains.annotations.NotNull()
    java.lang.String borrowedAtLabel, @org.jetbrains.annotations.Nullable()
    java.lang.String note) {
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