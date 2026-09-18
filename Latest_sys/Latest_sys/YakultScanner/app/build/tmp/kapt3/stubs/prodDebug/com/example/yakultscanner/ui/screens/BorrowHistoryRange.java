package com.example.yakultscanner.ui.screens;

import android.app.DatePickerDialog;
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
import com.example.yakultscanner.api.BorrowAccessDto;
import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.BorrowDeleteRequest;
import com.example.yakultscanner.api.BorrowLogDto;
import com.example.yakultscanner.api.BorrowLogPageResponse;
import com.example.yakultscanner.utils.TextExportUtils;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.time.temporal.ChronoUnit;
import java.util.Calendar;
import java.util.Locale;
import java.util.UUID;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\f\n\u0002\u0018\u0002\n\u0002\u0010\u0010\n\u0002\b\b\b\u0082\u0081\u0002\u0018\u00002\b\u0012\u0004\u0012\u00020\u00000\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003j\u0002\b\u0004j\u0002\b\u0005j\u0002\b\u0006j\u0002\b\u0007j\u0002\b\b\u00a8\u0006\t"}, d2 = {"Lcom/example/yakultscanner/ui/screens/BorrowHistoryRange;", "", "<init>", "(Ljava/lang/String;I)V", "ALL", "TODAY", "LAST_7_DAYS", "LAST_30_DAYS", "CUSTOM", "app_prodDebug"})
enum BorrowHistoryRange {
    /*public static final*/ ALL /* = new ALL() */,
    /*public static final*/ TODAY /* = new TODAY() */,
    /*public static final*/ LAST_7_DAYS /* = new LAST_7_DAYS() */,
    /*public static final*/ LAST_30_DAYS /* = new LAST_30_DAYS() */,
    /*public static final*/ CUSTOM /* = new CUSTOM() */;
    
    BorrowHistoryRange() {
    }
    
    @org.jetbrains.annotations.NotNull()
    public static kotlin.enums.EnumEntries<com.example.yakultscanner.ui.screens.BorrowHistoryRange> getEntries() {
        return null;
    }
}