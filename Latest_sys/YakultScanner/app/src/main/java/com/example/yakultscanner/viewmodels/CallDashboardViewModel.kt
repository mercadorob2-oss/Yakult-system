package com.example.yakultscanner.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.yakultscanner.api.CallDashboardResponse
import com.example.yakultscanner.data.repository.CallMonitoringRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed class CallDashboardUiState {
    object Loading : CallDashboardUiState()
    data class Success(val dashboard: CallDashboardResponse) : CallDashboardUiState()
    data class Error(val message: String) : CallDashboardUiState()
}

@HiltViewModel
class CallDashboardViewModel @Inject constructor(
    private val repository: CallMonitoringRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<CallDashboardUiState>(CallDashboardUiState.Loading)
    val uiState: StateFlow<CallDashboardUiState> = _uiState.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    fun loadDashboard(refresh: Boolean = false) {
        viewModelScope.launch {
            val current = _uiState.value
            if (refresh && current is CallDashboardUiState.Success) {
                _isRefreshing.value = true
            } else if (current !is CallDashboardUiState.Success) {
                _uiState.value = CallDashboardUiState.Loading
            }
            try {
                val response = repository.getDashboard()
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null && body.success) {
                        _uiState.value = CallDashboardUiState.Success(body)
                    } else if (current !is CallDashboardUiState.Success) {
                        _uiState.value = CallDashboardUiState.Error("Failed to load dashboard")
                    }
                } else if (current !is CallDashboardUiState.Success) {
                    _uiState.value = CallDashboardUiState.Error(friendlyHttpMessage(response.code()))
                }
            } catch (e: Exception) {
                if (current !is CallDashboardUiState.Success) {
                    _uiState.value = CallDashboardUiState.Error("Network error: ${e.message}")
                }
            } finally {
                _isRefreshing.value = false
            }
        }
    }
}

private fun friendlyHttpMessage(code: Int): String = when (code) {
    401 -> "Your ITCM session has expired. Please sign in again."
    403 -> "Your account is not authorized for mobile IT Call Monitoring."
    404 -> "Dashboard data could not be found."
    503 -> "ITCM is temporarily unavailable. Try again in a moment."
    else -> "ITCM could not load the dashboard. Please try again."
}