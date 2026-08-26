namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Platform-agnostic device discovery interface</summary>
    public interface IDeviceDiscoveryService
    {
        /// <summary>Gets all locations/sites for the authenticated user</summary>
        Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets all devices at a specific location</summary>
        Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId, CancellationToken cancellationToken = default);

        /// <summary>Gets a specific device details</summary>
        Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    }

    /// <summary>Represents a location/site (home, office, etc.)</summary>
    public record Location(
        string Id,
        string Name,
        string? Address = null,
        Dictionary<string, string>? Metadata = null
    );

    /// <summary>Represents a security device (camera, doorbell, etc.)</summary>
    public record Device(
        string Id,
        string Name,
        string Type, // "camera", "doorbell", "base_station", etc.
        string LocationId,
        bool IsOnline,
        Dictionary<string, string>? Metadata = null
    );
}
