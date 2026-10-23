
using AngleSharp.Dom;

using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Evidence;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Evidence;

public class MediaThumbTests : BunitContext
{
    [Fact]
    public void Image_RendersImgWithUrl_AndNoVideoElement()
    {
        IRenderedComponent<MediaThumb> component = Render<MediaThumb>(p => p
            .Add(x => x.Url, "https://cdn.test/a.jpg?ticket=1")
            .Add(x => x.IsVideo, false)
            .Add(x => x.Alt, "Front Door"));

        IElement img = component.Find("img");
        Assert.Equal("https://cdn.test/a.jpg?ticket=1", img.GetAttribute("src"));
        Assert.Empty(component.FindAll("video"));
    }

    /// <summary>
    /// Why: a grid can list hundreds of items at once; native lazy loading defers each request until its tile
    /// nears the viewport so the server's media rate limit is not hit by one burst of thumbnail requests.
    /// </summary>
    [Fact]
    public void MediaThumb_Render_SetsLazyLoading()
    {
        IRenderedComponent<MediaThumb> component = Render<MediaThumb>(p => p
            .Add(x => x.Url, "https://cdn.test/a.jpg?ticket=1")
            .Add(x => x.IsVideo, false)
            .Add(x => x.Alt, "Front Door"));

        Assert.Equal("lazy", component.Find("img").GetAttribute("loading"));
    }

    /// <summary>
    /// A video's thumbnail is the frame the server stored at import, served from the thumbnail route that the same
    /// ticket authorises. The clip itself is never loaded for a thumbnail.
    /// </summary>
    [Fact]
    public void Video_RendersImgWithServerThumbnailUrl_AndNoVideoElement()
    {
        IRenderedComponent<MediaThumb> component = Render<MediaThumb>(p => p
            .Add(x => x.Url, "/api/v1/media/abc/content?ticket=2")
            .Add(x => x.IsVideo, true)
            .Add(x => x.Alt, "Driveway"));

        IElement img = component.Find("img");
        Assert.Equal("/api/v1/media/abc/thumbnail?ticket=2", img.GetAttribute("src"));
        Assert.Empty(component.FindAll("video"));
    }
}
