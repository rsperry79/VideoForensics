using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Client.Ui.Shared.Services.Time;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The review events tab: one merged list of camera and system events, paging, states, row clicks, reloads, refreshes, and disposal.</summary>
public class ReviewEventsTab_Tests : ReviewTestContext
{
    private const string RowSelector = "[data-testid='review-event-row']";

    private static readonly DateTime Base = FixedNowUtc.AddHours(-2);

    private static EventDto EventAt(Guid device, DateTime utc, string type = "motion")
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = device,
            ProviderEventId = Guid.NewGuid().ToString("N"),
            EventType = type,
            OccurredAtUtc = utc,
        }.ToDto();
    }

    /// <summary>
    /// Serves events from one list (given newest first), keeping only the requested cameras and applying skip and take.
    /// The list is read on every call, so a test can change it between calls.
    /// </summary>
    private void ServeFrom(List<EventDto> all)
    {
        EventLoader.Responder = (call, _) =>
        {
            var matching = all.Where(e => call.DeviceIds.Contains(e.DeviceId)).ToList();
            return Task.FromResult(new EventPageDto(matching.Skip(call.Skip).Take(call.Take).ToList(), matching.Count));
        };
    }

    private IRenderedComponent<ReviewEventsTab> RenderTab()
    {
        return Render<ReviewEventsTab>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));
    }

    private static string[] RowIds(IRenderedComponent<ReviewEventsTab> cut)
    {
        return cut.FindAll(RowSelector).Select(row => row.GetAttribute("data-event-id")!).ToArray();
    }

    private static string[] RowKinds(IRenderedComponent<ReviewEventsTab> cut)
    {
        return cut.FindAll(RowSelector).Select(row => row.GetAttribute("data-kind")!).ToArray();
    }

    private static string RowSelectorOfKind(string kind)
    {
        return $"{RowSelector}[data-kind='{kind}']";
    }

    private static JammingIncidentDto JammingAt(Guid device, DateTime startUtc)
    {
        return new JammingIncidentDto(Guid.NewGuid(), device, startUtc, startUtc.AddMinutes(5), 3, 1.5, "High", startUtc, null, "AutoDetected", null);
    }

    private static ReviewAnomaly AnomalyAt(Guid device, DateTime startUtc)
    {
        return new ReviewAnomaly(device, startUtc, startUtc.AddHours(1), SignalDeviationLevel.TwoSigma);
    }

    private static ReviewBitRateFlag FlagAt(Guid device, DateTime startUtc)
    {
        return new ReviewBitRateFlag(Guid.NewGuid(), device, startUtc, startUtc.AddMinutes(5), "BitrateDeviation", null);
    }

    /// <summary>The local clock text the row and the hover text show for a stored UTC instant (no time zone host in tests).</summary>
    private static string LocalText(DateTime utc)
    {
        return new UserTimeZone(TimeZoneInfo.Local).Format(utc, "g");
    }

    /// <summary>A camera event detail with every field the hover shows.</summary>
    private static EventDetailDto DetailWithFields(Guid eventId)
    {
        var detection = new EventDetection
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            PersonDetected = true,
            DetectionType = "human",
            ShortDescription = "Short summary text",
            FullDescription = "Full description text",
            Confidence = 0.5m,
            Anomaly = 0.25m,
            Similarity = 0.75m,
            ModelVersion = "model-v9",
        };
        var zone = new EventDetectionZone { Id = Guid.NewGuid(), EventDetectionId = detection.Id, ZoneId = "zone-1", ZoneName = "Driveway", Confidence = 0.9m };
        var person = new EventDetectedPerson { Id = Guid.NewGuid(), EventId = eventId, ProfileId = "p-1", ProfileName = "Alex", Confidence = 0.8m };
        var alert = new EventSecurityAlert { Id = Guid.NewGuid(), EventId = eventId, Severity = "high", AlertText = "Loitering near door" };
        return new EventDetail(eventId, detection, [], [zone], [person], [alert]).ToDto();
    }

    [Fact]
    public void ReviewEventsTab_SelectedCamera_ShowsItsEventsNewestFirst()
    {
        // Arrange
        AddDevices(2);
        EventDto newest = EventAt(Devices[0].Id, Base.AddMinutes(30));
        EventDto older = EventAt(Devices[0].Id, Base.AddMinutes(10));
        ServeFrom([newest, EventAt(Devices[1].Id, Base.AddMinutes(20)), older]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
        Assert.Equal(new[] { newest.Id.ToString("D"), older.Id.ToString("D") }, RowIds(cut));
        Assert.Equal(new[] { Devices[0].Id }, EventLoader.Calls[0].DeviceIds);
        Assert.Equal(0, EventLoader.Calls[0].Skip);
        Assert.Equal(ReviewEventFilters.PageSize, EventLoader.Calls[0].Take);
    }

    [Fact]
    public void ReviewEventsTab_Row_ShowsTheCameraNameAndTheTime()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        IElement row = cut.Find(RowSelector);
        Assert.Contains(NameOf(Devices[0]), row.TextContent);
        _ = Assert.Single(row.QuerySelectorAll("time"));
    }

    [Fact]
    public void ReviewEventsTab_UnknownCamera_ShowsTheDeviceIdText()
    {
        // Arrange
        var unknown = Guid.NewGuid();
        ServeFrom([EventAt(unknown, Base)]);
        ReviewState.SetCameras([unknown]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        Assert.Contains(unknown.ToString("D"), cut.Find(RowSelector).TextContent);
    }

    [Fact]
    public void ReviewEventsTab_KnownEventType_ShowsItsLocalizedLabel()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base, "ding")]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        Assert.Contains(L["EventType_ding"].Value, cut.Find(RowSelector).TextContent);
    }

    [Fact]
    public void ReviewEventsTab_UnavailableEvent_ShowsNoMediaBadge()
    {
        // Arrange
        AddDevices(1);
        EventDto evt = EventAt(Devices[0].Id, Base);
        evt = evt with { DownloadStatus = EventDownloadStatusDto.Unavailable };
        ServeFrom([evt]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert: the row says the event has no media, so the operator knows it cannot be played
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        Assert.NotEmpty(cut.FindAll("[data-testid='review-event-unavailable']"));
    }

    [Fact]
    public void ReviewEventsTab_MotionRowWithAPerson_ShowsPersonInsteadOfMotion()
    {
        // Arrange
        AddDevices(1);
        EventDto evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        var detection = new EventDetection { Id = Guid.NewGuid(), EventId = evt.Id, DetectionType = "human", PersonDetected = true };
        DetailLoader.DetailResponder = (_, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetailDto>>(
            new Dictionary<Guid, EventDetailDto> { [evt.Id] = new EventDetail(evt.Id, detection, [], [], [], []).ToDto() });
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert: the real detection type replaces the generic motion label once the row's detail is read.
        cut.WaitForAssertion(() => Assert.Contains(L["DetectionType_human"].Value, cut.Find(RowSelector).TextContent));
    }

    [Fact]
    public void ReviewEventsTab_TimelapseRow_ShowsTimeLapse()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base, "timelapse")]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Contains(L["EventType_timelapse"].Value, cut.Find(RowSelector).TextContent));
    }

    [Fact]
    public void ReviewEventsTab_UnmappedEventType_ShowsTheRawTypeString()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base, "unmapped-type")]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        Assert.Contains("unmapped-type", cut.Find(RowSelector).TextContent);
    }

    [Fact]
    public void ReviewEventsTab_EventsAcrossPages_FetchesEveryPageIntoOneList()
    {
        // Arrange
        AddDevices(1);
        ServeFrom(Enumerable.Range(0, 250).Select(i => EventAt(Devices[0].Id, Base.AddMinutes(-i))).ToList());
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert: three pages are read in turn until every row is in the list
        cut.WaitForAssertion(() => Assert.Equal(250, cut.FindAll(RowSelector).Count), TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 0, 100, 200 }, EventLoader.Calls.Select(call => call.Skip).ToArray());
        Assert.All(EventLoader.Calls, call => Assert.Equal(ReviewEventFilters.PageSize, call.Take));
    }

    [Fact]
    public void ReviewEventsTab_Loaded_HasNoLoadMoreButtonAndNoTotalLine()
    {
        // Arrange
        AddDevices(1);
        ServeFrom(Enumerable.Range(0, 150).Select(i => EventAt(Devices[0].Id, Base.AddMinutes(-i))).ToList());
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(150, cut.FindAll(RowSelector).Count), TimeSpan.FromSeconds(5));
        Assert.Empty(cut.FindAll("[data-testid='review-events-load-more']"));
        Assert.Empty(cut.FindAll("[data-testid='review-events-total']"));
    }

    [Fact]
    public void ReviewEventsTab_AllSources_ShowsOneListNewestFirst()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        ServeFrom([EventAt(device, Base.AddMinutes(30))]);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentDto>>([JammingAt(device, Base.AddMinutes(20))]);
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>([AnomalyAt(device, Base.AddMinutes(40))]);
        DetailLoader.BitRateResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewBitRateFlag>>([FlagAt(device, Base.AddMinutes(10))]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll(RowSelector).Count));
        Assert.Equal(new[] { "SignalAnomaly", "CameraEvent", "Jamming", "BitRate" }, RowKinds(cut));
    }

    [Fact]
    public void ReviewEventsTab_SystemRows_ShowTheirTypeLabels()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentDto>>([JammingAt(device, Base.AddMinutes(3))]);
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>([AnomalyAt(device, Base.AddMinutes(2))]);
        DetailLoader.BitRateResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewBitRateFlag>>([FlagAt(device, Base.AddMinutes(1))]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert: each system row carries the label the Detail tab used for its section
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(RowSelector).Count));
        Assert.Contains(L["DetailJammingTitle"].Value, cut.Find(RowSelectorOfKind("Jamming")).TextContent);
        Assert.Contains(L["DetailAnomaliesTitle"].Value, cut.Find(RowSelectorOfKind("SignalAnomaly")).TextContent);
        Assert.Contains(L["DetailBitRateTitle"].Value, cut.Find(RowSelectorOfKind("BitRate")).TextContent);
    }

    [Fact]
    public void ReviewEventsTab_SignalRow_ShowsItsEndTimeAndCameraRowShowsOnlyTheStart()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        ServeFrom([EventAt(device, Base)]);
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>([AnomalyAt(device, Base.AddMinutes(-30))]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
        Assert.Equal(2, cut.Find(RowSelectorOfKind("SignalAnomaly")).QuerySelectorAll("time").Length);
        _ = Assert.Single(cut.Find(RowSelectorOfKind("CameraEvent")).QuerySelectorAll("time"));
    }

    [Fact]
    public void ReviewEventsTab_CameraRowHover_ShowsItsDetailAsPlainLines()
    {
        // Arrange
        AddDevices(1);
        EventDto evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        DetailLoader.DetailResponder = (_, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetailDto>>(
            new Dictionary<Guid, EventDetailDto> { [evt.Id] = DetailWithFields(evt.Id) });
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        string row = $"{RowSelector}[data-event-id='{evt.Id:D}']";
        cut.WaitForAssertion(() => Assert.Contains("Short summary text", cut.Find(row).GetAttribute("title")));
        string title = cut.Find(row).GetAttribute("title")!;
        Assert.Contains("\n", title);
        Assert.Contains("Full description text", title);
        Assert.Contains(L["DetailFieldDetectionType"].Value, title);
        Assert.Contains(L["DetectionType_human"].Value, title);
        Assert.Contains(L["DetailFieldConfidence"].Value, title);
        Assert.Contains(L["DetailFieldAnomaly"].Value, title);
        Assert.Contains(L["DetailFieldSimilarity"].Value, title);
        Assert.Contains("model-v9", title);
        Assert.Contains("Driveway", title);
        Assert.Contains("Alex", title);
        Assert.Contains("Loitering near door", title);
    }

    [Fact]
    public void ReviewEventsTab_JammingRowHover_ShowsConfidenceAndAffectedCount()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentDto>>([JammingAt(device, Base)]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelectorOfKind("Jamming"))));
        string title = cut.Find(RowSelectorOfKind("Jamming")).GetAttribute("title")!;
        Assert.Contains(L["JammingConfidence_High"].Value, title);
        Assert.Contains(string.Format(L["DetailJammingAffected"].Value, 3), title);
    }

    [Fact]
    public void ReviewEventsTab_AnomalyRowHover_ShowsLevelAndRange()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>([AnomalyAt(device, Base)]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelectorOfKind("SignalAnomaly"))));
        string title = cut.Find(RowSelectorOfKind("SignalAnomaly")).GetAttribute("title")!;
        Assert.Contains(L["DetailLevelTwoSigma"].Value, title);
        Assert.Contains(string.Format(L["EventHoverRange"].Value, LocalText(Base), LocalText(Base.AddHours(1))), title);
    }

    [Fact]
    public void ReviewEventsTab_BitRateRowHover_ShowsFlagTypeAndRange()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        DetailLoader.BitRateResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewBitRateFlag>>([FlagAt(device, Base)]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelectorOfKind("BitRate"))));
        string title = cut.Find(RowSelectorOfKind("BitRate")).GetAttribute("title")!;
        Assert.Contains(L["DetailBitRateDeviation"].Value, title);
        Assert.Contains(string.Format(L["EventHoverRange"].Value, LocalText(Base), LocalText(Base.AddMinutes(5))), title);
    }

    [Fact]
    public void ReviewEventsTab_SystemRowClick_SetsTheCameraCursorAndTimelineTab()
    {
        // Arrange
        AddDevices(2);
        DateTime start = Base.AddMinutes(20);
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30))]);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentDto>>([JammingAt(Devices[1].Id, start)]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));

        // Act
        cut.Find(RowSelectorOfKind("Jamming")).Click();

        // Assert
        Assert.Equal(new[] { Devices[1].Id }, ReviewState.CameraIds);
        Assert.Equal(start, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewEventsTab_SystemOnlySelection_DoesNotQueryCameraEvents()
    {
        // Arrange
        AddDevices(1);
        Guid device = Devices[0].Id;
        ReviewState.SetEventTypes([ReviewEventFilters.Jamming]);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentDto>>([JammingAt(device, Base)]);
        ReviewState.SetCameras([device]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert: an empty provider list means no provider events were asked for, so none are queried
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        Assert.Empty(EventLoader.Calls);
        _ = Assert.Single(DetailLoader.JammingCalls);
        Assert.Empty(DetailLoader.AnomalyCalls);
        Assert.Empty(DetailLoader.BitRateCalls);
    }

    [Fact]
    public void ReviewEventsTab_NoEventTypeSelected_QueriesCameraEventsAndEverySystemSource()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        _ = Assert.Single(EventLoader.Calls);
        Assert.Null(EventLoader.Calls[0].EventTypes);
        _ = Assert.Single(DetailLoader.JammingCalls);
        _ = Assert.Single(DetailLoader.AnomalyCalls);
        _ = Assert.Single(DetailLoader.BitRateCalls);
    }

    [Fact]
    public async Task ReviewEventsTab_HealthChange_RefreshesTheSystemSourcesOnly()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
        _ = Assert.Single(DetailLoader.JammingCalls);

        // Act
        HealthSource.Changes.OnNext(new DeviceHealthChangedDto(Devices[0].Id));

        // Assert: the jamming source is read again; the camera events are not
        cut.WaitForAssertion(() => Assert.Equal(2, DetailLoader.JammingCalls.Count), TimeSpan.FromSeconds(5));
        await Task.Delay(1000);
        Assert.Equal(2, DetailLoader.JammingCalls.Count);
        _ = Assert.Single(EventLoader.Calls);
    }

    [Fact]
    public void ReviewEventsTab_SystemSourceFails_KeepsTheCameraRowsAndShowsASystemError()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        DetailLoader.JammingResponder = (_, _) => throw new InvalidOperationException("boom");
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-events-system-error']")));
        Assert.Single(cut.FindAll(RowSelector));
        Assert.Empty(cut.FindAll("[data-testid='review-events-error']"));
    }

    [Fact]
    public void ReviewEventsTab_NoCameraSelected_ShowsTheMessageAndDoesNotLoad()
    {
        // Arrange
        AddDevices(2);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        Assert.Contains(L["EventsNoCameras"].Value, cut.Find("[data-testid='review-events-no-cameras']").TextContent);
        Assert.Empty(EventLoader.Calls);
    }

    [Fact]
    public void ReviewEventsTab_NoEvents_ShowsTheEmptyMessage()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-events-empty']")));
        Assert.Contains(L["EventsEmpty"].Value, cut.Find("[data-testid='review-events-empty']").TextContent);
        Assert.Empty(cut.FindAll(RowSelector));
    }

    [Fact]
    public void ReviewEventsTab_LoadInProgress_ShowsTheLoadingIndicator()
    {
        // Arrange
        AddDevices(1);
        var never = new TaskCompletionSource<EventPageDto>();
        EventLoader.Responder = (_, _) => never.Task;
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-events-loading']")));
        Assert.Contains(L["EventsLoading"].Value, cut.Find("[data-testid='review-events-loading']").TextContent);
    }

    [Fact]
    public void ReviewEventsTab_LoadFails_ShowsTheErrorMessageAndLogsTypeAndMessage()
    {
        // Arrange
        AddDevices(1);
        EventLoader.Responder = (_, _) => throw new InvalidOperationException("boom");
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-events-error']")));
        Assert.Contains(L["EventsLoadFailed"].Value, cut.Find("[data-testid='review-events-error']").TextContent);
        EventsLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("InvalidOperationException", StringComparison.Ordinal) && state.ToString()!.Contains("boom", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReviewEventsTab_RowClick_SelectsTheCameraEventCursorAndTimelineTab()
    {
        // Arrange
        AddDevices(2);
        EventDto target = EventAt(Devices[1].Id, Base.AddMinutes(20), "ding");
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30)), target]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));

        // Act
        cut.Find($"{RowSelector}[data-event-id='{target.Id:D}']").Click();

        // Assert
        Assert.Equal(new[] { Devices[1].Id }, ReviewState.CameraIds);
        Assert.Equal(target.Id, ReviewState.SelectedEventId);
        Assert.Equal(target.OccurredAtUtc, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewEventsTab_RowClick_HighlightsTheSelectedRow()
    {
        // Arrange
        AddDevices(2);
        EventDto target = EventAt(Devices[1].Id, Base.AddMinutes(20));
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30)), target]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));

        // Act
        cut.Find($"{RowSelector}[data-event-id='{target.Id:D}']").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.True(cut.Find($"{RowSelector}[data-event-id='{target.Id:D}']").ClassList.Contains("active")));
        _ = Assert.Single(cut.FindAll($"{RowSelector}.active"));
    }

    [Fact]
    public void ReviewEventsTab_RowClickOnTheOnlySelectedCamera_KeepsTheCameraAndDoesNotReload()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));

        // Act
        cut.Find(RowSelector).Click();

        // Assert
        Assert.Equal(new[] { Devices[0].Id }, ReviewState.CameraIds);
        _ = Assert.Single(EventLoader.Calls);
    }

    [Fact]
    public void ReviewEventsTab_CameraChange_ReloadsFromTheStart()
    {
        // Arrange
        AddDevices(2);
        ServeFrom([EventAt(Devices[0].Id, Base), EventAt(Devices[1].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));

        // Act
        ReviewState.SetCameras([Devices[1].Id]);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count));
        Assert.Equal(new[] { Devices[1].Id }, EventLoader.Calls[1].DeviceIds);
        Assert.Equal(0, EventLoader.Calls[1].Skip);
    }

    [Fact]
    public void ReviewEventsTab_RangeChange_ReloadsWithTheNewRange()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));
        DateTime from = FixedNowUtc.AddDays(-2);

        // Act
        ReviewState.SetRange(from, FixedNowUtc);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count));
        Assert.Equal(from, EventLoader.Calls[1].FromUtc);
    }

    [Fact]
    public async Task ReviewEventsTab_CursorTabAndSelectionChange_DoesNotReload()
    {
        // Arrange
        AddDevices(1);
        EventDto evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));

        // Act
        await cut.InvokeAsync(() =>
        {
            ReviewState.SetCursor(Base.AddMinutes(5));
            ReviewState.SetTab(ReviewTab.Detail);
            ReviewState.SelectEvent(evt.Id);
        });

        // Assert
        _ = Assert.Single(EventLoader.Calls);
    }

    [Fact]
    public async Task ReviewEventsTab_EvidenceBurstForAShownDevice_RequeriesOnceAndRefreshesTheLoadedRows()
    {
        // Arrange
        AddDevices(2);
        var all = new List<EventDto> { EventAt(Devices[0].Id, Base.AddMinutes(10)), EventAt(Devices[0].Id, Base.AddMinutes(5)) };
        ServeFrom(all);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
        EventDto fresh = EventAt(Devices[0].Id, Base.AddMinutes(15));
        all.Insert(0, fresh);

        // Act
        for (int i = 0; i < 3; i++)
        {
            EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        }

        // Assert: one re-query for the burst (a single page, since only two rows exist), and the new row is on top
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Equal(fresh.Id.ToString("D"), RowIds(cut)[0]), TimeSpan.FromSeconds(5));
        await Task.Delay(1000);
        Assert.Equal(2, EventLoader.Calls.Count);
        Assert.Equal(ReviewEventFilters.PageSize, EventLoader.Calls[1].Take);
    }

    [Fact]
    public async Task ReviewEventsTab_EvidenceForAnotherDevice_DoesNotRequery()
    {
        // Arrange
        AddDevices(2);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));

        // Act
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[1].Id));
        await Task.Delay(1000);

        // Assert: a related change still re-queries, so the subscription is live
        _ = Assert.Single(EventLoader.Calls);
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ReviewEventsTab_SlowLoadSupersededByNewerLoad_DiscardsTheStaleResult()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        var slow = new TaskCompletionSource<EventPageDto>();
        EventDto stale = EventAt(Devices[0].Id, Base.AddMinutes(10));
        EventDto fast = EventAt(Devices[1].Id, Base.AddMinutes(5));
        int calls = 0;
        EventLoader.Responder = (_, _) => ++calls == 1 ? slow.Task : Task.FromResult(new EventPageDto([fast], 1));
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));

        // Act: the newer load finishes first, then the slow one completes
        ReviewState.SetCameras([Devices[1].Id]);
        cut.WaitForAssertion(() => Assert.Equal(new[] { fast.Id.ToString("D") }, RowIds(cut)));
        slow.SetResult(new EventPageDto([stale], 1));

        // Assert
        Assert.Equal(new[] { fast.Id.ToString("D") }, RowIds(cut));
    }

    [Fact]
    public async Task ReviewEventsTab_Disposed_CancelsTheLoadUnsubscribesAndIgnoresStateChanges()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        CancellationToken captured = default;
        var never = new TaskCompletionSource<EventPageDto>();
        EventLoader.Responder = (_, ct) =>
        {
            captured = ct;
            return never.Task;
        };
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));

        // Act
        ((IDisposable)cut.Instance).Dispose();
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        ReviewState.SetCameras([Devices[1].Id]);
        await Task.Delay(1000);

        // Assert
        Assert.True(captured.IsCancellationRequested);
        _ = Assert.Single(EventLoader.Calls);
    }

    [Fact]
    public void ReviewEventsTab_CameraRowClick_RequestsPlayOnArrivalAndEndsOnTimeline()
    {
        // Arrange
        AddDevices(2);
        EventDto target = EventAt(Devices[1].Id, Base.AddMinutes(20), "ding");
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30)), target]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));

        // Act
        cut.Find($"{RowSelector}[data-event-id='{target.Id:D}']").Click();

        // Assert
        Assert.True(ReviewState.PlayOnArrival);
        Assert.Equal(target.OccurredAtUtc, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewEventsTab_JammingRowClick_RequestsPlayOnArrivalAndEndsOnTimeline()
    {
        // Arrange
        AddDevices(2);
        DateTime start = Base.AddMinutes(20);
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30))]);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentDto>>([JammingAt(Devices[1].Id, start)]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelectorOfKind("Jamming"))));

        // Act
        cut.Find(RowSelectorOfKind("Jamming")).Click();

        // Assert
        Assert.True(ReviewState.PlayOnArrival);
        Assert.Equal(start, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewEventsTab_SignalAnomalyRowClick_RequestsPlayOnArrivalAndEndsOnTimeline()
    {
        // Arrange
        AddDevices(2);
        DateTime start = Base.AddMinutes(20);
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30))]);
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>([AnomalyAt(Devices[1].Id, start)]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelectorOfKind("SignalAnomaly"))));

        // Act
        cut.Find(RowSelectorOfKind("SignalAnomaly")).Click();

        // Assert
        Assert.True(ReviewState.PlayOnArrival);
        Assert.Equal(start, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewEventsTab_BitRateRowClick_RequestsPlayOnArrivalAndEndsOnTimeline()
    {
        // Arrange
        AddDevices(2);
        DateTime start = Base.AddMinutes(20);
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30))]);
        DetailLoader.BitRateResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewBitRateFlag>>([FlagAt(Devices[1].Id, start)]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelectorOfKind("BitRate"))));

        // Act
        cut.Find(RowSelectorOfKind("BitRate")).Click();

        // Assert
        Assert.True(ReviewState.PlayOnArrival);
        Assert.Equal(start, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }
}
