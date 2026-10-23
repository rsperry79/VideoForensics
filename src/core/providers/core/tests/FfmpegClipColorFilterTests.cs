using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    /// <summary>A still image needs its own colour chain: fps=1 drops the single frame and leaves nothing to measure.</summary>
    public class FfmpegClipColorFilterTests
    {
        [Theory]
        [InlineData("snapshot.jpg")]
        [InlineData("SNAPSHOT.JPEG")]
        [InlineData("frame.png")]
        public void Still_UsesChainWithoutFpsSampler(string path)
        {
            Assert.Equal(FfmpegClipColorAnalyzer.StillFilterChain, FfmpegClipColorAnalyzer.FilterFor(path));
            Assert.DoesNotContain("fps=", FfmpegClipColorAnalyzer.StillFilterChain);
        }

        [Theory]
        [InlineData("clip.mp4")]
        [InlineData("loop.mkv")]
        public void Video_KeepsOneFrameASecondSampler(string path)
        {
            Assert.Equal(FfmpegClipColorAnalyzer.FilterChain, FfmpegClipColorAnalyzer.FilterFor(path));
            Assert.Contains("fps=1", FfmpegClipColorAnalyzer.FilterChain);
        }
    }
}
