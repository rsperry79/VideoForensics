using Bogus;

namespace VideoForensics.Providers.Ring.Forensics.Tests.Fixtures
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
                .RuleFor(e => e.ExtractedData, _ => new Dictionary<string, object>())
                .RuleFor(e => e.Checksums, _ => new Dictionary<string, string>());
        }

        public ForensicTestDataBuilder WithDeviceId(string deviceId)
        {
            _faker.RuleFor(e => e.SourceDeviceId, _ => deviceId);
            return this;
        }

        public ForensicTestDataBuilder WithEventTimestamp(DateTime timestamp)
        {
            _faker.RuleFor(e => e.EventTimestamp, _ => timestamp);
            return this;
        }

        public ForensicTestDataBuilder WithEventType(string eventType)
        {
            _faker.RuleFor(e => e.EventType, _ => eventType);
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
            _faker.RuleFor(e => e.ExtractionHandler, _ => handler);
            return this;
        }

        public ForensicTestDataBuilder AddEvent(DoorbotHistoryEvent @event)
        {
            _events.Add(@event);
            return this;
        }

        public EvidenceMetadata BuildEvidence()
        {
            var evidence = _faker.Generate();

            // Apply any extracted data overrides
            foreach (var kvp in _extractedDataOverrides)
            {
                evidence.ExtractedData[kvp.Key] = kvp.Value;
            }

            // Apply any checksum overrides
            foreach (var kvp in _checksumOverrides)
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
