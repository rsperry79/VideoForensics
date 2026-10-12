using System;
using System.Text.Json;

using VideoForensics.Providers.Ring.Converters;

namespace VideoForensics.Providers.Ring.Common.Tests;

/// <summary>
/// Tests for LenientNullableGuidConverter JSON converter.
/// </summary>
public class LenientNullableGuidConverter_JsonRoundTrip_Tests
{
    [Fact]
    public void Read_WithNullToken_ReturnsNull()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        string json = "null";
        Guid? result = JsonSerializer.Deserialize<Guid?>(json, options);

        Assert.Null(result);
    }

    [Fact]
    public void Read_WithEmptyString_ReturnsNull()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        string json = "\"\"";
        Guid? result = JsonSerializer.Deserialize<Guid?>(json, options);

        Assert.Null(result);
    }

    [Fact]
    public void Read_WithValidGuidString_ParsesGuid()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        var expectedGuid = Guid.NewGuid();
        string json = $"\"{expectedGuid:D}\"";
        Guid? result = JsonSerializer.Deserialize<Guid?>(json, options);

        Assert.Equal(expectedGuid, result);
    }

    [Fact]
    public void Read_WithInvalidGuidString_DerivesPseudoGuid()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        string invalidGuidString = "c22vpq-55qqu-0";
        string json = $"\"{invalidGuidString}\"";
        Guid? result = JsonSerializer.Deserialize<Guid?>(json, options);

        _ = Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public void Read_WithInvalidGuidString_ProducesDeterministicPseudoGuid()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        string invalidGuidString = "c22vpq-55qqu-0";
        string json = $"\"{invalidGuidString}\"";

        Guid? result1 = JsonSerializer.Deserialize<Guid?>(json, options);
        Guid? result2 = JsonSerializer.Deserialize<Guid?>(json, options);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Read_WithDifferentInvalidStrings_ProducesDifferentPseudoGuids()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        string invalidString1 = "c22vpq-55qqu-0";
        string invalidString2 = "c22vpq-55qqu-1";

        Guid? result1 = JsonSerializer.Deserialize<Guid?>($"\"{invalidString1}\"", options);
        Guid? result2 = JsonSerializer.Deserialize<Guid?>($"\"{invalidString2}\"", options);

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Write_WithGuidValue_WritesGuidString()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        var guid = Guid.NewGuid();
        string json = JsonSerializer.Serialize(guid, options);

        Assert.Contains(guid.ToString(), json);
    }

    [Fact]
    public void Write_WithNullValue_WritesNull()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        Guid? nullGuid = null;
        string json = JsonSerializer.Serialize(nullGuid, options);

        Assert.Equal("null", json);
    }

    [Fact]
    public void RoundTrip_ValidGuid_PreservesValue()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        var originalGuid = Guid.NewGuid();
        string json = JsonSerializer.Serialize((Guid?)originalGuid, options);
        Guid? deserializedGuid = JsonSerializer.Deserialize<Guid?>(json, options);

        Assert.Equal(originalGuid, deserializedGuid);
    }

    [Fact]
    public void RoundTrip_NullGuid_PreservesNull()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new LenientNullableGuidConverter() }
        };

        Guid? originalGuid = null;
        string json = JsonSerializer.Serialize(originalGuid, options);
        Guid? deserializedGuid = JsonSerializer.Deserialize<Guid?>(json, options);

        Assert.Null(deserializedGuid);
    }
}
