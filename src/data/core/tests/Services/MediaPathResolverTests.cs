using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests;

public class MediaPathResolverTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "vf-media-root");

    private static MediaPathResolver Create()
    {
        var config = new Mock<IForensicsConfiguration>();
        _ = config.SetupGet(c => c.DownloadLocation).Returns(Root);
        var service = new Mock<IForensicsConfigurationService>();
        _ = service.Setup(s => s.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(config.Object);
        return new MediaPathResolver(new StorageLocationProvider(), service.Object);
    }

    [Fact]
    public async Task ToStored_PathUnderRoot_IsRelativeWithForwardSlashes()
    {
        string stored = await Create().ToStoredAsync(Path.Combine(Root, "ring", "home", "front_door", "clip.mp4"), CancellationToken.None);

        Assert.Equal("ring/home/front_door/clip.mp4", stored);
    }

    [Fact]
    public async Task ToStored_PathOutsideRoot_IsUnchanged()
    {
        string outside = Path.Combine(Path.GetTempPath(), "elsewhere", "clip.mp4");

        Assert.Equal(outside, await Create().ToStoredAsync(outside, CancellationToken.None));
    }

    [Fact]
    public async Task ToAbsolute_RelativePath_IsCombinedWithRoot()
    {
        string full = await Create().ToAbsoluteAsync("ring/home/front_door/clip.mp4", CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "ring", "home", "front_door", "clip.mp4"), full);
    }

    [Fact]
    public async Task ToAbsolute_RootedPath_IsUnchanged()
    {
        string rooted = Path.Combine(Root, "clip.mp4");

        Assert.Equal(rooted, await Create().ToAbsoluteAsync(rooted, CancellationToken.None));
    }
}
