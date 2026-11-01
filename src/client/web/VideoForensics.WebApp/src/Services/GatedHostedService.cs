using Microsoft.Extensions.Hosting;

using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// Holds a background service back until the startup initialization has finished. StartAsync returns at once, so
/// the host can start without waiting; the inner service's StartAsync (and so its ExecuteAsync) runs when the gate
/// opens. A shutdown before then cancels the wait and the inner service never starts.
/// </summary>
public sealed class GatedHostedService : IHostedService
{
    private readonly IHostedService _inner;
    private readonly StartupInitializationGate _gate;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _starting;

    public GatedHostedService(IHostedService inner, StartupInitializationGate gate)
    {
        _inner = inner;
        _gate = gate;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _starting = StartWhenReadyAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        if (_starting is not null)
        {
            try
            {
                await _starting;
            }
            catch (OperationCanceledException)
            {
                // Shut down before the gate opened: the inner service never started, so there is nothing to stop.
            }
        }

        await _inner.StopAsync(cancellationToken);
    }

    private async Task StartWhenReadyAsync(CancellationToken ct)
    {
        await _gate.WhenReadyAsync(ct);
        await _inner.StartAsync(ct);
    }
}
