namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Shared rule for whether a device is offline, implemented once per provider. Callers (for example the snapshot capture
    /// service) ask this instead of reading a stored connection flag directly, because each provider reports connectivity differently.
    /// Resolve it keyed by provider name.
    /// </summary>
    public interface IProviderDeviceStatus
    {
        /// <summary>
        /// Returns true only when the provider reports the device as offline. An unknown connection state (null) is not offline:
        /// the provider gave no state to act on, so callers should keep the device.
        /// </summary>
        /// <param name="deviceType">The provider's own device type string (for example Ring's "doorbot" or "chime").</param>
        /// <param name="isOnline">The last known connection state, null when the provider gave none.</param>
        bool IsOffline(string deviceType, bool? isOnline);
    }
}
