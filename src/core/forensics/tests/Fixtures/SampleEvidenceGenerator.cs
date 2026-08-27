using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Providers.Ring.Forensics.Tests.Fixtures
{
    /// <summary>
    /// Generates realistic sample evidence and events for testing.
    /// </summary>
    public static class SampleEvidenceGenerator
    {
        public static EvidenceMetadata GenerateBasicEvidence(string deviceId = "test-camera-001")
        {
            return new EvidenceMetadata
            {
                SourceDeviceId = deviceId,
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "test-examiner",
                ExtractedData = new Dictionary<string, object>
                {
                    { "motion_detected", true },
                    { "thumbnail_available", true }
                },
                Checksums = new Dictionary<string, string>
                {
                    { "sha256", GenerateRandomHash() }
                }
            };
        }

        public static List<DoorbotHistoryEvent> GenerateEventSequence(
            string deviceId = "test-camera-001",
            int eventCount = 10,
            double? baselineRssi = -50)
        {
            var events = new List<DoorbotHistoryEvent>();
            var now = DateTime.UtcNow;

            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new DoorbotHistoryEvent
                {
                    // Set event properties with RSSI data
                    // This will be populated based on actual DoorbotHistoryEvent structure
                });
            }

            return events;
        }

        public static List<DoorbotHistoryEvent> GenerateAnomalousSequence(
            string deviceId = "test-camera-001",
            int normalEventCount = 5,
            int anomalousEventCount = 3)
        {
            var events = new List<DoorbotHistoryEvent>();

            // Add normal events
            events.AddRange(GenerateEventSequence(deviceId, normalEventCount));

            // Add anomalous events (weak signal)
            events.AddRange(GenerateEventSequence(deviceId, anomalousEventCount, baselineRssi: -80));

            return events.ToList();
        }

        public static RssiStatistics GenerateBaselineStatistics(string deviceId = "test-camera-001")
        {
            return new RssiStatistics
            {
                DeviceId = deviceId,
                MedianRssi = -55,
                StandardDeviation = 5.0,
                MinRssi = -65,
                MaxRssi = -45,
                SampleCount = 100,
                AnalyzedFrom = DateTime.UtcNow.AddDays(-7),
                AnalyzedTo = DateTime.UtcNow
            };
        }

        private static string GenerateRandomHash()
        {
            var random = new Random();
            const string chars = "0123456789abcdef";
            var result = new StringBuilder();

            for (int i = 0; i < 64; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }
    }
}
