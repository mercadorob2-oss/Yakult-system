using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.Monitoring.ViewModels
{
    public sealed class ConnectedDeviceRow : ViewModelBase
    {
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string Module { get; set; }
        public string ClientVersion { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public bool Online { get; set; }
        public bool IsThisDevice { get; set; }

        public string StatusDisplay => Online ? "● Online" : "○ Stale";

        public string LastSeenDisplay => LastSeenUtc == DateTime.MinValue
            ? ""
            : LastSeenUtc.ToLocalTime().ToString("MM/dd HH:mm") + " (" + FmtAgo(DateTime.UtcNow - LastSeenUtc) + ")";

        public static string FmtAgo(TimeSpan age)
        {
            if (age.TotalSeconds < 60) return Math.Max(0, (int)age.TotalSeconds) + "s ago";
            if (age.TotalMinutes < 60) return ((int)age.TotalMinutes) + "m ago";
            if (age.TotalHours < 24) return ((int)age.TotalHours) + "h ago";
            return ((int)age.TotalDays) + "d ago";
        }
    }

    /// <summary>
    /// Admin Portal > Connected Devices (WPF). Live view of desktops beating
    /// into dbo.CallClientPresence, all modules. STOP queues a surprise alert.
    /// </summary>
    public sealed class ConnectedDevicesViewModel : ViewModelBase
    {
        private readonly string _cs = DatabaseConfig.ConnectionString;

        private List<ConnectedDeviceRow> _all = new List<ConnectedDeviceRow>();
        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _filterOption = "All";
        private string _statusMessage = string.Empty;
        private int _onlineCount;
        private int _knownCount;

        public ObservableCollection<ConnectedDeviceRow> FilteredRows { get; } = new ObservableCollection<ConnectedDeviceRow>();

        public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
        public int OnlineCount { get => _onlineCount; private set => SetField(ref _onlineCount, value); }
        public int KnownCount { get => _knownCount; private set => SetField(ref _knownCount, value); }
        public bool IsEmpty => FilteredRows.Count == 0 && !IsLoading;

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public string FilterOption
        {
            get => _filterOption;
            set { if (SetField(ref _filterOption, value)) ApplyFilter(); }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var rows = new List<ConnectedDeviceRow>();
                string error = null;
                try
                {
                    using (var con = new SqlConnection(_cs))
                    {
                        await con.OpenAsync();
                        const string guard = "SELECT CASE WHEN OBJECT_ID('dbo.CallClientPresence') IS NOT NULL THEN 1 ELSE 0 END";
                        using (var cmd = new SqlCommand(guard, con))
                        {
                            if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
                                error = "dbo.CallClientPresence not found.";
                        }
                        if (error == null)
                        {
                            const string sql = @"
                                SELECT MachineName, UserName, ClientVersion, Module, FirstSeenUtc, LastSeenUtc,
                                       CASE WHEN LastSeenUtc >= DATEADD(minute, -15, SYSUTCDATETIME()) THEN 1 ELSE 0 END AS Online
                                FROM dbo.CallClientPresence
                                ORDER BY Online DESC, LastSeenUtc DESC";
                            using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 })
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                string self = Environment.MachineName;
                                while (await reader.ReadAsync())
                                {
                                    string machine = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                    rows.Add(new ConnectedDeviceRow
                                    {
                                        MachineName = machine,
                                        UserName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                        ClientVersion = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        Module = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                        FirstSeenUtc = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                                        LastSeenUtc = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                                        Online = !reader.IsDBNull(6) && reader.GetInt32(6) == 1,
                                        IsThisDevice = string.Equals(machine, self, StringComparison.OrdinalIgnoreCase)
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = "Could not load presence: " + ex.Message;
                }

                _all = rows;
                OnlineCount = _all.Count(r => r.Online);
                KnownCount = _all.Count;
                StatusMessage = error ?? "";
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public async Task<string> SendStopAsync(ConnectedDeviceRow row, string alertKind)
        {
            int id = await Task.Run(() =>
            {
                var repo = new Repositories.AdminAlertRepository();
                if (!repo.BroadcastTablesExist()) return -1;
                return repo.SendStopAlert(
                    row.MachineName, row.UserName,
                    Session.AppSession.CurrentUserId, Session.AppSession.CurrentUserName,
                    alertKind: string.IsNullOrEmpty(alertKind) ? Repositories.AdminAlertKinds.StopSound : alertKind);
            });
            string message = id > 0
                ? "Alert queued for " + row.MachineName + ". It pops there within ~20 seconds."
                : id == -1
                    ? "Broadcast tables are missing. Run Migration_AdminAlertBroadcast_CreateTable.sql on this database first."
                    : "Could not queue alert. Check the database connection.";
            StatusMessage = message;
            return message;
        }

        private void ApplyFilter()
        {
            var q = (_searchText ?? "").Trim().ToLowerInvariant();
            var filter = _filterOption ?? "All";

            var rows = _all.Where(r =>
            {
                if (!string.IsNullOrEmpty(q))
                {
                    bool match = (r.MachineName ?? "").ToLowerInvariant().Contains(q)
                              || (r.UserName ?? "").ToLowerInvariant().Contains(q)
                              || (r.Module ?? "").ToLowerInvariant().Contains(q);
                    if (!match) return false;
                }
                if (filter == "Online Only" && !r.Online) return false;
                if (filter == "Stale Only" && r.Online) return false;
                return true;
            }).ToList();

            FilteredRows.Clear();
            foreach (var r in rows) FilteredRows.Add(r);
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
