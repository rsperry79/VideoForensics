using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// The raw Ring event payload is no longer stored in Events/MediaItems.MetadataJson: only keys without a structured
    /// home survive. The integrity hash is still computed over the full raw payload.
    /// </summary>
    public class RingEventPayloadRemainderTests
    {
        // A doorbot history event as the Ring API sends it (values are made up). Serialized again through the typed model,
        // this is what RingMediaDownloadService used to store as MetadataJson.
        private const string RealShapedEvent = """
            {
              "id": 7000000000000000001,
              "created_at": "2026-01-01T08:00:00.000Z",
              "answered": false,
              "events": [],
              "kind": "motion",
              "favorite": false,
              "snapshot_url": "",
              "recording": { "status": "ready" },
              "duration": 15.0,
              "cv_properties": {
                "person_detected": true,
                "stream_broken": false,
                "detection_type": "human",
                "detection_types": [ { "detection_type": "human", "verified_timestamps": [ 1767254400000 ] } ],
                "security_alerts": null, "profiles": null, "full_description": null, "short_description": null,
                "similarity": null, "anomaly": null, "detection_details": null, "tags": null, "key_moments": null
              },
              "properties": { "is_alexa": false, "is_sidewalk": false, "is_autoreply": false, "stark_reviewed": false },
              "doorbot": { "id": 1, "description": "Front Door", "type": "doorbell_graham_cracker" },
              "device_placement": null, "geolocation": null, "last_location": null, "siren": null,
              "is_e2ee": false, "had_subscription": true, "owner_id": "1", "riid": "abc"
            }
            """;

        private const string PinnedHash = "08C4D91307E309B5C0DE8D8B15F5AACF78BFC9B8D9FA0071C63B8C16A34D9A06";

        private static string Serialized(string apiJson)
        {
            DoorbotHistoryEvent evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>(apiJson)!;
            return JsonSerializer.Serialize(evt);
        }

        [Fact]
        public void Compute_RealShapedEvent_ReturnsNull()
        {
            Assert.Null(RingEventPayloadRemainder.Compute(Serialized(RealShapedEvent)));
        }

        [Fact]
        public void Compute_InventedRootKeyOnTypedEvent_SurvivesTheTypedRoundTrip()
        {
            // DoorbotHistoryEvent keeps unknown Ring keys (JsonExtensionData), so an invented key reaches the remainder as an unknown leaf.
            string json = RealShapedEvent.Replace("\"riid\": \"abc\"", "\"riid\": \"abc\", \"invented_ring_key\": \"fake-value\"", StringComparison.Ordinal);

            Assert.Equal("""{"invented_ring_key":"fake-value"}""", RingEventPayloadRemainder.Compute(Serialized(json)));
        }

        [Fact]
        public void Split_InventedRootKey_RemainderHoldsItAndTheHashCoversIt()
        {
            string json = RealShapedEvent.Replace("\"riid\": \"abc\"", "\"riid\": \"abc\", \"invented_ring_key\": \"fake-value\"", StringComparison.Ordinal);
            DoorbotHistoryEvent evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>(json)!;

            (string? remainder, string hash) = RingEventPayloadRemainder.Split(evt);

            Assert.Equal("""{"invented_ring_key":"fake-value"}""", remainder);
            Assert.NotEqual(PinnedHash, hash);
            Assert.Equal(ExpectedHashFor(evt), hash);
        }

        [Fact]
        public void Compute_NullAndEmptyInput_ReturnsNull()
        {
            Assert.Null(RingEventPayloadRemainder.Compute(null));
            Assert.Null(RingEventPayloadRemainder.Compute(""));
            Assert.Null(RingEventPayloadRemainder.Compute("   "));
            Assert.Null(RingEventPayloadRemainder.Compute("{}"));
            Assert.Null(RingEventPayloadRemainder.Compute("null"));
        }

        [Fact]
        public void Compute_InventedTopLevelAndNestedKeys_SurviveAndNothingElse()
        {
            string json = Serialized(RealShapedEvent)
                .Replace("\"kind\":\"motion\"", "\"kind\":\"motion\",\"new_top_level\":\"hello\"", StringComparison.Ordinal)
                .Replace("\"stark_reviewed\":false", "\"stark_reviewed\":false,\"new_flag\":true", StringComparison.Ordinal);

            string? remainder = RingEventPayloadRemainder.Compute(json);

            Assert.NotNull(remainder);
            using JsonDocument doc = JsonDocument.Parse(remainder);
            Assert.Equal("hello", doc.RootElement.GetProperty("new_top_level").GetString());
            Assert.True(doc.RootElement.GetProperty("properties").GetProperty("new_flag").GetBoolean());
            Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
            Assert.Single(doc.RootElement.GetProperty("properties").EnumerateObject());
        }

        [Fact]
        public void Compute_NonStringKeyMoments_StaysBecauseOnlyTextIsStored()
        {
            string? remainder = RingEventPayloadRemainder.Compute("""{"cv_properties":{"key_moments":{"a":1},"person_detected":true}}""");

            Assert.Equal("""{"cv_properties":{"key_moments":{"a":1}}}""", remainder);
        }

        [Fact]
        public void Compute_StringKeyMoments_IsDropped()
        {
            Assert.Null(RingEventPayloadRemainder.Compute("""{"cv_properties":{"key_moments":"text"}}"""));
        }

        [Fact]
        public void Compute_NonEmptyEventsArray_StaysBecauseItsElementShapeIsNotStored()
        {
            string? remainder = RingEventPayloadRemainder.Compute("""{"events":[{"x":1}],"kind":"motion"}""");

            Assert.Equal("""{"events":[{"x":1}]}""", remainder);
        }

        [Fact]
        public void Compute_ModelNullArtifactsOnDoorbot_AreDropped()
        {
            Assert.Null(RingEventPayloadRemainder.Compute("""{"doorbot":{"id":1,"device_id":null,"alerts":null,"features":null}}"""));
        }

        [Fact]
        public void Compute_DerivedCreatedAtDateTime_IsNotUnknown()
        {
            Assert.Null(RingEventPayloadRemainder.Compute("""{"created_at":"2026-01-01T08:00:00.000Z","CreatedAtDateTime":"2026-01-01T00:00:00-08:00"}"""));
        }

        [Fact]
        public void Compute_InvalidJson_IsKeptUnchanged()
        {
            Assert.Equal("{not json", RingEventPayloadRemainder.Compute("{not json"));
        }

        [Fact]
        public void Compute_AppMarkers_SurviveWhole()
        {
            // Markers carry no Ring keys, so every key is "unknown" and survives.
            const string marker = """{"type":"snapshot","source":"capture"}""";

            Assert.Equal(marker, RingEventPayloadRemainder.Compute(marker));
        }

        [Fact]
        public void IsRawEventPayload_RawEventTrue_MarkersFalse()
        {
            using JsonDocument raw = JsonDocument.Parse(Serialized(RealShapedEvent));
            using JsonDocument snap = JsonDocument.Parse("""{"type":"snapshot","source":"capture"}""");
            using JsonDocument snapWithEventId = JsonDocument.Parse("""{"deviceId":"1","eventId":"snapshot-1","timestamp":"2026-01-01T00:00:00Z","type":"snapshot"}""");
            using JsonDocument rollup = JsonDocument.Parse("""{"type":"snapshot-rollup"}""");
            using JsonDocument array = JsonDocument.Parse("[1]");

            Assert.True(RingEventPayloadRemainder.IsRawEventPayload(raw.RootElement));
            Assert.False(RingEventPayloadRemainder.IsRawEventPayload(snap.RootElement));
            Assert.False(RingEventPayloadRemainder.IsRawEventPayload(snapWithEventId.RootElement));
            Assert.False(RingEventPayloadRemainder.IsRawEventPayload(rollup.RootElement));
            Assert.False(RingEventPayloadRemainder.IsRawEventPayload(array.RootElement));
        }

        [Fact]
        public void Split_RemainderIsNullButHashStaysOverTheFullPayload()
        {
            DoorbotHistoryEvent evt = JsonSerializer.Deserialize<DoorbotHistoryEvent>(RealShapedEvent)!;

            (string? remainder, string hash) = RingEventPayloadRemainder.Split(evt);

            Assert.Null(remainder);
            // Pinned: SHA-256 (upper-case hex) of the UTF-8 bytes of JsonSerializer.Serialize(evt), exactly as
            // RingMediaDownloadService.SerializeMetadata computed it before the raw payload stopped being stored.
            Assert.Equal(ExpectedHashFor(evt), hash);
            Assert.Equal(PinnedHash, hash);
        }

        [Fact]
        public void Split_NullResponse_GivesNullRemainderAndEmptyHash()
        {
            (string? remainder, string hash) = RingEventPayloadRemainder.Split(null);

            Assert.Null(remainder);
            Assert.Equal(string.Empty, hash);
        }

        private static string ExpectedHashFor(DoorbotHistoryEvent evt)
        {
            string json = JsonSerializer.Serialize(evt);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
        }
    }
}
