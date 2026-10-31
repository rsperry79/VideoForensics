using Moq;

using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Client.Hosting.Tests.Signal;

/// <summary>The server-side bit rate figures: window versus all-time maths, unknown ids and the window check.</summary>
public class BitrateAnalysisServiceTests
{
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc); // a Monday
    private static readonly DateTime To = From.AddDays(7);

    private static Device Camera(Guid id, string name = "Front door")
    {
        return new Device { Id = id, Name = name, ProviderDeviceId = "1234", Type = "camera", ProviderName = "Ring" };
    }

    private static MeasuredBitrate Row(DateTime at, long bps, string? lighting = null, string? flags = null)
    {
        return new MeasuredBitrate(at, bps, lighting, flags);
    }

    private static BitrateAnalysisService Create(Device[] devices, Dictionary<Guid, MeasuredBitrate[]> history)
    {
        var deviceRepo = new Mock<IDeviceRepository>();
        _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(devices);
        var media = new Mock<IMediaItemRepository>();
        _ = media
            .Setup(r => r.GetMeasuredBitrateByDeviceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => history.TryGetValue(id, out MeasuredBitrate[]? rows) ? rows : []);
        return new BitrateAnalysisService(deviceRepo.Object, media.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_WindowAndHistory_SplitsWindowStatsFromAllTimeBaseline()
    {
        var id = Guid.NewGuid();
        MeasuredBitrate[] rows =
        [
            Row(From.AddDays(-3), 5_000_000),
            Row(From.AddHours(9), 1_000_000, "Day"),
            Row(From.AddHours(10), 2_000_000, "Ir", "BitrateCollapse"),
            Row(From.AddHours(11), 3_000_000, "Day", "ClippedAudio,BitrateCollapse"),
        ];
        BitrateAnalysisService service = Create([Camera(id)], new() { [id] = rows });

        BitrateAnalysis result = await service.AnalyzeAsync([id], From, To, CancellationToken.None);

        BitrateDeviceAnalysis device = Assert.Single(result.Devices);
        Assert.Equal("Front door", device.Name);
        Assert.Equal("Ring", device.ProviderName);
        Assert.Equal(4, device.AllTime.Count);
        Assert.Equal(2_750, device.AllTime.Average);
        Assert.Equal(3, device.PeriodStats.Count);
        Assert.Equal(2_000, device.PeriodStats.Average);
        Assert.Equal(1_000, device.PeriodStats.Min);
        Assert.Equal(3_000, device.PeriodStats.Max);
        Assert.Equal(3_000, device.PeriodStats.Latest);
        Assert.Equal(
            [new BitrateSample(From.AddHours(9), 1_000, "Day", false), new BitrateSample(From.AddHours(10), 2_000, "Ir", true), new BitrateSample(From.AddHours(11), 3_000, "Day", true)],
            device.Readings);
        Assert.Equal(3, device.Hourly.Count);
        Assert.Single(device.Daily);
        Assert.Equal(3, device.Calendar.DayOfWeek(0).Count);
    }

    [Fact]
    public async Task AnalyzeAsync_WideSpreadReading_FlagsItsHourAgainstAllTimeBaseline()
    {
        var id = Guid.NewGuid();
        var rows = new List<MeasuredBitrate>();
        for (int i = 0; i < 20; i++)
        {
            rows.Add(Row(From.AddHours(i), 4_000_000));
        }

        rows.Add(Row(From.AddHours(30), 100_000));
        BitrateAnalysisService service = Create([Camera(id)], new() { [id] = [.. rows] });

        BitrateAnalysis result = await service.AnalyzeAsync([id], From, To, CancellationToken.None);

        SignalDeviationWindow flagged = Assert.Single(result.Devices[0].Deviations);
        Assert.Equal(From.AddHours(30), flagged.HourStartUtc);
        Assert.Equal(100, flagged.WorstRssiDbm);
    }

    [Fact]
    public async Task AnalyzeAsync_UnknownId_IsOmitted()
    {
        var known = Guid.NewGuid();
        BitrateAnalysisService service = Create([Camera(known)], []);

        BitrateAnalysis result = await service.AnalyzeAsync([known, Guid.NewGuid()], From, To, CancellationToken.None);

        BitrateDeviceAnalysis device = Assert.Single(result.Devices);
        Assert.Equal(known, device.DeviceId);
        Assert.Equal(0, device.AllTime.Count);
        Assert.Empty(device.Readings);
    }

    [Fact]
    public async Task AnalyzeAsync_FromAfterTo_Throws()
    {
        BitrateAnalysisService service = Create([], []);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.AnalyzeAsync([Guid.NewGuid()], To, From, CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsync_BlankName_FallsBackToProviderId()
    {
        var id = Guid.NewGuid();
        BitrateAnalysisService service = Create([Camera(id, name: " ")], []);

        BitrateAnalysis result = await service.AnalyzeAsync([id], From, To, CancellationToken.None);

        Assert.Contains("1234", result.Devices[0].Name);
    }
}
