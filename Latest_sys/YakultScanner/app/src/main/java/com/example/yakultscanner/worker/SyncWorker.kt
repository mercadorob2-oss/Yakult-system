package com.example.yakultscanner.worker

import android.content.Context
import androidx.hilt.work.HiltWorker
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.data.repository.SyncRepository
import com.example.yakultscanner.data.repository.SyncResult
import dagger.assisted.Assisted
import dagger.assisted.AssistedInject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

@HiltWorker
class SyncWorker @AssistedInject constructor(
    @Assisted context: Context,
    @Assisted params: WorkerParameters,
    private val syncRepository: SyncRepository
) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        try {
            val user = UserSession.currentUser?.username
            
            when (val result = syncRepository.syncPendingUpdates(user)) {
                is SyncResult.Success -> {
                    Result.success()
                }
                is SyncResult.PartialSuccess -> {
                    // Some failed, but we should retry later
                    Result.retry()
                }
                is SyncResult.NetworkUnavailable -> {
                    // No network, retry later
                    Result.retry()
                }
                is SyncResult.Error -> {
                    // Hard error, but still retry
                    Result.retry()
                }
                is SyncResult.NoPendingUpdates -> {
                    Result.success()
                }
            }
        } catch (e: Exception) {
            Result.retry()
        }
    }

    companion object {
        const val WORK_NAME = "yakult_sync_worker"
    }
}
