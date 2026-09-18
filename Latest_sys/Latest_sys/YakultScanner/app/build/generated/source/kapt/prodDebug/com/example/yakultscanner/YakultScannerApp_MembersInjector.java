package com.example.yakultscanner;

import com.example.yakultscanner.worker.SyncWorkScheduler;
import dagger.MembersInjector;
import dagger.internal.DaggerGenerated;
import dagger.internal.InjectedFieldSignature;
import dagger.internal.Provider;
import dagger.internal.QualifierMetadata;
import javax.annotation.processing.Generated;

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
public final class YakultScannerApp_MembersInjector implements MembersInjector<YakultScannerApp> {
  private final Provider<SyncWorkScheduler> syncSchedulerProvider;

  private YakultScannerApp_MembersInjector(Provider<SyncWorkScheduler> syncSchedulerProvider) {
    this.syncSchedulerProvider = syncSchedulerProvider;
  }

  @Override
  public void injectMembers(YakultScannerApp instance) {
    injectSyncScheduler(instance, syncSchedulerProvider.get());
  }

  public static MembersInjector<YakultScannerApp> create(
      Provider<SyncWorkScheduler> syncSchedulerProvider) {
    return new YakultScannerApp_MembersInjector(syncSchedulerProvider);
  }

  @InjectedFieldSignature("com.example.yakultscanner.YakultScannerApp.syncScheduler")
  public static void injectSyncScheduler(YakultScannerApp instance,
      SyncWorkScheduler syncScheduler) {
    instance.syncScheduler = syncScheduler;
  }
}
