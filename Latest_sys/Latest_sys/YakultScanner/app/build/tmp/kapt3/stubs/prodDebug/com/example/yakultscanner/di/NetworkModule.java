package com.example.yakultscanner.di;

import com.example.yakultscanner.api.ApiClient;
import com.example.yakultscanner.api.YakultApiService;
import com.example.yakultscanner.data.repository.CallMonitoringRepository;
import com.example.yakultscanner.data.repository.InventoryRepository;
import dagger.Module;
import dagger.Provides;
import dagger.hilt.InstallIn;
import dagger.hilt.components.SingletonComponent;
import javax.inject.Singleton;

@dagger.Module()
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000 \n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\b\u00c7\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\b\u0010\u0004\u001a\u00020\u0005H\u0007J\u0010\u0010\u0006\u001a\u00020\u00072\u0006\u0010\b\u001a\u00020\u0005H\u0007J\b\u0010\t\u001a\u00020\nH\u0007\u00a8\u0006\u000b"}, d2 = {"Lcom/example/yakultscanner/di/NetworkModule;", "", "<init>", "()V", "provideYakultApiService", "Lcom/example/yakultscanner/api/YakultApiService;", "provideInventoryRepository", "Lcom/example/yakultscanner/data/repository/InventoryRepository;", "apiService", "provideCallMonitoringRepository", "Lcom/example/yakultscanner/data/repository/CallMonitoringRepository;", "app_prodDebug"})
@dagger.hilt.InstallIn(value = {dagger.hilt.components.SingletonComponent.class})
public final class NetworkModule {
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.di.NetworkModule INSTANCE = null;
    
    private NetworkModule() {
        super();
    }
    
    @dagger.Provides()
    @javax.inject.Singleton()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.api.YakultApiService provideYakultApiService() {
        return null;
    }
    
    @dagger.Provides()
    @javax.inject.Singleton()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.repository.InventoryRepository provideInventoryRepository(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.api.YakultApiService apiService) {
        return null;
    }
    
    @dagger.Provides()
    @javax.inject.Singleton()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.repository.CallMonitoringRepository provideCallMonitoringRepository() {
        return null;
    }
}