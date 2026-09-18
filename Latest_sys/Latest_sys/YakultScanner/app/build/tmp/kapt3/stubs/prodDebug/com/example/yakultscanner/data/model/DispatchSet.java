package com.example.yakultscanner.data.model;

import com.google.gson.annotations.SerializedName;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\n\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\b\n\u0002\b!\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001B\u0087\u0001\u0012\b\u0010\u0002\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\u0004\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\u0005\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\u0006\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\u0007\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\b\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\t\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\n\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\u000b\u001a\u0004\u0018\u00010\u0003\u0012\b\u0010\f\u001a\u0004\u0018\u00010\u0003\u0012\u000e\u0010\r\u001a\n\u0012\u0004\u0012\u00020\u000f\u0018\u00010\u000e\u0012\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u0011\u00a2\u0006\u0004\b\u0012\u0010\u0013J\u000b\u0010$\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010%\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010&\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010\'\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010(\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010)\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010*\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010+\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010,\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u000b\u0010-\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\u0011\u0010.\u001a\n\u0012\u0004\u0012\u00020\u000f\u0018\u00010\u000eH\u00c6\u0003J\u0010\u0010/\u001a\u0004\u0018\u00010\u0011H\u00c6\u0003\u00a2\u0006\u0002\u0010\"J\u00a4\u0001\u00100\u001a\u00020\u00002\n\b\u0002\u0010\u0002\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0005\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00032\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u00032\u0010\b\u0002\u0010\r\u001a\n\u0012\u0004\u0012\u00020\u000f\u0018\u00010\u000e2\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u0011H\u00c6\u0001\u00a2\u0006\u0002\u00101J\u0013\u00102\u001a\u0002032\b\u00104\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u00105\u001a\u00020\u0011H\u00d6\u0001J\t\u00106\u001a\u00020\u0003H\u00d6\u0001R\u0018\u0010\u0002\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0015R\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0016\u0010\u0015R\u0018\u0010\u0005\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0015R\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0018\u0010\u0015R\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u0015R\u0018\u0010\b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001a\u0010\u0015R\u0018\u0010\t\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u0015R\u0018\u0010\n\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u0015R\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u0015R\u0018\u0010\f\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001e\u0010\u0015R\u001e\u0010\r\u001a\n\u0012\u0004\u0012\u00020\u000f\u0018\u00010\u000e8\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001f\u0010 R\u001a\u0010\u0010\u001a\u0004\u0018\u00010\u00118\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010#\u001a\u0004\b!\u0010\"\u00a8\u00067"}, d2 = {"Lcom/example/yakultscanner/data/model/DispatchSet;", "", "setCode", "", "employee", "department", "branch", "status", "createdBy", "company", "logistics", "dispatchDate", "createdDate", "items", "", "Lcom/example/yakultscanner/data/model/DispatchItem;", "imageCount", "", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/util/List;Ljava/lang/Integer;)V", "getSetCode", "()Ljava/lang/String;", "getEmployee", "getDepartment", "getBranch", "getStatus", "getCreatedBy", "getCompany", "getLogistics", "getDispatchDate", "getCreatedDate", "getItems", "()Ljava/util/List;", "getImageCount", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "copy", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/util/List;Ljava/lang/Integer;)Lcom/example/yakultscanner/data/model/DispatchSet;", "equals", "", "other", "hashCode", "toString", "app_prodDebug"})
public final class DispatchSet {
    @com.google.gson.annotations.SerializedName(value = "set_code")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String setCode = null;
    @com.google.gson.annotations.SerializedName(value = "employee_name")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String employee = null;
    @com.google.gson.annotations.SerializedName(value = "department")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String department = null;
    @com.google.gson.annotations.SerializedName(value = "branch")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String branch = null;
    @com.google.gson.annotations.SerializedName(value = "status")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String status = null;
    @com.google.gson.annotations.SerializedName(value = "created_by")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String createdBy = null;
    @com.google.gson.annotations.SerializedName(value = "company")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String company = null;
    @com.google.gson.annotations.SerializedName(value = "logistics_provider")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String logistics = null;
    @com.google.gson.annotations.SerializedName(value = "Request_date")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String dispatchDate = null;
    @com.google.gson.annotations.SerializedName(value = "created_date")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String createdDate = null;
    @com.google.gson.annotations.SerializedName(value = "items")
    @org.jetbrains.annotations.Nullable()
    private final java.util.List<com.example.yakultscanner.data.model.DispatchItem> items = null;
    @com.google.gson.annotations.SerializedName(value = "image_count")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer imageCount = null;
    
    public DispatchSet(@org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.String employee, @org.jetbrains.annotations.Nullable()
    java.lang.String department, @org.jetbrains.annotations.Nullable()
    java.lang.String branch, @org.jetbrains.annotations.Nullable()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    java.lang.String createdBy, @org.jetbrains.annotations.Nullable()
    java.lang.String company, @org.jetbrains.annotations.Nullable()
    java.lang.String logistics, @org.jetbrains.annotations.Nullable()
    java.lang.String dispatchDate, @org.jetbrains.annotations.Nullable()
    java.lang.String createdDate, @org.jetbrains.annotations.Nullable()
    java.util.List<com.example.yakultscanner.data.model.DispatchItem> items, @org.jetbrains.annotations.Nullable()
    java.lang.Integer imageCount) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSetCode() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getEmployee() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDepartment() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getBranch() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCreatedBy() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCompany() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLogistics() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDispatchDate() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCreatedDate() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.util.List<com.example.yakultscanner.data.model.DispatchItem> getItems() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getImageCount() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component10() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.util.List<com.example.yakultscanner.data.model.DispatchItem> component11() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component12() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component2() {
        return null;
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
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component8() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.model.DispatchSet copy(@org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.String employee, @org.jetbrains.annotations.Nullable()
    java.lang.String department, @org.jetbrains.annotations.Nullable()
    java.lang.String branch, @org.jetbrains.annotations.Nullable()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    java.lang.String createdBy, @org.jetbrains.annotations.Nullable()
    java.lang.String company, @org.jetbrains.annotations.Nullable()
    java.lang.String logistics, @org.jetbrains.annotations.Nullable()
    java.lang.String dispatchDate, @org.jetbrains.annotations.Nullable()
    java.lang.String createdDate, @org.jetbrains.annotations.Nullable()
    java.util.List<com.example.yakultscanner.data.model.DispatchItem> items, @org.jetbrains.annotations.Nullable()
    java.lang.Integer imageCount) {
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