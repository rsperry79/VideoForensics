using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Hosting;

// Build services
var services = new ServiceCollection();
services.AddVideoForensicsDataLayer();
var provider = services.BuildServiceProvider();

// Get database context
var factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
await using var db = await factory.CreateDbContextAsync();

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("DATABASE REDUNDANCY DIAGNOSTIC REPORT");
Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

// SECTION 1: Duplicate Provider IDs
Console.WriteLine("SECTION 1: DUPLICATE PROVIDER IDS (Missing Unique Constraints)");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var duplicateDevices = await db.Devices
    .GroupBy(d => new { d.LocationId, d.ProviderDeviceId })
    .Where(g => g.Count() > 1)
    .Select(g => new
    {
        LocationId = g.Key.LocationId,
        ProviderDeviceId = g.Key.ProviderDeviceId,
        Count = g.Count(),
        DeviceIds = string.Join(", ", g.Select(d => d.Id.ToString().Substring(0, 8)))
    })
    .ToListAsync();

if (duplicateDevices.Count > 0)
{
    Console.WriteLine($"⚠️  FOUND {duplicateDevices.Count} duplicate device entries:");
    foreach (var dup in duplicateDevices.Take(10))
    {
        Console.WriteLine($"  • Location: {dup.LocationId.ToString().Substring(0, 8)}..., ProviderDeviceId: {dup.ProviderDeviceId}, Count: {dup.Count}");
    }
}
else
{
    Console.WriteLine("✅ No duplicate devices found.");
}

var duplicateEvents = await db.Events
    .GroupBy(e => new { e.DeviceId, e.ProviderEventId })
    .Where(g => g.Count() > 1)
    .Select(g => new
    {
        DeviceId = g.Key.DeviceId,
        ProviderEventId = g.Key.ProviderEventId,
        Count = g.Count()
    })
    .ToListAsync();

if (duplicateEvents.Count > 0)
{
    Console.WriteLine($"\n⚠️  FOUND {duplicateEvents.Count} duplicate event entries:");
    foreach (var dup in duplicateEvents.Take(10))
    {
        Console.WriteLine($"  • Device: {dup.DeviceId.ToString().Substring(0, 8)}..., ProviderEventId: {dup.ProviderEventId}, Count: {dup.Count}");
    }
}
else
{
    Console.WriteLine("\n✅ No duplicate events found.");
}

// SECTION 2: Redundant Detection Data
Console.WriteLine("\n\nSECTION 2: REDUNDANT DETECTION DATA");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var mediaDetectionCount = await db.MediaItemDetections.CountAsync();
var eventDetectionCount = await db.EventDetections.CountAsync();

Console.WriteLine($"MediaItemDetection rows: {mediaDetectionCount:N0}");
Console.WriteLine($"EventDetection rows:     {eventDetectionCount:N0}");

if (mediaDetectionCount > 0 && eventDetectionCount > 0)
{
    var avgMediaDetections = Math.Round((double)mediaDetectionCount / await db.MediaItems.CountAsync(), 2);
    var avgEventDetections = Math.Round((double)eventDetectionCount / await db.Events.CountAsync(), 2);
    Console.WriteLine($"\nAverage detections per MediaItem: {avgMediaDetections}");
    Console.WriteLine($"Average detections per Event:     {avgEventDetections}");
}

// SECTION 3: Device Health
Console.WriteLine("\n\nSECTION 3: DEVICE HEALTH");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var deviceHealthCount = await db.DeviceHealths.CountAsync();
Console.WriteLine($"DeviceHealth rows: {deviceHealthCount:N0}");

var healthDates = await db.DeviceHealths
    .GroupBy(h => h.CapturedAtUtc.Date)
    .OrderByDescending(g => g.Key)
    .Select(g => new { Date = g.Key, Count = g.Count() })
    .Take(5)
    .ToListAsync();

if (healthDates.Count > 0)
{
    Console.WriteLine($"\n   Recent dates:");
    foreach (var date in healthDates)
    {
        Console.WriteLine($"     • {date.Date}: {date.Count} records");
    }
}

// SECTION 4: Device Features Redundancy
Console.WriteLine("\n\nSECTION 4: DEVICE FEATURES REDUNDANCY");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var deviceCapabilitiesCount = await db.DeviceCapabilities.CountAsync();
var deviceFeaturesCount = await db.DeviceFeatures.CountAsync();

Console.WriteLine($"DeviceCapabilities rows:  {deviceCapabilitiesCount:N0}");
Console.WriteLine($"DeviceFeatures rows:      {deviceFeaturesCount:N0}");

var bothTables = await db.DeviceCapabilities
    .Join(db.DeviceFeatures, dc => dc.DeviceId, df => df.DeviceId, (dc, df) => new { dc.DeviceId })
    .Select(x => x.DeviceId)
    .Distinct()
    .CountAsync();

var capOnly = await db.DeviceCapabilities
    .Where(dc => !db.DeviceFeatures.Any(df => df.DeviceId == dc.DeviceId))
    .CountAsync();

var featuresOnly = await db.DeviceFeatures
    .Where(df => !db.DeviceCapabilities.Any(dc => dc.DeviceId == df.DeviceId))
    .CountAsync();

Console.WriteLine($"\nDevices with both tables:     {bothTables}");
Console.WriteLine($"Devices with Capabilities only: {capOnly}");
Console.WriteLine($"Devices with Features only:     {featuresOnly}");

if (bothTables > 0)
{
    Console.WriteLine($"\n⚠️  {bothTables} devices have BOTH DeviceCapabilities and DeviceFeatures!");
}

// SECTION 5: Data Quality Metrics
Console.WriteLine("\n\nSECTION 6: OVERALL TABLE SIZES");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var tableStats = new[]
{
    ("MediaItems", await db.MediaItems.CountAsync()),
    ("Events", await db.Events.CountAsync()),
    ("Devices", await db.Devices.CountAsync()),
    ("Locations", await db.Locations.CountAsync()),
    ("Detections (both)", mediaDetectionCount + eventDetectionCount),
    ("DeviceHealth", deviceHealthCount),
};

foreach (var (name, count) in tableStats.OrderByDescending(x => x.Item2))
{
    Console.WriteLine($"  {name,-30} {count:N0} rows");
}

// SECTION 7: Summary
Console.WriteLine("\n\nSUMMARY & RECOMMENDATIONS");
Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

var issues = new List<string>();

if (duplicateDevices.Count > 0)
    issues.Add($"❌ {duplicateDevices.Count} duplicate devices (fix with unique constraint)");
if (duplicateEvents.Count > 0)
    issues.Add($"❌ {duplicateEvents.Count} duplicate events (fix with unique constraint)");
if (bothTables > 0)
    issues.Add($"⚠️  {bothTables} devices with both DeviceCapabilities AND DeviceFeatures (merge needed)");

if (issues.Count == 0)
{
    Console.WriteLine("✅ Database looks clean! No critical redundancy issues detected.");
}
else
{
    Console.WriteLine("Issues found:");
    foreach (var issue in issues)
    {
        Console.WriteLine($"  {issue}");
    }
}

// SECTION 7: Check for orphaned records
Console.WriteLine("\n\nSECTION 7: ORPHANED RECORDS (Foreign Key Violations)");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var orphanedEvents = await db.Events
    .Where(e => !db.Devices.Any(d => d.Id == e.DeviceId))
    .CountAsync();

var orphanedMediaItems = await db.MediaItems
    .Where(m => !db.Devices.Any(d => d.Id == m.DeviceId))
    .CountAsync();

var orphanedDetections = await db.MediaItemDetections
    .Where(mid => !db.MediaItems.Any(m => m.Id == mid.MediaItemId))
    .CountAsync();

var orphanedEventDetections = await db.EventDetections
    .Where(ed => !db.Events.Any(e => e.Id == ed.EventId))
    .CountAsync();

var orphanedDevices = await db.Devices
    .Where(d => !db.Locations.Any(l => l.Id == d.LocationId))
    .CountAsync();

var orphanedDownloadEvents = await db.DownloadEvents
    .Where(de => !db.Devices.Any(d => d.Id == de.DeviceId))
    .CountAsync();

if (orphanedEvents > 0)
    Console.WriteLine($"⚠️  {orphanedEvents} orphaned Events (Device deleted?)");
if (orphanedMediaItems > 0)
    Console.WriteLine($"⚠️  {orphanedMediaItems} orphaned MediaItems (Device deleted?)");
if (orphanedDetections > 0)
    Console.WriteLine($"⚠️  {orphanedDetections} orphaned MediaItemDetections (MediaItem deleted?)");
if (orphanedEventDetections > 0)
    Console.WriteLine($"⚠️  {orphanedEventDetections} orphaned EventDetections (Event deleted?)");
if (orphanedDevices > 0)
    Console.WriteLine($"⚠️  {orphanedDevices} orphaned Devices (Location deleted?)");
if (orphanedDownloadEvents > 0)
    Console.WriteLine($"⚠️  {orphanedDownloadEvents} orphaned DownloadEvents (ProviderAccount deleted?)");

if (orphanedEvents == 0 && orphanedMediaItems == 0 && orphanedDetections == 0 &&
    orphanedEventDetections == 0 && orphanedDevices == 0 && orphanedDownloadEvents == 0)
    Console.WriteLine("✅ No orphaned records found.");

Console.WriteLine("\nNext steps:");
Console.WriteLine("  1. Run the SQL queries in db_duplicate_check.sql for detailed analysis");
Console.WriteLine("  2. Review the consolidation roadmap in database_redundancy_analysis.md");
Console.WriteLine("  3. Backup database before making structural changes");
