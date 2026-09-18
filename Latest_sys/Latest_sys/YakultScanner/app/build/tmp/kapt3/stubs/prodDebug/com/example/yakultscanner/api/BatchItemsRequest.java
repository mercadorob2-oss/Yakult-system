package com.example.yakultscanner.api;

import com.google.gson.annotations.SerializedName;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00004\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0010\u0006\n\u0002\b3\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001B\u00a5\u0001\u0012\f\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003\u0012\u0006\u0010\u0005\u001a\u00020\u0006\u0012\u0006\u0010\u0007\u001a\u00020\u0006\u0012\b\u0010\b\u001a\u0004\u0018\u00010\u0006\u0012\b\u0010\t\u001a\u0004\u0018\u00010\n\u0012\b\u0010\u000b\u001a\u0004\u0018\u00010\u0006\u0012\u0006\u0010\f\u001a\u00020\u0006\u0012\b\u0010\r\u001a\u0004\u0018\u00010\u000e\u0012\b\u0010\u000f\u001a\u0004\u0018\u00010\u0006\u0012\b\u0010\u0010\u001a\u0004\u0018\u00010\u0006\u0012\b\u0010\u0011\u001a\u0004\u0018\u00010\n\u0012\b\u0010\u0012\u001a\u0004\u0018\u00010\n\u0012\b\u0010\u0013\u001a\u0004\u0018\u00010\n\u0012\b\u0010\u0014\u001a\u0004\u0018\u00010\u0006\u0012\b\u0010\u0015\u001a\u0004\u0018\u00010\u0006\u0012\b\u0010\u0016\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\u0004\b\u0017\u0010\u0018J\u000f\u0010/\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003H\u00c6\u0003J\t\u00100\u001a\u00020\u0006H\u00c6\u0003J\t\u00101\u001a\u00020\u0006H\u00c6\u0003J\u000b\u00102\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u0010\u00103\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010 J\u000b\u00104\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\t\u00105\u001a\u00020\u0006H\u00c6\u0003J\u0010\u00106\u001a\u0004\u0018\u00010\u000eH\u00c6\u0003\u00a2\u0006\u0002\u0010%J\u000b\u00107\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u00108\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u0010\u00109\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010 J\u0010\u0010:\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010 J\u0010\u0010;\u001a\u0004\u0018\u00010\nH\u00c6\u0003\u00a2\u0006\u0002\u0010 J\u000b\u0010<\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u0010=\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u000b\u0010>\u001a\u0004\u0018\u00010\u0006H\u00c6\u0003J\u00cc\u0001\u0010?\u001a\u00020\u00002\u000e\b\u0002\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00062\b\b\u0002\u0010\u0007\u001a\u00020\u00062\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00062\b\b\u0002\u0010\f\u001a\u00020\u00062\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u000e2\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0010\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\u0012\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\u0013\u001a\u0004\u0018\u00010\n2\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0015\u001a\u0004\u0018\u00010\u00062\n\b\u0002\u0010\u0016\u001a\u0004\u0018\u00010\u0006H\u00c6\u0001\u00a2\u0006\u0002\u0010@J\u0013\u0010A\u001a\u00020B2\b\u0010C\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010D\u001a\u00020\nH\u00d6\u0001J\t\u0010E\u001a\u00020\u0006H\u00d6\u0001R\u0017\u0010\u0002\u001a\b\u0012\u0004\u0012\u00020\u00040\u0003\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u001aR\u0011\u0010\u0005\u001a\u00020\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u001cR\u0011\u0010\u0007\u001a\u00020\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u001cR\u0013\u0010\b\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001e\u0010\u001cR\u0015\u0010\t\u001a\u0004\u0018\u00010\n\u00a2\u0006\n\n\u0002\u0010!\u001a\u0004\b\u001f\u0010 R\u0013\u0010\u000b\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\"\u0010\u001cR\u0011\u0010\f\u001a\u00020\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010\u001cR\u0015\u0010\r\u001a\u0004\u0018\u00010\u000e\u00a2\u0006\n\n\u0002\u0010&\u001a\u0004\b$\u0010%R\u0013\u0010\u000f\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b\'\u0010\u001cR\u0013\u0010\u0010\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b(\u0010\u001cR\u0015\u0010\u0011\u001a\u0004\u0018\u00010\n\u00a2\u0006\n\n\u0002\u0010!\u001a\u0004\b)\u0010 R\u0015\u0010\u0012\u001a\u0004\u0018\u00010\n\u00a2\u0006\n\n\u0002\u0010!\u001a\u0004\b*\u0010 R\u0015\u0010\u0013\u001a\u0004\u0018\u00010\n\u00a2\u0006\n\n\u0002\u0010!\u001a\u0004\b+\u0010 R\u0013\u0010\u0014\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b,\u0010\u001cR\u0013\u0010\u0015\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b-\u0010\u001cR\u0013\u0010\u0016\u001a\u0004\u0018\u00010\u0006\u00a2\u0006\b\n\u0000\u001a\u0004\b.\u0010\u001c\u00a8\u0006F"}, d2 = {"Lcom/example/yakultscanner/api/BatchItemsRequest;", "", "items", "", "Lcom/example/yakultscanner/api/BatchItemRequest;", "createdBy", "", "itemName", "description", "categoryId", "", "categoryName", "unitOfMeasure", "amount", "", "startDate", "endDate", "conditionId", "vendorId", "warrantyYears", "datePurchased", "licenseNumber", "remarks", "<init>", "(Ljava/util/List;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Double;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V", "getItems", "()Ljava/util/List;", "getCreatedBy", "()Ljava/lang/String;", "getItemName", "getDescription", "getCategoryId", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getCategoryName", "getUnitOfMeasure", "getAmount", "()Ljava/lang/Double;", "Ljava/lang/Double;", "getStartDate", "getEndDate", "getConditionId", "getVendorId", "getWarrantyYears", "getDatePurchased", "getLicenseNumber", "getRemarks", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "component13", "component14", "component15", "component16", "copy", "(Ljava/util/List;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Double;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)Lcom/example/yakultscanner/api/BatchItemsRequest;", "equals", "", "other", "hashCode", "toString", "app_prodDebug"})
public final class BatchItemsRequest {
    @org.jetbrains.annotations.NotNull()
    private final java.util.List<com.example.yakultscanner.api.BatchItemRequest> items = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String createdBy = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String itemName = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String description = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer categoryId = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String categoryName = null;
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String unitOfMeasure = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Double amount = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String startDate = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String endDate = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer conditionId = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer vendorId = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer warrantyYears = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String datePurchased = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String licenseNumber = null;
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String remarks = null;
    
    public BatchItemsRequest(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BatchItemRequest> items, @org.jetbrains.annotations.NotNull()
    java.lang.String createdBy, @org.jetbrains.annotations.NotNull()
    java.lang.String itemName, @org.jetbrains.annotations.Nullable()
    java.lang.String description, @org.jetbrains.annotations.Nullable()
    java.lang.Integer categoryId, @org.jetbrains.annotations.Nullable()
    java.lang.String categoryName, @org.jetbrains.annotations.NotNull()
    java.lang.String unitOfMeasure, @org.jetbrains.annotations.Nullable()
    java.lang.Double amount, @org.jetbrains.annotations.Nullable()
    java.lang.String startDate, @org.jetbrains.annotations.Nullable()
    java.lang.String endDate, @org.jetbrains.annotations.Nullable()
    java.lang.Integer conditionId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer vendorId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer warrantyYears, @org.jetbrains.annotations.Nullable()
    java.lang.String datePurchased, @org.jetbrains.annotations.Nullable()
    java.lang.String licenseNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String remarks) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.BatchItemRequest> getItems() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getCreatedBy() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getItemName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDescription() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getCategoryId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCategoryName() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getUnitOfMeasure() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Double getAmount() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getStartDate() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getEndDate() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getConditionId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getVendorId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getWarrantyYears() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getDatePurchased() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLicenseNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getRemarks() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.util.List<com.example.yakultscanner.api.BatchItemRequest> component1() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component10() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component11() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component12() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component13() {
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
    public final java.lang.String component16() {
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
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component6() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Double component8() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.BatchItemsRequest copy(@org.jetbrains.annotations.NotNull()
    java.util.List<com.example.yakultscanner.api.BatchItemRequest> items, @org.jetbrains.annotations.NotNull()
    java.lang.String createdBy, @org.jetbrains.annotations.NotNull()
    java.lang.String itemName, @org.jetbrains.annotations.Nullable()
    java.lang.String description, @org.jetbrains.annotations.Nullable()
    java.lang.Integer categoryId, @org.jetbrains.annotations.Nullable()
    java.lang.String categoryName, @org.jetbrains.annotations.NotNull()
    java.lang.String unitOfMeasure, @org.jetbrains.annotations.Nullable()
    java.lang.Double amount, @org.jetbrains.annotations.Nullable()
    java.lang.String startDate, @org.jetbrains.annotations.Nullable()
    java.lang.String endDate, @org.jetbrains.annotations.Nullable()
    java.lang.Integer conditionId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer vendorId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer warrantyYears, @org.jetbrains.annotations.Nullable()
    java.lang.String datePurchased, @org.jetbrains.annotations.Nullable()
    java.lang.String licenseNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String remarks) {
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