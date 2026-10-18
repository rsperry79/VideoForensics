using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Common.Tests;

/// <summary>
/// Real Ring zone payloads send style as a number (1, 4) and state as "enabled"/"disabled" (or a number on
/// advanced zones). These tests pin how RingZone and RingZoneProperties accept those shapes.
/// </summary>
public class RingZoneProperties_StyleDeserialization_Tests
{
    [Fact]
    public void RingZoneProperties_StyleNumber_ReadsAsString()
    {
        RingZoneProperties? props = JsonSerializer.Deserialize<RingZoneProperties>("{\"style\":1}");

        Assert.Equal("1", props!.Style);
    }

    [Fact]
    public void RingZoneProperties_StyleString_ReadsAsString()
    {
        RingZoneProperties? props = JsonSerializer.Deserialize<RingZoneProperties>("{\"style\":\"solid\"}");

        Assert.Equal("solid", props!.Style);
    }

    [Fact]
    public void RingZoneProperties_StyleNull_StaysNull()
    {
        RingZoneProperties? props = JsonSerializer.Deserialize<RingZoneProperties>("{\"style\":null}");

        Assert.Null(props!.Style);
    }
}

public class RingZone_StateDeserialization_Tests
{
    [Fact]
    public void RingZone_StateEnabled_ReadsAsOne()
    {
        RingZone? zone = JsonSerializer.Deserialize<RingZone>("{\"id\":\"a\",\"state\":\"enabled\"}");

        Assert.Equal(1, zone!.State);
    }

    [Fact]
    public void RingZone_StateDisabled_ReadsAsZero()
    {
        RingZone? zone = JsonSerializer.Deserialize<RingZone>("{\"id\":\"a\",\"state\":\"disabled\"}");

        Assert.Equal(0, zone!.State);
    }

    [Fact]
    public void RingZone_StateNumeric_ReadsAsNumber()
    {
        RingZone? zone = JsonSerializer.Deserialize<RingZone>("{\"id\":\"a\",\"state\":2}");

        Assert.Equal(2, zone!.State);
    }

    [Fact]
    public void RingZone_StateNull_StaysNull()
    {
        RingZone? zone = JsonSerializer.Deserialize<RingZone>("{\"id\":\"a\",\"state\":null}");

        Assert.Null(zone!.State);
    }
}
