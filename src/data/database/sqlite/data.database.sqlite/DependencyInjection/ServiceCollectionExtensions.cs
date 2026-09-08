using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.Migrations;

namespace VideoForensics.Data.Database.Sqlite.DependencyInjection
{
    /// <summary>Extension methods for registering SQLite-backed VideoForensics database layer.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds SQLite as the concrete database provider for the VideoForensics data access layer.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="dbPath">Optional path to the SQLite database file. Defaults to %ProgramData%\VideoForensics\videoforensics.db.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddVideoForensicsSqlite(this IServiceCollection services, string? dbPath = null)
        {
            // Resolve default database path if not provided
            if (string.IsNullOrEmpty(dbPath))
            {
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                dbPath = Path.Combine(programDataPath, "VideoForensics", "videoforensics.db");
            }

            // Ensure parent directory exists
            string? dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory))
            {
                bool directoryExistedBefore = Directory.Exists(dbDirectory);
                _ = Directory.CreateDirectory(dbDirectory);

                // On Windows, %ProgramData% is machine-wide and not writable by normal users by default for newly-created
                // subdirectories. Grant the built-in "Users" group Modify rights so the app can function whether run as a
                // normal user, elevated process, or (eventually) Windows Service.
                if (!directoryExistedBefore && OperatingSystem.IsWindows())
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dbDirectory);
                        var acl = dirInfo.GetAccessControl();
                        var usersIdentity = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                        acl.AddAccessRule(
                            new FileSystemAccessRule(
                                usersIdentity,
                                FileSystemRights.Modify,
                                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                PropagationFlags.None,
                                AccessControlType.Allow));
                        dirInfo.SetAccessControl(acl);
                    }
                    catch
                    {
                        // Silently continue if ACL modification fails (e.g., sandboxed/restricted environment).
                        // Never block app startup due to a permissions issue here.
                    }
                }
            }

            // Register DbContext factory with SQLite provider
            // Using a factory (not AddDbContext) for thread-safety compatibility with MAUI and concurrent access patterns.
            string connectionString = $"Data Source={dbPath};Pooling=true;Cache=Shared;Default Timeout=5";

            _ = services.AddDbContextFactory<VideoForensicsDbContext>(options =>
                options.UseSqlite(connectionString, b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite")));

            _ = services.AddScoped<IDatabaseMaintenanceService, SqliteDatabaseMaintenanceService>();

            return services;
        }
    }
}
