using AngleSharp.Dom;
using Bunit;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review detail tab: jamming, anomalies, and expandable events, their states, clicks, reloads, refreshes, and disposal.</summary>
public class ReviewDetailTab_Tests : ReviewTestContext
{
    private const string EventRow = "[data-testid='review-event-toggle']";
    private const string JammingRow = "[data-testid='review-detail-jamming-row']";
    private const string AnomalyRow = "[data-testid='review-detail-anomaly-row']";
    private const string EventBody = "[data-testid='review-detail-event-body']";
    private const string LoadMoreSelector = "[data-testid='review-events-load-more']";

    private static readonly DateTime Base = FixedNowUtc.AddHours(-2);

    private static Event EventAt(Guid device, DateTime utc, string type = "motion") => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = device,
        ProviderEventId = Guid.NewGuid().ToString("N"),
        EventType = type,
        OccurredAtUtc = utc,
    };

    private static JammingIncidentRecord Jam(Guid device, DateTime startUtc) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = device,
        StartUtc = startUtc,
        EndUtc = startUtc.AddMinutes(5),
        AffectedEventCount = 3,
        Confidence = JammingConfidenceLevel.High,
        Source = JammingIncidentSource.AutoDetected,
    };

    private static EventDetail DetailWithAllFields(Guid eventId, Guid device)
    {
        var detection = new EventDetection
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            PersonDetected = true,
            StreamBroken = true,
            DetectionType = "human",
            ShortDescription = "Short summary text",
            FullDescription = "Full description text",
            Confidence = 0.5m,
            Anomaly = 0.25m,
            Similarity = 0.75m,
            ModelVersion = "model-v9",
        };
        var later = new EventDetectionTypeOccurrence { Id = Guid.NewGuid(), EventDetectionId = detection.Id, DetectionType = "vehicle", DetectedAtUtc = Base.AddMinutes(9) };
        var earlier = new EventDetectionTypeOccurrence { Id = Guid.NewGuid(), EventDetectionId = detection.Id, DetectionType = "human", DetectedAtUtc = Base.AddMinutes(1) };
        var zone = new EventDetectionZone { Id = Guid.NewGuid(), EventDetectionId = detection.Id, ZoneId = "zone-1", ZoneName = "Driveway", Confidence = 0.9m };
        var person = new EventDetectedPerson { Id = Guid.NewGuid(), EventId = eventId, ProfileId = "p-1", ProfileName = "Alex", Confidence = 0.8m };
        var alert = new EventSecurityAlert { Id = Guid.NewGuid(), EventId = eventId, Severity = "high", AlertText = "Loitering near door" };
        return new EventDetail(eventId, detection, [later, earlier], [zone], [person], [alert]);
    }

    /// <summary>Serves events from one list (newest first), keeping only the requested cameras and applying skip and take.</summary>
    private void ServeFrom(List<Event> all)
    {
        EventLoader.Responder = (call, _) =>
        {
            List<Event> matching = all.Where(e => call.DeviceIds.Contains(e.DeviceId)).ToList();
            return Task.FromResult(new EventPage(matching.Skip(call.Skip).Take(call.Take).ToList(), matching.Count));
        };
    }

    private IRenderedComponent<ReviewDetailTab> RenderTab()
    {
        return Render<ReviewDetailTab>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));
    }

    private static string Toggle(Event evt) => $"[data-testid='review-event-toggle'][data-event-id='{evt.Id:D}']";

    [Fact]
    public void ReviewDetailTab_EachSection_RendersItsData()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentRecord>>([Jam(Devices[0].Id, Base)]);
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>(
            [new ReviewAnomaly(Devices[0].Id, Base, Base.AddHours(1), SignalDeviationLevel.TwoSigma)]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(JammingRow)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(AnomalyRow)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));
        Assert.Contains(NameOf(Devices[0]), cut.Find(JammingRow).TextContent);
        Assert.Contains(L["DetailFieldConfidence"].Value, cut.Find(JammingRow).TextContent);
        Assert.Contains(L["JammingConfidence_High"].Value, cut.Find(JammingRow).TextContent);
        Assert.Contains(string.Format(L["DetailJammingAffected"].Value, 3), cut.Find(JammingRow).TextContent);
        Assert.Contains(L["DetailLevelTwoSigma"].Value, cut.Find(AnomalyRow).TextContent);
    }

    [Fact]
    public void ReviewDetailTab_NoCameras_ShowsTheNoCamerasMessageInEachSectionAndDoesNotLoad()
    {
        // Arrange
        AddDevices(1);

        // Act
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();

        // Assert
        Assert.Equal(3, cut.FindAll("[data-testid='review-detail-no-cameras']").Count);
        Assert.Empty(DetailLoader.JammingCalls);
        Assert.Empty(DetailLoader.AnomalyCalls);
        Assert.Empty(EventLoader.Calls);
    }

    [Fact]
    public void ReviewDetailTab_JammingFails_OtherSectionsStillRenderAndTheErrorIsLogged()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        DetailLoader.JammingResponder = (_, _) => throw new InvalidOperationException("jam down");
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>(
            [new ReviewAnomaly(Devices[0].Id, Base, Base.AddHours(1), SignalDeviationLevel.OneSigma)]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-detail-jamming-error']")));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(AnomalyRow)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));
        DetailLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("InvalidOperationException", StringComparison.Ordinal) && state.ToString()!.Contains("jam down", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReviewDetailTab_AnomalyFails_OtherSectionsStillRender()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentRecord>>([Jam(Devices[0].Id, Base)]);
        DetailLoader.AnomalyResponder = (_, _) => throw new InvalidOperationException("signal down");
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-detail-anomalies-error']")));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(JammingRow)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));
    }

    [Fact]
    public void ReviewDetailTab_EventsFail_OtherSectionsStillRender()
    {
        // Arrange
        AddDevices(1);
        EventLoader.Responder = (_, _) => throw new InvalidOperationException("events down");
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentRecord>>([Jam(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-detail-events-error']")));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(JammingRow)));
    }

    [Fact]
    public void ReviewDetailTab_ExpandEvent_LoadsOnlyThatEventsDetailOnceAndCachesIt()
    {
        // Arrange
        AddDevices(1);
        Event first = EventAt(Devices[0].Id, Base.AddMinutes(20));
        Event second = EventAt(Devices[0].Id, Base.AddMinutes(10));
        ServeFrom([first, second]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(EventRow).Count));

        // Act
        cut.Find(Toggle(first)).Click();
        cut.Find(Toggle(first)).Click();
        cut.Find(Toggle(first)).Click();
        cut.Find(Toggle(second)).Click();

        // Assert
        Assert.Equal(new[] { new[] { first.Id }, new[] { second.Id } }, DetailLoader.DetailCalls.Select(c => c.ToArray()).ToArray());
    }

    [Fact]
    public void ReviewDetailTab_ToggleButton_ReportsItsExpandedState()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));

        // Act
        cut.Find(Toggle(evt)).Click();

        // Assert
        Assert.Equal("true", cut.Find(Toggle(evt)).GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll(EventBody));
    }

    [Fact]
    public void ReviewDetailTab_ExpandedEvent_ShowsTheDetectionFieldsAndLocalizedLabels()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        DetailLoader.DetailResponder = (ids, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetail>>(
            new Dictionary<Guid, EventDetail> { [evt.Id] = DetailWithAllFields(evt.Id, Devices[0].Id) });
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));

        // Act
        cut.Find(Toggle(evt)).Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventBody)));
        string body = cut.Find(EventBody).TextContent;
        Assert.Contains("Short summary text", body);
        Assert.Contains("Full description text", body);
        Assert.Contains(L["DetectionType_human"].Value, body);
        Assert.Contains("model-v9", body);
        Assert.Contains(L["DetailFieldStreamBroken"].Value, body);
        Assert.Contains("Driveway", body);
        Assert.Contains("Alex", body);
        Assert.Contains("Loitering near door", body);
        Assert.Contains("high", body);
        Assert.Contains("50%", body);
        Assert.Contains("25%", body);
        Assert.Contains("75%", body);
        Assert.Contains("90%", body);
        Assert.Contains("80%", body);
    }

    [Fact]
    public void ReviewDetailTab_ExpandedEventWithoutDetail_ShowsTheNoDetailText()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));

        // Act
        cut.Find(Toggle(evt)).Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Contains(L["DetailNone"].Value, cut.Find(EventBody).TextContent));
    }

    [Fact]
    public void ReviewDetailTab_Occurrences_AreListedInTimeOrder()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        DetailLoader.DetailResponder = (_, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetail>>(
            new Dictionary<Guid, EventDetail> { [evt.Id] = DetailWithAllFields(evt.Id, Devices[0].Id) });
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));

        // Act
        cut.Find(Toggle(evt)).Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='review-detail-occurrence']").Count));
        string[] occurrences = cut.FindAll("[data-testid='review-detail-occurrence']").Select(o => o.TextContent).ToArray();
        Assert.Contains(L["DetectionType_human"].Value, occurrences[0]);
        Assert.Contains(L["DetectionType_vehicle"].Value, occurrences[1]);
    }

    [Fact]
    public void ReviewDetailTab_OccurrenceClick_SetsCameraCursorAndTimelineTab()
    {
        // Arrange
        AddDevices(2);
        Event evt = EventAt(Devices[1].Id, Base);
        ServeFrom([evt]);
        DetailLoader.DetailResponder = (_, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetail>>(
            new Dictionary<Guid, EventDetail> { [evt.Id] = DetailWithAllFields(evt.Id, Devices[1].Id) });
        ReviewState.SetCameras([Devices[1].Id]);
        ReviewState.SetTab(ReviewTab.Detail);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));
        cut.Find(Toggle(evt)).Click();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='review-detail-occurrence']").Count));
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);

        // Act
        cut.FindAll("[data-testid='review-detail-occurrence']")[1].Click();

        // Assert
        Assert.Equal(new[] { Devices[1].Id }, ReviewState.CameraIds);
        Assert.Equal(Base.AddMinutes(9), ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewDetailTab_JammingClick_SetsCameraCursorAndTimelineTab()
    {
        // Arrange
        AddDevices(2);
        DetailLoader.JammingResponder = (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentRecord>>([Jam(Devices[1].Id, Base.AddMinutes(30))]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(JammingRow)));

        // Act
        cut.Find(JammingRow).Click();

        // Assert
        Assert.Equal(new[] { Devices[1].Id }, ReviewState.CameraIds);
        Assert.Equal(Base.AddMinutes(30), ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewDetailTab_AnomalyClickOnTheOnlySelectedCamera_KeepsTheCamera()
    {
        // Arrange
        AddDevices(1);
        DetailLoader.AnomalyResponder = (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>(
            [new ReviewAnomaly(Devices[0].Id, Base, Base.AddHours(1), SignalDeviationLevel.OneSigma)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(AnomalyRow)));

        // Act
        cut.Find(AnomalyRow).Click();

        // Assert
        Assert.Equal(new[] { Devices[0].Id }, ReviewState.CameraIds);
        Assert.Equal(Base, ReviewState.Cursor);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewDetailTab_EventsLoadMore_AppendsTheNextPage()
    {
        // Arrange
        AddDevices(1);
        ServeFrom(Enumerable.Range(0, 150).Select(i => EventAt(Devices[0].Id, Base.AddMinutes(-i))).ToList());
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(ReviewEventFilters.PageSize, cut.FindAll(EventRow).Count));

        // Act
        cut.Find(LoadMoreSelector).Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(150, cut.FindAll(EventRow).Count));
        Assert.Equal(ReviewEventFilters.PageSize, EventLoader.Calls[1].Skip);
    }

    [Fact]
    public async Task ReviewDetailTab_CameraOrRangeChange_ReloadsAllSections()
    {
        // Arrange
        AddDevices(2);
        ServeFrom([EventAt(Devices[0].Id, Base), EventAt(Devices[1].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));
        cut.WaitForAssertion(() => Assert.Single(DetailLoader.JammingCalls));
        cut.WaitForAssertion(() => Assert.Single(DetailLoader.AnomalyCalls));

        // Act
        await cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id]));
        await cut.InvokeAsync(() => ReviewState.SetRange(FixedNowUtc.AddDays(-2), FixedNowUtc));

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(3, EventLoader.Calls.Count));
        cut.WaitForAssertion(() => Assert.Equal(3, DetailLoader.JammingCalls.Count));
        cut.WaitForAssertion(() => Assert.Equal(3, DetailLoader.AnomalyCalls.Count));
        Assert.Equal(new[] { Devices[1].Id }, DetailLoader.JammingCalls[1].DeviceIds);
        Assert.Equal(FixedNowUtc.AddDays(-2), DetailLoader.AnomalyCalls[2].FromUtc);
    }

    [Fact]
    public async Task ReviewDetailTab_CursorTabAndSelectionChange_DoesNotReloadAnySection()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));

        // Act
        await cut.InvokeAsync(() =>
        {
            ReviewState.SetCursor(Base.AddMinutes(5));
            ReviewState.SetTab(ReviewTab.Events);
            ReviewState.SelectEvent(evt.Id);
        });
        await Task.Delay(300);

        // Assert
        Assert.Single(EventLoader.Calls);
        Assert.Single(DetailLoader.JammingCalls);
        Assert.Single(DetailLoader.AnomalyCalls);
    }

    [Fact]
    public async Task ReviewDetailTab_EvidenceBurstForAShownDevice_RequeriesEventsOnceFromTheStart()
    {
        // Arrange
        AddDevices(2);
        var all = new List<Event> { EventAt(Devices[0].Id, Base.AddMinutes(10)), EventAt(Devices[0].Id, Base.AddMinutes(5)) };
        ServeFrom(all);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(EventRow).Count));
        Event fresh = EventAt(Devices[0].Id, Base.AddMinutes(15));
        all.Insert(0, fresh);

        // Act
        for (int i = 0; i < 3; i++)
        {
            EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        }

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count), TimeSpan.FromSeconds(5));
        Assert.Equal(2, EventLoader.Calls[1].Take);
        Assert.Equal(0, EventLoader.Calls[1].Skip);
        cut.WaitForAssertion(() => Assert.Equal(fresh.Id.ToString("D"), cut.FindAll(EventRow)[0].GetAttribute("data-event-id")), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ReviewDetailTab_EvidenceChange_InvalidatesTheCachedDetailOfAShownRow()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));
        cut.Find(Toggle(evt)).Click();
        cut.WaitForAssertion(() => Assert.Single(DetailLoader.DetailCalls));

        // Act
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));

        // Assert: the expanded row's detail is read again after the refresh
        cut.WaitForAssertion(() => Assert.Equal(2, DetailLoader.DetailCalls.Count), TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { evt.Id }, DetailLoader.DetailCalls[1]);
    }

    [Fact]
    public async Task ReviewDetailTab_HealthChange_RequeriesJammingAndAnomaliesButNotEvents()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(DetailLoader.JammingCalls));

        // Act
        HealthSource.Changes.OnNext(new DeviceHealthChangedDto(Devices[0].Id));

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, DetailLoader.JammingCalls.Count), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Equal(2, DetailLoader.AnomalyCalls.Count), TimeSpan.FromSeconds(5));
        Assert.Single(EventLoader.Calls);
    }

    [Fact]
    public void ReviewDetailTab_SelectedEventOnTheFirstPage_IsExpandedAutomatically()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30)), evt]);
        DetailLoader.DetailResponder = (_, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetail>>(
            new Dictionary<Guid, EventDetail> { [evt.Id] = DetailWithAllFields(evt.Id, Devices[0].Id) });
        ReviewState.SetCameras([Devices[0].Id]);
        ReviewState.SelectEvent(evt.Id);

        // Act
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventBody)));
        Assert.Equal("true", cut.Find(Toggle(evt)).GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll($"{EventRow}.active"));
    }

    [Fact]
    public async Task ReviewDetailTab_StaleJammingLoadAfterCameraChange_IsDiscarded()
    {
        // Arrange
        AddDevices(2);
        var stale = new TaskCompletionSource<IReadOnlyList<JammingIncidentRecord>>();
        int calls = 0;
        DetailLoader.JammingResponder = (call, _) =>
        {
            calls++;
            return calls == 1
                ? stale.Task
                : Task.FromResult<IReadOnlyList<JammingIncidentRecord>>([Jam(Devices[1].Id, Base.AddMinutes(40))]);
        };
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(DetailLoader.JammingCalls));

        // Act
        await cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id]));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(JammingRow)));
        stale.SetResult([Jam(Devices[0].Id, Base)]);
        await Task.Delay(100);

        // Assert
        Assert.Single(cut.FindAll(JammingRow));
        Assert.Contains(NameOf(Devices[1]), cut.Find(JammingRow).TextContent);
    }

    [Fact]
    public void ReviewDetailTab_Dispose_UnsubscribesFromTheChangeSources()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base)]);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));

        // Act
        ((IDisposable)cut.Instance).Dispose();

        // Assert
        Assert.False(EvidenceSource.Changes.HasObservers);
        Assert.False(HealthSource.Changes.HasObservers);
    }

    [Fact]
    public void ReviewDetailTab_DetectionTypeWithoutALabel_ShowsTheRawString()
    {
        // Arrange
        AddDevices(1);
        Event evt = EventAt(Devices[0].Id, Base);
        ServeFrom([evt]);
        DetailLoader.DetailResponder = (_, _) =>
        {
            EventDetail detail = new(evt.Id, new EventDetection { Id = Guid.NewGuid(), EventId = evt.Id, DetectionType = "mystery-kind" }, [], [], [], []);
            return Task.FromResult<IReadOnlyDictionary<Guid, EventDetail>>(new Dictionary<Guid, EventDetail> { [evt.Id] = detail });
        };
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewDetailTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(EventRow)));

        // Act
        cut.Find(Toggle(evt)).Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Contains("mystery-kind", cut.Find(EventBody).TextContent));
    }
}
