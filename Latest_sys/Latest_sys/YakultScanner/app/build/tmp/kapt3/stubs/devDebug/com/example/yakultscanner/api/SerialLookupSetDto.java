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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u001f\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001Bq\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\u0004\b\r\u0010\u000eJ\t\u0010\u001a\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u001b\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u001c\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u001d\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u001e\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u001f\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010 \u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010!\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\"\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003Js\u0010#\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u0005H\u00c6\u0001J\u0013\u0010$\u001a\u00020%2\b\u0010&\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\'\u001a\u00020\u0003H\u00d6\u0001J\t\u0010(\u001a\u00020\u0005H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000f\u0010\u0010R\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\u0012R\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0012R\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0012R\u0018\u0010\b\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0012R\u0018\u0010\t\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0012R\u0018\u0010\n\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0012R\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0012R\u0018\u0010\f\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0012\u00a8\u0006)"}, d2 = {"Lcom/example/yakultscanner/api/SerialLookupSetDto;", "", "setId", "", "setCode", "", "status", "dispatchDate", "remarks", "site", "qrToken", "currentBranch", "currentDepartment", "<init>", "(ILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", "getSetId", "()I", "getSetCode", "()Ljava/lang/String;", "getStatus", "getDispatchDate", "getRemarks", "getSite", "getQrToken", "getCurrentBranch", "getCurrentDepartment", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "copy", "equals", "", "other", "hashCode", "toString", "app_devDebug"})
public final class SerialLookupSetDto {
    @com.google.gson.annotations.SerializedName(value = "setId")
    private final int setId = 0;
    @com.google.gson.annotations.SerializedName(value = "setCode")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String setCode = null;
    @com.google.gson.annotations.SerializedName(value = "status")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String status = null;
    @com.google.gson.annotations.SerializedName(value = "dispatchDate")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String dispatchDate = null;
    @com.google.gson.annotations.SerializedName(value = "remarks")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String remarks = null;
    @com.google.gson.annotations.SerializedName(value = "site")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String site = null;
    @com.google.gson.annotations.SerializedName(value = "qrToken")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String qrToken = null;
    @com.google.gson.annotations.SerializedName(value = "currentBranch")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String currentBranch = null;
    @com.google.gson.annotations.SerializedName(value = "currentDepartment")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String currentDepartment = null;
    
    public SerialLookupSetDto(int setId, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    java.lang.String dispatchDate, @org.jetbrains.annotations.Nullable()
    java.lang.String remarks, @org.jetbrains.annotations.Nullable()
    java.lang.String site, @org.jetbrains.annotations.Nullable()
    java.lang.String qrToken, @org.jetbrains.annotations.Nullable()
    java.lang.String currentBranch, @org.jetbrains.annotations.Nullable()
    java.lang.String currentDepartment) {
        super();
    }
    
    public final int getSetId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSetCode() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDispatchDate() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getRemarks() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSite() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getQrToken() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCurrentBranch() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCurrentDepartment() {
        return null;
    }
    
    public SerialLookupSetDto() {
        super();
    }
    
    public final int component1() {
        return 0;
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
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component8() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.SerialLookupSetDto copy(int setId, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    java.lang.String dispatchDate, @org.jetbrains.annotations.Nullable()
    java.lang.String remarks, @org.jetbrains.annotations.Nullable()
    java.lang.String site, @org.jetbrains.annotations.Nullable()
    java.lang.String qrToken, @org.jetbrains.annotations.Nullable()
    java.lang.String currentBranch, @org.jetbrains.annotations.Nullable()
    java.lang.String currentDepartment) {
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