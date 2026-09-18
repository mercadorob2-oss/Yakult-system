using System;
using System.IO;
using System.Text.Json;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Reads the database connection string from appsettings JSON files,
    /// mirroring the ASP.NET Core appsettings pattern.
    ///
    /// Load order (first non-empty value wins):
    ///   1. appsettings.{DOTNET_ENVIRONMENT}.json  (e.g. appsettings.Development.json)
    ///   2. appsettings.Development.json           (present on dev machines, absent in production)
    ///   3. appsettings.json                        (base / production config)
    /// </summary>
    internal static class AppSettingsLoader
    {
        internal static string LoadConnectionString()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Environment-variable-driven override (mirrors ASP.NET Core convention)
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                   ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (!string.IsNullOrWhiteSpace(env))
            {
                var envConn = TryRead(Path.Combine(baseDir, $"appsettings.{env}.json"));
                if (envConn != null) return envConn;
            }

            // 2. Development file — present on dev machines, excluded from production packages
            var devConn = TryRead(Path.Combine(baseDir, "appsettings.Development.json"));
            if (devConn != null) return devConn;

            // 3. Base config (production default)
            return TryRead(Path.Combine(baseDir, "appsettings.json"));
        }

        private static string TryRead(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
                using var doc = JsonDocument.Parse(json, options);

                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                    cs.TryGetProperty("DefaultConnection", out var conn))
                {
                    var value = conn.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch
            {
                // Malformed JSON or missing file — fall through to next source
            }

            return null;
        }
    }
}
