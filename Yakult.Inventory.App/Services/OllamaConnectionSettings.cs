using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yakult.Inventory.App.Services
{
    public sealed class OllamaConnectionCheck
    {
        public string BaseUrl { get; internal set; }
        public string Version { get; internal set; }
        public string Model { get; internal set; }
        public IReadOnlyList<string> Models { get; internal set; }
        public bool ModelAvailable { get { return Model != null; } }
    }

    /// <summary>Checks a candidate connection without changing the active settings or sending chat data.</summary>
    public static class OllamaConnectionSettings
    {
        public static string NormalizeBaseUrl(string value)
        {
            value=(value ?? "").Trim();
            if(value.Length==0) throw new ArgumentException("Enter the AI computer's server address.");
            if(!value.Contains("://")) value="http://"+value;
            Uri address;
            if(!Uri.TryCreate(value,UriKind.Absolute,out address) ||
                (address.Scheme!=Uri.UriSchemeHttp && address.Scheme!=Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(address.Host) || !string.IsNullOrEmpty(address.UserInfo) ||
                !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
                throw new ArgumentException("Use an HTTP/HTTPS server address without credentials, a query or a fragment.");
            IPAddress ip;
            if(IPAddress.TryParse(address.DnsSafeHost.Trim('[',']'),out ip) && (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)))
                throw new ArgumentException("Enter the AI computer's actual name or IP address. 0.0.0.0 is a listening address.");
            string path=address.AbsolutePath.TrimEnd('/');
            if(path.EndsWith("/api",StringComparison.OrdinalIgnoreCase) || path.IndexOf("/api/",StringComparison.OrdinalIgnoreCase)>=0)
                throw new ArgumentException("Enter the server address only, without /api/tags, /api/chat or other API paths.");
            return address.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        internal static string ValidateModel(string model)
        {
            model=(model ?? "").Trim();
            if(model.Length==0) throw new ArgumentException("Choose or enter an installed model.");
            if(model.Length>200 || model.Any(char.IsWhiteSpace) || model.Any(char.IsControl))
                throw new ArgumentException("Enter a model name such as qwen2.5:3b, without spaces.");
            return model;
        }

        public static async Task<OllamaConnectionCheck> ValidateAsync(string baseUrl,string model,CancellationToken ct)
        {
            using(var client=new HttpClient {Timeout=Timeout.InfiniteTimeSpan})
                return await ValidateAsync(baseUrl,model,client,ct).ConfigureAwait(false);
        }

        internal static async Task<OllamaConnectionCheck> ValidateAsync(string baseUrl,string model,HttpClient client,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string url=NormalizeBaseUrl(baseUrl);model=ValidateModel(model);
            using(var budget=CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                budget.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    string version=await ReadVersionAsync(url,client,budget.Token).ConfigureAwait(false);
                    var models=await ReadModelsAsync(url,client,budget.Token).ConfigureAwait(false);
                    string lookup=model.Substring(model.LastIndexOf('/')+1).Contains(":")?model:model+":latest";
                    string installed=models.FirstOrDefault(m=>m.Equals(model,StringComparison.OrdinalIgnoreCase) || m.Equals(lookup,StringComparison.OrdinalIgnoreCase));
                    return new OllamaConnectionCheck {BaseUrl=url,Version=version,Models=models.AsReadOnly(),Model=installed};
                }
                catch(OperationCanceledException) when(!ct.IsCancellationRequested)
                {throw new TimeoutException("The AI server did not respond within 15 seconds. Check its address, network and firewall.");}
            }
        }

        internal static async Task<string> ReadVersionAsync(string url,HttpClient client,CancellationToken ct)
        {
            var json=await ReadJsonAsync(url,"/api/version",client,ct).ConfigureAwait(false);
            string version=json["version"]?.Type==JTokenType.String?(string)json["version"]:null;
            if(string.IsNullOrWhiteSpace(version)) throw InvalidServer();
            return version;
        }

        internal static async Task<List<string>> ReadModelsAsync(string url,HttpClient client,CancellationToken ct)
        {
            var json=await ReadJsonAsync(url,"/api/tags",client,ct).ConfigureAwait(false);
            var models=json["models"] as JArray;
            if(models==null) throw InvalidServer();
            return models.OfType<JObject>().Where(m=>m["name"]?.Type==JTokenType.String)
                .Select(m=>(string)m["name"]).Where(m=>!string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m=>m,StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static async Task<JObject> ReadJsonAsync(string url,string route,HttpClient client,CancellationToken ct)
        {
            try
            {
                using(var response=await client.GetAsync(url+route,ct).ConfigureAwait(false))
                {
                    if(!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("The server returned HTTP "+(int)response.StatusCode+". Check the Ollama server address and network access.");
                    string body=await response.Content.ReadAsStringAsync().ConfigureAwait(false);ct.ThrowIfCancellationRequested();
                    try {return JObject.Parse(body);}
                    catch(JsonException) {throw InvalidServer();}
                }
            }
            catch(HttpRequestException)
            {throw new InvalidOperationException("Cannot reach the AI server. Check its address, that Ollama is running, and that the firewall allows its port.");}
        }
        private static InvalidOperationException InvalidServer()
        {return new InvalidOperationException("This address did not return a valid Ollama response. Check the server address and port.");}
    }
}
