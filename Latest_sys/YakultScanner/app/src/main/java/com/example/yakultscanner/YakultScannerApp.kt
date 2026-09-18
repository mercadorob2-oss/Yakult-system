package com.example.yakultscanner

import android.app.Application
import androidx.hilt.work.HiltWorkerFactory
import androidx.work.Configuration
import com.example.yakultscanner.settings.ApiSettings
import com.example.yakultscanner.worker.SyncWorkScheduler
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject

@HiltAndroidApp(Application::class)
class YakultScannerApp : Hilt_YakultScannerApp(), Configuration.Provider {
    @Inject lateinit var syncScheduler: SyncWorkScheduler
    @Inject lateinit var workerFactory: HiltWorkerFactory

    override val workManagerConfiguration: Configuration
        get() = Configuration.Builder()
            .setWorkerFactory(workerFactory)
            .build()

    override fun onCreate() {
        super.onCreate()
        ApiSettings.init(this)
        UserSession.init(this)
        syncScheduler.schedulePeriodicSync()
    }
}
