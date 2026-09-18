using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;

namespace Yakult.Inventory.App.Repositories
{
    internal static class CallSchemaGate
    {
        private sealed class CacheEntry
        {
            public bool Exists { get; set; }
            public DateTime ExpiresUtc { get; set; }
        }

        private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();
        private static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromSeconds(30);

        private static string GetConnectionScope(SqlConnection connection)
        {
            if (connection == null)
                return string.Empty;

            try
            {
                var dataSource = connection.DataSource ?? string.Empty;
                var database = connection.Database ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(dataSource) || !string.IsNullOrWhiteSpace(database))
                    return dataSource + "|" + database;
            }
            catch
            {
            }

            return connection.ConnectionString ?? string.Empty;
        }

        private static bool TryGetCached(string key, out bool exists)
        {
            lock (CacheLock)
            {
                if (!Cache.TryGetValue(key, out var entry))
                {
                    exists = false;
                    return false;
                }

                if (entry.ExpiresUtc <= DateTime.UtcNow)
                {
                    Cache.Remove(key);
                    exists = false;
                    return false;
                }

                exists = entry.Exists;
                return true;
            }
        }

        private static void SetCached(string key, bool exists)
        {
            lock (CacheLock)
            {
                Cache[key] = new CacheEntry
                {
                    Exists = exists,
                    ExpiresUtc = exists ? DateTime.MaxValue : DateTime.UtcNow.Add(NegativeCacheDuration)
                };
            }
        }

        internal static Task<bool> ObjectExistsAsync(SqlConnection connection, string objectName, string objectType)
            => ObjectExistsAsync(connection, objectName, objectType, tx: null);

        internal static async Task<bool> ObjectExistsAsync(SqlConnection connection, string objectName, string objectType, SqlTransaction tx)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var key = GetConnectionScope(connection) + "|OBJ|" + objectType + "|" + objectName;
            if (TryGetCached(key, out var cachedExists))
                return cachedExists;

            var exists = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT CASE WHEN OBJECT_ID(@ObjectName, @ObjectType) IS NULL THEN 0 ELSE 1 END",
                    new { ObjectName = objectName, ObjectType = objectType },
                    transaction: tx));

            SetCached(key, exists == 1);

            return exists == 1;
        }

        internal static Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
            => ObjectExistsAsync(connection, tableName, "U", tx: null);

        internal static Task<bool> TableExistsAsync(SqlConnection connection, string tableName, SqlTransaction tx)
            => ObjectExistsAsync(connection, tableName, "U", tx);

        internal static Task<bool> ViewExistsAsync(SqlConnection connection, string viewName)
            => ObjectExistsAsync(connection, viewName, "V", tx: null);

        internal static Task<bool> ViewExistsAsync(SqlConnection connection, string viewName, SqlTransaction tx)
            => ObjectExistsAsync(connection, viewName, "V", tx);

        internal static Task<bool> StoredProcExistsAsync(SqlConnection connection, string procName)
            => ObjectExistsAsync(connection, procName, "P", tx: null);

        internal static Task<bool> StoredProcExistsAsync(SqlConnection connection, string procName, SqlTransaction tx)
            => ObjectExistsAsync(connection, procName, "P", tx);

        internal static async Task<bool> ColumnExistsAsync(SqlConnection connection, string tableName, string columnName)
            => await ColumnExistsAsync(connection, tableName, columnName, tx: null);

        internal static async Task<bool> ColumnExistsAsync(SqlConnection connection, string tableName, string columnName, SqlTransaction tx)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var key = GetConnectionScope(connection) + "|COL|" + tableName + "|" + columnName;
            if (TryGetCached(key, out var cachedExists))
                return cachedExists;

            var exists = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT CASE WHEN COL_LENGTH(@TableName, @ColumnName) IS NULL THEN 0 ELSE 1 END",
                    new { TableName = tableName, ColumnName = columnName },
                    transaction: tx));

            SetCached(key, exists == 1);

            return exists == 1;
        }
    }
}

