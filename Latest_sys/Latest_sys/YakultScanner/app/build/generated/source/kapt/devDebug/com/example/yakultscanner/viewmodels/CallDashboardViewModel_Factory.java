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
public final class CallDashboardViewModel_Factory implements Factory<CallDashboardViewModel> {
  private final Provider<CallMonitoringRepository> repositoryProvider;

  private CallDashboardViewModel_Factory(Provider<CallMonitoringRepository> repositoryProvider) {
    this.repositoryProvider = repositoryProvider;
  }

  @Override
  public CallDashboardViewModel get() {
    return newInstance(repositoryProvider.get());
  }

  public static CallDashboardViewModel_Factory create(
      Provider<CallMonitoringRepository> repositoryProvider) {
    return new CallDashboardViewModel_Factory(repositoryProvider);
  }

  public static CallDashboardViewModel newInstance(CallMonitoringRepository repository) {
    return new CallDashboardViewModel(repository);
  }
}
