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

@kotlin.Metadata(mv = {2, 2, 0}, k = 2, xi = 48, d1 = {"\u0000:\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\u001a\u001c\u0010\u0000\u001a\b\u0012\u0004\u0012\u00020\u00020\u00012\u0006\u0010\u0003\u001a\u00020\u0004H\u0086@\u00a2\u0006\u0002\u0010\u0005\u001a\u001c\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\u00070\u00012\u0006\u0010\b\u001a\u00020\tH\u0086@\u00a2\u0006\u0002\u0010\n\u001a0\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\f0\u00012\b\u0010\b\u001a\u0004\u0018\u00010\t2\b\u0010\r\u001a\u0004\u0018\u00010\t2\u0006\u0010\u000e\u001a\u00020\u000fH\u0086@\u00a2\u0006\u0002\u0010\u0010\u001a(\u0010\u0011\u001a\b\u0012\u0004\u0012\u00020\u00120\u00012\b\u0010\b\u001a\u0004\u0018\u00010\t2\b\u0010\r\u001a\u0004\u0018\u00010\tH\u0086@\u00a2\u0006\u0002\u0010\u0013\u00a8\u0006\u0014"}, d2 = {"deploySet", "Lretrofit2/Response;", "Lcom/example/yakultscanner/api/DeployResponse;", "setId", "", "(ILkotlin/coroutines/Continuation;)Ljava/lang/Object;", "resolveToken", "Lcom/example/yakultscanner/api/ResolveTokenResponse;", "token", "", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "uploadSetImage", "Lcom/example/yakultscanner/api/SetImageUploadResponse;", "setCode", "request", "Lcom/example/yakultscanner/api/SetImageUploadRequest;", "(Ljava/lang/String;Ljava/lang/String;Lcom/example/yakultscanner/api/SetImageUploadRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getSetImages", "Lcom/example/yakultscanner/api/SetImagesResponse;", "(Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "app_prodDebug"})
public final class ApiClientKt {
    
    @org.jetbrains.annotations.Nullable()
    public static final java.lang.Object deploySet(int setId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.DeployResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public static final java.lang.Object resolveToken(@org.jetbrains.annotations.NotNull()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.ResolveTokenResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public static final java.lang.Object uploadSetImage(@org.jetbrains.annotations.Nullable()
    java.lang.String token, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetImageUploadRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetImageUploadResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public static final java.lang.Object getSetImages(@org.jetbrains.annotations.Nullable()
    java.lang.String token, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetImagesResponse>> $completion) {
        return null;
    }
}