using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace TextDispatch.Ai
{
    /// <summary>
    /// The local model, over whichever server is running.
    ///
    /// Ollama is the default: it speaks its own /api/chat shape, and also exposes an
    /// OpenAI-compatible endpoint. Both are understood here, so the same plugin works against
    /// Ollama, LM Studio, or anything else that answers chat completions - the provider only
    /// decides the default port and which request shape to send.
    ///
    /// Every model failure is recoverable by design: the caller always has a scripted line to fall
    /// back on, so nothing here is allowed to take the conversation down with it.
    /// </summary>
    internal static class LocalModel
    {
        public const string OllamaEndpoint = "http://localhost:11434/api/chat";
        public const string LmStudioEndpoint = "http://localhost:1234/v1/chat/completions";

        public static string DefaultEndpoint(string provider)
        {
            if ("ollama".Equals(provider, StringComparison.OrdinalIgnoreCase)) return OllamaEndpoint;
            if ("lmstudio".Equals(provider, StringComparison.OrdinalIgnoreCase)) return LmStudioEndpoint;
            return LmStudioEndpoint;   // anything else: assume an OpenAI-compatible server
        }

        /// <summary>Ollama's own endpoint uses a different request and response shape.</summary>
        private static bool IsOllamaNative(string endpoint)
        {
            return endpoint.IndexOf("/api/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string Endpoint(Settings settings)
        {
            return string.IsNullOrWhiteSpace(settings.AiEndpoint)
                ? DefaultEndpoint(settings.AiProvider)
                : settings.AiEndpoint.Trim();
        }

        // ------------------------------------------------------------------ chat

        public static string Chat(Settings settings, string system, string user, out string error)
        {
            return Chat(settings, system, user, settings.AiTimeoutMs, out error);
        }

        /// <summary>
        /// As above with an explicit deadline, which the warm-up needs: loading a model is allowed to
        /// take minutes, while answering a sentence is not.
        /// </summary>
        public static string Chat(Settings settings, string system, string user, int timeoutMs, out string error)
        {
            error = null;
            try
            {
                var endpoint = Endpoint(settings);
                var ollama = IsOllamaNative(endpoint);

                var model = EnsureModel(settings, ollama, out error);
                if (model == null) return null;

                var payload = BuildPayload(settings, model, system, user, ollama);

                var request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;

                var bytes = Encoding.UTF8.GetBytes(payload);
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var raw = reader.ReadToEnd();
                    var text = ExtractContent(raw, ollama);
                    if (string.IsNullOrEmpty(text))
                    {
                        error = "the model returned nothing usable";
                        return null;
                    }
                    return Clean(text);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private static string BuildPayload(Settings settings, string model, string system, string user, bool ollama)
        {
            var json = new StringBuilder();
            json.Append("{\"model\":\"").Append(Escape(model)).Append("\",");

            if (ollama)
            {
                // Ollama takes sampling options in a nested object and does not stream unless asked.
                json.Append("\"stream\":false,\"options\":{");
                json.Append("\"temperature\":").Append(settings.AiTemperature.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
                json.Append("\"num_predict\":").Append(settings.AiMaxTokens);
                json.Append("},");
            }
            else
            {
                json.Append("\"temperature\":").Append(settings.AiTemperature.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
                json.Append("\"max_tokens\":").Append(settings.AiMaxTokens).Append(',');
                json.Append("\"stream\":false,");
            }

            json.Append("\"messages\":[");
            json.Append("{\"role\":\"system\",\"content\":\"").Append(Escape(system)).Append("\"},");
            json.Append("{\"role\":\"user\",\"content\":\"").Append(Escape(user)).Append("\"}");
            json.Append("]}");
            return json.ToString();
        }

        private static string ExtractContent(string raw, bool ollama)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var root = serializer.Deserialize<Dictionary<string, object>>(raw);
                if (root == null) return null;

                // Ollama: { "message": { "content": "..." } }
                if (root.ContainsKey("message"))
                {
                    var message = root["message"] as Dictionary<string, object>;
                    if (message != null && message.ContainsKey("content")) return message["content"] as string;
                }

                // OpenAI-compatible: { "choices": [ { "message": { "content": "..." } } ] }
                if (root.ContainsKey("choices"))
                {
                    var choices = AsItems(root["choices"]);
                    if (choices.Count > 0)
                    {
                        var first = choices[0] as Dictionary<string, object>;
                        if (first != null)
                        {
                            var message = first.ContainsKey("message") ? first["message"] as Dictionary<string, object> : null;
                            if (message != null && message.ContainsKey("content")) return message["content"] as string;
                            if (first.ContainsKey("text")) return first["text"] as string;
                        }
                    }
                }

                // Ollama's completion shape, if something answers there instead.
                if (root.ContainsKey("response")) return root["response"] as string;

                return null;
            }
            catch (Exception ex)
            {
                Log.Error("parse model reply", ex);
                return null;
            }
        }

        /// <summary>
        /// Turn whatever the model produced into something a person could have said out loud.
        /// Models asked for one short line return paragraphs, stage directions, or their own name
        /// as a label - all of that is stripped rather than shown.
        /// </summary>
        private static string Clean(string text)
        {
            var cleaned = text.Replace("\r", " ").Replace("\n", " ").Trim();

            cleaned = cleaned.Trim('"', '\'', ' ');
            if (cleaned.StartsWith("*", StringComparison.Ordinal) && cleaned.EndsWith("*", StringComparison.Ordinal) && cleaned.Length > 2)
                cleaned = cleaned.Trim('*').Trim();

            var colon = cleaned.IndexOf(':');
            if (colon > 0 && colon < 24) cleaned = cleaned.Substring(colon + 1).Trim();

            while (cleaned.IndexOf("  ", StringComparison.Ordinal) >= 0)
                cleaned = cleaned.Replace("  ", " ");

            // Models asked for one short line sometimes return a paragraph. Cutting mid-word reads
            // as broken, so prefer the last sentence boundary, then the last whole word.
            if (cleaned.Length > 240)
            {
                var window = cleaned.Substring(0, 240);
                var cut = window.LastIndexOfAny(new[] { '.', '!', '?' });
                if (cut < 80) cut = window.LastIndexOf(' ');
                cleaned = (cut > 0 ? window.Substring(0, cut) : window).TrimEnd(' ', ',', ';', ':') + "...";
            }

            return cleaned;
        }

        // ------------------------------------------------------------------ models

        /// <summary>
        /// Which model to ask for. An explicit setting wins; otherwise the first model the server
        /// reports is used, so a fresh Ollama install needs no configuration at all.
        /// </summary>
        private static string EnsureModel(Settings settings, bool ollama, out string error)
        {
            error = null;
            if (!string.IsNullOrWhiteSpace(settings.AiModel)) return settings.AiModel.Trim();

            if (settings.ResolvedModel != null) return settings.ResolvedModel;

            var models = Models(settings, out error);
            if (models.Count == 0)
            {
                if (error == null) error = "no model is configured and the server reported none";
                return null;
            }

            settings.ResolvedModel = models[0];
            Log.Line("model: none configured, using '" + models[0] + "' (server offered " + models.Count + ")");
            return settings.ResolvedModel;
        }

        public static List<string> Models(Settings settings, out string error)
        {
            error = null;
            var names = new List<string>();

            try
            {
                var endpoint = Endpoint(settings);
                var baseUrl = BaseOf(endpoint);
                var listUrl = IsOllamaNative(endpoint) ? baseUrl + "/api/tags" : baseUrl + "/v1/models";

                var request = (HttpWebRequest)WebRequest.Create(listUrl);
                request.Method = "GET";
                request.Timeout = Math.Min(settings.AiTimeoutMs, 3000);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var raw = reader.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    var root = serializer.Deserialize<Dictionary<string, object>>(raw);
                    if (root == null) return names;

                    // Ollama: { "models": [ { "name": "llama3.2:latest" } ] }
                    if (root.ContainsKey("models"))
                    {
                        foreach (var entry in AsItems(root["models"]))
                        {
                            var item = entry as Dictionary<string, object>;
                            if (item == null) continue;
                            if (item.ContainsKey("name") && item["name"] != null) names.Add(item["name"].ToString());
                            else if (item.ContainsKey("model") && item["model"] != null) names.Add(item["model"].ToString());
                        }
                    }

                    // OpenAI-compatible: { "data": [ { "id": "..." } ] }
                    if (root.ContainsKey("data"))
                    {
                        foreach (var entry in AsItems(root["data"]))
                        {
                            var item = entry as Dictionary<string, object>;
                            if (item == null) continue;
                            if (item.ContainsKey("id") && item["id"] != null) names.Add(item["id"].ToString());
                        }
                    }
                }
            }
            catch (Exception ex) { error = ex.Message; }

            return names;
        }

        private const int WarmUpTimeoutMs = 240000;

        /// <summary>
        /// Load the model before anyone needs it.
        ///
        /// Ollama loads a model on first use, and that can take far longer than one reply is allowed
        /// to take - so without this the first thing the player says gets an instant scripted answer
        /// because the real one was still loading. Doing it at startup means the model is warm by the
        /// time anyone speaks.
        /// </summary>
        public static void WarmUp(Settings settings)
        {
            if (settings == null || !settings.UseModel) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var started = Environment.TickCount;
                    string error;
                    Chat(settings, "Reply with one word.", "ready?", WarmUpTimeoutMs, out error);

                    var seconds = (Environment.TickCount - started) / 1000.0;
                    if (error != null) Log.Line("model warm-up failed after " + seconds.ToString("0.0") + "s: " + error);
                    else Log.Line("model warm-up done in " + seconds.ToString("0.0") + "s");
                }
                catch (Exception ex) { Log.Error("warm-up", ex); }
            });
        }

        /// <summary>Is a server actually there? Used by AiMode=auto.</summary>
        public static bool Available(Settings settings)
        {
            string error;
            return Models(settings, out error).Count > 0;
        }

        /// <summary>
        /// Read a JSON array whatever shape the serializer chose for it.
        ///
        /// This exists because of a real bug: JavaScriptSerializer hands back an ArrayList rather
        /// than an object[] for arrays nested inside a Dictionary, so casting with "as object[]"
        /// returned null and every reply and every model list came back silently empty.
        /// </summary>
        private static List<object> AsItems(object value)
        {
            var items = new List<object>();
            if (value == null || value is string) return items;

            var enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null) return items;

            foreach (var item in enumerable) items.Add(item);
            return items;
        }

        /// <summary>"http://host:port/api/chat" -> "http://host:port"</summary>
        private static string BaseOf(string endpoint)
        {
            var scheme = endpoint.IndexOf("://", StringComparison.Ordinal);
            if (scheme < 0) return endpoint;

            var slash = endpoint.IndexOf('/', scheme + 3);
            return slash < 0 ? endpoint : endpoint.Substring(0, slash);
        }

        private static string Escape(string value)
        {
            if (value == null) return "";

            var builder = new StringBuilder(value.Length + 16);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            return builder.ToString();
        }
    }
}
