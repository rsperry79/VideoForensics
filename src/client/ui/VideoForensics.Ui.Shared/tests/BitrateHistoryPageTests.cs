using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The Bit rate history page shows the server's bit rate analysis per device, in kbps, with collapsed clips marked.</summary>
public class BitrateHistory_Tests : ScopeRailTestContext
{
    private readonly Device _device = MakeDevice("Front door");

    private IStringLocalizer<BitrateHistory> Localizer => Services.GetRequiredService<IStringLocalizer<BitrateHistory>>();

    private static BitrateDeviceAnalysis Analysis(Device device, IReadOnlyList<BitrateSample> readings)
    {
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow.AddDays(1);
        var samples = readings.Select(r => r.ToSignalSample()).ToList();
        var allTime = SignalStats.Compute(samples);
        return new BitrateDeviceAnalysis(
            device.Id,
            device.Name,
            "Ring",
            device.ProviderDeviceId,
            allTime,
            allTime,
            readings,
            SignalDeviation.Windows(samples, from, to, allTime),
            SignalCalendar.Compute(samples, from, to),
            SignalBuckets.Compute(samples, from, to, SignalBucket.Hour),
            SignalBuckets.Compute(samples, from, to, SignalBucket.Day),
            SignalBuckets.Compute(samples, from, to, SignalBucket.Week));
    }

    private IRenderedComponent<BitrateHistory> RenderWith(params BitrateDeviceAnalysis[] devices)
    {
        _ = RegisterDeviceRepository(_device);
        var service = new Mock<IBitrateAnalysisService>();
        _ = service
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BitrateAnalysis(devices));
        _ = Services.AddScoped(_ => service.Object);
        _ = Services.AddScoped<SignalDrillTrail>();
        var flags = new Mock<IMediaFlagRepository>();
        _ = flags
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0).ToDto());
        _ = Services.AddScoped(_ => flags.Object);
        return Render<BitrateHistory>();
    }

    private static List<BitrateSample> Steady(int count, int kbps = 2000, int collapsed = 0)
    {
        DateTime start = DateTime.UtcNow.AddDays(-3);
        return Enumerable.Range(0, count)
            .Select(i => new BitrateSample(start.AddHours(i * 3), kbps, "Day", i < collapsed))
            .ToList();
    }

    [Fact]
    public void Renders_ADeviceCardWithStatsInKbps()
    {
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, Steady(6)));

        _ = Assert.Single(page.FindAll("[data-testid='bitrate-device']"));
        Assert.Contains("Front door", page.Markup);
        Assert.Contains("2000 kbps", page.Find("[data-testid='bitrate-stats']").TextContent);
        Assert.Empty(page.FindAll("[data-testid='signal-device']"));
    }

    [Fact]
    public void Renders_TheCollapsedClipCount_AndMarksEachCollapsedReading()
    {
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, Steady(6, collapsed: 2)));

        Assert.Equal("2", page.Find("[data-testid='bitrate-collapsed-count']").TextContent.Trim());
        Assert.Equal(2, page.FindAll("circle.bitrate-collapse").Count);
        Assert.Contains(Localizer["LegendCollapse"].Value, page.Find("[data-testid='bitrate-legend']").TextContent);
    }

    [Fact]
    public void Renders_NoSignalOverlays_NoWeakRow_AndNoRingSettingsLink()
    {
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, Steady(6)));

        Assert.Empty(page.FindAll(".signal-period"));
        Assert.Empty(page.FindAll("a[href*='ring']"));
        Assert.DoesNotContain("%", page.Find("[data-testid='bitrate-stats']").TextContent);
    }

    [Fact]
    public void ADeviceWithNoReadings_ShowsTheNoReadingsMessage()
    {
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, []));

        Assert.Contains(Localizer["NoReadings"].Value, page.Find("[data-testid='bitrate-device']").TextContent);
        Assert.Empty(page.FindAll("[data-testid='bitrate-stats']"));
    }

    [Fact]
    public void TheBucketPicker_SwitchesTheRowsShown()
    {
        // Eight readings every 12 hours cover several days inside one or two weeks.
        DateTime start = DateTime.UtcNow.AddDays(-4);
        var readings = Enumerable.Range(0, 8)
            .Select(i => new BitrateSample(start.AddHours(i * 12), 2000 + i, "Day", false))
            .ToList();
        BitrateDeviceAnalysis analysis = Analysis(_device, readings);
        IRenderedComponent<BitrateHistory> page = RenderWith(analysis);

        Assert.Equal(analysis.Weekly.Count, page.FindAll("[data-testid='bitrate-buckets'] tbody tr").Count);

        page.FindAll("[data-testid='bitrate-bucket-picker'] button")[1].Click();

        Assert.Equal(analysis.Daily.Count, page.FindAll("[data-testid='bitrate-buckets'] tbody tr").Count);

        page.FindAll("[data-testid='bitrate-bucket-picker'] button")[0].Click();

        Assert.Equal(analysis.Hourly.Count, page.FindAll("[data-testid='bitrate-buckets'] tbody tr").Count);
    }

    [Fact]
    public void ATwoSigmaOutlier_ShowsTheAlert_ThatDismisses()
    {
        List<BitrateSample> readings = Steady(20);
        readings.Add(new BitrateSample(DateTime.UtcNow.AddHours(-1), 9000, "Day", false));
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, readings));

        _ = Assert.Single(page.FindAll("[data-testid='bitrate-alert']"));
        Assert.NotEmpty(page.FindAll("rect.bitrate-deviation[data-level='TwoSigma']"));

        page.Find("[data-testid='bitrate-alert-close']").Click();

        Assert.Empty(page.FindAll("[data-testid='bitrate-alert']"));
    }

    [Fact]
    public void SteadyReadings_ShowNoAlert()
    {
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, Steady(10)));

        Assert.Empty(page.FindAll("[data-testid='bitrate-alert']"));
    }

    private IStringLocalizer<SharedResources> SharedL => Services.GetRequiredService<IStringLocalizer<SharedResources>>();

    /// <summary>Two-sigma windows on consecutive past hours, one alert each.</summary>
    private static SignalDeviationWindow[] TwoSigmaHours(int count)
    {
        DateTime latest = DateTime.UtcNow.Date.AddDays(-1);
        return Enumerable.Range(0, count)
            .Select(i => new SignalDeviationWindow(latest.AddHours(-i), SignalDeviationLevel.TwoSigma, 1, 9000))
            .ToArray();
    }

    [Fact]
    public void SevenTwoSigmaAlerts_ShowFiveAndAShowMoreLink()
    {
        BitrateDeviceAnalysis analysis = Analysis(_device, Steady(6)) with { Deviations = TwoSigmaHours(7) };
        IRenderedComponent<BitrateHistory> page = RenderWith(analysis);

        Assert.Equal(5, page.FindAll("[data-testid='bitrate-alert'] li").Count);
        Assert.Equal(SharedL["ShowMoreAlerts", 2].Value, page.Find("[data-testid='bitrate-alert-toggle']").TextContent.Trim());
    }

    [Fact]
    public void ShowMore_ExpandsTheAlertList_AndShowLessCollapsesIt()
    {
        BitrateDeviceAnalysis analysis = Analysis(_device, Steady(6)) with { Deviations = TwoSigmaHours(7) };
        IRenderedComponent<BitrateHistory> page = RenderWith(analysis);

        page.Find("[data-testid='bitrate-alert-toggle']").Click();

        Assert.Equal(7, page.FindAll("[data-testid='bitrate-alert'] li").Count);
        Assert.Equal(SharedL["ShowLessAlerts"].Value, page.Find("[data-testid='bitrate-alert-toggle']").TextContent.Trim());

        page.Find("[data-testid='bitrate-alert-toggle']").Click();

        Assert.Equal(5, page.FindAll("[data-testid='bitrate-alert'] li").Count);
    }

    [Fact]
    public void BitrateHistory_Renders_TheFlaggedMediaTable()
    {
        IRenderedComponent<BitrateHistory> page = RenderWith(Analysis(_device, Steady(6)));

        FlaggedMediaTable table = page.FindComponent<FlaggedMediaTable>().Instance;

        Assert.NotNull(table);
        Assert.Equal(new[] { "BitrateCollapse", "BitrateDeviation" }, table.Types);
    }

    [Fact]
    public void CalendarBox_IsGreenUnderOneStdDevAndRedAtTwoOrMore()
    {
        // All-time mean 2000 kbps with a 100 kbps sd. Monday 00:00 is at 2050 (0.5 sd), Monday 01:00 at 1800 (2 sd).
        var cells = Enumerable.Range(0, SignalCalendar.Days * SignalCalendar.Hours)
            .Select(_ => SignalStats.Compute(Array.Empty<SignalSample>()))
            .ToList();
        DateTime when = DateTime.UtcNow.AddDays(-3);
        cells[0] = SignalStats.Compute([new SignalSample(when, 2050)]);
        cells[1] = SignalStats.Compute([new SignalSample(when, 1800)]);
        var calendar = new SignalCalendar(
            cells,
            Enumerable.Range(0, SignalCalendar.Hours).Select(_ => SignalStats.Compute(Array.Empty<SignalSample>())).ToList(),
            Enumerable.Range(0, SignalCalendar.Days).Select(_ => SignalStats.Compute(Array.Empty<SignalSample>())).ToList());
        var allTime = new SignalStats(Count: 10, Min: 1700, Max: 2300, Average: 2000, Latest: 2000, PercentWeak: 0, StdDev: 100);
        var analysis = new BitrateDeviceAnalysis(
            _device.Id,
            _device.Name,
            "Ring",
            _device.ProviderDeviceId,
            allTime,
            allTime,
            Array.Empty<BitrateSample>(),
            Array.Empty<SignalDeviationWindow>(),
            calendar,
            Array.Empty<SignalBucketStats>(),
            Array.Empty<SignalBucketStats>(),
            Array.Empty<SignalBucketStats>());

        IRenderedComponent<BitrateHistory> page = RenderWith(analysis);

        Assert.Single(page.FindAll("[data-testid='bitrate-calendar'] tbody tr:first-child td.calendar-box-green"));
        Assert.Single(page.FindAll("[data-testid='bitrate-calendar'] tbody tr:first-child td.calendar-box-red"));
    }

    private IStringLocalizer<BitrateHistory> PageL => Services.GetRequiredService<IStringLocalizer<BitrateHistory>>();

    private const string FirstBoxCell = "[data-testid='bitrate-calendar'] tbody tr:first-child td:nth-child(2)";
    private const string SecondBoxCell = "[data-testid='bitrate-calendar'] tbody tr:first-child td:nth-child(3)";
    private const string FooterCells = "[data-testid='bitrate-calendar'] tfoot";

    /// <summary>A grid box's stats: a mean over the count readings, with an optional sample sd.</summary>
    private static SignalStats Box(int count, double mean, double? stdDev = null)
    {
        return new SignalStats(Count: count, Min: null, Max: null, Average: mean, Latest: null, PercentWeak: 0, StdDev: stdDev);
    }

    /// <summary>A week grid from the given cells, hour-of-day and day-of-week profiles, keyed by index. Missing entries are empty.</summary>
    private static SignalCalendar BuildCalendar(Dictionary<int, SignalStats> cells, Dictionary<int, SignalStats> hours, Dictionary<int, SignalStats> days)
    {
        SignalStats empty = SignalStats.Compute(Array.Empty<SignalSample>());
        return new SignalCalendar(
            Enumerable.Range(0, SignalCalendar.Days * SignalCalendar.Hours).Select(i => cells.GetValueOrDefault(i, empty)).ToList(),
            Enumerable.Range(0, SignalCalendar.Hours).Select(i => hours.GetValueOrDefault(i, empty)).ToList(),
            Enumerable.Range(0, SignalCalendar.Days).Select(i => days.GetValueOrDefault(i, empty)).ToList());
    }

    /// <summary>One camera with a 2000 kbps all-time mean and a 100 kbps sd, and the given grid.</summary>
    private static BitrateDeviceAnalysis DeviceWithCalendar(SignalCalendar calendar)
    {
        var allTime = new SignalStats(Count: 10, Min: 1700, Max: 2300, Average: 2000, Latest: 2000, PercentWeak: 0, StdDev: 100);
        return new BitrateDeviceAnalysis(
            Guid.NewGuid(),
            "Front door",
            "Ring",
            "ring-front",
            allTime,
            allTime,
            Array.Empty<BitrateSample>(),
            Array.Empty<SignalDeviationWindow>(),
            calendar,
            Array.Empty<SignalBucketStats>(),
            Array.Empty<SignalBucketStats>(),
            Array.Empty<SignalBucketStats>());
    }

    /// <summary>Renders the page. The first load gets the first analysis, and each later load (a scope change) the next one.</summary>
    private IRenderedComponent<BitrateHistory> RenderSequence(params BitrateDeviceAnalysis[] responses)
    {
        _ = RegisterDeviceRepository(_device);
        var queue = new Queue<BitrateAnalysis>(responses.Select(r => new BitrateAnalysis([r])));
        var service = new Mock<IBitrateAnalysisService>();
        _ = service
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek());
        _ = Services.AddScoped(_ => service.Object);
        _ = Services.AddScoped<SignalDrillTrail>();
        var flags = new Mock<IMediaFlagRepository>();
        _ = flags
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0).ToDto());
        _ = Services.AddScoped(_ => flags.Object);
        return Render<BitrateHistory>();
    }

    [Fact]
    public void CalendarBox_TooltipShowsItsValue_ReadingCount_AndWeekAndHourSigma()
    {
        IRenderedComponent<BitrateHistory> page = RenderSequence(DeviceWithCalendar(BuildCalendar(
            new() { [0] = Box(2, 2050, 3.5) },
            new() { [0] = Box(4, 2050, 6.8) },
            new() { [0] = Box(5, 2050, 4.2) })));

        string title = page.Find(FirstBoxCell).GetAttribute("title");

        Assert.Equal(
            PageL["CalendarCellTitle", PageL["CalendarTipValue"].Value, "2050.0 kbps", 2, PageL["CalendarTipWeekSigma"].Value, "4.2", PageL["CalendarTipHourSigma"].Value, "6.8"].Value,
            title);
    }

    [Fact]
    public void GridMode_ReadingShowsTheMean_AndDeviationShowsDToOneDecimal()
    {
        // The box mean of 2150 kbps sits 1.5 sd from the all-time mean of 2000 kbps (sd 100). The next box is empty.
        IRenderedComponent<BitrateHistory> page = RenderSequence(DeviceWithCalendar(BuildCalendar(new() { [0] = Box(1, 2150) }, new(), new())));

        Assert.Equal("2150", page.Find(FirstBoxCell).TextContent.Trim());

        page.Find("[data-testid='bitrate-grid-mode-deviation']").Click();

        Assert.Equal("1.5", page.Find(FirstBoxCell).TextContent.Trim());
        Assert.Equal(string.Empty, page.Find(SecondBoxCell).TextContent.Trim());

        page.Find("[data-testid='bitrate-grid-mode-reading']").Click();

        Assert.Equal("2150", page.Find(FirstBoxCell).TextContent.Trim());
    }

    [Fact]
    public void ChangingTheRange_RedrawsTheGrid_ButLeavesTheFooterSigmaUnchanged()
    {
        Dictionary<int, SignalStats> hours = new() { [0] = Box(4, 2000, 6.8) };
        BitrateDeviceAnalysis before = DeviceWithCalendar(BuildCalendar(new() { [0] = Box(2, 2050) }, hours, new()));
        BitrateDeviceAnalysis after = DeviceWithCalendar(BuildCalendar(new() { [0] = Box(2, 2300) }, hours, new()));
        IRenderedComponent<BitrateHistory> page = RenderSequence(before, after);
        string footer = page.Find(FooterCells).TextContent;
        Assert.Equal("2050", page.Find(FirstBoxCell).TextContent.Trim());

        Services.GetRequiredService<ScopeState>().Set(new ForensicScope([], DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1), null));

        page.WaitForAssertion(() => Assert.Equal("2300", page.Find(FirstBoxCell).TextContent.Trim()));
        Assert.Equal(footer, page.Find(FooterCells).TextContent);
    }
}
