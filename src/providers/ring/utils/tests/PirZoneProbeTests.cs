using System.Text.Json;

using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    public class PirZoneProbeTests
    {
        [Theory]
        [InlineData(5, 6)]
        [InlineData(0, 1)]
        [InlineData(10, 9)]
        public void Probe_MovesOneStepInsideRange(int captured, int expected)
        {
            Assert.Equal(expected, PirZoneProbe.Probe(captured));
        }

        [Fact]
        public void Body_IsValidJsonSettingOnlyZone1()
        {
            using JsonDocument doc = JsonDocument.Parse(PirZoneProbe.Body(6));
            JsonElement zone = doc.RootElement.GetProperty("motion_settings").GetProperty("advanced_pir_motion_zones");

            Assert.Equal(6, zone.GetProperty("zone1_sensitivity").GetInt32());
            Assert.Equal(1, zone.EnumerateObject().Count());
        }
    }
}
