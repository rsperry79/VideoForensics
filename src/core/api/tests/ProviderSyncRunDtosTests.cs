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
    }
}
