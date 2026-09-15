using VideoForensics.Client.Core.Utilities;
using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class DateTimeUtilitiesTests
    {
        [Fact]
        public void TryParseDate_ValidDate_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("2024-03-15");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidShortDate_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("3-15-24");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidSlashFormat_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("03/15/2024");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidSlashFormatTwoDigitYear_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("3/15/24");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_ValidSlashFormatFourDigitYear_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("2024/03/15");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 15), result.Value);
        }

        [Fact]
        public void TryParseDate_SingleDigitMonth_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("3-5-24");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 3, 5), result.Value);
        }

        [Fact]
        public void TryParseDate_InvalidDate_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("not-a-date");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_InvalidMonth_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("2024-13-01");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_InvalidDay_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("2024-02-30");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_EmptyString_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate(string.Empty);

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_NullString_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate(null);

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_WhitespaceString_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("   ");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_LeadingWhitespace_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("  2024-03-15");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_TrailingWhitespace_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("2024-03-15  ");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_LeapYearDate_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("2024-02-29");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 2, 29), result.Value);
        }

        [Fact]
        public void TryParseDate_NonLeapYearFeb29_ReturnsNull()
        {
            var result = DateTimeUtilities.TryParseDate("2023-02-29");

            Assert.Null(result);
        }

        [Fact]
        public void TryParseDate_MinDateTime_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("0001-01-01");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(1, 1, 1), result.Value);
        }

        [Fact]
        public void TryParseDate_YearWithLeadingZeros_ReturnsDateTime()
        {
            var result = DateTimeUtilities.TryParseDate("2024-01-01");

            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 1, 1), result.Value);
        }

        [Fact]
        public void ParseDateOrDefault_ValidDate_ReturnsDateTime()
        {
            var fallback = new DateTime(2000, 1, 1);
            var result = DateTimeUtilities.ParseDateOrDefault("2024-03-15", fallback);

            Assert.Equal(new DateTime(2024, 3, 15), result);
        }

        [Fact]
        public void ParseDateOrDefault_InvalidDate_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            var result = DateTimeUtilities.ParseDateOrDefault("not-a-date", fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_NullString_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            var result = DateTimeUtilities.ParseDateOrDefault(null, fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_EmptyString_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            var result = DateTimeUtilities.ParseDateOrDefault(string.Empty, fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_WhitespaceString_ReturnsFallback()
        {
            var fallback = new DateTime(2000, 1, 1);
            var result = DateTimeUtilities.ParseDateOrDefault("   ", fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_RespectsFallbackValue()
        {
            var fallback = new DateTime(1999, 12, 31);
            var result = DateTimeUtilities.ParseDateOrDefault("invalid", fallback);

            Assert.Equal(fallback, result);
        }

        [Fact]
        public void ParseDateOrDefault_MultipleInvalidAttempts_AllReturnSameFallback()
        {
            var fallback = new DateTime(2000, 1, 1);

            var result1 = DateTimeUtilities.ParseDateOrDefault("bad", fallback);
            var result2 = DateTimeUtilities.ParseDateOrDefault("", fallback);
            var result3 = DateTimeUtilities.ParseDateOrDefault(null, fallback);

            Assert.Equal(fallback, result1);
            Assert.Equal(fallback, result2);
            Assert.Equal(fallback, result3);
        }

        [Fact]
        public void FormatDate_ValidDateTime_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 3, 15);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-03-15", result);
        }

        [Fact]
        public void FormatDate_SingleDigitMonth_PadsWithZero()
        {
            var date = new DateTime(2024, 3, 15);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-03-15", result);
            Assert.Contains("-03-", result);
        }

        [Fact]
        public void FormatDate_SingleDigitDay_PadsWithZero()
        {
            var date = new DateTime(2024, 3, 5);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-03-05", result);
            Assert.EndsWith("-05", result);
        }

        [Fact]
        public void FormatDate_MinDateTime_ReturnsFormattedString()
        {
            var date = new DateTime(1, 1, 1);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("0001-01-01", result);
        }

        [Fact]
        public void FormatDate_MaxDateTime_ReturnsFormattedString()
        {
            var date = new DateTime(9999, 12, 31);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("9999-12-31", result);
        }

        [Fact]
        public void FormatDate_LeapYearDate_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 2, 29);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-02-29", result);
        }

        [Fact]
        public void FormatDate_January1_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 1, 1);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-01-01", result);
        }

        [Fact]
        public void FormatDate_December31_ReturnsFormattedString()
        {
            var date = new DateTime(2024, 12, 31);

            var result = DateTimeUtilities.FormatDate(date);

            Assert.Equal("2024-12-31", result);
        }

        [Fact]
        public void FormatDate_ConsistentFormat()
        {
            var date1 = new DateTime(2024, 3, 15);
            var date2 = new DateTime(2024, 3, 15);

            var result1 = DateTimeUtilities.FormatDate(date1);
            var result2 = DateTimeUtilities.FormatDate(date2);

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void RoundTripConversion_ParseAndFormat()
        {
            var originalString = "2024-03-15";
            var parsed = DateTimeUtilities.TryParseDate(originalString);
            var formatted = DateTimeUtilities.FormatDate(parsed!.Value);

            Assert.Equal(originalString, formatted);
        }

        [Fact]
        public void MultipleFormats_AllParsedCorrectly()
        {
            var formats = new[] { "2024-03-15", "3-15-24", "3/15/24", "03/15/2024", "2024/03/15" };
            var expectedDate = new DateTime(2024, 3, 15);

            foreach (var format in formats)
            {
                var result = DateTimeUtilities.TryParseDate(format);
                Assert.NotNull(result);
                Assert.Equal(expectedDate, result.Value);
            }
        }

        [Fact]
        public void ParseDateOrDefault_ValidAndInvalidDates_ConsistentBehavior()
        {
            var fallback = new DateTime(2000, 1, 1);

            var validResult = DateTimeUtilities.ParseDateOrDefault("2024-03-15", fallback);
            var invalidResult = DateTimeUtilities.ParseDateOrDefault("not-a-date", fallback);

            Assert.NotEqual(validResult, invalidResult);
            Assert.Equal(fallback, invalidResult);
        }

        [Fact]
        public void FormatDate_CanRoundTripWithTryParseDate()
        {
            var originalDate = new DateTime(2024, 3, 15, 10, 30, 45);
            var formatted = DateTimeUtilities.FormatDate(originalDate);
            var reparsed = DateTimeUtilities.TryParseDate(formatted);

            Assert.NotNull(reparsed);
            // Note: Time component is lost in the format/parse round-trip
            Assert.Equal(originalDate.Year, reparsed.Value.Year);
            Assert.Equal(originalDate.Month, reparsed.Value.Month);
            Assert.Equal(originalDate.Day, reparsed.Value.Day);
        }
    }
}
