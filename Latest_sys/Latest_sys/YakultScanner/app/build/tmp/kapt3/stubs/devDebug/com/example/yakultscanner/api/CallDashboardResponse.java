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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00006\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000b\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0014\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001BI\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0007\u0012\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t\u0012\u000e\b\u0002\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\f0\t\u00a2\u0006\u0004\b\r\u0010\u000eJ\t\u0010\u0018\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010\u0019\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010\u001a\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003J\u000f\u0010\u001b\u001a\b\u0012\u0004\u0012\u00020\n0\tH\u00c6\u0003J\u000f\u0010\u001c\u001a\b\u0012\u0004\u0012\u00020\f0\tH\u00c6\u0003JK\u0010\u001d\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00072\u000e\b\u0002\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t2\u000e\b\u0002\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\f0\tH\u00c6\u0001J\u0013\u0010\u001e\u001a\u00020\u00032\b\u0010\u001f\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010 \u001a\u00020!H\u00d6\u0001J\t\u0010\"\u001a\u00020\u0005H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000f\u0010\u0010R\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0011\u0010\u0012R\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0014R\u001c\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0016R\u001c\u0010\u000b\u001a\b\u0012\u0004\u0012\u00020\f0\t8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0016\u00a8\u0006#"}, d2 = {"Lcom/example/yakultscanner/api/CallDashboardResponse;", "", "success", "", "generatedUtc", "", "metrics", "Lcom/example/yakultscanner/api/CallDashboardMetricsDto;", "volume", "", "Lcom/example/yakultscanner/api/CallVolumePointDto;", "issueTypes", "Lcom/example/yakultscanner/api/CallIssueTypeDto;", "<init>", "(ZLjava/lang/String;Lcom/example/yakultscanner/api/CallDashboardMetricsDto;Ljava/util/List;Ljava/util/List;)V", "getSuccess", "()Z", "getGeneratedUtc", "()Ljava/lang/String;", "getMetrics", "()Lcom/example/yakultscanner/api/CallDashboardMetricsDto;", "getVolume", "()Ljava/util/List;", "getIssueTypes", "component1", "component2", "component3", "component4", "component5", "copy", "equals", "other", "hashCode", "", "toString", "app_devDebug"})
public final class CallDashboardResponse {
    @com.google.gson.annotations.SerializedName(value = "success")
    private final boolean success = false;
    @com.google.gson.annotations.SerializedName(value = "generatedUtc")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String generatedUtc = null;
    @com.google.gson.annotations.SerializedName(value = "metrics")
    @org.jetbrains.annotations.Nullable()
    private final com.example.yakultscanner.api.CallDashboardMetricsDto metrics = null;
    @com.google.gson.annotations.SerializedName(value = "volume")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.CallVolumePointDto> volume = null;
    @com.google.gson.annotations.SerializedName(value = "issueTypes")
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.CallIssueTypeDto> issueTypes = null;
    
    public CallDashboardResponse(boolean success, @org.jetbrains.annotations.Nullable()
    java.lang.String generatedUtc, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.CallDashboardMetricsDto metrics, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.CallVolumePointDto> volume, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.CallIssueTypeDto> issueTypes) {
        super();
    }
    
    public final boolean getSuccess() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getGeneratedUtc() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.CallDashboardMetricsDto getMetrics() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.CallVolumePointDto> getVolume() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.CallIssueTypeDto> getIssueTypes() {
        return null;
    }
    
    public CallDashboardResponse() {
        super();
    }
    
    public final boolean component1() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final com.example.yakultscanner.api.CallDashboardMetricsDto component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.CallVolumePointDto> component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.CallIssueTypeDto> component5() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.CallDashboardResponse copy(boolean success, @org.jetbrains.annotations.Nullable()
    java.lang.String generatedUtc, @org.jetbrains.annotations.Nullable()
    com.example.yakultscanner.api.CallDashboardMetricsDto metrics, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.CallVolumePointDto> volume, @org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.CallIssueTypeDto> issueTypes) {
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