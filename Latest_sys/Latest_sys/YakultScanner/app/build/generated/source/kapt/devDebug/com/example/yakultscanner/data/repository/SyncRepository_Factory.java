package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.YakultApiService;
import dagger.internal.DaggerGenerated;
import dagger.internal.Factory;
import dagger.internal.Provider;
import dagger.internal.QualifierMetadata;
import dagger.internal.ScopeMetadata;
import javax.annotation.processing.Generated;

@ScopeMetadata("javax.inject.Singleton")
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
public final class SyncRepository_Factory implements Factory<SyncRepository> {
  private final Provider<OfflineRepository> offlineRepositoryProvider;

  private final Provider<YakultApiService> apiServiceProvider;

  private SyncRepository_Factory(Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<YakultApiService> apiServiceProvider) {
    this.offlineRepositoryProvider = offlineRepositoryProvider;
    this.apiServiceProvider = apiServiceProvider;
  }

  @Override
  public SyncRepository get() {
    return newInstance(offlineRepositoryProvider.get(), apiServiceProvider.get());
  }

  public static SyncRepository_Factory create(Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<YakultApiService> apiServiceProvider) {
    return new SyncRepository_Factory(offlineRepositoryProvider, apiServiceProvider);
  }

  public static SyncRepository newInstance(OfflineRepository offlineRepository,
      YakultApiService apiService) {
    return new SyncRepository(offlineRepository, apiService);
  }
}
