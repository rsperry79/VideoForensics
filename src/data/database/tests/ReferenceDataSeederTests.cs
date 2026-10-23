using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.ReferenceData;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Versioned reference-data seeding: loads on first run, re-applies only when a source's version moves.</summary>
    public class ReferenceDataSeederTests : IAsyncLifetime, IDisposable
    {
        private readonly SqliteInMemoryFixture _fixture = new();

        public async ValueTask InitializeAsync()
        {
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        private static ReferenceDataSet Set(string modelName, params string[] features)
        {
            var models = new List<DeviceModel>
            {
                new() { Kind = "chime_pro_v2", Provider = "Ring", Family = "Chime", ModelName = modelName, Generation = null },
            };
            var featureRows = features.Select(f => new DeviceFeatureDefinition { Code = f, Name = f }).ToList();
            var links = features.Select(f => new DeviceModelFeature { DeviceModelKind = "chime_pro_v2", FeatureCode = f }).ToList();
            return new ReferenceDataSet(models, featureRows, links);
        }

        private static FakeSource Source(int version, ReferenceDataSet data)
        {
            return new FakeSource("Ring", version, data);
        }

        [Fact]
        public async Task Seed_NewSource_InsertsModelsFeaturesLinksAndVersion()
        {
            var source = Source(1, Set("Chime Pro (2nd Gen)", "chime", "wifi_bridge"));

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [source], CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Single(db.DeviceModels);
            Assert.Equal(2, db.Set<DeviceModelFeature>().Count());
            ReferenceDataVersion version = await db.ReferenceDataVersions.SingleAsync();
            Assert.Equal("Ring", version.SourceName);
            Assert.Equal(1, version.Version);
        }

        [Fact]
        public async Task Seed_ManyToMany_ModelHasEveryLinkedFeature()
        {
            var source = Source(1, Set("Chime Pro (2nd Gen)", "chime", "wifi_bridge"));

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [source], CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            List<string> codes = await db.Set<DeviceModelFeature>()
                .Where(l => l.DeviceModelKind == "chime_pro_v2")
                .Select(l => l.FeatureCode)
                .OrderBy(c => c)
                .ToListAsync();
            Assert.Equal(new[] { "chime", "wifi_bridge" }, codes);
        }

        [Fact]
        public async Task Seed_SameVersionAgain_LeavesExistingRowsUntouched()
        {
            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, Set("Original"))], CancellationToken.None);
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                db.DeviceModels.Single().ModelName = "Edited locally";
                _ = await db.SaveChangesAsync();
            }

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, Set("Original"))], CancellationToken.None);

            await using VideoForensicsDbContext check = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal("Edited locally", (await check.DeviceModels.SingleAsync()).ModelName);
        }

        [Fact]
        public async Task Seed_HigherVersion_UpdatesChangedRowsAndVersion()
        {
            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, Set("Old name"))], CancellationToken.None);

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(2, Set("New name", "chime"))], CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal("New name", (await db.DeviceModels.SingleAsync()).ModelName);
            Assert.Single(db.Set<DeviceModelFeature>());
            Assert.Equal(2, (await db.ReferenceDataVersions.SingleAsync()).Version);
        }

        [Fact]
        public async Task Seed_TwoSources_EachKeepsItsOwnVersion()
        {
            var ring = Source(1, Set("Chime Pro (2nd Gen)", "chime"));
            var other = new FakeSource("Other", 5, new ReferenceDataSet([], [], []));

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [ring, other], CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Dictionary<string, int> versions = await db.ReferenceDataVersions.ToDictionaryAsync(v => v.SourceName, v => v.Version);
            Assert.Equal(1, versions["Ring"]);
            Assert.Equal(5, versions["Other"]);
        }

        [Fact]
        public async Task Seed_DiscoveredRowAndItsLinks_SurviveAVersionBump()
        {
            var camera = new List<DeviceFeatureDefinition> { new() { Code = "camera", Name = "Camera" } };
            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, new ReferenceDataSet([], camera, []))], CancellationToken.None);
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                db.DeviceModels.Add(new DeviceModel { Kind = "doorbell_brand_new", Provider = "Ring", Family = "Doorbell", Source = "Discovered" });
                db.DeviceModelFeatures.Add(new DeviceModelFeature { DeviceModelKind = "doorbell_brand_new", FeatureCode = "camera" });
                _ = await db.SaveChangesAsync();
            }

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(2, Set("Other"))], CancellationToken.None);

            await using VideoForensicsDbContext check = await _fixture.Factory.CreateDbContextAsync();
            Assert.True(await check.DeviceModels.AnyAsync(m => m.Kind == "doorbell_brand_new" && m.Source == "Discovered"));
            Assert.True(await check.DeviceModelFeatures.AnyAsync(l => l.DeviceModelKind == "doorbell_brand_new"));
        }

        [Fact]
        public async Task Seed_CatalogNowListsDiscoveredKind_PromotesRowToCatalog()
        {
            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, new ReferenceDataSet([], [], []))], CancellationToken.None);
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                db.DeviceModels.Add(new DeviceModel { Kind = "chime_pro_v2", Provider = "Ring", Family = "Chime", Source = "Discovered" });
                _ = await db.SaveChangesAsync();
            }

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(2, Set("Chime Pro (2nd Gen)", "chime"))], CancellationToken.None);

            await using VideoForensicsDbContext check = await _fixture.Factory.CreateDbContextAsync();
            DeviceModel row = await check.DeviceModels.SingleAsync(m => m.Kind == "chime_pro_v2");
            Assert.Equal("Catalog", row.Source);
            Assert.Equal("Chime Pro (2nd Gen)", row.ModelName);
        }

        [Fact]
        public async Task Seed_PromotedDiscoveredRow_KeepsOneRowAndReplacesItsLinks()
        {
            // Devices store the kind string, so promotion must keep the same key and drop the discovered row's own links.
            var camera = new List<DeviceFeatureDefinition> { new() { Code = "camera", Name = "Camera" } };
            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, new ReferenceDataSet([], camera, []))], CancellationToken.None);
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                db.DeviceModels.Add(new DeviceModel { Kind = "chime_pro_v2", Provider = "Ring", Family = "Unknown", Source = "Discovered" });
                db.DeviceModelFeatures.Add(new DeviceModelFeature { DeviceModelKind = "chime_pro_v2", FeatureCode = "camera" });
                _ = await db.SaveChangesAsync();
            }

            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(2, Set("Chime Pro (2nd Gen)", "chime", "wifi_bridge"))], CancellationToken.None);

            await using VideoForensicsDbContext check = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await check.DeviceModels.CountAsync(m => m.Kind == "chime_pro_v2"));
            DeviceModel row = await check.DeviceModels.SingleAsync(m => m.Kind == "chime_pro_v2");
            Assert.Equal("Catalog", row.Source);
            Assert.Equal("Chime", row.Family);
            List<string> links = await check.DeviceModelFeatures
                .Where(l => l.DeviceModelKind == "chime_pro_v2")
                .Select(l => l.FeatureCode)
                .OrderBy(c => c)
                .ToListAsync();
            Assert.Equal(new[] { "chime", "wifi_bridge" }, links);
        }

        [Fact]
        public async Task EnsureDiscoveredModel_UnknownKind_InsertsDiscoveredRowWithoutName()
        {
            await ReferenceDataSeeder.EnsureDiscoveredModelAsync(_fixture.Factory, "Ring", "doorbell_brand_new", "Doorbell", CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceModel row = await db.DeviceModels.SingleAsync(m => m.Kind == "doorbell_brand_new");
            Assert.Equal("Discovered", row.Source);
            Assert.Equal("Ring", row.Provider);
            Assert.Equal("Doorbell", row.Family);
            Assert.Null(row.ModelName);
        }

        [Fact]
        public async Task EnsureDiscoveredModel_KnownKind_LeavesExistingRowAlone()
        {
            await ReferenceDataSeeder.SeedAsync(_fixture.Factory, [Source(1, Set("Chime Pro (2nd Gen)", "chime"))], CancellationToken.None);

            await ReferenceDataSeeder.EnsureDiscoveredModelAsync(_fixture.Factory, "Ring", "chime_pro_v2", "Doorbell", CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceModel row = await db.DeviceModels.SingleAsync();
            Assert.Equal("Catalog", row.Source);
            Assert.Equal("Chime", row.Family);
            Assert.Equal("Chime Pro (2nd Gen)", row.ModelName);
        }

        private sealed class FakeSource(string name, int version, ReferenceDataSet data) : IReferenceDataSource
        {
            public string Name { get; } = name;

            public int Version { get; } = version;

            public ReferenceDataSet GetDataSet() => data;
        }
    }
}
