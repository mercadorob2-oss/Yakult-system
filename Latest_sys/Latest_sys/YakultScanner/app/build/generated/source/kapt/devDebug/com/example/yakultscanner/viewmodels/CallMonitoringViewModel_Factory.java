package com.example.yakultscanner.viewmodels;

import com.example.yakultscanner.data.repository.CallMonitoringRepository;
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
public final class CallMonitoringViewModel_Factory implements Factory<CallMonitoringViewModel> {
  private final Provider<CallMonitoringRepository> repositoryProvider;

  private CallMonitoringViewModel_Factory(Provider<CallMonitoringRepository> repositoryProvider) {
    this.repositoryProvider = repositoryProvider;
  }

  @Override
  public CallMonitoringViewModel get() {
    return newInstance(repositoryProvider.get());
  }

  public static CallMonitoringViewModel_Factory create(
      Provider<CallMonitoringRepository> repositoryProvider) {
    return new CallMonitoringViewModel_Factory(repositoryProvider);
  }

  public static CallMonitoringViewModel newInstance(CallMonitoringRepository repository) {
    return new CallMonitoringViewModel(repository);
  }
}
