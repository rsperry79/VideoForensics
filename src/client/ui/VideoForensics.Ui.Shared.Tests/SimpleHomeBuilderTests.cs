using System.Text.RegularExpressions;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Formatting;

namespace VideoForensics.Ui.Shared.Tests;

public class SimpleHomeBuilderTests
{
    // Fixed UTC-5 zone with no DST so results never depend on the machine zone.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("TestZone", TimeSpan.FromHours(-5), "Test", "Test");

    // 15:00 local on Wednesday 7 October 2026.
    private static readonly DateTime NowUtc = new(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc);

    private static readonly Guid FrontId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly Mock<IEventRepository> _events = new();
    private readonly Mock<IMediaItemRepository> _media = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IJammingRepository> _jamming = new();
    private readonly Mock<IMediaContentUrlProvider> _urls = new();

    public SimpleHomeBuilderTests()
    {
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { Device(FrontId, "front door camera") });
        _events.Setup(e => e.ListByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());
        _media.Setup(m => m.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem>());
        _jamming.Setup(j => j.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JammingIncidentRecord>());
        _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)ids.ToDictionary(i => i, i => "https://server/content/video"));
    }

    private static Device Device(Guid id, string name) => new()
    {
        Id = id, LocationId = Guid.NewGuid(), ProviderDeviceId = "ring-dev-1", Name = name, Type = "ring_doorbell"
    };

    private static Event Evt(string type, DateTime occurredUtc, Guid? deviceId = null) => new()
    {
        Id = Guid.NewGuid(), DeviceId = deviceId ?? FrontId, ProviderEventId = "ring-evt-" + Guid.NewGuid(), EventType = type, OccurredAtUtc = occurredUtc
    };

    private static MediaItem Media(DateTime recordedUtc, string format = "video/mp4", bool purged = false, Guid? deviceId = null) => new()
    {
        Id = Guid.NewGuid(), DeviceId = deviceId ?? FrontId, FileName = "clip.mp4", FilePath = @"C:\data\clip.mp4", MediaFormat = format,
        RecordedAtUtc = recordedUtc, DownloadedAtUtc = recordedUtc, Sha256Hash = new string('a', 64), IsPurged = purged
    };

    private static JammingIncidentRecord Jam(DateTime startUtc, TimeSpan length, Guid? deviceId = null) => new()
    {
        Id = Guid.NewGuid(), DeviceId = deviceId ?? FrontId, StartUtc = startUtc, EndUtc = startUtc + length, AverageDegradationDb = 22.5,
        Confidence = JammingConfidenceLevel.High
    };

    private void SetEvents(params Event[] items) =>
        _events.Setup(e => e.ListByDeviceAndDateRangeAsync(FrontId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());

    private void SetMedia(params MediaItem[] items) =>
        _media.Setup(m => m.GetByDeviceAndDateRangeAsync(FrontId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());

    private void SetJamming(params JammingIncidentRecord[] items) =>
        _jamming.Setup(j => j.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());

    private Task<SimpleHomeModel> BuildAsync(TimeSpan? window = null, CancellationToken ct = default) =>
        new SimpleHomeBuilder(new FakeTimeProvider(NowUtc), _urls.Object, Zone)
            .BuildAsync(_events.Object, _media.Object, _devices.Object, _jamming.Object, window ?? TimeSpan.FromDays(7), ct);

    [Fact]
    public async Task BuildAsync_NoData_ReturnsEmptyModel()
    {
        var model = await BuildAsync();

        Assert.NotNull(model);
        Assert.Empty(model.Days);
        Assert.Empty(model.Evidence);
        Assert.False(model.HasMoreTimeline);
    }

    [Fact]
    public async Task BuildAsync_Window_StartsSevenDaysBeforeNow()
    {
        var model = await BuildAsync();

        Assert.Equal(NowUtc.AddDays(-7), model.WindowStartUtc);
        _events.Verify(e => e.ListByDeviceAndDateRangeAsync(FrontId, NowUtc.AddDays(-7), NowUtc, It.IsAny<CancellationToken>()), Times.Once);
        _jamming.Verify(j => j.ListIncidentsAsync(null, NowUtc.AddDays(-7), NowUtc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_Events_AreOrderedNewestFirstAndGroupedByLocalDayAcrossMidnight()
    {
        // 04:30Z on the 7th is 23:30 local on the 6th (yesterday); 19:35Z is 14:35 local today.
        SetEvents(
            Evt("motion", new DateTime(2026, 10, 7, 4, 30, 0, DateTimeKind.Utc)),
            Evt("person", new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)),
            Evt("package", new DateTime(2026, 10, 7, 6, 0, 0, DateTimeKind.Utc)));

        var model = await BuildAsync();

        Assert.Equal(2, model.Days.Count);
        Assert.Equal(new DateOnly(2026, 10, 7), model.Days[0].Date);
        Assert.Equal(2, model.Days[0].Entries.Count);
        Assert.Equal("2:35 PM", model.Days[0].Entries[0].TimeText);
        Assert.Equal("1:00 AM", model.Days[0].Entries[1].TimeText);
        Assert.Equal(new DateOnly(2026, 10, 6), model.Days[1].Date);
        Assert.Equal("11:30 PM", Assert.Single(model.Days[1].Entries).TimeText);
    }

    [Fact]
    public async Task BuildAsync_Headings_UseTodayYesterdayAndDatedForm()
    {
        SetEvents(
            Evt("motion", new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)),
            Evt("motion", new DateTime(2026, 10, 6, 19, 35, 0, DateTimeKind.Utc)),
            Evt("motion", new DateTime(2026, 10, 5, 19, 35, 0, DateTimeKind.Utc)));

        var model = await BuildAsync();

        Assert.Equal(new[] { "Today", "Yesterday", "Monday 5 October" }, model.Days.Select(d => d.Heading).ToArray());
    }

    [Fact]
    public async Task BuildAsync_EventAndJamming_AreMergedWithKinds()
    {
        SetEvents(Evt("person", new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc)));
        SetJamming(Jam(new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(5)));

        var model = await BuildAsync();

        var entries = Assert.Single(model.Days).Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(SimpleEntryKind.Blocked, entries[0].Kind);
        Assert.Equal(new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc), entries[0].TimeUtc);
        Assert.Equal(SimpleEntryKind.Event, entries[1].Kind);
        Assert.Equal("Your front door camera detected a person.", entries[1].Text);
    }

    [Fact]
    public async Task BuildAsync_Jamming_TextMatchesFormatter()
    {
        var start = new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc);
        SetJamming(Jam(start, TimeSpan.FromMinutes(5)));

        var model = await BuildAsync();

        var entry = Assert.Single(Assert.Single(model.Days).Entries);
        Assert.Equal(EventPlainLanguageFormatter.DescribeJammingIncident("front door camera", start, start.AddMinutes(5)), entry.Text);
        Assert.Equal("Your front door camera was blocked for 5 minutes.", entry.Text);
    }

    [Fact]
    public async Task BuildAsync_MoreThan100Entries_CapsAndFlagsMore()
    {
        SetEvents(Enumerable.Range(0, 150).Select(i => Evt("motion", NowUtc.AddMinutes(-i - 1))).ToArray());

        var model = await BuildAsync();

        Assert.Equal(100, model.Days.Sum(d => d.Entries.Count));
        Assert.True(model.HasMoreTimeline);
        // Newest entries are kept.
        Assert.Equal(NowUtc.AddMinutes(-1), model.Days[0].Entries[0].TimeUtc);
    }

    [Fact]
    public async Task BuildAsync_Exactly100Entries_DoesNotFlagMore()
    {
        SetEvents(Enumerable.Range(0, 100).Select(i => Evt("motion", NowUtc.AddMinutes(-i - 1))).ToArray());

        var model = await BuildAsync();

        Assert.Equal(100, model.Days.Sum(d => d.Entries.Count));
        Assert.False(model.HasMoreTimeline);
    }

    [Fact]
    public async Task BuildAsync_Evidence_ExcludesPurgedAndUsesFriendlyLabelsAndLocalTime()
    {
        var shown = Media(new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc));
        var snap = Media(new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc), "image/jpeg");
        var gone = Media(new DateTime(2026, 10, 7, 17, 0, 0, DateTimeKind.Utc), purged: true);
        SetMedia(snap, gone, shown);

        var model = await BuildAsync();

        Assert.Equal(3, model.Evidence.Count);
        Assert.Equal("Front door camera - 2:35 PM", model.Evidence[0].Label);
        Assert.Equal("Video", model.Evidence[0].TypeText);
        Assert.Equal("2:35 PM", model.Evidence[0].TimeText);
        Assert.True(model.Evidence[0].IsAvailable);
        Assert.NotNull(model.Evidence[0].Url);
        Assert.Equal("Snapshot", model.Evidence[1].TypeText);
        Assert.False(model.Evidence[2].IsAvailable);
        Assert.Null(model.Evidence[2].Url);
    }

    [Fact]
    public async Task BuildAsync_UrlProviderOmitsItem_MarksUnavailable()
    {
        SetMedia(Media(new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)));
        _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var model = await BuildAsync();

        var item = Assert.Single(model.Evidence);
        Assert.False(item.IsAvailable);
        Assert.Null(item.Url);
    }

    [Fact]
    public async Task BuildAsync_UnknownDevice_FallsBackToYourCamera()
    {
        var gone = Guid.NewGuid();
        SetJamming(Jam(new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(30), gone));

        var model = await BuildAsync();

        Assert.Equal("Your camera was blocked for 30 seconds.", Assert.Single(Assert.Single(model.Days).Entries).Text);
    }

    [Fact]
    public async Task BuildAsync_DeviceWithBlankName_UsesYourCameraInEvidenceLabel()
    {
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { Device(FrontId, " ") });
        SetMedia(Media(new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)));

        var model = await BuildAsync();

        Assert.Equal("Your camera - 2:35 PM", Assert.Single(model.Evidence).Label);
    }

    [Fact]
    public async Task BuildAsync_MoreThan50Media_CapsEvidenceNewestFirst()
    {
        SetMedia(Enumerable.Range(0, 60).Select(i => Media(NowUtc.AddMinutes(-i - 1))).ToArray());

        var model = await BuildAsync();

        Assert.Equal(50, model.Evidence.Count);
        Assert.Equal(NowUtc.AddMinutes(-1), model.Evidence[0].TimeUtc);
        Assert.Equal(NowUtc.AddMinutes(-50), model.Evidence[^1].TimeUtc);
    }

    [Fact]
    public async Task BuildAsync_AllStrings_ContainNoTechnicalTerms()
    {
        SetEvents(Evt("motion", new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)));
        SetJamming(Jam(new DateTime(2026, 10, 7, 19, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(5)));
        SetMedia(Media(new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)), Media(new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc), "image/jpeg"));

        var model = await BuildAsync();

        var strings = model.Days.SelectMany(d => new[] { d.Heading }.Concat(d.Entries.SelectMany(e => new[] { e.TimeText, e.Text })))
            .Concat(model.Evidence.SelectMany(e => new[] { e.Label, e.TypeText, e.TimeText, e.Url ?? "" }))
            .ToList();
        Assert.NotEmpty(strings);
        foreach (var s in strings)
        {
            Assert.DoesNotMatch(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", s);
            Assert.DoesNotMatch("[0-9a-fA-F]{64}", s);
            Assert.DoesNotContain("dB", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RSSI", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ring", s.Replace("during", ""), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("clip.mp4", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@"C:\", s, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task BuildAsync_CancellationToken_IsForwardedToEveryDependency()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        SetEvents(Evt("motion", new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)));
        SetMedia(Media(new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)));
        _devices.Setup(d => d.ListAsync(token)).ReturnsAsync(new List<Device> { Device(FrontId, "front door camera") });
        _events.Setup(e => e.ListByDeviceAndDateRangeAsync(FrontId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token)).ReturnsAsync(new List<Event>());
        _media.Setup(m => m.GetByDeviceAndDateRangeAsync(FrontId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token))
            .ReturnsAsync(new List<MediaItem> { Media(new DateTime(2026, 10, 7, 19, 35, 0, DateTimeKind.Utc)) });
        _jamming.Setup(j => j.ListIncidentsAsync(null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), token)).ReturnsAsync(new List<JammingIncidentRecord>());
        _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), token))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyDictionary<Guid, string>)ids.ToDictionary(i => i, _ => "https://server/content/x"));

        var model = await BuildAsync(ct: token);

        Assert.Single(model.Evidence);
        _devices.Verify(d => d.ListAsync(token), Times.Once);
        _events.Verify(e => e.ListByDeviceAndDateRangeAsync(FrontId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token), Times.Once);
        _media.Verify(m => m.GetByDeviceAndDateRangeAsync(FrontId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token), Times.Once);
        _jamming.Verify(j => j.ListIncidentsAsync(null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), token), Times.Once);
        _urls.Verify(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), token), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_AlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildAsync(ct: cts.Token));
    }

    [Fact]
    public async Task BuildAsync_RepositoryThrows_Propagates()
    {
        _events.Setup(e => e.ListByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildAsync());

        Assert.Equal("boom", ex.Message);
    }
}