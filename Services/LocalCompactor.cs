using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace B4XContext.Services
{
    /// <summary>
    /// opencode-style anchored compaction against a local OpenAI-compatible endpoint
    /// (Ollama, LM Studio, llama.cpp server, vLLM...): sends an oversized file through a
    /// fixed Markdown template so a small-context model (e.g. Qwen 2.5 Coder 7B / 4K)
    /// gets a terse, trustworthy summary instead of raw code.
    /// Template/rules are adapted from opencode's SUMMARY_TEMPLATE (packages/core/src/session/compaction.ts).
    /// </summary>
    public static class LocalCompactor
    {
        public const string DefaultEndpoint = "http://localhost:11434/v1";
        public const string DefaultModel = "qwen3.5:latest";

        /// <summary>Preference order when the configured model is missing from the server (local models only).</summary>
        private static readonly string[] PreferredLocalPatterns = { "qwen3.5", "qwen3", "coder" };

        /// <summary>Max source chars sent to the local model (~3K tokens), so the call itself fits a 4K window.</summary>
        public const int MaxInputChars = 12000;

        /// <summary>
        /// Max tokens requested for the summary. Reasoning models (qwen3.5) spend a large part of
        /// the budget on hidden thinking, so this leaves them room to actually write the summary.
        /// </summary>
        public const int MaxOutputTokens = 2048;

        /// <summary>Short timeout used by the reachability probe.</summary>
        public const int ProbeTimeoutMs = 2500;

        /// <summary>
        /// Per-request cap for a summary call. Without it a slow/cold model blocks the UI flow
        /// for up to HttpClient's 300 s default, which reads as "never finishes".
        /// </summary>
        public const int RequestTimeoutSeconds = 120;

        public const string SummaryTemplate = @"Output exactly the Markdown structure shown inside <template> and keep the section order unchanged. Do not include the <template> tags in your response.
<template>
## Purpose
- [one or two short sentences: what this file does]

## Key Symbols
- [exact name/signature + one-line role for each public declaration; keep identifiers verbatim]

## Contracts
- [inputs, outputs, side effects, dependencies used, or ""(none)""]

## Notes
- [constraints, gotchas, exact strings worth preserving, or ""(none)""]
</template>

Rules:
- Keep every section, even when empty.
- Use terse bullets, not prose paragraphs.
- Preserve exact file paths, symbol names, commands, and identifiers: the reader only trusts what is verbatim.
- Cap the whole summary at 20 bullets.
- Do not mention that this is a summary or that the content was compacted.";

        public const string UpdateRules = @"The <prior-summary> describes the previous version of the <source>. Build a new summary that combines both. The <prior-summary> is discarded afterwards: anything you do not carry into the new summary is lost.
When combining:
- Carry forward purpose, contracts, key symbols, and constraints from the <prior-summary> even when the <source> does not restate them.
- The <source> is newer: where they conflict, the <source> wins.
- Add new or changed symbols from the <source>, and drop symbols that no longer exist.";

        /// <summary>Builds the compaction prompt for one file (truncated to <see cref="MaxInputChars"/>).</summary>
        public static string BuildSummaryPrompt(string path, string text)
        {
            var source = Truncate(text ?? "");
            return
                "Compress the source below into an anchored summary so another coding agent can continue the work with a tiny context window.\n\n" +
                $"<source file=\"{Escape(path)}\">\n{source}\n</source>\n\n" +
                SummaryTemplate;
        }

        /// <summary>Builds the incremental prompt when a summary already exists (opencode's merge instructions).</summary>
        public static string BuildUpdatePrompt(string path, string priorSummary, string text)
        {
            var source = Truncate(text ?? "");
            return
                $"<source file=\"{Escape(path)}\">\n{source}\n</source>\n\n" +
                $"Here is the summary of the previous version of this file:\n\n<prior-summary>\n{priorSummary}\n</prior-summary>\n\n" +
                UpdateRules + "\n\n" + SummaryTemplate;
        }

        /// <summary>
        /// Normalizes a user-entered base URL to a chat-completions URL:
        /// <c>http://localhost:11434</c> → <c>http://localhost:11434/v1/chat/completions</c>.
        /// </summary>
        public static string NormalizeChatUrl(string? endpoint)
        {
            var e = (endpoint ?? "").Trim().TrimEnd('/');
            if (e.Length == 0) e = DefaultEndpoint;
            if (e.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return e;
            if (!e.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) e += "/v1";
            return e + "/chat/completions";
        }

        /// <summary>Trims overlong source keeping head and tail (both ends carry the useful declarations).</summary>
        public static string Truncate(string text, int maxChars = MaxInputChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            int head = (int)(maxChars * 0.7);
            int tail = maxChars - head;
            int omitted = text.Length - head - tail;
            return text.Substring(0, head)
                + $"\n... [{omitted} chars truncated] ...\n"
                + text.Substring(text.Length - tail);
        }

        /// <summary>Base URL (without the chat path) of a configured endpoint: http://host:11434/v1</summary>
        public static string BaseUrl(string? endpoint)
        {
            var url = NormalizeChatUrl(endpoint);
            return url.Substring(0, url.Length - "/chat/completions".Length);
        }

        /// <summary>
        /// Reachability probe (opencode-style preflight): pings GET {base}/models before compacting.
        /// Returns null when the local server is up, or a human-readable reason when it is not
        /// (server closed, wrong port, wrong URL).
        /// </summary>
        public static async Task<string?> ProbeAsync(string? endpoint, CancellationToken ct = default)
        {
            var url = BaseUrl(endpoint) + "/models";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ProbeTimeoutMs);
                using var resp = await Client.SendAsync(req, cts.Token).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) return null;
                return $"The server answered {(int)resp.StatusCode} at {url} — check that it exposes an OpenAI-compatible /v1 API.";
            }
            catch (OperationCanceledException)
            {
                return $"No response from {url} (timeout after {ProbeTimeoutMs} ms) — the server is not running or the port is wrong.";
            }
            catch (HttpRequestException ex)
            {
                return $"Cannot reach {url} ({ex.Message}) — the local server appears to be closed.";
            }
        }

        /// <summary>True when the exception means "local server not running" rather than a model/HTTP error.</summary>
        public static bool IsOffline(Exception ex) => ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException;

        public static async Task<string> SummarizeAsync(string? endpoint, string model, string prompt, CancellationToken ct = default)
        {
            var mdl = string.IsNullOrWhiteSpace(model) ? DefaultModel : model!;
            var baseurl = BaseUrl(endpoint);
            var origin = baseurl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? baseurl.Substring(0, baseurl.Length - 3).TrimEnd('/')
                : baseurl.TrimEnd('/');

            // Ollama's native API can turn reasoning off (think:false): without it a reasoning
            // model like qwen3.5 burns the whole output budget on hidden thinking and returns
            // an empty answer. Non-Ollama servers keep the OpenAI-compatible path.
            bool native = await IsOllamaAsync(origin, ct).ConfigureAwait(false);
            var url = native ? origin + "/api/chat" : baseurl + "/chat/completions";
            var body = native
                ? JsonSerializer.Serialize(new
                {
                    model = mdl,
                    messages = new[] { new { role = "user", content = prompt } },
                    stream = false,
                    think = false,
                    options = new { num_predict = MaxOutputTokens }
                })
                : JsonSerializer.Serialize(new
                {
                    model = mdl,
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = 0.2,
                    max_tokens = MaxOutputTokens,
                    stream = false
                });

            string json;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                reqCts.CancelAfter(RequestTimeoutSeconds * 1000);
                using var resp = await Client.SendAsync(req, reqCts.Token).ConfigureAwait(false);
                json = await resp.Content.ReadAsStringAsync(reqCts.Token).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var apiError = ParseApiError(json);
                    throw new InvalidOperationException(apiError != null
                        ? $"The local endpoint rejected the request ({(int)resp.StatusCode}): {apiError}"
                        : $"Endpoint returned {(int)resp.StatusCode}: {Truncate(json, 400)}");
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"The model '{mdl}' did not answer within {RequestTimeoutSeconds}s. " +
                    "It may still be loading — try again, or pick a smaller/faster model in Settings.");
            }

            using var doc = JsonDocument.Parse(json);
            string? content = null;
            bool reasoned = false;

            if (native)
            {
                if (!doc.RootElement.TryGetProperty("message", out var nmsg))
                    throw new InvalidOperationException($"Unexpected response from {url}: no message.");
                content = nmsg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() : null;
                reasoned = nmsg.TryGetProperty("thinking", out var th) && th.ValueKind == JsonValueKind.String &&
                           !string.IsNullOrWhiteSpace(th.GetString());
            }
            else
            {
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    throw new InvalidOperationException($"Unexpected response from {url}: no choices. Check the model name and endpoint.");
                var m = choices[0].GetProperty("message");
                content = m.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() : null;
                reasoned = m.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String &&
                           !string.IsNullOrWhiteSpace(r.GetString());
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException(reasoned
                    ? $"The model '{mdl}' spent the whole output budget ({MaxOutputTokens} tokens) on reasoning " +
                      "and returned an empty answer. Try again, or pick a non-reasoning model in Settings."
                    : $"The local model '{mdl}' returned an empty summary.");
            }
            return content.Trim();
        }

        /// <summary>Detects an Ollama server (native /api/tags) so we can disable reasoning.</summary>
        private static async Task<bool> IsOllamaAsync(string origin, CancellationToken ct)
        {
            var json = await TryGetAsync(origin + "/api/tags", ct).ConfigureAwait(false);
            if (json == null) return false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Object &&
                       doc.RootElement.TryGetProperty("models", out _);
            }
            catch { return false; }
        }

        private static readonly HttpClient Client = CreateClient();

        /// <summary>
        /// Lists the models served by the local endpoint. Tries the OpenAI-style
        /// <c>GET {base}/models</c> first and falls back to Ollama's native <c>/api/tags</c>.
        /// Returns an empty list when the server cannot be reached.
        /// </summary>
        public static async Task<List<string>> ListModelsAsync(string? endpoint, CancellationToken ct = default)
        {
            var models = new List<string>();
            var baseurl = BaseUrl(endpoint);

            var json = await TryGetAsync(baseurl + "/models", ct).ConfigureAwait(false);
            if (json != null && TryParseOpenAiModels(json, models) && models.Count > 0)
                return models;

            var origin = baseurl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? baseurl.Substring(0, baseurl.Length - 3)
                : baseurl;
            json = await TryGetAsync(origin.TrimEnd('/') + "/api/tags", ct).ConfigureAwait(false);
            if (json != null) TryParseOllamaModels(json, models);
            return models;
        }

        /// <summary>Extracts <c>error.message</c> from an OpenAI/Ollama-style error body.</summary>
        public static string? ParseApiError(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    if (err.ValueKind == JsonValueKind.Object &&
                        err.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        return m.GetString();
                    if (err.ValueKind == JsonValueKind.String) return err.GetString();
                }
                if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    return msg.GetString();
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Picks the model to use: keeps the configured one when the server has it,
        /// otherwise falls back to the first local coding model (never a :cloud model).
        /// </summary>
        public static string PickModel(string? configured, IEnumerable<string> available)
        {
            var list = (available ?? Enumerable.Empty<string>())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            if (!string.IsNullOrWhiteSpace(configured) &&
                list.Any(m => m.Equals(configured, StringComparison.OrdinalIgnoreCase)))
                return configured!;
            if (list.Count == 0)
                return string.IsNullOrWhiteSpace(configured) ? DefaultModel : configured!;

            var locals = list.Where(m => !m.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var pattern in PreferredLocalPatterns)
            {
                var hit = locals.FirstOrDefault(m => m.Contains(pattern, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            return locals.FirstOrDefault() ?? list[0];
        }

        private static async Task<string?> TryGetAsync(string url, CancellationToken ct)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ProbeTimeoutMs);
                using var resp = await Client.SendAsync(req, cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch { return null; }
        }

        private static bool TryParseOpenAiModels(string json, List<string> into)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Array) return false;
                foreach (var item in data.EnumerateArray())
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                        into.Add(id.GetString()!);
                return true;
            }
            catch { return false; }
        }

        private static bool TryParseOllamaModels(string json, List<string> into)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("models", out var models) ||
                    models.ValueKind != JsonValueKind.Array) return false;
                foreach (var item in models.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) into.Add(n.GetString()!);
                    else if (item.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String) into.Add(m.GetString()!);
                }
                return true;
            }
            catch { return false; }
        }

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
            try { c.DefaultRequestHeaders.UserAgent.ParseAdd("B4XContext"); } catch { }
            return c;
        }

        private static string Escape(string? value) => (value ?? "").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
