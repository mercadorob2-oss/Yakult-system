package com.example.yakultscanner.data.model

import com.google.gson.annotations.SerializedName

// IMPORTANT CHANGE: String fields are now nullable (String?) to safely handle potential nulls from JSON.
data class DispatchItem(
    @SerializedName("item_id") val id: Int, // Int is non-nullable, Gson defaults to 0 if missing
    @SerializedName("item_type") val type: String?,
    @SerializedName("item_category") val category: String?,
    @SerializedName("quantity") val quantity: Int,
    @SerializedName("description") val description: String?,
    @SerializedName("model_number") val modelNumber: String?,
    @SerializedName("serial_number") val serialNumber: String?,
    @SerializedName("item_status") val status: String?,
    @SerializedName("computer_name") val computerName: String?,
    @SerializedName("ip_address") val ipAddress: String?
)

data class DispatchSet(
    @SerializedName("set_code") val setCode: String?,
    @SerializedName("employee_name") val employee: String?,
    @SerializedName("department") val department: String?,
    @SerializedName("branch") val branch: String?,
    @SerializedName("status") val status: String?,
    @SerializedName("created_by") val createdBy: String?,
    @SerializedName("company") val company: String?,
    @SerializedName("logistics_provider") val logistics: String?,
    @SerializedName("Request_date") val dispatchDate: String?,
    @SerializedName("created_date") val createdDate: String?,
    @SerializedName("items") val items: List<DispatchItem>?, // Made items list nullable too
    @SerializedName("image_count") val imageCount: Int? = null // Number of images attached to this set
)

data class ItemEditState(
    val id: Int,
    val base: DispatchItem,
    val statusInput: String,
    val remarkInput: String,
    val isEditing: Boolean
)
