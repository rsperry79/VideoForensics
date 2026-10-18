using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Core.DependencyInjection;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>
    /// Verifies IMediaBitrateAnalyzer is transient so it re-reads IFfmpegToolLocation.BinDirectory on each resolve
    /// instead of freezing the bin directory captured at first resolve (which may precede config load).
    /// </summary>
    public class MediaBitrateAnalyzerRegistrationTests
    {
        [Fact]
        public void IMediaBitrateAnalyzer_ResolvedBeforeAndAfterBinDirectorySet_IsNewInstanceEachTime()
        {
            var services = new ServiceCollection();
            _ = services.AddVideoForensicsDataCore();
            using ServiceProvider provider = services.BuildServiceProvider();

            IFfmpegToolLocation location = provider.GetRequiredService<IFfmpegToolLocation>();
            IMediaBitrateAnalyzer first = provider.GetRequiredService<IMediaBitrateAnalyzer>();

            location.BinDirectory = Path.GetTempPath();
            IMediaBitrateAnalyzer second = provider.GetRequiredService<IMediaBitrateAnalyzer>();

            Assert.NotSame(first, second);
        }

        [Fact]
        public void IClipColorAnalyzer_ResolvedBeforeAndAfterBinDirectorySet_IsNewInstanceEachTime()
        {
            var services = new ServiceCollection();
            _ = services.AddVideoForensicsDataCore();
            using ServiceProvider provider = services.BuildServiceProvider();

            IFfmpegToolLocation location = provider.GetRequiredService<IFfmpegToolLocation>();
            IClipColorAnalyzer first = provider.GetRequiredService<IClipColorAnalyzer>();

            location.BinDirectory = Path.GetTempPath();
            IClipColorAnalyzer second = provider.GetRequiredService<IClipColorAnalyzer>();

            Assert.NotSame(first, second);
        }

        [Fact]
        public void IAudioClipAnalyzer_ResolvedBeforeAndAfterBinDirectorySet_IsNewInstanceEachTime()
        {
            var services = new ServiceCollection();
            _ = services.AddVideoForensicsDataCore();
            using ServiceProvider provider = services.BuildServiceProvider();

            IFfmpegToolLocation location = provider.GetRequiredService<IFfmpegToolLocation>();
            IAudioClipAnalyzer first = provider.GetRequiredService<IAudioClipAnalyzer>();

            location.BinDirectory = Path.GetTempPath();
            IAudioClipAnalyzer second = provider.GetRequiredService<IAudioClipAnalyzer>();

            Assert.NotSame(first, second);
        }

        [Fact]
        public void ITimelineVideoBuilder_ResolvedBeforeAndAfterBinDirectorySet_IsNewInstanceEachTime()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddVideoForensicsDataCore();
            using ServiceProvider provider = services.BuildServiceProvider();

            IFfmpegToolLocation location = provider.GetRequiredService<IFfmpegToolLocation>();
            ITimelineVideoBuilder first = provider.GetRequiredService<ITimelineVideoBuilder>();

            location.BinDirectory = Path.GetTempPath();
            ITimelineVideoBuilder second = provider.GetRequiredService<ITimelineVideoBuilder>();

            Assert.NotSame(first, second);
        }
    }
}
