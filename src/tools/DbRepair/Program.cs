using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Diagnostics;
using VideoForensics.Diagnostics.Contracts;
using VideoForensics.DbRepair;
using VideoForensics.DbRepair.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Helpers.Platform;

// VideoForensics.DbRepair: repairs a VideoForensics SQLite database by removing duplicate records
// and orphaned records that have dangling foreign keys. Also provides a recovery switch for lost
// SuperAdmin passkeys. Operates in dry-run mode by default, requiring --apply to make changes
// for repair operations.
//
// Usage:
//   VideoForensics.DbRepair [--db-path <file>] [--data-root <dir>] [--apply] [--yes]
//   VideoForensics.DbRepair --reset-superadmin-password [--username <name>] [--db-path <file>] [--data-root <dir>] [--yes]
//
// Repair mode:
//   --db-path takes priority over --data-root; --data-root takes priority over the platform default
//   (StorageLocationProvider.GetDefaultRoot). Without --apply, only reports issues without modifying
//   the database. With --apply, fixes:
//     1. Orphaned records (Events/MediaItems/Detections/Devices/DownloadEvents with dangling FK)
//     2. Exact duplicates:
//        - Device: keep earliest by Id (Guid comparison)
//        - Event: keep earliest by DiscoveredAtUtc
//   Other categories (redundancy, feature overlap) are reported informally only.
//   All deletions are wrapped in a transaction, and requires a confirmation prompt before executing
//   (unless --yes is also passed to skip it).
//
// Recovery mode:
//   --reset-superadmin-password enables SuperAdmin password recovery when the passkey is lost.
//   Prompts interactively for a new password with confirmation. Requires typed confirmation unless --yes.
//   Resets the password, unlocks the account, re-activates, and invalidates existing sessions.

string? dbPathArg = null;
string? dataRootArg = null;
string? usernameArg = null;
bool apply = false;
bool skipConfirmation = false;
bool resetSuperAdminPassword = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--reset-superadmin-password":
            resetSuperAdminPassword = true;
            break;
        case "--db-path" when i + 1 < args.Length:
            dbPathArg = args[++i];
            break;
        case "--data-root" when i + 1 < args.Length:
            dataRootArg = args[++i];
            break;
        case "--username" when i + 1 < args.Length:
            usernameArg = args[++i];
            break;
        case "--apply":
            apply = true;
            break;
        case "--yes":
        case "-y":
            skipConfirmation = true;
            break;
        case "--help":
        case "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

string? resolvedDbPath = dbPathArg;
if (resolvedDbPath is null && dataRootArg is not null)
{
    resolvedDbPath = Path.Combine(dataRootArg, "videoforensics.db");
}

var services = new ServiceCollection();
_ = services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
_ = services.AddVideoForensicsDataLayer(resolvedDbPath);
_ = services.AddSingleton<IDatabaseHealthChecker, DatabaseHealthChecker>();

ServiceProvider provider = services.BuildServiceProvider();
ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbRepair");
IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
IDatabaseHealthChecker checker = provider.GetRequiredService<IDatabaseHealthChecker>();

string effectivePath = resolvedDbPath ?? Path.Combine(new StorageLocationProvider().GetDefaultRoot(StorageCategory.Database), "videoforensics.db");
Console.WriteLine($"Database: {effectivePath}");

// Handle SuperAdmin password recovery if requested
if (resetSuperAdminPassword)
{
    return await HandleSuperAdminRecoveryAsync(provider, usernameArg, skipConfirmation);
}

try
{
    var ct = CancellationToken.None;

    // Check for duplicates and orphaned records
    Console.WriteLine("\nScanning for issues...\n");

    var duplicateDevices = await checker.FindDuplicateDevicesAsync(ct);
    var duplicateEvents = await checker.FindDuplicateEventsAsync(ct);
    var orphaned = await checker.FindOrphanedRecordsAsync(ct);
    var redundancy = await checker.GetDetectionRedundancySummaryAsync(ct);
    var overlap = await checker.GetDeviceFeatureOverlapAsync(ct);

    // Report findings
    int totalDeleteCount = 0;

    if (duplicateDevices.Count > 0)
    {
        Console.WriteLine($"Found {duplicateDevices.Count} duplicate device group(s):");
        foreach (var group in duplicateDevices)
        {
            Console.WriteLine($"  Location {group.LocationId} / ProviderDeviceId '{group.ProviderKey}': {group.Count} duplicates (IDs: {string.Join(", ", group.RecordIds)})");
        }
        // Count duplicates to delete (all but the earliest per group)
        int duplicatesToDelete = duplicateDevices.Sum(g => g.Count - 1);
        Console.WriteLine($"  -> Will delete {duplicatesToDelete} duplicate record(s) if --apply is used\n");
        totalDeleteCount += duplicatesToDelete;
    }
    else
    {
        Console.WriteLine("No duplicate devices found.\n");
    }

    if (duplicateEvents.Count > 0)
    {
        Console.WriteLine($"Found {duplicateEvents.Count} duplicate event group(s):");
        foreach (var group in duplicateEvents)
        {
            Console.WriteLine($"  Device {group.DeviceId} / ProviderEventId '{group.ProviderKey}': {group.Count} duplicates (IDs: {string.Join(", ", group.RecordIds)})");
        }
        int duplicatesToDelete = duplicateEvents.Sum(g => g.Count - 1);
        Console.WriteLine($"  -> Will delete {duplicatesToDelete} duplicate record(s) if --apply is used\n");
        totalDeleteCount += duplicatesToDelete;
    }
    else
    {
        Console.WriteLine("No duplicate events found.\n");
    }

    if (orphaned.OrphanedEventCount > 0)
    {
        Console.WriteLine($"Found {orphaned.OrphanedEventCount} orphaned event(s) (IDs: {string.Join(", ", orphaned.OrphanedEventIds)})");
        totalDeleteCount += orphaned.OrphanedEventCount;
    }

    if (orphaned.OrphanedMediaItemCount > 0)
    {
        Console.WriteLine($"Found {orphaned.OrphanedMediaItemCount} orphaned media item(s) (IDs: {string.Join(", ", orphaned.OrphanedMediaItemIds)})");
        totalDeleteCount += orphaned.OrphanedMediaItemCount;
    }

    if (orphaned.OrphanedMediaItemDetectionCount > 0)
    {
        Console.WriteLine($"Found {orphaned.OrphanedMediaItemDetectionCount} orphaned media item detection(s) (IDs: {string.Join(", ", orphaned.OrphanedMediaItemDetectionIds)})");
        totalDeleteCount += orphaned.OrphanedMediaItemDetectionCount;
    }

    if (orphaned.OrphanedEventDetectionCount > 0)
    {
        Console.WriteLine($"Found {orphaned.OrphanedEventDetectionCount} orphaned event detection(s) (IDs: {string.Join(", ", orphaned.OrphanedEventDetectionIds)})");
        totalDeleteCount += orphaned.OrphanedEventDetectionCount;
    }

    if (orphaned.OrphanedDeviceCount > 0)
    {
        Console.WriteLine($"Found {orphaned.OrphanedDeviceCount} orphaned device(s) (IDs: {string.Join(", ", orphaned.OrphanedDeviceIds)})");
        totalDeleteCount += orphaned.OrphanedDeviceCount;
    }

    if (orphaned.OrphanedDownloadEventCount > 0)
    {
        Console.WriteLine($"Found {orphaned.OrphanedDownloadEventCount} orphaned download event(s) (IDs: {string.Join(", ", orphaned.OrphanedDownloadEventIds)})");
        totalDeleteCount += orphaned.OrphanedDownloadEventCount;
    }

    // Redundancy and overlap are informational only
    Console.WriteLine("\nInformational findings (no automated fix):");
    Console.WriteLine($"Detection redundancy: {redundancy.MediaItemDetectionCount} MediaItemDetections, {redundancy.EventDetectionCount} EventDetections");
    if (redundancy.AvgDetectionsPerMediaItem.HasValue)
    {
        Console.WriteLine($"  -> Avg detections per media item: {redundancy.AvgDetectionsPerMediaItem}");
    }
    if (redundancy.AvgDetectionsPerEvent.HasValue)
    {
        Console.WriteLine($"  -> Avg detections per event: {redundancy.AvgDetectionsPerEvent}");
    }

    Console.WriteLine($"Device feature overlap: {overlap.DevicesWithBoth} device(s) with both capabilities and features");
    if (overlap.CapabilitiesOnlyCount > 0 || overlap.FeaturesOnlyCount > 0)
    {
        Console.WriteLine($"  -> {overlap.CapabilitiesOnlyCount} with only capabilities, {overlap.FeaturesOnlyCount} with only features");
    }

    string redundancyFile = "database_redundancy_analysis.md";
    if (File.Exists(redundancyFile) || File.Exists(Path.Combine("docs", redundancyFile)))
    {
        Console.WriteLine($"  (see {redundancyFile} for guidance)");
    }
    else
    {
        Console.WriteLine("  (no automated fix available for this category)");
    }

    Console.WriteLine();

    if (totalDeleteCount == 0)
    {
        Console.WriteLine("No issues found. Database is clean.");
        return 0;
    }

    if (!apply)
    {
        Console.WriteLine($"DRY RUN: Would delete {totalDeleteCount} total record(s).");
        Console.WriteLine("Run with --apply to execute the repair.");
        return 0;
    }

    // Apply mode: get confirmation unless --yes
    Console.WriteLine($"APPLY MODE: Will delete {totalDeleteCount} total record(s).");
    if (!skipConfirmation)
    {
        Console.WriteLine("Proceed? Type 'y' to confirm: ");
        string? response = Console.ReadLine();
        if (response?.ToLower() != "y")
        {
            Console.WriteLine("Cancelled.");
            return 0;
        }
    }

    // Execute repairs in a transaction
    await using var context = await factory.CreateDbContextAsync(ct);
    await using var transaction = await context.Database.BeginTransactionAsync(ct);

    try
    {
        int totalDeleted = 0;

        // Fix duplicate devices: keep earliest by Id
        if (duplicateDevices.Count > 0)
        {
            Console.WriteLine("\nDeleting duplicate devices...");
            foreach (var group in duplicateDevices)
            {
                // Get the actual device records to determine which to keep
                var devices = await context.Devices
                    .Where(d => d.LocationId == group.LocationId && d.ProviderDeviceId == group.ProviderKey)
                    .ToListAsync(ct);

                // Sort by Id and keep the first one
                var sorted = devices.OrderBy(d => d.Id).ToList();
                var toDelete = sorted.Skip(1).ToList();

                foreach (var device in toDelete)
                {
                    Console.WriteLine($"  Deleting Device {device.Id} (LocationId {device.LocationId}, ProviderDeviceId '{device.ProviderDeviceId}')");
                    context.Devices.Remove(device);
                    totalDeleted++;
                }
            }
            await context.SaveChangesAsync(ct);
        }

        // Fix duplicate events: keep earliest by DiscoveredAtUtc
        if (duplicateEvents.Count > 0)
        {
            Console.WriteLine("Deleting duplicate events...");
            foreach (var group in duplicateEvents)
            {
                var events = await context.Events
                    .Where(e => e.DeviceId == group.DeviceId && e.ProviderEventId == group.ProviderKey)
                    .ToListAsync(ct);

                // Sort by DiscoveredAtUtc and keep the first one
                var sorted = events.OrderBy(e => e.DiscoveredAtUtc).ToList();
                var toDelete = sorted.Skip(1).ToList();

                foreach (var evt in toDelete)
                {
                    Console.WriteLine($"  Deleting Event {evt.Id} (DeviceId {evt.DeviceId}, ProviderEventId '{evt.ProviderEventId}', DiscoveredAtUtc {evt.DiscoveredAtUtc:O})");
                    context.Events.Remove(evt);
                    totalDeleted++;
                }
            }
            await context.SaveChangesAsync(ct);
        }

        // Delete orphaned records
        if (orphaned.OrphanedEventCount > 0)
        {
            Console.WriteLine("Deleting orphaned events...");
            var orphanedEvents = await context.Events
                .Where(e => orphaned.OrphanedEventIds.Contains(e.Id))
                .ToListAsync(ct);
            foreach (var evt in orphanedEvents)
            {
                Console.WriteLine($"  Deleting Event {evt.Id}");
                context.Events.Remove(evt);
                totalDeleted++;
            }
            await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedMediaItemCount > 0)
        {
            Console.WriteLine("Deleting orphaned media items...");
            var orphanedItems = await context.MediaItems
                .Where(m => orphaned.OrphanedMediaItemIds.Contains(m.Id))
                .ToListAsync(ct);
            foreach (var item in orphanedItems)
            {
                Console.WriteLine($"  Deleting MediaItem {item.Id}");
                context.MediaItems.Remove(item);
                totalDeleted++;
            }
            await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedMediaItemDetectionCount > 0)
        {
            Console.WriteLine("Deleting orphaned media item detections...");
            var orphanedDetections = await context.MediaItemDetections
                .Where(mid => orphaned.OrphanedMediaItemDetectionIds.Contains(mid.Id))
                .ToListAsync(ct);
            foreach (var detection in orphanedDetections)
            {
                Console.WriteLine($"  Deleting MediaItemDetection {detection.Id}");
                context.MediaItemDetections.Remove(detection);
                totalDeleted++;
            }
            await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedEventDetectionCount > 0)
        {
            Console.WriteLine("Deleting orphaned event detections...");
            var orphanedDetections = await context.EventDetections
                .Where(ed => orphaned.OrphanedEventDetectionIds.Contains(ed.Id))
                .ToListAsync(ct);
            foreach (var detection in orphanedDetections)
            {
                Console.WriteLine($"  Deleting EventDetection {detection.Id}");
                context.EventDetections.Remove(detection);
                totalDeleted++;
            }
            await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedDeviceCount > 0)
        {
            Console.WriteLine("Deleting orphaned devices...");
            var orphanedDevices = await context.Devices
                .Where(d => orphaned.OrphanedDeviceIds.Contains(d.Id))
                .ToListAsync(ct);
            foreach (var device in orphanedDevices)
            {
                Console.WriteLine($"  Deleting Device {device.Id}");
                context.Devices.Remove(device);
                totalDeleted++;
            }
            await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedDownloadEventCount > 0)
        {
            Console.WriteLine("Deleting orphaned download events...");
            var orphanedDownloads = await context.DownloadEvents
                .Where(de => orphaned.OrphanedDownloadEventIds.Contains(de.Id))
                .ToListAsync(ct);
            foreach (var download in orphanedDownloads)
            {
                Console.WriteLine($"  Deleting DownloadEvent {download.Id}");
                context.DownloadEvents.Remove(download);
                totalDeleted++;
            }
            await context.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        Console.WriteLine($"\nRepair complete. Deleted {totalDeleted} record(s).");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nRepair failed. Rolling back transaction.");
        await transaction.RollbackAsync(ct);
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Database repair failed: {ex.Message}");
    logger.LogError(ex, "Unhandled exception during repair");
    return 1;
}

static async Task<int> HandleSuperAdminRecoveryAsync(IServiceProvider provider, string? username, bool skipConfirmation)
{
    try
    {
        using IServiceScope scope = provider.CreateScope();
        var operatorRepo = scope.ServiceProvider.GetRequiredService<IOperatorRepository>();
        var appSettingRepo = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<ISecurityAuditLogRepository>();

        var passwordPrompt = new TerminalPasswordPrompt();
        var recovery = new SuperAdminRecovery(operatorRepo, appSettingRepo, passwordPrompt, auditRepo);

        Console.WriteLine("\n=== SuperAdmin Password Recovery ===");
        await recovery.ExecuteAsync(username, skipConfirmation, CancellationToken.None);

        return 0;
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Recovery cancelled.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        return 1;
    }
}

static void PrintUsage()
{
    Console.WriteLine("""
        VideoForensics.DbRepair - repair the VideoForensics database or recover SuperAdmin access.

        Usage (repair mode):
          VideoForensics.DbRepair [--db-path <file>] [--data-root <dir>] [--apply] [--yes]

        Usage (recovery mode):
          VideoForensics.DbRepair --reset-superadmin-password [--username <name>] [--db-path <file>] [--data-root <dir>] [--yes]

        Options:
          --db-path <file>              Exact path to the SQLite database file. Takes priority over --data-root.
          --data-root <dir>             Directory containing videoforensics.db. Ignored if --db-path is set.
          --apply                       Execute the repair. Without this flag, runs in dry-run mode and only reports issues.
          --reset-superadmin-password   Enable SuperAdmin password recovery (lost passkey recovery mode).
          --username <name>             Target operator username for recovery. If omitted, uses the primary SuperAdmin.
          --yes, -y                     Skips confirmation prompts. Use only in scripted contexts.

        With no arguments, uses the same default path the server itself would use.

        Repair mode:
          Dry-run (default): Calls database health checks and reports what WOULD be deleted, without modifying the database.

          Apply (--apply): Fixes two categories with unambiguous safe actions:
            1. Orphaned records: Events, MediaItems, Detections, Devices, and DownloadEvents with dangling foreign keys.
               These are unreachable data and safe to delete.
            2. Exact duplicates:
               - Devices sharing the same LocationId and ProviderDeviceId: keeps the earliest by Guid Id, deletes the rest.
               - Events sharing the same DeviceId and ProviderEventId: keeps the earliest by DiscoveredAtUtc, deletes the rest.

          Other findings (detection redundancy, device feature overlap) are reported informally only - no automated fix.

          All deletions are wrapped in a single database transaction. A confirmation prompt is required before execution
          unless --yes is also passed. Each deleted row is logged individually.

        Recovery mode (--reset-superadmin-password):
          Recovers SuperAdmin access when the passkey is lost.
          Prompts interactively for a new password (min 12 characters, with confirmation).
          Resets the password, unlocks the account, re-activates it, and invalidates existing sessions.
          Requires a confirmation prompt unless --yes is passed.
          Must be run on the server machine with administrator privileges (database is under ProgramData).
          Stop the Windows service before running, or be aware of SQLite WAL implications if concurrent writes occur.
        """);
}
