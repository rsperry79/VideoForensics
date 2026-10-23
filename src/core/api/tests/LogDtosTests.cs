using System.Text.Json;

using VideoForensics.Core.Logging.Services;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class LogDtosTests
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        private const string CompleteJson =
            "{\"sequence\":3,\"timestampUtc\":\"2026-01-02T03:04:05+00:00\",\"level\":\"Error\",\"category\":\"Cat\",\"message\":\"boom\",\"exception\":null}";

        [Theory]
        [InlineData("timestampUtc")]
        [InlineData("level")]
        [InlineData("category")]
        [InlineData("message")]
        public void LogEntryDto_MissingRequiredProperty_ThrowsJsonException(string property)
        {
            using var doc = JsonDocument.Parse(CompleteJson);
            var dict = doc.RootElement.EnumerateObject()
                .Where(p => p.Name != property)
                .ToDictionary(p => p.Name, p => p.Value.Clone());
            string json = JsonSerializer.Serialize(dict);

            _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LogEntryDto>(json, WebJson));
        }

        [Fact]
        public void LogEntryDto_CompleteJson_Deserializes()
        {
            LogEntryDto? dto = JsonSerializer.Deserialize<LogEntryDto>(CompleteJson, WebJson);

            Assert.NotNull(dto);
            Assert.Equal(3, dto!.Sequence);
            Assert.Equal("Error", dto.Level);
            Assert.Equal("Cat", dto.Category);
            Assert.Equal("boom", dto.Message);
            Assert.Null(dto.Exception);
        }

        [Fact]
        public void ToDto_NullLevelCategoryAndMessage_ProducesEmptyStrings()
        {
            var record = new LogRecord(1, DateTimeOffset.UtcNow, null!, null!, null!, null);

            LogEntryDto dto = record.ToDto();

            Assert.Equal(string.Empty, dto.Level);
            Assert.Equal(string.Empty, dto.Category);
            Assert.Equal(string.Empty, dto.Message);
        }

        [Fact]
        public void LogEntryDto_EmptyCategoryAndMessage_StillSerializesAllRequiredProperties()
        {
            var dto = new LogEntryDto(1, DateTimeOffset.UtcNow, string.Empty, string.Empty, string.Empty, null);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, WebJson));
            JsonElement root = doc.RootElement;

            Assert.True(root.TryGetProperty("timestampUtc", out _));
            Assert.Equal(string.Empty, root.GetProperty("level").GetString());
            Assert.Equal(string.Empty, root.GetProperty("category").GetString());
            Assert.Equal(string.Empty, root.GetProperty("message").GetString());
        }
    }
}
