using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.Migrations;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Helpers.Platform;

// VideoForensics.DbSetup: creates/migrates the VideoForensics SQLite database at a specified path,
// without needing to start the full server. Intended for provisioning a database ahead of first
// service start (e.g. with a custom data directory) or for scripted/CI database maintenance.
//
// Usage:
//   VideoForensics.DbSetup [--db-path <file>] [--data-root <dir>] [--set-network-tier <Local|Network>]
//
// --db-path takes priority over --data-root; --data-root takes priority over the platform default
// (StorageLocationProvider.GetDefaultRoot, itself overridable via the registry/env mechanism the
// installer and Windows Service use). Neither flag is required - with no arguments this creates/
// migrates the database at the same default path the server itself would use.
//
// Admin creation is controlled via the VIDEOFORENSICS_SETUP_ADMIN_USERNAME / _PASSWORD environment
// variables rather than a CLI flag, so the installer can pass a password to this process without it
// showing up in a process listing (see PrintUsage for details).

string? dbPathArg = null;
string? dataRootArg = null;
string? networkTierArg = null;

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
        case "--set-network-tier" when i + 1 < args.Length:
            networkTierArg = args[++i];
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

if (networkTierArg is not null && networkTierArg != "Local" && networkTierArg != "Network")
{
    Console.Error.WriteLine($"Invalid --set-network-tier value: '{networkTierArg}'. Must be 'Local' or 'Network'.");
    return 1;
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
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Database setup failed: {ex.Message}");
    return 1;
}

if (networkTierArg is not null)
{
    int tierResult = await SetNetworkTierAsync(factory, networkTierArg, CancellationToken.None);
    if (tierResult != 0)
    {
        return tierResult;
    }
}

string? adminUsername = Environment.GetEnvironmentVariable("VIDEOFORENSICS_SETUP_ADMIN_USERNAME");
string? adminPassword = Environment.GetEnvironmentVariable("VIDEOFORENSICS_SETUP_ADMIN_PASSWORD");
if (!string.IsNullOrEmpty(adminUsername) && !string.IsNullOrEmpty(adminPassword))
{
    int adminResult = await CreateAdminIfEmptyAsync(provider, adminUsername, adminPassword, CancellationToken.None);
    if (adminResult != 0)
    {
        return adminResult;
    }
}

return 0;

static async Task<int> SetNetworkTierAsync(IDbContextFactory<VideoForensicsDbContext> factory, string tierValue, CancellationToken ct)
{
    await using VideoForensicsDbContext context = await factory.CreateDbContextAsync(ct);

    AppSetting? existing = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "ConfiguredNetworkTier", ct);
    if (existing is not null)
    {
        existing.Value = tierValue;
        existing.UpdatedAtUtc = DateTime.UtcNow;
    }
    else
    {
        context.AppSettings.Add(new AppSetting
        {
            Id = Guid.NewGuid(),
            Key = "ConfiguredNetworkTier",
            Value = tierValue,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    await context.SaveChangesAsync(ct);
    Console.WriteLine($"Network tier set to '{tierValue}'.");
    return 0;
}

static async Task<int> CreateAdminIfEmptyAsync(IServiceProvider provider, string username, string password, CancellationToken ct)
{
    using IServiceScope scope = provider.CreateScope();
    IOperatorRepository operators = scope.ServiceProvider.GetRequiredService<IOperatorRepository>();

    if (!await operators.IsEmptyAsync(ct))
    {
        Console.WriteLine("An operator already exists; skipping admin creation.");
        return 0;
    }

    if (password.Length < 12)
    {
        Console.Error.WriteLine("Admin password must be at least 12 characters.");
        return 1;
    }

    var passwordHasher = new PasswordHasher<Operator>();
    var admin = new Operator
    {
        Id = Guid.NewGuid(),
        Username = username,
        DisplayName = username,
        FirstName = username,
        LastName = "Administrator",
        Email = $"{username}@localhost.invalid",
        Role = OperatorRole.SuperAdmin,
        IsApproved = true,
        Active = true,
        MustChangePassword = false,
        CreatedAtUtc = DateTime.UtcNow,
        PasswordUpdatedAtUtc = DateTime.UtcNow,
        SecurityStamp = Guid.NewGuid()
    };
    admin.PasswordHash = passwordHasher.HashPassword(admin, password);

    Operator created = await operators.AddAsync(admin, ct);
    Console.WriteLine($"Created SuperAdmin operator '{created.Username}'.");
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("""
        VideoForensics.DbSetup - create/migrate the VideoForensics database without starting the server.

        Usage:
          VideoForensics.DbSetup [--db-path <file>] [--data-root <dir>] [--set-network-tier <Local|Network>]

        Options:
          --db-path <file>              Exact path to the SQLite database file. Takes priority over --data-root.
          --data-root <dir>             Directory to place videoforensics.db in. Ignored if --db-path is set.
          --set-network-tier <tier>     Writes the ConfiguredNetworkTier app setting. Must be 'Local' or 'Network'
                                         ('Internet' is not settable here - that's configured in-app via the
                                         Cloudflare tunnel UI, not this installer-time flag).

        With no arguments, uses the same default path the server itself would use.

        Admin creation (env vars, not a CLI flag - keeps the password out of process listings):
          VIDEOFORENSICS_SETUP_ADMIN_USERNAME   Username for the initial SuperAdmin account.
          VIDEOFORENSICS_SETUP_ADMIN_PASSWORD   Password for the initial SuperAdmin account (min 12 chars).
        When both are set and non-empty, a SuperAdmin operator is created after the database is
        created/migrated - but only if no operator already exists (idempotent; safe to re-run).
        """);
}
