using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Client.Hosting;
using VideoForensics.Utils.DbRepair;
using VideoForensics.Utils.Diagnostics;
using VideoForensics.Utils.Diagnostics.Contracts;

// Windows host: read install-time storage overrides from the registry, as the server does.
VideoForensics.Core.Helpers.Platform.StorageOverrides.Current = new VideoForensics.Client.Platform.Windows.RegistryStorageOverrideSource();
VideoForensics.Core.Helpers.Platform.DataDirectoryAccess.Current = new VideoForensics.Client.Platform.Windows.WindowsDataDirectoryAccess();

// VideoForensics.Utils.DbRepair: repairs a VideoForensics SQLite database by removing duplicate records
// and orphaned records that have dangling foreign keys. Also provides a recovery switch for lost
// SuperAdmin passkeys. Operates in dry-run mode by default, requiring --apply to make changes
// for repair operations.
//
// Usage:
//   VideoForensics.Utils.DbRepair [--db-path <file>] [--data-root <dir>] [--apply] [--yes]
//   VideoForensics.Utils.DbRepair --reset-superadmin-password [--username <name>] [--db-path <file>] [--data-root <dir>] [--yes]
//   VideoForensics.Utils.DbRepair --repair-ding-ids [--apply] [--db-path <file>] [--data-root <dir>] [--yes]
//
// Ding id repair mode:
//   Relinks Events and DownloadEvents rows that were stored under the truncated doorbot history id to the matching
//   string ding id, and sets RingDoorbotHistoryEvents.LinkedEventId. Dry run unless --apply. With --apply, a copy of
//   the database is written to <db>.bak-<yyyyMMddHHmmss> before any change. Ambiguous and unmatched rows are never changed.
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
bool repairDingIds = false;
string? serverArg = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--reset-superadmin-password":
            resetSuperAdminPassword = true;
            break;
        case "--repair-ding-ids":
            repairDingIds = true;
            break;
        case "--db-path" when i + 1 < args.Length:
            dbPathArg = args[++i];
            break;
        case "--data-root" when i + 1 < args.Length:
            dataRootArg = args[++i];
            break;
        case "--server" when i + 1 < args.Length:
            serverArg = args[++i];
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

// Handle ding id repair if requested
if (repairDingIds)
{
    return await HandleDingIdRepairAsync(factory, effectivePath, apply, serverArg ?? ServerMaintenanceClient.DefaultServer.ToString(), CancellationToken.None);
}

// Handle SuperAdmin password recovery if requested
if (resetSuperAdminPassword)
{
    return await HandleSuperAdminRecoveryAsync(provider, usernameArg, skipConfirmation);
}

try
{
    CancellationToken ct = CancellationToken.None;

    // Check for duplicates and orphaned records
    Console.WriteLine("\nScanning for issues...\n");

    IReadOnlyList<DuplicateGroup> duplicateDevices = await checker.FindDuplicateDevicesAsync(ct);
    IReadOnlyList<DuplicateGroup> duplicateEvents = await checker.FindDuplicateEventsAsync(ct);
    OrphanedRecordSummary orphaned = await checker.FindOrphanedRecordsAsync(ct);
    DetectionRedundancySummary redundancy = await checker.GetDetectionRedundancySummaryAsync(ct);
    DeviceFeatureOverlapSummary overlap = await checker.GetDeviceFeatureOverlapAsync(ct);

    // Report findings
    int totalDeleteCount = 0;

    if (duplicateDevices.Count > 0)
    {
        Console.WriteLine($"Found {duplicateDevices.Count} duplicate device group(s):");
        foreach (DuplicateGroup group in duplicateDevices)
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
        foreach (DuplicateGroup group in duplicateEvents)
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

    // Why: the service holds the database open and caches what it read; stop it for the deletes and start it again after.
    using VideoForensics.Client.Platform.Windows.ServiceDbAccessGuard? serviceGuard = VideoForensics.Client.Platform.Windows.ServiceDbAccessGuard.Acquire("VideoForensics", out string? stopError);
    if (serviceGuard is null)
    {
        Console.Error.WriteLine(stopError);
        return 1;
    }

    // Execute repairs in a transaction
    await using VideoForensicsDbContext context = await factory.CreateDbContextAsync(ct);
    await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

    try
    {
        int totalDeleted = 0;

        // Fix duplicate devices: keep earliest by Id
        if (duplicateDevices.Count > 0)
        {
            Console.WriteLine("\nDeleting duplicate devices...");
            foreach (DuplicateGroup group in duplicateDevices)
            {
                // Get the actual device records to determine which to keep
                List<Device> devices = await context.Devices
                    .Where(d => d.LocationId == group.LocationId && d.ProviderDeviceId == group.ProviderKey)
                    .ToListAsync(ct);

                // Sort by Id and keep the first one
                var sorted = devices.OrderBy(d => d.Id).ToList();
                var toDelete = sorted.Skip(1).ToList();

                foreach (Device? device in toDelete)
                {
                    Console.WriteLine($"  Deleting Device {device.Id} (LocationId {device.LocationId}, ProviderDeviceId '{device.ProviderDeviceId}')");
                    _ = context.Devices.Remove(device);
                    totalDeleted++;
                }
            }

            _ = await context.SaveChangesAsync(ct);
        }

        // Fix duplicate events: keep earliest by DiscoveredAtUtc
        if (duplicateEvents.Count > 0)
        {
            Console.WriteLine("Deleting duplicate events...");
            foreach (DuplicateGroup group in duplicateEvents)
            {
                List<Event> events = await context.Events
                    .Where(e => e.DeviceId == group.DeviceId && e.ProviderEventId == group.ProviderKey)
                    .ToListAsync(ct);

                // Sort by DiscoveredAtUtc and keep the first one
                var sorted = events.OrderBy(e => e.DiscoveredAtUtc).ToList();
                var toDelete = sorted.Skip(1).ToList();

                foreach (Event? evt in toDelete)
                {
                    Console.WriteLine($"  Deleting Event {evt.Id} (DeviceId {evt.DeviceId}, ProviderEventId '{evt.ProviderEventId}', DiscoveredAtUtc {evt.DiscoveredAtUtc:O})");
                    _ = context.Events.Remove(evt);
                    totalDeleted++;
                }
            }

            _ = await context.SaveChangesAsync(ct);
        }

        // Delete orphaned records
        if (orphaned.OrphanedEventCount > 0)
        {
            Console.WriteLine("Deleting orphaned events...");
            List<Event> orphanedEvents = await context.Events
                .Where(e => orphaned.OrphanedEventIds.Contains(e.Id))
                .ToListAsync(ct);
            foreach (Event? evt in orphanedEvents)
            {
                Console.WriteLine($"  Deleting Event {evt.Id}");
                _ = context.Events.Remove(evt);
                totalDeleted++;
            }

            _ = await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedMediaItemCount > 0)
        {
            Console.WriteLine("Deleting orphaned media items...");
            List<MediaItem> orphanedItems = await context.MediaItems
                .Where(m => orphaned.OrphanedMediaItemIds.Contains(m.Id))
                .ToListAsync(ct);
            foreach (MediaItem? item in orphanedItems)
            {
                Console.WriteLine($"  Deleting MediaItem {item.Id}");
                _ = context.MediaItems.Remove(item);
                totalDeleted++;
            }

            _ = await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedMediaItemDetectionCount > 0)
        {
            Console.WriteLine("Deleting orphaned media item detections...");
            List<MediaItemDetection> orphanedDetections = await context.MediaItemDetections
                .Where(mid => orphaned.OrphanedMediaItemDetectionIds.Contains(mid.Id))
                .ToListAsync(ct);
            foreach (MediaItemDetection? detection in orphanedDetections)
            {
                Console.WriteLine($"  Deleting MediaItemDetection {detection.Id}");
                _ = context.MediaItemDetections.Remove(detection);
                totalDeleted++;
            }

            _ = await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedEventDetectionCount > 0)
        {
            Console.WriteLine("Deleting orphaned event detections...");
            List<EventDetection> orphanedDetections = await context.EventDetections
                .Where(ed => orphaned.OrphanedEventDetectionIds.Contains(ed.Id))
                .ToListAsync(ct);
            foreach (EventDetection? detection in orphanedDetections)
            {
                Console.WriteLine($"  Deleting EventDetection {detection.Id}");
                _ = context.EventDetections.Remove(detection);
                totalDeleted++;
            }

            _ = await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedDeviceCount > 0)
        {
            Console.WriteLine("Deleting orphaned devices...");
            List<Device> orphanedDevices = await context.Devices
                .Where(d => orphaned.OrphanedDeviceIds.Contains(d.Id))
                .ToListAsync(ct);
            foreach (Device? device in orphanedDevices)
            {
                Console.WriteLine($"  Deleting Device {device.Id}");
                _ = context.Devices.Remove(device);
                totalDeleted++;
            }

            _ = await context.SaveChangesAsync(ct);
        }

        if (orphaned.OrphanedDownloadEventCount > 0)
        {
            Console.WriteLine("Deleting orphaned download events...");
            List<DownloadEvent> orphanedDownloads = await context.DownloadEvents
                .Where(de => orphaned.OrphanedDownloadEventIds.Contains(de.Id))
                .ToListAsync(ct);
            foreach (DownloadEvent? download in orphanedDownloads)
            {
                Console.WriteLine($"  Deleting DownloadEvent {download.Id}");
                _ = context.DownloadEvents.Remove(download);
                totalDeleted++;
            }

            _ = await context.SaveChangesAsync(ct);
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
        // Why: the service caches operator and session state; stop it for the reset and start it again after.
        using VideoForensics.Client.Platform.Windows.ServiceDbAccessGuard? serviceGuard = VideoForensics.Client.Platform.Windows.ServiceDbAccessGuard.Acquire("VideoForensics", out string? stopError);
        if (serviceGuard is null)
        {
            Console.Error.WriteLine(stopError);
            return 1;
        }

        using IServiceScope scope = provider.CreateScope();
        IOperatorRepository operatorRepo = scope.ServiceProvider.GetRequiredService<IOperatorRepository>();
        IAppSettingRepository appSettingRepo = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
        ISecurityAuditLogRepository auditRepo = scope.ServiceProvider.GetRequiredService<ISecurityAuditLogRepository>();

        var passwordPrompt = new TerminalPasswordPrompt();
        var recovery = new SuperAdminRecovery(operatorRepo, appSettingRepo, passwordPrompt, auditRepo);

        Console.WriteLine("\n=== SuperAdmin Password Recovery ===");
        _ = await recovery.ExecuteAsync(username, skipConfirmation, CancellationToken.None);

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

// Why: a dry run only reads, so it takes no server hold and pairs nothing. An apply run holds the local server for the whole write, so the
// server neither keeps the file open nor caches what it read. The hold ends in the await-using disposal, after the repair finishes or fails.
static async Task<int> HandleDingIdRepairAsync(IDbContextFactory<VideoForensicsDbContext> factory, string dbPath, bool apply, string serverValue, CancellationToken ct)
{
    var service = new DingIdRepairService();
    // Why: the path is kept so the stop message can name the backup that the repair wrote before it was stopped.
    string? backupPath = null;
    Func<CancellationToken, Task> backup = async token => backupPath = await BackupDatabaseAsync(dbPath, token);

    if (!apply)
    {
        // Why: a dry run only reads, so it neither contacts nor validates the server.
        return await ReportDingIdRepairAsync(service, factory, apply: false, backup, ct);
    }

    if (!ServerMaintenanceClient.TryValidateServer(serverValue, out Uri? serverUri, out string? serverError))
    {
        Console.Error.WriteLine(serverError);
        return 1;
    }

    Uri server = serverUri!;

    // Why: Ctrl+C cancels the run instead of killing the process, so the disposal below still ends the server hold.
    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
    ConsoleCancelEventHandler onCancel = (_, e) =>
    {
        e.Cancel = true;
        cancel.Cancel();
    };

    Console.CancelKeyPress += onCancel;
    try
    {
        // Why: this client sends the server key, so it must not follow redirects to another address.
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        var maintenance = new ServerMaintenanceClient(http, new ConsoleMaintenancePrompts());
        // Why: the server ending the hold cancels this source, which stops the repair before it writes more. Ctrl+C cancels it too.
        using var repairStop = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
        ServerMaintenanceClient.ServerMaintenanceSession? session = null;
        try
        {
            session = await maintenance.StartAsync(server, cancel.Token, repairStop);
        }
        catch (OperationCanceledException)
        {
            // Cancelled while the hold was being taken: the begin call may still have held the server, and its safety timeout ends that.
        }

        if (session is null)
        {
            Console.Error.WriteLine("Ding id repair cancelled. The database was not changed.");
            return 1;
        }

        int exitCode;
        await using (session)
        {
            exitCode = await ReportDingIdRepairAsync(service, factory, apply: true, backup, repairStop.Token);
        }

        // Why: the session is disposed above, so HoldLost is final here. A stopped repair is never reported as success.
        if (session.HoldLost)
        {
            Console.Error.WriteLine($"the server ended maintenance; the repair was stopped; the database may be partly repaired; check the report and the backup at {backupPath ?? "(no backup was written)"}");
            return 1;
        }

        if (repairStop.IsCancellationRequested)
        {
            Console.Error.WriteLine($"Ding id repair cancelled. The database may be partly repaired; check the backup at {backupPath ?? "(no backup was written)"}");
            return 1;
        }

        return exitCode;
    }
    finally
    {
        Console.CancelKeyPress -= onCancel;
    }
}

static async Task<int> ReportDingIdRepairAsync(DingIdRepairService service, IDbContextFactory<VideoForensicsDbContext> factory, bool apply, Func<CancellationToken, Task> backup, CancellationToken ct)
{
    try
    {
        DingIdRepairReport report = await service.RunAsync(factory, apply, backup, ct);
        // Why: a repair that was stopped mid-run must not print a success summary, even if the service returned normally.
        ct.ThrowIfCancellationRequested();

        Console.WriteLine("\nDing id repair (DoorbotHistory feed):");
        Console.WriteLine($"  Matched: {report.DoorbotHistory.Matched}");
        Console.WriteLine($"  Unmatched: {report.DoorbotHistory.Unmatched}");
        Console.WriteLine($"  Ambiguous: {report.DoorbotHistory.Ambiguous}");
        if (report.Applied)
        {
            Console.WriteLine($"Repaired Events: {report.EventsRepaired}, DownloadEvents: {report.DownloadEventsRepaired}, skipped (collision): {report.EventsSkipped}");
        }
        else
        {
            Console.WriteLine("DRY RUN: no changes written. Run with --apply to repair the matched rows.");
        }

        return 0;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // The caller prints the message that explains the stop.
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Ding id repair failed: {ex.Message} ({ex.GetType().Name})");
        return 1;
    }
}

static Task<string> BackupDatabaseAsync(string dbPath, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    string backupPath = $"{dbPath}.bak-{DateTime.Now:yyyyMMddHHmmss}";
    if (File.Exists(backupPath))
    {
        throw new IOException($"Backup already exists: {backupPath}");
    }

    // Why: VACUUM INTO writes a consistent copy that includes committed WAL pages, which a plain file copy can miss.
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "VACUUM INTO $path";
    _ = command.Parameters.AddWithValue("$path", backupPath);
    _ = command.ExecuteNonQuery();
    Console.WriteLine($"Backup written: {backupPath}");
    return Task.FromResult(backupPath);
}

static void PrintUsage()
{
    Console.WriteLine("""
        VideoForensics.Utils.DbRepair - repair the VideoForensics database or recover SuperAdmin access.

        Usage (repair mode):
          VideoForensics.Utils.DbRepair [--db-path <file>] [--data-root <dir>] [--apply] [--yes]

        Usage (recovery mode):
          VideoForensics.Utils.DbRepair --reset-superadmin-password [--username <name>] [--db-path <file>] [--data-root <dir>] [--yes]

        Options:
          --db-path <file>              Exact path to the SQLite database file. Takes priority over --data-root.
          --data-root <dir>             Directory containing videoforensics.db. Ignored if --db-path is set.
          --apply                       Execute the repair. Without this flag, runs in dry-run mode and only reports issues.
          --reset-superadmin-password   Enable SuperAdmin password recovery (lost passkey recovery mode).
          --repair-ding-ids             Relink truncated Events/DownloadEvents to matched string ding ids. Dry run unless --apply; --apply writes a .bak copy first.
          --server <url>                Local server to hold in maintenance during --repair-ding-ids. Default http://localhost:5162/.
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
          unless --yes is also passed. Each deleted row is logged individually. The VideoForensics service is stopped
          for the deletes and started again afterwards.

        Recovery mode (--reset-superadmin-password):
          Recovers SuperAdmin access when the passkey is lost.
          Prompts interactively for a new password (min 12 characters, with confirmation).
          Resets the password, unlocks the account, re-activates it, and invalidates existing sessions.
          Requires a confirmation prompt unless --yes is passed.
          Must be run on the server machine with administrator privileges (database is under ProgramData).
          The VideoForensics service is stopped for the reset and started again afterwards (needs administrator privileges).
        """);
}
