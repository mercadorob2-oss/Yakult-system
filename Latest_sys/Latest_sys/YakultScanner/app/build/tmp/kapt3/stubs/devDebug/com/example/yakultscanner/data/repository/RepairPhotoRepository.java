package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.RepairPartPhotoUploadRequest;
import com.example.yakultscanner.api.RepairPartPhotoUploadResponse;
import com.example.yakultscanner.api.RepairTicketLookupResponse;
import com.example.yakultscanner.api.YakultApiService;
import retrofit2.Response;
import javax.inject.Inject;

/**
 * Repository for the mobile Repair Part photo upload flow — resolving a scanned/typed Repair
 * Ticket to its Parts list, then uploading evidence photos straight from the phone instead of
 * copying files to a PC first.
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u00002\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0010\u000e\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J(\u0010\u0006\u001a\b\u0012\u0004\u0012\u00020\b0\u00072\b\u0010\t\u001a\u0004\u0018\u00010\n2\b\u0010\u000b\u001a\u0004\u0018\u00010\nH\u0086@\u00a2\u0006\u0002\u0010\fJ\u001c\u0010\r\u001a\b\u0012\u0004\u0012\u00020\u000e0\u00072\u0006\u0010\u000f\u001a\u00020\u0010H\u0086@\u00a2\u0006\u0002\u0010\u0011R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u0012"}, d2 = {"Lcom/example/yakultscanner/data/repository/RepairPhotoRepository;", "", "apiService", "Lcom/example/yakultscanner/api/YakultApiService;", "<init>", "(Lcom/example/yakultscanner/api/YakultApiService;)V", "lookupRepairTicket", "Lretrofit2/Response;", "Lcom/example/yakultscanner/api/RepairTicketLookupResponse;", "ticketCode", "", "token", "(Ljava/lang/String;Ljava/lang/String;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "uploadRepairPartPhoto", "Lcom/example/yakultscanner/api/RepairPartPhotoUploadResponse;", "request", "Lcom/example/yakultscanner/api/RepairPartPhotoUploadRequest;", "(Lcom/example/yakultscanner/api/RepairPartPhotoUploadRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "app_devDebug"})
public final class RepairPhotoRepository {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.api.YakultApiService apiService = null;
    
    @javax.inject.Inject()
    public RepairPhotoRepository(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.YakultApiService apiService) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object lookupRepairTicket(@org.jetbrains.annotations.Nullable()
    java.lang.String ticketCode, @org.jetbrains.annotations.Nullable()
    java.lang.String token, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.RepairTicketLookupResponse>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object uploadRepairPartPhoto(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.RepairPartPhotoUploadRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.RepairPartPhotoUploadResponse>> $completion) {
        return null;
    }
}