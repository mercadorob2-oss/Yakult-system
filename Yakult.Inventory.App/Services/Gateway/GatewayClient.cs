using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services.Gateway
{
    /// <summary>
    /// HTTP client for Yakult.Inventory.Gateway, which holds the database
    /// connection so the desktop app doesn't have to (same idea as ITCM).
    /// Sign in once with LoginAsync; later calls reuse the bearer token, which is
    /// refreshed automatically while the app is open.
    ///
    /// Repositories moved to the gateway check <see cref="UseForData"/> and call
    /// GetAsync / PostAsync / PutAsync / DeleteAsync (or the blocking Get / Post /
    /// Put / Delete from synchronous methods). Every await here uses
    /// ConfigureAwait(false), so blocking on these from the UI thread cannot deadlock.
    /// </summary>
    public static class GatewayClient
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Refresh once the token is this close to expiring (tokens last 12 hours).
        private static readonly TimeSpan RefreshWindow = TimeSpan.FromHours(3);
        private static readonly TimeSpan RefreshTimerInterval = TimeSpan.FromMinutes(15);

        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private static Timer _refreshTimer;

        private static string _accessToken;
        private static DateTimeOffset _expiresAt;
        private static int _sessionExpiredRaised;

        public static bool IsSignedIn => !string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _expiresAt;

        /// <summary>
        /// True when repositories should call the gateway instead of SQL: the app has a
        /// GatewayUrl and is not a bootstrapped console host (the ITCM scheduler keeps SQL).
        /// </summary>
        public static bool UseForData => AppConfig.UseGateway && !DatabaseConfig.IsBootstrapped;

        /// <summary>The name typed at sign-in (may differ from the display name), for re-sign-in.</summary>
        public static string LoginName { get; private set; }

        /// <summary>
        /// Raised once when the gateway rejects the token (expired, or the account was
        /// deactivated). Raised on a background thread: marshal to the UI yourself.
        /// </summary>
        public static event EventHandler SessionExpired;

        /// <summary>The database this session signed in to (set by LoginAsync).</summary>
        public static GatewayEnvironmentInfo CurrentEnvironment { get; private set; }

        // The Ctrl+Shift+D choice, per Windows user. Holds only an environment name
        // such as "Test"; the gateway maps it to a connection on the server.
        private static readonly string EnvironmentFile = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "Yakult", "Inventory", "gateway-environment.txt");

        /// <summary>Environment name to sign in to. Null = the gateway's default (Production).</summary>
        public static string SelectedEnvironment
        {
            get
            {
                try
                {
                    var name = File.Exists(EnvironmentFile) ? File.ReadAllText(EnvironmentFile).Trim() : null;
                    return string.IsNullOrEmpty(name) ? null : name;
                }
                catch (IOException) { return null; }
                catch (UnauthorizedAccessException) { return null; }
            }
            set
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EnvironmentFile));
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (File.Exists(EnvironmentFile)) File.Delete(EnvironmentFile);
                }
                else
                {
                    File.WriteAllText(EnvironmentFile, value.Trim());
                }
            }
        }

        // ── Sign-in ──────────────────────────────────────────────────────────

        public static async Task<GatewaySession> LoginAsync(string userName, string password)
        {
            var response = await SendAsync<GatewayLoginResponse>(HttpMethod.Post, "api/auth/login",
                new { userName, password, environment = SelectedEnvironment }, authorize: false).ConfigureAwait(false);

            _accessToken = response.AccessToken;
            _expiresAt = response.ExpiresAt;
            CurrentEnvironment = response.Environment;
            LoginName = userName;
            Interlocked.Exchange(ref _sessionExpiredRaised, 0);
            StartRefreshTimer();
            return response.Session;
        }

        /// <summary>The databases the switcher can offer. No sign-in needed.</summary>
        public static Task<List<GatewayEnvironmentInfo>> GetEnvironmentsAsync() =>
            SendAsync<List<GatewayEnvironmentInfo>>(HttpMethod.Get, "api/environments", null, authorize: false);

        /// <summary>
        /// Migration bridge: the connection string for screens that still use SQL
        /// directly. Held in memory only; never written to disk.
        /// </summary>
        public static async Task<string> GetClientConnectionStringAsync()
        {
            var result = await SendAsync<JObject>(HttpMethod.Get, "api/session/db-connection", null, authorize: true)
                .ConfigureAwait(false);
            return (string)result["connectionString"];
        }

        public static void SignOut()
        {
            StopRefreshTimer();
            _accessToken = null;
            _expiresAt = default;
            CurrentEnvironment = null;
            LoginName = null;
        }

        // ── Data calls ───────────────────────────────────────────────────────

        public static Task<T> GetAsync<T>(string path) =>
            SendAsync<T>(HttpMethod.Get, path, null, authorize: true);

        public static Task<T> PostAsync<T>(string path, object body) =>
            SendAsync<T>(HttpMethod.Post, path, body ?? new object(), authorize: true);

        public static Task<T> PutAsync<T>(string path, object body) =>
            SendAsync<T>(HttpMethod.Put, path, body ?? new object(), authorize: true);

        public static Task<T> DeleteAsync<T>(string path) =>
            SendAsync<T>(HttpMethod.Delete, path, null, authorize: true);

        // Blocking versions for repositories whose public methods are synchronous.
        public static T Get<T>(string path) => GetAsync<T>(path).GetAwaiter().GetResult();
        public static T Post<T>(string path, object body) => PostAsync<T>(path, body).GetAwaiter().GetResult();
        public static T Put<T>(string path, object body) => PutAsync<T>(path, body).GetAwaiter().GetResult();
        public static T Delete<T>(string path) => DeleteAsync<T>(path).GetAwaiter().GetResult();

        // ── Token refresh ────────────────────────────────────────────────────

        private static void StartRefreshTimer()
        {
            StopRefreshTimer();
            _refreshTimer = new Timer(_ => { _ = RefreshIfDueAsync(); }, null, RefreshTimerInterval, RefreshTimerInterval);
        }

        private static void StopRefreshTimer()
        {
            var timer = Interlocked.Exchange(ref _refreshTimer, null);
            timer?.Dispose();
        }

        private static async Task RefreshIfDueAsync()
        {
            if (!IsSignedIn || _expiresAt - DateTimeOffset.UtcNow > RefreshWindow)
                return;

            await _refreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Another caller may have refreshed while we waited.
                if (!IsSignedIn || _expiresAt - DateTimeOffset.UtcNow > RefreshWindow)
                    return;

                var result = await SendCoreAsync<JObject>(HttpMethod.Post, "api/auth/refresh", new object(), authorize: true)
                    .ConfigureAwait(false);
                _accessToken = (string)result["accessToken"];
                _expiresAt = result["expiresAt"].ToObject<DateTimeOffset>();
            }
            catch (GatewayException ex) when (ex.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                OnSessionExpired();
            }
            catch (GatewayException)
            {
                // Server unreachable: keep the current token and try again on the next call or tick.
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>Lets SessionExpired fire again (after the user postponed signing back in).</summary>
        public static void AllowSessionExpiredNotice() => Interlocked.Exchange(ref _sessionExpiredRaised, 0);

        private static void OnSessionExpired()
        {
            _accessToken = null;
            _expiresAt = default;
            StopRefreshTimer();
            if (Interlocked.Exchange(ref _sessionExpiredRaised, 1) == 0)
                SessionExpired?.Invoke(null, EventArgs.Empty);
        }

        // ── HTTP ─────────────────────────────────────────────────────────────

        private static async Task<T> SendAsync<T>(HttpMethod method, string path, object body, bool authorize)
        {
            if (authorize)
            {
                if (!IsSignedIn)
                {
                    if (!string.IsNullOrEmpty(LoginName))
                        OnSessionExpired();
                    throw new GatewayException((int)HttpStatusCode.Unauthorized, "Your session has expired. Please sign in again.");
                }
                await RefreshIfDueAsync().ConfigureAwait(false);
            }

            try
            {
                return await SendCoreAsync<T>(method, path, body, authorize).ConfigureAwait(false);
            }
            catch (GatewayException ex) when (authorize && ex.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                OnSessionExpired();
                throw;
            }
        }

        private static async Task<T> SendCoreAsync<T>(HttpMethod method, string path, object body, bool authorize)
        {
            var baseUrl = (AppConfig.GatewayUrl ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
                throw new GatewayException(0, "GatewayUrl is not configured in App.config.");

            using (var request = new HttpRequestMessage(method, baseUrl + "/" + path.TrimStart('/')))
            {
                if (authorize)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                if (body != null)
                    request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    throw new GatewayException(0, $"Cannot reach the sign-in server at {baseUrl}.", ex);
                }

                using (response)
                {
                    var text = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new GatewayException((int)response.StatusCode, ReadError(text) ?? response.ReasonPhrase);

                    return string.IsNullOrWhiteSpace(text) ? default : JsonConvert.DeserializeObject<T>(text);
                }
            }
        }

        private static string ReadError(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var json = JObject.Parse(text);
                return (string)json["error"] ?? (string)json["detail"] ?? (string)json["title"];
            }
            catch (JsonException)
            {
                return text.Length > 200 ? text.Substring(0, 200) : text;
            }
        }
    }
}
