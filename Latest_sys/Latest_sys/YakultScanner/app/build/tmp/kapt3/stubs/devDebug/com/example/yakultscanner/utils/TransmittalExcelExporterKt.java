package com.example.yakultscanner.utils;

import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import androidx.core.content.FileProvider;
import com.example.yakultscanner.data.model.TransmittalReport;
import kotlinx.coroutines.Dispatchers;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;
import java.util.zip.ZipOutputStream;

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u00002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\u0012\n\u0002\b\u0016\u001a$\u0010\u0003\u001a\b\u0012\u0004\u0012\u00020\u00050\u00042\u0006\u0010\u0006\u001a\u00020\u00072\u0006\u0010\b\u001a\u00020\tH\u0086@\u00a2\u0006\u0002\u0010\n\u001a\u0016\u0010\u000b\u001a\u00020\f2\u0006\u0010\u0006\u001a\u00020\u00072\u0006\u0010\r\u001a\u00020\u0005\u001a\u0018\u0010\u000e\u001a\u00020\u000f2\u0006\u0010\u0010\u001a\u00020\u000f2\u0006\u0010\b\u001a\u00020\tH\u0002\u001a\u0010\u0010\u0014\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\u0001H\u0002\u001a\u0010\u0010\u0016\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\u0001H\u0002\u001a\u0010\u0010\u0017\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\u0001H\u0002\u001a\u0010\u0010\u0018\u001a\u00020\u00012\u0006\u0010\b\u001a\u00020\tH\u0002\u001a$\u0010\u0019\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\u00012\u0006\u0010\u001b\u001a\u00020\u00012\n\b\u0002\u0010\u001c\u001a\u0004\u0018\u00010\u0001H\u0002\u001a\u0010\u0010\u001d\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\u0001H\u0002\u001a\u0010\u0010\u001e\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\u0001H\u0002\u001a \u0010\u001f\u001a\u00020\u00012\u0006\u0010\u0015\u001a\u00020\u00012\u0006\u0010\u001a\u001a\u00020\u00012\u0006\u0010\u001b\u001a\u00020\u0001H\u0002\u001a\u0010\u0010 \u001a\u00020\u00012\u0006\u0010\u001b\u001a\u00020\u0001H\u0002\u001a \u0010!\u001a\u00020\u00052\u0006\u0010\u0006\u001a\u00020\u00072\u0006\u0010\"\u001a\u00020\u00012\u0006\u0010#\u001a\u00020\u000fH\u0002\u001a\b\u0010$\u001a\u00020\u0001H\u0002\"\u000e\u0010\u0000\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0002\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0011\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0012\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\"\u000e\u0010\u0013\u001a\u00020\u0001X\u0082T\u00a2\u0006\u0002\n\u0000\u00a8\u0006%"}, d2 = {"TEMPLATE_ASSET", "", "EXCEL_MIME", "exportTransmittalWorkbook", "Lkotlin/Result;", "Lcom/example/yakultscanner/utils/SavedTransmittalWorkbook;", "context", "Landroid/content/Context;", "report", "Lcom/example/yakultscanner/data/model/TransmittalReport;", "(Landroid/content/Context;Lcom/example/yakultscanner/data/model/TransmittalReport;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "openTransmittalWorkbook", "", "workbook", "populateTemplate", "", "templateBytes", "DETAILS_SHEET_NAME", "DETAILS_SHEET_PATH", "DETAILS_RELATIONSHIP_ID", "addDetailsWorksheetToWorkbook", "xml", "addDetailsWorksheetRelationship", "addDetailsWorksheetContentType", "buildDetailsSheet", "inlineCell", "reference", "value", "style", "normalizeExportView", "removeTemplateHighlightFill", "replaceCell", "escapeXml", "saveWorkbook", "fileName", "bytes", "fileStamp", "app_devDebug"})
public final class TransmittalExcelExporterKt {
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String TEMPLATE_ASSET = "transmittal_template.xlsx";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String EXCEL_MIME = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String DETAILS_SHEET_NAME = "Scanned Details";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String DETAILS_SHEET_PATH = "xl/worksheets/sheet3.xml";
    @org.jetbrains.annotations.NotNull()
    private static final java.lang.String DETAILS_RELATIONSHIP_ID = "rId7";
    
    @org.jetbrains.annotations.Nullable()
    public static final java.lang.Object exportTransmittalWorkbook(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.model.TransmittalReport report, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super kotlin.Result<com.example.yakultscanner.utils.SavedTransmittalWorkbook>> $completion) {
        return null;
    }
    
    public static final boolean openTransmittalWorkbook(@org.jetbrains.annotations.NotNull()
    android.content.Context context, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.utils.SavedTransmittalWorkbook workbook) {
        return false;
    }
    
    private static final byte[] populateTemplate(byte[] templateBytes, com.example.yakultscanner.data.model.TransmittalReport report) {
        return null;
    }
    
    private static final java.lang.String addDetailsWorksheetToWorkbook(java.lang.String xml) {
        return null;
    }
    
    private static final java.lang.String addDetailsWorksheetRelationship(java.lang.String xml) {
        return null;
    }
    
    private static final java.lang.String addDetailsWorksheetContentType(java.lang.String xml) {
        return null;
    }
    
    private static final java.lang.String buildDetailsSheet(com.example.yakultscanner.data.model.TransmittalReport report) {
        return null;
    }
    
    private static final java.lang.String inlineCell(java.lang.String reference, java.lang.String value, java.lang.String style) {
        return null;
    }
    
    private static final java.lang.String normalizeExportView(java.lang.String xml) {
        return null;
    }
    
    private static final java.lang.String removeTemplateHighlightFill(java.lang.String xml) {
        return null;
    }
    
    private static final java.lang.String replaceCell(java.lang.String xml, java.lang.String reference, java.lang.String value) {
        return null;
    }
    
    private static final java.lang.String escapeXml(java.lang.String value) {
        return null;
    }
    
    private static final com.example.yakultscanner.utils.SavedTransmittalWorkbook saveWorkbook(android.content.Context context, java.lang.String fileName, byte[] bytes) {
        return null;
    }
    
    private static final java.lang.String fileStamp() {
        return null;
    }
}