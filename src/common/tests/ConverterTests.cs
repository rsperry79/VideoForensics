using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Ring.Api.Converters;

namespace Ring.Api.Tests;

public class FlexibleStringConverterTests
{
    private readonly JsonSerializerOptions _options;

    public FlexibleStringConverterTests()
    {
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _options.Converters.Add(new FlexibleStringConverter());
    }

    [Fact]
    public void WithStringValue_ReturnsString()
    {
        var json = @"{""value"": ""test""}";
        var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
        Assert.Equal("test", result.Value);
    }

    [Fact]
    public void WithNumberValue_ReturnsStringNumber()
    {
        var json = @"{""value"": 42}";
        var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void WithBooleanTrue_ReturnsStringTrue()
    {
        var json = @"{""value"": true}";
        var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
        Assert.Equal("true", result.Value);
    }

    [Fact]
    public void WithBooleanFalse_ReturnsStringFalse()
    {
        var json = @"{""value"": false}";
        var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
        Assert.Equal("false", result.Value);
    }

    [Fact]
    public void WithNullValue_ReturnsNull()
    {
        var json = @"{""value"": null}";
        var result = JsonSerializer.Deserialize<StringTestModel>(json, _options);
        Assert.Null(result.Value);
    }

    private class StringTestModel
    {
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string Value { get; set; }
    }
}

public class BooleanConverterTests
{
    private readonly JsonSerializerOptions _options;

    public BooleanConverterTests()
    {
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _options.Converters.Add(new BooleanConverter());
    }

    [Fact]
    public void WithTrue_ReturnsTrue()
    {
        var json = @"{""value"": true}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.True(result.Value);
    }

    [Fact]
    public void WithFalse_ReturnsFalse()
    {
        var json = @"{""value"": false}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.False(result.Value);
    }

    [Fact]
    public void WithStringTrue_ReturnsTrue()
    {
        var json = @"{""value"": ""true""}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.True(result.Value);
    }

    [Fact]
    public void WithStringFalse_ReturnsFalse()
    {
        var json = @"{""value"": ""false""}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.False(result.Value);
    }

    [Fact]
    public void WithStringOne_ReturnsTrue()
    {
        var json = @"{""value"": ""1""}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.True(result.Value);
    }

    [Fact]
    public void WithStringZero_ReturnsFalse()
    {
        var json = @"{""value"": ""0""}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.False(result.Value);
    }

    [Fact]
    public void WithNumberOne_ReturnsTrue()
    {
        var json = @"{""value"": 1}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.True(result.Value);
    }

    [Fact]
    public void WithNumberZero_ReturnsFalse()
    {
        var json = @"{""value"": 0}";
        var result = JsonSerializer.Deserialize<BooleanTestModel>(json, _options);
        Assert.False(result.Value);
    }

    private class BooleanTestModel
    {
        [JsonConverter(typeof(BooleanConverter))]
        public bool Value { get; set; }
    }
}
