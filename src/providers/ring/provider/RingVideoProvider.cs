using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Core;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Providers.Ring
{
    public class RingVideoProvider : BaseVideoProvider
    {
        public override string ProviderName => "Ring";
        public override IProviderAuthService AuthService { get; }
        public override IDeviceDiscoveryService DeviceService { get; }
        public override IMediaDownloadService DownloadService { get; }
        public override IEventAndConfigService EventService { get; }

        public RingVideoProvider(
            ILogger logger,
            IProviderAuthService authService,
            IDeviceDiscoveryService deviceService,
            IMediaDownloadService downloadService,
            IEventAndConfigService eventService)
            : base(logger)
        {
            AuthService = authService;
            DeviceService = deviceService;
            DownloadService = downloadService;
            EventService = eventService;
        }
    }
}
