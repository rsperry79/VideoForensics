using System;
using System.Text.Json;
using Xunit;
using VideoForensics.Providers.Common.Helpers.Json.Converters;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Tests.Json.Converters
{
    public class FlexibleConverterTests
    {
        private readonly JsonSerializerOptions _options = new()
        {
            Converters =
            {
                new FlexibleStringConverter(),
                new FlexibleBooleanConverter(),
                new FlexibleDecimalConverter(),
                new FlexibleDoubleConverter(),
                new FlexibleIntConverter()
            }
        };

        #region FlexibleStringConverter Tests

        [Fact]
        public void FlexibleStringConverter_WithStringValue_ReturnsString()
        {
            var json = "\"hello\"";
            var result = JsonSerializer.Deserialize<string>(json, _options);
            Assert.Equal("hello", result);
        }

        [Fact]
        public void FlexibleStringConverter_WithNumberValue_ConvertsToString()
        {
            var json = "42";
            var result = JsonSerializer.Deserialize<string>(json, _options);
            Assert.Equal("42", result);
        }

        [Fact]
        public void FlexibleStringConverter_WithBooleanTrue_ConvertsToString()
        {
            var json = "true";
            var result = JsonSerializer.Deserialize<string>(json, _options);
            Assert.Equal("true", result);
        }

        [Fact]
        public void FlexibleStringConverter_WithBooleanFalse_ConvertsToString()
        {
            var json = "false";
            var result = JsonSerializer.Deserialize<string>(json, _options);
            Assert.Equal("false", result);
        }

        [Fact]
        public void FlexibleStringConverter_WithNull_ReturnsNull()
        {
            var json = "null";
            var result = JsonSerializer.Deserialize<string?>(json, _options);
            Assert.Null(result);
        }

        #endregion

        #region FlexibleBooleanConverter Tests

        [Fact]
        public void FlexibleBooleanConverter_WithBoolean_ReturnsBool()
        {
            var json = "true";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.True(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithNumberOne_ReturnsTrue()
        {
            var json = "1";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.True(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithNumberZero_ReturnsFalse()
        {
            var json = "0";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.False(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithStringTrue_ReturnsTrue()
        {
            var json = "\"true\"";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.True(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithStringOne_ReturnsTrue()
        {
            var json = "\"1\"";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.True(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithStringFalse_ReturnsFalse()
        {
            var json = "\"false\"";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.False(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithStringZero_ReturnsFalse()
        {
            var json = "\"0\"";
            var result = JsonSerializer.Deserialize<bool>(json, _options);
            Assert.False(result);
        }

        [Fact]
        public void FlexibleBooleanConverter_WithInvalidString_ThrowsException()
        {
            var json = "\"invalid\"";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<bool>(json, _options));
        }

        #endregion

        #region FlexibleDecimalConverter Tests

        [Fact]
        public void FlexibleDecimalConverter_WithNumber_ReturnsDecimal()
        {
            var json = "4.5";
            var result = JsonSerializer.Deserialize<decimal?>(json, _options);
            Assert.Equal(4.5m, result);
        }

        [Fact]
        public void FlexibleDecimalConverter_WithString_ConvertsToDecimal()
        {
            var json = "\"3.14\"";
            var result = JsonSerializer.Deserialize<decimal?>(json, _options);
            Assert.Equal(3.14m, result);
        }

        [Fact]
        public void FlexibleDecimalConverter_WithInvalidString_ReturnsNull()
        {
            var json = "\"not-a-number\"";
            var result = JsonSerializer.Deserialize<decimal?>(json, _options);
            Assert.Null(result);
        }

        [Fact]
        public void FlexibleDecimalConverter_WithNull_ReturnsNull()
        {
            var json = "null";
            var result = JsonSerializer.Deserialize<decimal?>(json, _options);
            Assert.Null(result);
        }

        #endregion

        #region FlexibleDoubleConverter Tests

        [Fact]
        public void FlexibleDoubleConverter_WithNumber_ReturnsDouble()
        {
            var json = "-45.5";
            var result = JsonSerializer.Deserialize<double?>(json, _options);
            Assert.Equal(-45.5, result);
        }

        [Fact]
        public void FlexibleDoubleConverter_WithString_ConvertsToDouble()
        {
            var json = "\"-45.5\"";
            var result = JsonSerializer.Deserialize<double?>(json, _options);
            Assert.Equal(-45.5, result);
        }

        [Fact]
        public void FlexibleDoubleConverter_WithInvalidString_ReturnsNull()
        {
            var json = "\"not-a-number\"";
            var result = JsonSerializer.Deserialize<double?>(json, _options);
            Assert.Null(result);
        }

        [Fact]
        public void FlexibleDoubleConverter_WithNull_ReturnsNull()
        {
            var json = "null";
            var result = JsonSerializer.Deserialize<double?>(json, _options);
            Assert.Null(result);
        }

        #endregion

        #region FlexibleIntConverter Tests

        [Fact]
        public void FlexibleIntConverter_WithNumber_ReturnsInt()
        {
            var json = "42";
            var result = JsonSerializer.Deserialize<int?>(json, _options);
            Assert.Equal(42, result);
        }

        [Fact]
        public void FlexibleIntConverter_WithString_ConvertsToInt()
        {
            var json = "\"100\"";
            var result = JsonSerializer.Deserialize<int?>(json, _options);
            Assert.Equal(100, result);
        }

        [Fact]
        public void FlexibleIntConverter_WithInvalidString_ReturnsNull()
        {
            var json = "\"not-a-number\"";
            var result = JsonSerializer.Deserialize<int?>(json, _options);
            Assert.Null(result);
        }

        [Fact]
        public void FlexibleIntConverter_WithNull_ReturnsNull()
        {
            var json = "null";
            var result = JsonSerializer.Deserialize<int?>(json, _options);
            Assert.Null(result);
        }

        #endregion
    }
}
