using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

public class MediaTransportControls_Tests : BunitContext
{
    public MediaTransportControls_Tests()
    {
        _ = Services.AddLocalization();
    }

    [Fact]
    public void MediaTransportControls_Render_ShowsReversePlayPauseAndForward()
    {
        IRenderedComponent<MediaTransportControls> cut = Render<MediaTransportControls>();

        Assert.NotNull(cut.Find("[data-testid='media-transport-back']"));
        Assert.NotNull(cut.Find("[data-testid='media-transport-playpause']"));
        Assert.NotNull(cut.Find("[data-testid='media-transport-forward']"));
    }

    [Fact]
    public void MediaTransportControls_BackClicked_StepsBackByTheSkipAmount()
    {
        double? stepped = null;
        IRenderedComponent<MediaTransportControls> cut = Render<MediaTransportControls>(p => p
            .Add(c => c.OnStep, EventCallback.Factory.Create<double>(this, seconds => stepped = seconds)));

        cut.Find("[data-testid='media-transport-back']").Click();

        Assert.Equal(-10, stepped);
    }

    [Fact]
    public void MediaTransportControls_ForwardClicked_StepsForwardByTheSkipAmount()
    {
        double? stepped = null;
        IRenderedComponent<MediaTransportControls> cut = Render<MediaTransportControls>(p => p
            .Add(c => c.OnStep, EventCallback.Factory.Create<double>(this, seconds => stepped = seconds)));

        cut.Find("[data-testid='media-transport-forward']").Click();

        Assert.Equal(10, stepped);
    }

    [Fact]
    public void MediaTransportControls_PlayPauseClicked_RaisesPlayPause()
    {
        bool raised = false;
        IRenderedComponent<MediaTransportControls> cut = Render<MediaTransportControls>(p => p
            .Add(c => c.OnPlayPause, EventCallback.Factory.Create(this, () => raised = true)));

        cut.Find("[data-testid='media-transport-playpause']").Click();

        Assert.True(raised);
    }

    [Fact]
    public void MediaTransportControls_Disabled_DisablesEveryButton()
    {
        IRenderedComponent<MediaTransportControls> cut = Render<MediaTransportControls>(p => p.Add(c => c.Disabled, true));

        Assert.All(cut.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
    }
}
