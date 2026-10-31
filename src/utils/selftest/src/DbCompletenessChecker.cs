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

        /// <summary>
        /// Response fields per endpoint that have no structured column anywhere in the schema.
        /// A field that only survives inside a *Json column does not count as stored.
        /// </summary>
        public Dictionary<string, List<string>> UnmappedFieldsByEndpoint { get; set; } = [];

        public int UnmappedFieldCount => UnmappedFieldsByEndpoint.Values.Sum(v => v.Count);
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
                report.UnmappedFieldsByEndpoint = FindUnmappedFields(db, outputDir);
            }

            return report;
        }

        /// <summary>Endpoints whose response keys are data (flag names), not columns; stored as rows.</summary>
        private static readonly HashSet<string> KeyedMapEndpoints = new(StringComparer.Ordinal) { "profile" };

        /// <summary>
        /// Compares every scalar key in each saved endpoint response with the non-JSON columns of the
        /// EF model. Names are compared ignoring case and punctuation (snake_case vs PascalCase).
        /// </summary>
        private static Dictionary<string, List<string>> FindUnmappedFields(VideoForensicsDbContext db, string outputDir)
        {
            var columns = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in db.Model.GetEntityTypes())
            {
                foreach (var prop in entity.GetProperties().Where(p => !p.Name.EndsWith("Json", StringComparison.Ordinal)))
                {
                    _ = columns.Add(Normalize(prop.Name));
                }
            }

            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(outputDir, "*.json"))
            {
                string name = Path.GetFileName(file);
                if (name is "index.json" or "db-completeness.json")
                {
                    continue;
                }

                string endpoint = name[..name.IndexOf('_', StringComparison.Ordinal)];
                if (KeyedMapEndpoints.Contains(endpoint))
                {
                    continue;
                }

                var keys = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                    CollectLeafKeys(doc.RootElement, keys);
                }
                catch (System.Text.Json.JsonException)
                {
                    continue;
                }

                var missing = keys.Where(k => !columns.Contains(Normalize(k))).ToList();
                if (missing.Count == 0)
                {
                    continue;
                }

                if (!result.TryGetValue(endpoint, out List<string>? existing))
                {
                    result[endpoint] = existing = [];
                }

                existing.AddRange(missing.Where(m => !existing.Contains(m)));
                existing.Sort(StringComparer.Ordinal);
            }

            return result;
        }

        private static void CollectLeafKeys(System.Text.Json.JsonElement element, HashSet<string> keys)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (System.Text.Json.JsonProperty p in element.EnumerateObject())
                    {
                        if (p.Value.ValueKind is System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array)
                        {
                            CollectLeafKeys(p.Value, keys);
                        }
                        else
                        {
                            _ = keys.Add(p.Name);
                        }
                    }

                    break;
                case System.Text.Json.JsonValueKind.Array:
                    foreach (System.Text.Json.JsonElement item in element.EnumerateArray())
                    {
                        CollectLeafKeys(item, keys);
                    }

                    break;
            }
        }

        private static string Normalize(string name)
        {
            return new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}
