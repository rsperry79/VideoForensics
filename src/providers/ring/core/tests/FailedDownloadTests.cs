#nullable disable
using System.Collections.Generic;

using VideoForensics.Providers.Ring.Models;

namespace VideoForensics.Providers.Ring.Tests
{
    public class FailedDownloadTests
    {
        [Fact]
        public void FailedDownloadCanBeCreatedWithAllProperties()
        {
            var timestamp = DateTime.UtcNow;
            var createdAt = DateTime.UtcNow.AddMinutes(-5);

            var download = new FailedDownload
            {
                Timestamp = timestamp,
                LocationName = "Front Door",
                CameraName = "Doorbell",
                CameraId = 123,
                EventId = "evt_456",
                EventType = "motion",
                CreatedAt = createdAt,
                ErrorDescription = "Network timeout"
            };

            Assert.Equal(timestamp, download.Timestamp);
            Assert.Equal("Front Door", download.LocationName);
            Assert.Equal("Doorbell", download.CameraName);
            Assert.Equal(123, download.CameraId);
            Assert.Equal("evt_456", download.EventId);
            Assert.Equal("motion", download.EventType);
            Assert.Equal(createdAt, download.CreatedAt);
            Assert.Equal("Network timeout", download.ErrorDescription);
        }

        [Fact]
        public void FailedDownloadHandlesNullValues()
        {
            var download = new FailedDownload
            {
                Timestamp = DateTime.UtcNow,
                LocationName = null,
                CameraName = null,
                CameraId = 0,
                EventId = null,
                EventType = null,
                CreatedAt = DateTime.UtcNow,
                ErrorDescription = "Test error"
            };

            Assert.Null(download.LocationName);
            Assert.Null(download.CameraName);
            Assert.Equal("Test error", download.ErrorDescription);
        }

        [Fact]
        public void FailedDownloadCanBeDeduplicatedByEventAndCamera()
        {
            var now = DateTime.UtcNow;
            var downloads = new List<FailedDownload>
            {
                new() { EventId = "1", CameraId = 10, Timestamp = now, CreatedAt = now, ErrorDescription = "Error 1" },
                new() { EventId = "1", CameraId = 10, Timestamp = now, CreatedAt = now, ErrorDescription = "Error 1" }, // Duplicate
                new() { EventId = "2", CameraId = 10, Timestamp = now, CreatedAt = now, ErrorDescription = "Error 2" }
            };

            var deduped = downloads
                .GroupBy(d => new { d.CameraId, d.EventId })
                .Select(g => g.First())
                .ToList();

            Assert.Equal(2, deduped.Count);
        }

        [Fact]
        public void FailedDownloadListCanBeTsvSerialized()
        {
            var downloads = new List<FailedDownload>
            {
                new()
                {
                    Timestamp = DateTime.UtcNow,
                    LocationName = "Front",
                    CameraName = "Doorbell",
                    CameraId = 1,
                    EventId = "e1",
                    EventType = "motion",
                    CreatedAt = DateTime.UtcNow,
                    ErrorDescription = "Timeout"
                }
            };

            // TSV format: tab-separated values with headers
            var header = "Timestamp\tLocationName\tCameraName\tCameraId\tEventId\tEventType\tCreatedAt\tErrorDescription";
            var lines = new List<string> { header };

            foreach (var d in downloads)
            {
                var line = $"{d.Timestamp}\t{d.LocationName}\t{d.CameraName}\t{d.CameraId}\t{d.EventId}\t{d.EventType}\t{d.CreatedAt}\t{d.ErrorDescription}";
                lines.Add(line);
            }

            var tsvContent = string.Join(Environment.NewLine, lines);
            Assert.False(string.IsNullOrEmpty(tsvContent));
            Assert.True(tsvContent.Contains("Timeout"));
            Assert.True(tsvContent.Contains("Doorbell"));
        }
    }
}
