package com.example.yakultscanner.data.repository

import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.BatchItemsRequest
import com.example.yakultscanner.api.BatchItemsResponse
import com.example.yakultscanner.api.ConditionDto
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.VendorDto
import com.example.yakultscanner.api.YakultApiService
import retrofit2.Response
import javax.inject.Inject

/**
 * Repository to handle all inventory-related operations.
 * Separates data fetching logic from ViewModels.
 */
class InventoryRepository @Inject constructor(
    private val apiService: YakultApiService
) {

    private val service = apiService

    suspend fun getItemCategories(): Response<List<ItemCategoryDto>> {
        return service.getItemCategories()
    }

    suspend fun getConditions(): Response<List<ConditionDto>> {
        return service.getConditions()
    }

    suspend fun getVendors(): Response<List<VendorDto>> {
        return service.getVendors()
    }

    suspend fun createBatchItems(request: BatchItemsRequest): Response<BatchItemsResponse> {
        return service.createBatchItems(request)
    }
}
