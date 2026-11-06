using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices;

/// <summary>The server-side bit rate deviation check run by the health poll: one notice and one review flag per camera and hour, when a clip's bit rate is more than 2 std devs out.</summary>
public class BitrateDeviationAlertsTests
{
    private readonly Device _device = new() { Id = Guid.NewGuid(), Name = "Rear camera", ProviderName = "Ring", ProviderDeviceId = "1234", Type = "doorbot" };

    [Fact]
    public async Task BitrateDeviationAlerts_SpikeAboveTwoStdDevs_RaisesOneFlagAndOneNotice()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();
        NotificationEvent? sent = null;
        _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, CancellationToken>((e, _) => sent = e)
            .Returns(Task.CompletedTask);
        var flags = new Mock<IMediaFlagRepository>();
        var written = new List<MediaFlag>();
        _ = flags.Setup(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()))
            .Callback<MediaFlag, CancellationToken>((f, _) => written.Add(f))
            .ReturnsAsync((MediaFlag f, CancellationToken _) => f);

        int raised = await CheckAsync(notices.Object, dispatcher.Object, flags.Object, Spike());

        DateTime alertedHourUtc = DateTime.UtcNow.AddHours(-1);
        alertedHourUtc = new DateTime(alertedHourUtc.Year, alertedHourUtc.Month, alertedHourUtc.Day, alertedHourUtc.Hour, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, raised);
        Assert.NotNull(sent);
        Assert.Equal("BitrateDeviation", sent!.EventType);
        Assert.Equal(NoticeSeverity.Alert, sent.Severity);
        _ = Assert.NotNull(sent.NoticeId);
        Assert.Contains("Rear camera", sent.Details, StringComparison.Ordinal);
        Assert.Contains("sig-dev:bitrate:Ring:1234:", sent.Details, StringComparison.Ordinal);
        Assert.StartsWith($"/bitrate-history?devices={_device.Id:D}&from=", sent.ActionPath, StringComparison.Ordinal);

        MediaFlag flag = Assert.Single(written);
        Assert.Equal(MediaFlagType.BitrateDeviation, flag.FlagType);
        Assert.Null(flag.MediaItemId);
        Assert.Equal(_device.Id, flag.DeviceId);
        Assert.Equal(alertedHourUtc, flag.WindowStartUtc);
        Assert.Equal(alertedHourUtc.AddHours(1), flag.WindowEndUtc);
        Assert.Equal(sent.NoticeId, flag.NoticeId);
    }

    [Fact]
    public async Task BitrateDeviationAlerts_RepeatCheckOfSameWindow_RaisesNothing()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var dispatcher = new Mock<INotificationDispatcher>();
        var flags = new Mock<IMediaFlagRepository>();

        int raised = await CheckAsync(notices.Object, dispatcher.Object, flags.Object, Spike());

        Assert.Equal(0, raised);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        flags.Verify(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BitrateDeviationAlerts_RssiOnlyDevice_WritesNoFlag()
    {
        // A camera with signal history but no measured clip bit rates has nothing for this check to read.
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();
        var flags = new Mock<IMediaFlagRepository>();

        int raised = await CheckAsync(notices.Object, dispatcher.Object, flags.Object, []);

        Assert.Equal(0, raised);
        flags.Verify(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()), Times.Never);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Ten clips alternating 5000 and 7000 kbps (mean about 5500), then one clip at 1000 kbps an hour ago. The 1000 sits
    /// about 2.5 sample std devs below the mean, so it is an alert; the others are inside one std dev.
    /// </summary>
    private static List<MeasuredBitrate> Spike()
    {
        DateTime now = DateTime.UtcNow;
        var rows = new List<MeasuredBitrate>();
        for (int i = 0; i < 10; i++)
        {
            rows.Add(new MeasuredBitrate(now.AddHours(-(i + 2)), (i % 2 == 0 ? 5000L : 7000L) * 1000, "Day", null));
        }

        rows.Add(new MeasuredBitrate(now.AddHours(-1), 1000L * 1000, "Day", null));
        return rows;
    }

    private async Task<int> CheckAsync(
        INoticeRepository notices,
        INotificationDispatcher dispatcher,
        IMediaFlagRepository flags,
        List<MeasuredBitrate> rows)
    {
        var mediaItems = new Mock<IMediaItemRepository>();
        _ = mediaItems.Setup(m => m.GetMeasuredBitrateByDeviceAsync(_device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var checker = new BitrateDeviationAlerts(
            mediaItems.Object,
            notices,
            flags,
            dispatcher,
            TimeProvider.System,
            NullLogger<BitrateDeviationAlerts>.Instance);

        return await checker.CheckAsync(_device, CancellationToken.None);
    }
}
