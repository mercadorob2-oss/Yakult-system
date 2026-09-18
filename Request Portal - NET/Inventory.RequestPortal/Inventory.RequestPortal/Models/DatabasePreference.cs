namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// Stores user's database selection preference (DEV-ONLY).
    /// Stored in session to allow per-user database switching in development.
    /// </summary>
    public class DatabasePreference
    {
        /// <summary>Selected remote database name (preset list).</summary>
        public string DatabaseName { get; set; } = "Yakult_Inventory_System_DEV";

        /// <summary>When true, use the local Windows Auth connection instead of the remote server.</summary>
        public bool UseLocalDatabase { get; set; } = false;

        /// <summary>Database name on the local server (used when UseLocalDatabase is true).</summary>
        public string? LocalDatabaseName { get; set; }

        /// <summary>When true, use a fully custom connection string entered by the user.</summary>
        public bool UseCustomConnection { get; set; } = false;

        /// <summary>Full custom connection string (built from user-supplied fields).</summary>
        public string? CustomConnectionString { get; set; }

        public DateTime LastChanged { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Available database options for switching.
    /// Databases available on Server 192.168.100.186,50301
    /// </summary>
    public static class DatabaseOptions
    {
        public static readonly List<string> AvailableDatabases = new()
        {
            "Yakult_Inventory_System_DEV",  // Dev database (real data)
            "YIMS",                          // Dummy database (test data)
            "YIMS_PROD"                      // Production YIMS database
        };
    }
}
