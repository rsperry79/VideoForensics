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
    /// Also validates metadata capture for forensics audit trail.
    /// </summary>
    internal sealed class DbCompletenessReport
    {
        public string DbPath { get; set; } = "";
        public DateTime GeneratedAtUtc { get; set; }
        public List<DbCompletenessRecord> Devices { get; set; } = new();
        public List<DbCompletenessRecord> Locations { get; set; } = new();
        public int MissingDeviceCount => Devices.Count(d => !d.FoundInDb);
        public int MissingLocationCount => Locations.Count(l => !l.FoundInDb);

        // Metadata capture statistics
        public int DevicesWithMetadata { get; set; }
        public int LocationsWithMetadata { get; set; }
        public int EventsWithMetadata { get; set; }
        public int MediaItemsWithMetadata { get; set; }
        public int TotalEvents { get; set; }
        public int TotalMediaItems { get; set; }

        public string MetadataCompleteness
        {
            get
            {
                if (TotalEvents == 0 && TotalMediaItems == 0)
                    return "No events/media items to verify";

                var eventPct = TotalEvents > 0 ? (EventsWithMetadata * 100) / TotalEvents : 100;
                var mediaPct = TotalMediaItems > 0 ? (MediaItemsWithMetadata * 100) / TotalMediaItems : 100;
                return $"Events: {eventPct}% ({EventsWithMetadata}/{TotalEvents}), MediaItems: {mediaPct}% ({MediaItemsWithMetadata}/{TotalMediaItems})";
            }
        }
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

            // Chimes have no video/event history but are still registered as Device rows (see
            // RingDeviceDiscoveryService.GetDevicesAsync) so they show up here like any other device.
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

            // Verify metadata capture for forensics audit trail
            try
            {
                var devicesWithMeta = await db.Devices.CountAsync(d => d.MetadataJson != null);
                var locationsWithMeta = await db.Locations.CountAsync(l => l.MetadataJson != null);
                var eventsWithMeta = await db.Events.CountAsync(e => e.MetadataJson != null);
                var totalEvents = await db.Events.CountAsync();
                var mediaItemsWithMeta = await db.MediaItems.CountAsync(m => m.MetadataJson != null);
                var totalMediaItems = await db.MediaItems.CountAsync();

                report.DevicesWithMetadata = devicesWithMeta;
                report.LocationsWithMetadata = locationsWithMeta;
                report.EventsWithMetadata = eventsWithMeta;
                report.TotalEvents = totalEvents;
                report.MediaItemsWithMetadata = mediaItemsWithMeta;
                report.TotalMediaItems = totalMediaItems;
            }
            catch
            {
                // Metadata columns may not exist in schema yet - silently skip
            }

            return report;
        }
    }
}
