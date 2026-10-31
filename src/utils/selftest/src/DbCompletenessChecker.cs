using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>One Ring-reported item (device or location) and whether it exists in the local DB.</summary>
    internal sealed class DbCompletenessRecord
    {
        public string Kind { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string? Name { get; set; }
        public bool FoundInDb { get; set; }
    }

    /// <summary>
    /// Cross-checks every device/location the Ring API reports for this account against the
    /// VideoForensics app's own SQLite database, so a gap between "what Ring says exists" and
    /// "what actually got persisted" is visible without manually diffing JSON against SQL.
    /// Fields that exist only inside a *Json column are never counted as stored.
    /// </summary>
    internal sealed class DbCompletenessReport
    {
        public string DbPath { get; set; } = "";
        public DateTime GeneratedAtUtc { get; set; }
        public List<DbCompletenessRecord> Devices { get; set; } = [];
        public List<DbCompletenessRecord> Locations { get; set; } = [];
        public int MissingDeviceCount => Devices.Count(d => !d.FoundInDb);
        public int MissingLocationCount => Locations.Count(l => !l.FoundInDb);

        /// <summary>Number of entries in the checked-in field map (field-map.json) the response paths were checked against.</summary>
        public int FieldMapEntryCount { get; set; }

        /// <summary>
        /// Response paths checked against the explicit field map: unmapped paths, stale map entries, endpoints with no
        /// entries and paths that were null in every sample. A value kept only in a *Json column does not count as stored.
        /// Null when no saved responses were checked.
        /// </summary>
        public FieldCoverageReport? Fields { get; set; }
    }

    internal static class DbCompletenessChecker
    {
        public static async Task<DbCompletenessReport> CheckAsync(
            Devices? devices,
            List<Location>? locations,
            string dbPath,
            string? outputDir = null)
        {
            var report = new DbCompletenessReport { DbPath = dbPath, GeneratedAtUtc = DateTime.UtcNow };

            string connectionString = SqliteConnectionStrings.ForFile(dbPath);
            var optionsBuilder = new DbContextOptionsBuilder<VideoForensicsDbContext>();
            _ = optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"));

            await using var db = new VideoForensicsDbContext(optionsBuilder.Options);

            var dbDeviceIdSet = new HashSet<string>(
                await db.Devices.Select(d => d.ProviderDeviceId).ToListAsync(), StringComparer.Ordinal);
            var dbLocationIdSet = new HashSet<string>(
                await db.Locations.Select(l => l.ProviderLocationId).ToListAsync(), StringComparer.Ordinal);

            void AddDevice(string kind, string providerId, string? name)
            {
                report.Devices.Add(new DbCompletenessRecord
                {
                    Kind = kind,
                    ProviderId = providerId,
                    Name = name,
                    FoundInDb = dbDeviceIdSet.Contains(providerId)
                });
            }

            if (devices?.Doorbots != null)
            {
                foreach (Doorbot d in devices.Doorbots)
                {
                    AddDevice("Doorbot", d.Id.ToString(), d.Description);
                }
            }

            if (devices?.StickupCams != null)
            {
                foreach (StickupCam? d in devices.StickupCams.Where(d => d.Id.HasValue))
                {
                    AddDevice("StickupCam", d.Id!.Value.ToString(), d.Description);
                }
            }

            if (devices?.AuthorizedDoorbots != null)
            {
                var alreadySeen = new HashSet<string>(report.Devices.Select(x => x.ProviderId), StringComparer.Ordinal);
                foreach (Doorbot? d in devices.AuthorizedDoorbots.Where(d => !alreadySeen.Contains(d.Id.ToString())))
                {
                    AddDevice("AuthorizedDoorbot", d.Id.ToString(), d.Description);
                }
            }

            // Chimes have no video/event history but are still registered as Device rows (see
            // RingDeviceDiscoveryService.GetDevicesAsync) so they show up here like any other device.
            if (devices?.Chimes != null)
            {
                foreach (Chime c in devices.Chimes)
                {
                    AddDevice("Chime", c.Id.ToString(), c.Description);
                }
            }

            if (locations != null)
            {
                foreach (Location? l in locations.Where(l => l.Id.HasValue))
                {
                    string providerId = l.Id!.Value.ToString();
                    report.Locations.Add(new DbCompletenessRecord
                    {
                        Kind = "Location",
                        ProviderId = providerId,
                        Name = l.Name,
                        FoundInDb = dbLocationIdSet.Contains(providerId)
                    });
                }
            }

            if (outputDir is not null)
            {
                FieldMap map = FieldMap.LoadEmbedded();
                report.FieldMapEntryCount = map.Entries.Count;
                report.Fields = FieldCoverageAnalyzer.Analyze(outputDir, map, DbSchema.FromModel(db.Model));

                // Extension point: a later step passes an IFieldValueVerifier here to compare each mapped value with its
                // database row and fill FieldCoverageReport.ValueMismatches. Only the path map is checked today.
            }

            return report;
        }

        /// <summary>Writes the console summary of a completeness report (presence counts, then the field-map counts).</summary>
        internal static void WriteSummary(DbCompletenessReport report, TextWriter output)
        {
            foreach (DbCompletenessRecord? missing in report.Devices.Where(d => !d.FoundInDb))
            {
                output.WriteLine($"   MISSING device: {missing.Kind} {missing.ProviderId} ({missing.Name ?? "(unnamed)"})");
            }

            foreach (DbCompletenessRecord? missing in report.Locations.Where(l => !l.FoundInDb))
            {
                output.WriteLine($"   MISSING location: {missing.ProviderId} ({missing.Name ?? "(unnamed)"})");
            }

            output.WriteLine($"Devices: {report.Devices.Count - report.MissingDeviceCount}/{report.Devices.Count} found in DB. " +
                $"Locations: {report.Locations.Count - report.MissingLocationCount}/{report.Locations.Count} found in DB.");

            FieldCoverageReport? fields = report.Fields;
            if (fields is null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<string>> gap in fields.UnmappedByEndpoint.OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                output.WriteLine($"   UNMAPPED: {gap.Key} ({gap.Value.Count})");
            }

            foreach (StaleMapEntry stale in fields.StaleEntries)
            {
                output.WriteLine($"   STALE MAP ENTRY: {stale.Key} -> {stale.Target} ({stale.Reason})");
            }

            foreach (string endpoint in fields.EndpointsWithoutMapEntries)
            {
                output.WriteLine($"   NO MAP ENTRIES: {endpoint} returned data but the field map stores nothing for it");
            }

            foreach (SkippedResponseFile skipped in fields.SkippedFiles)
            {
                output.WriteLine($"   SKIPPED: {skipped.File} ({skipped.Reason})");
            }

            output.WriteLine($"Field map ({report.FieldMapEntryCount} entries): {fields.UnmappedPathCount} unmapped paths, " +
                $"{fields.StaleEntryCount} stale map entries, {fields.NullInSamplePathCount} null-in-sample paths (type unverified).");
        }
    }
}
