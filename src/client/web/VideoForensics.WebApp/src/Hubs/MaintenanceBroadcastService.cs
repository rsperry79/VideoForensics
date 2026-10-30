using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR;

using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Sends every maintenance state change to all connected clients, in the order it happened. A channel keeps the order:
    /// the gate publishes synchronously, and each send is awaited before the next. Clients that connect later get the
    /// current state from <see cref="LiveHub"/> on connect.
    /// </summary>
    public sealed class MaintenanceBroadcastService(IMaintenanceGate gate, IHubContext<LiveHub> hubContext, ILogger<MaintenanceBroadcastService> logger)
        : BackgroundService
    {
        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var changes = Channel.CreateUnbounded<MaintenanceStatusDto>(new UnboundedChannelOptions { SingleReader = true });
            using IDisposable subscription = gate.Subscribe(new ChannelObserver(changes.Writer));

            await foreach (MaintenanceStatusDto status in changes.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await hubContext.Clients.All.SendAsync(LiveHubMethods.MaintenanceState, status, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError("Maintenance state broadcast failed: {ExceptionType}", ex.GetType().Name);
                }
            }
        }

        private sealed class ChannelObserver(ChannelWriter<MaintenanceStatusDto> writer) : IObserver<MaintenanceStatusDto>
        {
            public void OnNext(MaintenanceStatusDto value) => _ = writer.TryWrite(value);

            public void OnError(Exception error) => writer.TryComplete(error);

            public void OnCompleted() => writer.TryComplete();
        }
    }
}
