using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Core
{
    /// <summary>Base class for video provider implementations</summary>
    public abstract class BaseVideoProvider : IVideoProvider
    {
        protected readonly ILogger Logger;

        public abstract string ProviderName { get; }
        public abstract IProviderAuthService AuthService { get; }
        public abstract IDeviceDiscoveryService DeviceService { get; }
        public abstract IMediaDownloadService DownloadService { get; }
        public abstract IEventAndConfigService EventService { get; }

        protected BaseVideoProvider(ILogger logger)
        {
            Logger = logger;
        }
    }
}
