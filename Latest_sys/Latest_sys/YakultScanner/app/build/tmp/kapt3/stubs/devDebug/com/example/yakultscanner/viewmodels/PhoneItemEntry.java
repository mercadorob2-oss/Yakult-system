package com.example.yakultscanner.viewmodels;

import androidx.lifecycle.ViewModel;
import com.example.yakultscanner.UserSession;
import com.example.yakultscanner.api.ApiResult;
import com.example.yakultscanner.api.BatchItemRequest;
import com.example.yakultscanner.api.BatchItemsRequest;
import com.example.yakultscanner.api.ConditionDto;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.VendorDto;
import com.example.yakultscanner.data.repository.InventoryRepository;
import java.text.SimpleDateFormat;
import java.util.*;
import dagger.hilt.android.lifecycle.HiltViewModel;
import javax.inject.Inject;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0010\n\u0002\u0010\u000b\n\u0002\b\u0002\n\u0002\u0010\b\n\u0002\b\u0002\b\u0086\b\u0018\u00002\u00020\u0001B/\u0012\b\b\u0002\u0010\u0002\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0004\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0005\u001a\u00020\u0003\u0012\b\b\u0002\u0010\u0006\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0007\u0010\bJ\t\u0010\u000e\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u000f\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u0010\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\u0011\u001a\u00020\u0003H\u00c6\u0003J1\u0010\u0012\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00032\b\b\u0002\u0010\u0006\u001a\u00020\u0003H\u00c6\u0001J\u0013\u0010\u0013\u001a\u00020\u00142\b\u0010\u0015\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010\u0016\u001a\u00020\u0017H\u00d6\u0001J\t\u0010\u0018\u001a\u00020\u0003H\u00d6\u0001R\u0011\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\t\u0010\nR\u0011\u0010\u0004\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u000b\u0010\nR\u0011\u0010\u0005\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\f\u0010\nR\u0011\u0010\u0006\u001a\u00020\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\r\u0010\n\u00a8\u0006\u0019"}, d2 = {"Lcom/example/yakultscanner/viewmodels/PhoneItemEntry;", "", "cellPhoneNumber", "", "serialNumber", "imei1", "imei2", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", "getCellPhoneNumber", "()Ljava/lang/String;", "getSerialNumber", "getImei1", "getImei2", "component1", "component2", "component3", "component4", "copy", "equals", "", "other", "hashCode", "", "toString", "app_devDebug"})
public final class PhoneItemEntry {
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String cellPhoneNumber = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String serialNumber = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String imei1 = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String imei2 = null;
    
    public PhoneItemEntry(@org.jetbrains.annotations.NotNull()
    java.lang.String cellPhoneNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String imei1, @org.jetbrains.annotations.NotNull()
    java.lang.String imei2) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getCellPhoneNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getImei1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getImei2() {
        return null;
    }
    
    public PhoneItemEntry() {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component2() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component3() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.viewmodels.PhoneItemEntry copy(@org.jetbrains.annotations.NotNull()
    java.lang.String cellPhoneNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String serialNumber, @org.jetbrains.annotations.NotNull()
    java.lang.String imei1, @org.jetbrains.annotations.NotNull()
    java.lang.String imei2) {
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