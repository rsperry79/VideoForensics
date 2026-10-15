namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services;

public class MediaFormatHelper_IsImage_Tests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/png")]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("JPG")]
    [InlineData("JPEG")]
    [InlineData("PNG")]
    [InlineData("IMAGE/JPEG")]
    public void IsImage_WithImageFormats_ReturnsTrue(string format)
    {
        var result = MediaFormatHelper.IsImage(format);
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mp4")]
    [InlineData("video/mp4")]
    [InlineData("application/octet-stream")]
    public void IsImage_WithNonImageFormats_ReturnsFalse(string? format)
    {
        var result = MediaFormatHelper.IsImage(format);
        Assert.False(result);
    }
}

public class MediaFormatHelper_IsVideo_Tests
{
    [Theory]
    [InlineData("video/mp4")]
    [InlineData("mp4")]
    [InlineData("MP4")]
    [InlineData("video")]
    [InlineData("VIDEO")]
    [InlineData("VIDEO/MP4")]
    public void IsVideo_WithVideoFormats_ReturnsTrue(string format)
    {
        var result = MediaFormatHelper.IsVideo(format);
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jpg")]
    [InlineData("image/jpeg")]
    [InlineData("application/octet-stream")]
    public void IsVideo_WithNonVideoFormats_ReturnsFalse(string? format)
    {
        var result = MediaFormatHelper.IsVideo(format);
        Assert.False(result);
    }
}
