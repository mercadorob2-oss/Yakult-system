package com.example.yakultscanner.worker;

import android.content.Context;
import androidx.work.WorkerParameters;
import com.example.yakultscanner.data.repository.SyncRepository;
import dagger.internal.DaggerGenerated;
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
public final class SyncWorker_Factory {
  private final Provider<SyncRepository> syncRepositoryProvider;

  private SyncWorker_Factory(Provider<SyncRepository> syncRepositoryProvider) {
    this.syncRepositoryProvider = syncRepositoryProvider;
  }

  public SyncWorker get(Context context, WorkerParameters params) {
    return newInstance(context, params, syncRepositoryProvider.get());
  }

  public static SyncWorker_Factory create(Provider<SyncRepository> syncRepositoryProvider) {
    return new SyncWorker_Factory(syncRepositoryProvider);
  }

  public static SyncWorker newInstance(Context context, WorkerParameters params,
      SyncRepository syncRepository) {
    return new SyncWorker(context, params, syncRepository);
  }
}
