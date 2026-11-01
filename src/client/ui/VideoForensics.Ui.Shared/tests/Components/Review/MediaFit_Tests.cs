using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The media fit: plays a playable clip through the media player, and shows one localized message for every other state.</summary>
public class MediaFit_Tests : BunitContext
{
    public MediaFit_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
    }

    private IStringLocalizer<SharedResources> Localizer => Services.GetRequiredService<IStringLocalizer<SharedResources>>();

    [Fact]
    public void MediaFit_Playable_RendersMediaPlayer()
    {
        IRenderedComponent<MediaFit> cut = Render<MediaFit>(p => p
            .Add(c => c.State, MediaFitState.Playable)
            .Add(c => c.Source, "/clips/a.mp4"));

        _ = cut.FindComponent<MediaPlayer>();
        Assert.Empty(cut.FindAll(".media-fit-state"));
    }

    [Fact]
    public void MediaFit_Purged_ShowsPurgedMessageAndNoPlayer()
    {
        IRenderedComponent<MediaFit> cut = Render<MediaFit>(p => p
            .Add(c => c.State, MediaFitState.Purged));

        Assert.Empty(cut.FindComponents<MediaPlayer>());
        Assert.Equal(Localizer["MediaFitPurged"].Value, cut.Find(".media-fit-state").TextContent.Trim());
    }

    [Theory]
    [InlineData("Purged")]
    [InlineData("FileMissing")]
    [InlineData("DeviceDeleted")]
    [InlineData("FlagNotFound")]
    [InlineData("NoClipInHour")]
    public void MediaFit_EachNonPlayableState_ShowsItsOwnMessage(string stateName)
    {
        var state = Enum.Parse<MediaFitState>(stateName);

        IRenderedComponent<MediaFit> cut = Render<MediaFit>(p => p
            .Add(c => c.State, state));

        Assert.Empty(cut.FindComponents<MediaPlayer>());
        Assert.Equal(Localizer["MediaFit" + stateName].Value, cut.Find(".media-fit-state").TextContent.Trim());
    }
}
