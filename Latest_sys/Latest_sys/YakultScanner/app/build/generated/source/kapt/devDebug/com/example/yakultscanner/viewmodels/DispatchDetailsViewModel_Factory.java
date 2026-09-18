package com.example.yakultscanner.viewmodels;

import com.example.yakultscanner.api.YakultApiService;
import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.data.repository.SyncRepository;
import com.example.yakultscanner.network.ConnectivityMonitor;
import dagger.internal.DaggerGenerated;
import dagger.internal.Factory;
import dagger.internal.Provider;
import dagger.internal.QualifierMetadata;
import dagger.internal.ScopeMetadata;
import javax.annotation.processing.Generated;

@ScopeMetadata
@QualifierMetadata
@DaggerGenerated
@Generated(
    value = "dagger.internal.codegen.ComponentProcessor",
    comments = "https://dagger.dev"
)
@SuppressWarnings({
    "unchecked",
    "rawtypes",
    "KotlinInternal",
    "KotlinInternalInJava",
    "cast",
    "deprecation",
    "nullness:initialization.field.uninitialized"
})
public final class DispatchDetailsViewModel_Factory implements Factory<DispatchDetailsViewModel> {
  private final Provider<YakultApiService> apiServiceProvider;

  private final Provider<OfflineRepository> offlineRepositoryProvider;

  private final Provider<SyncRepository> syncRepositoryProvider;

  private final Provider<ConnectivityMonitor> connectivityMonitorProvider;

  private DispatchDetailsViewModel_Factory(Provider<YakultApiService> apiServiceProvider,
      Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<SyncRepository> syncRepositoryProvider,
      Provider<ConnectivityMonitor> connectivityMonitorProvider) {
    this.apiServiceProvider = apiServiceProvider;
    this.offlineRepositoryProvider = offlineRepositoryProvider;
    this.syncRepositoryProvider = syncRepositoryProvider;
    this.connectivityMonitorProvider = connectivityMonitorProvider;
  }

  @Override
  public DispatchDetailsViewModel get() {
    return newInstance(apiServiceProvider.get(), offlineRepositoryProvider.get(), syncRepositoryProvider.get(), connectivityMonitorProvider.get());
  }

  public static DispatchDetailsViewModel_Factory create(
      Provider<YakultApiService> apiServiceProvider,
      Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<SyncRepository> syncRepositoryProvider,
      Provider<ConnectivityMonitor> connectivityMonitorProvider) {
    return new DispatchDetailsViewModel_Factory(apiServiceProvider, offlineRepositoryProvider, syncRepositoryProvider, connectivityMonitorProvider);
  }

  public static DispatchDetailsViewModel newInstance(YakultApiService apiService,
      OfflineRepository offlineRepository, SyncRepository syncRepository,
      ConnectivityMonitor connectivityMonitor) {
    return new DispatchDetailsViewModel(apiService, offlineRepository, syncRepository, connectivityMonitor);
  }
}
