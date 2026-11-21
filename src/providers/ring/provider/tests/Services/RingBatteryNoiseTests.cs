using System.Linq;
using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests.Services
{
    /// <summary>
    /// A wired device reports a battery entry with every battery value null. Those nulls are expected, so they must not
    /// be logged as unstored. A device that does report a battery keeps the notes for its null fields.
    /// </summary>
    public class RingBatteryNoiseTests
    {
        private const string WiredHealth =
            "{\"status_time\":1,\"batteries\":[{\"battery_number\":1,\"battery_id\":null,\"battery_percentage\":null,\"battery_present\":null," +
            "\"battery_charging\":null,\"battery_voltage\":null,\"battery_family\":null,\"battery_fw_version\":null,\"battery_ota_status\":null," +
            "\"battery_charge_fault\":null,\"battery_temp\":2941.0}]}";

        private const string BatteryHealth =
            "{\"status_time\":1,\"batteries\":[{\"battery_number\":1,\"battery_id\":null,\"battery_percentage\":80,\"battery_present\":true," +
            "\"battery_charging\":false,\"battery_voltage\":\"4012.0\",\"battery_family\":null,\"battery_fw_version\":null,\"battery_ota_status\":null," +
            "\"battery_charge_fault\":null,\"battery_temp\":2941.0}]}";

        [Fact]
        public void WiredDevice_NullBatteryFields_AreNotLogged()
        {
            RingReportedHealth parsed = RingDeviceHealthParser.Parse(JsonSerializer.Deserialize<DeviceHealth>(WiredHealth)!)!;

            Assert.DoesNotContain(parsed.Unstored, n => n.StartsWith("health.batteries", System.StringComparison.Ordinal));
        }

        [Fact]
        public void BatteryDevice_NullBatteryFields_StillLogged()
        {
            RingReportedHealth parsed = RingDeviceHealthParser.Parse(JsonSerializer.Deserialize<DeviceHealth>(BatteryHealth)!)!;

            Assert.Contains(parsed.Unstored, n => n == "health.batteries.battery_family is null; its column type is a guess");
        }
    }
}
