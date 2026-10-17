namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Time-series health metrics for a device (P0 entity for provider metadata normalization).</summary>
    public class DeviceHealth
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public decimal? BatteryPercentage { get; set; }
        public decimal? BatteryVoltageValue { get; set; }
        public int? WifiSignalRssi { get; set; }
        public string? WifiName { get; set; }
        public bool? IsExternalPowerConnected { get; set; }
        public string? OtaStatus { get; set; }
        public bool? IsOnline { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public string? FirmwareVersion { get; set; }
        public DateTime CapturedAtUtc { get; set; }

        public string? DeviceType { get; set; }

        public string? FirmwareVersionStatus { get; set; }

        public bool? BatteryPresent { get; set; }

        public string? BatteryPercentageCategory { get; set; }

        public string? BatteryVoltageCategory { get; set; }

        public string? RssiCategory { get; set; }

        public string? NetworkConnectionValue { get; set; }

        public double? PacketLoss { get; set; }

        public string? PacketLossCategory { get; set; }

        public double? LatestSignalStrength { get; set; }

        public string? LatestSignalCategory { get; set; }

        public double? AverageSignalStrength { get; set; }

        public string? AverageSignalCategory { get; set; }

        public string? Firmware { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

        public double? PacketLossStrength { get; set; }

        public bool? WifiIsRingNetwork { get; set; }

        public bool? PtzConnected { get; set; }

        /// <summary>Ring health object id (the "id" key), for matching a reading to Ring's record.</summary>
        public long? RingHealthId { get; set; }

        /// <summary>Ring supported_rpc_commands, one child row per command in Ring order.</summary>
        public List<DeviceHealthRpcCommand> SupportedRpcCommands { get; set; } = [];
    }
}
