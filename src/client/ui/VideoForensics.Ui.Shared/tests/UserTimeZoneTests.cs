
using VideoForensics.Ui.Shared.Services.Time;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>Converts stored UTC instants into the signed-in user's zone for display.</summary>
public class UserTimeZoneTests
{
    private static readonly TimeZoneInfo Plus9 = TimeZoneInfo.CreateCustomTimeZone("test-plus9", TimeSpan.FromHours(9), "test-plus9", "test-plus9");

    [Fact]
    public void ToLocal_UtcKindValue_ConvertsIntoZone()
    {
        var zone = new UserTimeZone(Plus9);

        DateTime local = zone.ToLocal(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 10, 20, 21, 0, 0), local);
    }

    [Fact]
    public void ToLocal_UnspecifiedKind_TreatedAsUtc()
    {
        var zone = new UserTimeZone(Plus9);

        DateTime local = zone.ToLocal(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(new DateTime(2026, 10, 20, 21, 0, 0), local);
    }

    [Fact]
    public void ToLocal_LocalKindValue_ConvertedThroughUtcFirst()
    {
        var zone = new UserTimeZone(TimeZoneInfo.Utc);
        var utc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        DateTime local = utc.ToLocalTime();

        Assert.Equal(utc, zone.ToLocal(local));
    }

    [Fact]
    public void Unavailable_FallsBackToUtc()
    {
        UserTimeZone zone = UserTimeZone.Unavailable;

        Assert.Equal(TimeZoneInfo.Utc, zone.Zone);
        Assert.Equal(new DateTime(2026, 10, 20, 12, 0, 0), zone.ToLocal(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Format_UsesZoneThenFormatString()
    {
        var zone = new UserTimeZone(Plus9);

        string text = zone.Format(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), "HH:mm");

        Assert.Equal("21:00", text);
    }
}
