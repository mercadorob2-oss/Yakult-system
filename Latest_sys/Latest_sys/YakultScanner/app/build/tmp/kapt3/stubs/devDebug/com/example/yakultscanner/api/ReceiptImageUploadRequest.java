package com.example.yakultscanner.api;

import android.os.Handler;
import android.os.Looper;
import com.example.yakultscanner.BuildConfig;
import com.example.yakultscanner.GlobalNav;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.data.model.DispatchSet;
import com.example.yakultscanner.settings.ApiSettings;
import com.google.gson.annotations.SerializedName;
import okhttp3.OkHttpClient;
import okhttp3.logging.HttpLoggingInterceptor;
import retrofit2.Retrofit;
import retrofit2.converter.gson.GsonConverterFactory;
import retrofit2.http.Body;
import retrofit2.http.GET;
import retrofit2.http.POST;
import retrofit2.http.PUT;
import retrofit2.http.Path;
import retrofit2.http.Query;
import java.util.concurrent.TimeUnit;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0006\n\u0002\u0010\b\n\u0002\b\u0019\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001B_\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0003\u0012\u0006\u0010\b\u001a\u00020\u0003\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\u0004\b\f\u0010\rJ\t\u0010\u0019\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u001a\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001b\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001c\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\u001d\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\t\u0010\u001e\u001a\u00020\u0003H\u00c6\u0003J\u0010\u0010\u001f\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010\u0016J\u000b\u0010 \u001a\u0004\u0018\u00010\u0003H\u00c6\u0003Jj\u0010!\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\b\u001a\u00020\u00032\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u0003H\u00c6\u0001\u00a2\u0006\u0002\u0010\"J\u0013\u0010#\u001a\u00020$2\b\u0010%\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010&\u001a\u00020\nH\u00d6\u0001J\t\u0010\'\u001a\u00020\u0003H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000e\u0010\u000fR\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u000fR\u0018\u0010\u0005\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\u000fR\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u000fR\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u000fR\u0016\u0010\b\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u000fR\u001a\u0010\t\u001a\u0004\u0018\u00010\n8\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0017\u001a\u0004\b\u0015\u0010\u0016R\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u000f\u00a8\u0006("}, d2 = {"Lcom/example/yakultscanner/api/ReceiptImageUploadRequest;", "", "docType", "", "supplier", "siNumber", "drNumber", "poNumber", "imageBase64", "receiptSetId", "", "mimeType", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;)V", "getDocType", "()Ljava/lang/String;", "getSupplier", "getSiNumber", "getDrNumber", "getPoNumber", "getImageBase64", "getReceiptSetId", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getMimeType", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "copy", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;)Lcom/example/yakultscanner/api/ReceiptImageUploadRequest;", "equals", "", "other", "hashCode", "toString", "app_devDebug"})
public final class ReceiptImageUploadRequest {
    @com.google.gson.annotations.SerializedName(value = "doc_type")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String docType = null;
    @com.google.gson.annotations.SerializedName(value = "supplier")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String supplier = null;
    @com.google.gson.annotations.SerializedName(value = "si_number")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String siNumber = null;
    @com.google.gson.annotations.SerializedName(value = "dr_number")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String drNumber = null;
    @com.google.gson.annotations.SerializedName(value = "po_number")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String poNumber = null;
    @com.google.gson.annotations.SerializedName(value = "image_base64")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String imageBase64 = null;
    @com.google.gson.annotations.SerializedName(value = "receipt_set_id")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer receiptSetId = null;
    @com.google.gson.annotations.SerializedName(value = "mime_type")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String mimeType = null;
    
    public ReceiptImageUploadRequest(@org.jetbrains.annotations.NotNull()
    java.lang.String docType, @org.jetbrains.annotations.Nullable()
    java.lang.String supplier, @org.jetbrains.annotations.Nullable()
    java.lang.String siNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String drNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String poNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String imageBase64, @org.jetbrains.annotations.Nullable()
    java.lang.Integer receiptSetId, @org.jetbrains.annotations.Nullable()
    java.lang.String mimeType) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getDocType() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSupplier() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSiNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDrNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getPoNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getImageBase64() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getReceiptSetId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getMimeType() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component5() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component7() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.ReceiptImageUploadRequest copy(@org.jetbrains.annotations.NotNull()
    java.lang.String docType, @org.jetbrains.annotations.Nullable()
    java.lang.String supplier, @org.jetbrains.annotations.Nullable()
    java.lang.String siNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String drNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String poNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String imageBase64, @org.jetbrains.annotations.Nullable()
    java.lang.Integer receiptSetId, @org.jetbrains.annotations.Nullable()
    java.lang.String mimeType) {
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