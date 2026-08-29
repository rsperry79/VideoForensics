using Microsoft.Extensions.Logging;
using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Abstract base class for repository tests. Provides shared database fixture and logger setup.</summary>
    public abstract class RepositoryTestBase : IAsyncLifetime
    {
        /// <summary>Shared SQLite in-memory database fixture.</summary>
        protected SqliteInMemoryFixture Fixture { get; private set; } = null!;

        /// <summary>Logger factory for creating typed loggers.</summary>
        protected ILoggerFactory LoggerFactory { get; private set; } = null!;

        /// <summary>Initialize database fixture and logger factory. Subclass must call base.InitializeAsync().</summary>
        public virtual async ValueTask InitializeAsync()
        {
            Fixture = new SqliteInMemoryFixture();
            await Fixture.InitializeAsync();
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
        }

        /// <summary>Dispose fixture and logger factory. Subclass must call base.DisposeAsync().</summary>
        public virtual async ValueTask DisposeAsync()
        {
            await Fixture.DisposeAsync();
            Fixture.Dispose();
            LoggerFactory.Dispose();
        }

        /// <summary>Create a typed logger for the given type.</summary>
        protected ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    }
}
