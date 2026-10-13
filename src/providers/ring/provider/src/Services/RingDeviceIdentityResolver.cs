using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Resolves and caches Ring device identity: User → Account → Location → Device.
    /// This identity resolution is cached so it only happens once per process.
    /// </summary>
    public interface IRingDeviceIdentityResolver
    {
        /// <summary>
        /// Ensures a device exists in the database with the correct account/location identity,
        /// and caches the result per device ID so redundant EnsureDeviceAsync calls are avoided.
        /// </summary>
        Task<Guid> EnsureDeviceIdentityAsync(string providerDeviceId, string deviceName, string? providerLocationId, CancellationToken ct);

        /// <summary>
        /// Sets the active provider account ID for this resolution session.
        /// Must be called before any download starts to avoid falling back to a synthetic "default" account.
        /// </summary>
        void SetActiveProviderAccountId(Guid accountId);
    }

    public class RingDeviceIdentityResolver : IRingDeviceIdentityResolver
    {
        private readonly IVideoForensicsDataClient _dataClient;
        private readonly ILogger _logger;

        // Cache the User/Account/Location identity resolution so we only do it once per process.
        // _cachedProviderAccountId is primed by the caller via SetActiveProviderAccountId before any
        // download starts - without that, this used to always fall back to finding-or-creating a
        // synthetic "Ring"/"default" account, silently attributing every download to the wrong
        // account regardless of which one was actually authenticated. _locationIdCache is keyed by
        // the device's real provider location id (not a single field) since an account can have more
        // than one location - a single cached value meant every device after the first got its DB
        // record silently relocated to whichever location the first device resolved to.
        private readonly SemaphoreSlim _identityResolutionLock = new(1, 1);
        private Guid? _cachedProviderAccountId;
        private readonly ConcurrentDictionary<string, Guid> _locationIdCache = new();

        // Per-device Guid cache to avoid redundant EnsureDeviceAsync calls for the same providerDeviceId
        private readonly ConcurrentDictionary<string, Guid> _deviceIdCache = new();

        public RingDeviceIdentityResolver(IVideoForensicsDataClient dataClient, ILogger logger)
        {
            _dataClient = dataClient ?? throw new ArgumentNullException(nameof(dataClient));
            _logger = logger;
        }

        public void SetActiveProviderAccountId(Guid accountId)
        {
            _cachedProviderAccountId = accountId;
        }

        public async Task<Guid> EnsureDeviceIdentityAsync(string providerDeviceId, string deviceName, string? providerLocationId, CancellationToken ct)
        {
            // Check per-device cache first
            if (_deviceIdCache.TryGetValue(providerDeviceId, out Guid cachedDeviceId))
            {
                return cachedDeviceId;
            }

            // Falls back to a synthetic "default" location only when the caller genuinely has no
            // real Ring location id for this device — matches VideoDownloadServiceAdapter's own
            // fallback so both layers agree on the placeholder's identity instead of creating two
            // different "default" locations.
            string effectiveLocationId = string.IsNullOrEmpty(providerLocationId) ? "default" : providerLocationId;

            // Resolve Account/Location once per effectiveLocationId and cache them
            await _identityResolutionLock.WaitAsync(ct);
            Guid locationGuid;
            try
            {
                if (_deviceIdCache.TryGetValue(providerDeviceId, out cachedDeviceId))
                {
                    return cachedDeviceId;
                }

                if (!_cachedProviderAccountId.HasValue)
                {
                    // SetActiveProviderAccountId wasn't called before this - shouldn't normally
                    // happen since the caller resolves the real active account first, but fall back
                    // to a synthetic placeholder rather than failing the whole download outright.
                    _logger.LogWarning("No active provider account set; falling back to a synthetic placeholder account for device identity resolution");
                    (_, ProviderAccount? account) = await _dataClient.EnsureUserAndAccountAsync(
                        "Ring",
                        "default",
                        "default",
                        null,
                        ct);
                    _cachedProviderAccountId = account.Id;
                }

                if (!_locationIdCache.TryGetValue(effectiveLocationId, out locationGuid))
                {
                    Data.Common.Entities.Location location = await _dataClient.EnsureLocationAsync(
                        _cachedProviderAccountId.Value,
                        effectiveLocationId,
                        effectiveLocationId,
                        null,
                        ct: ct);
                    locationGuid = location.Id;
                    _locationIdCache[effectiveLocationId] = locationGuid;
                }
            }
            finally
            {
                _ = _identityResolutionLock.Release();
            }

            // Now resolve the device
            Data.Common.Entities.Device device = await _dataClient.EnsureDeviceAsync(
                locationGuid,
                providerDeviceId,
                deviceName,
                "camera",
                true,
                ct: ct);

            // Cache in the per-device dictionary
            _ = _deviceIdCache.TryAdd(providerDeviceId, device.Id);
            return device.Id;
        }
    }
}
