using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices;

/// <summary>The one-off deviation backfill: fills null sigmas from the all-time baselines, once per pass, each guarded by its own marker.</summary>
public class MediaFlagDeviationBackfillTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();

    // The spike hour: one reading at -95 among readings around -50 and -70, so the spike sits a little over 2 std devs out.
    private static readonly DateTime SpikeHourUtc = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    // The all-time bit rate baseline of the device: mean 5545.45 kbps, sample sd about 1809 kbps.
    private static readonly AllTimeBitrateBaseline BitrateAllTime = new(11, 5_545_454.5, 1_809_076.0);

    private static MediaFlag RssiFlag(DateTime hourUtc) => new()
    {
        Id = Guid.NewGuid(),
        FlagType = MediaFlagType.RssiDeviation,
        DeviceId = DeviceId,
        WindowStartUtc = hourUtc,
        WindowEndUtc = hourUtc.AddHours(1),
        RaisedAtUtc = hourUtc,
    };

    private static MediaFlag CollapseFlag(Guid mediaItemId) => new()
    {
        Id = Guid.NewGuid(),
        FlagType = MediaFlagType.BitrateCollapse,
        DeviceId = DeviceId,
        MediaItemId = mediaItemId,
        WindowStartUtc = SpikeHourUtc,
        WindowEndUtc = SpikeHourUtc.AddMinutes(1),
        RaisedAtUtc = SpikeHourUtc,
    };

    private static MediaItem Clip(long videoBitrateBps) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = DeviceId,
        FileName = "clip.mp4",
        FilePath = "clip.mp4",
        MediaFormat = "mp4",
        Sha256Hash = Guid.NewGuid().ToString("N"),
        VideoBitrateBps = videoBitrateBps,
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
        IReadOnlyList<MediaFlag> pending,
        string? marker = null,
        string? bitrateMarker = null,
        IReadOnlyList<MediaFlag>? collapse = null,
        MediaItem? clip = null)
    {
        var flags = new Mock<IMediaFlagDeviationRepository>();
        _ = flags.Setup(f => f.ListWithoutDeviationSigmaAsync(MediaFlagType.RssiDeviation, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _ = flags.Setup(f => f.ListWithoutDeviationSigmaAsync(MediaFlagType.BitrateCollapse, It.IsAny<CancellationToken>())).ReturnsAsync(collapse ?? []);
        var health = new Mock<IDeviceHealthRepository>();
        _ = health.Setup(h => h.GetHistoryAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(History());
        var media = new Mock<IMediaItemRepository>();
        _ = media.Setup(m => m.GetBitrateBaselineByDeviceAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(BitrateAllTime);
        _ = media.Setup(m => m.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        var settings = new Mock<IAppSettingRepository>();
        _ = settings.Setup(s => s.GetAsync(MediaFlagDeviationBackfill.MarkerKey, It.IsAny<CancellationToken>())).ReturnsAsync(marker);
        _ = settings.Setup(s => s.GetAsync(MediaFlagDeviationBackfill.BitrateMarkerKey, It.IsAny<CancellationToken>())).ReturnsAsync(bitrateMarker);
        var backfill = new MediaFlagDeviationBackfill(flags.Object, health.Object, media.Object, settings.Object, NullLogger<MediaFlagDeviationBackfill>.Instance);
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
    public async Task RunOnceAsync_MarkersAlreadyWritten_SkipsBothPasses()
    {
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, Mock<IAppSettingRepository> settings) =
            Build([RssiFlag(SpikeHourUtc)], marker: "2026-10-02T00:00:00Z", bitrateMarker: "2026-11-08T00:00:00Z");

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, filled);
        flags.Verify(f => f.ListWithoutDeviationSigmaAsync(It.IsAny<MediaFlagType>(), It.IsAny<CancellationToken>()), Times.Never);
        settings.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_RssiMarkerOnly_BitrateCollapsePassStillRuns()
    {
        // The RSSI pass is done, but the bit rate pass has its own marker and has not run yet.
        MediaItem clip = Clip(1_000_000);
        MediaFlag collapse = CollapseFlag(clip.Id);
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, _) =
            Build([RssiFlag(SpikeHourUtc)], marker: "2026-10-02T00:00:00Z", collapse: [collapse], clip: clip);

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, filled);
        flags.Verify(f => f.ListWithoutDeviationSigmaAsync(MediaFlagType.RssiDeviation, It.IsAny<CancellationToken>()), Times.Never);
        flags.Verify(f => f.SetDeviationSigmaAsync(collapse.Id, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_BitrateCollapseFlagWithoutSigma_StoresSigmaOfItsClipAgainstAllTimeBaseline()
    {
        // A 1000 kbps clip against the all-time baseline (mean about 5545, sd about 1809) sits about 2.5 std devs out.
        MediaItem clip = Clip(1_000_000);
        MediaFlag collapse = CollapseFlag(clip.Id);
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, Mock<IAppSettingRepository> settings) =
            Build([], collapse: [collapse], clip: clip);

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, filled);
        flags.Verify(f => f.SetDeviationSigmaAsync(collapse.Id, It.Is<double>(s => s > 2.4 && s < 2.6), It.IsAny<CancellationToken>()), Times.Once);
        settings.Verify(s => s.SetAsync(MediaFlagDeviationBackfill.BitrateMarkerKey, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_BitrateCollapseFlagWhoseClipIsGone_LeavesSigmaNull()
    {
        MediaFlag collapse = CollapseFlag(Guid.NewGuid());
        (MediaFlagDeviationBackfill backfill, Mock<IMediaFlagDeviationRepository> flags, _) = Build([], collapse: [collapse], clip: null);

        int filled = await backfill.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, filled);
        flags.Verify(f => f.SetDeviationSigmaAsync(It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
