package com.example.yakultscanner.di;

import com.example.yakultscanner.api.YakultApiService;
import dagger.internal.DaggerGenerated;
import dagger.internal.Factory;
import dagger.internal.Preconditions;
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
public final class NetworkModule_ProvideYakultApiServiceFactory implements Factory<YakultApiService> {
  @Override
  public YakultApiService get() {
    return provideYakultApiService();
  }

  public static NetworkModule_ProvideYakultApiServiceFactory create() {
    return InstanceHolder.INSTANCE;
  }

  public static YakultApiService provideYakultApiService() {
    return Preconditions.checkNotNullFromProvides(NetworkModule.INSTANCE.provideYakultApiService());
  }

  private static final class InstanceHolder {
    static final NetworkModule_ProvideYakultApiServiceFactory INSTANCE = new NetworkModule_ProvideYakultApiServiceFactory();
  }
}
