package com.example.yakultscanner.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.data.db.LocalScanItemEntity
import com.example.yakultscanner.data.db.LocalScanSessionEntity
import com.example.yakultscanner.data.db.LocalScanSessionWithCount
import com.example.yakultscanner.data.repository.LocalScanRepository
import com.example.yakultscanner.data.repository.LocalScanSendResult
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import javax.inject.Inject

@HiltViewModel
class LocalSerialScanViewModel @Inject constructor(
    private val repository: LocalScanRepository
) : ViewModel() {
    val sessions: StateFlow<List<LocalScanSessionWithCount>> = repository.observeSessions()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    suspend fun saveSession(
        sessionId: String?,
        title: String,
        notes: String?,
        rows: List<LocalScanItemEntity>
    ): String {
        return repository.saveSession(
            sessionId = sessionId,
            title = title,
            createdBy = UserSession.currentUser?.displayName ?: UserSession.currentUser?.username,
            notes = notes,
            rows = rows
        )
    }

    suspend fun getSession(sessionId: String): LocalScanSessionEntity? = repository.getSession(sessionId)

    suspend fun getItems(sessionId: String): List<LocalScanItemEntity> = repository.getItems(sessionId)

    suspend fun deleteSession(sessionId: String) = repository.deleteSession(sessionId)

    suspend fun sendSession(sessionId: String): LocalScanSendResult = repository.sendSession(sessionId)
}
