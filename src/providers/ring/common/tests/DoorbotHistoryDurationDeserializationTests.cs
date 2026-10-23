using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Common.Tests;

/// <summary>
/// Ring sends a history event's duration in seconds as a decimal (for example 40.0) on some events and as an integer on
/// others. These tests pin that DoorbotHistoryEvent.Duration accepts both, so one event never fails a whole history page.
/// </summary>
public class DoorbotHistoryEvent_DurationDeserialization_Tests
{
    [Fact]
    public void DoorbotHistoryEvent_DurationDecimal_ReadsAsRoundedInt()
    {
        DoorbotHistoryEvent? evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>("{\"id\":1,\"duration\":40.0}");

        Assert.Equal(40, evt!.Duration);
    }

    [Fact]
    public void DoorbotHistoryEvent_DurationInteger_StaysInteger()
    {
        DoorbotHistoryEvent? evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>("{\"id\":1,\"duration\":95}");

        Assert.Equal(95, evt!.Duration);
    }

    [Fact]
    public void DoorbotHistoryEvent_DurationNull_StaysNull()
    {
        DoorbotHistoryEvent? evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>("{\"id\":1,\"duration\":null}");

        Assert.Null(evt!.Duration);
    }

    [Fact]
    public void DoorbotHistoryEvent_DurationFractional_RoundsHalfAwayFromZero()
    {
        DoorbotHistoryEvent? evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>("{\"id\":1,\"duration\":40.5}");

        Assert.Equal(41, evt!.Duration);
    }

    [Fact]
    public void DoorbotHistoryEvent_DurationDecimalString_ReadsAsRoundedInt()
    {
        DoorbotHistoryEvent? evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>("{\"id\":1,\"duration\":\"120.0\"}");

        Assert.Equal(120, evt!.Duration);
    }

    [Fact]
    public void DoorbotHistoryEvent_DurationNonNumericString_ReadsAsNull()
    {
        DoorbotHistoryEvent? evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>("{\"id\":1,\"duration\":\"n/a\"}");

        Assert.Null(evt!.Duration);
    }
}

/// <summary>Direct checks on DecimalToIntConverter, for values the entity tests do not reach.</summary>
public class DecimalToIntConverter_Read_Tests
{
    private sealed class Holder
    {
        [JsonPropertyName("v")]
        public int? V { get; set; }
    }

    private static int? Read(string json)
    {
        return JsonSerializer.Deserialize<Holder>(
            $"{{\"v\":{json}}}", new JsonSerializerOptions
            {
                Converters = { new VideoForensics.Providers.Ring.Converters.DecimalToIntConverter() }
            })!.V;
    }

    [Fact]
    public void Read_NegativeHalf_RoundsAwayFromZero()
    {
        Assert.Equal(-3, Read("-2.5"));
    }

    [Fact]
    public void Read_OutOfRangeNumber_ReadsAsNull()
    {
        Assert.Null(Read("1e20"));
    }

    [Fact]
    public void Read_BooleanToken_ReadsAsNullAndSkips()
    {
        Assert.Null(Read("true"));
    }
}
