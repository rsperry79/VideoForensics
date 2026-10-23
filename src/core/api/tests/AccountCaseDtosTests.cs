using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class AccountCaseDtosTests
    {
        private static ForensicCase CreateCase(string caseNumber, CaseStatus status = CaseStatus.Open)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                CaseNumber = caseNumber,
                Title = "Driveway motion",
                Status = status,
                CreatedBy = "admin",
                CreatedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc),
                ScopeFromUtc = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        [Fact]
        public void ForensicCaseDtoMapping_AccountCaseRoundTrip_KeepsListFields()
        {
            ForensicCase entity = CreateCase("detected-2026-10-01-12-00-0", CaseStatus.Closed);

            ForensicCase result = entity.ToDto().ToDomain();

            Assert.Equal(entity.Id, result.Id);
            Assert.Equal(entity.CaseNumber, result.CaseNumber);
            Assert.Equal(entity.Title, result.Title);
            Assert.Equal(entity.Status, result.Status);
            Assert.Equal(entity.CreatedAtUtc, result.CreatedAtUtc);
            Assert.Equal(entity.ScopeFromUtc, result.ScopeFromUtc);
        }

        [Fact]
        public void ForensicCaseDtoMapping_AccountCaseRoundTrip_DocumentsPartialMapping()
        {
            ForensicCase entity = CreateCase("manual-2026-10-01-12-00-1");

            ForensicCase result = entity.ToDto().ToDomain();

            // AccountCaseDto carries no creator or update timestamp, so ToDomain fills placeholders.
            Assert.Equal(string.Empty, result.CreatedBy);
            Assert.Equal(result.CreatedAtUtc, result.UpdatedAtUtc);
        }

        [Fact]
        public void ForensicCaseDtoMapping_AccountCaseToDomain_InvalidStatus_ThrowsArgumentException()
        {
            AccountCaseDto dto = new(
                Id: Guid.NewGuid(),
                CaseNumber: "manual-2026-10-01-12-00-2",
                Title: "Bad status",
                Status: "NotARealStatus",
                CaseType: "Manual",
                ScopeFromUtc: null,
                CreatedAtUtc: DateTime.UtcNow);

            _ = Assert.Throws<ArgumentException>(dto.ToDomain);
        }

        [Theory]
        [InlineData("detected-2026-10-01-12-00-0", "Detected")]
        [InlineData("suspected-2026-10-01-12-00-1", "Suspected")]
        [InlineData("manual-2026-10-01-12-00-2", "Manual")]
        [InlineData("CASE-2026-001", "Other")]
        public void ForensicCaseDtoMapping_ToDto_DerivesCaseTypeFromPrefix(string caseNumber, string expectedCaseType)
        {
            AccountCaseDto dto = CreateCase(caseNumber).ToDto();

            Assert.Equal(expectedCaseType, dto.CaseType);
        }
    }
}
