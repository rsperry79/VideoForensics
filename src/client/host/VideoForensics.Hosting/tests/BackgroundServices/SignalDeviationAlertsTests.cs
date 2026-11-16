using Microsoft.Extensions.Logging.Abstractions;

using System.Globalization;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices;

/// <summary>The server-side deviation check run by the health poll: one alert per camera and hour, when a reading is more than 2 std devs out.</summary>
public class SignalDeviationAlertsTests
{
    private readonly Device _device = new() { Id = Guid.NewGuid(), Name = "Front door", ProviderDeviceId = "1234", Type = "doorbot" };

    [Fact]
    public async Task Check_RaisesAnAlertForAReadingMoreThanTwoStdDevsFromTheMean()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();

        int raised = await CheckAsync(notices.Object, dispatcher.Object);

        Assert.Equal(1, raised);
        dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationEvent>(e => e.EventType == "SignalDeviation"
                && e.Severity == NoticeSeverity.Alert
                && e.Details!.Contains("Front door")
                && e.Details.Contains("sig-dev:")
                && e.ActionPath!.StartsWith($"/signal-history?devices={_device.Id:D}&from=", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Check_ActionPathDrillsToTheAlertedHourPlusOrMinusOneHour()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();
        NotificationEvent? sent = null;
        _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, CancellationToken>((e, _) => sent = e)
            .Returns(Task.CompletedTask);

        _ = await CheckAsync(notices.Object, dispatcher.Object);

        // The alerted hour is the one holding the -95 reading. The scope runs from an hour before it to an hour after it ends.
        DateTime alertedHourUtc = DateTime.UtcNow.AddHours(-1);
        alertedHourUtc = new DateTime(alertedHourUtc.Year, alertedHourUtc.Month, alertedHourUtc.Day, alertedHourUtc.Hour, 0, 0, DateTimeKind.Utc);
        string from = alertedHourUtc.AddHours(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string to = alertedHourUtc.AddHours(2).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        Assert.NotNull(sent);
        Assert.Equal($"/signal-history?devices={_device.Id:D}&from={from}&to={to}", sent!.ActionPath);
    }

    [Fact]
    public async Task Check_DoesNotRaiseTheSameAlertTwice()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var dispatcher = new Mock<INotificationDispatcher>();

        int raised = await CheckAsync(notices.Object, dispatcher.Object);

        Assert.Equal(0, raised);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Check_NoticeStatesTheHourAverageAndCameraMean_InLocalTime()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();
        NotificationEvent? sent = null;
        _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, CancellationToken>((e, _) => sent = e)
            .Returns(Task.CompletedTask);

        _ = await CheckAsync(notices.Object, dispatcher.Object);

        // The alerted hour holds one reading at -95, so its average is -95.0. The camera mean is about -60.
        DateTime alertedHourUtc = DateTime.UtcNow.AddHours(-1);
        alertedHourUtc = new DateTime(alertedHourUtc.Year, alertedHourUtc.Month, alertedHourUtc.Day, alertedHourUtc.Hour, 0, 0, DateTimeKind.Utc);
        string localHour = alertedHourUtc.ToLocalTime().ToString("ddd MMM d HH:00", CultureInfo.InvariantCulture);
        Assert.NotNull(sent);
        Assert.Contains($"hour average RSSI -95.0 dBm at {localHour}", sent!.Details, StringComparison.Ordinal);
        Assert.Contains("camera mean -6", sent.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("UTC", sent.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_TwoSigmaHour_WritesRssiFlagLinkedToNotice()
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

        _ = await CheckAsync(notices.Object, dispatcher.Object, flags.Object);

        DateTime alertedHourUtc = DateTime.UtcNow.AddHours(-1);
        alertedHourUtc = new DateTime(alertedHourUtc.Year, alertedHourUtc.Month, alertedHourUtc.Day, alertedHourUtc.Hour, 0, 0, DateTimeKind.Utc);
        Assert.NotNull(sent);
        _ = Assert.NotNull(sent!.NoticeId);
        MediaFlag flag = Assert.Single(written);
        Assert.Equal(MediaFlagType.RssiDeviation, flag.FlagType);
        Assert.Null(flag.MediaItemId);
        Assert.Equal(_device.Id, flag.DeviceId);
        Assert.Equal(alertedHourUtc, flag.WindowStartUtc);
        Assert.Equal(alertedHourUtc.AddHours(1), flag.WindowEndUtc);
        Assert.Equal(sent.NoticeId, flag.NoticeId);
    }

    [Fact]
    public async Task CheckAsync_RepeatCheckOfSameHour_WritesNoSecondFlag()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var dispatcher = new Mock<INotificationDispatcher>();
        var flags = new Mock<IMediaFlagRepository>();

        _ = await CheckAsync(notices.Object, dispatcher.Object, flags.Object);

        flags.Verify(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Check_DispatchFails_StillWritesRssiFlag()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();
        NotificationEvent? sent = null;
        _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, CancellationToken>((e, _) => sent = e)
            .ThrowsAsync(new InvalidOperationException("boom"));
        var flags = new Mock<IMediaFlagRepository>();
        var written = new List<MediaFlag>();
        _ = flags.Setup(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()))
            .Callback<MediaFlag, CancellationToken>((f, _) => written.Add(f))
            .ReturnsAsync((MediaFlag f, CancellationToken _) => f);

        // The dispatch failure propagates, but the flag was already written, so the next poll does not lose this hour.
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => CheckAsync(notices.Object, dispatcher.Object, flags.Object));

        DateTime alertedHourUtc = DateTime.UtcNow.AddHours(-1);
        alertedHourUtc = new DateTime(alertedHourUtc.Year, alertedHourUtc.Month, alertedHourUtc.Day, alertedHourUtc.Hour, 0, 0, DateTimeKind.Utc);
        Assert.NotNull(sent);
        MediaFlag flag = Assert.Single(written);
        Assert.Equal(MediaFlagType.RssiDeviation, flag.FlagType);
        Assert.Equal(_device.Id, flag.DeviceId);
        Assert.Equal(alertedHourUtc, flag.WindowStartUtc);
        Assert.Equal(alertedHourUtc.AddHours(1), flag.WindowEndUtc);
        Assert.Equal(sent!.NoticeId, flag.NoticeId);
    }

    [Fact]
    public async Task CheckAsync_TwoSigmaHour_StoresDeviationSigmaOfWorstReading()
    {
        var notices = new Mock<INoticeRepository>();
        _ = notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();
        var flags = new Mock<IMediaFlagRepository>();
        var written = new List<MediaFlag>();
        _ = flags.Setup(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()))
            .Callback<MediaFlag, CancellationToken>((f, _) => written.Add(f))
            .ReturnsAsync((MediaFlag f, CancellationToken _) => f);

        _ = await CheckAsync(notices.Object, dispatcher.Object, flags.Object);

        // The worst reading is -95 against a camera mean near -63 and a std dev near 14, so it sits a little over 2 std devs out.
        MediaFlag flag = Assert.Single(written);
        Assert.NotNull(flag.DeviationSigma);
        Assert.InRange(flag.DeviationSigma!.Value, 2.0, 2.5);
    }

    /// <summary>
    /// Ten readings alternating -50 and -70 (mean about -60), then one at -95 an hour ago. The -95 sits about 2.2 sample
    /// std devs below the mean, so it is an alert; the others are inside one std dev.
    /// </summary>
    private async Task<int> CheckAsync(INoticeRepository notices, INotificationDispatcher dispatcher, IMediaFlagRepository? flags = null)
    {
        DateTime now = DateTime.UtcNow;
        var rows = new List<DeviceHealth>();
        for (int i = 0; i < 10; i++)
        {
            rows.Add(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = _device.Id, CapturedAtUtc = now.AddHours(-(i + 2)), WifiSignalRssi = i % 2 == 0 ? -50 : -70 });
        }

        rows.Add(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = _device.Id, CapturedAtUtc = now.AddHours(-1), WifiSignalRssi = -95 });

        var health = new Mock<IDeviceHealthRepository>();
        _ = health.Setup(h => h.GetHistoryAsync(_device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var checker = new SignalDeviationAlerts(
            health.Object,
            notices,
            flags ?? new Mock<IMediaFlagRepository>().Object,
            dispatcher,
            TimeProvider.System,
            NullLogger<SignalDeviationAlerts>.Instance);

        return await checker.CheckAsync(_device, CancellationToken.None);
    }
}
