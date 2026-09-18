package com.example.yakultscanner.ui.screens;

import com.example.yakultscanner.data.repository.OfflineRepository;
import com.example.yakultscanner.data.repository.SyncRepository;
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
public final class PendingUpdatesViewModel_Factory implements Factory<PendingUpdatesViewModel> {
  private final Provider<OfflineRepository> offlineRepositoryProvider;

  private final Provider<SyncRepository> syncRepositoryProvider;

  private PendingUpdatesViewModel_Factory(Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<SyncRepository> syncRepositoryProvider) {
    this.offlineRepositoryProvider = offlineRepositoryProvider;
    this.syncRepositoryProvider = syncRepositoryProvider;
  }

  @Override
  public PendingUpdatesViewModel get() {
    return newInstance(offlineRepositoryProvider.get(), syncRepositoryProvider.get());
  }

  public static PendingUpdatesViewModel_Factory create(
      Provider<OfflineRepository> offlineRepositoryProvider,
      Provider<SyncRepository> syncRepositoryProvider) {
    return new PendingUpdatesViewModel_Factory(offlineRepositoryProvider, syncRepositoryProvider);
  }

  public static PendingUpdatesViewModel newInstance(OfflineRepository offlineRepository,
      SyncRepository syncRepository) {
    return new PendingUpdatesViewModel(offlineRepository, syncRepository);
  }
}
