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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0016\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001BM\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\u0004\b\n\u0010\u000bJ\t\u0010\u0014\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u0015\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u0016\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u0017\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u0018\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u0019\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003JO\u0010\u001a\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0005H\u00c6\u0001J\u0013\u0010\u001b\u001a\u00020\u001c2\b\u0010\u001d\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u001e\u001a\u00020\u0003H\u00d6\u0001J\t\u0010\u001f\u001a\u00020\u0005H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\f\u0010\rR\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000e\u0010\u000fR\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u000fR\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\u000fR\u0018\u0010\b\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u000fR\u0018\u0010\t\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u000f\u00a8\u0006 "}, d2 = {"Lcom/example/yakultscanner/api/CallTicketHistoryDto;", "", "historyId", "", "fieldName", "", "oldValue", "newValue", "changedBy", "changedAt", "<init>", "(ILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", "getHistoryId", "()I", "getFieldName", "()Ljava/lang/String;", "getOldValue", "getNewValue", "getChangedBy", "getChangedAt", "component1", "component2", "component3", "component4", "component5", "component6", "copy", "equals", "", "other", "hashCode", "toString", "app_prodDebug"})
public final class CallTicketHistoryDto {
    @com.google.gson.annotations.SerializedName(value = "historyId")
    private final int historyId = 0;
    @com.google.gson.annotations.SerializedName(value = "fieldName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String fieldName = null;
    @com.google.gson.annotations.SerializedName(value = "oldValue")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String oldValue = null;
    @com.google.gson.annotations.SerializedName(value = "newValue")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String newValue = null;
    @com.google.gson.annotations.SerializedName(value = "changedBy")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String changedBy = null;
    @com.google.gson.annotations.SerializedName(value = "changedAt")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String changedAt = null;
    
    public CallTicketHistoryDto(int historyId, @org.jetbrains.annotations.Nullable()
    java.lang.String fieldName, @org.jetbrains.annotations.Nullable()
    java.lang.String oldValue, @org.jetbrains.annotations.Nullable()
    java.lang.String newValue, @org.jetbrains.annotations.Nullable()
    java.lang.String changedBy, @org.jetbrains.annotations.Nullable()
    java.lang.String changedAt) {
        super();
    }
    
    public final int getHistoryId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getFieldName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getOldValue() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getNewValue() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getChangedBy() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getChangedAt() {
        return null;
    }
    
    public CallTicketHistoryDto() {
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
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.CallTicketHistoryDto copy(int historyId, @org.jetbrains.annotations.Nullable()
    java.lang.String fieldName, @org.jetbrains.annotations.Nullable()
    java.lang.String oldValue, @org.jetbrains.annotations.Nullable()
    java.lang.String newValue, @org.jetbrains.annotations.Nullable()
    java.lang.String changedBy, @org.jetbrains.annotations.Nullable()
    java.lang.String changedAt) {
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