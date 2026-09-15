using VideoForensics.Client.Core.Utilities;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class DateTimeUtilitiesTests
    {
        [Fact]
        public void TryParseDate_ValidDate_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024-03-15");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidShortDate_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("3-15-24");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidSlashFormat_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("03/15/2024");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidSlashFormatTwoDigitYear_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("3/15/24");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidSlashFormatFourDigitYear_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024/03/15");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_SingleDigitMonth_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("3-5-24");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 5), result.Value);
        }

        [Fact]
        public void TryParseDate_InvalidDate_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("not-a-date");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_InvalidMonth_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024-13-01");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_InvalidDay_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024-02-30");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_EmptyString_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate(string.Empty);

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_NullString_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate(null);

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_WhitespaceString_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("   ");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_LeadingWhitespace_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("  2024-03-15");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_TrailingWhitespace_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024-03-15  ");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_LeapYearDate_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024-02-29");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 2, 29), result.Value);
        }

        [Fact]
        public void TryParseDate_NonLeapYearFeb29_ReturnsNull()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2023-02-29");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_MinDateTime_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("0001-01-01");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(1, 1, 1), result.Value);
        }

        [Fact]
        public void TryParseDate_YearWithLeadingZeros_ReturnsDateTime()
        {
            DateTime? result = DateTimeUtilities.TryParseDate("2024-01-01");

            _ = Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 1, 1), result.Value);
        }

        [Fact]
        public void ParseDateOrDefault_ValidDate_ReturnsDateTime()
        {
            var fallback = new DateTime(2000, 1, 1);
            DateTime result = DateTimeUtilities.ParseDateOrDefault("2024-03-15", fallback);

            Assert.Equal(new DateTime(2024, 3, 15), result);
        }

        [Fact]
        public void ParseDateOrDefault_InvalidDate_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            DateTime result = DateTimeUtilities.ParseDateOrDefault("not-a-date", fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_NullString_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            DateTime result = DateTimeUtilities.ParseDateOrDefault(null, fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_EmptyString_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            DateTime result = DateTimeUtilities.ParseDateOrDefault(string.Empty, fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_WhitespaceString_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            DateTime result = DateTimeUtilities.ParseDateOrDefault("   ", fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_RespectsFallbackValue()
        {
            var fallback = new DateTime(1999, 12, 31);
            DateTime result = DateTimeUtilities.ParseDateOrDefault("invalid", fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_MultipleInvalidAttempts_AllReturnSameFallback()
        {
            var fallback = new DateTime(2000, 1, 1);

            DateTime result1 = DateTimeUtilities.ParseDateOrDefault("bad", fallback);
            DateTime result2 = DateTimeUtilities.ParseDateOrDefault("", fallback);
            DateTime result3 = DateTimeUtilities.ParseDateOrDefault(null, fallback);

            Assert.Equal(fallback, result1);
            Assert.Equal(fallback, result2);
            Assert.Equal(fallback, result3);
        }

        [Fact]
        public void FormatDate_ValidDateTime_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 3, 15);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-03-15", result);
        }

        [Fact]
        public void FormatDate_SingleDigitMonth_PadsWithZero()
        {
            var date = new DateTime(2024, 3, 15);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-03-15", result);
            Assert.Contains("-03-", result);
        }

        [Fact]
        public void FormatDate_SingleDigitDay_PadsWithZero()
        {
            var date = new DateTime(2024, 3, 5);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-03-05", result);
            Assert.EndsWith("-05", result);
        }

        [Fact]
        public void FormatDate_MinDateTime_ReturnsFormattedString()
        {
            var date = new DateTime(1, 1, 1);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("0001-01-01", result);
        }

        [Fact]
        public void FormatDate_MaxDateTime_ReturnsFormattedString()
        {
            var date = new DateTime(9999, 12, 31);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("9999-12-31", result);
        }

        [Fact]
        public void FormatDate_LeapYearDate_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 2, 29);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-02-29", result);
        }

        [Fact]
        public void FormatDate_January1_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 1, 1);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-01-01", result);
        }

        [Fact]
        public void FormatDate_December31_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 12, 31);

            string result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-12-31", result);
        }

        [Fact]
        public void FormatDate_ConsistentFormat()
        {
            var date1 = new DateTime(2024, 3, 15);
            var date2 = new DateTime(2024, 3, 15);

            string result1 = DateTimeUtilities.FormatDate(date1);
            string result2 = DateTimeUtilities.FormatDate(date2);

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void RoundTripConversion_ParseAndFormat()
        {
            string originalString = "2024-03-15";
            DateTime? parsed = DateTimeUtilities.TryParseDate(originalString);
            string formatted = DateTimeUtilities.FormatDate(parsed!.Value);

            Assert.Equal(originalString, formatted);
        }

        [Fact]
        public void MultipleFormats_AllParsedCorrectly()
        {
            string[] formats = new[] { "2024-03-15", "3-15-24", "3/15/24", "03/15/2024", "2024/03/15" };
            var expectedDate = new DateTime(2024, 3, 15);

            foreach (string? format in formats)
            {
                DateTime? result = DateTimeUtilities.TryParseDate(format);
                _ = Assert.NotNull(result);
                Assert.Equal(expectedDate, result.Value);
            }
        }

        [Fact]
        public void ParseDateOrDefault_ValidAndInvalidDates_ConsistentBehavior()
        {
            var fallback = new DateTime(2000, 1, 1);

            DateTime validResult = DateTimeUtilities.ParseDateOrDefault("2024-03-15", fallback);
            DateTime invalidResult = DateTimeUtilities.ParseDateOrDefault("not-a-date", fallback);

            Assert.NotEqual(validResult, invalidResult);
            Assert.Equal(fallback, invalidResult);
        }

        [Fact]
        public void FormatDate_CanRoundTripWithTryParseDate()
        {
            var originalDate = new DateTime(2024, 3, 15, 10, 30, 45);
            string formatted = DateTimeUtilities.FormatDate(originalDate);
            DateTime? reparsed = DateTimeUtilities.TryParseDate(formatted);

            _ = Assert.NotNull(reparsed);
            // Note: Time component is lost in the format/parse round-trip
            Assert.Equal(originalDate.Year, reparsed.Value.Year);
            Assert.Equal(originalDate.Month, reparsed.Value.Month);
            Assert.Equal(originalDate.Day, reparsed.Value.Day);
        }
    }
}
