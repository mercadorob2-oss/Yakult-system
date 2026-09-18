package com.example.yakultscanner.viewmodels;

import com.example.yakultscanner.data.repository.InventoryRepository;
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
public final class BatchEntryViewModel_Factory implements Factory<BatchEntryViewModel> {
  private final Provider<InventoryRepository> repositoryProvider;

  private BatchEntryViewModel_Factory(Provider<InventoryRepository> repositoryProvider) {
    this.repositoryProvider = repositoryProvider;
  }

  @Override
  public BatchEntryViewModel get() {
    return newInstance(repositoryProvider.get());
  }

  public static BatchEntryViewModel_Factory create(
      Provider<InventoryRepository> repositoryProvider) {
    return new BatchEntryViewModel_Factory(repositoryProvider);
  }

  public static BatchEntryViewModel newInstance(InventoryRepository repository) {
    return new BatchEntryViewModel(repository);
  }
}
