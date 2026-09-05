using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class ProviderApiErrorLogRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ProviderApiErrorLogRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new ProviderApiErrorLogRepository(_fixture.Factory);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static ProviderApiErrorLog BuildEntry(Guid? eventId, Guid deviceId, int attemptNumber, string category)
        {
            return new ProviderApiErrorLog
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                DeviceId = deviceId,
                AttemptNumber = attemptNumber,
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(attemptNumber),
                HttpStatusCode = 404,
                ResponseBody = "{\"error\":\"not found\"}",
                ExceptionType = "DeviceUnknownException",
                ErrorMessage = "not found",
                ErrorCategory = category
            };
        }

        [Fact]
        public async Task RecordAsync_ThenGetByEventId_RoundTrips()
        {
            var eventId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            ProviderApiErrorLog entry = BuildEntry(eventId, deviceId, 1, "RecordingNotFound");

            await _repository.RecordAsync(entry, CancellationToken.None);
            IReadOnlyList<ProviderApiErrorLog> results = await _repository.GetByEventIdAsync(eventId, CancellationToken.None);

            var retrieved = Assert.Single(results);
            Assert.Equal(entry.Id, retrieved.Id);
            Assert.Equal(eventId, retrieved.EventId);
            Assert.Equal(404, retrieved.HttpStatusCode);
            Assert.Equal("RecordingNotFound", retrieved.ErrorCategory);
        }

        [Fact]
        public async Task RecordAsync_MultipleAttemptsForSameEvent_AllPersistedAndReturned()
        {
            var eventId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await _repository.RecordAsync(BuildEntry(eventId, deviceId, 1, "RecordingNotFound"), CancellationToken.None);
            await _repository.RecordAsync(BuildEntry(eventId, deviceId, 2, "RecordingNotFound"), CancellationToken.None);
            await _repository.RecordAsync(BuildEntry(eventId, deviceId, 3, "RecordingDeletedAfterDownload"), CancellationToken.None);

            IReadOnlyList<ProviderApiErrorLog> results = await _repository.GetByEventIdAsync(eventId, CancellationToken.None);

            Assert.Equal(3, results.Count);
            Assert.Equal("RecordingDeletedAfterDownload", results[^1].ErrorCategory);
        }

        [Fact]
        public async Task GetByDeviceIdAsync_ReturnsEntriesWithNullEventId()
        {
            var deviceId = Guid.NewGuid();
            ProviderApiErrorLog entry = BuildEntry(eventId: null, deviceId, 1, "Other");

            await _repository.RecordAsync(entry, CancellationToken.None);
            IReadOnlyList<ProviderApiErrorLog> results = await _repository.GetByDeviceIdAsync(deviceId, CancellationToken.None);

            var retrieved = Assert.Single(results);
            Assert.Null(retrieved.EventId);
            Assert.Equal(deviceId, retrieved.DeviceId);
        }

        [Fact]
        public async Task GetByEventIdAsync_NoMatches_ReturnsEmpty()
        {
            IReadOnlyList<ProviderApiErrorLog> results = await _repository.GetByEventIdAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Empty(results);
        }
    }
}
