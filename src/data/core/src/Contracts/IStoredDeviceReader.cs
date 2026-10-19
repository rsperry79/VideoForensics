using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>
    /// Answers location and device discovery reads from stored rows only. It never calls a provider:
    /// only the config sync pulls from the provider and writes these rows.
    /// </summary>
    public interface IStoredDeviceReader
    {
        /// <summary>Lists stored locations; <see cref="Location.Id"/> is the provider location id.</summary>
        Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken ct);

        /// <summary>Lists stored devices at the location with the given provider location id (empty when unknown).</summary>
        Task<IReadOnlyList<Device>> GetDevicesAsync(string providerLocationId, CancellationToken ct);

        /// <summary>Gets one stored device by provider device id, or null when none is stored.</summary>
        Task<Device?> GetDeviceAsync(string providerDeviceId, CancellationToken ct);
    }
}
