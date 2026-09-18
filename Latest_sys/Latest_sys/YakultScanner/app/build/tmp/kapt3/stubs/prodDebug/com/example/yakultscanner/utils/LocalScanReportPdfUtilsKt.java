package com.example.yakultscanner.utils;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.pdf.PdfDocument;
import com.example.yakultscanner.data.db.LocalScanItemEntity;
import com.example.yakultscanner.data.db.LocalScanSessionEntity;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000\u001e\n\u0000\n\u0002\u0010\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\u001a$\u0010\u0000\u001a\u00020\u00012\u0006\u0010\u0002\u001a\u00020\u00032\u0006\u0010\u0004\u001a\u00020\u00052\f\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u0007\u00a8\u0006\t"}, d2 = {"generateLocalScanReportPdf", "", "context", "Landroid/content/Context;", "session", "Lcom/example/yakultscanner/data/db/LocalScanSessionEntity;", "items", "", "Lcom/example/yakultscanner/data/db/LocalScanItemEntity;", "app_prodDebug"})
public final class LocalScanReportPdfUtilsKt {
    
    public static final void generateLocalScanReportPdf(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.LocalScanSessionEntity session, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.data.db.LocalScanItemEntity> items) {
    }
}