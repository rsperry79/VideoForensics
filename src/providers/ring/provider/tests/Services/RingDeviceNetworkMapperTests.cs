using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the paid-feature, network-settings and network-diagnosis groups (sanitized fixture) onto the settings child rows.</summary>
    public class RingDeviceNetworkMapperTests
    {
        private static readonly Guid DeviceId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        private static RingDeviceDetails Load(string providerId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "device-paid-network-sanitized.json");
            Devices devices = JsonSerializer.Deserialize<Devices>(File.ReadAllText(path))!;
            return RingDeviceDetailsMapper.Find(devices, providerId)!;
        }

        [Fact]
        public void ToSettingsChildren_DoorbotSettings_MapsPaidFeaturesNetworkSettingsAndDiagnosis()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("4001"), DeviceId)!;

            RingDevicePaidFeatures paid = children.PaidFeatures!;
            Assert.Equal(DeviceId, paid.DeviceId);
            Assert.NotEqual(Guid.Empty, paid.Id);
            Assert.True(paid.CvPaidBabyCry);
            Assert.True(paid.CvPaidHuman);
            Assert.Null(paid.CvPaidAnimal);
            Assert.True(paid.OtherPaidAlexaConcierge);
            Assert.False(paid.OtherPaidRecording24x7);

            DeviceNetworkSettings network = children.NetworkSettings!;
            Assert.Equal(DeviceId, network.DeviceId);
            Assert.Equal("02:00:00:00:00:02", network.MacAddressWifi24);
            Assert.Equal(1, network.MultiNetPref);

            RingDeviceNetworkDiagnosis diagnosis = children.NetworkDiagnosis!;
            Assert.Equal(DeviceId, diagnosis.DeviceId);
            Assert.Equal(5201, diagnosis.TcpPort);
            Assert.Equal("diag-host-fake", diagnosis.UdpHost);
            Assert.Equal(1000000L, diagnosis.UdpBandwidth);
        }

        [Fact]
        public void ToSettingsChildren_StickupCamWithNullDiagnosis_WritesRowsOfNulls()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("5001"), DeviceId)!;

            Assert.NotNull(children.NetworkSettings);
            Assert.Null(children.NetworkSettings!.MacAddressBle);
            Assert.NotNull(children.NetworkDiagnosis);
            Assert.Null(children.NetworkDiagnosis!.ChannelAnalysis);
            Assert.Null(children.NetworkDiagnosis.UdpBandwidth);
        }

        [Fact]
        public void ToSettingsChildren_StickupCamWithOnlyOtherPaidFeatures_LeavesCvColumnsNull()
        {
            RingDevicePaidFeatures paid = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("5001"), DeviceId)!.PaidFeatures!;

            Assert.Null(paid.CvPaidBabyCry);
            Assert.False(paid.OtherPaidLiveSpeak);
            Assert.Null(paid.OtherPaidCriticalAlerts);
        }

        [Fact]
        public void ToSettingsChildren_AuthorizedDoorbotWithoutGroups_LeavesGroupRowsNull()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("4002"), DeviceId)!;

            Assert.Null(children.PaidFeatures);
            Assert.Null(children.NetworkSettings);
            Assert.Null(children.NetworkDiagnosis);
        }

        [Fact]
        public void ToSettingsChildren_Chime_LeavesGroupRowsNull()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("6001"), DeviceId)!;

            Assert.Null(children.PaidFeatures);
            Assert.Null(children.NetworkSettings);
            Assert.Null(children.NetworkDiagnosis);
        }

        [Fact]
        public void ToSettingsChildren_CalledTwice_RowsCarryFreshIds()
        {
            RingDeviceDetails details = Load("4001");

            DeviceSettingsChildren first = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details, DeviceId)!;
            DeviceSettingsChildren second = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details, DeviceId)!;

            Assert.NotEqual(first.PaidFeatures!.Id, second.PaidFeatures!.Id);
            Assert.NotEqual(first.NetworkSettings!.Id, second.NetworkSettings!.Id);
            Assert.NotEqual(first.NetworkDiagnosis!.Id, second.NetworkDiagnosis!.Id);
        }

        [Fact]
        public void UnstoredNotes_PaidFeatureWrongTypeAndUnknownKey_ReportedWithoutValues()
        {
            RingDeviceDetails stickup = Load("5001");
            RingDeviceDetails doorbot = Load("4001");

            Assert.Contains(stickup.UnstoredNotes!, n => n.StartsWith("other_paid_features.critical_alerts not stored"));
            Assert.DoesNotContain(stickup.UnstoredNotes!, n => n.Contains("\"yes\"") || n.Contains("yes)"));
            Assert.Contains(doorbot.UnstoredNotes!, n => n.StartsWith("cv_paid_features.future_cv_flag not stored"));
        }
    }
}
