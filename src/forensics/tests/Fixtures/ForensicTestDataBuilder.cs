using Ring.Api.Entities;
using Ring.Api.Forensics.Models;

namespace Ring.Api.Forensics.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for constructing test forensic data.
    /// </summary>
    public class ForensicTestDataBuilder
    {
        private EvidenceMetadata _evidence = new();
        private List<DoorbotHistoryEvent> _events = new();

        public ForensicTestDataBuilder WithDeviceId(string deviceId)
        {
            _evidence.SourceDeviceId = deviceId;
            return this;
        }

        public ForensicTestDataBuilder WithEventTimestamp(DateTime timestamp)
        {
            _evidence.EventTimestamp = timestamp;
            return this;
        }

        public ForensicTestDataBuilder WithEventType(string eventType)
        {
            _evidence.EventType = eventType;
            return this;
        }

        public ForensicTestDataBuilder WithExtractedData(string key, object value)
        {
            _evidence.ExtractedData[key] = value;
            return this;
        }

        public ForensicTestDataBuilder WithChecksum(string algorithm, string hash)
        {
            _evidence.Checksums[algorithm] = hash;
            return this;
        }

        public ForensicTestDataBuilder WithHandler(string handler)
        {
            _evidence.ExtractionHandler = handler;
            return this;
        }

        public ForensicTestDataBuilder AddEvent(DoorbotHistoryEvent @event)
        {
            _events.Add(@event);
            return this;
        }

        public EvidenceMetadata BuildEvidence()
        {
            return _evidence;
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
