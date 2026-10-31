using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the full ring_devices <c>features</c> object (sanitized fixture) onto DeviceFeatures scalars and DeviceFeatureState rows.</summary>
    public class RingDeviceFeatureStateMapperTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static RingDeviceDetails Load(string providerId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "device-features-sanitized.json");
            Devices devices = JsonSerializer.Deserialize<Devices>(File.ReadAllText(path))!;
            return RingDeviceDetailsMapper.Find(devices, providerId)!;
        }

        private static IReadOnlyList<DeviceFeatureState> States(RingDeviceDetails details)
        {
            return RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details, DeviceId)!.FeatureStates!;
        }

        [Fact]
        public void ToFeatures_Doorbot_MapsScalarLeavesUnderFeatures()
        {
            DeviceFeatures f = RingDeviceDetailsMapper.ToFeatures(Load("1001"), DeviceId)!;

            Assert.True(f.CfesEligible);
            Assert.True(f.ShowVodSettings);
            Assert.True(f.RichNotificationsEligible);
            Assert.False(f.ShowOfflineMotionEvents);
            Assert.True(f.SheilaCameraEligible);
            Assert.True(f.SheilaCameraProcessingEligible);
            Assert.False(f.DynamicNetworkSwitchingEligible);
            Assert.True(f.VehicleDetectionEligible);
            Assert.True(f.Show24x7Lite);
            Assert.False(f.MotionZoneRecommendation);
            Assert.Null(f.AnimalDetectionEligible);
            Assert.Null(f.Recording24x7Eligible);
            Assert.Null(f.PackageWarning);
            Assert.Null(f.Tracklight);
            Assert.Null(f.PropertyView);
        }

        [Fact]
        public void ToFeatures_StickupCam_MapsMotionMessageEnabledAndScalars()
        {
            DeviceFeatures f = RingDeviceDetailsMapper.ToFeatures(Load("2001"), DeviceId)!;

            Assert.True(f.MotionMessageEnabled);
            Assert.True(f.CfesEligible);
            Assert.True(f.ShowVodSettings);
            Assert.Null(f.VehicleDetectionEligible);
        }

        [Fact]
        public void ToFeatures_Chime_MapsCfesEligibleFalse()
        {
            DeviceFeatures f = RingDeviceDetailsMapper.ToFeatures(Load("3001"), DeviceId)!;

            Assert.False(f.CfesEligible);
            Assert.True(f.RingtonesEnabled);
        }

        [Fact]
        public void ToSettingsChildren_Doorbot_CreatesOneStatePerFeatureObject()
        {
            IReadOnlyList<DeviceFeatureState> states = States(Load("1001"));

            Assert.Equal(20, states.Count);
            Assert.Equal(states.Count, states.Select(s => s.FeatureKey).Distinct().Count());
            Assert.DoesNotContain(states, s => s.FeatureKey == "cfes_eligible");
            Assert.All(states, s => Assert.Equal(DeviceId, s.DeviceId));
            Assert.All(states, s => Assert.NotEqual(Guid.Empty, s.Id));
        }

        [Fact]
        public void ToSettingsChildren_SmartVideoSearch_MapsEligibilityEnablementAndReasons()
        {
            DeviceFeatureState s = States(Load("1001")).Single(x => x.FeatureKey == "smart_video_search");

            Assert.False(s.Eligible);
            Assert.False(s.Allowed);
            Assert.False(s.Enabled);
            Assert.Equal(["unsupported_language"], Reasons(s, DeviceFeatureReason.Ineligibility));
            Assert.Equal(["not_subscribed", "ineligible"], Reasons(s, DeviceFeatureReason.Disallow));
            Assert.Equal([0, 1], s.Reasons.Where(r => r.Kind == DeviceFeatureReason.Disallow).Select(r => r.Position));
        }

        [Fact]
        public void ToSettingsChildren_NullObjectsAndEmptyArrays_CreateNoStateOrReasons()
        {
            IReadOnlyList<DeviceFeatureState> states = States(Load("2001"));

            Assert.DoesNotContain(states, s => s.FeatureKey == "property_view");
            Assert.DoesNotContain(states, s => s.FeatureKey == "transformer_score");
            Assert.DoesNotContain(states.Single(s => s.FeatureKey == "smart_video_description").Reasons, r => r.Kind == DeviceFeatureReason.Ineligibility);
            Assert.Equal(18, states.Count);
        }

        [Fact]
        public void ToSettingsChildren_ScalarShapedFeatures_MapToTypedColumns()
        {
            IReadOnlyList<DeviceFeatureState> states = States(Load("1001"));

            Assert.Equal(6.0, states.Single(s => s.FeatureKey == "video_rendering").MaxDigitalZoomLevel);
            Assert.Equal(24.0, states.Single(s => s.FeatureKey == "retinal_tuning").RtMaxDigitalZoomLevel);
            Assert.False(states.Single(s => s.FeatureKey == "original_video_quality_download_offer").IsEnabled);
            Assert.False(states.Single(s => s.FeatureKey == "chime_settings").IsEligible);
            Assert.False(states.Single(s => s.FeatureKey == "property_view").Enabled);
            DeviceFeatureState daily = states.Single(s => s.FeatureKey == "ai_labs_daily_clip");
            Assert.False(daily.AllowsNoLocation);
            Assert.False(daily.Enabled);
            Assert.Null(daily.Allowed);
            Assert.Equal(3, Reasons(daily, DeviceFeatureReason.Ineligibility).Count);
        }

        [Fact]
        public void ToSettingsChildren_TransformerScore_MapsScoreAndAffectedFeatures()
        {
            DeviceFeatureState s = States(Load("1001")).Single(x => x.FeatureKey == "transformer_score");
            Assert.Equal("best", s.Score);
            Assert.Empty(s.Reasons);

            var devices = new Devices
            {
                Doorbots = [new Doorbot
                {
                    Id = 1,
                    Features = Features("{\"transformer_score\":{\"score\":\"low\",\"affected_features\":[\"a\",\"b\"]},\"unusual_alert\":{\"learning\":\"in_progress\"}}")
                }]
            };
            IReadOnlyList<DeviceFeatureState> states = States(RingDeviceDetailsMapper.Find(devices, "1")!);

            DeviceFeatureState scored = states.Single(x => x.FeatureKey == "transformer_score");
            Assert.Equal("low", scored.Score);
            Assert.Equal(["a", "b"], Reasons(scored, DeviceFeatureReason.AffectedFeature));
            Assert.Equal("in_progress", states.Single(x => x.FeatureKey == "unusual_alert").Learning);
        }

        [Fact]
        public void ToSettingsChildren_VideoRecording_MapsRecordingAndContinuousAvailability()
        {
            DeviceFeatureState s = States(Load("1001")).Single(x => x.FeatureKey == "video_recording");

            Assert.Equal("motion_based", s.RecordingMode);
            Assert.True(s.RecordingEnabled);
            Assert.Equal("active", s.RecordingState);
            Assert.False(s.ContinuousSupported);
            Assert.Null(s.ContinuousAvailable);
            Assert.Null(s.ContinuousUnavailabilityReason);
        }

        [Fact]
        public void ToSettingsChildren_Chime_MapsItsTwoFeatureObjects()
        {
            IReadOnlyList<DeviceFeatureState> states = States(Load("3001"));

            Assert.Equal(["auto_track", "video_rendering"], states.Select(s => s.FeatureKey).OrderBy(k => k));
        }

        [Fact]
        public void ToSettingsChildren_NoFeaturesObject_LeavesFeatureStatesUntouched()
        {
            var devices = new Devices { Doorbots = [new Doorbot { Id = 5 }] };

            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(RingDeviceDetailsMapper.Find(devices, "5")!, DeviceId)!;

            Assert.Null(children.FeatureStates);
        }

        private static List<string> Reasons(DeviceFeatureState s, string kind)
        {
            return s.Reasons.Where(r => r.Kind == kind).OrderBy(r => r.Position).Select(r => r.Reason).ToList();
        }

        private static DoorbotFeatures Features(string json)
        {
            return JsonSerializer.Deserialize<DoorbotFeatures>(json)!;
        }
    }
}
