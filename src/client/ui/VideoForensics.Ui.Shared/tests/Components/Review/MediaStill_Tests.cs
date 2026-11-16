using VideoForensics.Client.Ui.Shared.Components.Review;using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The still check: a clip whose file name or path is a JPEG or PNG image is a still, and every other clip is not.</summary>
public class MediaStill_Tests
{
    private static MediaItemDto Clip(string fileName, string filePath)
    {
        return new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = fileName,
            FilePath = filePath,
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow,
        }.ToDto();
    }

    [Fact]
    public void MediaStill_IsStill_TrueForJpgAndPng()
    {
        Assert.True(MediaStill.IsStill(Clip("snap.jpg", "/snap.jpg")));
        Assert.True(MediaStill.IsStill(Clip("snap.jpeg", "/snap.jpeg")));
        Assert.True(MediaStill.IsStill(Clip("snap.png", "/snap.png")));
        Assert.True(MediaStill.IsStill(Clip("snap", "/media/snap.jpg")));
    }

    [Fact]
    public void MediaStill_IsStill_FalseForMp4()
    {
        Assert.False(MediaStill.IsStill(Clip("clip.mp4", "/clip.mp4")));
        Assert.False(MediaStill.IsStill(Clip("clip.jpg.mp4", "/clip.jpg.mp4")));
    }

    [Fact]
    public void MediaStill_IsStill_CaseInsensitive()
    {
        Assert.True(MediaStill.IsStill(Clip("SNAP.JPG", "/SNAP.JPG")));
        Assert.True(MediaStill.IsStill(Clip("Snap.JpEg", "/Snap.JpEg")));
        Assert.True(MediaStill.IsStill(Clip("snap.PNG", "/snap.PNG")));
    }
}
