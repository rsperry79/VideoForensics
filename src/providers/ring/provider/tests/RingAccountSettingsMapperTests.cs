using System.Linq;
using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Maps the sanitized captures of profile, ringtones, light groups, location mode, mode settings and
    /// mode sharing into persistence entities. Expected values are read from the fixtures, not hand-copied.
    /// </summary>
    public class RingAccountSettingsMapperTests
    {
        private const string RedactedToken = "REDACTED-NOT-STORED";

        /// <summary>The 35 feature flags that have a typed column on RingAccountSessionFeatures.</summary>
        private static readonly string[] TypedFeatureKeys =
        [
            "remote_logging_format_storing", "remote_logging_level", "subscriptions_enabled", "stickupcam_setup_enabled",
            "vod_enabled", "nw_enabled", "nw_user_activated", "ringplus_enabled", "lpd_enabled", "reactive_snoozing_enabled",
            "proactive_snoozing_enabled", "owner_proactive_snoozing_enabled", "live_view_settings_enabled",
            "delete_all_settings_enabled", "power_cable_enabled", "device_health_alerts_enabled", "chime_pro_enabled",
            "multiple_calls_enabled", "ujet_enabled", "multiple_delete_enabled", "delete_all_enabled",
            "lpd_motion_announcement_enabled", "starred_events_enabled", "chime_dnd_enabled", "video_search_enabled",
            "floodlight_cam_enabled", "nw_larger_area_enabled", "ring_cam_battery_enabled", "elite_cam_enabled",
            "doorbell_v2_enabled", "spotlight_battery_dashboard_controls_enabled", "bypass_account_verification",
            "legacy_cvr_retention_enabled", "new_dashboard_enabled", "ring_cam_enabled"
        ];

        private static readonly Guid AccountId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";

        private static string ReadFixture(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
            return File.ReadAllText(path);
        }

        private static JsonElement FixtureRoot(string name)
        {
            using JsonDocument doc = JsonDocument.Parse(ReadFixture(name));
            return doc.RootElement.Clone();
        }

        private static Profile CapturedProfile() =>
            JsonSerializer.Deserialize<ProfileResponse>(ReadFixture("profile-sanitized.json"))!.Profile;

        [Fact]
        public void RingAccountSettingsMapper_CapturedProfile_StoresTypedFields()
        {
            JsonElement p = FixtureRoot("profile-sanitized.json").GetProperty("profile");

            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(CapturedProfile(), AccountId);

            Assert.Equal(AccountId, result.RingAccountId);
            Assert.Equal(p.GetProperty("id").GetInt32(), result.ProviderUserId);
            Assert.Equal(p.GetProperty("email").GetString(), result.Email);
            Assert.Equal(p.GetProperty("first_name").GetString(), result.FirstName);
            Assert.Equal(p.GetProperty("last_name").GetString(), result.LastName);
            Assert.Equal(p.GetProperty("phone_number").GetString(), result.PhoneNumber);
            Assert.Equal(p.GetProperty("hardware_id").GetString(), result.HardwareId);
            Assert.Equal(p.GetProperty("explorer_program_terms").ValueKind == JsonValueKind.Null ? null : p.GetProperty("explorer_program_terms").GetString(), result.ExplorerProgramTerms);
            Assert.Equal(p.GetProperty("user_flow").GetString(), result.UserFlow);
            Assert.Equal(p.GetProperty("app_brand").GetString(), result.AppBrand);
            Assert.Equal(p.GetProperty("country").GetString(), result.Country);
            Assert.Equal(p.GetProperty("status").GetString(), result.Status);
            Assert.Equal(p.GetProperty("created_at").GetString(), result.CreatedAt);
            Assert.Equal(p.GetProperty("tfa_enabled").GetBoolean(), result.TfaEnabled);
            Assert.Equal(p.GetProperty("account_type").GetString(), result.AccountType);
            Assert.Equal(p.GetProperty("cfes_enrolled").GetBoolean(), result.CfesEnrolled);
            Assert.Equal(p.GetProperty("tsv_state").GetString(), result.TsvState);
        }

        [Fact]
        public void RingAccountSettingsMapper_AuthenticationToken_IsNeverStored()
        {
            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(CapturedProfile(), AccountId);

            Assert.DoesNotContain(typeof(RingAccountProfile).GetProperties(), p => p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
            string graph = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(RedactedToken, graph);
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedFeatures_StoresTypedFlagsAsColumns()
        {
            JsonElement features = FixtureRoot("profile-sanitized.json").GetProperty("profile").GetProperty("features");

            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(CapturedProfile(), AccountId);

            Assert.NotNull(result.SessionFeatures);
            Assert.Equal(features.GetProperty("remote_logging_level").GetInt32(), result.SessionFeatures!.RemoteLoggingLevel);
            Assert.Equal(features.GetProperty("video_search_enabled").GetBoolean(), result.SessionFeatures.VideoSearchEnabled);
            Assert.Equal(features.GetProperty("ring_cam_enabled").GetBoolean(), result.SessionFeatures.RingCamEnabled);
            // Ring omitted this flag in the capture, so the column stays null rather than defaulting.
            Assert.False(features.TryGetProperty("new_dashboard_enabled", out _));
            Assert.Null(result.SessionFeatures.NewDashboardEnabled);
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedFeatures_StoresEveryNonTypedFlagAsRow()
        {
            JsonElement features = FixtureRoot("profile-sanitized.json").GetProperty("profile").GetProperty("features");
            string[] catchAllKeys = features.EnumerateObject().Select(p => p.Name).Where(k => !TypedFeatureKeys.Contains(k)).ToArray();

            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(CapturedProfile(), AccountId);

            List<RingAccountProfileFlag> featureFlags = result.Flags.Where(f => f.Scope == "features").ToList();
            Assert.Equal(catchAllKeys.Length, featureFlags.Count);
            Assert.Equal(catchAllKeys.OrderBy(k => k), featureFlags.Select(f => f.Name).OrderBy(k => k));
            Assert.DoesNotContain(featureFlags, f => TypedFeatureKeys.Contains(f.Name));

            RingAccountProfileFlag boolFlag = featureFlags.Single(f => f.Name == "2fa_dur_reg_enabled");
            Assert.Equal("bool", boolFlag.ValueKind);
            Assert.Equal(features.GetProperty("2fa_dur_reg_enabled").GetBoolean(), boolFlag.BoolValue);

            RingAccountProfileFlag intFlag = featureFlags.Single(f => f.Name == "audio_jitter_buffer_ms");
            Assert.Equal("int", intFlag.ValueKind);
            Assert.Equal(features.GetProperty("audio_jitter_buffer_ms").GetInt64(), intFlag.IntValue);

            RingAccountProfileFlag stringFlag = featureFlags.Single(f => f.Name == "qa_manual_testing");
            Assert.Equal("string", stringFlag.ValueKind);
            Assert.Equal(features.GetProperty("qa_manual_testing").GetString(), stringFlag.StringValue);

            RingAccountProfileFlag listFlag = featureFlags.Single(f => f.Name == "a_g_c_l_device_types");
            Assert.Equal("list", listFlag.ValueKind);
            Assert.Equal(features.GetProperty("a_g_c_l_device_types").GetArrayLength(), listFlag.Items.Count);
        }

        [Fact]
        public void RingAccountSettingsMapper_ListFeatureWithItems_StoresOneItemRowPerElement()
        {
            Profile profile = JsonSerializer.Deserialize<ProfileResponse>(
                @"{ ""profile"": { ""id"": 1, ""features"": { ""we_rc_an_2_device_types"": [ ""doorbell"", ""stickup_cam"" ] } } }")!.Profile;

            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(profile, AccountId);

            RingAccountProfileFlag flag = Assert.Single(result.Flags, f => f.Name == "we_rc_an_2_device_types");
            Assert.Equal("list", flag.ValueKind);
            Assert.Equal(new[] { "doorbell", "stickup_cam" }, flag.Items.OrderBy(i => i.Position).Select(i => i.Value));
        }

        [Fact]
        public void RingAccountSettingsMapper_UserPreferences_StoresSettingsAndPreferencesAsScopedFlags()
        {
            JsonElement settings = FixtureRoot("profile-sanitized.json").GetProperty("profile").GetProperty("user_preferences").GetProperty("settings");

            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(CapturedProfile(), AccountId);

            RingAccountProfileFlag flag = Assert.Single(result.Flags, f => f.Scope == "settings");
            Assert.Equal("targeted_advertising", flag.Name);
            Assert.Equal("bool", flag.ValueKind);
            Assert.Equal(settings.GetProperty("targeted_advertising").GetBoolean(), flag.BoolValue);
        }

        [Fact]
        public void RingAccountSettingsMapper_NestedObject_FlattensChildrenWithDottedNames()
        {
            Profile profile = JsonSerializer.Deserialize<ProfileResponse>(
                @"{ ""profile"": { ""id"": 1, ""user_preferences"": { ""settings"": {}, ""preferences"": { ""digest"": { ""weekly"": true, ""hour"": 9 } } } } }")!.Profile;

            RingAccountProfile result = RingAccountSettingsMapper.MapProfile(profile, AccountId);

            Assert.Contains(result.Flags, f => f.Scope == "preferences" && f.Name == "digest.weekly" && f.BoolValue == true);
            Assert.Contains(result.Flags, f => f.Scope == "preferences" && f.Name == "digest.hour" && f.IntValue == 9);
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedRingtones_OneRowPerAudioWithDeviceKindChildren()
        {
            JsonElement root = FixtureRoot("ringtones-sanitized.json");
            RingtonesResponse response = JsonSerializer.Deserialize<RingtonesResponse>(ReadFixture("ringtones-sanitized.json"))!;

            List<RingAccountRingtone> rows = RingAccountSettingsMapper.MapRingtones(response, AccountId).ToList();

            JsonElement first = root.GetProperty("audios")[0];
            Assert.Equal(root.GetProperty("audios").GetArrayLength(), rows.Count);
            RingAccountRingtone row = rows[0];
            Assert.Equal(AccountId, row.RingAccountId);
            Assert.Equal(first.GetProperty("id").GetString(), row.RingtoneId);
            Assert.Equal(first.GetProperty("description").GetString(), row.Description);
            Assert.Equal(first.GetProperty("kind").GetString(), row.Kind);
            Assert.Equal(first.GetProperty("category").GetString(), row.Category);
            Assert.Equal(first.GetProperty("available").GetBoolean(), row.Available);
            Assert.Equal(first.GetProperty("checksum").GetInt32(), row.Checksum);
            Assert.Equal(first.GetProperty("sample_rate_khz").GetInt32(), row.SampleRateKhz);
            Assert.Equal(first.GetProperty("url").GetString(), row.Url);
            Assert.Equal(first.GetProperty("url_amz").GetString(), row.UrlAmz);
            Assert.Equal(first.GetProperty("user_id").GetString(), row.UserId);
            Assert.Equal(
                first.GetProperty("supported_device_kinds").EnumerateArray().Select(e => e.GetString()),
                row.SupportedDeviceKinds.OrderBy(k => k.Position).Select(k => k.DeviceKind));
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedRingtoneDefaults_StoresAllSevenDefaults()
        {
            JsonElement root = FixtureRoot("ringtones-sanitized.json");
            RingtonesResponse response = JsonSerializer.Deserialize<RingtonesResponse>(ReadFixture("ringtones-sanitized.json"))!;

            RingAccountRingtoneDefaults defaults = RingAccountSettingsMapper.MapRingtoneDefaults(response, AccountId);

            Assert.Equal(AccountId, defaults.RingAccountId);
            Assert.Equal(root.GetProperty("default_ding_id").GetString(), defaults.DefaultDingId);
            Assert.Equal(root.GetProperty("default_ding_user_id").GetString(), defaults.DefaultDingUserId);
            Assert.Equal(root.GetProperty("default_motion_id").GetString(), defaults.DefaultMotionId);
            Assert.Equal(root.GetProperty("default_motion_user_id").GetString(), defaults.DefaultMotionUserId);
            Assert.Equal(root.GetProperty("default_alarm_user_id").GetString(), defaults.DefaultAlarmUserId);
            Assert.Equal(root.GetProperty("default_chirp_user_id").GetString(), defaults.DefaultChirpUserId);
            Assert.Equal(root.GetProperty("default_appstore_user_id").GetString(), defaults.DefaultAppstoreUserId);
        }

        [Fact]
        public void RingAccountSettingsMapper_NullDeviceGroups_StoresIsOwnerWithNoGroups()
        {
            GroupsResponse response = JsonSerializer.Deserialize<GroupsResponse>(ReadFixture("light-groups-sanitized.json"))!;

            LightGroupSet result = RingAccountSettingsMapper.MapLightGroups(response, LocationId);

            Assert.Equal(LocationId, result.ProviderLocationId);
            Assert.True(result.IsOwner);
            Assert.Empty(result.Groups);
        }

        [Fact]
        public void RingAccountSettingsMapper_GroupsWithDevices_OneRowPerGroupAndPerDevice()
        {
            GroupsResponse response = JsonSerializer.Deserialize<GroupsResponse>(
                @"{ ""device_groups"": [ { ""device_group_id"": ""grp-1"", ""name"": ""Backyard Lights"" }, { ""device_group_id"": ""grp-2"", ""name"": null } ], ""is_owner"": false }")!;
            var devices = new Dictionary<string, IReadOnlyList<string>>
            {
                ["grp-1"] = new[] { "dev-1", "dev-2" }
            };

            LightGroupSet result = RingAccountSettingsMapper.MapLightGroups(response, LocationId, devices);

            Assert.False(result.IsOwner);
            Assert.Equal(2, result.Groups.Count);
            LightGroup first = result.Groups.Single(g => g.ProviderDeviceGroupId == "grp-1");
            Assert.Equal("Backyard Lights", first.Name);
            Assert.Equal(new[] { "dev-1", "dev-2" }, first.Devices.Select(d => d.ProviderDeviceId).OrderBy(d => d));
            LightGroup second = result.Groups.Single(g => g.ProviderDeviceGroupId == "grp-2");
            Assert.Null(second.Name);
            Assert.Empty(second.Devices);
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedLocationMode_StoresTypedColumns()
        {
            JsonElement root = FixtureRoot("location-mode-sanitized.json");
            LocationMode mode = JsonSerializer.Deserialize<LocationMode>(ReadFixture("location-mode-sanitized.json"))!;

            RingLocationMode result = RingAccountSettingsMapper.MapLocationMode(mode, LocationId);

            Assert.Equal(LocationId, result.ProviderLocationId);
            Assert.Equal(root.GetProperty("mode").GetString(), result.Mode);
            Assert.Equal(root.GetProperty("lastUpdateTimeMS").GetInt64(), result.LastUpdateTimeMs);
            Assert.Equal(root.GetProperty("securityStatus").GetProperty("md").GetString(), result.SecurityStatusMode);
            Assert.Equal(root.GetProperty("securityStatus").GetProperty("lu").GetInt64(), result.SecurityStatusLastUpdateMs);
            Assert.Equal(root.GetProperty("readOnly").GetBoolean(), result.ReadOnly);
            Assert.Equal(root.GetProperty("modePanelType").GetString(), result.ModePanelType);
            Assert.Equal(root.GetProperty("notYetParticipatingInMode").GetArrayLength(), result.NotYetParticipating.Count);
        }

        [Fact]
        public void RingAccountSettingsMapper_NotYetParticipatingEntries_StoredAsRawJsonRows()
        {
            LocationMode mode = JsonSerializer.Deserialize<LocationMode>(
                @"{ ""mode"": ""away"", ""notYetParticipatingInMode"": [ { ""deviceId"": ""dev-9"" } ] }")!;

            RingLocationMode result = RingAccountSettingsMapper.MapLocationMode(mode, LocationId);

            RingLocationModeNotYetParticipating row = Assert.Single(result.NotYetParticipating);
            Assert.Contains("dev-9", row.ValueJson);
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedModeSettings_OneEntryRowPerModeDevice()
        {
            JsonElement root = FixtureRoot("location-mode-settings-sanitized.json");
            LocationModeSettingsResponse response = JsonSerializer.Deserialize<LocationModeSettingsResponse>(ReadFixture("location-mode-settings-sanitized.json"))!;

            RingLocationModeSettings result = RingAccountSettingsMapper.MapLocationModeSettings(response, LocationId);

            Assert.Equal(root.GetProperty("lastUpdateTimeMS").GetInt64(), result.LastUpdateTimeMs);
            Assert.Equal(root.GetProperty("options").GetProperty("defaultCameraOnlyMode").GetString(), result.DefaultCameraOnlyMode);
            Assert.Equal(
                root.GetProperty("options").GetProperty("supportedActionGroups").EnumerateArray().Select(e => e.GetString()),
                result.SupportedActionGroups.OrderBy(g => g.Position).Select(g => g.GroupName));

            int expectedEntries = 0;
            int expectedEnabled = 0;
            int expectedSupported = 0;
            foreach (string mode in new[] { "disarmed", "home", "away" })
            {
                foreach (JsonElement entry in root.GetProperty(mode).EnumerateArray())
                {
                    expectedEntries++;
                    expectedEnabled += entry.GetProperty("actions").GetArrayLength();
                    foreach (JsonProperty group in entry.GetProperty("supportedActions").EnumerateObject())
                    {
                        expectedSupported += group.Value.GetArrayLength();
                    }
                }
            }

            Assert.Equal(expectedEntries, result.Entries.Count);
            Assert.Equal(new[] { "disarmed", "home", "away" }, result.Entries.Select(e => e.Mode).Distinct());
            Assert.Equal(expectedEnabled, result.Entries.SelectMany(e => e.Actions).Count(a => a.Kind == "enabled"));
            Assert.Equal(expectedSupported, result.Entries.SelectMany(e => e.Actions).Count(a => a.Kind == "supported"));
            Assert.All(result.Entries.SelectMany(e => e.Actions).Where(a => a.Kind == "enabled"), a => Assert.Null(a.ActionGroup));
            Assert.All(result.Entries.SelectMany(e => e.Actions).Where(a => a.Kind == "supported"), a => Assert.NotNull(a.ActionGroup));
        }

        [Fact]
        public void RingAccountSettingsMapper_CapturedSharing_StoresSharedUsersEnabledAndTimestamp()
        {
            JsonElement root = FixtureRoot("location-mode-sharing-sanitized.json");
            LocationModeSharingResponse response = JsonSerializer.Deserialize<LocationModeSharingResponse>(ReadFixture("location-mode-sharing-sanitized.json"))!;

            RingLocationModeSharing result = RingAccountSettingsMapper.MapLocationModeSharing(response, LocationId);

            Assert.Equal(LocationId, result.ProviderLocationId);
            Assert.Equal(root.GetProperty("sharedUsersEnabled").GetBoolean(), result.SharedUsersEnabled);
            Assert.Equal(root.GetProperty("lastUpdateTimeMS").GetInt64(), result.LastUpdateTimeMs);
        }
    }
}
