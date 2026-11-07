using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices;

/// <summary>The one-off deviation backfill: fills null sigmas from the all-time baseline, once, guarded by a marker.</summary>
public class MediaFlagDeviationBackfillTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();

    // The spike hour: one reading at -95 among readings around -50 and -70, so the spike sits a little over 2 std devs out.
    private static readonly DateTime SpikeHourUtc = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static MediaFlag RssiFlag(DateTime hourUtc) => new()
    {
        Id = Guid.NewGuid(),
        FlagType = MediaFlagType.RssiDeviation,
        DeviceId = DeviceId,
        WindowStartUtc = hourUtc,
        WindowEndUtc = hourUtc.AddHours(1),
        RaisedAtUtc = hourUtc,
    };

    private static List<DeviceHealth> History()
    {
        var rows = new List<DeviceHealth>();
        for (int i = 0; i < 10; i++)
        {
            rows.Add(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = DeviceId, CapturedAtUtc = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc).AddHours(i), WifiSignalRssi = i % 2 == 0 ? -50 : -70 });
        }

        rows.Add(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = DeviceId, CapturedAtUtc = SpikeHourUtc.AddMinutes(10), WifiSignalRssi = -95 });
        return rows;
    }

    private static (MediaFlagDeviationBackfill Backfill, Mock<IMediaFlagDeviationRepository> Flags, Mock<IAppSettingRepository> Settings) Build(
        IReadOnlyList<MediaFlag> pending, string? marker = null)
    {
        var flags = new Mock<IMediaFlagDeviationRepository>();
        _ = flags.Setup(f => f.ListWithoutDeviationSigmaAsync(MediaFlagType.RssiDeviation, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        var health = new Mock<IDeviceHealthRepository>();
        _ = health.Setup(h => h.GetHistoryAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(History());
        var settings = new Mock<IAppSettingRepository>();
        _ = settings.Setup(s => s.GetAsync(MediaFlagDeviationBackfill.MarkerKey, It.IsAny<CancellationToken>())).ReturnsAsync(marker);
        var backfill = new MediaFlagDeviationBackfill(flags.Object, health.Object, settings.Object, NullLogger<MediaFlagDeviationBackfill>.Instance);
        return (backfill, flags, settings);
    }

    [Fact]
    public async Task RunOnceAsync_RssiFlagWithoutSigma_StoresSigmaOfWorstReadingInItsHour()
    {
        MediaFlag flag = RssiFlag(SpikeHourUtc);
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, _) = Build([flag]);

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, filled);
        flags.Verify(f => f.SetDeviationSigmaAsync(flag.Id, It.Is<double>(s => s > 2.0 && s < 2.5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_FlagHourWithNoDeviatingReading_LeavesSigmaNull()
    {
        MediaFlag flag = RssiFlag(SpikeHourUtc.AddHours(-7));
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, _) = Build([flag]);

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, filled);
        flags.Verify(f => f.SetDeviationSigmaAsync(It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_CompletedPass_WritesMarker()
    {
        (MediaFlagDeviationBackfill backfill, _, Mock<IAppSettingRepository> settings) = Build([RssiFlag(SpikeHourUtc)]);

        _ = await backfill.RunOnceAsync(CancellationToken.None);

        settings.Verify(s => s.SetAsync(MediaFlagDeviationBackfill.MarkerKey, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_MarkerAlreadyWritten_SkipsThePass()
    {
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, Mock<IAppSettingRepository> settings) =
            Build([RssiFlag(SpikeHourUtc)], marker: "2026-10-02T00:00:00Z");

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, filled);
        flags.Verify(f => f.ListWithoutDeviationSigmaAsync(It.IsAny<MediaFlagType>(), It.IsAny<CancellationToken>()), Times.Never);
        settings.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
