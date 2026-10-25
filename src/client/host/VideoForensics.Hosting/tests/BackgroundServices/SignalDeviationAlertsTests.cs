using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

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
        notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dispatcher = new Mock<INotificationDispatcher>();

        int raised = await CheckAsync(notices.Object, dispatcher.Object);

        Assert.Equal(1, raised);
        dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationEvent>(e => e.EventType == "SignalDeviation"
                && e.Severity == NoticeSeverity.Alert
                && e.Details!.Contains("Front door")
                && e.Details.Contains("sig-dev:")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Check_DoesNotRaiseTheSameAlertTwice()
    {
        var notices = new Mock<INoticeRepository>();
        notices.Setup(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var dispatcher = new Mock<INotificationDispatcher>();

        int raised = await CheckAsync(notices.Object, dispatcher.Object);

        Assert.Equal(0, raised);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Ten readings alternating -50 and -70 (mean about -60), then one at -95 an hour ago. The -95 sits about 2.2 sample
    /// std devs below the mean, so it is an alert; the others are inside one std dev.
    /// </summary>
    private async Task<int> CheckAsync(INoticeRepository notices, INotificationDispatcher dispatcher)
    {
        DateTime now = DateTime.UtcNow;
        var rows = new List<DeviceHealth>();
        for (int i = 0; i < 10; i++)
        {
            rows.Add(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = _device.Id, CapturedAtUtc = now.AddHours(-(i + 2)), WifiSignalRssi = i % 2 == 0 ? -50 : -70 });
        }

        rows.Add(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = _device.Id, CapturedAtUtc = now.AddHours(-1), WifiSignalRssi = -95 });

        var health = new Mock<IDeviceHealthRepository>();
        health.Setup(h => h.GetHistoryAsync(_device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var checker = new SignalDeviationAlerts(
            health.Object,
            notices,
            dispatcher,
            TimeProvider.System,
            NullLogger<SignalDeviationAlerts>.Instance);

        return await checker.CheckAsync(_device, CancellationToken.None);
    }
}
