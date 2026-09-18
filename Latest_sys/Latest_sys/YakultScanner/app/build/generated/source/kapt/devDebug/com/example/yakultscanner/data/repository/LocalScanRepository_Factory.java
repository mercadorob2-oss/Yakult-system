package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.data.db.LocalScanDao;
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
public final class LocalScanRepository_Factory implements Factory<LocalScanRepository> {
  private final Provider<LocalScanDao> daoProvider;

  private LocalScanRepository_Factory(Provider<LocalScanDao> daoProvider) {
    this.daoProvider = daoProvider;
  }

  @Override
  public LocalScanRepository get() {
    return newInstance(daoProvider.get());
  }

  public static LocalScanRepository_Factory create(Provider<LocalScanDao> daoProvider) {
    return new LocalScanRepository_Factory(daoProvider);
  }

  public static LocalScanRepository newInstance(LocalScanDao dao) {
    return new LocalScanRepository(dao);
  }
}
