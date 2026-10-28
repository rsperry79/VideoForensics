using Bunit;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The page-level timeline feeder: owns the right-panel timeline model. It loads the primary camera's clips on camera or range
/// changes, reloads on evidence changes for that camera, and clears the model when it has nothing to show or is disposed.
/// </summary>
public class ReviewTimelineFeeder_Tests : ReviewTestContext
{
    private static readonly DateTime ClipStart = FixedNowUtc.AddHours(-1);

    private static MediaItem Clip(Guid device, DateTime startUtc) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = device,
        FileName = "clip.mp4",
        FilePath = "clip.mp4",
        MediaFormat = "mp4",
        RecordedAtUtc = startUtc,
        DurationSeconds = 120,
        Sha256Hash = "hash",
    };

    /// <summary>Gives every requested camera one clip at <see cref="ClipStart"/>.</summary>
    private void ServeOneClipPerCamera()
    {
        ClipLoader.Responder = (call, _) => Task.FromResult(new ReviewClipSet(
            call.DeviceIds.Select(id => Clip(id, ClipStart)).ToList(), false));
    }

    private IRenderedComponent<ReviewTimelineFeeder> RenderFeeder()
    {
        return Render<ReviewTimelineFeeder>();
    }

    /// <summary>
    /// Polls an assertion until it passes. The feeder renders nothing, so bunit's WaitForAssertion would never re-check it;
    /// the poll keeps the wait short and rethrows the last failure at the deadline.
    /// </summary>
    private static void WaitFor(Action assertion)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }
        }
    }

    [Fact]
    public void ReviewTimelineFeeder_Startup_PublishesOnlyThePrimaryCamerasClips()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        ServeOneClipPerCamera();

        // Act
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();

        // Assert
        WaitFor(() => Assert.Single(TimelineModel.Clips));
        Assert.Equal(Devices[0].Id, Assert.Single(ClipLoader.Calls).DeviceIds.Single());
        Assert.Equal(ReviewState.FromUtc, TimelineModel.FromUtc);
        Assert.Equal(ReviewState.ToUtc, TimelineModel.ToUtc);
    }

    [Fact]
    public void ReviewTimelineFeeder_PrimaryCameraChanged_ReloadsForTheNewPrimary()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(ClipLoader.Calls));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id, Devices[0].Id]));

        // Assert
        WaitFor(() => Assert.Equal(2, ClipLoader.Calls.Count));
        Assert.Equal(Devices[1].Id, ClipLoader.Calls[1].DeviceIds.Single());
        WaitFor(() => Assert.Single(TimelineModel.Clips));
    }

    [Fact]
    public void ReviewTimelineFeeder_RangeChanged_Reloads()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(ClipLoader.Calls));
        DateTime newFrom = FixedNowUtc.AddHours(-2);

        // Act
        cut.InvokeAsync(() => ReviewState.SetRange(newFrom, FixedNowUtc));

        // Assert
        WaitFor(() => Assert.Equal(2, ClipLoader.Calls.Count));
        Assert.Equal(newFrom, ClipLoader.Calls[1].FromUtc);
        WaitFor(() => Assert.Equal(newFrom, TimelineModel.FromUtc));
    }

    [Fact]
    public void ReviewTimelineFeeder_OnlyCursorOrTabChanged_DoesNotReload()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(ClipLoader.Calls));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCursor(FixedNowUtc.AddMinutes(-20)));
        cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Events));

        // Assert
        Assert.Single(ClipLoader.Calls);
    }

    [Fact]
    public void ReviewTimelineFeeder_CamerasBecomeEmpty_ClearsTheModel()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(TimelineModel.Clips));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCameras([]));

        // Assert
        WaitFor(() => Assert.Empty(TimelineModel.Clips));
        Assert.Single(ClipLoader.Calls);
    }

    [Fact]
    public void ReviewTimelineFeeder_EvidenceBurstForThePrimaryCamera_ReQueriesOnce()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(ClipLoader.Calls));

        // Act
        for (int i = 0; i < 3; i++)
        {
            EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        }

        // Assert
        WaitFor(() => Assert.Equal(2, ClipLoader.Calls.Count));
        Thread.Sleep(1200);
        Assert.Equal(2, ClipLoader.Calls.Count);
    }

    [Fact]
    public void ReviewTimelineFeeder_EvidenceForAnotherDevice_DoesNotReload()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(ClipLoader.Calls));

        // Act
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[1].Id));
        Thread.Sleep(1200);

        // Assert: a related change still reloads, so the subscription is live
        Assert.Single(ClipLoader.Calls);
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        WaitFor(() => Assert.Equal(2, ClipLoader.Calls.Count));
    }

    [Fact]
    public void ReviewTimelineFeeder_SlowLoadSupersededByANewerLoad_DiscardsTheStaleResult()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        var slow = new TaskCompletionSource<ReviewClipSet>();
        int calls = 0;
        ClipLoader.Responder = (_, _) =>
        {
            calls++;
            return calls == 1 ? slow.Task : Task.FromResult(new ReviewClipSet([], false));
        };
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(ClipLoader.Calls));

        // Act: the newer load finishes first with no clips, then the slow one completes with a clip
        cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id]));
        WaitFor(() => Assert.Equal(2, ClipLoader.Calls.Count));
        slow.SetResult(new ReviewClipSet([Clip(Devices[0].Id, ClipStart)], false));
        Thread.Sleep(200);

        // Assert
        Assert.Empty(TimelineModel.Clips);
    }

    [Fact]
    public void ReviewTimelineFeeder_LoadThrows_ClearsTheModelWithoutThrowingAndLogsTypeAndMessage()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(TimelineModel.Clips));
        ClipLoader.Responder = (_, _) => Task.FromException<ReviewClipSet>(new InvalidOperationException("boom"));

        // Act
        Exception? error = Record.Exception(() => cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id])).GetAwaiter().GetResult());

        // Assert
        Assert.Null(error);
        WaitFor(() => Assert.Empty(TimelineModel.Clips));
        FeederLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("InvalidOperationException", StringComparison.Ordinal) && state.ToString()!.Contains("boom", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReviewTimelineFeeder_Disposed_UnsubscribesCancelsTheLoadAndClearsTheModel()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineFeeder> cut = RenderFeeder();
        WaitFor(() => Assert.Single(TimelineModel.Clips));
        CancellationToken captured = default;
        ClipLoader.Responder = async (_, ct) =>
        {
            captured = ct;
            await Task.Delay(Timeout.Infinite, ct);
            return new ReviewClipSet([], false);
        };
        cut.InvokeAsync(() => ReviewState.SetRange(FixedNowUtc.AddHours(-3), FixedNowUtc));
        WaitFor(() => Assert.Equal(2, ClipLoader.Calls.Count));

        // Act
        ((IDisposable)cut.Instance).Dispose();
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        ReviewState.SetCameras([Devices[0].Id, Guid.NewGuid()]);
        Thread.Sleep(1200);

        // Assert
        Assert.True(captured.IsCancellationRequested);
        Assert.Equal(2, ClipLoader.Calls.Count);
        Assert.Empty(TimelineModel.Clips);
    }
}
