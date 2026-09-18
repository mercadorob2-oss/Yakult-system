using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Presence beat: tells the ITCM web server this desktop is actively
    /// using Call Monitoring. One anonymous POST per interval while an ITCM
    /// view is open; best-effort and silent — it must never disturb the UI.
    /// Identity is self-reported (machine + app user), matching the ping
    /// endpoint's trust model for this intranet ops display.
    /// </summary>
    public static class ItcmPresenceReporter
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private static System.Threading.Timer _timer;
        private static int _started;
        private static readonly object _gate = new object();

        public static TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

        public static bool Running => _timer != null;

        public static void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            lock (_gate)
            {
                if (_timer != null)
                    return;

                // Immediate first beat, then periodic.
                _ = SendBeatAsync();
                _timer = new System.Threading.Timer(
                    _ => { _ = SendBeatAsync(); },
                    null,
                    Interval,
                    Interval);
            }
        }

        public static void Stop()
        {
            Interlocked.Exchange(ref _started, 0);

            lock (_gate)
            {
                try
                {
                    _timer?.Dispose();
                }
                catch
                {
                }
                finally
                {
                    _timer = null;
                }
            }
        }

        private static async Task SendBeatAsync()
        {
            try
            {
                var baseUrl = (AppConfig.ItcmServerUrl ?? string.Empty).Trim().TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl))
                    return;

                string user;
                try
                {
                    user = Session.AppSession.IsLoggedIn
                        ? (Session.AppSession.CurrentUserName ?? string.Empty)
                        : string.Empty;
                }
                catch
                {
                    user = string.Empty;
                }

                string version;
                try
                {
                    var name = Assembly.GetExecutingAssembly().GetName();
                    version = name.Version == null ? string.Empty : name.Version.ToString();
                }
                catch
                {
                    version = string.Empty;
                }

                var payload = "{\"machineName\":" + JsonQuote(Environment.MachineName ?? string.Empty)
                    + ",\"userName\":" + JsonQuote(user)
                    + ",\"module\":\"CallMonitoring\""
                    + ",\"clientVersion\":" + JsonQuote(version) + "}";

                using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
                using (var response = await _client.PostAsync(baseUrl + "/api/itcm/presence", content).ConfigureAwait(false))
                {
                    // Best-effort: any status (including 404 on old servers) is fine.
                }
            }
            catch
            {
                // Silent by design.
            }
        }

        private static string JsonQuote(string value)
        {
            if (value == null)
                return "null";

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
