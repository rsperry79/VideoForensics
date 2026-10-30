using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Process-wide <see cref="IReferenceDataCache"/>. Each account's snapshot is an immutable value held in a volatile field,
    /// so reads take no lock. Writers copy the map and swap it under a lock, which keeps concurrent writers from losing updates.
    /// </summary>
    public sealed class ReferenceDataCache : IReferenceDataCache
    {
        private readonly object _writeGate = new();

        // Why: a volatile reference to an immutable map lets readers see a complete snapshot without locking.
        private volatile IReadOnlyDictionary<Guid, AccountReferenceData> _accounts = new Dictionary<Guid, AccountReferenceData>();

        /// <inheritdoc />
        public AccountReferenceData? Get(Guid providerAccountId)
        {
            return _accounts.TryGetValue(providerAccountId, out AccountReferenceData? data) ? data : null;
        }

        /// <inheritdoc />
        public void Replace(Guid providerAccountId, AccountReferenceData data)
        {
            ArgumentNullException.ThrowIfNull(data);
            lock (_writeGate)
            {
                _accounts = WithAccount(providerAccountId, data);
            }
        }

        /// <inheritdoc />
        public CachedDevice? FindDevice(Guid providerAccountId, string providerDeviceId)
        {
            AccountReferenceData? data = Get(providerAccountId);
            return data is not null && data.DevicesByProviderId.TryGetValue(providerDeviceId, out CachedDevice? device) ? device : null;
        }

        /// <inheritdoc />
        public void UpsertDevice(Guid providerAccountId, CachedDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            lock (_writeGate)
            {
                // Why: without a loaded snapshot the location list would be incomplete, so the next sync or load fills it instead.
                if (!_accounts.TryGetValue(providerAccountId, out AccountReferenceData? current))
                {
                    return;
                }

                var devices = new Dictionary<string, CachedDevice>(current.DevicesByProviderId, StringComparer.Ordinal)
                {
                    [device.ProviderDeviceId] = device
                };
                _accounts = WithAccount(providerAccountId, new AccountReferenceData(current.Locations, devices));
            }
        }

        private IReadOnlyDictionary<Guid, AccountReferenceData> WithAccount(Guid providerAccountId, AccountReferenceData data)
        {
            var next = new Dictionary<Guid, AccountReferenceData>(_accounts) { [providerAccountId] = data };
            return next;
        }
    }
}
