using System.Text.Json;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>The value verifier's comparison rules, one value at a time (no database).</summary>
    public class FieldMapValueComparerTests
    {
        /// <summary>The verifier's comparison rules, one value at a time.</summary>
        [Theory]
        [InlineData("true", true, true, false)]
        [InlineData("false", true, false, false)]
        [InlineData("true", 1L, true, true)]
        [InlineData("12", 12L, true, false)]
        [InlineData("12", 13L, false, false)]
        [InlineData("1.5", 1.5d, true, false)]
        [InlineData("1.5", 1L, false, false)]
        [InlineData("\"abc\"", "abc", true, false)]
        [InlineData("\"abc\"", "abc ", false, false)]
        [InlineData("12", "12", true, true)]
        public void ValueComparer_Equal_FollowsTheDocumentedRules(string json, object stored, bool expectedEqual, bool expectedConversion)
        {
            using var doc = JsonDocument.Parse(json);
            bool equal = ValueComparer.Equal(Expected.FromJson(doc.RootElement), new DbCell(stored), out bool conversion);

            Assert.Equal(expectedEqual, equal);
            if (equal)
            {
                Assert.Equal(expectedConversion, conversion);
            }
        }

        [Fact]
        public void ValueComparer_IsoDateString_MatchesUtcDateTime()
        {
            using var doc = JsonDocument.Parse("\"2026-10-16T19:27:46.238Z\"");

            Assert.True(ValueComparer.Equal(Expected.FromJson(doc.RootElement), new DbCell(new DateTime(2026, 10, 16, 19, 27, 46, 238, DateTimeKind.Utc)), out _));
            Assert.False(ValueComparer.Equal(Expected.FromJson(doc.RootElement), new DbCell(new DateTime(2026, 10, 16, 19, 27, 46, 239, DateTimeKind.Utc)), out _));
        }

        [Fact]
        public void ValueComparer_Describe_HidesPersonalLikeText()
        {
            Assert.Equal("text", ValueComparer.Describe(new DbCell("someone@example.com")));
            Assert.Equal("text", ValueComparer.Describe(new DbCell("+15555550100")));
            Assert.Equal("text 'devices-12'", ValueComparer.Describe(new DbCell("devices-12")));
        }
    }
}
