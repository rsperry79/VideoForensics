using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class RingHealthTelemetryMapperTests
    {
        [Fact]
        public void ToExternalPowerConnected_StateOne_ReturnsTrue()
        {
            Assert.True(RingHealthTelemetryMapper.ToExternalPowerConnected(1));
        }

        [Fact]
        public void ToExternalPowerConnected_StateZero_ReturnsFalse()
        {
            Assert.False(RingHealthTelemetryMapper.ToExternalPowerConnected(0));
        }

        [Fact]
        public void ToExternalPowerConnected_UnrecognisedOrMissingState_ReturnsNull()
        {
            Assert.Null(RingHealthTelemetryMapper.ToExternalPowerConnected(null));
            Assert.Null(RingHealthTelemetryMapper.ToExternalPowerConnected(2));
        }

        [Fact]
        public void ToHeartbeatUtc_EpochSeconds_ConvertsToUtc()
        {
            Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), RingHealthTelemetryMapper.ToHeartbeatUtc(1_700_000_000L));
        }

        [Fact]
        public void ToHeartbeatUtc_EpochMilliseconds_ConvertsToUtc()
        {
            Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), RingHealthTelemetryMapper.ToHeartbeatUtc(1_700_000_000_000L));
        }

        [Fact]
        public void ToHeartbeatUtc_NullOrNonPositive_ReturnsNull()
        {
            Assert.Null(RingHealthTelemetryMapper.ToHeartbeatUtc(null));
            Assert.Null(RingHealthTelemetryMapper.ToHeartbeatUtc(0L));
            Assert.Null(RingHealthTelemetryMapper.ToHeartbeatUtc(-5L));
        }

        [Fact]
        public void ToUpdatedAtUtc_OffsetString_ConvertsToUtc()
        {
            DateTime? result = RingHealthTelemetryMapper.ToUpdatedAtUtc("2026-10-16T07:30:00-05:00");

            Assert.Equal(new DateTime(2026, 10, 16, 12, 30, 0, DateTimeKind.Utc), result);
        }

        [Fact]
        public void ToUpdatedAtUtc_StringWithoutOffset_AssumesUtc()
        {
            DateTime? result = RingHealthTelemetryMapper.ToUpdatedAtUtc("2026-10-16T12:30:00");

            Assert.Equal(new DateTime(2026, 10, 16, 12, 30, 0, DateTimeKind.Utc), result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-date")]
        public void ToUpdatedAtUtc_NullOrUnparseable_ReturnsNull(string? value)
        {
            Assert.Null(RingHealthTelemetryMapper.ToUpdatedAtUtc(value));
        }
    }
}
