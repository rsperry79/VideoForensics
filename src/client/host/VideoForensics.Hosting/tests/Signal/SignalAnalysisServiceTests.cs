using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.Signal;

/// <summary>The server-side signal calendar: cells follow the selected range, hour and day profiles are all-time.</summary>
public class SignalAnalysisServiceTests
{
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc); // a Monday
    private static readonly DateTime To = From.AddDays(7);

    private static Device Camera(Guid id)
    {
        return new Device { Id = id, Name = "Front door", ProviderDeviceId = "1234", Type = "camera", ProviderName = "Ring" };
    }

    private static SignalAnalysisService Create(Device[] devices, Dictionary<Guid, DeviceHealth[]> history)
    {
        var deviceRepo = new Mock<IDeviceRepository>();
        _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(devices);
        var health = new Mock<IDeviceHealthRepository>();
        _ = health
            .Setup(r => r.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => history.TryGetValue(id, out DeviceHealth[]? rows) ? rows : []);
        var schedules = new Mock<ISyncScheduleRepository>();
        var accounts = new Mock<IProviderAccountRepository>();
        _ = accounts.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ProviderAccount>());
        var locations = new Mock<ILocationRepository>();
        var users = new Mock<IUserRepository>();
        var ownership = new DeviceOwnershipService(accounts.Object, locations.Object, users.Object);
        return new SignalAnalysisService(deviceRepo.Object, health.Object, schedules.Object, ownership);
    }

    [Fact]
    public async Task AnalyzeAsync_ReadingOutsideRange_IsLeftOutOfCellsButKeepsAllTimeHourProfile()
    {
        var id = Guid.NewGuid();
        // Sunday 14:00 is before the window, so the 14:00 Sunday cell is empty. Monday 09:00 is inside the window.
        DeviceHealth[] rows =
        [
            new() { DeviceId = id, CapturedAtUtc = From.AddDays(-1).AddHours(14), WifiSignalRssi = -60 },
            new() { DeviceId = id, CapturedAtUtc = From.AddHours(9), WifiSignalRssi = -50 },
        ];
        SignalAnalysisService service = Create([Camera(id)], new() { [id] = rows });

        SignalAnalysis result = await service.AnalyzeAsync([id], From, To, CancellationToken.None);

        SignalCalendar calendar = Assert.Single(result.Devices).Calendar;
        Assert.Equal(0, calendar.Cell(6, 14).Count);
        Assert.Equal(1, calendar.HourOfDay(14).Count);
        Assert.Equal(1, calendar.Cell(0, 9).Count);
        Assert.Equal(1, calendar.HourOfDay(9).Count);
    }
}
