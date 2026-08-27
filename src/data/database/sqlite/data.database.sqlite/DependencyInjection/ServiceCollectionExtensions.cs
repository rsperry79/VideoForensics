using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite.DependencyInjection
{
    /// <summary>Extension methods for registering SQLite-backed VideoForensics database layer.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds SQLite as the concrete database provider for the VideoForensics data access layer.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="dbPath">Optional path to the SQLite database file. Defaults to %AppData%\VideoForensics\videoforensics.db.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddVideoForensicsSqlite(this IServiceCollection services, string? dbPath = null)
        {
            // Resolve default database path if not provided
            if (string.IsNullOrEmpty(dbPath))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                dbPath = Path.Combine(appDataPath, "VideoForensics", "videoforensics.db");
            }

            // Ensure parent directory exists
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            // Register DbContext factory with SQLite provider
            // Using a factory (not AddDbContext) for thread-safety compatibility with MAUI and concurrent access patterns.
            var connectionString = $"Data Source={dbPath};Pooling=true;Cache=Shared;Default Timeout=5";

            services.AddDbContextFactory<VideoForensicsDbContext>(options =>
                options.UseSqlite(connectionString, b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite")));

            return services;
        }
    }
}
