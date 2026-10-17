using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class ProviderSyncRunDtosTests
    {
        private static ProviderSyncRun CreateRun(bool success, string? errorMessage, DateTime? completedUtc) => new()
        {
            Id = Guid.NewGuid(),
            ProviderAccountId = Guid.NewGuid(),
            StartedUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            CompletedUtc = completedUtc,
            Success = success,
            ErrorMessage = errorMessage
        };

        [Fact]
        public void ProviderSyncRunDtoMapping_ToDto_MapsAllFields()
        {
            ProviderSyncRun run = CreateRun(success: true, errorMessage: null, completedUtc: new DateTime(2026, 10, 1, 12, 5, 0, DateTimeKind.Utc));

            ProviderSyncRunDetailDto dto = run.ToDto();

            Assert.Equal(run.Id, dto.Id);
            Assert.Equal(run.ProviderAccountId, dto.ProviderAccountId);
            Assert.Equal(run.StartedUtc, dto.StartedUtc);
            Assert.Equal(run.CompletedUtc, dto.CompletedUtc);
            Assert.True(dto.Success);
            Assert.Null(dto.ErrorMessage);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToDto_FailedRun_CarriesErrorMessage()
        {
            ProviderSyncRun run = CreateRun(success: false, errorMessage: "Credentials expired", completedUtc: DateTime.UtcNow);

            ProviderSyncRunDetailDto dto = run.ToDto();

            Assert.False(dto.Success);
            Assert.Equal("Credentials expired", dto.ErrorMessage);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToDto_RunWithoutCompletion_LeavesCompletedUtcNull()
        {
            ProviderSyncRun run = CreateRun(success: false, errorMessage: null, completedUtc: null);

            ProviderSyncRunDetailDto dto = run.ToDto();

            Assert.Null(dto.CompletedUtc);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToSummaryDto_MapsIdTimesAndSuccess()
        {
            var completed = new DateTime(2026, 10, 1, 12, 5, 0, DateTimeKind.Utc);
            ProviderSyncRun run = CreateRun(success: true, errorMessage: null, completedUtc: completed);

            ProviderSyncRunSummaryDto dto = run.ToSummaryDto();

            Assert.Equal(run.Id, dto.Id);
            Assert.Equal(run.StartedUtc, dto.StartedUtc);
            Assert.Equal(completed, dto.CompletedUtc);
            Assert.True(dto.Success);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToSummaryDto_FailedRun_KeepsSuccessFalse()
        {
            ProviderSyncRun run = CreateRun(success: false, errorMessage: "boom", completedUtc: null);

            ProviderSyncRunSummaryDto dto = run.ToSummaryDto();

            Assert.False(dto.Success);
            Assert.Null(dto.CompletedUtc);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToSummaryDto_MapsSyncTypeAndCounts()
        {
            ProviderSyncRun run = CreateRun(success: true, errorMessage: null, completedUtc: null);
            run.SyncType = "Full";
            run.DevicesSynced = 3;
            run.LocationsSynced = 1;
            run.EventsSynced = 42;
            run.ConfigAreasSynced = 6;
            run.FeedsFailed = 0;

            ProviderSyncRunSummaryDto dto = run.ToSummaryDto();

            Assert.Equal("Full", dto.SyncType);
            Assert.Equal(3, dto.DevicesSynced);
            Assert.Equal(1, dto.LocationsSynced);
            Assert.Equal(42, dto.EventsSynced);
            Assert.Equal(6, dto.ConfigAreasSynced);
            Assert.Equal(0, dto.FeedsFailed);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToSummaryDto_OldRowKeepsUnknownTypeAndNullCounts()
        {
            ProviderSyncRun run = CreateRun(success: true, errorMessage: null, completedUtc: null);

            ProviderSyncRunSummaryDto dto = run.ToSummaryDto();

            Assert.Equal("Unknown", dto.SyncType);
            Assert.Null(dto.DevicesSynced);
            Assert.Null(dto.EventsSynced);
            Assert.Null(dto.FeedsFailed);
        }

        [Fact]
        public void ProviderSyncRunDtoMapping_ToDto_MapsSyncTypeAndCounts()
        {
            ProviderSyncRun run = CreateRun(success: false, errorMessage: "boom", completedUtc: null);
            run.SyncType = "Events";
            run.EventsSynced = 7;
            run.FeedsFailed = 2;

            ProviderSyncRunDetailDto dto = run.ToDto();

            Assert.Equal("Events", dto.SyncType);
            Assert.Equal(7, dto.EventsSynced);
            Assert.Equal(2, dto.FeedsFailed);
            Assert.Null(dto.ConfigAreasSynced);
        }
    }
}
