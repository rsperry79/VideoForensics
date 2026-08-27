using System;
using Xunit;
using VideoForensics.Providers.Common.Helpers.Contracts;
using VideoForensics.Providers.Common.Helpers.Json;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Tests.Json
{
    public class JsonSerializerTests
    {
        private readonly IJsonSerializer _serializer = new JsonSerializer();

        private record TestObject(string Name, int Value, string? Optional);

        [Fact]
        public void Serialize_WithDefaultMode_ProducesCompactJson()
        {
            var obj = new TestObject("test", 42, null);
            var result = _serializer.Serialize(obj);

            Assert.NotNull(result);
            Assert.DoesNotContain("\n", result);
            Assert.Contains("\"Name\":\"test\"", result);
            Assert.Contains("\"Value\":42", result);
        }

        [Fact]
        public void Serialize_WithPrettyMode_ProducesFormattedJson()
        {
            var obj = new TestObject("test", 42, null);
            var result = _serializer.Serialize(obj, JsonSerializationMode.Pretty);

            Assert.NotNull(result);
            Assert.Contains("\n", result);
            Assert.Contains("\"Name\": \"test\"", result);
        }

        [Fact]
        public void Serialize_OmitsNullProperties()
        {
            var obj = new TestObject("test", 42, null);
            var result = _serializer.Serialize(obj);

            Assert.DoesNotContain("\"Optional\"", result);
        }

        [Fact]
        public void Deserialize_WithValidJson_ReturnsObject()
        {
            var json = """{"Name":"test","Value":42}""";
            var result = _serializer.Deserialize<TestObject>(json);

            Assert.NotNull(result);
            Assert.Equal("test", result.Name);
            Assert.Equal(42, result.Value);
            Assert.Null(result.Optional);
        }

        [Fact]
        public void Deserialize_WithInvalidJson_ReturnsNull()
        {
            var json = "{ invalid json";
            var result = _serializer.Deserialize<TestObject>(json);

            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_WithUtf8Bytes_ReturnsObject()
        {
            var json = """{"Name":"test","Value":42}""";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var result = _serializer.Deserialize<TestObject>(bytes);

            Assert.NotNull(result);
            Assert.Equal("test", result.Name);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Serialize_Deserialize_RoundTrip()
        {
            var original = new TestObject("roundtrip", 99, "optional_value");
            var json = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestObject>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Value, deserialized.Value);
            Assert.Equal(original.Optional, deserialized.Optional);
        }
    }
}
