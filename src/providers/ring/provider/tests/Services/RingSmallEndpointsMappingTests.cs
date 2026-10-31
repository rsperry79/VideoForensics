using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Mapping of the small endpoints (locations, location mode, health 2nd battery keys, event CV and location history
    /// fields, shared users, linked chime doorbots, snapshot timestamps) onto persistence entities.
    /// </summary>
    public class RingSmallEndpointsMappingTests
    {
        private static readonly Guid AccountId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        private static readonly Guid LocationGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly Guid ChimeGuid = Guid.Parse("99999999-8888-7777-6666-555555555555");

        [Fact]
        public void RingDeviceHealthParser_SecondBatteryAndHatchKeys_AreStored()
        {
            var health = new Entities.DeviceHealth
            {
                AdditionalFields = new Dictionary<string, JsonElement>
                {
                    ["battery_hatch_open"] = JsonSerializer.SerializeToElement("closed"),
                    ["battery_percentage_2"] = JsonSerializer.SerializeToElement("80"),
                    ["battery_voltage_2"] = JsonSerializer.SerializeToElement("12.1"),
                    ["battery_percentage_category_2"] = JsonSerializer.SerializeToElement("high"),
                    ["battery_voltage_category_2"] = JsonSerializer.SerializeToElement("ok"),
                },
            };

            RingReportedHealth? parsed = RingDeviceHealthParser.Parse(health);

            Assert.NotNull(parsed);
            Assert.Equal("closed", parsed!.Extras.BatteryHatchOpen);
            Assert.Equal("80", parsed.Extras.BatteryPercentage2);
            Assert.Equal("12.1", parsed.Extras.BatteryVoltage2);
            Assert.Equal("high", parsed.Extras.SecondBatteryPercentageCategory);
            Assert.Equal("ok", parsed.Extras.SecondBatteryVoltageCategory);
            Assert.DoesNotContain(parsed.Unstored, n => n.Contains("battery_hatch_open", StringComparison.Ordinal));
        }

        [Fact]
        public void RingDeviceHealthParser_HatchOpenWrongJsonType_StoresNothingAndNotesKeyAndType()
        {
            var health = new Entities.DeviceHealth
            {
                AdditionalFields = new Dictionary<string, JsonElement>
                {
                    ["battery_hatch_open"] = JsonSerializer.SerializeToElement(5),
                },
            };

            RingReportedHealth? parsed = RingDeviceHealthParser.Parse(health);

            Assert.NotNull(parsed);
            Assert.Null(parsed!.Extras.BatteryHatchOpen);
            Assert.Contains(parsed.Unstored, n => n.Contains("battery_hatch_open", StringComparison.Ordinal) && n.Contains("Number", StringComparison.Ordinal));
            Assert.DoesNotContain(parsed.Unstored, n => n.Contains('5'));
        }

        [Fact]
        public void MapLocationMode_InitiatingEntityId_IsStored()
        {
            LocationMode mode = JsonSerializer.Deserialize<LocationMode>("""{"mode":"home","securityStatus":{"lu":1,"md":"home","initiatingEntityId":"entity-1"}}""")!;

            RingLocationMode mapped = RingAccountSettingsMapper.MapLocationMode(mode, "loc");

            Assert.Equal("entity-1", mapped.SecurityStatusInitiatingEntityId);
        }

        [Fact]
        public void ToContractLocation_EveryLocationKey_IsCarriedInMetadata()
        {
            const string json = """
                {"user_locations":[{"location_id":"11111111-2222-3333-4444-555555555555","name":"Home",
                "address":{"address1":"1 Main","address2":"Apt 2","city":"Arlington","state":"WA","zip_code":"98223","timezone":"America/Los_Angeles","country":"US","cross_street":"Oak"},
                "is_owner":true,"owner_id":42,"created_at":"2021-08-29T22:54:22Z","updated_at":"2026-01-02T03:04:05Z",
                "geo_coordinates":{"latitude":47.5,"longitude":-122.25},"geo_service_verified":"verified","is_jobsite":false,
                "location_resource_id":"res-1","location_type":"residential","user_verified":true,
                "entitlements":{"business_command_center":false,"guard_patrol":true,"private_guard_response":false,"vm_video_verification":false,"vm_virtual_security_guard":true}}]}
                """;
            UserLocationsResponse response = JsonSerializer.Deserialize<UserLocationsResponse>(json)!;

            var location = RingDeviceDiscoveryService.ToContractLocation(response.UserLocations[0]);
            Dictionary<string, string> metadata = location.Metadata!;

            Assert.Equal("Apt 2", metadata["address2"]);
            Assert.Equal("Oak", metadata["cross_street"]);
            Assert.Equal("US", metadata["country"]);
            Assert.Equal("47.5", metadata["latitude"]);
            Assert.Equal("-122.25", metadata["longitude"]);
            Assert.Equal("42", metadata["owner_id"]);
            Assert.Equal("2021-08-29T22:54:22Z", metadata["created_at"]);
            Assert.Equal("2026-01-02T03:04:05Z", metadata["updated_at"]);
            Assert.Equal("true", metadata["guard_patrol"]);
            Assert.Equal("false", metadata["business_command_center"]);
            Assert.Equal("true", metadata["vm_virtual_security_guard"]);
            Assert.Equal("true", metadata["user_verified"]);
            Assert.Equal("false", metadata["is_jobsite"]);
            Assert.Equal("res-1", metadata["location_resource_id"]);
            Assert.Equal("residential", metadata["location_type"]);
            Assert.Equal("verified", metadata["geo_service_verified"]);
        }

        [Fact]
        public void RingLocationDetailsMapper_RingOnlyKeys_MapToTypedValues()
        {
            var metadata = new Dictionary<string, string>
            {
                ["created_at"] = "2021-08-29T22:54:22Z",
                ["updated_at"] = "2026-01-02T03:04:05Z",
                ["owner_id"] = "42",
                ["guard_patrol"] = "true",
                ["business_command_center"] = "false",
                ["private_guard_response"] = "false",
                ["vm_video_verification"] = "false",
                ["vm_virtual_security_guard"] = "true",
                ["user_verified"] = "true",
                ["is_jobsite"] = "false",
                ["location_type"] = "residential",
                ["location_resource_id"] = "res-1",
                ["geo_service_verified"] = "verified",
            };

            RingLocationDetails details = RingLocationDetailsMapper.Map(LocationGuid, metadata);

            Assert.Equal(LocationGuid, details.LocationId);
            Assert.Equal(new DateTime(2021, 8, 29, 22, 54, 22, DateTimeKind.Utc), details.CreatedAtUtc);
            Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), details.UpdatedAtUtc);
            Assert.Equal(42L, details.OwnerId);
            Assert.True(details.GuardPatrol);
            Assert.False(details.BusinessCommandCenter);
            Assert.True(details.VmVirtualSecurityGuard);
            Assert.True(details.UserVerified);
            Assert.False(details.IsJobsite);
            Assert.Equal("residential", details.LocationType);
            Assert.Equal("res-1", details.LocationResourceId);
            Assert.Equal("verified", details.GeoServiceVerified);
        }

        [Fact]
        public void RingLocationDetailsMapper_GenericKeys_FillLocationMetadataColumns()
        {
            var metadata = new Dictionary<string, string>
            {
                ["address2"] = "Apt 2",
                ["cross_street"] = "Oak",
                ["country"] = "US",
                ["latitude"] = "47.5",
                ["longitude"] = "-122.25",
            };
            var target = new LocationMetadata();

            RingLocationDetailsMapper.ApplyGenericFields(target, metadata);

            Assert.Equal("Apt 2", target.StreetAddress2);
            Assert.Equal("Oak", target.CrossStreet);
            Assert.Equal("US", target.Country);
            Assert.Equal(47.5, target.Latitude);
            Assert.Equal(-122.25, target.Longitude);
        }

        [Fact]
        public void MapVideoSearch_KeyMomentsString_IsStoredAndArrayIsNoted()
        {
            VideoSearchItem stored = JsonSerializer.Deserialize<VideoSearchItem>("""{"id":1,"cv_properties":{"key_moments":"moment-a"}}""")!;
            VideoSearchItem wrong = JsonSerializer.Deserialize<VideoSearchItem>("""{"id":1,"cv_properties":{"key_moments":[1,2]}}""")!;

            MappedRingEvent<RingVideoSearchEvent> a = RingEventHistoryMapper.MapVideoSearch(stored, AccountId);
            MappedRingEvent<RingVideoSearchEvent> b = RingEventHistoryMapper.MapVideoSearch(wrong, AccountId);

            Assert.Equal("moment-a", a.Details.CvProperties!.KeyMoments);
            Assert.Null(b.Details.CvProperties!.KeyMoments);
            Assert.Contains(b.Unstored, n => n.Contains("key_moments", StringComparison.Ordinal) && n.Contains("Array", StringComparison.Ordinal));
        }

        [Fact]
        public void MapLocationHistoryItem_NewCaptureFields_AreMapped()
        {
            LocationHistoryItem item = JsonSerializer.Deserialize<LocationHistoryItem>(
                """{"event_id":"e1","cek_transfer_recipient_response":"cek-item","visualizations":{"map_visualization":"map-v","radar_visualization":"radar-v","potential_gap_visualization":"gap-v"},"cv":{"key_moments":"moments","detection_details":[{"timestamp":5,"type":"human","objects_per_lens":"lens-a"}]}}""")!;

            MappedRingEvent<RingLocationHistoryEvent> mapped = RingEventHistoryMapper.MapLocationHistoryItem(item, "loc");

            Assert.Equal("cek-item", mapped.Event.CekTransferRecipientResponse);
            Assert.Equal("map-v", mapped.Event.MapVisualization);
            Assert.Equal("radar-v", mapped.Event.RadarVisualization);
            Assert.Equal("gap-v", mapped.Event.PotentialGapVisualization);
            Assert.Equal("moments", mapped.Details.CvProperties!.KeyMoments);
            Assert.Equal("lens-a", mapped.Details.CvProperties.DetectionDetails[0].ObjectsPerLens);
        }

        [Fact]
        public void MapLocationHistoryPage_RootCekTransferRecipientResponse_IsStored()
        {
            var notes = new List<string>();

            RingLocationHistoryPage page = RingEventHistoryMapper.MapLocationHistoryPage("loc", "pk", JsonSerializer.SerializeToElement("cek-root"), notes);

            Assert.Equal("cek-root", page.CekTransferRecipientResponse);
            Assert.Empty(notes);
        }

        [Fact]
        public void MapLocationSharedUsers_DeviceGrantsAndPermissions_BecomeChildRows()
        {
            List<SharedUser> users = JsonSerializer.Deserialize<List<SharedUser>>(
                """[{"id":7,"verified":true,"first_name":"Ann","last_name":"Lee","email":"ann-lee","object_type":"user","devices":[{"id":100,"role":"owner","device_type":"doorbot","permissions":["view","edit"]}]}]""")!;

            IReadOnlyList<RingLocationSharedUser> mapped = RingSmallEndpointMapper.MapLocationSharedUsers(users, LocationGuid);

            RingLocationSharedUser user = Assert.Single(mapped);
            Assert.Equal(LocationGuid, user.LocationId);
            Assert.Equal(7L, user.ProviderUserId);
            Assert.Equal("ann-lee", user.Email);
            RingLocationSharedUserDevice device = Assert.Single(user.Devices);
            Assert.Equal(100L, device.ProviderDeviceId);
            Assert.Equal("owner", device.Role);
            Assert.Equal(["view", "edit"], device.Permissions.Select(p => p.Permission));
            Assert.Equal([0, 1], device.Permissions.Select(p => p.Position));
        }

        [Fact]
        public void MapChimeLinkedDoorbots_LinkedRows_AreMapped()
        {
            using JsonDocument doc = JsonDocument.Parse("""{"linked_doorbots":[{"id":12,"device_id":"12","kind":"doorbot","call_alerts":true,"motion_alerts":false}]}""");

            IReadOnlyList<RingChimeLinkedDoorbot> rows = RingSmallEndpointMapper.MapChimeLinkedDoorbots(doc.RootElement.Clone(), ChimeGuid);

            RingChimeLinkedDoorbot row = Assert.Single(rows);
            Assert.Equal(ChimeGuid, row.ChimeDeviceId);
            Assert.Equal(12L, row.LinkedDoorbotId);
            Assert.Equal("12", row.DeviceId);
            Assert.Equal("doorbot", row.Kind);
            Assert.True(row.CallAlerts);
            Assert.False(row.MotionAlerts);
        }

        [Fact]
        public void MapSnapshotTimestamps_EachDoorbot_BecomesARow()
        {
            DoorbotTimestamps timestamps = JsonSerializer.Deserialize<DoorbotTimestamps>("""{"timestamps":[{"doorbot_id":12,"timestamp":1700000000000}]}""")!;

            IReadOnlyList<RingSnapshotTimestamp> rows = RingSmallEndpointMapper.MapSnapshotTimestamps(timestamps, AccountId);

            RingSnapshotTimestamp row = Assert.Single(rows);
            Assert.Equal(AccountId, row.RingAccountId);
            Assert.Equal(12L, row.DoorbotId);
            Assert.Equal(1700000000000L, row.TimestampEpoch);
        }
    }
}
