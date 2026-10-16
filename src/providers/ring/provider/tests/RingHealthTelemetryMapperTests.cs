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
    }
}
