using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Ring's offline rule. Ring reports connection through the <c>health.connected</c> field, which is stored as
    /// <c>Device.IsOnline</c>. A device is offline only when that reads false. The rule is the same for every Ring kind,
    /// so the device type is not used.
    /// </summary>
    public sealed class RingDeviceOnlineStatus : IProviderDeviceStatus
    {
        /// <inheritdoc />
        public bool IsOffline(string deviceType, bool? isOnline)
        {
            return isOnline == false;
        }
    }
}
