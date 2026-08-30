using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.SelfTester
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
    /// </summary>
    internal sealed class DbCompletenessReport
    {
        public string DbPath { get; set; } = "";
        public DateTime GeneratedAtUtc { get; set; }
        public List<DbCompletenessRecord> Devices { get; set; } = new();
        public List<DbCompletenessRecord> Locations { get; set; } = new();
        public int MissingDeviceCount => Devices.Count(d => !d.FoundInDb);
        public int MissingLocationCount => Locations.Count(l => !l.FoundInDb);
    }

    internal static class DbCompletenessChecker
    {
        public static async Task<DbCompletenessReport> CheckAsync(
            Devices? devices,
            List<Location>? locations,
            string dbPath)
        {
            var report = new DbCompletenessReport { DbPath = dbPath, GeneratedAtUtc = DateTime.UtcNow };

            var connectionString = $"Data Source={dbPath};Pooling=true;Cache=Shared;Default Timeout=5";
            var optionsBuilder = new DbContextOptionsBuilder<VideoForensicsDbContext>();
            optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"));

            await using var db = new VideoForensicsDbContext(optionsBuilder.Options);

            var dbDeviceIdSet = new HashSet<string>(
                await db.Devices.Select(d => d.ProviderDeviceId).ToListAsync(), StringComparer.Ordinal);
            var dbLocationIdSet = new HashSet<string>(
                await db.Locations.Select(l => l.ProviderLocationId).ToListAsync(), StringComparer.Ordinal);

            void AddDevice(string kind, string providerId, string? name) =>
                report.Devices.Add(new DbCompletenessRecord
                {
                    Kind = kind,
                    ProviderId = providerId,
                    Name = name,
                    FoundInDb = dbDeviceIdSet.Contains(providerId)
                });

            if (devices?.Doorbots != null)
            {
                foreach (var d in devices.Doorbots)
                {
                    AddDevice("Doorbot", d.Id.ToString(), d.Description);
                }
            }

            if (devices?.StickupCams != null)
            {
                foreach (var d in devices.StickupCams.Where(d => d.Id.HasValue))
                {
                    AddDevice("StickupCam", d.Id!.Value.ToString(), d.Description);
                }
            }

            if (devices?.AuthorizedDoorbots != null)
            {
                var alreadySeen = new HashSet<string>(report.Devices.Select(x => x.ProviderId), StringComparer.Ordinal);
                foreach (var d in devices.AuthorizedDoorbots.Where(d => !alreadySeen.Contains(d.Id.ToString())))
                {
                    AddDevice("AuthorizedDoorbot", d.Id.ToString(), d.Description);
                }
            }

            // Chimes are not currently mapped into Device rows by the main app's device discovery
            // (only Doorbots/StickupCams/AuthorizedDoorbots are - see RingDeviceDiscoveryService),
            // so every chime will show FoundInDb=false today. That reflects reality, not a bug in
            // this checker - it's exactly the kind of gap this report exists to surface.
            if (devices?.Chimes != null)
            {
                foreach (var c in devices.Chimes)
                {
                    AddDevice("Chime", c.Id.ToString(), c.Description);
                }
            }

            if (locations != null)
            {
                foreach (var l in locations.Where(l => l.Id.HasValue))
                {
                    var providerId = l.Id!.Value.ToString();
                    report.Locations.Add(new DbCompletenessRecord
                    {
                        Kind = "Location",
                        ProviderId = providerId,
                        Name = l.Name,
                        FoundInDb = dbLocationIdSet.Contains(providerId)
                    });
                }
            }

            return report;
        }
    }
}
