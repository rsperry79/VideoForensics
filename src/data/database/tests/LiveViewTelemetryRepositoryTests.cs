using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Telemetry is written to and read from logs.db, and pruning removes only samples past the cutoff.</summary>
    public class LiveViewTelemetryRepositoryTests : IAsyncLifetime
    {
        private SqliteLogsInMemoryFixture _fixture = null!;
        private LiveViewTelemetryRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteLogsInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new LiveViewTelemetryRepository(_fixture.Factory, NullLogger<LiveViewTelemetryRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static LiveViewTelemetrySample Sample(Guid sessionId, DateTime capturedAtUtc) =>
            new() { Id = Guid.NewGuid(), SessionId = sessionId, CapturedAtUtc = capturedAtUtc };

        [Fact]
        public async Task AddThenGet_ReturnsTheSessionsSamplesInTimeOrder()
        {
            var sessionId = Guid.NewGuid();
            var later = Sample(sessionId, DateTime.UtcNow.AddMinutes(-1));
            var earlier = Sample(sessionId, DateTime.UtcNow.AddMinutes(-5));
            await _repository.AddSampleAsync(later, CancellationToken.None);
            await _repository.AddSampleAsync(earlier, CancellationToken.None);
            await _repository.AddSampleAsync(Sample(Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);

            IReadOnlyList<LiveViewTelemetrySample> samples = await _repository.GetSamplesAsync(sessionId, CancellationToken.None);

            Assert.Equal(new[] { earlier.Id, later.Id }, samples.Select(s => s.Id).ToArray());
        }

        [Fact]
        public async Task PruneOlderThan_DeletesOnlyOldSamples()
        {
            var sessionId = Guid.NewGuid();
            var old = Sample(sessionId, DateTime.UtcNow.AddDays(-40));
            var recent = Sample(sessionId, DateTime.UtcNow.AddDays(-1));
            await _repository.AddSampleAsync(old, CancellationToken.None);
            await _repository.AddSampleAsync(recent, CancellationToken.None);

            int deleted = await _repository.PruneOlderThanAsync(TimeSpan.FromDays(30), CancellationToken.None);

            Assert.Equal(1, deleted);
            IReadOnlyList<LiveViewTelemetrySample> remaining = await _repository.GetSamplesAsync(sessionId, CancellationToken.None);
            Assert.Equal(new[] { recent.Id }, remaining.Select(s => s.Id).ToArray());
        }
    }
}
