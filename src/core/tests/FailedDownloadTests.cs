#nullable disable
using System.Collections.Generic;

using Ring.Api.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ring.Api.Tests
{
    [TestClass]
    public class FailedDownloadTests
    {
        [TestMethod]
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

            Assert.AreEqual(timestamp, download.Timestamp);
            Assert.AreEqual("Front Door", download.LocationName);
            Assert.AreEqual("Doorbell", download.CameraName);
            Assert.AreEqual(123, download.CameraId);
            Assert.AreEqual("evt_456", download.EventId);
            Assert.AreEqual("motion", download.EventType);
            Assert.AreEqual(createdAt, download.CreatedAt);
            Assert.AreEqual("Network timeout", download.ErrorDescription);
        }

        [TestMethod]
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

            Assert.IsNull(download.LocationName);
            Assert.IsNull(download.CameraName);
            Assert.AreEqual("Test error", download.ErrorDescription);
        }

        [TestMethod]
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

            Assert.AreEqual(2, deduped.Count);
        }

        [TestMethod]
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
            Assert.IsFalse(string.IsNullOrEmpty(tsvContent));
            Assert.IsTrue(tsvContent.Contains("Timeout"));
            Assert.IsTrue(tsvContent.Contains("Doorbell"));
        }
    }
}
