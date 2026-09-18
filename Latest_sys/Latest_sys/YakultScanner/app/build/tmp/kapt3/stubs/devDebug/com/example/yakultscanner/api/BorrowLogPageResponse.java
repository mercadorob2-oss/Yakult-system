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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00000\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0010\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001B9\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\u000e\b\u0002\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u0007\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n\u00a2\u0006\u0004\b\u000b\u0010\fJ\t\u0010\u0015\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u0016\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000f\u0010\u0017\u001a\b\u0012\u0004\u0012\u00020\b0\u0007H\u00c6\u0003J\u000b\u0010\u0018\u001a\u0004\u0018\u00010\nH\u00c6\u0003J;\u0010\u0019\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\u000e\b\u0002\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u00072\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\nH\u00c6\u0001J\u0013\u0010\u001a\u001a\u00020\u001b2\b\u0010\u001c\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u001d\u001a\u00020\u0003H\u00d6\u0001J\t\u0010\u001e\u001a\u00020\u0005H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\r\u0010\u000eR\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000f\u0010\u0010R\u001c\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u00078\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\u0012R\u0018\u0010\t\u001a\u0004\u0018\u00010\n8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0014\u00a8\u0006\u001f"}, d2 = {"Lcom/example/yakultscanner/api/BorrowLogPageResponse;", "", "totalCount", "", "oldestBorrowedAtUtc", "", "rows", "", "Lcom/example/yakultscanner/api/BorrowLogDto;", "access", "Lcom/example/yakultscanner/api/BorrowAccessDto;", "<init>", "(ILjava/lang/String;Ljava/util/List;Lcom/example/yakultscanner/api/BorrowAccessDto;)V", "getTotalCount", "()I", "getOldestBorrowedAtUtc", "()Ljava/lang/String;", "getRows", "()Ljava/util/List;", "getAccess", "()Lcom/example/yakultscanner/api/BorrowAccessDto;", "component1", "component2", "component3", "component4", "copy", "equals", "", "other", "hashCode", "toString", "app_devDebug"})
public final class BorrowLogPageResponse {
    @com.google.gson.annotations.SerializedName(value = "totalCount")
    private final int totalCount = 0;
    @com.google.gson.annotations.SerializedName(value = "oldestBorrowedAtUtc")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String oldestBorrowedAtUtc = null;
    @com.google.gson.annotations.SerializedName(value = "rows")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.BorrowLogDto> rows = null;
    @com.google.gson.annotations.SerializedName(value = "access")
    @org.jetbrains.annotations.Nullable()
    private final com.example.yakultscanner.api.BorrowAccessDto access = null;
    
    public BorrowLogPageResponse(int totalCount, @org.jetbrains.annotations.Nullable()
    java.lang.String oldestBorrowedAtUtc, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BorrowLogDto> rows, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowAccessDto access) {
        super();
    }
    
    public final int getTotalCount() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getOldestBorrowedAtUtc() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.BorrowLogDto> getRows() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowAccessDto getAccess() {
        return null;
    }
    
    public BorrowLogPageResponse() {
        super();
    }
    
    public final int component1() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.BorrowLogDto> component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowAccessDto component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.BorrowLogPageResponse copy(int totalCount, @org.jetbrains.annotations.Nullable()
    java.lang.String oldestBorrowedAtUtc, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BorrowLogDto> rows, @org.jetbrains.annotations.Nullable()
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