package com.example.yakultscanner.di;

import android.content.Context;
import com.example.yakultscanner.network.ConnectivityMonitor;
import dagger.internal.DaggerGenerated;
import dagger.internal.Factory;
import dagger.internal.Preconditions;
import dagger.internal.Provider;
import dagger.internal.QualifierMetadata;
import dagger.internal.ScopeMetadata;
import javax.annotation.processing.Generated;

@ScopeMetadata("javax.inject.Singleton")
@QualifierMetadata("dagger.hilt.android.qualifiers.ApplicationContext")
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
public final class DatabaseModule_ProvideConnectivityMonitorFactory implements Factory<ConnectivityMonitor> {
  private final Provider<Context> contextProvider;

  private DatabaseModule_ProvideConnectivityMonitorFactory(Provider<Context> contextProvider) {
    this.contextProvider = contextProvider;
  }

  @Override
  public ConnectivityMonitor get() {
    return provideConnectivityMonitor(contextProvider.get());
  }

  public static DatabaseModule_ProvideConnectivityMonitorFactory create(
      Provider<Context> contextProvider) {
    return new DatabaseModule_ProvideConnectivityMonitorFactory(contextProvider);
  }

  public static ConnectivityMonitor provideConnectivityMonitor(Context context) {
    return Preconditions.checkNotNullFromProvides(DatabaseModule.INSTANCE.provideConnectivityMonitor(context));
  }
}
