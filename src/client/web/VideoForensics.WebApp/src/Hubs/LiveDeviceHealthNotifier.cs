using Microsoft.AspNetCore.SignalR;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Announces a device health change to two audiences: the WebApp's own in-process views through
    /// <see cref="DeviceHealthChangedBus"/>, and every remote client connected to <see cref="LiveHub"/>.
    /// The hub already requires paired-device auth for each connection, so a broadcast reaches only authorized clients.
    /// </summary>
    public sealed class LiveDeviceHealthNotifier : IDeviceHealthChangeNotifier
    {
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly DeviceHealthChangedBus _bus;
        private readonly ILogger<LiveDeviceHealthNotifier> _logger;

        /// <summary>Creates the notifier.</summary>
        public LiveDeviceHealthNotifier(IHubContext<LiveHub> hubContext, DeviceHealthChangedBus bus, ILogger<LiveDeviceHealthNotifier> logger)
        {
            _hubContext = hubContext;
            _bus = bus;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task DeviceHealthRecordedAsync(Guid deviceId, CancellationToken ct)
        {
            var change = new DeviceHealthChangedDto(deviceId);
            _bus.Publish(change);
            try
            {
                await _hubContext.Clients.All.SendAsync(LiveHubMethods.DeviceHealthChanged, change, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The reading is already stored; a failed push only means open pages show the new status on their next load.
                _logger.LogWarning(ex, "Could not push a device health change for device {DeviceId} to live clients", deviceId);
            }
        }
    }
}
