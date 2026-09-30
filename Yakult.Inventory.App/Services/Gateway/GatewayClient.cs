using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services.Gateway
{
    /// <summary>
    /// HTTP client for Yakult.Inventory.Gateway, which holds the database
    /// connection so the desktop app doesn't have to (same idea as ITCM).
    /// Sign in once with LoginAsync; later calls reuse the bearer token.
    /// Modules moved off direct SQL call GetAsync / PostAsync.
    /// </summary>
    public static class GatewayClient
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private static string _accessToken;
        private static DateTimeOffset _expiresAt;

        public static bool IsSignedIn => !string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _expiresAt;

        public static async Task<GatewaySession> LoginAsync(string userName, string password)
        {
            var response = await SendAsync<GatewayLoginResponse>(HttpMethod.Post, "api/auth/login",
                new { userName, password }, authorize: false).ConfigureAwait(false);

            _accessToken = response.AccessToken;
            _expiresAt = response.ExpiresAt;
            return response.Session;
        }

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
            _accessToken = null;
            _expiresAt = default;
        }

        public static Task<T> GetAsync<T>(string path) =>
            SendAsync<T>(HttpMethod.Get, path, null, authorize: true);

        public static Task<T> PostAsync<T>(string path, object body) =>
            SendAsync<T>(HttpMethod.Post, path, body, authorize: true);

        private static async Task<T> SendAsync<T>(HttpMethod method, string path, object body, bool authorize)
        {
            var baseUrl = (AppConfig.GatewayUrl ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
                throw new GatewayException(0, "GatewayUrl is not configured in App.config.");

            if (authorize && !IsSignedIn)
                throw new GatewayException((int)HttpStatusCode.Unauthorized, "Your session has expired. Please sign in again.");

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
