using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// Guards the provider's known-path set (what the Ring mapper persists structurally) against the field map, and checks a
    /// real-shaped payload reduces to nothing while an invented key survives.
    /// </summary>
    public class RingEventPayloadRemainderGuard_Tests
    {
        private const string FieldMapPrefix = "doorbot-history:[].";

        private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "payload-fixtures", "doorbot-history-events-sanitized.json");

        private static JsonElement[] FixtureEvents()
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FixturePath));
            return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
        }

        [Fact]
        public void KnownPaths_EveryPathHasAStoredDoorbotHistoryFieldMapEntryOrAReasonedExemption()
        {
            FieldMap map = FieldMap.LoadEmbedded();
            var problems = new List<string>();
            foreach (string path in RingEventPayloadRemainder.KnownPaths)
            {
                if (RingEventPayloadRemainder.NotApiKeys.TryGetValue(path, out string? reason))
                {
                    if (string.IsNullOrWhiteSpace(reason))
                    {
                        problems.Add($"{path}: exemption without a reason");
                    }

                    continue;
                }

                if (!map.Entries.TryGetValue(FieldMapPrefix + path, out FieldTarget? target))
                {
                    problems.Add($"{path}: no field-map entry {FieldMapPrefix}{path}");
                }
                else if (target.Kind == FieldTargetKind.NotData)
                {
                    problems.Add($"{path}: field map says NotData, so it is not stored and must not be dropped from the remainder");
                }
            }

            Assert.Empty(problems);
        }

        [Fact]
        public void KnownPaths_ExemptionsAreAllKnownPaths()
        {
            Assert.Empty(RingEventPayloadRemainder.NotApiKeys.Keys.Except(RingEventPayloadRemainder.KnownPaths));
        }

        [Fact]
        public void KnownPaths_EveryStoredDoorbotHistoryFieldMapEntryIsKnown()
        {
            // The other direction: a structured doorbot-history field must not stay in MetadataJson as an "unknown" key.
            FieldMap map = FieldMap.LoadEmbedded();
            string[] stored = map.Entries
                .Where(e => e.Key.StartsWith(FieldMapPrefix, StringComparison.Ordinal) && e.Value.Kind != FieldTargetKind.NotData)
                .Select(e => e.Key[FieldMapPrefix.Length..])
                .ToArray();

            Assert.NotEmpty(stored);
            Assert.Empty(stored.Except(RingEventPayloadRemainder.KnownPaths));
        }

        [Fact]
        public void Fixture_RawApiEvents_LeaveNoRemainder()
        {
            JsonElement[] events = FixtureEvents();
            Assert.True(events.Length >= 5);

            foreach (JsonElement evt in events)
            {
                Assert.Null(RingEventPayloadRemainder.Compute(evt.GetRawText()));
            }
        }

        [Fact]
        public void Fixture_EventsAsTheServiceSerializesThem_LeaveNoRemainder()
        {
            foreach (JsonElement evt in FixtureEvents())
            {
                DoorbotHistoryEvent typed = JsonSerializer.Deserialize<DoorbotHistoryEvent>(evt.GetRawText())!;
                string stored = JsonSerializer.Serialize(typed);

                Assert.Null(RingEventPayloadRemainder.Compute(stored));
                Assert.Null(RingEventPayloadRemainder.Split(typed).RemainderJson);
            }
        }

        [Fact]
        public void Fixture_WithAnInventedExtraKey_KeepsOnlyThatKey()
        {
            JsonElement populated = FixtureEvents()[^1];
            string withExtra = populated.GetRawText().TrimEnd().TrimEnd('}') + ",\"invented_key\":{\"depth\":[1,2],\"label\":\"x\"}}";

            string? remainder = RingEventPayloadRemainder.Compute(withExtra);

            Assert.Equal("{\"invented_key\":{\"depth\":[1,2],\"label\":\"x\"}}", remainder);
        }

        [Fact]
        public void Fixture_WithAnInventedNestedKey_KeepsOnlyThatPath()
        {
            JsonElement first = FixtureEvents()[0];
            string withExtra = first.GetRawText().Replace("\"stark_reviewed\": false", "\"stark_reviewed\": false, \"invented_flag\": true", StringComparison.Ordinal);
            Assert.Contains("invented_flag", withExtra, StringComparison.Ordinal);

            string? remainder = RingEventPayloadRemainder.Compute(withExtra);

            Assert.Equal("{\"properties\":{\"invented_flag\":true}}", remainder);
        }
    }
}
