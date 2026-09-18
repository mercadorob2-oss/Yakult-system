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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\u0005\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005\u00a8\u0006\u0006"}, d2 = {"Lcom/example/yakultscanner/ui/screens/EmployeeEntryMode;", "", "<init>", "(Ljava/lang/String;I)V", "LISTED", "ADD_NEW", "app_devDebug"})
enum EmployeeEntryMode {
    /*public static final*/ LISTED /* = new LISTED() */,
    /*public static final*/ ADD_NEW /* = new ADD_NEW() */;
    
    EmployeeEntryMode() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.ui.screens.EmployeeEntryMode> getEntries() {
        return null;
    }
}