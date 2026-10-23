using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class MediaBitrateAnalysisTests
    {
        private const string StreamBitrateJson = """
            {
              "format": { "duration": "60.000000", "bit_rate": "1500000" },
              "streams": [
                { "codec_type": "video", "bit_rate": "1200000" },
                { "codec_type": "audio", "bit_rate": "96000" }
              ]
            }
            """;

        private const string NoStreamBitrateJson = """
            {
              "format": { "duration": "10.000000" },
              "streams": [ { "codec_type": "video" } ]
            }
            """;

        [Fact]
        public void FromFfprobeJson_VideoStreamReportsBitrate_UsesStreamBitrate()
        {
            var result = MediaBitrateAnalysis.FromFfprobeJson(StreamBitrateJson, fileSizeBytes: 9_000_000);

            Assert.NotNull(result);
            Assert.Equal(1_200_000, result.VideoBitrateBps);
            Assert.Equal(60.0, result.DurationSeconds);
            Assert.False(result.BitrateEstimated);
        }

        [Fact]
        public void FromFfprobeJson_NoStreamBitrate_EstimatesFromFileSizeAndDuration()
        {
            // 10 MB over 10 s = 8,000,000 bits/s.
            var result = MediaBitrateAnalysis.FromFfprobeJson(NoStreamBitrateJson, fileSizeBytes: 10_000_000);

            Assert.NotNull(result);
            Assert.Equal(8_000_000, result.VideoBitrateBps);
            Assert.True(result.BitrateEstimated);
        }

        [Fact]
        public void FromFfprobeJson_NoDuration_ReturnsNull()
        {
            const string json = """{ "format": {}, "streams": [ { "codec_type": "video" } ] }""";

            Assert.Null(MediaBitrateAnalysis.FromFfprobeJson(json, fileSizeBytes: 1_000_000));
        }

        [Fact]
        public void FromFfprobeJson_MalformedOutput_ReturnsNull()
        {
            Assert.Null(MediaBitrateAnalysis.FromFfprobeJson("not json", fileSizeBytes: 1_000_000));
        }
    }
}
