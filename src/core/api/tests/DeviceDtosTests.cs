using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class DeviceDtosTests
    {
        private static Device CreateDeviceWithRingFields() => new()
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            LocationId = Guid.Parse("66666666-7777-8888-9999-000000000000"),
            ProviderDeviceId = "123",
            Name = "Front Door",
            Type = "doorbell",
            RingDeviceId = "aabbcc",
            RingKind = "doorbell_v4",
            FirmwareVersion = "2.0.1",
            Latitude = 41.5,
            Longitude = -87.6,
            Address = "1 Main St",
            Subscribed = true,
            SubscribedMotions = false,
            BatteryLife = "87",
            ExternalConnection = true,
            Owned = true,
            Stolen = false
        };

        [Fact]
        public void DeviceDtoMapping_ToDto_WithRingFields_CopiesThemToDto()
        {
            Device device = CreateDeviceWithRingFields();

            DeviceDto dto = device.ToDto();

            Assert.Equal("aabbcc", dto.RingDeviceId);
            Assert.Equal("doorbell_v4", dto.RingKind);
            Assert.Equal("2.0.1", dto.FirmwareVersion);
            Assert.Equal(41.5, dto.Latitude);
            Assert.Equal(-87.6, dto.Longitude);
            Assert.Equal("1 Main St", dto.Address);
            Assert.True(dto.Subscribed);
            Assert.False(dto.SubscribedMotions);
            Assert.Equal("87", dto.BatteryLife);
            Assert.True(dto.ExternalConnection);
            Assert.True(dto.Owned);
            Assert.False(dto.Stolen);
        }

        [Fact]
        public void DeviceDtoMapping_ToDto_MissingRingFields_StayNull()
        {
            var device = new Device { ProviderDeviceId = "1", Name = "Unknown", Type = "doorbell" };

            DeviceDto dto = device.ToDto();

            Assert.Null(dto.RingDeviceId);
            Assert.Null(dto.Latitude);
            Assert.Null(dto.Longitude);
            Assert.Null(dto.Owned);
            Assert.Null(dto.Stolen);
            Assert.Null(dto.Subscribed);
        }

        [Fact]
        public void DeviceDtoMapping_ToDomain_RoundTripKeepsRingFields()
        {
            Device original = CreateDeviceWithRingFields();

            Device restored = original.ToDto().ToDomain();

            Assert.Equal("aabbcc", restored.RingDeviceId);
            Assert.Equal("doorbell_v4", restored.RingKind);
            Assert.Equal(41.5, restored.Latitude);
            Assert.Equal(-87.6, restored.Longitude);
            Assert.Equal("87", restored.BatteryLife);
            Assert.True(restored.Owned);
            Assert.False(restored.Stolen);
        }

        [Fact]
        public void DeviceHealthDtoMapping_ToDto_WithExtendedFields_CopiesThemToDto()
        {
            var captured = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
            var entity = CreateHealthWithExtendedFields(captured);

            VideoForensics.Core.Api.Contracts.DeviceHealthDto dto = entity.ToDto();

            Assert.Equal("doorbell", dto.DeviceType);
            Assert.Equal("current", dto.FirmwareVersionStatus);
            Assert.True(dto.BatteryPresent);
            Assert.Equal("good", dto.BatteryPercentageCategory);
            Assert.Equal("ok", dto.BatteryVoltageCategory);
            Assert.Equal("strong", dto.RssiCategory);
            Assert.Equal("wifi", dto.NetworkConnectionValue);
            Assert.Equal(0.5, dto.PacketLoss);
            Assert.Equal("low", dto.PacketLossCategory);
            Assert.Equal(-60.5, dto.LatestSignalStrength);
            Assert.Equal("strong", dto.LatestSignalCategory);
            Assert.Equal(-61.0, dto.AverageSignalStrength);
            Assert.Equal("strong", dto.AverageSignalCategory);
            Assert.Equal("2.0.1", dto.Firmware);
            Assert.Equal(captured, dto.UpdatedAtUtc);
            Assert.Equal(1.25, dto.PacketLossStrength);
            Assert.True(dto.WifiIsRingNetwork);
            Assert.False(dto.PtzConnected);
        }

        [Fact]
        public void DeviceHealthDtoMapping_ToDomain_RoundTripKeepsExtendedFields()
        {
            var captured = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
            DeviceHealth original = CreateHealthWithExtendedFields(captured);

            DeviceHealth restored = original.ToDto().ToDomain();

            Assert.Equal("doorbell", restored.DeviceType);
            Assert.Equal("current", restored.FirmwareVersionStatus);
            Assert.True(restored.BatteryPresent);
            Assert.Equal("wifi", restored.NetworkConnectionValue);
            Assert.Equal(-61.0, restored.AverageSignalStrength);
            Assert.Equal("2.0.1", restored.Firmware);
            Assert.Equal(captured, restored.UpdatedAtUtc);
            Assert.Equal(1.25, restored.PacketLossStrength);
            Assert.True(restored.WifiIsRingNetwork);
            Assert.False(restored.PtzConnected);
        }

        private static DeviceHealth CreateHealthWithExtendedFields(DateTime captured) => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CapturedAtUtc = captured,
            DeviceType = "doorbell",
            FirmwareVersionStatus = "current",
            BatteryPresent = true,
            BatteryPercentageCategory = "good",
            BatteryVoltageCategory = "ok",
            RssiCategory = "strong",
            NetworkConnectionValue = "wifi",
            PacketLoss = 0.5,
            PacketLossCategory = "low",
            LatestSignalStrength = -60.5,
            LatestSignalCategory = "strong",
            AverageSignalStrength = -61.0,
            AverageSignalCategory = "strong",
            Firmware = "2.0.1",
            UpdatedAtUtc = captured,
            PacketLossStrength = 1.25,
            WifiIsRingNetwork = true,
            PtzConnected = false
        };
    }
}
