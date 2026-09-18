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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0002\b\u0002\n\u0002\u0010\u000e\n\u0002\b\u0012\n\u0002\u0010\u000b\n\u0002\b7\b\u0086\b\u0018\u00002\u00020\u0001B\u00f7\u0001\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0004\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0006\u0012\b\b\u0002\u0010\n\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u0006\u0012\b\b\u0002\u0010\f\u001a\u00020\u0003\u0012\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u0006\u0012\b\b\u0002\u0010\u000e\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0012\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0013\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u0003\u0012\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u0006\u0012\n\b\u0002\u0010\u0017\u001a\u0004\u0018\u00010\u0006\u0012\b\b\u0002\u0010\u0018\u001a\u00020\u0019\u00a2\u0006\u0004\b\u001a\u0010\u001bJ\t\u00105\u001a\u00020\u0003H\u00c6\u0003J\t\u00106\u001a\u00020\u0003H\u00c6\u0003J\u000b\u00107\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u00108\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u00109\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u0010:\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\t\u0010;\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010<\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\t\u0010=\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010>\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\t\u0010?\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010@\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u0010A\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u0010\u0010B\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003\u00a2\u0006\u0002\u0010,J\u000b\u0010C\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u0010\u0010D\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003\u00a2\u0006\u0002\u0010,J\u000b\u0010E\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u0010\u0010F\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003\u00a2\u0006\u0002\u0010,J\u000b\u0010G\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u0010H\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\t\u0010I\u001a\u00020\u0019H\u00c6\u0003J\u00fe\u0001\u0010J\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00062\b\b\u0002\u0010\n\u001a\u00020\u00032\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00062\b\b\u0002\u0010\f\u001a\u00020\u00032\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u00062\b\b\u0002\u0010\u000e\u001a\u00020\u00032\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0012\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0013\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0017\u001a\u0004\u0018\u00010\u00062\b\b\u0002\u0010\u0018\u001a\u00020\u0019H\u00c6\u0001\u00a2\u0006\u0002\u0010KJ\u0013\u0010L\u001a\u00020\u00192\b\u0010M\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010N\u001a\u00020\u0003H\u00d6\u0001J\t\u0010O\u001a\u00020\u0006H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u001dR\u0016\u0010\u0004\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001e\u0010\u001dR\u0018\u0010\u0005\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001f\u0010 R\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b!\u0010 R\u0018\u0010\b\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\"\u0010 R\u0018\u0010\t\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010 R\u0016\u0010\n\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b$\u0010\u001dR\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b%\u0010 R\u0016\u0010\f\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b&\u0010\u001dR\u0018\u0010\r\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\'\u0010 R\u0016\u0010\u000e\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b(\u0010\u001dR\u0018\u0010\u000f\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b)\u0010 R\u0018\u0010\u0010\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b*\u0010 R\u001a\u0010\u0011\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010-\u001a\u0004\b+\u0010,R\u0018\u0010\u0012\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b.\u0010 R\u001a\u0010\u0013\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010-\u001a\u0004\b/\u0010,R\u0018\u0010\u0014\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b0\u0010 R\u001a\u0010\u0015\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010-\u001a\u0004\b1\u0010,R\u0018\u0010\u0016\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b2\u0010 R\u0018\u0010\u0017\u001a\u0004\u0018\u00010\u00068\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b3\u0010 R\u0016\u0010\u0018\u001a\u00020\u00198\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u00104\u00a8\u0006P"}, d2 = {"Lcom/example/yakultscanner/api/BorrowLogDto;", "", "borrowId", "", "itemId", "serialNumber", "", "itemName", "itemDescription", "modelNumber", "borrowedByEmpId", "borrowedByEmpName", "borrowedByDeptId", "borrowedByDeptName", "borrowEncodedByUserId", "borrowEncodedByUserName", "borrowedAtUtc", "returnedByEmpId", "returnedByEmpName", "returnedByDeptId", "returnedByDeptName", "returnEncodedByUserId", "returnEncodedByUserName", "returnedAtUtc", "isOpen", "", "<init>", "(IILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;ILjava/lang/String;ILjava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Z)V", "getBorrowId", "()I", "getItemId", "getSerialNumber", "()Ljava/lang/String;", "getItemName", "getItemDescription", "getModelNumber", "getBorrowedByEmpId", "getBorrowedByEmpName", "getBorrowedByDeptId", "getBorrowedByDeptName", "getBorrowEncodedByUserId", "getBorrowEncodedByUserName", "getBorrowedAtUtc", "getReturnedByEmpId", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getReturnedByEmpName", "getReturnedByDeptId", "getReturnedByDeptName", "getReturnEncodedByUserId", "getReturnEncodedByUserName", "getReturnedAtUtc", "()Z", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "component13", "component14", "component15", "component16", "component17", "component18", "component19", "component20", "component21", "copy", "(IILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;ILjava/lang/String;ILjava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Z)Lcom/example/yakultscanner/api/BorrowLogDto;", "equals", "other", "hashCode", "toString", "app_prodDebug"})
public final class BorrowLogDto {
    @com.google.gson.annotations.SerializedName(value = "borrowId")
    private final int borrowId = 0;
    @com.google.gson.annotations.SerializedName(value = "itemId")
    private final int itemId = 0;
    @com.google.gson.annotations.SerializedName(value = "serialNumber")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String serialNumber = null;
    @com.google.gson.annotations.SerializedName(value = "itemName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String itemName = null;
    @com.google.gson.annotations.SerializedName(value = "itemDescription")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String itemDescription = null;
    @com.google.gson.annotations.SerializedName(value = "modelNumber")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String modelNumber = null;
    @com.google.gson.annotations.SerializedName(value = "borrowedByEmpId")
    private final int borrowedByEmpId = 0;
    @com.google.gson.annotations.SerializedName(value = "borrowedByEmpName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String borrowedByEmpName = null;
    @com.google.gson.annotations.SerializedName(value = "borrowedByDeptId")
    private final int borrowedByDeptId = 0;
    @com.google.gson.annotations.SerializedName(value = "borrowedByDeptName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String borrowedByDeptName = null;
    @com.google.gson.annotations.SerializedName(value = "borrowEncodedByUserId")
    private final int borrowEncodedByUserId = 0;
    @com.google.gson.annotations.SerializedName(value = "borrowEncodedByUserName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String borrowEncodedByUserName = null;
    @com.google.gson.annotations.SerializedName(value = "borrowedAtUtc")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String borrowedAtUtc = null;
    @com.google.gson.annotations.SerializedName(value = "returnedByEmpId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer returnedByEmpId = null;
    @com.google.gson.annotations.SerializedName(value = "returnedByEmpName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String returnedByEmpName = null;
    @com.google.gson.annotations.SerializedName(value = "returnedByDeptId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer returnedByDeptId = null;
    @com.google.gson.annotations.SerializedName(value = "returnedByDeptName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String returnedByDeptName = null;
    @com.google.gson.annotations.SerializedName(value = "returnEncodedByUserId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer returnEncodedByUserId = null;
    @com.google.gson.annotations.SerializedName(value = "returnEncodedByUserName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String returnEncodedByUserName = null;
    @com.google.gson.annotations.SerializedName(value = "returnedAtUtc")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String returnedAtUtc = null;
    @com.google.gson.annotations.SerializedName(value = "isOpen")
    private final boolean isOpen = false;
    
    public BorrowLogDto(int borrowId, int itemId, @org.jetbrains.annotations.Nullable()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String itemName, @org.jetbrains.annotations.Nullable()
    java.lang.String itemDescription, @org.jetbrains.annotations.Nullable()
    java.lang.String modelNumber, int borrowedByEmpId, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowedByEmpName, int borrowedByDeptId, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowedByDeptName, int borrowEncodedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowEncodedByUserName, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowedAtUtc, @org.jetbrains.annotations.Nullable()
    java.lang.Integer returnedByEmpId, @org.jetbrains.annotations.Nullable()
    java.lang.String returnedByEmpName, @org.jetbrains.annotations.Nullable()
    java.lang.Integer returnedByDeptId, @org.jetbrains.annotations.Nullable()
    java.lang.String returnedByDeptName, @org.jetbrains.annotations.Nullable()
    java.lang.Integer returnEncodedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String returnEncodedByUserName, @org.jetbrains.annotations.Nullable()
    java.lang.String returnedAtUtc, boolean isOpen) {
        super();
    }
    
    public final int getBorrowId() {
        return 0;
    }
    
    public final int getItemId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getItemName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getItemDescription() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getModelNumber() {
        return null;
    }
    
    public final int getBorrowedByEmpId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getBorrowedByEmpName() {
        return null;
    }
    
    public final int getBorrowedByDeptId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getBorrowedByDeptName() {
        return null;
    }
    
    public final int getBorrowEncodedByUserId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getBorrowEncodedByUserName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getBorrowedAtUtc() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getReturnedByEmpId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getReturnedByEmpName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getReturnedByDeptId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getReturnedByDeptName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getReturnEncodedByUserId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getReturnEncodedByUserName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getReturnedAtUtc() {
        return null;
    }
    
    public final boolean isOpen() {
        return false;
    }
    
    public BorrowLogDto() {
        super();
    }
    
    public final int component1() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component10() {
        return null;
    }
    
    public final int component11() {
        return 0;
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
    public final java.lang.Integer component14() {
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
    public final java.lang.Integer component18() {
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
    
    public final boolean component21() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    public final int component7() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component8() {
        return null;
    }
    
    public final int component9() {
        return 0;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.BorrowLogDto copy(int borrowId, int itemId, @org.jetbrains.annotations.Nullable()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String itemName, @org.jetbrains.annotations.Nullable()
    java.lang.String itemDescription, @org.jetbrains.annotations.Nullable()
    java.lang.String modelNumber, int borrowedByEmpId, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowedByEmpName, int borrowedByDeptId, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowedByDeptName, int borrowEncodedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowEncodedByUserName, @org.jetbrains.annotations.Nullable()
    java.lang.String borrowedAtUtc, @org.jetbrains.annotations.Nullable()
    java.lang.Integer returnedByEmpId, @org.jetbrains.annotations.Nullable()
    java.lang.String returnedByEmpName, @org.jetbrains.annotations.Nullable()
    java.lang.Integer returnedByDeptId, @org.jetbrains.annotations.Nullable()
    java.lang.String returnedByDeptName, @org.jetbrains.annotations.Nullable()
    java.lang.Integer returnEncodedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String returnEncodedByUserName, @org.jetbrains.annotations.Nullable()
    java.lang.String returnedAtUtc, boolean isOpen) {
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