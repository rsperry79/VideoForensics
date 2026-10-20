namespace VideoForensics.Ui.Shared.Tests.Components.Evidence;

using Bunit;
using Xunit;
using VideoForensics.Ui.Shared.Components.Evidence;

public class MediaThumbTests : BunitContext
{
    [Fact]
    public void Image_RendersImgWithUrl_AndNoVideoElement()
    {
        var component = Render<MediaThumb>(p => p
            .Add(x => x.Url, "https://cdn.test/a.jpg?ticket=1")
            .Add(x => x.IsVideo, false)
            .Add(x => x.Alt, "Front Door"));

        var img = component.Find("img");
        Assert.Equal("https://cdn.test/a.jpg?ticket=1", img.GetAttribute("src"));
        Assert.Empty(component.FindAll("video"));
    }

    /// <summary>
    /// A video has no image to show, so the thumbnail is the first frame of the file itself: metadata-only
    /// preload plus a seek to 0.1s makes the browser paint a frame without downloading the whole clip.
    /// </summary>
    [Fact]
    public void Video_RendersVideoElementSeekedToFirstFrame_WithMetadataPreload()
    {
        var component = Render<MediaThumb>(p => p
            .Add(x => x.Url, "https://cdn.test/clip.mp4?ticket=2")
            .Add(x => x.IsVideo, true)
            .Add(x => x.Alt, "Driveway"));

        var video = component.Find("video");
        Assert.Equal("https://cdn.test/clip.mp4?ticket=2#t=0.1", video.GetAttribute("src"));
        Assert.Equal("metadata", video.GetAttribute("preload"));
        Assert.NotNull(video.GetAttribute("muted"));
        Assert.Empty(component.FindAll("img"));
    }
}
