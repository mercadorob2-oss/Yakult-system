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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0000\n\u0002\u0010\b\n\u0002\b\u0004\n\u0002\u0010\u000b\n\u0002\bD\b\u0086\b\u0018\u00002\u00020\u0001B\u00ed\u0001\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0005\u0012\u0006\u0010\u0006\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\n\u0012\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0012\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0013\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u0005\u0012\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0017\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0018\u001a\u0004\u0018\u00010\u0003\u00a2\u0006\u0004\b\u0019\u0010\u001aJ\t\u00104\u001a\u00020\u0003H\u00c6\u0003J\t\u00105\u001a\u00020\u0005H\u00c6\u0003J\t\u00106\u001a\u00020\u0003H\u00c6\u0003J\u000b\u00107\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u0010\u00108\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u0010\u00109\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010$J\u0010\u0010:\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010$J\u0010\u0010;\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u0010\u0010<\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u0010\u0010=\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u0010\u0010>\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u000b\u0010?\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010@\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010A\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010B\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u0010\u0010C\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u000b\u0010D\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010E\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010F\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010G\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u00f8\u0001\u0010H\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00052\b\b\u0002\u0010\u0006\u001a\u00020\u00032\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0012\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0013\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0017\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0018\u001a\u0004\u0018\u00010\u0003H\u00c6\u0001\u00a2\u0006\u0002\u0010IJ\u0013\u0010J\u001a\u00020\n2\b\u0010K\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010L\u001a\u00020\u0005H\u00d6\u0001J\t\u0010M\u001a\u00020\u0003H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u001cR\u0016\u0010\u0004\u001a\u00020\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u001eR\u0016\u0010\u0006\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001f\u0010\u001cR\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b \u0010\u001cR\u001a\u0010\b\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b!\u0010\"R\u001a\u0010\t\u001a\u0004\u0018\u00010\n8\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010%\u001a\u0004\b\t\u0010$R\u001a\u0010\u000b\u001a\u0004\u0018\u00010\n8\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010%\u001a\u0004\b&\u0010$R\u001a\u0010\f\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b\'\u0010\"R\u001a\u0010\r\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b(\u0010\"R\u001a\u0010\u000e\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b)\u0010\"R\u001a\u0010\u000f\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b*\u0010\"R\u0018\u0010\u0010\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b+\u0010\u001cR\u0018\u0010\u0011\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b,\u0010\u001cR\u0018\u0010\u0012\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b-\u0010\u001cR\u0018\u0010\u0013\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b.\u0010\u001cR\u001a\u0010\u0014\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b/\u0010\"R\u0018\u0010\u0015\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b0\u0010\u001cR\u0018\u0010\u0016\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b1\u0010\u001cR\u0018\u0010\u0017\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b2\u0010\u001cR\u0018\u0010\u0018\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b3\u0010\u001c\u00a8\u0006N"}, d2 = {"Lcom/example/yakultscanner/api/ResolutionRequest;", "", "action", "", "ticketId", "", "resolutionType", "remarks", "userId", "isTemporary", "", "useUnlistedOldItem", "oldItemId", "newItemId", "quantity", "oldItemConditionId", "oldItemConditionRemarks", "oldItemRepairAction", "unlistedOldItemName", "unlistedOldItemDescription", "unlistedOldItemCategoryId", "unlistedOldItemCategoryName", "unlistedOldItemSerialNumber", "unlistedOldItemModelNumber", "unlistedOldItemUnitOfMeasure", "<init>", "(Ljava/lang/String;ILjava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Boolean;Ljava/lang/Boolean;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", "getAction", "()Ljava/lang/String;", "getTicketId", "()I", "getResolutionType", "getRemarks", "getUserId", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "()Ljava/lang/Boolean;", "Ljava/lang/Boolean;", "getUseUnlistedOldItem", "getOldItemId", "getNewItemId", "getQuantity", "getOldItemConditionId", "getOldItemConditionRemarks", "getOldItemRepairAction", "getUnlistedOldItemName", "getUnlistedOldItemDescription", "getUnlistedOldItemCategoryId", "getUnlistedOldItemCategoryName", "getUnlistedOldItemSerialNumber", "getUnlistedOldItemModelNumber", "getUnlistedOldItemUnitOfMeasure", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "component13", "component14", "component15", "component16", "component17", "component18", "component19", "component20", "copy", "(Ljava/lang/String;ILjava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Boolean;Ljava/lang/Boolean;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)Lcom/example/yakultscanner/api/ResolutionRequest;", "equals", "other", "hashCode", "toString", "app_prodDebug"})
public final class ResolutionRequest {
    @com.google.gson.annotations.SerializedName(value = "action")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String action = null;
    @com.google.gson.annotations.SerializedName(value = "ticketId")
    private final int ticketId = 0;
    @com.google.gson.annotations.SerializedName(value = "resolutionType")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String resolutionType = null;
    @com.google.gson.annotations.SerializedName(value = "remarks")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String remarks = null;
    @com.google.gson.annotations.SerializedName(value = "userId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer userId = null;
    @com.google.gson.annotations.SerializedName(value = "isTemporary")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Boolean isTemporary = null;
    @com.google.gson.annotations.SerializedName(value = "useUnlistedOldItem")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Boolean useUnlistedOldItem = null;
    @com.google.gson.annotations.SerializedName(value = "oldItemId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer oldItemId = null;
    @com.google.gson.annotations.SerializedName(value = "newItemId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer newItemId = null;
    @com.google.gson.annotations.SerializedName(value = "quantity")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer quantity = null;
    @com.google.gson.annotations.SerializedName(value = "oldItemConditionId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer oldItemConditionId = null;
    @com.google.gson.annotations.SerializedName(value = "oldItemConditionRemarks")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String oldItemConditionRemarks = null;
    @com.google.gson.annotations.SerializedName(value = "oldItemRepairAction")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String oldItemRepairAction = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String unlistedOldItemName = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemDescription")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String unlistedOldItemDescription = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemCategoryId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer unlistedOldItemCategoryId = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemCategoryName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String unlistedOldItemCategoryName = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemSerialNumber")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String unlistedOldItemSerialNumber = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemModelNumber")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String unlistedOldItemModelNumber = null;
    @com.google.gson.annotations.SerializedName(value = "unlistedOldItemUnitOfMeasure")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String unlistedOldItemUnitOfMeasure = null;
    
    public ResolutionRequest(@org.jetbrains.annotations.NotNull()
    java.lang.String action, int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String resolutionType, @org.jetbrains.annotations.Nullable()
    java.lang.String remarks, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId, @org.jetbrains.annotations.Nullable()
    java.lang.Boolean isTemporary, @org.jetbrains.annotations.Nullable()
    java.lang.Boolean useUnlistedOldItem, @org.jetbrains.annotations.Nullable()
    java.lang.Integer oldItemId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer newItemId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer quantity, @org.jetbrains.annotations.Nullable()
    java.lang.Integer oldItemConditionId, @org.jetbrains.annotations.Nullable()
    java.lang.String oldItemConditionRemarks, @org.jetbrains.annotations.Nullable()
    java.lang.String oldItemRepairAction, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemName, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemDescription, @org.jetbrains.annotations.Nullable()
    java.lang.Integer unlistedOldItemCategoryId, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemCategoryName, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemSerialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemModelNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemUnitOfMeasure) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getAction() {
        return null;
    }
    
    public final int getTicketId() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getResolutionType() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getRemarks() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getUserId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Boolean isTemporary() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Boolean getUseUnlistedOldItem() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getOldItemId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getNewItemId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getQuantity() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getOldItemConditionId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getOldItemConditionRemarks() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getOldItemRepairAction() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUnlistedOldItemName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUnlistedOldItemDescription() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getUnlistedOldItemCategoryId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUnlistedOldItemCategoryName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUnlistedOldItemSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUnlistedOldItemModelNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUnlistedOldItemUnitOfMeasure() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component10() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component11() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component12() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component13() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component14() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component15() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component16() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component17() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component18() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component19() {
        return null;
    }
    
    public final int component2() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component20() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Boolean component6() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Boolean component7() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component8() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.ResolutionRequest copy(@org.jetbrains.annotations.NotNull()
    java.lang.String action, int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String resolutionType, @org.jetbrains.annotations.Nullable()
    java.lang.String remarks, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId, @org.jetbrains.annotations.Nullable()
    java.lang.Boolean isTemporary, @org.jetbrains.annotations.Nullable()
    java.lang.Boolean useUnlistedOldItem, @org.jetbrains.annotations.Nullable()
    java.lang.Integer oldItemId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer newItemId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer quantity, @org.jetbrains.annotations.Nullable()
    java.lang.Integer oldItemConditionId, @org.jetbrains.annotations.Nullable()
    java.lang.String oldItemConditionRemarks, @org.jetbrains.annotations.Nullable()
    java.lang.String oldItemRepairAction, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemName, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemDescription, @org.jetbrains.annotations.Nullable()
    java.lang.Integer unlistedOldItemCategoryId, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemCategoryName, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemSerialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemModelNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String unlistedOldItemUnitOfMeasure) {
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