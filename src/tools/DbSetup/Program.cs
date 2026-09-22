using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.Migrations;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Helpers.Platform;

// VideoForensics.DbSetup: creates/migrates the VideoForensics SQLite database at a specified path,
// without needing to start the full server. Intended for provisioning a database ahead of first
// service start (e.g. with a custom data directory) or for scripted/CI database maintenance.
//
// Usage:
//   VideoForensics.DbSetup [--db-path <file>] [--data-root <dir>]
//
// --db-path takes priority over --data-root; --data-root takes priority over the platform default
// (StorageLocationProvider.GetDefaultRoot, itself overridable via the registry/env mechanism the
// installer and Windows Service use). Neither flag is required - with no arguments this creates/
// migrates the database at the same default path the server itself would use.

string? dbPathArg = null;
string? dataRootArg = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--db-path" when i + 1 < args.Length:
            dbPathArg = args[++i];
            break;
        case "--data-root" when i + 1 < args.Length:
            dataRootArg = args[++i];
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

ServiceProvider provider = services.BuildServiceProvider();
ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSetup");
IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

string effectivePath = resolvedDbPath ?? Path.Combine(new StorageLocationProvider().GetDefaultRoot(StorageCategory.Database), "videoforensics.db");
Console.WriteLine($"Creating/migrating database at: {effectivePath}");

try
{
    await DatabaseInitializer.InitializeAsync(factory, logger, CancellationToken.None);
    Console.WriteLine("Database is up to date.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Database setup failed: {ex.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        VideoForensics.DbSetup - create/migrate the VideoForensics database without starting the server.

        Usage:
          VideoForensics.DbSetup [--db-path <file>] [--data-root <dir>]

        Options:
          --db-path <file>    Exact path to the SQLite database file. Takes priority over --data-root.
          --data-root <dir>   Directory to place videoforensics.db in. Ignored if --db-path is set.

        With no arguments, uses the same default path the server itself would use.
        """);
}
