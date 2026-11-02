using VideoForensics.Providers.Ring.Catalog;

using Xunit;

namespace VideoForensics.Providers.Ring.Common.Tests.Catalog
{
    /// <summary>Checks the official-spec resolution the catalog holds for a kind.</summary>
    public class RingModelResolutionTests
    {
        [Fact]
        public void Find_DoorbellGrahamCracker_IsVideoDoorbell4At1080p()
        {
            RingModelInfo? info = new RingModelCatalog().Find("doorbell_graham_cracker");

            Assert.NotNull(info);
            Assert.Equal("Video Doorbell 4", info!.ModelName);
            Assert.Equal("1920x1080", info.Resolution);
        }
    }
}
