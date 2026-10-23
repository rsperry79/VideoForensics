using System.Collections.Concurrent;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Tracks elevated polling windows for devices. When a device's RSSI degrades significantly,
    /// it enters an elevated window where faster polling occurs to detect jamming incidents.
    /// </summary>
    public class ElevatedPollingWindowTracker
    {
        private readonly ConcurrentDictionary<Guid, DateTime> _elevatedUntilUtc = new();

        /// <summary>
        /// Enters or extends an elevated polling window for a device.
        /// </summary>
        /// <param name="deviceId">The device to track.</param>
        /// <param name="window">Duration of the elevated window from now.</param>
        public void EnterElevated(Guid deviceId, TimeSpan window)
        {
            _elevatedUntilUtc[deviceId] = DateTime.UtcNow + window;
        }

        /// <summary>
        /// Checks if a device is currently in an elevated polling window.
        /// </summary>
        /// <param name="deviceId">The device to check.</param>
        /// <returns>True if the device is in an active elevated window; false otherwise.</returns>
        public bool IsElevated(Guid deviceId)
        {
            return _elevatedUntilUtc.TryGetValue(deviceId, out var until) && until > DateTime.UtcNow;
        }

        /// <summary>
        /// Clears a device from the elevated window tracker.
        /// </summary>
        /// <param name="deviceId">The device to clear.</param>
        public void Clear(Guid deviceId)
        {
            _elevatedUntilUtc.TryRemove(deviceId, out _);
        }

        /// <summary>
        /// Gets all device IDs currently in elevated polling windows.
        /// </summary>
        public IReadOnlyCollection<Guid> ElevatedDeviceIds =>
            _elevatedUntilUtc
                .Where(kv => kv.Value > DateTime.UtcNow)
                .Select(kv => kv.Key)
                .ToList();
    }
}
