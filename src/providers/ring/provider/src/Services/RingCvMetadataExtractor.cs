using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

using DetectedPerson = VideoForensics.Data.Common.Entities.DetectedPerson;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Extracts structured CV metadata from Ring API responses, consolidating shared logic
    /// for both Event-scoped and MediaItem-scoped detection entities.
    /// </summary>
    public static class RingCvMetadataExtractor
    {
        /// <summary>
        /// Extracts structured CV metadata from a Ring event for EventDetection entities.
        /// </summary>
        public static (EventDetection detection, List<EventDetectionZone> zones, List<EventSecurityAlert> alerts, List<EventDetectedPerson> persons, List<EventDetectionTypeOccurrence> occurrences) ExtractEventMetadata(Entities.DoorbotHistoryEvent @event, Guid eventId)
        {
            var zones = new List<EventDetectionZone>();
            var alerts = new List<EventSecurityAlert>();
            var persons = new List<EventDetectedPerson>();
            var occurrences = new List<EventDetectionTypeOccurrence>();

            Entities.CvProperties? cv = @event.CvProperties;
            if (cv == null)
            {
                // No CV data, return minimal detection record
                var emptyDetection = new EventDetection
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId
                };
                return (emptyDetection, zones, alerts, persons, occurrences);
            }

            var detectionId = Guid.NewGuid();

            // Create EventDetection from CV properties
            var detection = new EventDetection
            {
                Id = detectionId,
                EventId = eventId,
                PersonDetected = cv.PersonDetected,
                StreamBroken = cv.StreamBroken,
                DetectionType = cv.DetectionType,
                FullDescription = cv.FullDescription,
                ShortDescription = cv.ShortDescription,
                Similarity = cv.Similarity.HasValue ? (decimal)cv.Similarity.Value : null,
                Anomaly = cv.Anomaly.HasValue ? (decimal)cv.Anomaly.Value : null,
                ModelVersion = cv.DetectionDetails?.ModelVersion
            };

            // Add detection details confidence if available
            if (cv.DetectionDetails?.Confidence.HasValue == true)
            {
                detection.Confidence = (decimal)cv.DetectionDetails.Confidence.Value;
            }

            // Extract detection zones
            if (cv.DetectionDetails?.Zones != null)
            {
                foreach (CvZone zone in cv.DetectionDetails.Zones)
                {
                    if (zone != null && !string.IsNullOrEmpty(zone.Id))
                    {
                        zones.Add(new EventDetectionZone
                        {
                            Id = Guid.NewGuid(),
                            EventDetectionId = detectionId,
                            ZoneId = zone.Id,
                            ZoneName = zone.Name,
                            Confidence = zone.Confidence.HasValue ? (decimal)zone.Confidence.Value : null
                        });
                    }
                }
            }

            // Extract security alerts
            if (cv.SecurityAlerts != null && cv.SecurityAlerts.Alerts != null)
            {
                foreach (string alertText in cv.SecurityAlerts.Alerts)
                {
                    if (!string.IsNullOrEmpty(alertText))
                    {
                        alerts.Add(new EventSecurityAlert
                        {
                            Id = Guid.NewGuid(),
                            EventId = eventId,
                            Severity = cv.SecurityAlerts.Severity,
                            AlertText = alertText
                        });
                    }
                }
            }

            // Extract detected persons
            if (cv.Profiles != null)
            {
                foreach (CvProfile profile in cv.Profiles)
                {
                    if (profile != null && !string.IsNullOrEmpty(profile.Id))
                    {
                        persons.Add(new EventDetectedPerson
                        {
                            Id = Guid.NewGuid(),
                            EventId = eventId,
                            ProfileId = profile.Id,
                            ProfileName = profile.Name,
                            Confidence = profile.Confidence.HasValue ? (decimal)profile.Confidence.Value : null,
                            ThumbnailUrl = profile.ThumbnailUrl
                        });
                    }
                }
            }

            // Extract detection type occurrences from verified timestamps
            if (cv.DetectionTypes != null)
            {
                foreach (CvDetectionType detectionType in cv.DetectionTypes)
                {
                    if (detectionType != null && !string.IsNullOrEmpty(detectionType.DetectionType) && detectionType.VerifiedTimestamps != null)
                    {
                        foreach (long epochMs in detectionType.VerifiedTimestamps)
                        {
                            DateTime detectedAtUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(epochMs);
                            occurrences.Add(new EventDetectionTypeOccurrence
                            {
                                Id = Guid.NewGuid(),
                                EventDetectionId = detectionId,
                                DetectionType = detectionType.DetectionType,
                                DetectedAtUtc = detectedAtUtc
                            });
                        }
                    }
                }
            }

            return (detection, zones, alerts, persons, occurrences);
        }

        /// <summary>
        /// Extracts structured CV metadata from a Ring event and creates related database entities.
        /// Detection zones and security alerts are captured separately by ExtractEventMetadata (as
        /// EventDetectionZone/EventSecurityAlert, keyed to the Event rather than the MediaItemDetection) -
        /// not duplicated here.
        /// </summary>
        public static (MediaItemDetection detection, List<DetectedPerson> persons, List<DetectionTypeOccurrence> occurrences) ExtractMetadata(Entities.DoorbotHistoryEvent @event, Guid mediaItemId, Guid mediaItemDetectionId)
        {
            var persons = new List<DetectedPerson>();
            var occurrences = new List<DetectionTypeOccurrence>();

            Entities.CvProperties? cv = @event.CvProperties;
            if (cv == null)
            {
                // No CV data, return minimal detection record
                var emptyDetection = new MediaItemDetection
                {
                    Id = mediaItemDetectionId,
                    MediaItemId = mediaItemId
                };
                return (emptyDetection, persons, occurrences);
            }

            // Create MediaItemDetection from CV properties
            var detection = new MediaItemDetection
            {
                Id = mediaItemDetectionId,
                MediaItemId = mediaItemId,
                PersonDetected = cv.PersonDetected,
                StreamBroken = cv.StreamBroken,
                DetectionType = cv.DetectionType,
                FullDescription = cv.FullDescription,
                ShortDescription = cv.ShortDescription,
                Similarity = cv.Similarity.HasValue ? (decimal)cv.Similarity.Value : null,
                Anomaly = cv.Anomaly.HasValue ? (decimal)cv.Anomaly.Value : null,
                ModelVersion = cv.DetectionDetails?.ModelVersion
            };

            // Add detection details confidence if available
            if (cv.DetectionDetails?.Confidence.HasValue == true)
            {
                detection.Confidence = (decimal)cv.DetectionDetails.Confidence.Value;
            }

            // Extract detected persons
            if (cv.Profiles != null)
            {
                foreach (CvProfile profile in cv.Profiles)
                {
                    if (profile != null && !string.IsNullOrEmpty(profile.Id))
                    {
                        persons.Add(new DetectedPerson
                        {
                            Id = Guid.NewGuid(),
                            MediaItemId = mediaItemId,
                            ProfileId = profile.Id,
                            ProfileName = profile.Name,
                            Confidence = profile.Confidence.HasValue ? (decimal)profile.Confidence.Value : null,
                            ThumbnailUrl = profile.ThumbnailUrl
                        });
                    }
                }
            }

            // Extract detection type occurrences from verified timestamps
            if (cv.DetectionTypes != null)
            {
                foreach (CvDetectionType detectionType in cv.DetectionTypes)
                {
                    if (detectionType != null && !string.IsNullOrEmpty(detectionType.DetectionType) && detectionType.VerifiedTimestamps != null)
                    {
                        foreach (long epochMs in detectionType.VerifiedTimestamps)
                        {
                            DateTime detectedAtUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(epochMs);
                            occurrences.Add(new DetectionTypeOccurrence
                            {
                                Id = Guid.NewGuid(),
                                MediaItemDetectionId = mediaItemDetectionId,
                                DetectionType = detectionType.DetectionType,
                                DetectedAtUtc = detectedAtUtc
                            });
                        }
                    }
                }
            }

            return (detection, persons, occurrences);
        }
    }
}
