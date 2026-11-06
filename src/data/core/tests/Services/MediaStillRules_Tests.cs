using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Core.Tests.Services
{
    /// <summary>The still-image rule recognises JPEG and PNG file names, in any letter case, and nothing else.</summary>
    public class MediaStillRules_Tests
    {
        [Theory]
        [InlineData("snap.jpg")]
        [InlineData("SNAP.JPG")]
        [InlineData("snap.jpeg")]
        [InlineData("snap.Jpeg")]
        [InlineData("snap.png")]
        [InlineData("SNAP.PNG")]
        public void MediaStillRules_IsStill_TrueForJpgJpegPng_CaseInsensitive(string fileName)
        {
            Assert.True(MediaStillRules.IsStill(fileName));
        }

        [Theory]
        [InlineData("clip.mp4")]
        [InlineData("clip.mkv")]
        [InlineData("snap.jpg.mp4")]
        [InlineData("jpgfile")]
        [InlineData("")]
        [InlineData(null)]
        public void MediaStillRules_IsStill_FalseForVideoAndNull(string? fileName)
        {
            Assert.False(MediaStillRules.IsStill(fileName));
        }
    }
}
