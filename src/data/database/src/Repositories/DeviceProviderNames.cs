using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Fills Device.ProviderName from the provider links of each device's location.</summary>
    internal static class DeviceProviderNames
    {
        /// <summary>
        /// Sets ProviderName on each device from its location's links. A location with several linked accounts takes the
        /// first by name, so the result is stable across calls. Devices whose location has no link keep null.
        /// </summary>
        public static void Assign(IEnumerable<Device> devices, IEnumerable<(Guid LocationId, string ProviderName)> links)
        {
            var names = links
                .GroupBy(x => x.LocationId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ProviderName).OrderBy(n => n, StringComparer.Ordinal).First());

            foreach (Device device in devices)
            {
                device.ProviderName = names.GetValueOrDefault(device.LocationId);
            }
        }
    }
}
