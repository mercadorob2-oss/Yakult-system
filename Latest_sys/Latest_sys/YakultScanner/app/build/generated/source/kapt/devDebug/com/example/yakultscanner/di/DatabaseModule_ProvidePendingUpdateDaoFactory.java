package com.example.yakultscanner.di;

import com.example.yakultscanner.data.db.AppDatabase;
import com.example.yakultscanner.data.db.PendingUpdateDao;
import dagger.internal.DaggerGenerated;
import dagger.internal.Factory;
import dagger.internal.Preconditions;
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
public final class DatabaseModule_ProvidePendingUpdateDaoFactory implements Factory<PendingUpdateDao> {
  private final Provider<AppDatabase> databaseProvider;

  private DatabaseModule_ProvidePendingUpdateDaoFactory(Provider<AppDatabase> databaseProvider) {
    this.databaseProvider = databaseProvider;
  }

  @Override
  public PendingUpdateDao get() {
    return providePendingUpdateDao(databaseProvider.get());
  }

  public static DatabaseModule_ProvidePendingUpdateDaoFactory create(
      Provider<AppDatabase> databaseProvider) {
    return new DatabaseModule_ProvidePendingUpdateDaoFactory(databaseProvider);
  }

  public static PendingUpdateDao providePendingUpdateDao(AppDatabase database) {
    return Preconditions.checkNotNullFromProvides(DatabaseModule.INSTANCE.providePendingUpdateDao(database));
  }
}
