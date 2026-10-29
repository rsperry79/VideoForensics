using AngleSharp.Dom;
using Bunit;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review events tab: rows for the selected cameras, paging, states, row clicks, reloads, refreshes, and disposal.</summary>
public class ReviewEventsTab_Tests : ReviewTestContext
{
    private const string RowSelector = "[data-testid='review-event-row']";
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

    /// <summary>
    /// Serves events from one list (given newest first), keeping only the requested cameras and applying skip and take.
    /// The list is read on every call, so a test can change it between calls.
    /// </summary>
    private void ServeFrom(List<Event> all)
    {
        EventLoader.Responder = (call, _) =>
        {
            List<Event> matching = all.Where(e => call.DeviceIds.Contains(e.DeviceId)).ToList();
            return Task.FromResult(new EventPage(matching.Skip(call.Skip).Take(call.Take).ToList(), matching.Count));
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

    [Fact]
    public void ReviewEventsTab_SelectedCamera_ShowsItsEventsNewestFirst()
    {
        // Arrange
        AddDevices(2);
        Event newest = EventAt(Devices[0].Id, Base.AddMinutes(30));
        Event older = EventAt(Devices[0].Id, Base.AddMinutes(10));
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
        Assert.Single(row.QuerySelectorAll("time"));
    }

    [Fact]
    public void ReviewEventsTab_UnknownCamera_ShowsTheDeviceIdText()
    {
        // Arrange
        Guid unknown = Guid.NewGuid();
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
    public void ReviewEventsTab_Loaded_ShowsTheLocalizedTotal()
    {
        // Arrange
        AddDevices(1);
        ServeFrom([EventAt(Devices[0].Id, Base), EventAt(Devices[0].Id, Base.AddMinutes(-1))]);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-events-total']")));
        Assert.Equal(string.Format(L["EventsTotal"].Value, 2), cut.Find("[data-testid='review-events-total']").TextContent.Trim());
    }

    [Fact]
    public void ReviewEventsTab_LoadMore_AppendsTheNextPageAndHidesAtTheEnd()
    {
        // Arrange
        AddDevices(1);
        ServeFrom(Enumerable.Range(0, 150).Select(i => EventAt(Devices[0].Id, Base.AddMinutes(-i))).ToList());
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(ReviewEventFilters.PageSize, cut.FindAll(RowSelector).Count));
        Assert.Single(cut.FindAll(LoadMoreSelector));

        // Act
        cut.Find(LoadMoreSelector).Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(150, cut.FindAll(RowSelector).Count));
        Assert.Equal(ReviewEventFilters.PageSize, EventLoader.Calls[1].Skip);
        Assert.Empty(cut.FindAll(LoadMoreSelector));
        Assert.Equal(string.Format(L["EventsTotal"].Value, 150), cut.Find("[data-testid='review-events-total']").TextContent.Trim());
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
        var never = new TaskCompletionSource<EventPage>();
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
        Event target = EventAt(Devices[1].Id, Base.AddMinutes(20), "ding");
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
        Event target = EventAt(Devices[1].Id, Base.AddMinutes(20));
        ServeFrom([EventAt(Devices[0].Id, Base.AddMinutes(30)), target]);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));

        // Act
        cut.Find($"{RowSelector}[data-event-id='{target.Id:D}']").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.True(cut.Find($"{RowSelector}[data-event-id='{target.Id:D}']").ClassList.Contains("active")));
        Assert.Single(cut.FindAll($"{RowSelector}.active"));
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
        Assert.Single(EventLoader.Calls);
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
        Event evt = EventAt(Devices[0].Id, Base);
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
        Assert.Single(EventLoader.Calls);
    }

    [Fact]
    public async Task ReviewEventsTab_EvidenceBurstForAShownDevice_RequeriesOnceAndRefreshesTheLoadedRows()
    {
        // Arrange
        AddDevices(2);
        var all = new List<Event> { EventAt(Devices[0].Id, Base.AddMinutes(10)), EventAt(Devices[0].Id, Base.AddMinutes(5)) };
        ServeFrom(all);
        ReviewState.SetCameras([Devices[0].Id]);
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
        Event fresh = EventAt(Devices[0].Id, Base.AddMinutes(15));
        all.Insert(0, fresh);

        // Act
        for (int i = 0; i < 3; i++)
        {
            EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        }

        // Assert: one re-query for the burst, asking for as many rows as are loaded, and the new row is on top
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Equal(fresh.Id.ToString("D"), RowIds(cut)[0]), TimeSpan.FromSeconds(5));
        await Task.Delay(1000);
        Assert.Equal(2, EventLoader.Calls.Count);
        Assert.Equal(2, EventLoader.Calls[1].Take);
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
        Assert.Single(EventLoader.Calls);
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        cut.WaitForAssertion(() => Assert.Equal(2, EventLoader.Calls.Count), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ReviewEventsTab_SlowLoadSupersededByNewerLoad_DiscardsTheStaleResult()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        var slow = new TaskCompletionSource<EventPage>();
        Event stale = EventAt(Devices[0].Id, Base.AddMinutes(10));
        Event fast = EventAt(Devices[1].Id, Base.AddMinutes(5));
        int calls = 0;
        EventLoader.Responder = (_, _) => ++calls == 1 ? slow.Task : Task.FromResult(new EventPage([fast], 1));
        IRenderedComponent<ReviewEventsTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(EventLoader.Calls));

        // Act: the newer load finishes first, then the slow one completes
        ReviewState.SetCameras([Devices[1].Id]);
        cut.WaitForAssertion(() => Assert.Equal(new[] { fast.Id.ToString("D") }, RowIds(cut)));
        slow.SetResult(new EventPage([stale], 1));

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
        var never = new TaskCompletionSource<EventPage>();
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
        Assert.Single(EventLoader.Calls);
    }
}
