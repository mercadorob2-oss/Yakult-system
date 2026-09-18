package com.example.yakultscanner.di;

import com.example.yakultscanner.data.db.AppDatabase;
import com.example.yakultscanner.data.db.LocalScanDao;
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
public final class DatabaseModule_ProvideLocalScanDaoFactory implements Factory<LocalScanDao> {
  private final Provider<AppDatabase> databaseProvider;

  private DatabaseModule_ProvideLocalScanDaoFactory(Provider<AppDatabase> databaseProvider) {
    this.databaseProvider = databaseProvider;
  }

  @Override
  public LocalScanDao get() {
    return provideLocalScanDao(databaseProvider.get());
  }

  public static DatabaseModule_ProvideLocalScanDaoFactory create(
      Provider<AppDatabase> databaseProvider) {
    return new DatabaseModule_ProvideLocalScanDaoFactory(databaseProvider);
  }

  public static LocalScanDao provideLocalScanDao(AppDatabase database) {
    return Preconditions.checkNotNullFromProvides(DatabaseModule.INSTANCE.provideLocalScanDao(database));
  }
}
