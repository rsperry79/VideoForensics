using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class JammingDtoMappingTests
    {
        [Fact]
        public void JammingIncidentRecord_ToDto_ToDomain_RoundTripsEveryField()
        {
            var entity = new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                StartUtc = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 3, 1, 10, 45, 0, DateTimeKind.Utc),
                AffectedEventCount = 7,
                AverageDegradationDb = 18.25,
                Confidence = JammingConfidenceLevel.Definite,
                DetectedAtUtc = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
                Notes = "n",
                Source = JammingIncidentSource.AutoDetected,
                CaseId = Guid.NewGuid()
            };

            JammingIncidentRecord back = entity.ToDto().ToDomain();

            Assert.Equivalent(entity, back, strict: true);
        }

        [Fact]
        public void JammingIncidentRecord_ToDto_UsesNamesForEnumsAndKeepsNullCaseId()
        {
            JammingIncidentDto dto = new JammingIncidentRecord { Confidence = JammingConfidenceLevel.Medium, Source = JammingIncidentSource.ManuallyRecorded }.ToDto();

            Assert.Equal("Medium", dto.Confidence);
            Assert.Equal("ManuallyRecorded", dto.Source);
            Assert.Null(dto.CaseId);
        }

        [Fact]
        public void UpsertRequest_ToDomain_SetsManualSourceAndNoCaseId()
        {
            var request = new UpsertJammingIncidentRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 2, 9.5, "Low", "x");

            JammingIncidentRecord record = request.ToDomain();

            Assert.Equal(request.Id, record.Id);
            Assert.Equal(request.DeviceId, record.DeviceId);
            Assert.Equal(JammingConfidenceLevel.Low, record.Confidence);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, record.Source);
            Assert.Null(record.CaseId);
            Assert.Equal("x", record.Notes);
        }

        [Fact]
        public void JammingStatsSummary_ToDto_ToDomain_RoundTripsEveryField()
        {
            var entity = new JammingStatsSummary
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                IncidentCount = 5,
                TotalJammedDurationMinutes = 120.5,
                AverageDegradationDb = 11.1,
                MaxDegradationDb = 22.2,
                LowConfidenceCount = 1,
                MediumConfidenceCount = 2,
                HighConfidenceCount = 1,
                DefiniteConfidenceCount = 1,
                FirstIncidentUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastIncidentUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdatedUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            Assert.Equivalent(entity, entity.ToDto().ToDomain(), strict: true);
        }

        [Fact]
        public void JammingStatsSummary_ToDto_PreservesNullIncidentTimes()
        {
            JammingStatsSummaryDto dto = new JammingStatsSummary { FirstIncidentUtc = null, LastIncidentUtc = null }.ToDto();

            Assert.Null(dto.FirstIncidentUtc);
            Assert.Null(dto.LastIncidentUtc);
        }
    }
}