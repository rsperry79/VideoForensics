using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Catalog;
using VideoForensics.Providers.Ring.Interfaces;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the Ring model catalog into the reference-data rows the seeder loads.</summary>
    public class RingReferenceDataSourceTests
    {
        private readonly IRingModelCatalog _catalog = new RingModelCatalog();

        private RingReferenceDataSource Source()
        {
            return new(_catalog);
        }

        [Fact]
        public void Name_And_Version_MatchTheCatalog()
        {
            IReferenceDataSource source = Source();

            Assert.Equal("Ring", source.Name);
            Assert.Equal(RingModelCatalog.Version, source.Version);
        }

        [Fact]
        public void GetDataSet_ModelCount_MatchesTheCatalog()
        {
            ReferenceDataSet data = Source().GetDataSet();

            Assert.Equal(_catalog.All.Count, data.Models.Count);
            Assert.All(data.Models, m => Assert.Equal("Ring", m.Provider));
        }

        [Fact]
        public void GetDataSet_ChimePro_LinksChimeAndWifiBridge()
        {
            ReferenceDataSet data = Source().GetDataSet();

            var features = data.ModelFeatures
                .Where(l => l.DeviceModelKind == "chime_pro_v2")
                .Select(l => l.FeatureCode)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(new[] { "chime", "wifi_bridge" }, features);
        }

        [Fact]
        public void GetDataSet_Doorbell_LinksCameraOnly()
        {
            ReferenceDataSet data = Source().GetDataSet();

            var features = data.ModelFeatures
                .Where(l => l.DeviceModelKind == "doorbell_v4")
                .Select(l => l.FeatureCode)
                .ToList();

            Assert.Equal(new[] { "camera" }, features);
        }

        [Fact]
        public void GetDataSet_EveryLinkPointsAtAModelAndAFeature()
        {
            ReferenceDataSet data = Source().GetDataSet();
            var kinds = data.Models.Select(m => m.Kind).ToHashSet(StringComparer.Ordinal);
            var codes = data.Features.Select(f => f.Code).ToHashSet(StringComparer.Ordinal);

            Assert.All(data.ModelFeatures, l =>
            {
                Assert.Contains(l.DeviceModelKind, kinds);
                Assert.Contains(l.FeatureCode, codes);
            });
        }
    }
}
