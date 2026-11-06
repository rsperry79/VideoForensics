using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Converted siblings of the raw Ring health values: status_time (32768 Hz ticks) and battery_temp (assumed deci-kelvin).</summary>
    public class RingHealthUnitsTests
    {
        private static RingReportedHealth Parse(string json)
        {
            return RingDeviceHealthParser.Parse(JsonSerializer.Deserialize<Entities.DeviceHealth>(json)!)!;
        }

        [Theory]
        [InlineData(58766634686231L, "2026-10-31T02:56:05.1803894Z")]
        [InlineData(58765751484416L, "2026-10-30T19:26:52Z")]
        public void Parse_StatusTime_ConvertsTicksToUtcAndKeepsRaw(long ticks, string expected)
        {
            RingReportedHealth parsed = Parse($"{{\"status_time\":{ticks}}}");

            Assert.Equal(ticks, parsed.Health.StatusTime);
            Assert.NotNull(parsed.Health.StatusAtUtc);
            Assert.Equal(DateTimeKind.Utc, parsed.Health.StatusAtUtc!.Value.Kind);
            double diffMs = Math.Abs((parsed.Health.StatusAtUtc.Value - DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal)).TotalMilliseconds);
            Assert.True(diffMs <= 1, $"off by {diffMs} ms");
            Assert.DoesNotContain(parsed.Unstored, n => n.Contains("status_time"));
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(-5L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        [InlineData(3_000_000_000_000_000L)]
        public void Parse_StatusTimeOutOfRange_StoresNullAndNotesWithoutValue(long ticks)
        {
            RingReportedHealth parsed = Parse($"{{\"status_time\":{ticks}}}");

            Assert.Equal(ticks, parsed.Health.StatusTime);
            Assert.Null(parsed.Health.StatusAtUtc);
            Assert.Equal("health.status_time not converted to a date: outside 2000-01-01..2100-01-01", Assert.Single(parsed.Unstored, n => n.Contains("health.status_time")));
        }

        [Fact]
        public void Parse_BatteryTemp_ConvertsDeciKelvinToCelsiusOnHealthAndBattery()
        {
            RingReportedHealth parsed = Parse("{\"battery_temp\":2921.0,\"batteries\":[{\"battery_number\":1,\"battery_temp\":2921.0}]}");

            Assert.Equal(2921.0, parsed.Health.BatteryTemp);
            Assert.Equal(18.95m, parsed.Health.BatteryTempCelsius);
            Assert.Equal(2921.0, parsed.Batteries[0].Temp);
            Assert.Equal(18.95m, parsed.Batteries[0].BatteryTempCelsius);
        }

        [Theory]
        [InlineData(1500.0, -123.15)]
        [InlineData(4500.0, 176.85)]
        [InlineData(2731.5, 0.0)]
        [InlineData(2955.55, 22.41)]
        public void ToCelsius_InRange_RoundsToTwoDecimals(double raw, double expected)
        {
            Assert.Equal((decimal)expected, RingHealthUnits.BatteryTempToCelsius(raw));
        }

        [Theory]
        [InlineData(1499.9)]
        [InlineData(4500.1)]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void ToCelsius_OutOfRange_ReturnsNull(double raw)
        {
            Assert.Null(RingHealthUnits.BatteryTempToCelsius(raw));
        }

        [Fact]
        public void Parse_BatteryTempOutOfRange_StoresNullAndNotesWithoutValue()
        {
            RingReportedHealth parsed = Parse("{\"battery_temp\":99999.0,\"batteries\":[{\"battery_temp\":12.0}]}");

            Assert.Equal(99999.0, parsed.Health.BatteryTemp);
            Assert.Null(parsed.Health.BatteryTempCelsius);
            Assert.Equal(12.0, parsed.Batteries[0].Temp);
            Assert.Null(parsed.Batteries[0].BatteryTempCelsius);
            Assert.Contains(parsed.Unstored, n => n.Contains("health.battery_temp") && !n.Contains("99999"));
            Assert.Contains(parsed.Unstored, n => n.Contains("health.batteries[0].battery_temp") && !n.Contains("12.0"));
        }
    }
}
