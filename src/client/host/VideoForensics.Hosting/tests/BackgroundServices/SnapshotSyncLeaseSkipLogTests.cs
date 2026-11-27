using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    public class SnapshotSyncLeaseSkipLogTests
    {
        [Fact]
        public async Task SnapshotSyncBackgroundService_LeaseHeld_LogsSkipAtWarning()
        {
            // Why: the skip is the only sign that a capture tick did nothing, so it must surface at Warning, not Information.
            var logger = new CapturingLogger();
            IServiceProvider services = new ServiceCollection()
                .AddSingleton<IMaintenanceGate>(new ClosedGate())
                .BuildServiceProvider();
            var service = new SnapshotSyncBackgroundService(
                services.GetRequiredService<IServiceScopeFactory>(),
                logger,
                TimeProvider.System);

            await service.RunOneTickAsync(CancellationToken.None);

            (LogLevel Level, string Message) entry = Assert.Single(logger.Entries, e => e.Message.Contains("Snapshot capture tick skipped"));
            Assert.Equal(LogLevel.Warning, entry.Level);
        }

        /// <summary>A gate that always refuses new work, as during maintenance.</summary>
        private sealed class ClosedGate : IMaintenanceGate
        {
            public MaintenanceStatusDto Current => MaintenanceStatusDto.Normal;

            public IDisposable? TryEnterWork() => null;

            public Task BeginDrainAsync(string message, CancellationToken ct) => Task.CompletedTask;

            public void MarkRunning(string message)
            {
            }

            public void End()
            {
            }

            public IDisposable Subscribe(IObserver<MaintenanceStatusDto> observer) => new NoopDisposable();

            private sealed class NoopDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }

        /// <summary>Records each log entry with its level.</summary>
        private sealed class CapturingLogger : ILogger<SnapshotSyncBackgroundService>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}