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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u001e\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000b\n\u0002\b\f\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u001b\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0005\u0010\u0006J\t\u0010\n\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u000b\u001a\u00020\u0003H\u00c6\u0003J\u001d\u0010\f\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\r\u001a\u00020\u00032\b\u0010\u000e\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u000f\u001a\u00020\u0010H\u00d6\u0001J\t\u0010\u0011\u001a\u00020\u0012H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0007\u0010\bR\u0016\u0010\u0004\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\b\u00a8\u0006\u0013"}, d2 = {"Lcom/example/yakultscanner/api/BorrowAccessDto;", "", "canDeleteOpenBorrow", "", "canExportCsv", "<init>", "(ZZ)V", "getCanDeleteOpenBorrow", "()Z", "getCanExportCsv", "component1", "component2", "copy", "equals", "other", "hashCode", "", "toString", "", "app_prodDebug"})
public final class BorrowAccessDto {
    @com.google.gson.annotations.SerializedName(value = "canDeleteOpenBorrow")
    private final boolean canDeleteOpenBorrow = false;
    @com.google.gson.annotations.SerializedName(value = "canExportCsv")
    private final boolean canExportCsv = false;
    
    public BorrowAccessDto(boolean canDeleteOpenBorrow, boolean canExportCsv) {
        super();
    }
    
    public final boolean getCanDeleteOpenBorrow() {
        return false;
    }
    
    public final boolean getCanExportCsv() {
        return false;
    }
    
    public BorrowAccessDto() {
        super();
    }
    
    public final boolean component1() {
        return false;
    }
    
    public final boolean component2() {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.BorrowAccessDto copy(boolean canDeleteOpenBorrow, boolean canExportCsv) {
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