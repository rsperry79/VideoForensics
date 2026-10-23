using VideoForensics.Client.Ui.Shared.Services.Evidence;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Evidence;

public class MediaViewerKeys_ImageActions_Tests
{
    [Theory]
    [InlineData("+")]
    [InlineData("=")]
    public void Map_PlusOrEqual_ReturnsZoomIn(string key)
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(key, isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.ZoomIn, action);
    }

    [Fact]
    public void Map_Minus_ReturnsZoomOut()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map("-", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.ZoomOut, action);
    }
}

public class MediaViewerKeys_NavigationActions_Tests
{
    [Fact]
    public void Map_ArrowLeft_ReturnsPrevious()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map("ArrowLeft", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.Previous, action);
    }

    [Fact]
    public void Map_ArrowRight_ReturnsNext()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map("ArrowRight", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.Next, action);
    }

    [Fact]
    public void Map_Escape_ReturnsClose()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map("Escape", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.Close, action);
    }
}

public class MediaViewerKeys_VideoActions_Tests
{
    [Fact]
    public void Map_Space_ReturnsTogglePlayForVideo()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(" ", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.TogglePlay, action);
    }

    [Fact]
    public void Map_Space_ReturnsNoneForImage()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(" ", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_Comma_ReturnsFrameBackForVideo()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(",", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.FrameBack, action);
    }

    [Fact]
    public void Map_Comma_ReturnsNoneForImage()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(",", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_Period_ReturnsFrameForwardForVideo()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(".", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.FrameForward, action);
    }

    [Fact]
    public void Map_Period_ReturnsNoneForImage()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(".", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("S")]
    public void Map_S_ReturnsGrabStillForVideo(string key)
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(key, isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.GrabStill, action);
    }

    [Fact]
    public void Map_S_ReturnsNoneForImage()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map("s", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }
}

public class MediaViewerKeys_UnknownKeys_Tests
{
    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("Enter")]
    [InlineData("")]
    public void Map_UnknownKey_ReturnsNone(string key)
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(key, isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_NullKey_ReturnsNone()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(null, isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }
}

public class MediaViewerKeys_ZoomKeysOnVideo_Tests
{
    [Theory]
    [InlineData("+")]
    [InlineData("=")]
    public void Map_ZoomInKey_ReturnsNoneForVideo(string key)
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map(key, isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_ZoomOutKey_ReturnsNoneForVideo()
    {
        // Act
        MediaViewerAction action = MediaViewerKeys.Map("-", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }
}
