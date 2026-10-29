using Moq;

using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The case view the review page shows: entering a case loads its pinned items, and exiting clears them.</summary>
public class ReviewCaseMode_Tests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<IMediaItemRepository> _media = new();
    private readonly Mock<IEventRepository> _events = new();
    private readonly Dictionary<Guid, MediaItem> _mediaById = [];
    private readonly Dictionary<Guid, Event> _eventsById = [];

    public ReviewCaseMode_Tests()
    {
        _ = _media
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<MediaItem?>(_mediaById.GetValueOrDefault(id)));
        _ = _events
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Event?>(_eventsById.GetValueOrDefault(id)));
    }

    private ReviewCaseMode Create()
    {
        return new ReviewCaseMode(_cases.Object, _media.Object, _events.Object);
    }

    private MediaItem AddMedia(Guid device, DateTime recordedUtc, double durationSeconds)
    {
        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = device,
            FileName = "clip.mp4",
            FilePath = "clip.mp4",
            MediaFormat = "mp4",
            RecordedAtUtc = recordedUtc,
            DurationSeconds = durationSeconds,
            Sha256Hash = "hash",
        };
        _mediaById[item.Id] = item;
        return item;
    }

    private Event AddEvent(Guid device, DateTime occurredUtc)
    {
        var evt = new Event { Id = Guid.NewGuid(), DeviceId = device, ProviderEventId = Guid.NewGuid().ToString(), EventType = "motion", OccurredAtUtc = occurredUtc };
        _eventsById[evt.Id] = evt;
        return evt;
    }

    private static CaseItem MediaPin(Guid caseId, Guid mediaId, DateTime addedUtc)
    {
        return new CaseItem { Id = Guid.NewGuid(), CaseId = caseId, Kind = CaseItemKind.Media, MediaItemId = mediaId, Reason = "r", AddedBy = "op", AddedAtUtc = addedUtc };
    }

    private static CaseItem EventPin(Guid caseId, Guid eventId, DateTime addedUtc)
    {
        return new CaseItem { Id = Guid.NewGuid(), CaseId = caseId, Kind = CaseItemKind.Event, EventId = eventId, Reason = "r", AddedBy = "op", AddedAtUtc = addedUtc };
    }

    private Guid SetCase(string title, IReadOnlyList<CaseItem> pins)
    {
        Guid caseId = Guid.NewGuid();
        _ = _cases
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicCase { Id = caseId, CaseNumber = "CASE-1", Title = title, CreatedBy = "op" });
        _ = _cases
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);
        return caseId;
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_LoadsPinnedItemsAndTheCaseName()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem clip = AddMedia(device, Now.AddHours(-3), 60);
        Event evt = AddEvent(device, Now.AddHours(-2));
        Guid caseId = SetCase("Driveway", [MediaPin(Guid.Empty, clip.Id, Now), EventPin(Guid.Empty, evt.Id, Now)]);
        ReviewCaseMode mode = Create();

        // Act
        bool entered = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.True(entered);
        Assert.True(mode.Active);
        Assert.Equal(caseId, mode.CaseId);
        Assert.Equal("Driveway", mode.CaseName);
        Assert.Equal([clip.Id], mode.MediaItems.Select(m => m.Id));
        Assert.Contains(clip.Id, mode.MediaItemIds);
        Assert.Equal([evt.Id], mode.Events.Select(e => e.Id));
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_DerivesDevicesInFirstSeenOrder()
    {
        // Arrange
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        MediaItem a = AddMedia(first, Now.AddHours(-3), 60);
        MediaItem b = AddMedia(second, Now.AddHours(-2), 60);
        MediaItem c = AddMedia(first, Now.AddHours(-1), 60);
        Guid caseId = SetCase("Case", [MediaPin(Guid.Empty, a.Id, Now), MediaPin(Guid.Empty, b.Id, Now), MediaPin(Guid.Empty, c.Id, Now)]);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal([first, second], mode.DeviceIds);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_PadsTheRangeByOneMinuteEachSide()
    {
        // Arrange: the earliest pinned start is 3 hours back; the latest end is 1 hour back plus 120 seconds
        Guid device = Guid.NewGuid();
        MediaItem early = AddMedia(device, Now.AddHours(-3), 60);
        MediaItem late = AddMedia(device, Now.AddHours(-1), 120);
        Guid caseId = SetCase("Case", [MediaPin(Guid.Empty, early.Id, Now), MediaPin(Guid.Empty, late.Id, Now)]);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal(Now.AddHours(-3).AddMinutes(-1), mode.FromUtc);
        Assert.Equal(Now.AddHours(-1).AddSeconds(120).AddMinutes(1), mode.ToUtc);
        Assert.Equal(Now.AddHours(-3), mode.EarliestItemUtc);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_MediaWithoutDurationEndsAtItsStart()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem clip = AddMedia(device, Now.AddHours(-2), 0);
        clip.DurationSeconds = null;
        Guid caseId = SetCase("Case", [MediaPin(Guid.Empty, clip.Id, Now)]);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal(Now.AddHours(-2).AddMinutes(1), mode.ToUtc);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_EventsAreNewestFirst()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        Event older = AddEvent(device, Now.AddHours(-5));
        Event newer = AddEvent(device, Now.AddHours(-1));
        Event middle = AddEvent(device, Now.AddHours(-3));
        Guid caseId = SetCase("Case", [EventPin(Guid.Empty, older.Id, Now), EventPin(Guid.Empty, newer.Id, Now), EventPin(Guid.Empty, middle.Id, Now)]);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal([newer.Id, middle.Id, older.Id], mode.Events.Select(e => e.Id));
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_SkipsPinsWhoseItemIsGone()
    {
        // Arrange: the pinned media row no longer exists (for example, purged)
        Guid device = Guid.NewGuid();
        MediaItem kept = AddMedia(device, Now.AddHours(-1), 60);
        Guid caseId = SetCase("Case", [MediaPin(Guid.Empty, Guid.NewGuid(), Now), MediaPin(Guid.Empty, kept.Id, Now)]);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal([kept.Id], mode.MediaItems.Select(m => m.Id));
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_UnknownCase_ReturnsFalseAndStaysInactive()
    {
        // Arrange
        Guid unknown = Guid.NewGuid();
        _ = _cases.Setup(r => r.GetAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((ForensicCase?)null);
        ReviewCaseMode mode = Create();

        // Act
        bool entered = await mode.EnterAsync(unknown, CancellationToken.None);

        // Assert
        Assert.False(entered);
        Assert.False(mode.Active);
        Assert.Null(mode.CaseId);
        Assert.Empty(mode.MediaItems);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_EmptyCase_EntersWithEmptyLists()
    {
        // Arrange
        Guid caseId = SetCase("Empty", []);
        ReviewCaseMode mode = Create();

        // Act
        bool entered = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.True(entered);
        Assert.True(mode.Active);
        Assert.Empty(mode.MediaItems);
        Assert.Empty(mode.Events);
        Assert.Empty(mode.DeviceIds);
        Assert.Null(mode.FromUtc);
        Assert.Null(mode.ToUtc);
        Assert.Null(mode.EarliestItemUtc);
        Assert.False(mode.Truncated);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_MoreThanTheCap_KeepsTheNewestPinsAndSetsTruncated()
    {
        // Arrange: pins are added one minute apart, so pin 0 is the oldest
        Guid device = Guid.NewGuid();
        var pins = new List<CaseItem>();
        Guid caseId = Guid.NewGuid();
        for (int i = 0; i <= ReviewCaseMode.MaxItemsPerKind; i++)
        {
            MediaItem clip = AddMedia(device, Now.AddMinutes(i), 10);
            pins.Add(MediaPin(caseId, clip.Id, Now.AddMinutes(i)));
        }

        _ = _cases
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicCase { Id = caseId, CaseNumber = "CASE-1", Title = "Big", CreatedBy = "op" });
        _ = _cases
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal(ReviewCaseMode.MaxItemsPerKind, mode.MediaItems.Count);
        Assert.True(mode.Truncated);
        Assert.DoesNotContain(pins[0].MediaItemId!.Value, mode.MediaItemIds);
        Assert.Contains(pins[^1].MediaItemId!.Value, mode.MediaItemIds);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_ExactlyTheCap_IsNotTruncated()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();
        var pins = new List<CaseItem>();
        for (int i = 0; i < ReviewCaseMode.MaxItemsPerKind; i++)
        {
            MediaItem clip = AddMedia(device, Now.AddMinutes(i), 10);
            pins.Add(MediaPin(caseId, clip.Id, Now.AddMinutes(i)));
        }

        _ = _cases
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicCase { Id = caseId, CaseNumber = "CASE-1", Title = "Full", CreatedBy = "op" });
        _ = _cases
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);
        ReviewCaseMode mode = Create();

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal(ReviewCaseMode.MaxItemsPerKind, mode.MediaItems.Count);
        Assert.False(mode.Truncated);
    }

    [Fact]
    public async Task ReviewCaseMode_Exit_ClearsTheCaseAndRaisesOnChange()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem clip = AddMedia(device, Now.AddHours(-1), 60);
        Guid caseId = SetCase("Case", [MediaPin(Guid.Empty, clip.Id, Now)]);
        ReviewCaseMode mode = Create();
        _ = await mode.EnterAsync(caseId, CancellationToken.None);
        int changes = 0;
        mode.OnChange += () => changes++;

        // Act
        mode.Exit();

        // Assert
        Assert.False(mode.Active);
        Assert.Null(mode.CaseId);
        Assert.Null(mode.CaseName);
        Assert.Empty(mode.MediaItems);
        Assert.Empty(mode.MediaItemIds);
        Assert.Empty(mode.DeviceIds);
        Assert.Null(mode.FromUtc);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsync_RaisesOnChangeOnce()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem clip = AddMedia(device, Now.AddHours(-1), 60);
        Guid caseId = SetCase("Case", [MediaPin(Guid.Empty, clip.Id, Now)]);
        ReviewCaseMode mode = Create();
        int changes = 0;
        mode.OnChange += () => changes++;

        // Act
        _ = await mode.EnterAsync(caseId, CancellationToken.None);

        // Assert
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsyncTwice_ReplacesTheFirstCase()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem firstClip = AddMedia(device, Now.AddHours(-4), 60);
        MediaItem secondClip = AddMedia(device, Now.AddHours(-1), 60);
        Guid first = SetCase("First", [MediaPin(Guid.Empty, firstClip.Id, Now)]);
        Guid second = SetCase("Second", [MediaPin(Guid.Empty, secondClip.Id, Now)]);
        ReviewCaseMode mode = Create();
        _ = await mode.EnterAsync(first, CancellationToken.None);

        // Act
        _ = await mode.EnterAsync(second, CancellationToken.None);

        // Assert
        Assert.Equal(second, mode.CaseId);
        Assert.Equal("Second", mode.CaseName);
        Assert.Equal([secondClip.Id], mode.MediaItems.Select(m => m.Id));
        Assert.DoesNotContain(firstClip.Id, mode.MediaItemIds);
    }

    [Fact]
    public async Task ReviewCaseMode_EnterAsyncUnknownAfterActive_ExitsTheOldCase()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem clip = AddMedia(device, Now.AddHours(-1), 60);
        Guid known = SetCase("Known", [MediaPin(Guid.Empty, clip.Id, Now)]);
        Guid unknown = Guid.NewGuid();
        _ = _cases.Setup(r => r.GetAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((ForensicCase?)null);
        ReviewCaseMode mode = Create();
        _ = await mode.EnterAsync(known, CancellationToken.None);

        // Act
        bool entered = await mode.EnterAsync(unknown, CancellationToken.None);

        // Assert
        Assert.False(entered);
        Assert.False(mode.Active);
        Assert.Empty(mode.MediaItems);
    }
}
