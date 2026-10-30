namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>A location as the event path needs it: its database id and the provider id that media folders and provider calls use.</summary>
    /// <param name="Id">Database id of the location.</param>
    /// <param name="ProviderLocationId">Provider's id for the location.</param>
    /// <param name="Name">Display name, or null when the provider did not send one.</param>
    public sealed record CachedLocation(Guid Id, string ProviderLocationId, string? Name);

    /// <summary>A device as the event path needs it, linked to the location it belongs to.</summary>
    /// <param name="Id">Database id of the device. Events are written under this id.</param>
    /// <param name="LocationId">Database id of the device's location. Always set: a device is never cached without one.</param>
    /// <param name="ProviderLocationId">Provider's id for the device's location, needed by media downloads.</param>
    /// <param name="ProviderDeviceId">Provider's id for the device.</param>
    /// <param name="Name">Display name, or null when unknown.</param>
    public sealed record CachedDevice(Guid Id, Guid LocationId, string ProviderLocationId, string ProviderDeviceId, string? Name);

    /// <summary>
    /// One account's locations and devices, immutable once built. A reader that holds a snapshot never sees it change under it.
    /// </summary>
    /// <param name="Locations">Locations linked to the account.</param>
    /// <param name="DevicesByProviderId">Devices keyed by provider device id.</param>
    public sealed record AccountReferenceData(
        IReadOnlyList<CachedLocation> Locations,
        IReadOnlyDictionary<string, CachedDevice> DevicesByProviderId);

    /// <summary>
    /// In-memory cache of the location and device rows an event pull links new events to. It is filled from the database
    /// by the inventory sync (scheduled or manual) and read by event pulls, so a pull does not query the database or the
    /// provider for reference data. Thread-safe: reads take a snapshot, writes replace it.
    /// </summary>
    public interface IReferenceDataCache
    {
        /// <summary>Returns the account's snapshot, or null when none has been loaded.</summary>
        AccountReferenceData? Get(Guid providerAccountId);

        /// <summary>Replaces the account's snapshot with a freshly loaded one.</summary>
        void Replace(Guid providerAccountId, AccountReferenceData data);

        /// <summary>Returns the cached device for a provider device id, or null when the account has no such device cached.</summary>
        CachedDevice? FindDevice(Guid providerAccountId, string providerDeviceId);

        /// <summary>Adds or replaces one device in the account's snapshot, for a device written outside a full sync.</summary>
        void UpsertDevice(Guid providerAccountId, CachedDevice device);
    }
}
