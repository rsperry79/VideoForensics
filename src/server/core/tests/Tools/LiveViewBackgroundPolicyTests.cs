using VideoForensics.Client.Core.Tools;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Tools;

public class LiveViewBackgroundPolicyTests
{
    private static readonly DateTime Start = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Clamp_RequestedWithinCap_ReturnsRequested()
    {
        TimeSpan result = LiveViewBackgroundPolicy.Clamp(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(240));

        Assert.Equal(TimeSpan.FromMinutes(30), result);
    }

    [Fact]
    public void Clamp_RequestedAboveCap_ReturnsCap()
    {
        TimeSpan result = LiveViewBackgroundPolicy.Clamp(TimeSpan.FromHours(10), TimeSpan.FromMinutes(240));

        Assert.Equal(TimeSpan.FromMinutes(240), result);
    }

    [Fact]
    public void Clamp_NoRequest_ReturnsCap()
    {
        TimeSpan result = LiveViewBackgroundPolicy.Clamp(null, TimeSpan.FromMinutes(240));

        Assert.Equal(TimeSpan.FromMinutes(240), result);
    }

    [Fact]
    public void Clamp_NonPositiveRequest_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LiveViewBackgroundPolicy.Clamp(TimeSpan.Zero, TimeSpan.FromMinutes(240)));
    }

    [Fact]
    public void IsExpired_BeforeDeadline_ReturnsFalse()
    {
        DateTime until = Start.AddMinutes(30);

        Assert.False(LiveViewBackgroundPolicy.IsExpired(until, Start.AddMinutes(29)));
    }

    [Fact]
    public void IsExpired_AtOrAfterDeadline_ReturnsTrue()
    {
        DateTime until = Start.AddMinutes(30);

        Assert.True(LiveViewBackgroundPolicy.IsExpired(until, until));
        Assert.True(LiveViewBackgroundPolicy.IsExpired(until, until.AddSeconds(1)));
    }

    [Fact]
    public void IsExpired_NoDeadline_ReturnsFalse()
    {
        Assert.False(LiveViewBackgroundPolicy.IsExpired(null, Start.AddDays(7)));
    }

    [Fact]
    public void DeadlineFor_SustainedSinceAndDuration_ReturnsSumUtc()
    {
        DateTime deadline = LiveViewBackgroundPolicy.DeadlineFor(Start, TimeSpan.FromMinutes(45));

        Assert.Equal(Start.AddMinutes(45), deadline);
        Assert.Equal(DateTimeKind.Utc, deadline.Kind);
    }
}
