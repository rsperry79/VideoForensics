using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Formatting;
using Xunit;

namespace VideoForensics.Ui.Shared.Tests;

/// <summary>The Simple home shows timeline times in the signed-in user's zone, not the zone the builder was created with.</summary>
public class SimpleHomeBuilderZoneTests
{
    // Fixed UTC-5 zone with no DST so results never depend on the machine zone.
    private static readonly TimeZoneInfo UserZone = TimeZoneInfo.CreateCustomTimeZone("UserZone", TimeSpan.FromHours(-5), "User", "User");

    private static readonly DateTime NowUtc = new(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task ForZone_TimelineTimeTextUsesThatZone()
    {
        var events = new Mock<IEventRepository>();
        events.Setup(e => e.ListByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>
            {
                new() { Id = Guid.NewGuid(), DeviceId = DeviceId, ProviderEventId = "evt-1", EventType = "motion", OccurredAtUtc = new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc) }
            });
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { new() { Id = DeviceId, LocationId = Guid.NewGuid(), ProviderDeviceId = "ring-dev-1", Name = "front door", Type = "ring_doorbell" } });
        var media = new Mock<IMediaItemRepository>();
        media.Setup(m => m.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem>());
        var jamming = new Mock<IJammingRepository>();
        jamming.Setup(j => j.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JammingIncidentRecord>());
        var urls = new Mock<IMediaContentUrlProvider>();

        // Built for UTC (the server's default in this scenario); the user's zone is applied per call.
        var builder = new SimpleHomeBuilder(new FakeTimeProvider(NowUtc), urls.Object, TestLocalizer.Create(), TimeZoneInfo.Utc);

        var model = await builder.ForZone(UserZone).BuildAsync(events.Object, media.Object, devices.Object, jamming.Object, TimeSpan.FromDays(7), CancellationToken.None);

        var entry = Assert.Single(model.Days.SelectMany(d => d.Entries));
        Assert.Equal("2:00 PM", entry.TimeText);
    }
}
