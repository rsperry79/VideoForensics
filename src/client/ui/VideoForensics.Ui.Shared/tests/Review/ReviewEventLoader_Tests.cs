using Moq;

using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review event loader: excluded event types, paging, and the device cap.</summary>
public class ReviewEventLoader_Tests
{
    private static readonly DateTime From = new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(1);

    /// <summary>What the repository double was last asked for.</summary>
    private sealed class Capture
    {
        public List<Guid> Devices { get; set; } = [];
        public IReadOnlyCollection<string>? Excluded { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    private static Mock<IEventRepository> Repository(Capture capture, EventPage? page = null)
    {
        var repository = new Mock<IEventRepository>();
        _ = repository
            .Setup(r => r.ListPageByDevicesAndDateRangeAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, DateTime, DateTime, IReadOnlyCollection<string>?, int, int, CancellationToken>(
                (devices, _, _, excluded, skip, take, _) =>
                {
                    capture.Devices = devices.ToList();
                    capture.Excluded = excluded;
                    capture.Skip = skip;
                    capture.Take = take;
                })
            .ReturnsAsync(page ?? new EventPage([], 0));
        return repository;
    }

    [Fact]
    public async Task ReviewEventLoader_Load_PassesTheExcludedEventTypes()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);

        // Act
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, 0, 100, CancellationToken.None);

        // Assert: stills are written as "snapshot" events and must not show in the events list
        Assert.Equal(new[] { "snapshot" }, capture.Excluded);
    }

    [Fact]
    public async Task ReviewEventLoader_Load_PassesSkipAndTakeToTheRepository()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);

        // Act
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, 200, 100, CancellationToken.None);

        // Assert
        Assert.Equal(200, capture.Skip);
        Assert.Equal(100, capture.Take);
    }

    [Fact]
    public async Task ReviewEventLoader_Load_ReturnsThePageFromTheRepository()
    {
        // Arrange
        var evt = new Event { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), ProviderEventId = "p1", EventType = "motion", OccurredAtUtc = From };
        var loader = new ReviewEventLoader(Repository(new Capture(), new EventPage([evt], 7)).Object);

        // Act
        EventPage page = await loader.LoadAsync([evt.DeviceId], From, To, 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(new[] { evt.Id }, page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task ReviewEventLoader_MoreThan64Devices_TruncatesToTheFirst64()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);
        List<Guid> devices = Enumerable.Range(0, 70).Select(_ => Guid.NewGuid()).ToList();

        // Act
        _ = await loader.LoadAsync(devices, From, To, 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(64, capture.Devices.Count);
        Assert.Equal(devices.Take(64), capture.Devices);
    }

    [Fact]
    public async Task ReviewEventLoader_DuplicateDevices_AreSentOnce()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);
        Guid device = Guid.NewGuid();

        // Act
        _ = await loader.LoadAsync([device, device], From, To, 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { device }, capture.Devices);
    }
}
