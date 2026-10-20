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
    /// A video's thumbnail is the frame the server stored at import, served from the thumbnail route that the same
    /// ticket authorises. The clip itself is never loaded for a thumbnail.
    /// </summary>
    [Fact]
    public void Video_RendersImgWithServerThumbnailUrl_AndNoVideoElement()
    {
        var component = Render<MediaThumb>(p => p
            .Add(x => x.Url, "/api/v1/media/abc/content?ticket=2")
            .Add(x => x.IsVideo, true)
            .Add(x => x.Alt, "Driveway"));

        var img = component.Find("img");
        Assert.Equal("/api/v1/media/abc/thumbnail?ticket=2", img.GetAttribute("src"));
        Assert.Empty(component.FindAll("video"));
    }
}
