package com.example.yakultscanner.api;

import com.google.gson.annotations.SerializedName;

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\b\n\u0000\n\u0002\u0010\u000e\n\u0002\b\n\n\u0002\u0010\u000b\n\u0002\b/\b\u0086\b\u0018\u00002\u00020\u0001B\u00a1\u0001\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\b\u0010\u0004\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\u0006\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\u0007\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\b\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\t\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\n\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\u000b\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\f\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\r\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\u000e\u001a\u0004\u0018\u00010\u0005\u0012\u0006\u0010\u000f\u001a\u00020\u0010\u0012\b\u0010\u0011\u001a\u0004\u0018\u00010\u0005\u0012\b\u0010\u0012\u001a\u0004\u0018\u00010\u0005\u0012\u0006\u0010\u0013\u001a\u00020\u0003\u0012\b\u0010\u0014\u001a\u0004\u0018\u00010\u0005\u00a2\u0006\u0004\b\u0015\u0010\u0016J\t\u0010*\u001a\u00020\u0003H\u00c6\u0003J\u000b\u0010+\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010,\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010-\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010.\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u0010/\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u00100\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u00101\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u00102\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u00103\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u00104\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\t\u00105\u001a\u00020\u0010H\u00c6\u0003J\u000b\u00106\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u000b\u00107\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\t\u00108\u001a\u00020\u0003H\u00c6\u0003J\u000b\u00109\u001a\u0004\u0018\u00010\u0005H\u00c6\u0003J\u00c3\u0001\u0010:\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\n\b\u0002\u0010\u0004\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0007\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\f\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u00052\b\b\u0002\u0010\u000f\u001a\u00020\u00102\n\b\u0002\u0010\u0011\u001a\u0004\u0018\u00010\u00052\n\b\u0002\u0010\u0012\u001a\u0004\u0018\u00010\u00052\b\b\u0002\u0010\u0013\u001a\u00020\u00032\n\b\u0002\u0010\u0014\u001a\u0004\u0018\u00010\u0005H\u00c6\u0001J\u0013\u0010;\u001a\u00020\u00102\b\u0010<\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u0010=\u001a\u00020\u0003H\u00d6\u0001J\t\u0010>\u001a\u00020\u0005H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0017\u0010\u0018R\u0018\u0010\u0004\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0019\u0010\u001aR\u0018\u0010\u0006\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u001aR\u0018\u0010\u0007\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u001aR\u0018\u0010\b\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u001aR\u0018\u0010\t\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001e\u0010\u001aR\u0018\u0010\n\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001f\u0010\u001aR\u0018\u0010\u000b\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b \u0010\u001aR\u0018\u0010\f\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b!\u0010\u001aR\u0018\u0010\r\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\"\u0010\u001aR\u0018\u0010\u000e\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b#\u0010\u001aR\u0016\u0010\u000f\u001a\u00020\u00108\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b$\u0010%R\u0018\u0010\u0011\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b&\u0010\u001aR\u0018\u0010\u0012\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\'\u0010\u001aR\u0016\u0010\u0013\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b(\u0010\u0018R\u0018\u0010\u0014\u001a\u0004\u0018\u00010\u00058\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b)\u0010\u001a\u00a8\u0006?"}, d2 = {"Lcom/example/yakultscanner/api/SetItemUpdateDto;", "", "updateId", "", "setCode", "", "serialNumber", "modelNumber", "previousStatus", "newStatus", "remark", "updatedByUserId", "updatedByName", "source", "createdAt", "processed", "", "processedBy", "processedAt", "repairCount", "lastRepairAction", "<init>", "(ILjava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;ZLjava/lang/String;Ljava/lang/String;ILjava/lang/String;)V", "getUpdateId", "()I", "getSetCode", "()Ljava/lang/String;", "getSerialNumber", "getModelNumber", "getPreviousStatus", "getNewStatus", "getRemark", "getUpdatedByUserId", "getUpdatedByName", "getSource", "getCreatedAt", "getProcessed", "()Z", "getProcessedBy", "getProcessedAt", "getRepairCount", "getLastRepairAction", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "component13", "component14", "component15", "component16", "copy", "equals", "other", "hashCode", "toString", "app_devDebug"})
public final class SetItemUpdateDto {
    @com.google.gson.annotations.SerializedName(value = "UpdateId")
    private final int updateId = 0;
    @com.google.gson.annotations.SerializedName(value = "SetCode")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String setCode = null;
    @com.google.gson.annotations.SerializedName(value = "SerialNumber")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String serialNumber = null;
    @com.google.gson.annotations.SerializedName(value = "ModelNumber")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String modelNumber = null;
    @com.google.gson.annotations.SerializedName(value = "PreviousStatus")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String previousStatus = null;
    @com.google.gson.annotations.SerializedName(value = "NewStatus")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String newStatus = null;
    @com.google.gson.annotations.SerializedName(value = "Remark")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String remark = null;
    @com.google.gson.annotations.SerializedName(value = "UpdatedByUserId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String updatedByUserId = null;
    @com.google.gson.annotations.SerializedName(value = "UpdatedByName")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String updatedByName = null;
    @com.google.gson.annotations.SerializedName(value = "Source")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String source = null;
    @com.google.gson.annotations.SerializedName(value = "CreatedAt")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String createdAt = null;
    @com.google.gson.annotations.SerializedName(value = "Processed")
    private final boolean processed = false;
    @com.google.gson.annotations.SerializedName(value = "ProcessedBy")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String processedBy = null;
    @com.google.gson.annotations.SerializedName(value = "ProcessedAt")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String processedAt = null;
    @com.google.gson.annotations.SerializedName(value = "RepairCount")
    private final int repairCount = 0;
    @com.google.gson.annotations.SerializedName(value = "LastRepairAction")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String lastRepairAction = null;
    
    public SetItemUpdateDto(int updateId, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String modelNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String previousStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String newStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String remark, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByName, @org.jetbrains.annotations.Nullable()
    java.lang.String source, @org.jetbrains.annotations.Nullable()
    java.lang.String createdAt, boolean processed, @org.jetbrains.annotations.Nullable()
    java.lang.String processedBy, @org.jetbrains.annotations.Nullable()
    java.lang.String processedAt, int repairCount, @org.jetbrains.annotations.Nullable()
    java.lang.String lastRepairAction) {
        super();
    }
    
    public final int getUpdateId() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSetCode() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSerialNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getModelNumber() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getPreviousStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getNewStatus() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getRemark() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUpdatedByUserId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getUpdatedByName() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getSource() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getCreatedAt() {
        return null;
    }
    
    public final boolean getProcessed() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getProcessedBy() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getProcessedAt() {
        return null;
    }
    
    public final int getRepairCount() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getLastRepairAction() {
        return null;
    }
    
    public final int component1() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component10() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component11() {
        return null;
    }
    
    public final boolean component12() {
        return false;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component13() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component14() {
        return null;
    }
    
    public final int component15() {
        return 0;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component16() {
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
    public final com.example.yakultscanner.api.SetItemUpdateDto copy(int updateId, @org.jetbrains.annotations.Nullable()
    java.lang.String setCode, @org.jetbrains.annotations.Nullable()
    java.lang.String serialNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String modelNumber, @org.jetbrains.annotations.Nullable()
    java.lang.String previousStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String newStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String remark, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByUserId, @org.jetbrains.annotations.Nullable()
    java.lang.String updatedByName, @org.jetbrains.annotations.Nullable()
    java.lang.String source, @org.jetbrains.annotations.Nullable()
    java.lang.String createdAt, boolean processed, @org.jetbrains.annotations.Nullable()
    java.lang.String processedBy, @org.jetbrains.annotations.Nullable()
    java.lang.String processedAt, int repairCount, @org.jetbrains.annotations.Nullable()
    java.lang.String lastRepairAction) {
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