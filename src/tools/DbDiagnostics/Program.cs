using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Diagnostics.Contracts;
using VideoForensics.Hosting;

// Build services
var services = new ServiceCollection();
services.AddVideoForensicsDataLayer();
services.AddScoped<IDatabaseHealthChecker, VideoForensics.Diagnostics.DatabaseHealthChecker>();
var provider = services.BuildServiceProvider();

// Get the health checker
var healthChecker = provider.GetRequiredService<IDatabaseHealthChecker>();
var ct = CancellationToken.None;

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("DATABASE REDUNDANCY DIAGNOSTIC REPORT");
Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

// SECTION 1: Duplicate Provider IDs
Console.WriteLine("SECTION 1: DUPLICATE PROVIDER IDS (Missing Unique Constraints)");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var duplicateDevices = await healthChecker.FindDuplicateDevicesAsync(ct);

if (duplicateDevices.Count > 0)
{
    Console.WriteLine($"⚠️  FOUND {duplicateDevices.Count} duplicate device entries:");
    foreach (var dup in duplicateDevices.Take(10))
    {
        Console.WriteLine($"  • Location: {dup.LocationId?.ToString().Substring(0, 8)}..., ProviderDeviceId: {dup.ProviderKey}, Count: {dup.Count}");
    }
}
else
{
    Console.WriteLine("✅ No duplicate devices found.");
}

var duplicateEvents = await healthChecker.FindDuplicateEventsAsync(ct);

if (duplicateEvents.Count > 0)
{
    Console.WriteLine($"\n⚠️  FOUND {duplicateEvents.Count} duplicate event entries:");
    foreach (var dup in duplicateEvents.Take(10))
    {
        Console.WriteLine($"  • Device: {dup.DeviceId?.ToString().Substring(0, 8)}..., ProviderEventId: {dup.ProviderKey}, Count: {dup.Count}");
    }
}
else
{
    Console.WriteLine("\n✅ No duplicate events found.");
}

// SECTION 2: Redundant Detection Data
Console.WriteLine("\n\nSECTION 2: REDUNDANT DETECTION DATA");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var detectionSummary = await healthChecker.GetDetectionRedundancySummaryAsync(ct);

Console.WriteLine($"MediaItemDetection rows: {detectionSummary.MediaItemDetectionCount:N0}");
Console.WriteLine($"EventDetection rows:     {detectionSummary.EventDetectionCount:N0}");

if (detectionSummary.AvgDetectionsPerMediaItem.HasValue)
{
    Console.WriteLine($"\nAverage detections per MediaItem: {detectionSummary.AvgDetectionsPerMediaItem}");
    Console.WriteLine($"Average detections per Event:     {detectionSummary.AvgDetectionsPerEvent}");
}

// SECTION 3: Device Health
Console.WriteLine("\n\nSECTION 3: DEVICE HEALTH");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var healthSummary = await healthChecker.GetDeviceHealthSummaryAsync(ct);

Console.WriteLine($"DeviceHealth rows: {healthSummary.DeviceHealthRowCount:N0}");

if (healthSummary.RecentDates.Count > 0)
{
    Console.WriteLine($"\n   Recent dates:");
    foreach (var date in healthSummary.RecentDates)
    {
        Console.WriteLine($"     • {date.Date}: {date.Count} records");
    }
}

// SECTION 4: Device Features Redundancy
Console.WriteLine("\n\nSECTION 4: DEVICE FEATURES REDUNDANCY");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var featureOverlap = await healthChecker.GetDeviceFeatureOverlapAsync(ct);

Console.WriteLine($"DeviceCapabilities rows:  {featureOverlap.DeviceCapabilitiesCount:N0}");
Console.WriteLine($"DeviceFeatures rows:      {featureOverlap.DeviceFeaturesCount:N0}");

Console.WriteLine($"\nDevices with both tables:     {featureOverlap.DevicesWithBoth}");
Console.WriteLine($"Devices with Capabilities only: {featureOverlap.CapabilitiesOnlyCount}");
Console.WriteLine($"Devices with Features only:     {featureOverlap.FeaturesOnlyCount}");

if (featureOverlap.DevicesWithBoth > 0)
{
    Console.WriteLine($"\n⚠️  {featureOverlap.DevicesWithBoth} devices have BOTH DeviceCapabilities and DeviceFeatures!");
}

// SECTION 5: Data Quality Metrics (actually SECTION 6 in output)
Console.WriteLine("\n\nSECTION 6: OVERALL TABLE SIZES");
Console.WriteLine("─────────────────────────────────────────────────────────────────\n");

var tableSizes = await healthChecker.GetTableSizesAsync(ct);
var tableStats = new[]
{
    ("MediaItems", tableSizes.FirstOrDefault(t => t.TableName == "MediaItems")?.RowCount ?? 0),
    ("Events", tableSizes.FirstOrDefault(t => t.TableName == "Events")?.RowCount ?? 0),
    ("Devices", tableSizes.FirstOrDefault(t => t.TableName == "Devices")?.RowCount ?? 0),
    ("Locations", tableSizes.FirstOrDefault(t => t.TableName == "Locations")?.RowCount ?? 0),
    ("Detections (both)", detectionSummary.MediaItemDetectionCount + detectionSummary.EventDetectionCount),
    ("DeviceHealth", healthSummary.DeviceHealthRowCount),
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
if (featureOverlap.DevicesWithBoth > 0)
    issues.Add($"⚠️  {featureOverlap.DevicesWithBoth} devices with both DeviceCapabilities AND DeviceFeatures (merge needed)");

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

var orphanedSummary = await healthChecker.FindOrphanedRecordsAsync(ct);

if (orphanedSummary.OrphanedEventCount > 0)
    Console.WriteLine($"⚠️  {orphanedSummary.OrphanedEventCount} orphaned Events (Device deleted?)");
if (orphanedSummary.OrphanedMediaItemCount > 0)
    Console.WriteLine($"⚠️  {orphanedSummary.OrphanedMediaItemCount} orphaned MediaItems (Device deleted?)");
if (orphanedSummary.OrphanedMediaItemDetectionCount > 0)
    Console.WriteLine($"⚠️  {orphanedSummary.OrphanedMediaItemDetectionCount} orphaned MediaItemDetections (MediaItem deleted?)");
if (orphanedSummary.OrphanedEventDetectionCount > 0)
    Console.WriteLine($"⚠️  {orphanedSummary.OrphanedEventDetectionCount} orphaned EventDetections (Event deleted?)");
if (orphanedSummary.OrphanedDeviceCount > 0)
    Console.WriteLine($"⚠️  {orphanedSummary.OrphanedDeviceCount} orphaned Devices (Location deleted?)");
if (orphanedSummary.OrphanedDownloadEventCount > 0)
    Console.WriteLine($"⚠️  {orphanedSummary.OrphanedDownloadEventCount} orphaned DownloadEvents (ProviderAccount deleted?)");

if (orphanedSummary.OrphanedEventCount == 0 && orphanedSummary.OrphanedMediaItemCount == 0 &&
    orphanedSummary.OrphanedMediaItemDetectionCount == 0 && orphanedSummary.OrphanedEventDetectionCount == 0 &&
    orphanedSummary.OrphanedDeviceCount == 0 && orphanedSummary.OrphanedDownloadEventCount == 0)
    Console.WriteLine("✅ No orphaned records found.");

Console.WriteLine("\nNext steps:");
Console.WriteLine("  1. Run the SQL queries in db_duplicate_check.sql for detailed analysis");
Console.WriteLine("  2. Review the consolidation roadmap in database_redundancy_analysis.md");
Console.WriteLine("  3. Backup database before making structural changes");
