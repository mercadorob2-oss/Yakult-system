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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00002\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0014\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001BM\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0004\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0005\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0007\u0012\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\f\u00a2\u0006\u0004\b\r\u0010\u000eJ\t\u0010\u0019\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001a\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u001b\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u001c\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003J\u000f\u0010\u001d\u001a\b\u0012\u0004\u0012\u00020\n0\tH\u00c6\u0003J\u000b\u0010\u001e\u001a\u0004\u0018\u00010\fH\u00c6\u0003JO\u0010\u001f\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00032\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00072\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\fH\u00c6\u0001J\u0013\u0010 \u001a\u00020!2\b\u0010\"\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010#\u001a\u00020\u0003H\u00d6\u0001J\t\u0010$\u001a\u00020\u0007H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000f\u0010\u0010R\u0016\u0010\u0004\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\u0010R\u0016\u0010\u0005\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u0010R\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0014R\u001c\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0016R\u0018\u0010\u000b\u001a\u0004\u0018\u00010\f8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0018\u00a8\u0006%"}, d2 = {"Lcom/example/yakultscanner/api/BorrowHomeSummaryResponse;", "", "openCount", "", "overdueCount", "returnedTodayCount", "oldestOpenBorrowedAtUtc", "", "recentRows", "", "Lcom/example/yakultscanner/api/BorrowLogDto;", "access", "Lcom/example/yakultscanner/api/BorrowAccessDto;", "<init>", "(IIILjava/lang/String;Ljava/util/List;Lcom/example/yakultscanner/api/BorrowAccessDto;)V", "getOpenCount", "()I", "getOverdueCount", "getReturnedTodayCount", "getOldestOpenBorrowedAtUtc", "()Ljava/lang/String;", "getRecentRows", "()Ljava/util/List;", "getAccess", "()Lcom/example/yakultscanner/api/BorrowAccessDto;", "component1", "component2", "component3", "component4", "component5", "component6", "copy", "equals", "", "other", "hashCode", "toString", "app_devDebug"})
public final class BorrowHomeSummaryResponse {
    @com.google.gson.annotations.SerializedName(value = "openCount")
    private final int openCount = 0;
    @com.google.gson.annotations.SerializedName(value = "overdueCount")
    private final int overdueCount = 0;
    @com.google.gson.annotations.SerializedName(value = "returnedTodayCount")
    private final int returnedTodayCount = 0;
    @com.google.gson.annotations.SerializedName(value = "oldestOpenBorrowedAtUtc")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String oldestOpenBorrowedAtUtc = null;
    @com.google.gson.annotations.SerializedName(value = "recentRows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.BorrowLogDto> recentRows = null;
    @com.google.gson.annotations.SerializedName(value = "access")
    @org.jetbrains.annotations.Nullable()
    private final com.example.yakultscanner.api.BorrowAccessDto access = null;
    
    public BorrowHomeSummaryResponse(int openCount, int overdueCount, int returnedTodayCount, @org.jetbrains.annotations.Nullable()
    java.lang.String oldestOpenBorrowedAtUtc, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BorrowLogDto> recentRows, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowAccessDto access) {
        super();
    }
    
    public final int getOpenCount() {
        return 0;
    }
    
    public final int getOverdueCount() {
        return 0;
    }
    
    public final int getReturnedTodayCount() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getOldestOpenBorrowedAtUtc() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.BorrowLogDto> getRecentRows() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowAccessDto getAccess() {
        return null;
    }
    
    public BorrowHomeSummaryResponse() {
        super();
    }
    
    public final int component1() {
        return 0;
    }
    
    public final int component2() {
        return 0;
    }
    
    public final int component3() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.BorrowLogDto> component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowAccessDto component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.BorrowHomeSummaryResponse copy(int openCount, int overdueCount, int returnedTodayCount, @org.jetbrains.annotations.Nullable()
    java.lang.String oldestOpenBorrowedAtUtc, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BorrowLogDto> recentRows, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowAccessDto access) {
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