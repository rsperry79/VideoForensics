using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Formatting;
using VideoForensics.Ui.Shared.Layout.Simple;

namespace VideoForensics.Ui.Shared.Tests;

public class SimpleHomeTests : SimpleModeLayoutTestBase
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("TestZone", TimeSpan.FromHours(-5), "Test", "Test");

    // 15:00 local on Wednesday 7 October 2026.
    private static readonly DateTime NowUtc = new(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc);

    private static readonly Guid FrontId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly Mock<IEventRepository> _events = new();
    private readonly Mock<IMediaItemRepository> _media = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IJammingRepository> _jamming = new();
    private readonly Mock<IMediaContentUrlProvider> _urls = new();

    private List<Event> _eventData = new();
    private List<MediaItem> _mediaData = new();
    private List<JammingIncidentRecord> _jammingData = new();

    public SimpleHomeTests()
    {
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { Device(FrontId, "front door camera") });
        _events.Setup(e => e.ListByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _eventData);
        _media.Setup(m => m.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _mediaData);
        _jamming.Setup(j => j.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _jammingData);
        _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)ids.ToDictionary(i => i, i => "https://server/content/video"));

        Services.AddScoped(_ => _events.Object);
        Services.AddScoped(_ => _media.Object);
        Services.AddScoped(_ => _devices.Object);
        Services.AddScoped(_ => _jamming.Object);
        Services.AddScoped(_ => new SimpleHomeBuilder(new FakeTimeProvider(NowUtc), _urls.Object, Zone));
    }

    private static Device Device(Guid id, string name) => new()
    {
        Id = id, LocationId = Guid.NewGuid(), ProviderDeviceId = "ring-dev-1", Name = name, Type = "ring_doorbell"
    };

    private static Event Evt(string type, DateTime occurredUtc) => new()
    {
        Id = Guid.NewGuid(), DeviceId = FrontId, ProviderEventId = "ring-evt-" + Guid.NewGuid(), EventType = type, OccurredAtUtc = occurredUtc
    };

    private static MediaItem Media(DateTime recordedUtc, string format = "video/mp4", bool purged = false) => new()
    {
        Id = Guid.NewGuid(), DeviceId = FrontId, FileName = "clip.mp4", FilePath = @"C:\data\clip.mp4", MediaFormat = format,
        RecordedAtUtc = recordedUtc, DownloadedAtUtc = recordedUtc, Sha256Hash = new string('a', 64), IsPurged = purged
    };

    private static JammingIncidentRecord Jam(DateTime startUtc, TimeSpan length) => new()
    {
        Id = Guid.NewGuid(), DeviceId = FrontId, StartUtc = startUtc, EndUtc = startUtc + length, AverageDegradationDb = 22.5,
        Confidence = JammingConfidenceLevel.High
    };

    private IRenderedComponent<SimpleHome> RenderLoaded()
    {
        var component = Render<SimpleHome>();
        component.WaitForAssertion(() => Assert.Empty(component.FindAll(".simple-home-loading")));
        return component;
    }

    [Fact]
    public void SimpleHome_WhileLoading_ShowsLoadingState()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<Device>>();
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).Returns(gate.Task);

        var component = Render<SimpleHome>();

        Assert.NotEmpty(component.FindAll(".simple-home-loading"));
        Assert.Empty(component.FindAll(".simple-day"));

        gate.SetResult(new List<Device>());
        component.WaitForAssertion(() => Assert.Empty(component.FindAll(".simple-home-loading")));
    }

    [Fact]
    public void SimpleHome_Loaded_ShowsHeading()
    {
        var component = RenderLoaded();

        Assert.Equal("What happened this week", component.Find("h1.simple-home-heading").TextContent.Trim());
        Assert.Contains("Your evidence", component.Find("h2.simple-evidence-heading").TextContent);
    }

    [Fact]
    public void SimpleHome_WithData_ShowsDayHeadingsAndEntries()
    {
        _eventData = new List<Event> { Evt("motion", NowUtc.AddMinutes(-25)), Evt("person", NowUtc.AddDays(-1).AddMinutes(-5)) };

        var component = RenderLoaded();

        var headings = component.FindAll(".simple-day-heading").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Today", "Yesterday" }, headings);
        var entries = component.FindAll(".simple-entry");
        Assert.Equal(2, entries.Count);
        Assert.Contains("2:35 PM", entries[0].QuerySelector(".simple-entry-time")!.TextContent);
        Assert.Contains("detected motion", entries[0].QuerySelector(".simple-entry-text")!.TextContent);
        Assert.Contains("detected a person", entries[1].QuerySelector(".simple-entry-text")!.TextContent);
    }

    [Fact]
    public void SimpleHome_BlockedEntry_CarriesBlockedClass()
    {
        _eventData = new List<Event> { Evt("motion", NowUtc.AddMinutes(-25)) };
        _jammingData = new List<JammingIncidentRecord> { Jam(NowUtc.AddHours(-2), TimeSpan.FromMinutes(10)) };

        var component = RenderLoaded();

        var blocked = component.FindAll(".simple-entry-blocked");
        Assert.Single(blocked);
        Assert.Contains("was blocked for 10 minutes", blocked[0].TextContent);
        Assert.Single(component.FindAll(".simple-entry:not(.simple-entry-blocked)"));
    }

    [Fact]
    public void SimpleHome_NoData_ShowsFriendlyEmptyStates()
    {
        var component = RenderLoaded();

        Assert.Contains("Nothing has been recorded in the last 7 days.", component.Find(".simple-timeline-empty").TextContent);
        Assert.Contains("No evidence yet.", component.Find(".simple-evidence-empty").TextContent);
        Assert.Empty(component.FindAll(".simple-more-note"));
    }

    [Fact]
    public void SimpleHome_MoreThanCap_ShowsOlderActivityNote()
    {
        _eventData = Enumerable.Range(0, SimpleHomeBuilder.MaxTimelineEntries + 1)
            .Select(i => Evt("motion", NowUtc.AddMinutes(-1 - i))).ToList();

        var component = RenderLoaded();

        Assert.Contains("Older activity is not shown here.", component.Find(".simple-more-note").TextContent);
    }

    [Fact]
    public void SimpleHome_UnavailableEvidence_ShowsTextAndNoLink()
    {
        _mediaData = new List<MediaItem> { Media(NowUtc.AddHours(-1), purged: true) };

        var component = RenderLoaded();

        var item = component.Find(".simple-evidence-item");
        Assert.Contains("No longer available", item.TextContent);
        Assert.Empty(item.QuerySelectorAll("a"));
    }

    [Fact]
    public void SimpleHome_AvailableEvidence_ShowsLabelTypeAndSafeLink()
    {
        _mediaData = new List<MediaItem> { Media(NowUtc.AddHours(-1)), Media(NowUtc.AddHours(-2), format: "image/jpeg") };

        var component = RenderLoaded();

        var items = component.FindAll(".simple-evidence-item");
        Assert.Equal(2, items.Count);
        Assert.Contains("Front door camera - 2:00 PM", items[0].TextContent);
        Assert.Contains("Video", items[0].TextContent);
        Assert.Contains("Snapshot", items[1].TextContent);
        var link = items[0].QuerySelector("a")!;
        Assert.Equal("https://server/content/video", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        Assert.Contains("Open", link.TextContent);
    }

    [Fact]
    public void SimpleHome_RepositoryThrows_ShowsFriendlyErrorWithoutExceptionText()
    {
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("SECRET-DB-FAILURE at table X"));

        var component = RenderLoaded();

        var error = component.Find(".simple-home-error");
        Assert.Contains("We couldn't load your activity. Please try again later.", error.TextContent);
        Assert.NotNull(error.QuerySelector("button.simple-home-retry"));
        Assert.DoesNotContain("SECRET-DB-FAILURE", component.Markup);
        Assert.DoesNotContain("InvalidOperationException", component.Markup);
        Assert.Empty(component.FindAll(".simple-day"));
    }

    [Fact]
    public void SimpleHome_RetryAfterError_ReloadsAndShowsData()
    {
        _devices.SetupSequence(d => d.ListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(new List<Device> { Device(FrontId, "front door camera") });
        _eventData = new List<Event> { Evt("motion", NowUtc.AddMinutes(-25)) };

        var component = RenderLoaded();
        Assert.NotEmpty(component.FindAll(".simple-home-error"));

        component.Find("button.simple-home-retry").Click();

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindAll(".simple-home-error"));
            Assert.Single(component.FindAll(".simple-entry"));
        });
    }

    [Fact]
    public async Task SimpleHome_Disposed_CancelsInFlightLoad()
    {
        CancellationToken captured = default;
        var gate = new TaskCompletionSource<IReadOnlyList<Device>>();
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => { captured = ct; return gate.Task; });

        var component = Render<SimpleHome>();
        component.WaitForAssertion(() => Assert.True(captured.CanBeCanceled));

        await DisposeComponentsAsync();

        Assert.True(captured.IsCancellationRequested);
    }

    [Fact]
    public void SimpleHome_WithAllData_ShowsNoIdentifiersOrHashes()
    {
        _eventData = new List<Event> { Evt("motion", NowUtc.AddMinutes(-25)), Evt("weird_type_9", NowUtc.AddMinutes(-30)) };
        _mediaData = new List<MediaItem> { Media(NowUtc.AddHours(-1)) };
        _jammingData = new List<JammingIncidentRecord> { Jam(NowUtc.AddHours(-2), TimeSpan.FromMinutes(10)) };

        var component = RenderLoaded();

        var text = component.Find(".simple-home").TextContent;
        Assert.DoesNotMatch(new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), text);
        Assert.DoesNotMatch(new Regex("[0-9a-fA-F]{32,}"), text);
        Assert.DoesNotContain("weird_type_9", text);
        Assert.DoesNotContain("ring", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimpleHome_NoJammingRepositoryRegistered_ShowsTimelineAndMutedNote()
    {
        // Remote-client mode: the server-only jamming repository is not in DI.
        foreach (var descriptor in Services.Where(d => d.ServiceType == typeof(IJammingRepository)).ToList())
            Services.Remove(descriptor);
        _eventData = new List<Event> { Evt("motion", NowUtc.AddMinutes(-25)) };

        var component = RenderLoaded();

        Assert.Empty(component.FindAll(".simple-home-error"));
        Assert.Single(component.FindAll(".simple-entry"));
        Assert.Contains("Camera-blocked activity can't be shown right now.", component.Find(".simple-blocked-unavailable").TextContent);
    }

    [Fact]
    public void SimpleHome_JammingRepositoryHealthy_ShowsNoNote()
    {
        _eventData = new List<Event> { Evt("motion", NowUtc.AddMinutes(-25)) };

        var component = RenderLoaded();

        Assert.Empty(component.FindAll(".simple-blocked-unavailable"));
    }
}