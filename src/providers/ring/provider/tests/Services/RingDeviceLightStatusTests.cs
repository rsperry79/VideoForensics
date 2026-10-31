using System.Text.Json;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>led_status is mapped to the observed light seconds that become a light event, never to a Devices column.</summary>
    public class RingDeviceLightStatusTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static RingDeviceDetails Load(string providerId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "device-light-status-sanitized.json");
            Devices devices = JsonSerializer.Deserialize<Devices>(File.ReadAllText(path))!;
            return RingDeviceDetailsMapper.Find(devices, providerId)!;
        }

        [Theory]
        [InlineData("5001", 0)]
        [InlineData("5002", 20)]
        public void Find_LedStatusObject_MapsSecondsRemaining(string id, int expected)
        {
            Assert.Equal(expected, Load(id).LedSecondsRemaining);
        }

        [Theory]
        [InlineData("5007", "String")]
        [InlineData("5004", "Number")]
        public void Find_LedStatusStringOrNumber_NotStoredAndNoted(string id, string jsonType)
        {
            RingDeviceDetails d = Load(id);

            Assert.Null(d.LedSecondsRemaining);
            Assert.Contains(d.UnstoredNotes!, n => n == $"led_status.seconds_remaining not stored: missing or unexpected shape ({jsonType})");
        }

        [Theory]
        [InlineData("5003", true)]
        [InlineData("5006", false)]
        public void Find_LedStatusOnOffString_MapsStateWithoutSecondsOrNote(string id, bool on)
        {
            RingDeviceDetails d = Load(id);

            Assert.Equal(on, d.LedOn);
            Assert.Null(d.LedSecondsRemaining);
            Assert.DoesNotContain(d.UnstoredNotes!, n => n.StartsWith("led_status"));
        }

        [Fact]
        public void ToSettingsChildrenWithFeatures_LedOn_CarriedToChildren()
        {
            Assert.True(RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("5003"), DeviceId)!.LightIsOn);
        }

        [Fact]
        public void Find_NoLedStatus_NoSecondsAndNoNote()
        {
            RingDeviceDetails d = Load("5005");

            Assert.Null(d.LedSecondsRemaining);
            Assert.DoesNotContain(d.UnstoredNotes!, n => n.StartsWith("led_status"));
        }

        [Fact]
        public void ToSettingsChildrenWithFeatures_LedSeconds_CarriedToChildren()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("5002"), DeviceId)!;

            Assert.Equal(20, children.LightSecondsRemaining);
        }

        [Fact]
        public void UnstoredLog_UnknownLedStringNote_LogsInfoWithDeviceNameKeyAndTypeButNoValue()
        {
            var logger = new Mock<ILogger>();
            _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

            RingUnstoredValueLog.Log(logger.Object, "Gate", Load("5007"));

            logger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => s.ToString()!.Contains("Gate") && s.ToString()!.Contains("led_status") && s.ToString()!.Contains("String") && !s.ToString()!.Contains("bright")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}
