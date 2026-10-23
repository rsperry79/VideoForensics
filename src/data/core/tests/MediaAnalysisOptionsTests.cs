using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class MediaAnalysisOptionsTests
    {
        [Fact]
        public void Options_Default_UsesNorthernHemisphere()
        {
            Assert.Equal(Hemisphere.Northern, new MediaAnalysisOptions().Hemisphere);
        }
    }
}
