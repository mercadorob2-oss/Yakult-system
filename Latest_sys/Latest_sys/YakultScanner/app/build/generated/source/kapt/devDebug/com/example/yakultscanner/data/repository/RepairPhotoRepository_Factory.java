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
public final class RepairPhotoRepository_Factory implements Factory<RepairPhotoRepository> {
  private final Provider<YakultApiService> apiServiceProvider;

  private RepairPhotoRepository_Factory(Provider<YakultApiService> apiServiceProvider) {
    this.apiServiceProvider = apiServiceProvider;
  }

  @Override
  public RepairPhotoRepository get() {
    return newInstance(apiServiceProvider.get());
  }

  public static RepairPhotoRepository_Factory create(
      Provider<YakultApiService> apiServiceProvider) {
    return new RepairPhotoRepository_Factory(apiServiceProvider);
  }

  public static RepairPhotoRepository newInstance(YakultApiService apiService) {
    return new RepairPhotoRepository(apiService);
  }
}
