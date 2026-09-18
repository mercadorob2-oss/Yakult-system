package com.example.yakultscanner.di;

import com.example.yakultscanner.data.repository.CallMonitoringRepository;
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
public final class NetworkModule_ProvideCallMonitoringRepositoryFactory implements Factory<CallMonitoringRepository> {
  @Override
  public CallMonitoringRepository get() {
    return provideCallMonitoringRepository();
  }

  public static NetworkModule_ProvideCallMonitoringRepositoryFactory create() {
    return InstanceHolder.INSTANCE;
  }

  public static CallMonitoringRepository provideCallMonitoringRepository() {
    return Preconditions.checkNotNullFromProvides(NetworkModule.INSTANCE.provideCallMonitoringRepository());
  }

  private static final class InstanceHolder {
    static final NetworkModule_ProvideCallMonitoringRepositoryFactory INSTANCE = new NetworkModule_ProvideCallMonitoringRepositoryFactory();
  }
}
