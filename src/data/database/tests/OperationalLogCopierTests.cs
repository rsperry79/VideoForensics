using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database;
using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Old operational rows in the main database are copied to logs.db once, and copying again adds nothing. Telemetry is not
    /// seeded here: its rows need a live-view session with device and location rows, which this test does not build.
    /// </summary>
    public class OperationalLogCopierTests : IAsyncLifetime
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
        public async Task CopyAsync_CopiesMissingRowsAndIsIdempotent()
        {
            var deviceId = Guid.NewGuid();
            var errorId = Guid.NewGuid();
            var callId = Guid.NewGuid();
            await using (VideoForensicsDbContext db = await _main.Factory.CreateDbContextAsync())
            {
                _ = db.ProviderApiErrorLogs.Add(new ProviderApiErrorLog
                {
                    Id = errorId,
                    DeviceId = deviceId,
                    AttemptNumber = 1,
                    OccurredAtUtc = DateTime.UtcNow,
                    ErrorCategory = "Other",
                });
                _ = db.ProviderApiCallRecords.Add(new ProviderApiCallRecord { Id = callId, ProviderName = "Ring", TimestampUtc = DateTime.UtcNow });
                _ = await db.SaveChangesAsync();
            }

            int first = await OperationalLogCopier.CopyAsync(_main.Factory, _logs.Factory, CancellationToken.None);
            int second = await OperationalLogCopier.CopyAsync(_main.Factory, _logs.Factory, CancellationToken.None);

            Assert.Equal(2, first);
            Assert.Equal(0, second);
            await using VideoForensicsLogsDbContext logs = await _logs.Factory.CreateDbContextAsync();
            Assert.Single(logs.ProviderApiErrorLogs.Where(e => e.Id == errorId));
            Assert.Single(logs.ProviderApiCallRecords.Where(c => c.Id == callId));
        }
    }
}
