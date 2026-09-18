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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000b\n\u0002\b\u0007\n\u0002\u0010\u0002\n\u0000\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\b\u0010\b\u001a\u00020\tH\u0002J\u0010\u0010\n\u001a\u00020\u000b2\u0006\u0010\f\u001a\u00020\u0005H\u0002J\u0010\u0010\r\u001a\u00020\u00072\u0006\u0010\u000e\u001a\u00020\u0005H\u0002J\u0006\u0010\u0012\u001a\u00020\u0013R\u0010\u0010\u0004\u001a\u0004\u0018\u00010\u0005X\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u0010\u0010\u0006\u001a\u0004\u0018\u00010\u0007X\u0082\u000e\u00a2\u0006\u0002\n\u0000R\u0011\u0010\u000f\u001a\u00020\u00078F\u00a2\u0006\u0006\u001a\u0004\b\u0010\u0010\u0011\u00a8\u0006\u0014"}, d2 = {"Lcom/example/yakultscanner/api/ApiClient;", "", "<init>", "()V", "cachedBaseUrl", "", "cachedService", "Lcom/example/yakultscanner/api/YakultApiService;", "buildClient", "Lokhttp3/OkHttpClient;", "shouldAutoLogoutForUnauthorized", "", "path", "buildService", "baseUrl", "service", "getService", "()Lcom/example/yakultscanner/api/YakultApiService;", "clearCachedService", "", "app_prodDebug"})
public final class ApiClient {
    @kotlin.jvm.Volatile()
    @org.jetbrains.annotations.Nullable()
    private static volatile java.lang.String cachedBaseUrl;
    @kotlin.jvm.Volatile()
    @org.jetbrains.annotations.Nullable()
    private static volatile com.example.yakultscanner.api.YakultApiService cachedService;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.api.ApiClient INSTANCE = null;
    
    private ApiClient() {
        super();
    }
    
    private final okhttp3.OkHttpClient buildClient() {
        return null;
    }
    
    private final boolean shouldAutoLogoutForUnauthorized(java.lang.String path) {
        return false;
    }
    
    private final com.example.yakultscanner.api.YakultApiService buildService(java.lang.String baseUrl) {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.YakultApiService getService() {
        return null;
    }
    
    public final void clearCachedService() {
    }
}