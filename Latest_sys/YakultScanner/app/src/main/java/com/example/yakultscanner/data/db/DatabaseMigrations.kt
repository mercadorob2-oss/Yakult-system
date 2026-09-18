package com.example.yakultscanner.data.db

import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

/**
 * All Room schema migrations for the Yakult Scanner local database.
 *
 * HOW TO ADD A MIGRATION
 * ──────────────────────
 * 1. Bump `version` in @Database (AppDatabase.kt) by 1 (e.g. 1 → 2).
 * 2. Add a Migration object below following the existing pattern.
 * 3. Register it in DatabaseModule.kt:
 *        .addMigrations(MIGRATION_1_2, MIGRATION_2_3, ...)
 * 4. Build the project. Room generates schemas/<new_version>.json —
 *    commit that file so the schema history is tracked in git.
 *
 * NEVER use fallbackToDestructiveMigration() in production — it silently
 * deletes all locally-pending sync data on an unhandled version bump.
 *
 * MIGRATION EXAMPLES
 * ──────────────────
 *   Add column (nullable):
 *     db.execSQL("ALTER TABLE pending_updates ADD COLUMN location TEXT")
 *
 *   Add column (non-nullable with default):
 *     db.execSQL("ALTER TABLE pending_updates ADD COLUMN priority INTEGER NOT NULL DEFAULT 0")
 *
 *   Add new table:
 *     db.execSQL("CREATE TABLE IF NOT EXISTS my_table (id TEXT PRIMARY KEY NOT NULL, ...)")
 *
 *   Rename / restructure (copy-and-replace pattern):
 *     db.execSQL("CREATE TABLE new_table (...)")
 *     db.execSQL("INSERT INTO new_table SELECT ... FROM old_table")
 *     db.execSQL("DROP TABLE old_table")
 *     db.execSQL("ALTER TABLE new_table RENAME TO old_table")
 */
object DatabaseMigrations {

    /**
     * v1 → v2 placeholder.
     *
     * No schema change was made in this version bump; this migration
     * establishes the migration infrastructure so future changes have
     * a safe, documented upgrade path instead of a destructive fallback.
     *
     * When a real schema change is needed:
     *   1. Replace the empty body below with the required ALTER/CREATE SQL.
     *   2. Bump AppDatabase.version to 2.
     *   3. Register this migration in DatabaseModule.kt.
     */
    val MIGRATION_1_2 = object : Migration(1, 2) {
        override fun migrate(db: SupportSQLiteDatabase) {
            db.execSQL("""
                CREATE TABLE IF NOT EXISTS local_scan_sessions (
                    id TEXT NOT NULL PRIMARY KEY,
                    title TEXT NOT NULL,
                    created_by TEXT,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL,
                    notes TEXT,
                    status TEXT NOT NULL,
                    sent_at INTEGER
                )
            """.trimIndent())

            db.execSQL("""
                CREATE TABLE IF NOT EXISTS local_scan_items (
                    id TEXT NOT NULL PRIMARY KEY,
                    session_id TEXT NOT NULL,
                    row_number INTEGER NOT NULL,
                    serial_number TEXT NOT NULL,
                    cell_phone_number TEXT,
                    imei1 TEXT,
                    imei2 TEXT,
                    source TEXT NOT NULL,
                    created_at INTEGER NOT NULL,
                    sent INTEGER NOT NULL,
                    sent_at INTEGER,
                    last_error TEXT,
                    FOREIGN KEY(session_id) REFERENCES local_scan_sessions(id) ON DELETE CASCADE
                )
            """.trimIndent())

            db.execSQL("CREATE INDEX IF NOT EXISTS index_local_scan_items_session_id ON local_scan_items(session_id)")
        }
    }
}

