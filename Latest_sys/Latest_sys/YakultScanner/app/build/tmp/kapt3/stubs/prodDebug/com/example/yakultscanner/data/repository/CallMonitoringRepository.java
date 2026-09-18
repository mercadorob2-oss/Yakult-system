package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.CallLookupResponse;
import com.example.yakultscanner.api.EscalationSettingsResponse;
import com.example.yakultscanner.api.SetEscalationOverrideRequest;
import com.example.yakultscanner.api.SetEscalationOverrideResponse;
import com.example.yakultscanner.api.CallTicketActionRequest;
import com.example.yakultscanner.api.CallTicketActionResponse;
import com.example.yakultscanner.api.CallTicketDetailResponse;
import com.example.yakultscanner.api.CallTicketListItem;
import com.example.yakultscanner.api.CallTicketListResponse;
import com.example.yakultscanner.api.CallTicketCreateResponse;
import com.example.yakultscanner.api.CreateTicketRequest;
import com.example.yakultscanner.api.CallDashboardResponse;
import com.example.yakultscanner.api.CallItemLookupResponse;
import com.example.yakultscanner.api.CallConditionResponse;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.ResolutionRequest;
import kotlinx.coroutines.Dispatchers;
import retrofit2.Response;
import javax.inject.Singleton;

@javax.inject.Singleton()
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u008c\u0001\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0004\n\u0002\u0010\b\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u000b\n\u0002\u0018\u0002\n\u0002\b\f\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0000\b\u0007\u0018\u00002\u00020\u0001B\u0007\u00a2\u0006\u0004\b\u0002\u0010\u0003JX\u0010\b\u001a\b\u0012\u0004\u0012\u00020\n0\t2\n\b\u0002\u0010\u000b\u001a\u0004\u0018\u00010\f2\n\b\u0002\u0010\r\u001a\u0004\u0018\u00010\f2\n\b\u0002\u0010\u000e\u001a\u0004\u0018\u00010\f2\n\b\u0002\u0010\u000f\u001a\u0004\u0018\u00010\f2\b\b\u0002\u0010\u0010\u001a\u00020\u00112\b\b\u0002\u0010\u0012\u001a\u00020\u0011H\u0086@\u00a2\u0006\u0002\u0010\u0013J\u001c\u0010\u0014\u001a\b\u0012\u0004\u0012\u00020\u00150\t2\u0006\u0010\u0016\u001a\u00020\u0011H\u0086@\u00a2\u0006\u0002\u0010\u0017J\u001c\u0010\u0018\u001a\b\u0012\u0004\u0012\u00020\u00190\t2\u0006\u0010\u001a\u001a\u00020\u001bH\u0086@\u00a2\u0006\u0002\u0010\u001cJ:\u0010\u001d\u001a\b\u0012\u0004\u0012\u00020\u001e0\t2\u0006\u0010\u0016\u001a\u00020\u00112\u0006\u0010\u001f\u001a\u00020\f2\b\b\u0002\u0010 \u001a\u00020\f2\n\b\u0002\u0010!\u001a\u0004\u0018\u00010\u0011H\u0086@\u00a2\u0006\u0002\u0010\"J<\u0010#\u001a\b\u0012\u0004\u0012\u00020\u001e0\t2\u0006\u0010\u0016\u001a\u00020\u00112\u0006\u0010$\u001a\u00020\f2\n\b\u0002\u0010%\u001a\u0004\u0018\u00010\f2\n\b\u0002\u0010!\u001a\u0004\u0018\u00010\u0011H\u0086@\u00a2\u0006\u0002\u0010\"J0\u0010&\u001a\b\u0012\u0004\u0012\u00020\u001e0\t2\u0006\u0010\u0016\u001a\u00020\u00112\u0006\u0010\'\u001a\u00020\f2\n\b\u0002\u0010!\u001a\u0004\u0018\u00010\u0011H\u0086@\u00a2\u0006\u0002\u0010(J\u0014\u0010)\u001a\b\u0012\u0004\u0012\u00020*0\tH\u0086@\u00a2\u0006\u0002\u0010+J \u0010,\u001a\b\u0012\u0004\u0012\u00020*0\t2\n\b\u0002\u0010-\u001a\u0004\u0018\u00010\u0011H\u0086@\u00a2\u0006\u0002\u0010.J,\u0010/\u001a\b\u0012\u0004\u0012\u00020*0\t2\n\b\u0002\u0010-\u001a\u0004\u0018\u00010\u00112\n\b\u0002\u00100\u001a\u0004\u0018\u00010\u0011H\u0086@\u00a2\u0006\u0002\u00101J4\u00102\u001a\b\u0012\u0004\u0012\u00020*0\t2\u0006\u00100\u001a\u00020\u00112\n\b\u0002\u0010-\u001a\u0004\u0018\u00010\u00112\n\b\u0002\u00103\u001a\u0004\u0018\u00010\u0011H\u0086@\u00a2\u0006\u0002\u00104J\u0014\u00105\u001a\b\u0012\u0004\u0012\u00020*0\tH\u0086@\u00a2\u0006\u0002\u0010+J\u0014\u00106\u001a\b\u0012\u0004\u0012\u0002070\tH\u0086@\u00a2\u0006\u0002\u0010+J\u001c\u00108\u001a\b\u0012\u0004\u0012\u0002090\t2\u0006\u0010\u001a\u001a\u00020:H\u0086@\u00a2\u0006\u0002\u0010;J\u001c\u0010<\u001a\b\u0012\u0004\u0012\u00020\u001e0\t2\u0006\u0010\u001a\u001a\u00020=H\u0086@\u00a2\u0006\u0002\u0010>J\u001c\u0010?\u001a\b\u0012\u0004\u0012\u00020@0\t2\u0006\u0010A\u001a\u00020\fH\u0086@\u00a2\u0006\u0002\u0010BJ\u0014\u0010C\u001a\b\u0012\u0004\u0012\u00020D0\tH\u0086@\u00a2\u0006\u0002\u0010+J\u0014\u0010E\u001a\b\u0012\u0004\u0012\u00020F0\tH\u0086@\u00a2\u0006\u0002\u0010+J\u001a\u0010G\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020I0H0\tH\u0086@\u00a2\u0006\u0002\u0010+R\u0014\u0010\u0004\u001a\u00020\u00058BX\u0082\u0004\u00a2\u0006\u0006\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006J"}, d2 = {"Lcom/example/yakultscanner/data/repository/CallMonitoringRepository;", "", "<init>", "()V", "apiService", "Lcom/example/yakultscanner/api/YakultApiService;", "getApiService", "()Lcom/example/yakultscanner/api/YakultApiService;", "getCallTickets", "Lretrofit2/Response;", "Lcom/example/yakultscanner/api/CallTicketListResponse;", "status", "", "search", "priority", "issueType", "page", "", "pageSize", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;IILkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getTicketDetail", "Lcom/example/yakultscanner/api/CallTicketDetailResponse;", "ticketId", "(ILkotlin/coroutines/Continuation;)Ljava/lang/Object;", "createTicket", "Lcom/example/yakultscanner/api/CallTicketCreateResponse;", "request", "Lcom/example/yakultscanner/api/CreateTicketRequest;", "(Lcom/example/yakultscanner/api/CreateTicketRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "addNote", "Lcom/example/yakultscanner/api/CallTicketActionResponse;", "noteText", "noteType", "userId", "(ILjava/lang/String;Ljava/lang/String;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "updateStatus", "newStatus", "note", "updatePriority", "newPriority", "(ILjava/lang/String;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCompanies", "Lcom/example/yakultscanner/api/CallLookupResponse;", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getDepartments", "comId", "(Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getBranches", "deptId", "(Ljava/lang/Integer;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getEmployees", "branchId", "(ILjava/lang/Integer;Ljava/lang/Integer;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getItEmployees", "getEscalationSettings", "Lcom/example/yakultscanner/api/EscalationSettingsResponse;", "setEscalationOverride", "Lcom/example/yakultscanner/api/SetEscalationOverrideResponse;", "Lcom/example/yakultscanner/api/SetEscalationOverrideRequest;", "(Lcom/example/yakultscanner/api/SetEscalationOverrideRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "applyResolution", "Lcom/example/yakultscanner/api/ResolutionRequest;", "(Lcom/example/yakultscanner/api/ResolutionRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallItemsLookup", "Lcom/example/yakultscanner/api/CallItemLookupResponse;", "type", "(Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getCallConditions", "Lcom/example/yakultscanner/api/CallConditionResponse;", "getDashboard", "Lcom/example/yakultscanner/api/CallDashboardResponse;", "getItemCategories", "", "Lcom/example/yakultscanner/api/ItemCategoryDto;", "app_prodDebug"})
public final class CallMonitoringRepository {
    
    public CallMonitoringRepository() {
        super();
    }
    
    private final com.example.yakultscanner.api.YakultApiService getApiService() {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getCallTickets(@org.jetbrains.annotations.Nullable()
    java.lang.String status, @org.jetbrains.annotations.Nullable()
    java.lang.String search, @org.jetbrains.annotations.Nullable()
    java.lang.String priority, @org.jetbrains.annotations.Nullable()
    java.lang.String issueType, int page, int pageSize, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketListResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getTicketDetail(int ticketId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketDetailResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object createTicket(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.CreateTicketRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketCreateResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object addNote(int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String noteText, @org.jetbrains.annotations.NotNull()
    java.lang.String noteType, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object updateStatus(int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String newStatus, @org.jetbrains.annotations.Nullable()
    java.lang.String note, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object updatePriority(int ticketId, @org.jetbrains.annotations.NotNull()
    java.lang.String newPriority, @org.jetbrains.annotations.Nullable()
    java.lang.Integer userId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getCompanies(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getDepartments(@org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getBranches(@org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer deptId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getEmployees(int deptId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer comId, @org.jetbrains.annotations.Nullable()
    java.lang.Integer branchId, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getItEmployees(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getEscalationSettings(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.EscalationSettingsResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object setEscalationOverride(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.SetEscalationOverrideRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.SetEscalationOverrideResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object applyResolution(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.ResolutionRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallTicketActionResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getCallItemsLookup(@org.jetbrains.annotations.NotNull()
    java.lang.String type, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallItemLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getCallConditions(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallConditionResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getDashboard(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.CallDashboardResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getItemCategories(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.ItemCategoryDto>>> $completion) {
        return null;
    }
}