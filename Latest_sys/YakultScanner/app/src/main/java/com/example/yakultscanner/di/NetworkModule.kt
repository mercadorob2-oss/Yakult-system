package com.example.yakultscanner.di

import com.example.yakultscanner.api.ApiClient
import com.example.yakultscanner.api.YakultApiService
import com.example.yakultscanner.data.repository.CallMonitoringRepository
import com.example.yakultscanner.data.repository.InventoryRepository
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    @Provides
    @Singleton
    fun provideYakultApiService(): YakultApiService {
        return ApiClient.service
    }

    @Provides
    @Singleton
    fun provideInventoryRepository(apiService: YakultApiService): InventoryRepository {
        return InventoryRepository(apiService)
    }

    @Provides
    @Singleton
    fun provideCallMonitoringRepository(): CallMonitoringRepository {
        return CallMonitoringRepository()
    }
}
