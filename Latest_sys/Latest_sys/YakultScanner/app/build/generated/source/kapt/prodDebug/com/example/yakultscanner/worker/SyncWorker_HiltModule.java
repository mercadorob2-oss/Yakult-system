package com.example.yakultscanner.worker;

import androidx.hilt.work.WorkerAssistedFactory;
import androidx.work.ListenableWorker;
import dagger.Binds;
import dagger.Module;
import dagger.hilt.InstallIn;
import dagger.hilt.codegen.OriginatingElement;
import dagger.hilt.components.SingletonComponent;
import dagger.multibindings.IntoMap;
import dagger.multibindings.StringKey;

@Module
@InstallIn(SingletonComponent.class)
@OriginatingElement(
    topLevelClass = SyncWorker.class
)
public interface SyncWorker_HiltModule {
  @Binds
  @IntoMap
  @StringKey("com.example.yakultscanner.worker.SyncWorker")
  WorkerAssistedFactory<? extends ListenableWorker> bind(SyncWorker_AssistedFactory factory);
}
