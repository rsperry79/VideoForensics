using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    /// <summary>Verifies that <see cref="EventDetail"/> maps to <see cref="EventDetailDto"/> without losing fields or order.</summary>
    public class EventDetailDtoMappingTests
    {
        /// <summary>Verifies every field is carried across, including the nested child rows.</summary>
        [Fact]
        public void EventDetail_ToDto_AllFieldsCarried()
        {
            var eventId = Guid.NewGuid();
            var detection = new EventDetection
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                PersonDetected = true,
                StreamBroken = false,
                DetectionType = "Motion",
                FullDescription = "full",
                ShortDescription = "short",
                Similarity = 0.5m,
                Anomaly = 0.1m,
                Confidence = 0.9m,
                ModelVersion = "v1",
            };
            var detail = new EventDetail(
                eventId,
                detection,
                new[] { new EventDetectionTypeOccurrence { Id = Guid.NewGuid(), EventDetectionId = detection.Id, DetectionType = "Motion", DetectedAtUtc = new DateTime(2026, 10, 1, 0, 0, 1, DateTimeKind.Utc) } },
                new[] { new EventDetectionZone { Id = Guid.NewGuid(), EventDetectionId = detection.Id, ZoneId = "z", ZoneName = "Drive", Confidence = 0.8m } },
                new[] { new EventDetectedPerson { Id = Guid.NewGuid(), EventId = eventId, ProfileId = "p", ProfileName = "Alex", Confidence = 0.7m, ThumbnailUrl = "https://example.test/t.jpg" } },
                new[] { new EventSecurityAlert { Id = Guid.NewGuid(), EventId = eventId, Severity = "High", AlertText = "alert" } });

            EventDetailDto dto = detail.ToDto();

            Assert.Equal(eventId, dto.EventId);
            Assert.NotNull(dto.Detection);
            Assert.Equal(true, dto.Detection!.PersonDetected);
            Assert.Equal(false, dto.Detection.StreamBroken);
            Assert.Equal("Motion", dto.Detection.DetectionType);
            Assert.Equal("full", dto.Detection.FullDescription);
            Assert.Equal("short", dto.Detection.ShortDescription);
            Assert.Equal(0.5m, dto.Detection.Similarity);
            Assert.Equal(0.1m, dto.Detection.Anomaly);
            Assert.Equal(0.9m, dto.Detection.Confidence);
            Assert.Equal("v1", dto.Detection.ModelVersion);
            Assert.Equal("Motion", Assert.Single(dto.Occurrences).DetectionType);
            Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 1, DateTimeKind.Utc), Assert.Single(dto.Occurrences).DetectedAtUtc);
            Assert.Equal("Drive", Assert.Single(dto.Zones).ZoneName);
            Assert.Equal(0.8m, Assert.Single(dto.Zones).Confidence);
            Assert.Equal("Alex", Assert.Single(dto.People).ProfileName);
            Assert.Equal("https://example.test/t.jpg", Assert.Single(dto.People).ThumbnailUrl);
            Assert.Equal("High", Assert.Single(dto.SecurityAlerts).Severity);
            Assert.Equal("alert", Assert.Single(dto.SecurityAlerts).AlertText);
        }

        /// <summary>Verifies that a missing detection and null optional fields stay null rather than becoming defaults.</summary>
        [Fact]
        public void EventDetail_ToDto_NullsPreserved()
        {
            var eventId = Guid.NewGuid();
            var detail = new EventDetail(
                eventId,
                null,
                Array.Empty<EventDetectionTypeOccurrence>(),
                Array.Empty<EventDetectionZone>(),
                new[] { new EventDetectedPerson { Id = Guid.NewGuid(), EventId = eventId, ProfileId = "p", ProfileName = null, Confidence = null, ThumbnailUrl = null } },
                Array.Empty<EventSecurityAlert>());

            EventDetailDto dto = detail.ToDto();

            Assert.Null(dto.Detection);
            Assert.Empty(dto.Occurrences);
            Assert.Empty(dto.Zones);
            Assert.Empty(dto.SecurityAlerts);
            EventDetailDto.EventPersonDto person = Assert.Single(dto.People);
            Assert.Null(person.ProfileName);
            Assert.Null(person.Confidence);
            Assert.Null(person.ThumbnailUrl);
        }

        /// <summary>Verifies occurrences keep the order they were given in, so the client can rely on time order.</summary>
        [Fact]
        public void EventDetail_ToDto_OccurrencesKeepOrder()
        {
            var eventId = Guid.NewGuid();
            var first = new DateTime(2026, 10, 1, 0, 0, 1, DateTimeKind.Utc);
            var second = new DateTime(2026, 10, 1, 0, 0, 9, DateTimeKind.Utc);
            var detail = new EventDetail(
                eventId,
                null,
                new[]
                {
                    new EventDetectionTypeOccurrence { Id = Guid.NewGuid(), EventDetectionId = Guid.NewGuid(), DetectionType = "A", DetectedAtUtc = first },
                    new EventDetectionTypeOccurrence { Id = Guid.NewGuid(), EventDetectionId = Guid.NewGuid(), DetectionType = "B", DetectedAtUtc = second },
                },
                Array.Empty<EventDetectionZone>(),
                Array.Empty<EventDetectedPerson>(),
                Array.Empty<EventSecurityAlert>());

            EventDetailDto dto = detail.ToDto();

            Assert.Equal(new[] { "A", "B" }, dto.Occurrences.Select(o => o.DetectionType).ToArray());
            Assert.Equal(new[] { first, second }, dto.Occurrences.Select(o => o.DetectedAtUtc).ToArray());
        }
    }
}
