namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Optional capability a provider implements when it can supply device health/signal telemetry
    /// independent of a media download (e.g. Ring's /clients_api/ring_devices response). Not every
    /// provider can - a provider without this capability simply has no implementation registered,
    /// and the RSSI/health background sync (see DeviceHealthSyncService) skips it.
    /// </summary>
    public interface IProviderHealthSource
    {
        /// <summary>
        /// Fetches current health/connectivity telemetry for every device visible across all active
        /// provider accounts in one call (for providers supporting multiple accounts per operator).
        /// Returns an empty list (not a throw) when no sessions are currently available, so a caller
        /// iterating multiple health sources isn't interrupted by one provider's account not being
        /// signed in. Automatically restores expired sessions from saved credentials where possible.
        /// </summary>
        Task<IReadOnlyList<DeviceHealthReading>> FetchHealthAsync(CancellationToken ct);
    }

    /// <summary>One device's point-in-time health telemetry, keyed by the provider's own device id (not the DB Guid).</summary>
    /// <remarks>The trailing telemetry fields are optional so providers that don't report them can omit them.</remarks>
    public record DeviceHealthReading(
        string ProviderDeviceId,
        bool? Connected,
        decimal? BatteryPercentage,
        int? Rssi,
        string? WifiName,
        string? FirmwareVersion,
        decimal? BatteryVoltageValue = null,
        bool? IsExternalPowerConnected = null,
        string? OtaStatus = null,
        DateTime? LastHeartbeatUtc = null,
        string? DeviceType = null,
        string? FirmwareVersionStatus = null,
        bool? BatteryPresent = null,
        string? BatteryPercentageCategory = null,
        string? BatteryVoltageCategory = null,
        string? RssiCategory = null,
        string? NetworkConnectionValue = null,
        double? PacketLoss = null,
        string? PacketLossCategory = null,
        double? LatestSignalStrength = null,
        string? LatestSignalCategory = null,
        double? AverageSignalStrength = null,
        string? AverageSignalCategory = null,
        string? Firmware = null,
        DateTime? UpdatedAtUtc = null,
        double? PacketLossStrength = null,
        bool? WifiIsRingNetwork = null,
        bool? PtzConnected = null,
        long? RingHealthId = null,
        IReadOnlyList<string>? SupportedRpcCommands = null
    );
}
