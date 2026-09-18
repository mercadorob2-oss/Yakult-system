package com.example.yakultscanner

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update

data class ConnectionHealthSnapshot(
    val baseUrl: String = "",
    val lastSuccessAtMs: Long? = null,
    val lastFailureAtMs: Long? = null,
    val lastHttpCode: Int? = null,
    val lastEndpoint: String? = null,
    val lastErrorMessage: String? = null
)

object ConnectionHealthStore {
    private val _state = MutableStateFlow(ConnectionHealthSnapshot())
    val state: StateFlow<ConnectionHealthSnapshot> = _state

    fun updateBaseUrl(baseUrl: String) {
        _state.update { it.copy(baseUrl = baseUrl) }
    }

    fun recordSuccess(endpoint: String?, httpCode: Int?) {
        val now = System.currentTimeMillis()
        _state.update {
            it.copy(
                lastSuccessAtMs = now,
                lastHttpCode = httpCode,
                lastEndpoint = endpoint,
                lastErrorMessage = null
            )
        }
    }

    fun recordFailure(endpoint: String?, httpCode: Int?, message: String?) {
        val now = System.currentTimeMillis()
        _state.update {
            it.copy(
                lastFailureAtMs = now,
                lastHttpCode = httpCode,
                lastEndpoint = endpoint,
                lastErrorMessage = (message ?: "").trim().ifBlank { null }
            )
        }
    }
}

