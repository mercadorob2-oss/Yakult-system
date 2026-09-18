package com.example.yakultscanner.ui.screens

import com.example.yakultscanner.api.ApiResult

internal fun buildApiError(
    title: String,
    endpoint: String?,
    typeLine: String,
    message: String?
): Pair<String, String> {
    val details = buildString {
        appendLine("Endpoint: ${endpoint ?: "(unknown)"}")
        appendLine(typeLine)
        if (!message.isNullOrBlank()) append("Message: $message")
    }.trim()
    return title to details
}

internal fun buildHttpApiError(
    title: String,
    result: ApiResult.HttpError
): Pair<String, String> {
    return buildApiError(
        title = title,
        endpoint = result.endpoint,
        typeLine = "Type: HTTP ${result.code}",
        message = result.message
    )
}

internal fun buildNetworkApiError(
    title: String,
    result: ApiResult.NetworkError
): Pair<String, String> {
    return buildApiError(
        title = title,
        endpoint = result.endpoint,
        typeLine = "Type: Network error",
        message = result.message
    )
}

internal fun buildUnknownApiError(
    title: String,
    result: ApiResult.UnknownError
): Pair<String, String> {
    return buildApiError(
        title = title,
        endpoint = result.endpoint,
        typeLine = "Type: Unexpected error",
        message = result.message
    )
}
