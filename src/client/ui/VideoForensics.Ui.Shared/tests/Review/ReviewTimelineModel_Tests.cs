using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The in-memory timeline model the right-panel timeline reads: publish, change detection, and clear.</summary>
public class ReviewTimelineModel_Tests
{
    private static readonly DateTime From = new(2026, 10, 28, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private static List<ClipWindow> OneClip(string id = "a")
    {
        return [new ClipWindow(id, From.AddMinutes(10), 60)];
    }

    [Fact]
    public void ReviewTimelineModel_PublishUnavailable_KeepsItApartFromPlayableClips()
    {
        var model = new ReviewTimelineModel();
        var unavailable = new List<ClipWindow> { new("event:x", From.AddMinutes(20), 10, null, true) };

        model.Publish(OneClip(), From, To, unavailable);

        // The playable list is unchanged, so playback never sees the unavailable window
        _ = Assert.Single(model.Clips);
        Assert.Equal("a", model.Clips[0].Id);
        _ = Assert.Single(model.UnavailableClips);
    }

    [Fact]
    public void ReviewTimelineModel_ChangedUnavailableOnly_RaisesOnChange()
    {
        var model = new ReviewTimelineModel();
        model.Publish(OneClip(), From, To);
        int raised = 0;
        model.OnChange += () => raised++;

        model.Publish(OneClip(), From, To, [new ClipWindow("event:x", From.AddMinutes(20), 10, null, true)]);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ReviewTimelineModel_PublishNewClips_RaisesOnChangeOnce()
    {
        var model = new ReviewTimelineModel();
        int raised = 0;
        model.OnChange += () => raised++;

        model.Publish(OneClip(), From, To);

        Assert.Equal(1, raised);
        _ = Assert.Single(model.Clips);
        Assert.Equal(From, model.FromUtc);
        Assert.Equal(To, model.ToUtc);
    }

    [Fact]
    public void ReviewTimelineModel_PublishSameClipsAndRange_DoesNotRaiseOnChange()
    {
        var model = new ReviewTimelineModel();
        model.Publish(OneClip(), From, To);
        int raised = 0;
        model.OnChange += () => raised++;

        model.Publish(OneClip(), From, To);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ReviewTimelineModel_PublishSameClipsDifferentRange_RaisesOnChange()
    {
        var model = new ReviewTimelineModel();
        model.Publish(OneClip(), From, To);
        int raised = 0;
        model.OnChange += () => raised++;

        model.Publish(OneClip(), From.AddHours(-1), To);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ReviewTimelineModel_PublishDifferentClips_RaisesOnChange()
    {
        var model = new ReviewTimelineModel();
        model.Publish(OneClip("a"), From, To);
        int raised = 0;
        model.OnChange += () => raised++;

        model.Publish(OneClip("b"), From, To);

        Assert.Equal(1, raised);
        Assert.Equal("b", model.Clips[0].Id);
    }

    [Fact]
    public void ReviewTimelineModel_ListMutatedAfterPublish_DoesNotChangeTheStoredClips()
    {
        var model = new ReviewTimelineModel();
        List<ClipWindow> source = OneClip();
        model.Publish(source, From, To);

        source.Clear();

        _ = Assert.Single(model.Clips);
    }

    [Fact]
    public void ReviewTimelineModel_ReversedRange_Throws()
    {
        var model = new ReviewTimelineModel();

        _ = Assert.Throws<ArgumentException>(() => model.Publish(OneClip(), To, From));
    }

    [Fact]
    public void ReviewTimelineModel_ClearWithClips_EmptiesTheClipsAndRaisesOnChange()
    {
        var model = new ReviewTimelineModel();
        model.Publish(OneClip(), From, To);
        int raised = 0;
        model.OnChange += () => raised++;

        model.Clear();

        Assert.Empty(model.Clips);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ReviewTimelineModel_ClearWhenAlreadyEmpty_DoesNotRaiseOnChange()
    {
        var model = new ReviewTimelineModel();
        int raised = 0;
        model.OnChange += () => raised++;

        model.Clear();

        Assert.Equal(0, raised);
    }
}
