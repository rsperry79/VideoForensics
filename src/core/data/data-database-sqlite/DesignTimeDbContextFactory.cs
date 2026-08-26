using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite
{
    /// <summary>Design-time factory for creating VideoForensicsDbContext for EF Core migrations.</summary>
    /// <remarks>
    /// This factory is used by the Entity Framework Core tooling (dotnet ef migrations) to create a DbContext
    /// instance at design time when the DbContext's constructor doesn't take a parameterless constructor.
    /// It is NOT used at runtime by the application.
    /// </remarks>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VideoForensicsDbContext>
    {
        /// <summary>
        /// Creates a VideoForensicsDbContext instance using a design-time placeholder database connection string.
        /// </summary>
        /// <param name="args">Command-line arguments passed by the EF Core tooling (not used).</param>
        /// <returns>A new VideoForensicsDbContext configured for SQLite.</returns>
        public VideoForensicsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<VideoForensicsDbContext>();
            optionsBuilder.UseSqlite(
                "Data Source=design-time-placeholder.db",
                b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"));
            return new VideoForensicsDbContext(optionsBuilder.Options);
        }
    }
}
