using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace Yakult.Inventory.App.Core
{
    public sealed class DatabaseConnectionProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public sealed class DatabaseConnectionProfile
    {
        public const string DefaultServer = "192.168.100.186,50301";
        public const string DefaultDatabase = "Yakult_Inventory_System_DEV";
        public const string DummyDatabase = "YIMS_PROD";
        private readonly SqlConnectionStringBuilder _builder;

        public DatabaseConnectionProfile(string connectionString)
        {
            _builder = string.IsNullOrWhiteSpace(connectionString)
                ? new SqlConnectionStringBuilder { DataSource = DefaultServer, InitialCatalog = DefaultDatabase, UserID = "remote_user", Encrypt = false, TrustServerCertificate = true }
                : new SqlConnectionStringBuilder(connectionString);
        }

        public string Server { get => _builder.DataSource; set => _builder.DataSource = (value ?? "").Trim(); }
        public string Database { get => _builder.InitialCatalog; set => _builder.InitialCatalog = (value ?? "").Trim(); }
        public string Username { get => _builder.UserID; set => _builder.UserID = (value ?? "").Trim(); }
        public string Password { get => _builder.Password; set => _builder.Password = value ?? ""; }
        public bool WindowsAuthentication { get => _builder.IntegratedSecurity; set => _builder.IntegratedSecurity = value; }
        public bool Encrypt { get => _builder.Encrypt; set => _builder.Encrypt = value; }
        public bool TrustServerCertificate { get => _builder.TrustServerCertificate; set => _builder.TrustServerCertificate = value; }
        public string EnvironmentLabel => Database.Equals(DefaultDatabase, StringComparison.OrdinalIgnoreCase) ? "Working database"
            : Database.Equals(DummyDatabase, StringComparison.OrdinalIgnoreCase) ? "Dummy database" : "Custom database";

        public string BuildConnectionString()
        {
            if (string.IsNullOrWhiteSpace(Server)) throw new ArgumentException("Enter the server address.");
            if (string.IsNullOrWhiteSpace(Database)) throw new ArgumentException("Enter the database name.");
            var result = new SqlConnectionStringBuilder(_builder.ConnectionString);
            if (WindowsAuthentication) { result.Remove("User ID"); result.Remove("Password"); }
            else if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password)) throw new ArgumentException("Enter the SQL username and password.");
            return result.ConnectionString;
        }

        public IReadOnlyList<DatabaseConnectionProperty> GetAllProperties()
        {
            var result = new SqlConnectionStringBuilder(BuildConnectionString());
            return result.Keys.Cast<string>().OrderBy(key => key).Select(key => new DatabaseConnectionProperty { Name = key, Value = Convert.ToString(result[key], CultureInfo.InvariantCulture) }).ToList();
        }
    }
}
