package com.example.yakultscanner.api

import com.google.gson.annotations.SerializedName

// Kotlin data classes that match the C# DTOs in Yakult.Inventory.Api

data class SetItemUpdateEntry(
    val itemSerialNumber: String,
    val itemModelNumber: String?,
    val previousStatus: String?,
    val newStatus: String?,
    val remark: String?,
    val itemType: String?
)

data class SetUpdatesRequest(
    val setCode: String,
    val updatedByUser: String,
    val updates: List<SetItemUpdateEntry>
)

data class SetUpdatesResponse(
    val success: Boolean,
    val message: String,
    val insertedCount: Int
)

data class PendingIssuesResponse(
    val pendingCount: Int,
    val processedCount: Int
)

// Response DTO for reading rows from dbo.SetItemUpdate
data class SetItemUpdateDto(
    @SerializedName("UpdateId") val updateId: Int,
    @SerializedName("SetCode") val setCode: String?,
    @SerializedName("SerialNumber") val serialNumber: String?,
    @SerializedName("ModelNumber") val modelNumber: String?,
    @SerializedName("PreviousStatus") val previousStatus: String?,
    @SerializedName("NewStatus") val newStatus: String?,
    @SerializedName("Remark") val remark: String?,
    @SerializedName("UpdatedByUserId") val updatedByUserId: String?,
    @SerializedName("UpdatedByName") val updatedByName: String?,
    @SerializedName("Source") val source: String?,
    @SerializedName("CreatedAt") val createdAt: String?,
    @SerializedName("Processed") val processed: Boolean,
    @SerializedName("ProcessedBy") val processedBy: String?,
    @SerializedName("ProcessedAt") val processedAt: String?,
    @SerializedName("RepairCount") val repairCount: Int,
    @SerializedName("LastRepairAction") val lastRepairAction: String?
)

data class ItemCategoryDto(
    @SerializedName("CategoryId") val categoryId: Int,
    @SerializedName("Name") val name: String
)

data class ConditionDto(
    @SerializedName("ConditionId") val conditionId: Int,
    @SerializedName("ConditionName") val conditionName: String
)

data class VendorDto(
    @SerializedName("VendorId") val vendorId: Int,
    @SerializedName("VendorName") val vendorName: String
)

// Models for batch item creation
data class BatchItemRequest(
    val serialNumber: String,
    val modelNumber: String,
    val itemType: String? = null,
    val cellPhoneNumber: String? = null,
    val imei1: String? = null,
    val imei2: String? = null
)

data class BatchItemsRequest(
    val items: List<BatchItemRequest>,
    val createdBy: String,
    val itemName: String,
    val description: String?,
    val categoryId: Int?,
    val categoryName: String?,
    val unitOfMeasure: String,
    val amount: Double?,
    val startDate: String?,
    val endDate: String?,
    // New fields for mobile-desktop parity
    val conditionId: Int?,
    val vendorId: Int?,
    val warrantyYears: Int?,
    val datePurchased: String?,
    val licenseNumber: String?,
    val remarks: String?
)
