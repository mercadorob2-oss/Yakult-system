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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00000\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0010\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0000\b\u0086\b\u0018\u00002\u00020\u0001B7\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\b\b\u0002\u0010\u0006\u001a\u00020\u0003\u0012\u000e\b\u0002\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\t0\b\u00a2\u0006\u0004\b\n\u0010\u000bJ\t\u0010\u0012\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u0013\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\t\u0010\u0014\u001a\u00020\u0003H\u00c6\u0003J\u000f\u0010\u0015\u001a\b\u0012\u0004\u0012\u00020\t0\bH\u00c6\u0003J9\u0010\u0016\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u00032\u000e\b\u0002\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\t0\bH\u00c6\u0001J\u0013\u0010\u0017\u001a\u00020\u00032\b\u0010\u0018\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u0019\u001a\u00020\u001aH\u00d6\u0001J\t\u0010\u001b\u001a\u00020\u001cH\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\f\u0010\rR\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000e\u0010\u000fR\u0016\u0010\u0006\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\rR\u001c\u0010\u0007\u001a\b\u0012\u0004\u0012\u00020\t0\b8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0010\u0010\u0011\u00a8\u0006\u001d"}, d2 = {"Lcom/example/yakultscanner/api/SerialLookupResponse;", "", "found", "", "item", "Lcom/example/yakultscanner/api/SerialLookupItemDto;", "isInSet", "sets", "", "Lcom/example/yakultscanner/api/SerialLookupSetDto;", "<init>", "(ZLcom/example/yakultscanner/api/SerialLookupItemDto;ZLjava/util/List;)V", "getFound", "()Z", "getItem", "()Lcom/example/yakultscanner/api/SerialLookupItemDto;", "getSets", "()Ljava/util/List;", "component1", "component2", "component3", "component4", "copy", "equals", "other", "hashCode", "", "toString", "", "app_devDebug"})
public final class SerialLookupResponse {
    @com.google.gson.annotations.SerializedName(value = "found")
    private final boolean found = false;
    @com.google.gson.annotations.SerializedName(value = "item")
    @org.jetbrains.annotations.Nullable()
    private final com.example.yakultscanner.api.SerialLookupItemDto item = null;
    @com.google.gson.annotations.SerializedName(value = "isInSet")
    private final boolean isInSet = false;
    @com.google.gson.annotations.SerializedName(value = "sets")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets = null;
    
    public SerialLookupResponse(boolean found, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.SerialLookupItemDto item, boolean isInSet, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets) {
        super();
    }
    
    public final boolean getFound() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.SerialLookupItemDto getItem() {
        return null;
    }
    
    public final boolean isInSet() {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> getSets() {
        return null;
    }
    
    public SerialLookupResponse() {
        super();
    }
    
    public final boolean component1() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.SerialLookupItemDto component2() {
        return null;
    }
    
    public final boolean component3() {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.SerialLookupResponse copy(boolean found, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.SerialLookupItemDto item, boolean isInSet, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.SerialLookupSetDto> sets) {
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