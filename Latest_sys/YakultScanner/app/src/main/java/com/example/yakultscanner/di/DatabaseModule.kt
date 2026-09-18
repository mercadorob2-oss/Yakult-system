package com.example.yakultscanner.di

import android.content.Context
import androidx.room.Room
import com.example.yakultscanner.data.db.AppDatabase
import com.example.yakultscanner.data.db.DatabaseMigrations
import com.example.yakultscanner.data.db.LocalScanDao
import com.example.yakultscanner.data.db.PendingUpdateDao
import com.example.yakultscanner.network.ConnectivityMonitor
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {
    @Provides
    @Singleton
    fun provideAppDatabase(@ApplicationContext context: Context): AppDatabase {
        return Room.databaseBuilder(
            context,
            AppDatabase::class.java,
            "yakult_scanner.db"
        )
            // Register migrations here as the schema evolves.
            // See DatabaseMigrations.kt for the pattern and instructions.
            // NEVER use fallbackToDestructiveMigration() — it silently
            // deletes all pending sync data on an unhandled version bump.
            .addMigrations(
                DatabaseMigrations.MIGRATION_1_2
                // Add future migrations: MIGRATION_2_3, MIGRATION_3_4, ...
            )
            .build()
    }

    @Provides
    fun providePendingUpdateDao(database: AppDatabase): PendingUpdateDao {
        return database.pendingUpdateDao()
    }

    @Provides
    fun provideLocalScanDao(database: AppDatabase): LocalScanDao {
        return database.localScanDao()
    }

    @Provides
    @Singleton
    fun provideConnectivityMonitor(@ApplicationContext context: Context): ConnectivityMonitor {
        return ConnectivityMonitor(context)
    }
}

