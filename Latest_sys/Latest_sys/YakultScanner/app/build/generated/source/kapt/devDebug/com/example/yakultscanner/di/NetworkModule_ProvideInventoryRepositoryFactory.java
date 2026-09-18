package com.example.yakultscanner.di;

import com.example.yakultscanner.api.YakultApiService;
import com.example.yakultscanner.data.repository.InventoryRepository;
import dagger.internal.DaggerGenerated;
import dagger.internal.Factory;
import dagger.internal.Preconditions;
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
public final class NetworkModule_ProvideInventoryRepositoryFactory implements Factory<InventoryRepository> {
  private final Provider<YakultApiService> apiServiceProvider;

  private NetworkModule_ProvideInventoryRepositoryFactory(
      Provider<YakultApiService> apiServiceProvider) {
    this.apiServiceProvider = apiServiceProvider;
  }

  @Override
  public InventoryRepository get() {
    return provideInventoryRepository(apiServiceProvider.get());
  }

  public static NetworkModule_ProvideInventoryRepositoryFactory create(
      Provider<YakultApiService> apiServiceProvider) {
    return new NetworkModule_ProvideInventoryRepositoryFactory(apiServiceProvider);
  }

  public static InventoryRepository provideInventoryRepository(YakultApiService apiService) {
    return Preconditions.checkNotNullFromProvides(NetworkModule.INSTANCE.provideInventoryRepository(apiService));
  }
}
