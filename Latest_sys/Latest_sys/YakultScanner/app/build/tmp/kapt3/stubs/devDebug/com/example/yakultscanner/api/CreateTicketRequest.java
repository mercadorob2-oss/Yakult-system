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

@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\"\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0010\b\n\u0002\b(\n\u0002\u0010\u000b\n\u0002\b\u0004\b\u0086\b\u0018\u00002\u00020\u0001B\u0085\u0001\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u0012\u0006\u0010\u0004\u001a\u00020\u0003\u0012\u0006\u0010\u0005\u001a\u00020\u0003\u0012\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u0007\u0012\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u0007\u0012\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u0007\u0012\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u0003\u0012\b\b\u0002\u0010\u000b\u001a\u00020\u0003\u0012\b\b\u0002\u0010\f\u001a\u00020\u0003\u0012\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u0007\u0012\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u0007\u0012\b\b\u0002\u0010\u000f\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0010\u0010\u0011J\t\u0010!\u001a\u00020\u0003H\u00c6\u0003J\t\u0010\"\u001a\u00020\u0003H\u00c6\u0003J\t\u0010#\u001a\u00020\u0003H\u00c6\u0003J\u0010\u0010$\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0017J\u0010\u0010%\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0017J\u0010\u0010&\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0017J\u000b\u0010\'\u001a\u0004\u0018\u00010\u0003H\u00c6\u0003J\t\u0010(\u001a\u00020\u0003H\u00c6\u0003J\t\u0010)\u001a\u00020\u0003H\u00c6\u0003J\u0010\u0010*\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0017J\u0010\u0010+\u001a\u0004\u0018\u00010\u0007H\u00c6\u0003\u00a2\u0006\u0002\u0010\u0017J\t\u0010,\u001a\u00020\u0003H\u00c6\u0003J\u0092\u0001\u0010-\u001a\u00020\u00002\b\b\u0002\u0010\u0002\u001a\u00020\u00032\b\b\u0002\u0010\u0004\u001a\u00020\u00032\b\b\u0002\u0010\u0005\u001a\u00020\u00032\n\b\u0002\u0010\u0006\u001a\u0004\u0018\u00010\u00072\n\b\u0002\u0010\b\u001a\u0004\u0018\u00010\u00072\n\b\u0002\u0010\t\u001a\u0004\u0018\u00010\u00072\n\b\u0002\u0010\n\u001a\u0004\u0018\u00010\u00032\b\b\u0002\u0010\u000b\u001a\u00020\u00032\b\b\u0002\u0010\f\u001a\u00020\u00032\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\u00072\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\u00072\b\b\u0002\u0010\u000f\u001a\u00020\u0003H\u00c6\u0001\u00a2\u0006\u0002\u0010.J\u0013\u0010/\u001a\u0002002\b\u00101\u001a\u0004\u0018\u00010\u0001H\u00d6\u0003J\t\u00102\u001a\u00020\u0007H\u00d6\u0001J\t\u00103\u001a\u00020\u0003H\u00d6\u0001R\u0016\u0010\u0002\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0012\u0010\u0013R\u0016\u0010\u0004\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0014\u0010\u0013R\u0016\u0010\u0005\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0015\u0010\u0013R\u001a\u0010\u0006\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0018\u001a\u0004\b\u0016\u0010\u0017R\u001a\u0010\b\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0018\u001a\u0004\b\u0019\u0010\u0017R\u001a\u0010\t\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0018\u001a\u0004\b\u001a\u0010\u0017R\u0018\u0010\n\u001a\u0004\u0018\u00010\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001b\u0010\u0013R\u0016\u0010\u000b\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001c\u0010\u0013R\u0016\u0010\f\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b\u001d\u0010\u0013R\u001a\u0010\r\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0018\u001a\u0004\b\u001e\u0010\u0017R\u001a\u0010\u000e\u001a\u0004\u0018\u00010\u00078\u0006X\u0087\u0004\u00a2\u0006\n\n\u0002\u0010\u0018\u001a\u0004\b\u001f\u0010\u0017R\u0016\u0010\u000f\u001a\u00020\u00038\u0006X\u0087\u0004\u00a2\u0006\b\n\u0000\u001a\u0004\b \u0010\u0013\u00a8\u00064"}, d2 = {"Lcom/example/yakultscanner/api/CreateTicketRequest;", "", "company", "", "callerName", "issue", "comId", "", "deptId", "branchId", "providedSolution", "issueType", "priority", "assignedToEmpId", "createdByUserId", "ticketSource", "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;)V", "getCompany", "()Ljava/lang/String;", "getCallerName", "getIssue", "getComId", "()Ljava/lang/Integer;", "Ljava/lang/Integer;", "getDeptId", "getBranchId", "getProvidedSolution", "getIssueType", "getPriority", "getAssignedToEmpId", "getCreatedByUserId", "getTicketSource", "component1", "component2", "component3", "component4", "component5", "component6", "component7", "component8", "component9", "component10", "component11", "component12", "copy", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Ljava/lang/Integer;Ljava/lang/String;)Lcom/example/yakultscanner/api/CreateTicketRequest;", "equals", "", "other", "hashCode", "toString", "app_devDebug"})
public final class CreateTicketRequest {
    @com.google.gson.annotations.SerializedName(value = "company")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String company = null;
    @com.google.gson.annotations.SerializedName(value = "callerName")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String callerName = null;
    @com.google.gson.annotations.SerializedName(value = "issue")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String issue = null;
    @com.google.gson.annotations.SerializedName(value = "comId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer comId = null;
    @com.google.gson.annotations.SerializedName(value = "deptId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer deptId = null;
    @com.google.gson.annotations.SerializedName(value = "branchId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer branchId = null;
    @com.google.gson.annotations.SerializedName(value = "providedSolution")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.String providedSolution = null;
    @com.google.gson.annotations.SerializedName(value = "issueType")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String issueType = null;
    @com.google.gson.annotations.SerializedName(value = "priority")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String priority = null;
    @com.google.gson.annotations.SerializedName(value = "assignedToEmpId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer assignedToEmpId = null;
    @com.google.gson.annotations.SerializedName(value = "createdByUserId")
    @org.jetbrains.annotations.Nullable()
    private final java.lang.Integer createdByUserId = null;
    @com.google.gson.annotations.SerializedName(value = "ticketSource")
    @org.jetbrains.annotations.NotNull()
    private final java.lang.String ticketSource = null;
    
    public CreateTicketRequest(@org.jetbrains.annotations.NotNull()
    java.lang.String company, @org.jetbrains.annotations.NotNull()
    java.lang.String callerName, @org.jetbrains.annotations.NotNull()
    java.lang.String issue, @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer deptId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer branchId, @org.jetbrains.annotations.Nullable()
    java.lang.String providedSolution, @org.jetbrains.annotations.NotNull()
    java.lang.String issueType, @org.jetbrains.annotations.NotNull()
    java.lang.String priority, @org.jetbrains.annotations.Nullable()
    java.lang.Integer assignedToEmpId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer createdByUserId, @org.jetbrains.annotations.NotNull()
    java.lang.String ticketSource) {
        super();
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getCompany() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getCallerName() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getIssue() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getComId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getDeptId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getBranchId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String getProvidedSolution() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getIssueType() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getPriority() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getAssignedToEmpId() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer getCreatedByUserId() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String getTicketSource() {
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
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component12() {
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
    public final java.lang.Integer component4() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component5() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Integer component6() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.String component7() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component8() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final java.lang.String component9() {
        return null;
    }
    
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.CreateTicketRequest copy(@org.jetbrains.annotations.NotNull()
    java.lang.String company, @org.jetbrains.annotations.NotNull()
    java.lang.String callerName, @org.jetbrains.annotations.NotNull()
    java.lang.String issue, @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer deptId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer branchId, @org.jetbrains.annotations.Nullable()
    java.lang.String providedSolution, @org.jetbrains.annotations.NotNull()
    java.lang.String issueType, @org.jetbrains.annotations.NotNull()
    java.lang.String priority, @org.jetbrains.annotations.Nullable()
    java.lang.Integer assignedToEmpId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer createdByUserId, @org.jetbrains.annotations.NotNull()
    java.lang.String ticketSource) {
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