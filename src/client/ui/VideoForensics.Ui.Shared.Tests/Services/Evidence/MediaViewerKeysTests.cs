namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Ui.Shared.Services.Evidence;

public class MediaViewerKeys_ImageActions_Tests
{
    [Theory]
    [InlineData("+")]
    [InlineData("=")]
    public void Map_PlusOrEqual_ReturnsZoomIn(string key)
    {
        // Act
        var action = MediaViewerKeys.Map(key, isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.ZoomIn, action);
    }

    [Fact]
    public void Map_Minus_ReturnsZoomOut()
    {
        // Act
        var action = MediaViewerKeys.Map("-", isVideo: false);

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
        var action = MediaViewerKeys.Map("ArrowLeft", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.Previous, action);
    }

    [Fact]
    public void Map_ArrowRight_ReturnsNext()
    {
        // Act
        var action = MediaViewerKeys.Map("ArrowRight", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.Next, action);
    }

    [Fact]
    public void Map_Escape_ReturnsClose()
    {
        // Act
        var action = MediaViewerKeys.Map("Escape", isVideo: false);

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
        var action = MediaViewerKeys.Map(" ", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.TogglePlay, action);
    }

    [Fact]
    public void Map_Space_ReturnsNoneForImage()
    {
        // Act
        var action = MediaViewerKeys.Map(" ", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_Comma_ReturnsFrameBackForVideo()
    {
        // Act
        var action = MediaViewerKeys.Map(",", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.FrameBack, action);
    }

    [Fact]
    public void Map_Comma_ReturnsNoneForImage()
    {
        // Act
        var action = MediaViewerKeys.Map(",", isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_Period_ReturnsFrameForwardForVideo()
    {
        // Act
        var action = MediaViewerKeys.Map(".", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.FrameForward, action);
    }

    [Fact]
    public void Map_Period_ReturnsNoneForImage()
    {
        // Act
        var action = MediaViewerKeys.Map(".", isVideo: false);

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
        var action = MediaViewerKeys.Map(key, isVideo: false);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_NullKey_ReturnsNone()
    {
        // Act
        var action = MediaViewerKeys.Map(null, isVideo: false);

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
        var action = MediaViewerKeys.Map(key, isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }

    [Fact]
    public void Map_ZoomOutKey_ReturnsNoneForVideo()
    {
        // Act
        var action = MediaViewerKeys.Map("-", isVideo: true);

        // Assert
        Assert.Equal(MediaViewerAction.None, action);
    }
}
