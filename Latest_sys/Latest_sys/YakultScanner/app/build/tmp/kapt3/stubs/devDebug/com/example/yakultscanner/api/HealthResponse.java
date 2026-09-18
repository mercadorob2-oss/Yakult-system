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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u000f\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B\'\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\u0004\b\u0007\u0010\bJ\t\u0010\u000e\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u000f\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u0010\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J+\u0010\u0011\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0005H\u00c6\u0001J\u0013\u0010\u0012\u001a\u00020\u00032\b\u0010\u0013\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u0014\u001a\u00020\u0015H\u00d6\u0001J\t\u0010\u0016\u001a\u00020\u0005H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\nR\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000b\u0010\fR\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\r\u0010\f\u00a8\u0006\u0017"}, d2 = {"Lcom/example/yakultscanner/api/HealthResponse;", "", "ok", "", "utc", "", "database", "<init>", "(ZLjava/lang/String;Ljava/lang/String;)V", "getOk", "()Z", "getUtc", "()Ljava/lang/String;", "getDatabase", "component1", "component2", "component3", "copy", "equals", "other", "hashCode", "", "toString", "app_devDebug"})
public final class HealthResponse {
    @com.google.gson.annotations.SerializedName(value = "ok")
    private final boolean ok = false;
    @com.google.gson.annotations.SerializedName(value = "utc")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String utc = null;
    @com.google.gson.annotations.SerializedName(value = "database")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String database = null;
    
    public HealthResponse(boolean ok, @org.jetbrains.annotations.Nullable()
    java.lang.String utc, @org.jetbrains.annotations.Nullable()
    java.lang.String database) {
        super();
    }
    
    public final boolean getOk() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUtc() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDatabase() {
        return null;
    }
    
    public final boolean component1() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.HealthResponse copy(boolean ok, @org.jetbrains.annotations.Nullable()
    java.lang.String utc, @org.jetbrains.annotations.Nullable()
    java.lang.String database) {
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