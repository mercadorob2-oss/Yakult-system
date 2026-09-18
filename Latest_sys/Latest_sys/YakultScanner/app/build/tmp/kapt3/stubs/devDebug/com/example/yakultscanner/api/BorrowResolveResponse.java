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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\n\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B\u001f\u0012\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\u0004\b\u0006\u0010\u0007J\u000b\u0010\f\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\r\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J!\u0010\u000e\u001a\u00020\u00002\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005H\u00c6\u0001J\u0013\u0010\u000f\u001a\u00020\u00102\b\u0010\u0011\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u0012\u001a\u00020\u0013H\u00d6\u0001J\t\u0010\u0014\u001a\u00020\u0015H\u00d6\u0001R\u0018\u0010\u0002\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\b\u0010\tR\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\n\u0010\u000b\u00a8\u0006\u0016"}, d2 = {"Lcom/example/yakultscanner/api/BorrowResolveResponse;", "", "item", "Lcom/example/yakultscanner/api/BorrowItemDto;", "openBorrow", "Lcom/example/yakultscanner/api/BorrowLogDto;", "<init>", "(Lcom/example/yakultscanner/api/BorrowItemDto;Lcom/example/yakultscanner/api/BorrowLogDto;)V", "getItem", "()Lcom/example/yakultscanner/api/BorrowItemDto;", "getOpenBorrow", "()Lcom/example/yakultscanner/api/BorrowLogDto;", "component1", "component2", "copy", "equals", "", "other", "hashCode", "", "toString", "", "app_devDebug"})
public final class BorrowResolveResponse {
    @com.google.gson.annotations.SerializedName(value = "item")
    @org.jetbrains.annotations.Nullable()
    private final com.example.yakultscanner.api.BorrowItemDto item = null;
    @com.google.gson.annotations.SerializedName(value = "openBorrow")
    @org.jetbrains.annotations.Nullable()
    private final com.example.yakultscanner.api.BorrowLogDto openBorrow = null;
    
    public BorrowResolveResponse(@org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowItemDto item, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowLogDto openBorrow) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowItemDto getItem() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowLogDto getOpenBorrow() {
        return null;
    }
    
    public BorrowResolveResponse() {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowItemDto component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.BorrowLogDto component2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.BorrowResolveResponse copy(@org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowItemDto item, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.BorrowLogDto openBorrow) {
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