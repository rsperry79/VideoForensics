using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class MediaStreamFieldsTests
    {
        [Fact]
        public void FromProbe_WithStreamInfo_CarriesCodecResolutionAndFrameRate()
        {
            var analysis = MediaBitrateAnalysis.FromProbe(
                TimeSpan.FromSeconds(60), 1_200_000, 1000,
                videoCodec: "h264", audioCodec: "aac", width: 1920, height: 1080, frameRate: 29.97);

            Assert.NotNull(analysis);
            Assert.Equal("h264", analysis.VideoCodec);
            Assert.Equal("aac", analysis.AudioCodec);
            Assert.Equal("1920x1080", analysis.Resolution);
            Assert.Equal(29.97m, analysis.FrameRate);
        }

        [Fact]
        public void FromProbe_WithoutStreamInfo_LeavesStreamFieldsNull()
        {
            var analysis = MediaBitrateAnalysis.FromProbe(TimeSpan.FromSeconds(60), 1_200_000, 1000);

            Assert.NotNull(analysis);
            Assert.Null(analysis.VideoCodec);
            Assert.Null(analysis.Resolution);
            Assert.Null(analysis.FrameRate);
        }
    }
}
