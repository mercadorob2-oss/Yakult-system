package com.example.yakultscanner.data.db;

import androidx.sqlite.db.SupportSQLiteDatabase;

/**
 * All Room schema migrations for the Yakult Scanner local database.
 *
 * HOW TO ADD A MIGRATION
 * ──────────────────────
 * 1. Bump `version` in @Database (AppDatabase.kt) by 1 (e.g. 1 → 2).
 * 2. Add a Migration object below following the existing pattern.
 * 3. Register it in DatabaseModule.kt:
 *       .addMigrations(MIGRATION_1_2, MIGRATION_2_3, ...)
 * 4. Build the project. Room generates schemas/<new_version>.json —
 *   commit that file so the schema history is tracked in git.
 *
 * NEVER use fallbackToDestructiveMigration() in production — it silently
 * deletes all locally-pending sync data on an unhandled version bump.
 *
 * MIGRATION EXAMPLES
 * ──────────────────
 *  Add column (nullable):
 *    db.execSQL("ALTER TABLE pending_updates ADD COLUMN location TEXT")
 *
 *  Add column (non-nullable with default):
 *    db.execSQL("ALTER TABLE pending_updates ADD COLUMN priority INTEGER NOT NULL DEFAULT 0")
 *
 *  Add new table:
 *    db.execSQL("CREATE TABLE IF NOT EXISTS my_table (id TEXT PRIMARY KEY NOT NULL, ...)")
 *
 *  Rename / restructure (copy-and-replace pattern):
 *    db.execSQL("CREATE TABLE new_table (...)")
 *    db.execSQL("INSERT INTO new_table SELECT ... FROM old_table")
 *    db.execSQL("DROP TABLE old_table")
 *    db.execSQL("ALTER TABLE new_table RENAME TO old_table")
 */
@kotlin.Metadata(mv = {2, 2, 0}, k = 1, xi = 48, d1 = {"\u0000\u0014\n\u0002\u0018\u0002\n\u0002\u0010\u0000\n\u0002\b\u0003\n\u0002\u0018\u0002\n\u0002\b\u0003\b\u00c6\u0002\u0018\u00002\u00020\u0001B\t\b\u0002\u00a2\u0006\u0004\b\u0002\u0010\u0003R\u0011\u0010\u0004\u001a\u00020\u0005\u00a2\u0006\b\n\u0000\u001a\u0004\b\u0006\u0010\u0007\u00a8\u0006\b"}, d2 = {"Lcom/example/yakultscanner/data/db/DatabaseMigrations;", "", "<init>", "()V", "MIGRATION_1_2", "Landroidx/room/migration/Migration;", "getMIGRATION_1_2", "()Landroidx/room/migration/Migration;", "app_prodDebug"})
public final class DatabaseMigrations {
    
    /**
     * v1 → v2 placeholder.
     *
     * No schema change was made in this version bump; this migration
     * establishes the migration infrastructure so future changes have
     * a safe, documented upgrade path instead of a destructive fallback.
     *
     * When a real schema change is needed:
     *  1. Replace the empty body below with the required ALTER/CREATE SQL.
     *  2. Bump AppDatabase.version to 2.
     *  3. Register this migration in DatabaseModule.kt.
     */
    @org.jetbrains.annotations.NotNull()
    private static final androidx.room.migration.Migration MIGRATION_1_2 = null;
    @org.jetbrains.annotations.NotNull()
    public static final com.example.yakultscanner.data.db.DatabaseMigrations INSTANCE = null;
    
    private DatabaseMigrations() {
        super();
    }
    
    /**
     * v1 → v2 placeholder.
     *
     * No schema change was made in this version bump; this migration
     * establishes the migration infrastructure so future changes have
     * a safe, documented upgrade path instead of a destructive fallback.
     *
     * When a real schema change is needed:
     *  1. Replace the empty body below with the required ALTER/CREATE SQL.
     *  2. Bump AppDatabase.version to 2.
     *  3. Register this migration in DatabaseModule.kt.
     */
    @org.jetbrains.annotations.NotNull()
    public final androidx.room.migration.Migration getMIGRATION_1_2() {
        return null;
    }
}