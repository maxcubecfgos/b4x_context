using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class LocalCompactorTests
    {
        [Fact]
        public void Summary_prompt_embeds_source_and_anchored_template()
        {
            var prompt = LocalCompactor.BuildSummaryPrompt("src/main.bas", "'Header\nSub Go\nEnd Sub\n");

            Assert.Contains("<source file=\"src/main.bas\">", prompt);
            Assert.Contains("Sub Go", prompt);
            Assert.Contains("<template>", prompt);
            Assert.Contains("## Purpose", prompt);
            Assert.Contains("## Key Symbols", prompt);
            Assert.Contains("## Contracts", prompt);
            Assert.Contains("## Notes", prompt);
            Assert.Contains("</template>", prompt);
        }

        [Fact]
        public void Summary_prompt_keeps_opencode_style_rules()
        {
            var prompt = LocalCompactor.BuildSummaryPrompt("a.bas", "Sub A\nEnd Sub");

            Assert.Contains("Keep every section, even when empty.", prompt);
            Assert.Contains("terse bullets", prompt);
            Assert.Contains("verbatim", prompt);
            Assert.Contains("Do not mention that this is a summary", prompt);
        }

        [Fact]
        public void Truncate_caps_input_and_marks_omission()
        {
            var big = new string('x', LocalCompactor.MaxInputChars * 3);
            var result = LocalCompactor.Truncate(big);

            Assert.Contains("chars truncated", result);
            Assert.True(result.Length < LocalCompactor.MaxInputChars + 120);
            Assert.StartsWith("xxxx", result);
            Assert.EndsWith("xxx", result);
        }

        [Fact]
        public void Truncate_keeps_short_text_untouched()
        {
            const string text = "Sub A\nEnd Sub";
            Assert.Equal(text, LocalCompactor.Truncate(text));
            Assert.Equal("", LocalCompactor.Truncate(""));
        }

        [Fact]
        public void Update_prompt_carries_prior_summary_forward()
        {
            var prompt = LocalCompactor.BuildUpdatePrompt("a.bas", "## Purpose\n- old", "new source");

            Assert.Contains("<prior-summary>", prompt);
            Assert.Contains("## Purpose\n- old", prompt);
            Assert.Contains("anything you do not carry into the new summary is lost", prompt);
            Assert.Contains("the <source> wins", prompt);
            Assert.Contains("new source", prompt);
        }

        [Theory]
        [InlineData("http://localhost:11434", "http://localhost:11434/v1/chat/completions")]
        [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
        [InlineData("http://localhost:11434/v1/", "http://localhost:11434/v1/chat/completions")]
        [InlineData("http://localhost:1234/v1/chat/completions", "http://localhost:1234/v1/chat/completions")]
        [InlineData(null, "http://localhost:11434/v1/chat/completions")]
        [InlineData("  ", "http://localhost:11434/v1/chat/completions")]
        public void NormalizeChatUrl_accepts_base_urls_and_full_urls(string? input, string expected)
        {
            Assert.Equal(expected, LocalCompactor.NormalizeChatUrl(input));
        }

        [Theory]
        [InlineData("http://localhost:11434", "http://localhost:11434/v1")]
        [InlineData("http://localhost:1234/v1/", "http://localhost:1234/v1")]
        [InlineData("http://localhost:1234/v1/chat/completions", "http://localhost:1234/v1")]
        public void BaseUrl_strips_the_chat_path(string? input, string expected)
        {
            Assert.Equal(expected, LocalCompactor.BaseUrl(input));
        }

        [Fact]
        public void IsOffline_recognizes_connection_failures()
        {
            Assert.True(LocalCompactor.IsOffline(new System.Net.Http.HttpRequestException("refused")));
            Assert.True(LocalCompactor.IsOffline(new System.Threading.Tasks.TaskCanceledException()));
            Assert.False(LocalCompactor.IsOffline(new InvalidOperationException("bad model name")));
        }

        [Fact]
        public void Default_model_is_the_requested_qwen35()
        {
            Assert.Equal("qwen3.5:latest", LocalCompactor.DefaultModel);
        }

        [Fact]
        public void ParseApiError_reads_ollama_error_message()
        {
            var json = "{\"error\":{\"message\":\"model 'qwen2.5-coder:7b' not found\",\"type\":\"not_found_error\"}}";

            Assert.Equal("model 'qwen2.5-coder:7b' not found", LocalCompactor.ParseApiError(json));
        }

        [Fact]
        public void ParseApiError_returns_null_when_there_is_no_error()
        {
            Assert.Null(LocalCompactor.ParseApiError("{\"choices\":[]}"));
            Assert.Null(LocalCompactor.ParseApiError("plain text"));
            Assert.Null(LocalCompactor.ParseApiError(null));
        }

        [Fact]
        public void PickModel_keeps_the_configured_one_when_the_server_has_it()
        {
            var models = new[] { "qwen2.5-coder:3b", "qwen3.5:latest" };

            Assert.Equal("qwen3.5:latest", LocalCompactor.PickModel("qwen3.5:latest", models));
        }

        [Fact]
        public void PickModel_falls_back_to_a_local_coding_model_and_skips_cloud()
        {
            var models = new[] { "kimi-k2.6:cloud", "glm-5.1:cloud", "qwen3.5:cloud", "qwen2.5-coder:3b" };

            // configured model is missing → prefer a local coding model, never a :cloud one
            Assert.Equal("qwen2.5-coder:3b", LocalCompactor.PickModel("qwen2.5-coder:7b", models));
        }

        [Fact]
        public void PickModel_prefers_qwen35_when_available()
        {
            var models = new[] { "qwen2.5-coder:3b", "qwen3.5:latest", "minimax-m2.7:cloud" };

            Assert.Equal("qwen3.5:latest", LocalCompactor.PickModel("missing:latest", models));
        }

        [Fact]
        public void PickModel_returns_default_when_server_lists_nothing()
        {
            Assert.Equal(LocalCompactor.DefaultModel, LocalCompactor.PickModel(null, System.Array.Empty<string>()));
            Assert.Equal("custom:tag", LocalCompactor.PickModel("custom:tag", System.Array.Empty<string>()));
        }

        [Fact]
        public async Task Probe_reports_offline_when_no_server_listens()
        {
            // Port 1 is never served: the probe must fail fast with a human-readable reason.
            var reason = await LocalCompactor.ProbeAsync("http://localhost:1/v1");

            Assert.NotNull(reason);
            Assert.Contains("http://localhost:1/v1/models", reason!);
        }
    }
}
