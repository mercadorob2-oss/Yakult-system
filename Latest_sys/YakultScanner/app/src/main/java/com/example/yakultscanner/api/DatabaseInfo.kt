package com.example.yakultscanner.api

import com.google.gson.annotations.SerializedName

data class DatabaseInfoResponse(
    @SerializedName("success") val success: Boolean = false,
    @SerializedName("configured") val configured: Boolean = false,
    @SerializedName("database") val database: String? = null,
    @SerializedName("dataSource") val dataSource: String? = null,
    @SerializedName("server") val server: String? = null,
    @SerializedName("port") val port: Int? = null,
    @SerializedName("message") val message: String? = null
)
