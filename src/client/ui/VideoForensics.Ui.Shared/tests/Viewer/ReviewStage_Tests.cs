using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>The review player's fixed stage: one box that stays put whether a clip, a gap or a loading state is shown.</summary>
public class ReviewStage_Tests : BunitContext
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public ReviewStage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<VideoForensics.Client.Ui.Shared.Services.IMediaPlaybackPolicy, VideoForensics.Client.Ui.Shared.Services.DefaultMediaPlaybackPolicy>();
    }

    private static List<ClipWindow> OneClip()
    {
        return [new("clip-a", Start, 60)];
    }

    [Fact]
    public void ReviewPlayer_Render_WrapsPlayerInStageElement()
    {
        // Act
        IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
            .Add(x => x.Clips, OneClip())
            .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
            .Add(x => x.Cursor, Start.AddSeconds(10)));

        // Assert
        Assert.NotNull(cut.Find("div.review-stage"));
        Assert.NotNull(cut.Find("div.review-stage .review-player"));
    }

    [Fact]
    public void ReviewPlayer_CursorInGap_KeepsStageElement()
    {
        // Act: the cursor is one hour after the only clip, so the player shows its gap state
        IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
            .Add(x => x.Clips, OneClip())
            .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
            .Add(x => x.Cursor, Start.AddHours(1)));

        // Assert
        Assert.NotNull(cut.Find("div.review-stage"));
    }
}
