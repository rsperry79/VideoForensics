using VideoForensics.WebApp.Chat;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Opt-in live test against the real Anthropic API; skipped unless ANTHROPIC_API_KEY is set.
    /// Never logs the key or request headers.
    /// </summary>
    public class LiveAnthropicChatProviderTests
    {
        /// <summary>Returns the ANTHROPIC_API_KEY value, or null when missing/blank.</summary>
        internal static string? ReadApiKey(Func<string, string?> getEnv)
        {
            string? value = getEnv("ANTHROPIC_API_KEY");
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        [Fact]
        public void ReadApiKey_NullValue_ReturnsNull()
        {
            Assert.Null(ReadApiKey(_ => null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ReadApiKey_BlankValue_ReturnsNull(string value)
        {
            Assert.Null(ReadApiKey(_ => value));
        }

        [Fact]
        public void ReadApiKey_SetValue_ReturnsValueFromCorrectVariable()
        {
            string? requested = null;

            string? key = ReadApiKey(name => { requested = name; return "test-value"; });

            Assert.Equal("test-value", key);
            Assert.Equal("ANTHROPIC_API_KEY", requested);
        }

        // Latest Haiku, so the live check exercises the current cheap model. Override with ANTHROPIC_CHAT_MODEL.
        private const string DefaultModel = "claude-haiku-5-5";

        [Fact]
        public async Task StreamCompleteAsync_LiveAnthropic_StreamsDeltasAndCompletes()
        {
            string? key = ReadApiKey(Environment.GetEnvironmentVariable);
            Assert.SkipWhen(key is null, "ANTHROPIC_API_KEY is not set");

            string? modelOverride = Environment.GetEnvironmentVariable("ANTHROPIC_CHAT_MODEL");
            string model = string.IsNullOrWhiteSpace(modelOverride) ? DefaultModel : modelOverride;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var provider = new AnthropicChatProvider(new AnthropicChatOptions { ApiKey = key!, Model = model }, http);

            var events = new List<LlmStreamEvent>();
            await foreach (var e in provider.StreamCompleteAsync(
                [new LlmMessage("user", "Reply with exactly the word: pong")], [], TestContext.Current.CancellationToken))
            {
                events.Add(e);
            }

            Assert.Contains(events, e => e is LlmTextDelta);
            var completed = Assert.IsType<LlmStepCompleted>(events[^1]);
            Assert.False(string.IsNullOrWhiteSpace(completed.Result.TextReply));
        }
    }
}
