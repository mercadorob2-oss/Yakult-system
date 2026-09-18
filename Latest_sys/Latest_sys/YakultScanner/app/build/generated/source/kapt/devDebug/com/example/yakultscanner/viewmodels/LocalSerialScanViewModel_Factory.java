package com.example.yakultscanner.viewmodels;

import com.example.yakultscanner.data.repository.LocalScanRepository;
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
public final class LocalSerialScanViewModel_Factory implements Factory<LocalSerialScanViewModel> {
  private final Provider<LocalScanRepository> repositoryProvider;

  private LocalSerialScanViewModel_Factory(Provider<LocalScanRepository> repositoryProvider) {
    this.repositoryProvider = repositoryProvider;
  }

  @Override
  public LocalSerialScanViewModel get() {
    return newInstance(repositoryProvider.get());
  }

  public static LocalSerialScanViewModel_Factory create(
      Provider<LocalScanRepository> repositoryProvider) {
    return new LocalSerialScanViewModel_Factory(repositoryProvider);
  }

  public static LocalSerialScanViewModel newInstance(LocalScanRepository repository) {
    return new LocalSerialScanViewModel(repository);
  }
}
