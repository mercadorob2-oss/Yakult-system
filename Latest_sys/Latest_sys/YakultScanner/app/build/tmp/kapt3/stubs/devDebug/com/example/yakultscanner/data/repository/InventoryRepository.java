package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.BatchItemsRequest;
import com.example.yakultscanner.api.BatchItemsResponse;
import com.example.yakultscanner.api.ConditionDto;
import com.example.yakultscanner.api.ItemCategoryDto;
import com.example.yakultscanner.api.VendorDto;
import com.example.yakultscanner.api.YakultApiService;
import retrofit2.Response;
import javax.inject.Inject;

/**
 * Repository to handle all inventory-related operations.
 * Separates data fetching logic from ViewModels.
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000<\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0004\n\u0002\u0018\u0002\n\u0002\u0010 \n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\u0018\u00002\u00020\u0001B\u0011\b\u0007\u0012\u0006\u0010\u0002\u001a\u00020\u0003\u00a2\u0006\u0004\b\u0004\u0010\u0005J\u001a\u0010\u0007\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\n0\t0\bH\u0086@\u00a2\u0006\u0002\u0010\u000bJ\u001a\u0010\f\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\r0\t0\bH\u0086@\u00a2\u0006\u0002\u0010\u000bJ\u001a\u0010\u000e\u001a\u000e\u0012\n\u0012\b\u0012\u0004\u0012\u00020\u000f0\t0\bH\u0086@\u00a2\u0006\u0002\u0010\u000bJ\u001c\u0010\u0010\u001a\b\u0012\u0004\u0012\u00020\u00110\b2\u0006\u0010\u0012\u001a\u00020\u0013H\u0086@\u00a2\u0006\u0002\u0010\u0014R\u000e\u0010\u0002\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000R\u000e\u0010\u0006\u001a\u00020\u0003X\u0082\u0004\u00a2\u0006\u0002\n\u0000\u00a8\u0006\u0015"}, d2 = {"Lcom/example/yakultscanner/data/repository/InventoryRepository;", "", "apiService", "Lcom/example/yakultscanner/api/YakultApiService;", "<init>", "(Lcom/example/yakultscanner/api/YakultApiService;)V", "service", "getItemCategories", "Lretrofit2/Response;", "", "Lcom/example/yakultscanner/api/ItemCategoryDto;", "(Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "getConditions", "Lcom/example/yakultscanner/api/ConditionDto;", "getVendors", "Lcom/example/yakultscanner/api/VendorDto;", "createBatchItems", "Lcom/example/yakultscanner/api/BatchItemsResponse;", "request", "Lcom/example/yakultscanner/api/BatchItemsRequest;", "(Lcom/example/yakultscanner/api/BatchItemsRequest;Lkotlin/coroutines/Continuation;)Ljava/lang/Object;", "app_devDebug"})
public final class InventoryRepository {
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.api.YakultApiService apiService = null;
    @org.jetbrains.annotations.NotNull()
    private final com.example.yakultscanner.api.YakultApiService service = null;
    
    @javax.inject.Inject()
    public InventoryRepository(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.YakultApiService apiService) {
        super();
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getItemCategories(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.ItemCategoryDto>>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getConditions(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.ConditionDto>>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object getVendors(@org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<java.util.List<com.example.yakultscanner.api.VendorDto>>> $completion) {
        return null;
    }
    
    @org.jetbrains.annotations.Nullable()
    public final java.lang.Object createBatchItems(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.BatchItemsRequest request, @org.jetbrains.annotations.NotNull()
    kotlin.coroutines.Continuation<? super retrofit2.Response<com.example.yakultscanner.api.BatchItemsResponse>> $completion) {
        return null;
    }
}