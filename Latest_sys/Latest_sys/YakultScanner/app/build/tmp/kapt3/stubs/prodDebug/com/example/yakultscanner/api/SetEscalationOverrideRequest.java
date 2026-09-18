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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0010\u000e\n\u0002\b\u0002\n\u0002\u0010\u000b\n\u0002\b\u001a\b\u0086\b\u0018\u00002\u00020\u0001BE\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u0003\u0012\u0006\u0010\u0006\u001a\u00020\u0007\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0003\u0012\b\b\u0002\u0010\t\u001a\u00020\n\u00a2\u0006\u0004\b\u000b\u0010\fJ\t\u0010\u0018\u001a\u00020\u0003H\u00c6\u0003J\u0010\u0010\u0019\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0010J\u0010\u0010\u001a\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0010J\t\u0010\u001b\u001a\u00020\u0007H\u00c6\u0003J\u0010\u0010\u001c\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0010J\t\u0010\u001d\u001a\u00020\nH\u00c6\u0003JP\u0010\u001e\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\u0006\u001a\u00020\u00072\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\t\u001a\u00020\nH\u00c6\u0001\u00a2\u0006\u0002\u0010\u001fJ\u0013\u0010 \u001a\u00020\n2\b\u0010!\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\"\u001a\u00020\u0003H\u00d6\u0001J\t\u0010#\u001a\u00020\u0007H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\r\u0010\u000eR\u001a\u0010\u0004\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0011\u001a\u0004\b\u000f\u0010\u0010R\u001a\u0010\u0005\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0011\u001a\u0004\b\u0012\u0010\u0010R\u0016\u0010\u0006\u001a\u00020\u00078\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0013\u0010\u0014R\u001a\u0010\b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0011\u001a\u0004\b\u0015\u0010\u0010R\u0016\u0010\t\u001a\u00020\n8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0017\u00a8\u0006$"}, d2 = {"Lcom/example/yakultscanner/api/SetEscalationOverrideRequest;", "", "ticketId", "", "daysToSupervisor", "daysToManager", "reason", "", "changedByUserId", "clear", "", "<init>", "(ILjava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/Integer;Z)V", "getTicketId", "()I", "getDaysToSupervisor", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getDaysToManager", "getReason", "()Ljava/lang/String;", "getChangedByUserId", "getClear", "()Z", "component1", "component2", "component3", "component4", "component5", "component6", "copy", "(ILjava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/Integer;Z)Lcom/example/yakultscanner/api/SetEscalationOverrideRequest;", "equals", "other", "hashCode", "toString", "app_prodDebug"})
public final class SetEscalationOverrideRequest {
    @com.google.gson.annotations.SerializedName(value = "ticketId")
    private final int ticketId = 0;
    @com.google.gson.annotations.SerializedName(value = "daysToSupervisor")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer daysToSupervisor = null;
    @com.google.gson.annotations.SerializedName(value = "daysToManager")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer daysToManager = null;
    @com.google.gson.annotations.SerializedName(value = "reason")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String reason = null;
    @com.google.gson.annotations.SerializedName(value = "changedByUserId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer changedByUserId = null;
    @com.google.gson.annotations.SerializedName(value = "clear")
    private final boolean clear = false;
    
    public SetEscalationOverrideRequest(int ticketId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer daysToSupervisor, @org.jetbrains.annotations.Nullable()
    java.lang.Integer daysToManager, @org.jetbrains.annotations.NotNull()
    java.lang.String reason, @org.jetbrains.annotations.Nullable()
    java.lang.Integer changedByUserId, boolean clear) {
        super();
    }
    
    public final int getTicketId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getDaysToSupervisor() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getDaysToManager() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getReason() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getChangedByUserId() {
        return null;
    }
    
    public final boolean getClear() {
        return false;
    }
    
    public final int component1() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component2() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component5() {
        return null;
    }
    
    public final boolean component6() {
        return false;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.SetEscalationOverrideRequest copy(int ticketId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer daysToSupervisor, @org.jetbrains.annotations.Nullable()
    java.lang.Integer daysToManager, @org.jetbrains.annotations.NotNull()
    java.lang.String reason, @org.jetbrains.annotations.Nullable()
    java.lang.Integer changedByUserId, boolean clear) {
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