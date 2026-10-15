using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Yakult.Inventory.App.Services
{
    public sealed class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// Inquiry-only chat client for the office Ollama server. Sends nothing
    /// but the user's typed messages plus a fixed system prompt: no session
    /// data, no inventory data, no actions.
    /// </summary>
    public static class OllamaChatService
    {
        public const string SystemPrompt = "You are Yakult's internal IT assistant inside the Yakult Inventory desktop app. Answer basic inquiries concisely. If you do not know something, say so instead of guessing.";

        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(180)
        };

        public static async Task<string> AskAsync(List<ChatMessage> history, string question, CancellationToken ct)
        {
            string baseUrl = (Core.AppConfig.OllamaBaseUrl ?? "http://localhost:11434").Trim().TrimEnd('/');
            string model = (Core.AppConfig.OllamaModel ?? "qwen2.5:1.5b").Trim();
            if (model.Length == 0) model = "qwen2.5:1.5b";

            var messages = new List<object> { new { role = "system", content = SystemPrompt } };
            if (history != null)
            {
                foreach (var m in history)
                {
                    if (m == null) continue;
                    if (m.Role != "user" && m.Role != "assistant") continue;
                    messages.Add(new { role = m.Role, content = m.Content ?? "" });
                }
            }
            messages.Add(new { role = "user", content = question ?? "" });

            string payload = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                model = model,
                messages = messages,
                stream = false
            });

            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            using (var response = await _client.PostAsync(baseUrl + "/api/chat", content, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string body = "";
                    try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }
                    if (body.Length > 200) body = body.Substring(0, 200);
                    throw new HttpRequestException("Ollama error " + (int)response.StatusCode + ": " + body);
                }
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string reply = (string)JObject.Parse(json).SelectToken("message.content");
                return (reply ?? "").Trim();
            }
        }
    }
}
