package com.example.yakultscanner.ui.screens;

import com.example.yakultscanner.data.repository.OfflineRepository;
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
public final class HomeViewModel_Factory implements Factory<HomeViewModel> {
  private final Provider<OfflineRepository> offlineRepositoryProvider;

  private final Provider<ConnectivityMonitor> connectivityMonitorProvider;

  private HomeViewModel_Factory(Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<ConnectivityMonitor> connectivityMonitorProvider) {
    this.offlineRepositoryProvider = offlineRepositoryProvider;
    this.connectivityMonitorProvider = connectivityMonitorProvider;
  }

  @Override
  public HomeViewModel get() {
    return newInstance(offlineRepositoryProvider.get(), connectivityMonitorProvider.get());
  }

  public static HomeViewModel_Factory create(Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<ConnectivityMonitor> connectivityMonitorProvider) {
    return new HomeViewModel_Factory(offlineRepositoryProvider, connectivityMonitorProvider);
  }

  public static HomeViewModel newInstance(OfflineRepository offlineRepository,
      ConnectivityMonitor connectivityMonitor) {
    return new HomeViewModel(offlineRepository, connectivityMonitor);
  }
}
