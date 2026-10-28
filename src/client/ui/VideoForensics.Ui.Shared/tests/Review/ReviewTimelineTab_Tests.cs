using Bunit;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review timeline tab: layout by camera count, loading, default cursor, reloads, stale results, errors, and disposal.</summary>
public class ReviewTimelineTab_Tests : ReviewTestContext
{
    private static readonly DateTime ClipStart = FixedNowUtc.AddHours(-1);

    private static MediaItem Clip(Guid device, DateTime startUtc, bool withPreview = false) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = device,
        FileName = "clip.mp4",
        FilePath = "clip.mp4",
        MediaFormat = "mp4",
        RecordedAtUtc = startUtc,
        DurationSeconds = 120,
        Sha256Hash = "hash",
        PreviewPath = withPreview ? "preview.mp4" : null,
    };

    /// <summary>Gives every requested camera one clip at <see cref="ClipStart"/>.</summary>
    private void ServeOneClipPerCamera()
    {
        ClipLoader.Responder = (call, _) => Task.FromResult(new ReviewClipSet(
            call.DeviceIds.Select(id => Clip(id, ClipStart, withPreview: true)).ToList(), false));
    }

    private IRenderedComponent<ReviewTimelineTab> RenderTab()
    {
        return Render<ReviewTimelineTab>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));
    }

    [Fact]
    public void ReviewTimelineTab_OneCameraSelected_RendersSingleCameraViewWithTheOthersInTheStrip()
    {
        // Arrange
        AddDevices(3);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<SingleCameraView>());
        });
        SingleCameraView view = cut.FindComponent<SingleCameraView>().Instance;
        Assert.Equal(3, view.Cameras.Count);
        Assert.Equal(0, view.SelectedIndex);
        Assert.Equal(2, cut.FindAll("[data-testid='single-camera-preview']").Count);
        Assert.Empty(cut.FindComponents<CameraGrid>());
    }

    [Fact]
    public void ReviewTimelineTab_ThreeCamerasSelected_RendersCameraGridWithThreeTiles()
    {
        // Arrange
        AddDevices(3);
        ReviewState.SetCameras(Devices.Select(d => d.Id));
        ServeOneClipPerCamera();

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, cut.FindAll("[data-testid='camera-tile']").Count);
        });
        Assert.Single(cut.FindComponents<CameraGrid>());
        Assert.Empty(cut.FindComponents<SingleCameraView>());
        Assert.Equal(3, ClipLoader.Calls.Single().DeviceIds.Count);
    }

    [Fact]
    public void ReviewTimelineTab_NoCamerasSelected_ShowsTheEmptyStateAndDoesNotLoad()
    {
        // Arrange
        AddDevices(2);

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        Assert.Equal(L["NoCamerasSelected"].Value, cut.Find("[data-testid='review-timeline-no-cameras']").TextContent);
        Assert.Empty(ClipLoader.Calls);
    }

    [Fact]
    public void ReviewTimelineTab_CameraWithNoClips_ShowsTheNoClipsMessage()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(L["NoClipsInRange"].Value, cut.Find("[data-testid='review-timeline-no-clips']").TextContent);
        });
    }

    [Fact]
    public void ReviewTimelineTab_CursorUnset_DefaultsToTheStartOfTheNewestClip()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras(Devices.Select(d => d.Id));
        DateTime older = FixedNowUtc.AddHours(-1);
        DateTime newest = FixedNowUtc.AddMinutes(-30);
        ClipLoader.Responder = (call, _) => Task.FromResult(new ReviewClipSet(
            [Clip(Devices[0].Id, older), Clip(Devices[1].Id, newest)], false));

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(newest, ReviewState.Cursor));
    }

    [Fact]
    public void ReviewTimelineTab_CameraSelectionChanged_ReloadsWithTheNewCameras()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(ClipLoader.Calls));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id]));

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, ClipLoader.Calls.Count));
        Assert.Contains(Devices[1].Id, ClipLoader.Calls[1].DeviceIds);
    }

    [Fact]
    public void ReviewTimelineTab_OnlyCursorChanged_DoesNotReload()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(ClipLoader.Calls));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCursor(FixedNowUtc.AddMinutes(-20)));

        // Assert
        Assert.Single(ClipLoader.Calls);
    }

    [Fact]
    public void ReviewTimelineTab_EvidenceBurstForALoadedDevice_ReQueriesOnce()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(ClipLoader.Calls));

        // Act
        for (int i = 0; i < 3; i++)
        {
            EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        }

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(2, ClipLoader.Calls.Count), TimeSpan.FromSeconds(5));
        Thread.Sleep(1200);
        Assert.Equal(2, ClipLoader.Calls.Count);
    }

    [Fact]
    public void ReviewTimelineTab_EvidenceForAnUnrelatedDevice_DoesNotReload()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(ClipLoader.Calls));

        // Act
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Guid.NewGuid()));
        Thread.Sleep(1200);

        // Assert: a related change still reloads, so the subscription is live
        Assert.Single(ClipLoader.Calls);
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        cut.WaitForAssertion(() => Assert.Equal(2, ClipLoader.Calls.Count), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ReviewTimelineTab_SlowLoadSupersededByANewerLoad_DiscardsTheStaleResult()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        var slow = new TaskCompletionSource<ReviewClipSet>();
        int calls = 0;
        ClipLoader.Responder = (call, _) =>
        {
            calls++;
            return calls == 1 ? slow.Task : Task.FromResult(new ReviewClipSet([], false));
        };
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(ClipLoader.Calls));

        // Act: the newer load finishes first with no clips, then the slow one completes with a clip
        cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id]));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='review-timeline-no-clips']")));
        slow.SetResult(new ReviewClipSet([Clip(Devices[0].Id, ClipStart)], false));
        Thread.Sleep(200);

        // Assert
        Assert.NotEmpty(cut.FindAll("[data-testid='review-timeline-no-clips']"));
        Assert.Empty(cut.FindComponents<SingleCameraView>());
    }

    [Fact]
    public void ReviewTimelineTab_LoadThrows_ShowsTheErrorMessageAndLogsTheTypeAndMessage()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ClipLoader.Responder = (_, _) => Task.FromException<ReviewClipSet>(new InvalidOperationException("boom"));

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(L["ClipsLoadFailed"].Value, cut.Find("[data-testid='review-timeline-error']").TextContent);
        });
        TabLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("InvalidOperationException", StringComparison.Ordinal) && state.ToString()!.Contains("boom", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReviewTimelineTab_TruncatedLoad_ShowsTheCapMessage()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ClipLoader.Responder = (call, _) => Task.FromResult(new ReviewClipSet([Clip(Devices[0].Id, ClipStart)], true));

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(ReviewClipLoader.MaxItems.ToString(), cut.Find("[data-testid='review-timeline-truncated']").TextContent);
        });
    }

    [Fact]
    public void ReviewTimelineTab_LoadedWithCameras_PublishesOnlyThePrimaryCamerasClips()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id, Devices[1].Id]);
        MediaItem primary = Clip(Devices[0].Id, ClipStart);
        MediaItem other = Clip(Devices[1].Id, ClipStart);
        ClipLoader.Responder = (_, _) => Task.FromResult(new ReviewClipSet([primary, other], false));

        // Act
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();

        // Assert
        cut.WaitForAssertion(() => Assert.Single(TimelineModel.Clips));
        Assert.Equal(primary.Id.ToString("D"), TimelineModel.Clips[0].Id);
        Assert.Equal(ReviewState.FromUtc, TimelineModel.FromUtc);
        Assert.Equal(ReviewState.ToUtc, TimelineModel.ToUtc);
    }

    [Fact]
    public void ReviewTimelineTab_CamerasBecomeEmpty_ClearsThePublishedClips()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(TimelineModel.Clips));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCameras([]));

        // Assert
        cut.WaitForAssertion(() => Assert.Empty(TimelineModel.Clips));
    }

    [Fact]
    public void ReviewTimelineTab_LoadFailsAfterAGoodLoad_ClearsThePublishedClips()
    {
        // Arrange
        AddDevices(2);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(TimelineModel.Clips));
        ClipLoader.Responder = (_, _) => Task.FromException<ReviewClipSet>(new InvalidOperationException("boom"));

        // Act
        cut.InvokeAsync(() => ReviewState.SetCameras([Devices[1].Id]));

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-timeline-error']")));
        Assert.Empty(TimelineModel.Clips);
    }

    [Fact]
    public void ReviewTimelineTab_Disposed_ClearsThePublishedClips()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        ServeOneClipPerCamera();
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(TimelineModel.Clips));

        // Act
        ((IDisposable)cut.Instance).Dispose();

        // Assert
        Assert.Empty(TimelineModel.Clips);
    }

    [Fact]
    public void ReviewTimelineTab_Disposed_CancelsTheLoadAndUnsubscribes()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);
        CancellationToken captured = default;
        ClipLoader.Responder = async (_, ct) =>
        {
            captured = ct;
            await Task.Delay(Timeout.Infinite, ct);
            return new ReviewClipSet([], false);
        };
        IRenderedComponent<ReviewTimelineTab> cut = RenderTab();
        cut.WaitForAssertion(() => Assert.Single(ClipLoader.Calls));

        // Act
        ((IDisposable)cut.Instance).Dispose();
        EvidenceSource.Changes.OnNext(new EvidenceChangedDto(Devices[0].Id));
        ReviewState.SetCameras([Devices[0].Id, Guid.NewGuid()]);
        Thread.Sleep(1200);

        // Assert
        Assert.True(captured.IsCancellationRequested);
        Assert.Single(ClipLoader.Calls);
    }
}
