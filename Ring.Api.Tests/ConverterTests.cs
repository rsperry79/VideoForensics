#nullable disable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using KoenZomers.Ring.Api.Converters;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ring.Api.Tests
{
    [TestClass]
    public class FlexibleStringConverterTests
    {
        private JsonSerializerOptions _options;

        [TestInitialize]
        public void Setup()
        {
            _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _options.Converters.Add(new FlexibleStringConverter());
        }

        [TestMethod]
        public void WithStringValue_ReturnsString()
        {
            var json = @"{""value"": ""test""}";
            var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
            Assert.AreEqual("test", result.Value);
        }

        [TestMethod]
        public void WithNumberValue_ReturnsStringNumber()
        {
            var json = @"{""value"": 42}";
            var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
            Assert.AreEqual("42", result.Value);
        }

        [TestMethod]
        public void WithBooleanTrue_ReturnsStringTrue()
        {
            var json = @"{""value"": true}";
            var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
            Assert.AreEqual("true", result.Value);
        }

        [TestMethod]
        public void WithBooleanFalse_ReturnsStringFalse()
        {
            var json = @"{""value"": false}";
            var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
            Assert.AreEqual("false", result.Value);
        }

        [TestMethod]
        public void WithNullValue_ReturnsNull()
        {
            var json = @"{""value"": null}";
            var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
            Assert.IsNull(result.Value);
        }

        private class StringTestModel
        {
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string Value { get; set; }
        }
    }

    [TestClass]
    public class BooleanConverterTests
    {
        private JsonSerializerOptions _options;

        [TestInitialize]
        public void Setup()
        {
            _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _options.Converters.Add(new BooleanConverter());
        }

        [TestMethod]
        public void WithTrue_ReturnsTrue()
        {
            var json = @"{""value"": true}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsTrue(result.Value);
        }

        [TestMethod]
        public void WithFalse_ReturnsFalse()
        {
            var json = @"{""value"": false}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsFalse(result.Value);
        }

        [TestMethod]
        public void WithStringTrue_ReturnsTrue()
        {
            var json = @"{""value"": ""true""}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsTrue(result.Value);
        }

        [TestMethod]
        public void WithStringFalse_ReturnsFalse()
        {
            var json = @"{""value"": ""false""}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsFalse(result.Value);
        }

        [TestMethod]
        public void WithStringOne_ReturnsTrue()
        {
            var json = @"{""value"": ""1""}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsTrue(result.Value);
        }

        [TestMethod]
        public void WithStringZero_ReturnsFalse()
        {
            var json = @"{""value"": ""0""}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsFalse(result.Value);
        }

        [TestMethod]
        public void WithNumberOne_ReturnsTrue()
        {
            var json = @"{""value"": 1}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsTrue(result.Value);
        }

        [TestMethod]
        public void WithNumberZero_ReturnsFalse()
        {
            var json = @"{""value"": 0}";
            var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
            Assert.IsFalse(result.Value);
        }

        private class BooleanTestModel
        {
            [JsonConverter(typeof(BooleanConverter))]
            public bool Value { get; set; }
        }
    }
}
#nullable restore
