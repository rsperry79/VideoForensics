using Bogus;

using VideoForensics.Core.Helpers.Forensics.Models;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Testing.Fixtures
{
    /// <summary>
    /// Fluent builder for constructing test forensic data.
    /// EvidenceMetadata defaults follow forensic extraction patterns.
    /// </summary>
    public class ForensicTestDataBuilder
    {
        private readonly Faker<EvidenceMetadata> _faker;
        private readonly Dictionary<string, object> _extractedDataOverrides = [];
        private readonly Dictionary<string, string> _checksumOverrides = [];
        private readonly List<DoorbotHistoryEvent> _events = [];

        public ForensicTestDataBuilder()
        {
            _faker = new Faker<EvidenceMetadata>()
                .RuleFor(e => e.SourceDeviceId, f => f.Random.String(16))
                .RuleFor(e => e.EventTimestamp, f => f.Date.Recent())
                .RuleFor(e => e.EventType, f => f.PickRandom("motion", "doorbell", "battery", "motion_detection"))
                .RuleFor(e => e.ExtractionHandler, _ => "ForensicExtractor")
                .RuleFor(e => e.ExtractedData, _ => [])
                .RuleFor(e => e.Checksums, _ => []);
        }

        public ForensicTestDataBuilder WithDeviceId(string deviceId)
        {
            _ = _faker.RuleFor(e => e.SourceDeviceId, _ => deviceId);
            return this;
        }

        public ForensicTestDataBuilder WithEventTimestamp(DateTime timestamp)
        {
            _ = _faker.RuleFor(e => e.EventTimestamp, _ => timestamp);
            return this;
        }

        public ForensicTestDataBuilder WithEventType(string eventType)
        {
            _ = _faker.RuleFor(e => e.EventType, _ => eventType);
            return this;
        }

        public ForensicTestDataBuilder WithExtractedData(string key, object value)
        {
            _extractedDataOverrides[key] = value;
            return this;
        }

        public ForensicTestDataBuilder WithChecksum(string algorithm, string hash)
        {
            _checksumOverrides[algorithm] = hash;
            return this;
        }

        public ForensicTestDataBuilder WithHandler(string handler)
        {
            _ = _faker.RuleFor(e => e.ExtractionHandler, _ => handler);
            return this;
        }

        public ForensicTestDataBuilder AddEvent(DoorbotHistoryEvent @event)
        {
            _events.Add(@event);
            return this;
        }

        public EvidenceMetadata BuildEvidence()
        {
            EvidenceMetadata evidence = _faker.Generate();

            // Apply any extracted data overrides
            foreach (KeyValuePair<string, object> kvp in _extractedDataOverrides)
            {
                evidence.ExtractedData[kvp.Key] = kvp.Value;
            }

            // Apply any checksum overrides
            foreach (KeyValuePair<string, string> kvp in _checksumOverrides)
            {
                evidence.Checksums[kvp.Key] = kvp.Value;
            }

            return evidence;
        }

        public List<DoorbotHistoryEvent> BuildEvents()
        {
            return _events;
        }

        public static ForensicTestDataBuilder Create()
        {
            return new ForensicTestDataBuilder();
        }
    }
}
