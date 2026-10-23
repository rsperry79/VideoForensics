
using Bunit;

using VideoForensics.Client.Ui.Shared.Components;
using VideoForensics.Client.Ui.Shared.Services.Time;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>Displays a stored UTC instant in the user's zone, with a fallback for missing values.</summary>
public class LocalTimeTextTests : BunitContext
{
    private static readonly TimeZoneInfo Plus9 = TimeZoneInfo.CreateCustomTimeZone("test-plus9", TimeSpan.FromHours(9), "test-plus9", "test-plus9");
    private static readonly DateTime NoonUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WithUserZone_ShowsValueInThatZone()
    {
        IRenderedComponent<LocalTimeText> rendered = Render<LocalTimeText>(p => p
            .AddCascadingValue(new UserTimeZone(Plus9))
            .Add(c => c.Value, (DateTime?)NoonUtc)
            .Add(c => c.Format, "HH:mm"));

        Assert.Equal("21:00", rendered.Find("time").TextContent);
        Assert.Equal("2026-10-20T12:00:00Z", rendered.Find("time").GetAttribute("datetime"));
    }

    [Fact]
    public void WithoutUserZone_ShowsMachineLocalTime()
    {
        IRenderedComponent<LocalTimeText> rendered = Render<LocalTimeText>(p => p
            .Add(c => c.Value, (DateTime?)NoonUtc)
            .Add(c => c.Format, "HH:mm"));

        Assert.Equal(NoonUtc.ToLocalTime().ToString("HH:mm"), rendered.Find("time").TextContent);
    }

    [Fact]
    public void NullValue_ShowsFallback()
    {
        IRenderedComponent<LocalTimeText> rendered = Render<LocalTimeText>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Fallback, "Never"));

        Assert.Equal("Never", rendered.Markup.Trim());
    }
}
