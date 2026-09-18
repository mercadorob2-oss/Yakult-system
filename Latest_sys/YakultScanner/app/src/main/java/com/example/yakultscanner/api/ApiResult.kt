package com.example.yakultscanner.api

import com.example.yakultscanner.ConnectionHealthStore
import org.json.JSONObject
import retrofit2.Response
import java.io.IOException

const val SCANNER_INVALID_QR_MESSAGE =
    "This QR code is not a valid Yakult dispatch QR code. Please scan a valid Yakult QR code."
const val SCANNER_UNSUPPORTED_QR_MESSAGE =
    "This QR code is not supported by Yakult Scanner. Please scan a valid Yakult QR code."
const val SCANNER_NETWORK_MESSAGE =
    "We couldn't connect to the server. Please check your connection and try again."
const val SCANNER_SERVICE_UNAVAILABLE_MESSAGE =
    "We couldn't reach the scanner service right now. Please try again in a moment."
const val SCANNER_GENERIC_MESSAGE =
    "Something went wrong while processing your request. Please try again."

sealed class ApiResult<out T> {
    data class Success<T>(
        val data: T,
        val httpCode: Int? = null,
        val endpoint: String? = null
    ) : ApiResult<T>()

    data class HttpError(
        val code: Int,
        val message: String? = null,
        val endpoint: String? = null
    ) : ApiResult<Nothing>()

    data class NetworkError(
        val message: String? = null,
        val endpoint: String? = null
    ) : ApiResult<Nothing>()

    data class UnknownError(
        val message: String? = null,
        val endpoint: String? = null
    ) : ApiResult<Nothing>()
}

fun ApiResult.HttpError.userMessageOr(defaultMessage: String): String {
    return message?.takeIf { it.isNotBlank() } ?: defaultMessage
}

fun ApiResult.NetworkError.userMessageOr(defaultMessage: String = SCANNER_NETWORK_MESSAGE): String {
    return message?.takeIf { it.isNotBlank() } ?: defaultMessage
}

fun ApiResult.UnknownError.userMessageOr(defaultMessage: String = SCANNER_GENERIC_MESSAGE): String {
    return message?.takeIf { it.isNotBlank() } ?: defaultMessage
}

suspend fun <T> safeApiCall(call: suspend () -> Response<T>): ApiResult<T> {
    return try {
        val response = call()
        val endpoint = try {
            response.raw().request.url.encodedPath
        } catch (_: Exception) {
            null
        }
        if (response.isSuccessful) {
            val body = response.body()
            if (body != null) {
                ConnectionHealthStore.recordSuccess(endpoint = endpoint, httpCode = response.code())
                ApiResult.Success(body, httpCode = response.code(), endpoint = endpoint)
            } else {
                ConnectionHealthStore.recordFailure(endpoint = endpoint, httpCode = response.code(), message = "Empty response body")
                ApiResult.HttpError(
                    code = response.code(),
                    message = "We couldn't read the server response. Please try again.",
                    endpoint = endpoint
                )
            }
        } else {
            val rawError = response.errorBody()?.string()?.takeIf { it.isNotBlank() }
            val parsedError = parseFriendlyHttpError(response.code(), rawError)
            ConnectionHealthStore.recordFailure(
                endpoint = endpoint,
                httpCode = response.code(),
                message = parsedError.logMessage ?: "HTTP ${response.code()}"
            )
            ApiResult.HttpError(code = response.code(), message = parsedError.userMessage, endpoint = endpoint)
        }
    } catch (io: IOException) {
        ConnectionHealthStore.recordFailure(endpoint = null, httpCode = null, message = io.message ?: "Network error")
        ApiResult.NetworkError(message = SCANNER_NETWORK_MESSAGE, endpoint = null)
    } catch (e: Exception) {
        ConnectionHealthStore.recordFailure(endpoint = null, httpCode = null, message = e.message ?: "Unknown error")
        ApiResult.UnknownError(message = SCANNER_GENERIC_MESSAGE, endpoint = null)
    }
}

private data class ParsedApiError(
    val userMessage: String,
    val logMessage: String? = null
)

private fun parseFriendlyHttpError(httpCode: Int, rawError: String?): ParsedApiError {
    val trimmed = rawError?.trim().orEmpty()
    if (trimmed.isBlank()) {
        return ParsedApiError(defaultHttpMessage(httpCode))
    }

    parseJsonErrorPayload(trimmed)?.let { payload ->
        val message = payload.message?.takeIf { it.isNotBlank() }
            ?: defaultHttpMessage(httpCode, payload.errorCode)
        return ParsedApiError(userMessage = message, logMessage = trimmed)
    }

    val normalized = trimmed.lowercase()
    val userMessage = when {
        normalized.contains("invalid qr") ||
            normalized.contains("qr token") ||
            normalized.contains("guid") ||
            normalized.contains("qr code format") -> SCANNER_INVALID_QR_MESSAGE
        normalized.contains("unable to resolve host") ||
            normalized.contains("failed to connect") ||
            normalized.contains("connection refused") ||
            normalized.contains("timeout") -> SCANNER_NETWORK_MESSAGE
        else -> defaultHttpMessage(httpCode)
    }

    return ParsedApiError(userMessage = userMessage, logMessage = trimmed)
}

private fun parseJsonErrorPayload(raw: String): JsonErrorPayload? {
    return try {
        val json = JSONObject(raw)
        val message = json.optString("message").takeIf { it.isNotBlank() }
            ?: json.optString("Message").takeIf { it.isNotBlank() }
        val errorCode = json.optString("error").takeIf { it.isNotBlank() }
            ?: json.optString("Error").takeIf { it.isNotBlank() }
        if (message == null && errorCode == null) {
            null
        } else {
            JsonErrorPayload(message = message, errorCode = errorCode)
        }
    } catch (_: Exception) {
        null
    }
}

private fun defaultHttpMessage(httpCode: Int, errorCode: String? = null): String {
    return when (errorCode?.lowercase()) {
        "empty_qr_data", "invalid_qr_format" -> SCANNER_INVALID_QR_MESSAGE
        "set_not_found" -> "This QR code was recognized, but no matching dispatch set was found."
        "service_unavailable", "database_unavailable" -> SCANNER_SERVICE_UNAVAILABLE_MESSAGE
        "dispatch_update_failed", "scanner_lookup_failed", "status_lookup_failed" -> SCANNER_GENERIC_MESSAGE
        else -> when (httpCode) {
            400 -> "We couldn't process that request. Please check the scanned QR code and try again."
            401 -> "Your session has expired or your login details are incorrect. Please try again."
            403 -> "You don't have permission to do that action."
            404 -> "The requested record could not be found."
            409 -> "This action has already been completed."
            500, 502, 503, 504 -> SCANNER_SERVICE_UNAVAILABLE_MESSAGE
            else -> SCANNER_GENERIC_MESSAGE
        }
    }
}

private data class JsonErrorPayload(
    val message: String?,
    val errorCode: String?
)
