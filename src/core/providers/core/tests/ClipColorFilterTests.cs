using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class ClipColorFilterTests
    {
        [Fact]
        public void FilterChain_ConvertsToEightBitBeforeMeasuring()
        {
            // signalstats reports luma in the stream's own bit depth; converting first keeps 10-bit clips on the 0-255 scale.
            string chain = FfmpegClipColorAnalyzer.FilterChain;

            int convert = chain.IndexOf("format=yuv420p", StringComparison.Ordinal);
            int measure = chain.IndexOf("signalstats", StringComparison.Ordinal);

            Assert.True(convert >= 0);
            Assert.True(convert < measure);
        }
    }
}
