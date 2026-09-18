package com.example.yakultscanner.utils;

import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.pdf.PdfDocument;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.widget.Toast;
import androidx.core.content.FileProvider;
import com.example.yakultscanner.api.BorrowLogDto;
import com.example.yakultscanner.api.CallTicketDetail;
import com.example.yakultscanner.api.CallTicketListItem;
import com.example.yakultscanner.api.CallTicketHistoryDto;
import com.example.yakultscanner.api.CallTicketNoteDto;
import com.example.yakultscanner.api.ReportListRowDto;
import com.example.yakultscanner.api.ReportsSummaryResponse;
import com.example.yakultscanner.api.SetItemUpdateDto;
import com.example.yakultscanner.data.db.PendingUpdateEntity;
import com.example.yakultscanner.data.model.DispatchSet;
import java.io.File;
import java.io.FileOutputStream;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000`\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\u001a*\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\b\u0010\u0006\u001a\u0004\u0018\u00010\u00072\b\u0010\b\u001a\u0004\u0018\u00010\u0007\u001a \u0010\t\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\rH\u0000\u001a\u001e\u0010\u000e\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u000f\u001a\u00020\u00102\u0006\u0010\u0011\u001a\u00020\r\u001a2\u0010\u0012\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0013\u001a\u00020\u00142\f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\u00170\u00162\f\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00190\u0016\u001a\u001c\u0010\u001a\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\f\u0010\u001b\u001a\b\u0012\u0004\u0012\u00020\u001c0\u0016\u001a$\u0010\u001d\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\f\u0010\u001e\u001a\b\u0012\u0004\u0012\u00020\u001f0\u00162\u0006\u0010 \u001a\u00020\r\u001a\u001c\u0010!\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\f\u0010\u001e\u001a\b\u0012\u0004\u0012\u00020\"0\u0016\u00a8\u0006#"}, d2 = {"generateAndOpenPdf", "", "context", "Landroid/content/Context;", "set", "Lcom/example/yakultscanner/data/model/DispatchSet;", "dispatcherBitmap", "Landroid/graphics/Bitmap;", "requesterBitmap", "savePdfAndOpen", "document", "Landroid/graphics/pdf/PdfDocument;", "fileName", "", "generateReportPdf", "summary", "Lcom/example/yakultscanner/api/ReportsSummaryResponse;", "rangeLabel", "generateTicketPdf", "ticket", "Lcom/example/yakultscanner/api/CallTicketDetail;", "notes", "", "Lcom/example/yakultscanner/api/CallTicketNoteDto;", "history", "Lcom/example/yakultscanner/api/CallTicketHistoryDto;", "generateProcessedUpdatesPdf", "updates", "Lcom/example/yakultscanner/api/SetItemUpdateDto;", "generateBorrowRecordsPdf", "records", "Lcom/example/yakultscanner/api/BorrowLogDto;", "tabTitle", "generatePendingUpdatesPdf", "Lcom/example/yakultscanner/data/db/PendingUpdateEntity;", "app_devDebug"})
public final class PdfUtilsKt {
    
    public static final void generateAndOpenPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.DispatchSet set, @org.jetbrains.annotations.Nullable()
    android.graphics.Bitmap dispatcherBitmap, @org.jetbrains.annotations.Nullable()
    android.graphics.Bitmap requesterBitmap) {
    }
    
    public static final void savePdfAndOpen(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    android.graphics.pdf.PdfDocument document, @org.jetbrains.annotations.NotNull()
    java.lang.String fileName) {
    }
    
    public static final void generateReportPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ReportsSummaryResponse summary, @org.jetbrains.annotations.NotNull()
    java.lang.String rangeLabel) {
    }
    
    public static final void generateTicketPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.CallTicketDetail ticket, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.CallTicketNoteDto> notes, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.CallTicketHistoryDto> history) {
    }
    
    public static final void generateProcessedUpdatesPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.SetItemUpdateDto> updates) {
    }
    
    public static final void generateBorrowRecordsPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BorrowLogDto> records, @org.jetbrains.annotations.NotNull()
    java.lang.String tabTitle) {
    }
    
    public static final void generatePendingUpdatesPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.PendingUpdateEntity> records) {
    }
}