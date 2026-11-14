using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using Moq;

using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

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
        public IReadOnlyCollection<string>? Included { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    private static Mock<IEventRepository> Repository(Capture capture, EventPageDto? page = null)
    {
        var repository = new Mock<IEventRepository>();
        _ = repository
            .Setup(r => r.ListPageByDevicesAndDateRangeAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, DateTime, DateTime, IReadOnlyCollection<string>?, IReadOnlyCollection<string>?, int, int, CancellationToken>(
                (devices, _, _, excluded, included, skip, take, _) =>
                {
                    capture.Devices = devices.ToList();
                    capture.Excluded = excluded;
                    capture.Included = included;
                    capture.Skip = skip;
                    capture.Take = take;
                })
            .ReturnsAsync(page ?? new EventPageDto([], 0));
        return repository;
    }

    [Fact]
    public async Task ReviewEventLoader_Load_PassesTheExcludedEventTypes()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);

        // Act
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, null, 0, 100, CancellationToken.None);

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
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, null, 200, 100, CancellationToken.None);

        // Assert
        Assert.Equal(200, capture.Skip);
        Assert.Equal(100, capture.Take);
    }

    [Fact]
    public async Task ReviewEventLoader_Load_ReturnsThePageFromTheRepository()
    {
        // Arrange
        var evt = new Event { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), ProviderEventId = "p1", EventType = "motion", OccurredAtUtc = From }.ToDto();
        var loader = new ReviewEventLoader(Repository(new Capture(), new EventPageDto([evt], 7)).Object);

        // Act
        EventPageDto page = await loader.LoadAsync([evt.DeviceId], From, To, null, 0, 100, CancellationToken.None);

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
        var devices = Enumerable.Range(0, 70).Select(_ => Guid.NewGuid()).ToList();

        // Act
        _ = await loader.LoadAsync(devices, From, To, null, 0, 100, CancellationToken.None);

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
        var device = Guid.NewGuid();

        // Act
        _ = await loader.LoadAsync([device, device], From, To, null, 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { device }, capture.Devices);
    }

    private static ReviewCaseMode NoCase()
    {
        return new ReviewCaseMode(new Mock<ICaseRepository>().Object, new Mock<IMediaItemRepository>().Object, new Mock<IEventRepository>().Object);
    }

    private static EventDto Evt(Guid device, DateTime occurred)
    {
        return new Event { Id = Guid.NewGuid(), DeviceId = device, ProviderEventId = Guid.NewGuid().ToString(), EventType = "motion", OccurredAtUtc = occurred }.ToDto();
    }

    [Fact]
    public async Task ReviewEventLoader_CaseMode_PagesInMemoryNewestFirstWithTheMatchingTotal()
    {
        // Arrange
        var device = Guid.NewGuid();
        EventDto a = Evt(device, From.AddHours(1));
        EventDto b = Evt(device, From.AddHours(2));
        EventDto c = Evt(device, From.AddHours(3));
        ReviewCaseMode caseMode = await CaseModeFakes.ActiveAsync([], [a, b, c]);
        var loader = new ReviewEventLoader(Repository(new Capture()).Object, caseMode);

        // Act
        EventPageDto page = await loader.LoadAsync([device], From, To, null, 1, 1, CancellationToken.None);

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.Equal([b.Id], page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task ReviewEventLoader_CaseMode_RestrictsToTheRequestedDevicesAndRange()
    {
        // Arrange
        var device = Guid.NewGuid();
        var other = Guid.NewGuid();
        EventDto inside = Evt(device, From.AddHours(1));
        EventDto wrongDevice = Evt(other, From.AddHours(1));
        EventDto outsideRange = Evt(device, To.AddDays(1));
        ReviewCaseMode caseMode = await CaseModeFakes.ActiveAsync([], [inside, wrongDevice, outsideRange]);
        var loader = new ReviewEventLoader(Repository(new Capture()).Object, caseMode);

        // Act
        EventPageDto page = await loader.LoadAsync([device], From, To, null, 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(1, page.TotalCount);
        Assert.Equal([inside.Id], page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task ReviewEventLoader_CaseMode_DoesNotQueryTheRepository()
    {
        // Arrange
        var device = Guid.NewGuid();
        ReviewCaseMode caseMode = await CaseModeFakes.ActiveAsync([], [Evt(device, From.AddHours(1))]);
        Mock<IEventRepository> repository = Repository(new Capture());
        var loader = new ReviewEventLoader(repository.Object, caseMode);

        // Act
        _ = await loader.LoadAsync([device], From, To, null, 0, 100, CancellationToken.None);

        // Assert
        repository.Verify(r => r.ListPageByDevicesAndDateRangeAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewEventLoader_InactiveCaseMode_QueriesTheRepositoryAsBefore()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object, NoCase());

        // Act
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, null, 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "snapshot" }, capture.Excluded);
    }

    [Fact]
    public async Task ReviewEventLoader_Load_EventTypesGiven_PassesThemAsTheIncludeList()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);

        // Act
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, ["motion"], 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "motion" }, capture.Included);
    }

    [Fact]
    public async Task ReviewEventLoader_Load_NoEventType_PassesNoIncludeList()
    {
        // Arrange
        var capture = new Capture();
        var loader = new ReviewEventLoader(Repository(capture).Object);

        // Act
        _ = await loader.LoadAsync([Guid.NewGuid()], From, To, null, 0, 100, CancellationToken.None);

        // Assert
        Assert.Null(capture.Included);
    }

    [Fact]
    public async Task ReviewEventLoader_CaseMode_EventTypeGiven_ReturnsOnlyThatType()
    {
        // Arrange
        var device = Guid.NewGuid();
        EventDto motion = Evt(device, From.AddHours(1));
        motion = motion with { EventType = "motion" };
        EventDto ding = Evt(device, From.AddHours(2));
        ding = ding with { EventType = "ding" };
        ReviewCaseMode caseMode = await CaseModeFakes.ActiveAsync([], [motion, ding]);
        var loader = new ReviewEventLoader(Repository(new Capture()).Object, caseMode);

        // Act
        EventPageDto page = await loader.LoadAsync([device], From, To, ["motion"], 0, 100, CancellationToken.None);

        // Assert
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(motion.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task ReviewEventLoader_Load_EmptyEventTypes_ReturnsAnEmptyPageWithoutQuerying()
    {
        // Arrange
        Mock<IEventRepository> repository = Repository(new Capture());
        var loader = new ReviewEventLoader(repository.Object);

        // Act
        EventPageDto page = await loader.LoadAsync([Guid.NewGuid()], From, To, [], 0, 100, CancellationToken.None);

        // Assert
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        repository.Verify(r => r.ListPageByDevicesAndDateRangeAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
