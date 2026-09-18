package com.example.yakultscanner.data.db;

import androidx.annotation.NonNull;
import androidx.room.InvalidationTracker;
import androidx.room.RoomOpenDelegate;
import androidx.room.migration.AutoMigrationSpec;
import androidx.room.migration.Migration;
import androidx.room.util.DBUtil;
import androidx.room.util.TableInfo;
import androidx.sqlite.SQLite;
import androidx.sqlite.SQLiteConnection;
import java.lang.Class;
import java.lang.Override;
import java.lang.String;
import java.lang.SuppressWarnings;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

@SuppressWarnings({"unchecked", "deprecation", "removal"})
public final class AppDatabase_Impl extends AppDatabase {
  private volatile PendingUpdateDao _pendingUpdateDao;

  private volatile LocalScanDao _localScanDao;

  @Override
  @NonNull
  protected RoomOpenDelegate createOpenDelegate() {
    final RoomOpenDelegate _openDelegate = new RoomOpenDelegate(2, "861f2c03f8de4ba0cd3370326df7bdd3", "960b9a5f29b806a6aa0127f2a3cdb707") {
      @Override
      public void createAllTables(@NonNull final SQLiteConnection connection) {
        SQLite.execSQL(connection, "CREATE TABLE IF NOT EXISTS `pending_updates` (`id` TEXT NOT NULL, `set_code` TEXT NOT NULL, `set_id` INTEGER, `item_id` INTEGER, `item_type` TEXT, `serial_number` TEXT NOT NULL, `model_number` TEXT, `previous_status` TEXT, `new_status` TEXT NOT NULL, `remark` TEXT, `updated_by_user_id` TEXT, `updated_by_name` TEXT, `created_at` INTEGER NOT NULL, `retry_count` INTEGER NOT NULL, `sync_status` TEXT NOT NULL, `last_error` TEXT, `synced_at` INTEGER, PRIMARY KEY(`id`))");
        SQLite.execSQL(connection, "CREATE TABLE IF NOT EXISTS `local_scan_sessions` (`id` TEXT NOT NULL, `title` TEXT NOT NULL, `created_by` TEXT, `created_at` INTEGER NOT NULL, `updated_at` INTEGER NOT NULL, `notes` TEXT, `status` TEXT NOT NULL, `sent_at` INTEGER, PRIMARY KEY(`id`))");
        SQLite.execSQL(connection, "CREATE TABLE IF NOT EXISTS `local_scan_items` (`id` TEXT NOT NULL, `session_id` TEXT NOT NULL, `row_number` INTEGER NOT NULL, `serial_number` TEXT NOT NULL, `cell_phone_number` TEXT, `imei1` TEXT, `imei2` TEXT, `source` TEXT NOT NULL, `created_at` INTEGER NOT NULL, `sent` INTEGER NOT NULL, `sent_at` INTEGER, `last_error` TEXT, PRIMARY KEY(`id`), FOREIGN KEY(`session_id`) REFERENCES `local_scan_sessions`(`id`) ON UPDATE NO ACTION ON DELETE CASCADE )");
        SQLite.execSQL(connection, "CREATE INDEX IF NOT EXISTS `index_local_scan_items_session_id` ON `local_scan_items` (`session_id`)");
        SQLite.execSQL(connection, "CREATE TABLE IF NOT EXISTS room_master_table (id INTEGER PRIMARY KEY,identity_hash TEXT)");
        SQLite.execSQL(connection, "INSERT OR REPLACE INTO room_master_table (id,identity_hash) VALUES(42, '861f2c03f8de4ba0cd3370326df7bdd3')");
      }

      @Override
      public void dropAllTables(@NonNull final SQLiteConnection connection) {
        SQLite.execSQL(connection, "DROP TABLE IF EXISTS `pending_updates`");
        SQLite.execSQL(connection, "DROP TABLE IF EXISTS `local_scan_sessions`");
        SQLite.execSQL(connection, "DROP TABLE IF EXISTS `local_scan_items`");
      }

      @Override
      public void onCreate(@NonNull final SQLiteConnection connection) {
      }

      @Override
      public void onOpen(@NonNull final SQLiteConnection connection) {
        SQLite.execSQL(connection, "PRAGMA foreign_keys = ON");
        internalInitInvalidationTracker(connection);
      }

      @Override
      public void onPreMigrate(@NonNull final SQLiteConnection connection) {
        DBUtil.dropFtsSyncTriggers(connection);
      }

      @Override
      public void onPostMigrate(@NonNull final SQLiteConnection connection) {
      }

      @Override
      @NonNull
      public RoomOpenDelegate.ValidationResult onValidateSchema(
          @NonNull final SQLiteConnection connection) {
        final Map<String, TableInfo.Column> _columnsPendingUpdates = new HashMap<String, TableInfo.Column>(17);
        _columnsPendingUpdates.put("id", new TableInfo.Column("id", "TEXT", true, 1, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("set_code", new TableInfo.Column("set_code", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("set_id", new TableInfo.Column("set_id", "INTEGER", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("item_id", new TableInfo.Column("item_id", "INTEGER", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("item_type", new TableInfo.Column("item_type", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("serial_number", new TableInfo.Column("serial_number", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("model_number", new TableInfo.Column("model_number", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("previous_status", new TableInfo.Column("previous_status", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("new_status", new TableInfo.Column("new_status", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("remark", new TableInfo.Column("remark", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("updated_by_user_id", new TableInfo.Column("updated_by_user_id", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("updated_by_name", new TableInfo.Column("updated_by_name", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("created_at", new TableInfo.Column("created_at", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("retry_count", new TableInfo.Column("retry_count", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("sync_status", new TableInfo.Column("sync_status", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("last_error", new TableInfo.Column("last_error", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsPendingUpdates.put("synced_at", new TableInfo.Column("synced_at", "INTEGER", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        final Set<TableInfo.ForeignKey> _foreignKeysPendingUpdates = new HashSet<TableInfo.ForeignKey>(0);
        final Set<TableInfo.Index> _indicesPendingUpdates = new HashSet<TableInfo.Index>(0);
        final TableInfo _infoPendingUpdates = new TableInfo("pending_updates", _columnsPendingUpdates, _foreignKeysPendingUpdates, _indicesPendingUpdates);
        final TableInfo _existingPendingUpdates = TableInfo.read(connection, "pending_updates");
        if (!_infoPendingUpdates.equals(_existingPendingUpdates)) {
          return new RoomOpenDelegate.ValidationResult(false, "pending_updates(com.example.yakultscanner.data.db.PendingUpdateEntity).\n"
                  + " Expected:\n" + _infoPendingUpdates + "\n"
                  + " Found:\n" + _existingPendingUpdates);
        }
        final Map<String, TableInfo.Column> _columnsLocalScanSessions = new HashMap<String, TableInfo.Column>(8);
        _columnsLocalScanSessions.put("id", new TableInfo.Column("id", "TEXT", true, 1, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("title", new TableInfo.Column("title", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("created_by", new TableInfo.Column("created_by", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("created_at", new TableInfo.Column("created_at", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("updated_at", new TableInfo.Column("updated_at", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("notes", new TableInfo.Column("notes", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("status", new TableInfo.Column("status", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanSessions.put("sent_at", new TableInfo.Column("sent_at", "INTEGER", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        final Set<TableInfo.ForeignKey> _foreignKeysLocalScanSessions = new HashSet<TableInfo.ForeignKey>(0);
        final Set<TableInfo.Index> _indicesLocalScanSessions = new HashSet<TableInfo.Index>(0);
        final TableInfo _infoLocalScanSessions = new TableInfo("local_scan_sessions", _columnsLocalScanSessions, _foreignKeysLocalScanSessions, _indicesLocalScanSessions);
        final TableInfo _existingLocalScanSessions = TableInfo.read(connection, "local_scan_sessions");
        if (!_infoLocalScanSessions.equals(_existingLocalScanSessions)) {
          return new RoomOpenDelegate.ValidationResult(false, "local_scan_sessions(com.example.yakultscanner.data.db.LocalScanSessionEntity).\n"
                  + " Expected:\n" + _infoLocalScanSessions + "\n"
                  + " Found:\n" + _existingLocalScanSessions);
        }
        final Map<String, TableInfo.Column> _columnsLocalScanItems = new HashMap<String, TableInfo.Column>(12);
        _columnsLocalScanItems.put("id", new TableInfo.Column("id", "TEXT", true, 1, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("session_id", new TableInfo.Column("session_id", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("row_number", new TableInfo.Column("row_number", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("serial_number", new TableInfo.Column("serial_number", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("cell_phone_number", new TableInfo.Column("cell_phone_number", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("imei1", new TableInfo.Column("imei1", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("imei2", new TableInfo.Column("imei2", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("source", new TableInfo.Column("source", "TEXT", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("created_at", new TableInfo.Column("created_at", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("sent", new TableInfo.Column("sent", "INTEGER", true, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("sent_at", new TableInfo.Column("sent_at", "INTEGER", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        _columnsLocalScanItems.put("last_error", new TableInfo.Column("last_error", "TEXT", false, 0, null, TableInfo.CREATED_FROM_ENTITY));
        final Set<TableInfo.ForeignKey> _foreignKeysLocalScanItems = new HashSet<TableInfo.ForeignKey>(1);
        _foreignKeysLocalScanItems.add(new TableInfo.ForeignKey("local_scan_sessions", "CASCADE", "NO ACTION", Arrays.asList("session_id"), Arrays.asList("id")));
        final Set<TableInfo.Index> _indicesLocalScanItems = new HashSet<TableInfo.Index>(1);
        _indicesLocalScanItems.add(new TableInfo.Index("index_local_scan_items_session_id", false, Arrays.asList("session_id"), Arrays.asList("ASC")));
        final TableInfo _infoLocalScanItems = new TableInfo("local_scan_items", _columnsLocalScanItems, _foreignKeysLocalScanItems, _indicesLocalScanItems);
        final TableInfo _existingLocalScanItems = TableInfo.read(connection, "local_scan_items");
        if (!_infoLocalScanItems.equals(_existingLocalScanItems)) {
          return new RoomOpenDelegate.ValidationResult(false, "local_scan_items(com.example.yakultscanner.data.db.LocalScanItemEntity).\n"
                  + " Expected:\n" + _infoLocalScanItems + "\n"
                  + " Found:\n" + _existingLocalScanItems);
        }
        return new RoomOpenDelegate.ValidationResult(true, null);
      }
    };
    return _openDelegate;
  }

  @Override
  @NonNull
  protected InvalidationTracker createInvalidationTracker() {
    final Map<String, String> _shadowTablesMap = new HashMap<String, String>(0);
    final Map<String, Set<String>> _viewTables = new HashMap<String, Set<String>>(0);
    return new InvalidationTracker(this, _shadowTablesMap, _viewTables, "pending_updates", "local_scan_sessions", "local_scan_items");
  }

  @Override
  public void clearAllTables() {
    super.performClear(true, "pending_updates", "local_scan_sessions", "local_scan_items");
  }

  @Override
  @NonNull
  protected Map<Class<?>, List<Class<?>>> getRequiredTypeConverters() {
    final Map<Class<?>, List<Class<?>>> _typeConvertersMap = new HashMap<Class<?>, List<Class<?>>>();
    _typeConvertersMap.put(PendingUpdateDao.class, PendingUpdateDao_Impl.getRequiredConverters());
    _typeConvertersMap.put(LocalScanDao.class, LocalScanDao_Impl.getRequiredConverters());
    return _typeConvertersMap;
  }

  @Override
  @NonNull
  public Set<Class<? extends AutoMigrationSpec>> getRequiredAutoMigrationSpecs() {
    final Set<Class<? extends AutoMigrationSpec>> _autoMigrationSpecsSet = new HashSet<Class<? extends AutoMigrationSpec>>();
    return _autoMigrationSpecsSet;
  }

  @Override
  @NonNull
  public List<Migration> getAutoMigrations(
      @NonNull final Map<Class<? extends AutoMigrationSpec>, AutoMigrationSpec> autoMigrationSpecs) {
    final List<Migration> _autoMigrations = new ArrayList<Migration>();
    return _autoMigrations;
  }

  @Override
  public PendingUpdateDao pendingUpdateDao() {
    if (_pendingUpdateDao != null) {
      return _pendingUpdateDao;
    } else {
      synchronized(this) {
        if(_pendingUpdateDao == null) {
          _pendingUpdateDao = new PendingUpdateDao_Impl(this);
        }
        return _pendingUpdateDao;
      }
    }
  }

  @Override
  public LocalScanDao localScanDao() {
    if (_localScanDao != null) {
      return _localScanDao;
    } else {
      synchronized(this) {
        if(_localScanDao == null) {
          _localScanDao = new LocalScanDao_Impl(this);
        }
        return _localScanDao;
      }
    }
  }
}
