using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Enforces that repository classes write through the ambient unit of work, not their own factory. A standalone write
    /// that opens its own context skips the write gate and contends with gated transactions for SQLite's write lock.
    /// </summary>
    public class RepositoryWriteAllowlistTests
    {
        // Files that legitimately keep the factory: the gate itself, the unit of work that owns the factory, and the
        // repositories for logs.db. logs.db is a separate SQLite file, so the main-database gate does not cover it.
        private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "UnitOfWork.cs",
            "ActionLogAppendGate.cs",
            "CameraBitrateBaselineRepository.cs",
            "LiveViewTelemetryRepository.cs",
            "ProviderApiCallLogRepository.cs",
            "ProviderApiErrorLogRepository.cs",
        };

        [Fact]
        public void Repositories_DoNotInjectDbContextFactory_ExceptAllowlisted()
        {
            string repositories = FindRepositoriesFolder();

            string[] violations = Directory.GetFiles(repositories, "*.cs", SearchOption.AllDirectories)
                .Where(path => !AllowedFiles.Contains(Path.GetFileName(path)))
                .Where(path => File.ReadAllText(path).Contains("IDbContextFactory", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(repositories, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.True(violations.Length == 0, $"{violations.Length} repository file(s) still inject IDbContextFactory: {string.Join(", ", violations)}");
        }

        private static string FindRepositoriesFolder()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null)
            {
                string candidate = Path.Combine(directory, "src", "data", "database", "src", "Repositories");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new DirectoryNotFoundException("Could not find src/data/database/src/Repositories above the test output folder.");
        }
    }
}
