using Microsoft.Extensions.Logging;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Core;

namespace VideoForensics.Providers.Uniview;

/// <summary>
/// Uniview NVR provider implementation using the platform-agnostic IVideoProvider interface.
/// Provides authentication, device discovery, video/snapshot downloads, and event management for Uniview NVRs.
/// </summary>
public class UniviewVideoProvider : BaseVideoProvider
{
    public override string ProviderName => "Uniview";
    public override IProviderAuthService AuthService { get; }
    public override IDeviceDiscoveryService DeviceService { get; }
    public override IMediaDownloadService DownloadService { get; }
    public override IEventAndConfigService EventService { get; }

    /// <summary>
    /// Initializes a new Uniview provider instance.
    /// </summary>
    /// <param name="logger">Logger for diagnostics and troubleshooting</param>
    /// <param name="authService">Provider authentication service</param>
    /// <param name="deviceService">Device discovery service</param>
    /// <param name="downloadService">Media download service</param>
    /// <param name="eventService">Event and configuration service</param>
    public UniviewVideoProvider(
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
