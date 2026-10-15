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
    /// in use. One anonymous POST per interval per owner module while at
    /// least one owner holds it; best-effort and silent — it must never
    /// disturb the UI. Identity is self-reported (machine + app user),
    /// matching the ping endpoint's trust model for this intranet display.
    /// Call Monitoring owns "CallMonitoring" while its views are open;
    /// the main shell owns "Workspace" for the whole login session.
    /// </summary>
    public static class ItcmPresenceReporter
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private static System.Threading.Timer _timer;
        private static readonly System.Collections.Generic.HashSet<string> _owners =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _gate = new object();

        public static TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

        public static bool Running => _timer != null;

        private static string NormalizeModule(string module)
        {
            return string.IsNullOrWhiteSpace(module) ? "CallMonitoring" : module.Trim();
        }

        public static void Start(string module = "CallMonitoring")
        {
            module = NormalizeModule(module);
            lock (_gate)
            {
                _owners.Add(module);
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

        public static void Stop(string module = "CallMonitoring")
        {
            module = NormalizeModule(module);
            lock (_gate)
            {
                _owners.Remove(module);
                if (_owners.Count > 0 || _timer == null)
                    return;

                try
                {
                    _timer.Dispose();
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

        public static void StopAll()
        {
            lock (_gate)
            {
                _owners.Clear();
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
            string[] modules;
            lock (_gate)
            {
                modules = new string[_owners.Count];
                _owners.CopyTo(modules);
            }
            foreach (var module in modules)
            {
                await SendBeatForModuleAsync(module).ConfigureAwait(false);
            }
        }

        private static async Task SendBeatForModuleAsync(string module)
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
                    + ",\"module\":" + JsonQuote(module)
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
