using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Calibration reads sessions from the main database and telemetry from logs.db. A bucket must still calibrate after the
    /// telemetry moved, so this seeds the two databases separately and checks the baseline is built from the logs samples.
    /// </summary>
    public class CameraBitrateBaselineTelemetryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _main = null!;
        private SqliteLogsInMemoryFixture _logs = null!;

        public async ValueTask InitializeAsync()
        {
            _main = new SqliteInMemoryFixture();
            await _main.InitializeAsync();
            _logs = new SqliteLogsInMemoryFixture();
            await _logs.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _main.DisposeAsync();
            _main.Dispose();
            await _logs.DisposeAsync();
            _logs.Dispose();
        }

        [Fact]
        public async Task RecomputeBucket_BuildsBaselineFromTelemetryInLogsDb()
        {
            var deviceId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            // A Wednesday at 14:00 UTC, so the bucket is hour 14, weekday.
            DateTime capturedAt = new(2026, 10, 21, 14, 5, 0, DateTimeKind.Utc);

            await using (VideoForensicsDbContext db = await _main.Factory.CreateDbContextAsync())
            {
                _ = db.LiveViewSessions.Add(new LiveViewSession { Id = sessionId, DeviceId = deviceId, StartedAtUtc = capturedAt, LastExtendedAtUtc = capturedAt });
                _ = await db.SaveChangesAsync();
            }
            await using (VideoForensicsLogsDbContext logs = await _logs.Factory.CreateDbContextAsync())
            {
                for (int i = 0; i < 3; i++)
                {
                    _ = logs.LiveViewTelemetrySamples.Add(new LiveViewTelemetrySample
                    {
                        Id = Guid.NewGuid(),
                        SessionId = sessionId,
                        CapturedAtUtc = capturedAt.AddMinutes(i),
                        BitrateBps = 1_000_000 + i,
                    });
                }
                _ = await logs.SaveChangesAsync();
            }

            var repository = new CameraBitrateBaselineRepository(_main.Factory, _logs.Factory, NullLogger<CameraBitrateBaselineRepository>.Instance);
            await repository.RecomputeBucketAsync(deviceId, 14, isWeekend: false, CancellationToken.None);

            CameraBitrateBaseline? baseline = await repository.GetBucketAsync(deviceId, 14, isWeekend: false, CancellationToken.None);
            Assert.NotNull(baseline);
            Assert.Equal(1_000_001, baseline!.MedianBitrateBps);
        }
    }
}
