using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Every unit of work logs its timing at Debug; one that runs past the threshold logs a Warning naming its caller.</summary>
    public class UnitOfWorkTimingTests : IAsyncLifetime
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();
        private SqliteInMemoryFixture _fixture = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b =>
            {
                b.SetMinimumLevel(LogLevel.Debug);
                b.AddProvider(new EntryCollector(_entries));
            });
        }

        public async ValueTask DisposeAsync()
        {
            _loggerFactory.Dispose();
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private UnitOfWork Build(TimeSpan? slowThreshold)
        {
            var serviceProvider = new TestServiceProvider(_fixture, _loggerFactory);
            return new UnitOfWork(_fixture.Factory, serviceProvider, _loggerFactory.CreateLogger<UnitOfWork>(), slowThreshold);
        }

        private IEnumerable<(LogLevel Level, string Message)> UnitOfWorkEntries()
        {
            return _entries.Where(e => e.Message.Contains("unit of work", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ExecuteAsync_FastTransaction_LogsTimingAtDebugOnly()
        {
            UnitOfWork unitOfWork = Build(TimeSpan.FromMinutes(1));

            _ = await unitOfWork.ExecuteAsync<object?>(_ => Task.FromResult<object?>(null), CancellationToken.None);

            Assert.Contains(UnitOfWorkEntries(), e => e.Level == LogLevel.Debug && e.Message.Contains("committed"));
            Assert.DoesNotContain(UnitOfWorkEntries(), e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task ExecuteAsync_SlowTransaction_LogsWarningNamingCallerAndPhases()
        {
            UnitOfWork unitOfWork = Build(TimeSpan.Zero);

            _ = await unitOfWork.ExecuteAsync<object?>(_ => Task.FromResult<object?>(null), CancellationToken.None);

            (LogLevel Level, string Message) warning = Assert.Single(UnitOfWorkEntries(), e => e.Level == LogLevel.Warning);
            Assert.Contains("UnitOfWorkTimingTests.ExecuteAsync_SlowTransaction_LogsWarningNamingCallerAndPhases", warning.Message);
            Assert.Contains("committed", warning.Message);
            Assert.Contains("begin", warning.Message);
            Assert.Contains("work", warning.Message);
            Assert.Contains("commit", warning.Message);
        }

        [Fact]
        public async Task ExecuteAsync_RolledBackTransaction_LogsOutcomeAsRolledBack()
        {
            UnitOfWork unitOfWork = Build(TimeSpan.Zero);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<object?>(
                _ => Task.FromException<object?>(new InvalidOperationException("boom")), CancellationToken.None));

            Assert.Contains(UnitOfWorkEntries(), e => e.Level == LogLevel.Warning && e.Message.Contains("rolled back"));
        }

        private sealed class EntryCollector(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new Collector(entries);

            public void Dispose()
            {
            }

            private sealed class Collector(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    entries.Enqueue((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
