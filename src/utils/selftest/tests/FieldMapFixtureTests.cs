using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// Runs the checked-in field map against the sanitized endpoint fixtures (small copies of real responses with
    /// every value replaced) and against the EF model, so the map cannot drift from either.
    /// </summary>
    public class FieldMapFixtureTests
    {
        private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "fixtures");

        private static DbSchema CurrentSchema()
        {
            var options = new DbContextOptionsBuilder<VideoForensicsDbContext>().UseSqlite("Data Source=:memory:").Options;
            using var db = new VideoForensicsDbContext(options);
            return DbSchema.FromModel(db.Model);
        }

        [Fact]
        public void Fixtures_EveryLeafPath_HasAMapEntryOrANotDataReason()
        {
            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(FixtureDir, FieldMap.LoadEmbedded(), CurrentSchema());

            Assert.Empty(report.UnmappedByEndpoint.SelectMany(e => e.Value.Select(p => $"{e.Key}:{p}")));
            Assert.Empty(report.EndpointsWithoutMapEntries);
            Assert.Empty(report.SkippedFiles);
        }

        [Fact]
        public void Fixtures_CoverTheEndpointsTheMapDescribes()
        {
            FieldMap map = FieldMap.LoadEmbedded();
            string[] fixtureEndpoints = Directory.EnumerateFiles(FixtureDir, "*.json")
                .Select(f => Path.GetFileName(f)[..Path.GetFileName(f).IndexOf('_', StringComparison.Ordinal)])
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            string[] mapEndpoints = map.Entries.Keys.Select(k => k[..k.IndexOf(':', StringComparison.Ordinal)]).Distinct(StringComparer.Ordinal).ToArray();

            Assert.Empty(mapEndpoints.Except(fixtureEndpoints, StringComparer.Ordinal));
        }

        [Fact]
        public void Map_EveryEntry_ExistsInTheEfModel()
        {
            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(FixtureDir, FieldMap.LoadEmbedded(), CurrentSchema());

            Assert.Empty(report.StaleEntries.Select(s => $"{s.Key} -> {s.Target}: {s.Reason}"));
        }

        [Fact]
        public void Fixtures_AreSanitized_ContainNoEmailsOrUrls()
        {
            foreach (string file in Directory.EnumerateFiles(FixtureDir, "*.json"))
            {
                string text = File.ReadAllText(file);
                Assert.DoesNotContain("@", text);
                Assert.DoesNotContain("http", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
