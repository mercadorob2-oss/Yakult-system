package com.example.yakultscanner.di;

import android.content.Context;
import androidx.room.Room;
import com.example.yakultscanner.data.db.AppDatabase;
import com.example.yakultscanner.data.db.DatabaseMigrations;
import com.example.yakultscanner.data.db.LocalScanDao;
import com.example.yakultscanner.data.db.PendingUpdateDao;
import com.example.yakultscanner.network.ConnectivityMonitor;
import dagger.Module;
import dagger.Provides;
import dagger.hilt.InstallIn;
import dagger.hilt.android.qualifiers.ApplicationContext;
import dagger.hilt.components.SingletonComponent;
import javax.inject.Singleton;

@dagger.Module()
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000,\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0002\b\u0002\n\u0002\u0018\u0002\n\u0000\n\u0002\u0018\u0002\n\u0000\b\u00c7\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003J\u0012\u0010\u0004\u001a\u00020\u00052\b\b\u0001\u0010\u0006\u001a\u00020\u0007H\u0007J\u0010\u0010\b\u001a\u00020\t2\u0006\u0010\n\u001a\u00020\u0005H\u0007J\u0010\u0010\u000b\u001a\u00020\f2\u0006\u0010\n\u001a\u00020\u0005H\u0007J\u0012\u0010\r\u001a\u00020\u000e2\b\b\u0001\u0010\u0006\u001a\u00020\u0007H\u0007\u00a8\u0006\u000f"}, d2 = {"Lcom/example/yakultscanner/di/DatabaseModule;", "", "<init>", "()V", "provideAppDatabase", "Lcom/example/yakultscanner/data/db/AppDatabase;", "context", "Landroid/content/Context;", "providePendingUpdateDao", "Lcom/example/yakultscanner/data/db/PendingUpdateDao;", "database", "provideLocalScanDao", "Lcom/example/yakultscanner/data/db/LocalScanDao;", "provideConnectivityMonitor", "Lcom/example/yakultscanner/network/ConnectivityMonitor;", "app_prodDebug"})
@dagger.hilt.InstallIn(value = {dagger.hilt.components.SingletonComponent.class})
public final class DatabaseModule {
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.di.DatabaseModule INSTANCE = null;
    
    private DatabaseModule() {
        super();
    }
    
    @dagger.Provides()
    @javax.inject.Singleton()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.AppDatabase provideAppDatabase(@dagger.hilt.android.qualifiers.ApplicationContext()
    @org.jetbrains.annotations.NotNull()
    android.content.Context context) {
        return null;
    }
    
    @dagger.Provides()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.PendingUpdateDao providePendingUpdateDao(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.AppDatabase database) {
        return null;
    }
    
    @dagger.Provides()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.data.db.LocalScanDao provideLocalScanDao(@org.jetbrains.annotations.NotNull()
    com.example.yakultscanner.data.db.AppDatabase database) {
        return null;
    }
    
    @dagger.Provides()
    @javax.inject.Singleton()
    @org.jetbrains.annotations.NotNull()
    public final com.example.yakultscanner.network.ConnectivityMonitor provideConnectivityMonitor(@dagger.hilt.android.qualifiers.ApplicationContext()
    @org.jetbrains.annotations.NotNull()
    android.content.Context context) {
        return null;
    }
}