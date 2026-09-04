namespace VideoForensics.Providers.Common.Contracts
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
        /// Fetches current health/connectivity telemetry for every device visible on the
        /// authenticated account in one call. Returns an empty list (not a throw) when no session
        /// is currently available, so a caller iterating multiple health sources isn't interrupted
        /// by one provider's account not being signed in.
        /// </summary>
        Task<IReadOnlyList<DeviceHealthReading>> FetchHealthAsync(CancellationToken ct);
    }

    /// <summary>One device's point-in-time health telemetry, keyed by the provider's own device id (not the DB Guid).</summary>
    public record DeviceHealthReading(
        string ProviderDeviceId,
        bool? Connected,
        decimal? BatteryPercentage,
        int? Rssi,
        string? WifiName,
        string? FirmwareVersion
    );
}
