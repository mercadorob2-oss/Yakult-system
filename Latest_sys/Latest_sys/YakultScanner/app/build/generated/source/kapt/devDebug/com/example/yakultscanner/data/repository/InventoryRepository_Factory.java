package com.example.yakultscanner.data.repository;

import com.example.yakultscanner.api.YakultApiService;
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
public final class InventoryRepository_Factory implements Factory<InventoryRepository> {
  private final Provider<YakultApiService> apiServiceProvider;

  private InventoryRepository_Factory(Provider<YakultApiService> apiServiceProvider) {
    this.apiServiceProvider = apiServiceProvider;
  }

  @Override
  public InventoryRepository get() {
    return newInstance(apiServiceProvider.get());
  }

  public static InventoryRepository_Factory create(Provider<YakultApiService> apiServiceProvider) {
    return new InventoryRepository_Factory(apiServiceProvider);
  }

  public static InventoryRepository newInstance(YakultApiService apiService) {
    return new InventoryRepository(apiService);
  }
}
