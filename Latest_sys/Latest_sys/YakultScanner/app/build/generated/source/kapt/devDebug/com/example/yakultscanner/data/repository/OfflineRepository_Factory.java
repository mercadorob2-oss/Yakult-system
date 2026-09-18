package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.data.db.PendingUpdateDao;
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
public final class OfflineRepository_Factory implements Factory<OfflineRepository> {
  private final Provider<PendingUpdateDao> pendingUpdateDaoProvider;

  private OfflineRepository_Factory(Provider<PendingUpdateDao> pendingUpdateDaoProvider) {
    this.pendingUpdateDaoProvider = pendingUpdateDaoProvider;
  }

  @Override
  public OfflineRepository get() {
    return newInstance(pendingUpdateDaoProvider.get());
  }

  public static OfflineRepository_Factory create(
      Provider<PendingUpdateDao> pendingUpdateDaoProvider) {
    return new OfflineRepository_Factory(pendingUpdateDaoProvider);
  }

  public static OfflineRepository newInstance(PendingUpdateDao pendingUpdateDao) {
    return new OfflineRepository(pendingUpdateDao);
  }
}
